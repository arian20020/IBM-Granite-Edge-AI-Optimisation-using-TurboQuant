using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Exceptions;
using LLama.Native;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Probes one local GGUF through the matched CPU runtime using LLamaSharp's
/// supported VocabOnly path.
/// </summary>
public sealed class VocabOnlyModelProbe
{
    private readonly ModelFileSnapshotService _snapshotService;

    /// <summary>
    /// Creates the probe with the production read-only snapshot service.
    /// </summary>
    public VocabOnlyModelProbe()
        : this(new ModelFileSnapshotService())
    {
    }

    internal VocabOnlyModelProbe(
        ModelFileSnapshotService snapshotService)
    {
        _snapshotService = snapshotService ??
            throw new ArgumentNullException(nameof(snapshotService));
    }

    /// <summary>
    /// Runs one read-only VocabOnly probe and returns ordinary project-owned
    /// evidence.
    /// </summary>
    public async Task<VocabOnlyModelProbeResult> RunAsync(
        string modelPath,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        var totalStopwatch = Stopwatch.StartNew();
        var logs = new ConcurrentQueue<NativeBackendLogEntry>();
        var progressRecorder = new NativeLoadProgressRecorder();
        using Process process = Process.GetCurrentProcess();

        process.Refresh();
        long workingSetBeforeBytes = process.WorkingSet64;

        string? fullModelPath = null;
        ModelFileSnapshot? beforeSnapshot = null;
        ModelFileSnapshot? afterSnapshot = null;
        ModelFileIntegrityComparison? integrity = null;
        string? integrityErrorType = null;
        string? integrityErrorMessage = null;
        SelectedNativeBackend? selectedBackend = null;
        VocabOnlyRuntimeModelEvidence? modelEvidence = null;
        long? loadDurationMilliseconds = null;
        long? workingSetAfterLoadBytes = null;
        long workingSetAfterDisposeBytes = workingSetBeforeBytes;
        bool? nativeHandleClosedAfterDispose = null;
        VocabOnlyProbeCompletionStatus completionStatus =
            VocabOnlyProbeCompletionStatus.Failed;
        string? failureCode = null;
        string? failureType = null;
        string? failureMessage = null;

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
            fullModelPath = Path.GetFullPath(modelPath);

            beforeSnapshot = await _snapshotService.CaptureAsync(
                fullModelPath,
                cancellationToken);

            CpuNativeRuntimeConfiguration.Configure(logs);

            bool nativeBackendAvailable =
                NativeLibraryConfig.LLama.DryRun(
                    out INativeLibrary? selectedLibrary);

            selectedBackend =
                CpuNativeRuntimeConfiguration.Describe(selectedLibrary);

            if (!nativeBackendAvailable)
            {
                failureCode = "MI-OP-RUNTIME-UNAVAILABLE";
                failureMessage =
                    "LLamaSharp could not load a published CPU backend.";
            }
            else
            {
                var modelParameters = new ModelParams(fullModelPath)
                {
                    VocabOnly = true,
                    GpuLayerCount = 0,
                    UseMemorymap = true,
                    UseMemoryLock = false
                };

                LLamaWeights? weights = null;
                var loadStopwatch = Stopwatch.StartNew();

                try
                {
                    weights = await LLamaWeights.LoadFromFileAsync(
                        modelParameters,
                        cancellationToken,
                        progressRecorder);

                    loadStopwatch.Stop();
                    loadDurationMilliseconds =
                        loadStopwatch.ElapsedMilliseconds;

                    process.Refresh();
                    workingSetAfterLoadBytes = process.WorkingSet64;

                    modelEvidence =
                        VocabOnlyEvidenceCollector.Collect(weights);

                    completionStatus =
                        VocabOnlyProbeCompletionStatus.Succeeded;
                }
                finally
                {
                    loadStopwatch.Stop();

                    if (weights is not null)
                    {
                        weights.Dispose();
                        nativeHandleClosedAfterDispose =
                            weights.NativeHandle.IsClosed;
                    }

                    process.Refresh();
                    workingSetAfterDisposeBytes = process.WorkingSet64;
                }
            }
        }
        catch (OperationCanceledException exception)
        {
            completionStatus =
                VocabOnlyProbeCompletionStatus.Cancelled;
            failureCode = "MI-PROBE-CANCELLED";
            failureType = exception.GetType().FullName;
            failureMessage = exception.Message;
        }
        catch (Exception exception)
        {
            completionStatus = VocabOnlyProbeCompletionStatus.Failed;

            (failureCode, failureType, failureMessage) =
                MapFailure(exception);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(fullModelPath) &&
                File.Exists(fullModelPath))
            {
                try
                {
                    afterSnapshot = await _snapshotService.CaptureAsync(
                        fullModelPath,
                        CancellationToken.None);

                    if (beforeSnapshot is not null)
                    {
                        integrity = ModelFileIntegrityComparison.Compare(
                            beforeSnapshot,
                            afterSnapshot);
                    }
                }
                catch (Exception exception)
                {
                    integrityErrorType = exception.GetType().FullName;
                    integrityErrorMessage = exception.Message;
                }
            }

