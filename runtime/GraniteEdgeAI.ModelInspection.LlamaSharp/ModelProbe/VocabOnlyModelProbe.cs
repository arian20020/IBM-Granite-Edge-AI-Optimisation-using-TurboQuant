using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using GraniteEdgeAI.ModelInspection.LlamaSharp;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Native;

namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Probes one local GGUF through the matched CPU runtime using LLamaSharp's
/// supported VocabOnly path.
/// </summary>
public sealed class VocabOnlyModelProbe : IVocabOnlyModelProbe
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
    public Task<VocabOnlyModelProbeResult> RunAsync(
        string modelPath,
        int? cancelAfterPreflightMilliseconds,
        int? cancelNativeAfterMilliseconds,
        CancellationToken cancellationToken)
    {
        return RunCoreAsync(
            modelPath,
            expectedIdentity: null,
            progress: null,
            cancelAfterPreflightMilliseconds,
            cancelNativeAfterMilliseconds,
            cancellationToken);
    }

    public Task<VocabOnlyModelProbeResult> RunAsync(
        VocabOnlyProbeRequest request,
        IProgress<VocabOnlyProbeProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RunCoreAsync(
            request.ModelPath,
            request,
            progress,
            cancelAfterPreflightMilliseconds: null,
            cancelNativeAfterMilliseconds: null,
            cancellationToken);
    }

    private async Task<VocabOnlyModelProbeResult> RunCoreAsync(
        string modelPath,
        VocabOnlyProbeRequest? expectedIdentity,
        IProgress<VocabOnlyProbeProgress>? progress,
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
        var phaseSequence = new VocabOnlyProbePhaseSequence(progress);
        var progressRecorder = new NativeLoadProgressRecorder(phaseSequence);
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
        LLamaWeights? weights = null;
        bool integrityAttempted = false;

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
            fullModelPath = Path.GetFullPath(modelPath);

            // Caller cancellation is honored during hashing. Timed diagnostic
            // cancellation starts only after this baseline exists so the final
            // result can still prove model preservation.
            await phaseSequence.RunAsync(
                    VocabOnlyProbePhase.CheckModelPackage,
                    async () =>
                    {
                        beforeSnapshot = expectedIdentity is null
                            ? await _snapshotService.CaptureAsync(
                                    fullModelPath,
                                    cancellationToken)
                                .ConfigureAwait(false)
                            : await _snapshotService.CaptureAsync(
                                    expectedIdentity with
                                    {
                                        ModelPath = fullModelPath
                                    },
                                    cancellationToken)
                                .ConfigureAwait(false);
                        return true;
                    })
                .ConfigureAwait(false);
            ModelFileSnapshot initialSnapshot = beforeSnapshot ??
                throw new InvalidDataException(
                    "The runtime returned no initial model snapshot.");

            using var operationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            if (cancelAfterPreflightMilliseconds.HasValue)
            {
                operationCancellation.CancelAfter(
                    cancelAfterPreflightMilliseconds.Value);
            }

            operationCancellation.Token.ThrowIfCancellationRequested();

            try
            {
                VocabOnlyConfigurationProjection configuration =
                    await phaseSequence.RunAsync(
                            VocabOnlyProbePhase.ReadModelConfiguration,
                            async () =>
                            {
                                operationCancellation.Token.ThrowIfCancellationRequested();
                                CpuNativeRuntimeConfiguration.Configure(logs);

                                bool nativeBackendAvailable =
                                    NativeLibraryConfig.LLama.DryRun(
                                        out INativeLibrary? selectedLibrary);

                                operationCancellation.Token.ThrowIfCancellationRequested();
                                selectedBackend =
                                    CpuNativeRuntimeConfiguration.Describe(
                                        selectedLibrary);

                                if (!nativeBackendAvailable)
                                {
                                    failure = new ProbeFailure(
                                        "MI-OP-RUNTIME-UNAVAILABLE",
                                        null,
                                        "LLamaSharp could not load a published CPU backend.");
                                    throw new NativeBackendUnavailableException();
                                }

                                var modelParameters = new ModelParams(
                                    fullModelPath)
                                {
                                    VocabOnly = true,
                                    GpuLayerCount = 0,
                                    UseMemorymap = true,
                                    UseMemoryLock = false
                                };
                                var loadStopwatch = Stopwatch.StartNew();
                                using var nativeLoadCancellation =
                                    CancellationTokenSource
                                        .CreateLinkedTokenSource(
                                            operationCancellation.Token);

                                if (cancelNativeAfterMilliseconds.HasValue)
                                {
                                    nativeLoadCancellation.CancelAfter(
                                        cancelNativeAfterMilliseconds.Value);
                                }

                                try
                                {
                                    weights = await LLamaWeights
                                        .LoadFromFileAsync(
                                            modelParameters,
                                            nativeLoadCancellation.Token,
                                            progressRecorder)
                                        .ConfigureAwait(false);
                                    nativeLoadCancellation.Token.ThrowIfCancellationRequested();

                                    loadStopwatch.Stop();
                                    loadDurationMilliseconds =
                                        loadStopwatch.ElapsedMilliseconds;

                                    process.Refresh();
                                    workingSetAfterLoadBytes =
                                        process.WorkingSet64;

                                    VocabOnlyConfigurationProjection projection =
                                        VocabOnlyEvidenceCollector
                                            .CollectConfiguration(weights);
                                    nativeLoadCancellation.Token.ThrowIfCancellationRequested();
                                    return projection;
                                }
                                finally
                                {
                                    loadStopwatch.Stop();
                                }
                            })
                        .ConfigureAwait(false);
                operationCancellation.Token.ThrowIfCancellationRequested();

                LLamaWeights loadedWeights = weights ??
                    throw new InvalidDataException(
                        "The runtime returned no loaded model handle.");
                VocabOnlyTokenizerProjection tokenizer =
                    phaseSequence.Run(
                        VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                        () =>
                        {
                            operationCancellation.Token.ThrowIfCancellationRequested();
                            VocabOnlyTokenizerProjection projection =
                                VocabOnlyEvidenceCollector.CollectTokenizerAndChat(
                                    loadedWeights);
                            operationCancellation.Token.ThrowIfCancellationRequested();
                            return projection;
                        });
                operationCancellation.Token.ThrowIfCancellationRequested();

                await phaseSequence.RunAsync(
                        VocabOnlyProbePhase.ValidateModelStructure,
                        async () =>
                        {
                            ExceptionDispatchInfo? stageFailure = null;
                            bool integrityVerified = false;

                            try
                            {
                                try
                                {
                                    operationCancellation.Token.ThrowIfCancellationRequested();
                                    VocabOnlyStructureProjection structure =
                                        VocabOnlyEvidenceCollector.CollectStructure(
                                            loadedWeights);
                                    modelEvidence =
                                        VocabOnlyEvidenceCollector.Compose(
                                            configuration,
                                            tokenizer,
                                            structure);
                                    operationCancellation.Token.ThrowIfCancellationRequested();
                                }
                                catch (Exception exception)
                                {
                                    stageFailure =
                                        ExceptionDispatchInfo.Capture(exception);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    loadedWeights.Dispose();
                                    weights = null;
                                    nativeHandleClosedAfterDispose =
                                        loadedWeights.NativeHandle.IsClosed;
                                }
                                catch (Exception exception)
                                {
                                    stageFailure ??=
                                        ExceptionDispatchInfo.Capture(exception);
                                }

                                integrityAttempted = true;

                                if (!File.Exists(fullModelPath))
                                {
                                    integrityErrorType =
                                        typeof(FileNotFoundException).FullName;
                                    integrityErrorMessage =
                                        "The selected model no longer exists after inspection.";
                                }
                                else
                                {
                                    try
                                    {
                                        afterSnapshot = await _snapshotService.CaptureAsync(
                                                fullModelPath,
                                                CancellationToken.None)
                                            .ConfigureAwait(false);
                                        integrity =
                                            ModelFileIntegrityComparison.Compare(
                                                initialSnapshot,
                                                afterSnapshot);
                                        integrityVerified = true;
                                    }
                                    catch (Exception exception)
                                    {
                                        integrityErrorType =
                                            exception.GetType().FullName;
                                        integrityErrorMessage =
                                            exception.Message;
                                    }
                                }
                            }

                            try
                            {
                                process.Refresh();
                                workingSetAfterDisposeBytes =
                                    process.WorkingSet64;
                            }
                            catch (Exception exception)
                            {
                                stageFailure ??=
                                    ExceptionDispatchInfo.Capture(exception);
                            }

                            if (stageFailure is not null)
                            {
                                stageFailure.Throw();
                            }

                            operationCancellation.Token.ThrowIfCancellationRequested();

                            if (!integrityVerified ||
                                integrity is null ||
                                !integrity.IsPreserved)
                            {
                                throw new FinalIntegrityVerificationException();
                            }

                            return true;
                        })
                    .ConfigureAwait(false);
                operationCancellation.Token.ThrowIfCancellationRequested();

                completionStatus = VocabOnlyProbeCompletionStatus.Succeeded;
            }
            finally
            {
                if (weights is not null)
                {
                    LLamaWeights abandonedWeights = weights;
                    abandonedWeights.Dispose();
                    weights = null;
                    nativeHandleClosedAfterDispose =
                        abandonedWeights.NativeHandle.IsClosed;
                }

                process.Refresh();
                workingSetAfterDisposeBytes = process.WorkingSet64;
            }
        }
        catch (NativeBackendUnavailableException)
        {
            completionStatus = VocabOnlyProbeCompletionStatus.Failed;
        }
        catch (FinalIntegrityVerificationException)
        {
            completionStatus = VocabOnlyProbeCompletionStatus.Succeeded;
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
            if (!integrityAttempted &&
                beforeSnapshot is not null &&
                !string.IsNullOrWhiteSpace(fullModelPath) &&
                File.Exists(fullModelPath))
            {
                integrityAttempted = true;

                try
                {
                    afterSnapshot = await _snapshotService.CaptureAsync(
                        fullModelPath,
                        CancellationToken.None);
                    integrity = ModelFileIntegrityComparison.Compare(
                        beforeSnapshot,
                        afterSnapshot);
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

    private sealed class NativeBackendUnavailableException : Exception
    {
    }

    private sealed class FinalIntegrityVerificationException : Exception
    {
    }
}
