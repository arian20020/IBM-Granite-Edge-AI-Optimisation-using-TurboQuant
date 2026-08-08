using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Adapts the project-owned LLamaSharp VocabOnly probe to the worker protocol
/// without allowing native or diagnostic details across the process boundary.
/// </summary>
internal sealed class LlamaSharpInspectionEngine(
    IVocabOnlyModelProbe probe,
    string workerVersion) : IWorkerInspectionEngine
{
    private const string GenericFailureCode =
        "MI-OP-RUNTIME-INSPECTION-FAILED";
    private const string GenericFailureMessage =
        "The model inspection runtime could not produce reliable evidence.";

    private readonly IVocabOnlyModelProbe _probe = probe ??
        throw new ArgumentNullException(nameof(probe));
    private readonly string _workerVersion =
        !string.IsNullOrWhiteSpace(workerVersion)
            ? workerVersion
            : throw new ArgumentException(
                "The worker version must not be empty.",
                nameof(workerVersion));

    public async Task<WorkerEngineResult> InspectAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = new InspectionProgressState(command.RequestId, progress);
        state.Begin();

        try
        {
            VocabOnlyModelProbeResult runtimeResult = await _probe.RunAsync(
                    new VocabOnlyProbeRequest(
                        command.ModelPath,
                        command.ExpectedFileIdentity.LengthBytes,
                        command.ExpectedFileIdentity.LastWriteTimeUtc),
                    new RuntimeProgressAdapter(state),
                    cancellationToken)
                .ConfigureAwait(false);

            if (runtimeResult is null)
            {
                throw new InvalidDataException(
                    "The runtime returned no inspection result.");
            }

            WorkerEngineResult? integrityFailure =
                ResolveIntegrityPrecedence(runtimeResult);
            if (integrityFailure is not null)
            {
                state.Finish(WorkerStageStatus.Failed);
                return integrityFailure;
            }

            switch (runtimeResult.CompletionStatus)
            {
                case VocabOnlyProbeCompletionStatus.Cancelled:
                    state.Finish(WorkerStageStatus.Cancelled);
                    return WorkerEngineResult.Cancelled();

                case VocabOnlyProbeCompletionStatus.Failed:
                    state.Finish(WorkerStageStatus.Failed);
                    return MapFailure(runtimeResult.FailureCode);

                case VocabOnlyProbeCompletionStatus.Succeeded:
                    state.CompleteReadStage();
                    state.CompleteSimpleStage(
                        WorkerStage.ValidateTokenizerAndChatSetup);
                    state.CompleteSimpleStage(
                        WorkerStage.ValidateModelStructure);
                    state.BeginRuntimeStage();

                    WorkerInspectionEvidence evidence =
                        LlamaSharpInspectionEvidenceMapper.Map(
                            runtimeResult,
                            command,
                            _workerVersion);
                    evidence.Validate();
                    state.CompleteRuntimeStage();
                    return WorkerEngineResult.Completed(evidence);

                default:
                    throw new InvalidDataException(
                        "The runtime returned an unknown completion status.");
            }
        }
        catch
        {
            state.Finish(WorkerStageStatus.Failed);
            return WorkerEngineResult.ControlledFailure(
                GenericFailureCode,
                GenericFailureMessage);
        }
    }

    private static WorkerEngineResult? ResolveIntegrityPrecedence(
        VocabOnlyModelProbeResult result)
    {
        if (result.CompletionStatus is not
            (VocabOnlyProbeCompletionStatus.Succeeded or
             VocabOnlyProbeCompletionStatus.Cancelled))
        {
            return null;
        }

        if (result.Integrity is null ||
            result.BeforeSnapshot is null ||
            result.AfterSnapshot is null)
        {
            return WorkerEngineResult.ControlledFailure(
                "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED",
                "The selected model integrity could not be verified.");
        }

        if (!result.Integrity.IsPreserved)
        {
            return WorkerEngineResult.ControlledFailure(
                "MI-OP-MODEL-INTEGRITY-CHANGED",
                "The selected model changed during inspection.");
        }

        return null;
    }

    private static WorkerEngineResult MapFailure(string? code) => code switch
    {
        "MI-OP-MODEL-CONTINUITY-MISMATCH" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model changed after it was prepared for inspection."),
        "MI-OP-MODEL-FILE-NOT-FOUND" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model file was not found."),
        "MI-OP-MODEL-FILE-ACCESS-DENIED" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model file could not be read."),
        "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The native model inspection runtime is incompatible with this process."),
        "MI-OP-RUNTIME-UNAVAILABLE" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The native model inspection runtime is unavailable."),
        "MI-PROBE-MODEL-LOAD-FAILED" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model could not be opened for inspection."),
        "MI-OP-MODEL-FILE-IO" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model file could not be read reliably."),
        "MI-OP-MODEL-INTEGRITY-CHANGED" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model changed during inspection."),
        "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED" =>
            WorkerEngineResult.ControlledFailure(
                code,
                "The selected model integrity could not be verified."),
        GenericFailureCode =>
            WorkerEngineResult.ControlledFailure(
                GenericFailureCode,
                GenericFailureMessage),
        _ => WorkerEngineResult.ControlledFailure(
            GenericFailureCode,
            GenericFailureMessage)
    };

    private sealed class RuntimeProgressAdapter(InspectionProgressState state) :
        IProgress<VocabOnlyProbeProgress>
    {
        public void Report(VocabOnlyProbeProgress value) =>
            state.ReportRuntimeProgress(value);
    }

    private sealed class InspectionProgressState(
        Guid requestId,
        IProgress<WorkerProgressMessage>? progress)
    {
        private readonly object _sync = new();
        private WorkerStage _stage = WorkerStage.CheckModelPackage;
        private int _completedStageCount;
        private bool _packageValidated;
        private bool _finished;

        internal void Begin()
        {
            lock (_sync)
            {
                Report(WorkerStageStatus.Active, stageFraction: null);
            }
        }

        internal void ReportRuntimeProgress(VocabOnlyProbeProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);

            lock (_sync)
            {
                ReportRuntimeProgressCore(value);
            }
        }

        private void ReportRuntimeProgressCore(VocabOnlyProbeProgress value)
        {
            bool isPackageCheckpoint =
                value.PackageValidated && value.NativeFraction is null;
            bool isNativeFraction =
                !value.PackageValidated &&
                value.NativeFraction is float fraction &&
                float.IsFinite(fraction) &&
                fraction >= 0f &&
                fraction <= 1f;

            if (isPackageCheckpoint)
            {
                if (_packageValidated ||
                    _stage != WorkerStage.CheckModelPackage)
                {
                    throw new InvalidDataException(
                        "The runtime reported an invalid package checkpoint.");
                }

                _packageValidated = true;
                _completedStageCount = 1;
                Report(WorkerStageStatus.Completed, stageFraction: null);
                _stage = WorkerStage.ReadModelConfiguration;
                Report(WorkerStageStatus.Active, stageFraction: null);
                return;
            }

            if (isNativeFraction)
            {
                if (!_packageValidated ||
                    _stage != WorkerStage.ReadModelConfiguration)
                {
                    throw new InvalidDataException(
                        "The runtime reported native progress before package validation.");
                }

                Report(
                    WorkerStageStatus.Active,
                    value.NativeFraction!.Value);
                return;
            }

            throw new InvalidDataException(
                "The runtime reported an impossible progress fact.");
        }

        internal void CompleteReadStage()
        {
            lock (_sync)
            {
                if (!_packageValidated ||
                    _stage != WorkerStage.ReadModelConfiguration)
                {
                    throw new InvalidDataException(
                        "The runtime completed without its package checkpoint.");
                }

                _completedStageCount = 2;
                Report(WorkerStageStatus.Completed, stageFraction: null);
            }
        }

        internal void CompleteSimpleStage(WorkerStage stage)
        {
            lock (_sync)
            {
                _stage = stage;
                Report(WorkerStageStatus.Active, stageFraction: null);
                _completedStageCount++;
                Report(WorkerStageStatus.Completed, stageFraction: null);
            }
        }

        internal void BeginRuntimeStage()
        {
            lock (_sync)
            {
                _stage = WorkerStage.ConfirmCoreRuntimeCompatibility;
                Report(WorkerStageStatus.Active, stageFraction: null);
            }
        }

        internal void CompleteRuntimeStage()
        {
            lock (_sync)
            {
                _completedStageCount = 5;
                Report(WorkerStageStatus.Completed, stageFraction: null);
                _finished = true;
            }
        }

        internal void Finish(WorkerStageStatus status)
        {
            lock (_sync)
            {
                if (_finished)
                {
                    return;
                }

                Report(status, stageFraction: null);
                _finished = true;
            }
        }

        private void Report(
            WorkerStageStatus status,
            double? stageFraction)
        {
            var message = new WorkerProgressMessage
            {
                ProtocolVersion = WorkerProtocol.Version,
                MessageType = WorkerMessageKind.Progress,
                RequestId = requestId,
                Stage = _stage,
                StageStatus = status,
                CompletedStageCount = _completedStageCount,
                TotalStageCount = 5,
                StageFraction = stageFraction
            };
            message.Validate();
            progress?.Report(message);
        }
    }
}
