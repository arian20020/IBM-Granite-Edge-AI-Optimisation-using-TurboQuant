using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Native;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Probes one local GGUF through the matched CPU runtime using LLamaSharp's
/// supported VocabOnly path.
/// </summary>
public sealed class VocabOnlyModelProbe
{
    private readonly ModelFileSnapshotService _snapshotService;

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
    /// Runs one read-only probe with caller-controlled cancellation only.
    /// </summary>
    public Task<VocabOnlyModelProbeResult> RunAsync(
        string modelPath,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            modelPath,
            cancelAfterPreflightMilliseconds: null,
            cancelNativeAfterMilliseconds: null,
            cancellationToken);
    }

    /// <summary>
    /// Runs one read-only probe with an optional timer scoped to native loading.
    /// </summary>
    public Task<VocabOnlyModelProbeResult> RunAsync(
        string modelPath,
        int? cancelNativeAfterMilliseconds,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            modelPath,
            cancelAfterPreflightMilliseconds: null,
            cancelNativeAfterMilliseconds,
            cancellationToken);
    }

    /// <summary>
    /// Runs one read-only probe. The post-preflight timer begins only after the
    /// initial model snapshot exists; the native timer begins immediately before
    /// LLamaSharp model loading. Caller cancellation remains active throughout.
    /// </summary>
    public async Task<VocabOnlyModelProbeResult> RunAsync(
        string modelPath,
        int? cancelAfterPreflightMilliseconds,
        int? cancelNativeAfterMilliseconds,
        CancellationToken cancellationToken)
    {
        ValidatePositiveDelay(
            cancelAfterPreflightMilliseconds,
            nameof(cancelAfterPreflightMilliseconds));
        ValidatePositiveDelay(
            cancelNativeAfterMilliseconds,
            nameof(cancelNativeAfterMilliseconds));

        if (cancelAfterPreflightMilliseconds.HasValue &&
            cancelNativeAfterMilliseconds.HasValue)
        {
            throw new ArgumentException(
                "Post-preflight and native-load cancellation timers are mutually exclusive.");
        }

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
        ProbeFailure? failure = null;

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
            fullModelPath = Path.GetFullPath(modelPath);

            // Caller cancellation is honored during hashing. Timed diagnostic
            // cancellation starts only after this baseline exists so the final
            // result can still prove model preservation.
            beforeSnapshot = await _snapshotService.CaptureAsync(
                fullModelPath,
                cancellationToken);

            using var operationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            if (cancelAfterPreflightMilliseconds.HasValue)
            {
                operationCancellation.CancelAfter(
                    cancelAfterPreflightMilliseconds.Value);
            }

            operationCancellation.Token.ThrowIfCancellationRequested();
            CpuNativeRuntimeConfiguration.Configure(logs);

            bool nativeBackendAvailable =
                NativeLibraryConfig.LLama.DryRun(
                    out INativeLibrary? selectedLibrary);

            operationCancellation.Token.ThrowIfCancellationRequested();

            selectedBackend =
                CpuNativeRuntimeConfiguration.Describe(selectedLibrary);

            if (!nativeBackendAvailable)
            {
                failure = new ProbeFailure(
                    "MI-OP-RUNTIME-UNAVAILABLE",
                    null,
                    "LLamaSharp could not load a published CPU backend.");
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
                using var nativeLoadCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        operationCancellation.Token);

                if (cancelNativeAfterMilliseconds.HasValue)
                {
                    nativeLoadCancellation.CancelAfter(
                        cancelNativeAfterMilliseconds.Value);
                }

                try
                {
                    weights = await LLamaWeights.LoadFromFileAsync(
                        modelParameters,
                        nativeLoadCancellation.Token,
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
            failure = new ProbeFailure(
                "MI-PROBE-CANCELLED",
                exception.GetType().FullName,
                exception.Message);
        }
        catch (Exception exception)
        {
            completionStatus = VocabOnlyProbeCompletionStatus.Failed;
            failure = ProbeFailureMapper.Map(exception);
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

            process.Refresh();
            workingSetAfterDisposeBytes = process.WorkingSet64;
            totalStopwatch.Stop();
        }

        ProbeCompletionResolution resolution = ProbeResultFinalizer.Resolve(
            completionStatus,
            failure,
            integrity,
            integrityErrorType,
            integrityErrorMessage);

        process.Refresh();

        return new VocabOnlyModelProbeResult
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DurationMilliseconds = totalStopwatch.ElapsedMilliseconds,
            CompletionStatus = resolution.Status,
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
            IntegrityVerificationErrorMessage = SensitiveTextRedactor.Redact(
                integrityErrorMessage,
                fullModelPath),
            ModelEvidence = modelEvidence,
            ProgressSamples = progressRecorder.GetSnapshot(),
            LoadDurationMilliseconds = loadDurationMilliseconds,
            WorkingSetBeforeBytes = workingSetBeforeBytes,
            WorkingSetAfterLoadBytes = workingSetAfterLoadBytes,
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            WorkingSetAfterDisposeBytes = workingSetAfterDisposeBytes,
            NativeHandleClosedAfterDispose =
                nativeHandleClosedAfterDispose,
            FailureCode = resolution.Failure?.Code,
            FailureType = resolution.Failure?.Type,
            FailureMessage = SensitiveTextRedactor.Redact(
                resolution.Failure?.Message,
                fullModelPath),
            Logs = SensitiveTextRedactor.RedactLogs(logs, fullModelPath)
        };
    }

    private static void ValidatePositiveDelay(
        int? milliseconds,
        string parameterName)
    {
        if (milliseconds.HasValue && milliseconds.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Cancellation delay must be positive.");
        }
    }
}