            if (completionStatus ==
                    VocabOnlyProbeCompletionStatus.Succeeded &&
                integrity is not null &&
                !integrity.IsPreserved)
            {
                completionStatus = VocabOnlyProbeCompletionStatus.Failed;
                failureCode = "MI-OP-MODEL-INTEGRITY-CHANGED";
                failureType = typeof(IOException).FullName;
                failureMessage =
                    "The selected model changed during the VocabOnly probe.";
            }
            else if (completionStatus ==
                         VocabOnlyProbeCompletionStatus.Succeeded &&
                     integrity is null)
            {
                completionStatus = VocabOnlyProbeCompletionStatus.Failed;
                failureCode =
                    "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED";
                failureType = integrityErrorType;
                failureMessage = integrityErrorMessage ??
                    "Post-probe model integrity could not be verified.";
            }

            process.Refresh();
            workingSetAfterDisposeBytes = process.WorkingSet64;
            totalStopwatch.Stop();
        }

        process.Refresh();

        return new VocabOnlyModelProbeResult
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DurationMilliseconds = totalStopwatch.ElapsedMilliseconds,
            CompletionStatus = completionStatus,
            GpuLayerCount = 0,
            ManagedPackageName =
                PinnedApplicationRuntime.ManagedPackageName,
            ManagedPackageVersion =
                PinnedApplicationRuntime.ManagedPackageVersion,
            BackendPackageName =
                PinnedApplicationRuntime.BackendPackageName,
            BackendPackageVersion =
                PinnedApplicationRuntime.BackendPackageVersion,
            LlamaSharpSourceTag =
                PinnedApplicationRuntime.LlamaSharpSourceTag,
            LlamaSharpReleaseCommit =
                PinnedApplicationRuntime.LlamaSharpReleaseCommit,
            ExpectedLlamaCppCommit =
                PinnedApplicationRuntime.ExpectedLlamaCppCommit,
            IntendedProductionRuntimeIdentifier =
                PinnedApplicationRuntime
                    .IntendedProductionRuntimeIdentifier,
            ProcessArchitecture =
                RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            FrameworkDescription =
                RuntimeInformation.FrameworkDescription,
            SelectedBackend = selectedBackend,
            BeforeSnapshot = beforeSnapshot,
            AfterSnapshot = afterSnapshot,
            Integrity = integrity,
            IntegrityVerificationErrorType = integrityErrorType,
            IntegrityVerificationErrorMessage = integrityErrorMessage,
            ModelEvidence = modelEvidence,
            ProgressSamples = progressRecorder.GetSnapshot(),
            LoadDurationMilliseconds = loadDurationMilliseconds,
            WorkingSetBeforeBytes = workingSetBeforeBytes,
            WorkingSetAfterLoadBytes = workingSetAfterLoadBytes,
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            WorkingSetAfterDisposeBytes = workingSetAfterDisposeBytes,
            NativeHandleClosedAfterDispose =
                nativeHandleClosedAfterDispose,
            FailureCode = failureCode,
            FailureType = failureType,
            FailureMessage = failureMessage,
            Logs = logs.ToArray()
        };
    }

    private static (string Code, string? Type, string Message)
        MapFailure(Exception exception)
    {
        string? type = exception.GetType().FullName;

        return exception switch
        {
            FileNotFoundException =>
                ("MI-OP-MODEL-FILE-NOT-FOUND", type, exception.Message),

            UnauthorizedAccessException =>
                ("MI-OP-MODEL-FILE-ACCESS-DENIED", type, exception.Message),

            BadImageFormatException =>
                ("MI-OP-RUNTIME-ARCHITECTURE-MISMATCH", type, exception.Message),

            DllNotFoundException =>
                ("MI-OP-RUNTIME-UNAVAILABLE", type, exception.Message),

            LoadWeightsFailedException =>
                ("MI-PROBE-MODEL-LOAD-FAILED", type, exception.Message),

            IOException =>
                ("MI-OP-MODEL-FILE-IO", type, exception.Message),

            TypeInitializationException
                when exception.InnerException is DllNotFoundException =>
                ("MI-OP-RUNTIME-UNAVAILABLE",
                    exception.InnerException.GetType().FullName,
                    exception.InnerException.Message),

            TypeInitializationException
                when exception.InnerException is BadImageFormatException =>
                ("MI-OP-RUNTIME-ARCHITECTURE-MISMATCH",
                    exception.InnerException.GetType().FullName,
                    exception.InnerException.Message),

            _ =>
                ("MI-OP-RUNTIME-INSPECTION-FAILED", type, exception.Message)
        };
    }
}
