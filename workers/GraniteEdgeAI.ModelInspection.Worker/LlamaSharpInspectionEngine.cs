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

            if (state.HasInvalidProbeTransition)
            {
                throw new InvalidOperationException(
                    "The runtime reported an invalid probe phase transition.");
            }

            if (runtimeResult is null)
            {
                throw new InvalidDataException(
                    "The runtime returned no inspection result.");
            }

            WorkerEngineResult? integrityFailure =
                ResolveIntegrityPrecedence(runtimeResult);
            if (integrityFailure is not null)
            {
                if (!state.Finish(WorkerStageStatus.Failed))
                {
                    throw new InvalidOperationException(
                        "The progress observer rejected a terminal update.");
                }

                return integrityFailure;
            }

            switch (runtimeResult.CompletionStatus)
            {
                case VocabOnlyProbeCompletionStatus.Cancelled:
                    if (!state.Finish(WorkerStageStatus.Cancelled))
                    {
                        throw new InvalidOperationException(
                            "The progress observer rejected a terminal update.");
                    }

                    return WorkerEngineResult.Cancelled();

                case VocabOnlyProbeCompletionStatus.Failed:
                    if (!state.Finish(WorkerStageStatus.Failed))
                    {
                        throw new InvalidOperationException(
                            "The progress observer rejected a terminal update.");
                    }

                    return MapFailure(runtimeResult.FailureCode);

                case VocabOnlyProbeCompletionStatus.Succeeded:
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
            state.AcceptProbeFact(value);
    }

    private sealed class InspectionProgressState(
        Guid requestId,
        IProgress<WorkerProgressMessage>? progress)
    {
        private static readonly (
            VocabOnlyProbePhase Phase,
            VocabOnlyProbePhaseStatus Status,
            WorkerStage Stage)[] ProbeTransitions =
        [
            (
                VocabOnlyProbePhase.CheckModelPackage,
                VocabOnlyProbePhaseStatus.Active,
                WorkerStage.CheckModelPackage),
            (
                VocabOnlyProbePhase.CheckModelPackage,
                VocabOnlyProbePhaseStatus.Completed,
                WorkerStage.CheckModelPackage),
            (
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Active,
                WorkerStage.ReadModelConfiguration),
            (
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Completed,
                WorkerStage.ReadModelConfiguration),
            (
                VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                VocabOnlyProbePhaseStatus.Active,
                WorkerStage.ValidateTokenizerAndChatSetup),
            (
                VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                VocabOnlyProbePhaseStatus.Completed,
                WorkerStage.ValidateTokenizerAndChatSetup),
            (
                VocabOnlyProbePhase.ValidateModelStructure,
                VocabOnlyProbePhaseStatus.Active,
                WorkerStage.ValidateModelStructure),
            (
                VocabOnlyProbePhase.ValidateModelStructure,
                VocabOnlyProbePhaseStatus.Completed,
                WorkerStage.ValidateModelStructure)
        ];

        private readonly object _sync = new();
        private int _nextTransitionIndex;
        private int _completedStageCount;
        private bool _invalid;
        private bool _progressObserverFailed;
        private bool _runtimeStageActive;
        private bool _finished;

        internal bool HasInvalidProbeTransition
        {
            get
            {
                lock (_sync)
                {
                    return _invalid;
                }
            }
        }

        internal void AcceptProbeFact(VocabOnlyProbeProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);

            lock (_sync)
            {
                AcceptProbeFactCore(value);
            }
        }

        private void AcceptProbeFactCore(VocabOnlyProbeProgress value)
        {
            if (_invalid || _finished)
            {
                RejectProbeFact();
            }

            if (value.Status == VocabOnlyProbePhaseStatus.Fraction)
            {
                float fraction = value.NativeFraction ?? float.NaN;

                if (_nextTransitionIndex != 3 ||
                    value.Phase !=
                        VocabOnlyProbePhase.ReadModelConfiguration ||
                    !float.IsFinite(fraction) ||
                    fraction < 0f ||
                    fraction > 1f)
                {
                    RejectProbeFact();
                }

                if (!TryReport(
                        WorkerStage.ReadModelConfiguration,
                        WorkerStageStatus.Active,
                        fraction,
                        _completedStageCount))
                {
                    RejectProbeFact();
                }

                return;
            }

            if (_nextTransitionIndex >= ProbeTransitions.Length)
            {
                RejectProbeFact();
            }

            (
                VocabOnlyProbePhase expectedPhase,
                VocabOnlyProbePhaseStatus expectedStatus,
                WorkerStage stage) =
                ProbeTransitions[_nextTransitionIndex];

            if (value.Phase != expectedPhase ||
                value.Status != expectedStatus ||
                value.NativeFraction is not null)
            {
                RejectProbeFact();
            }

            int nextCompletedStageCount = _completedStageCount;
            if (expectedStatus == VocabOnlyProbePhaseStatus.Completed)
            {
                nextCompletedStageCount++;
            }

            if (!TryReport(
                    stage,
                    expectedStatus == VocabOnlyProbePhaseStatus.Active
                        ? WorkerStageStatus.Active
                        : WorkerStageStatus.Completed,
                    stageFraction: null,
                    nextCompletedStageCount))
            {
                RejectProbeFact();
            }

            _completedStageCount = nextCompletedStageCount;
            _nextTransitionIndex++;
        }

        internal void BeginRuntimeStage()
        {
            lock (_sync)
            {
                if (_invalid ||
                    _finished ||
                    _runtimeStageActive ||
                    _nextTransitionIndex != ProbeTransitions.Length ||
                    _completedStageCount != 4)
                {
                    RejectProbeFact();
                }

                if (!TryReport(
                        WorkerStage.ConfirmCoreRuntimeCompatibility,
                        WorkerStageStatus.Active,
                        stageFraction: null,
                        _completedStageCount))
                {
                    RejectProbeFact();
                }

                _runtimeStageActive = true;
            }
        }

        internal void CompleteRuntimeStage()
        {
            lock (_sync)
            {
                if (_invalid ||
                    _finished ||
                    !_runtimeStageActive ||
                    _completedStageCount != 4)
                {
                    RejectProbeFact();
                }

                if (!TryReport(
                        WorkerStage.ConfirmCoreRuntimeCompatibility,
                        WorkerStageStatus.Completed,
                        stageFraction: null,
                        completedStageCount: 5))
                {
                    RejectProbeFact();
                }

                _completedStageCount = 5;
                _runtimeStageActive = false;
                _finished = true;
            }
        }

        internal bool Finish(WorkerStageStatus status)
        {
            lock (_sync)
            {
                if (_finished)
                {
                    return !_progressObserverFailed;
                }

                int stageNumber = Math.Clamp(
                    _completedStageCount + 1,
                    (int)WorkerStage.CheckModelPackage,
                    (int)WorkerStage.ConfirmCoreRuntimeCompatibility);
                bool reported;

                try
                {
                    reported = TryReport(
                        (WorkerStage)stageNumber,
                        status,
                        stageFraction: null,
                        _completedStageCount);
                }
                finally
                {
                    _runtimeStageActive = false;
                    _finished = true;
                }

                return reported && !_progressObserverFailed;
            }
        }

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private void RejectProbeFact()
        {
            _invalid = true;
            throw new InvalidDataException(
                "The runtime reported an invalid probe phase transition.");
        }

        private bool TryReport(
            WorkerStage stage,
            WorkerStageStatus status,
            double? stageFraction,
            int completedStageCount)
        {
            var message = new WorkerProgressMessage
            {
                ProtocolVersion = WorkerProtocol.Version,
                MessageType = WorkerMessageKind.Progress,
                RequestId = requestId,
                Stage = stage,
                StageStatus = status,
                CompletedStageCount = completedStageCount,
                TotalStageCount = 5,
                StageFraction = stageFraction
            };
            message.Validate();

            try
            {
                progress?.Report(message);
                return true;
            }
            catch
            {
                _progressObserverFailed = true;
                _invalid = true;
                return false;
            }
        }
    }
}
