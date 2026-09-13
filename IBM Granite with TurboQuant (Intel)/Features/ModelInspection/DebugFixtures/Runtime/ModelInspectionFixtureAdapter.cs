#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using WorkerProtocol = GraniteEdgeAI.ModelInspection.Contracts.WorkerProtocol;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;

internal static class ModelInspectionFixtureAdapter
{
    private const string SyntheticRoot = @"C:\GraniteEdgeAI-Fixtures\";
    private const long FixedLengthBytes = 1_610_612_736L;
    private const string FixedCanonicalPathSha256 =
        "9a7c48097a6fc38d061270f2bff446a24b24cb4732662420828ab45e5a4c56fe";
    private const string FixedMetadataSha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FixedModelSha256 =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string FixedLlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";
    private const string SupportedWarningCode =
        "MI-WARN-CHAT-TEMPLATE-MISSING";
    private const string VerifiedConversionRoute =
        "gguf-conversion-route-v1";
    private const int MaximumDetailLength = 512;

    private static readonly DateTimeOffset FixedUtc = new(
        2026,
        8,
        10,
        12,
        0,
        0,
        TimeSpan.Zero);

    private static readonly string MaximumProgressDetail =
        CreateMaximumDetail(
            "Inspection progress detail remains bounded for deterministic maximum-copy validation. ");

    private static readonly string MaximumFailureDetail =
        CreateMaximumDetail(
            "Operational failure detail remains bounded for deterministic maximum-copy validation. ");

    internal static ModelInspectionFixtureExecutionPlan CreatePlan(
        ValidatedModelInspectionFixtureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ModelInspectionRequest request = CreateRequest(input.Request);
        ImmutableArray<ModelInspectionFixtureAttemptPlan>.Builder attempts =
            ImmutableArray.CreateBuilder<ModelInspectionFixtureAttemptPlan>(
                input.Attempts.Count);
        ImmutableArray<ModelInspectionFixtureDeferredEvent>.Builder deferred =
            ImmutableArray.CreateBuilder<ModelInspectionFixtureDeferredEvent>();

        foreach (ModelInspectionFixtureAttemptDescriptor sourceAttempt in
                 input.Attempts)
        {
            ImmutableArray<ModelInspectionFixtureServiceStepPlan>.Builder steps =
                ImmutableArray.CreateBuilder<ModelInspectionFixtureServiceStepPlan>(
                    sourceAttempt.ServiceSteps.Count);
            foreach (ModelInspectionFixtureServiceStepDescriptor sourceStep in
                     sourceAttempt.ServiceSteps)
            {
                ModelInspectionFixtureServiceStepPlan mapped = CreateStep(
                    input.Request,
                    sourceAttempt.Attempt,
                    sourceStep);
                steps.Add(mapped);
                if (mapped.DeferredEvent is not null)
                {
                    deferred.Add(mapped.DeferredEvent);
                }
            }

            attempts.Add(new ModelInspectionFixtureAttemptPlan(
                sourceAttempt.Attempt,
                steps.MoveToImmutable()));
        }

        ImmutableArray<ModelInspectionFixtureAttemptPlan> mappedAttempts =
            attempts.MoveToImmutable();
        ValidatePresentationPreconditions(request, mappedAttempts);

        return new ModelInspectionFixtureExecutionPlan(
            request,
            mappedAttempts,
            deferred.ToImmutable());
    }

    private static ModelInspectionRequest CreateRequest(
        ModelInspectionFixtureRequestDescriptor source)
    {
        string quickScanArchitecture = source.EvidenceProfile ==
            ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction
                ? "granite-source"
                : "llama";
        ulong? contextLength = source.EvidenceProfile ==
            ModelInspectionFixtureEvidenceProfile.CompatibleMissingOptionalMetadata
                ? null
                : 8_192UL;
        ValidatedQuickScanSnapshot quickScan =
            ValidatedQuickScanSnapshot.CreateGguf(
                source.DisplayName,
                quickScanArchitecture,
                parameterSizeLabel: "8 billion",
                quantisation: "Q4_K_M",
                fileSizeBytes: FixedLengthBytes,
                declaredContextLength: contextLength,
                ggufVersion: 3);
        return new ModelInspectionRequest(
            SyntheticRoot + source.DisplayFileName,
            source.DisplayFileName,
            new ExpectedModelFileIdentity(FixedLengthBytes, FixedUtc),
            quickScan);
    }

    private static ModelInspectionFixtureServiceStepPlan CreateStep(
        ModelInspectionFixtureRequestDescriptor request,
        int ownerAttempt,
        ModelInspectionFixtureServiceStepDescriptor source)
    {
        string? checkpoint = source.Trigger.Kind switch
        {
            ModelInspectionFixtureServiceTriggerKind.Automatic
                when source.Trigger.Checkpoint is null => null,
            ModelInspectionFixtureServiceTriggerKind.Checkpoint
                when source.Trigger.Checkpoint is not null =>
                    source.Trigger.Checkpoint,
            _ => throw new InvalidOperationException(
                "The validated fixture trigger could not be mapped.")
        };

        ModelInspectionProgress? progress = null;
        ModelInspectionExecutionResult? terminal = null;
        ModelInspectionFixtureDeferredEvent? deferred = null;
        switch (source.Effect.Kind)
        {
            case ModelInspectionFixtureServiceEffectKind.Progress:
                progress = CreateProgress(source.Effect.Progress ??
                    throw new InvalidOperationException(
                        "The validated progress effect has no progress payload."));
                break;

            case ModelInspectionFixtureServiceEffectKind.Completed:
                terminal = ModelInspectionExecutionResult.Completed(
                    CreateResult(
                        request,
                        source.Effect.Outcome ??
                            throw new InvalidOperationException(
                                "The validated completed effect has no outcome."),
                        source.Effect.EvidenceProfile ??
                            throw new InvalidOperationException(
                                "The validated completed effect has no evidence profile.")));
                break;

            case ModelInspectionFixtureServiceEffectKind.Cancelled:
                terminal = ModelInspectionExecutionResult.Cancelled(
                    cooperative: true);
                break;

            case ModelInspectionFixtureServiceEffectKind.OperationalFailure:
                terminal = ModelInspectionExecutionResult.OperationalFailure(
                    CreateFailure(
                        source.Effect.FailureProfile ??
                            throw new InvalidOperationException(
                                "The validated failure effect has no failure profile."),
                        source.Effect.FailureDetailProfile ??
                            throw new InvalidOperationException(
                                "The validated failure effect has no detail profile.")));
                break;

            case ModelInspectionFixtureServiceEffectKind.DeferStaleProgress:
            case ModelInspectionFixtureServiceEffectKind.DeferStaleResultSnapshot:
            case ModelInspectionFixtureServiceEffectKind.DeferStaleMotion:
            case ModelInspectionFixtureServiceEffectKind.DeferStaleAnnouncement:
                if (checkpoint is null ||
                    source.Effect.DeferredCheckpoint is null)
                {
                    throw new InvalidOperationException(
                        "The validated deferred effect has no checkpoint identity.");
                }

                deferred = new ModelInspectionFixtureDeferredEvent(
                    MapDeferredKind(source.Effect.Kind),
                    ownerAttempt,
                    checkpoint,
                    source.Effect.DeferredCheckpoint);
                break;

            default:
                throw new InvalidOperationException(
                    "The validated fixture effect could not be mapped.");
        }

        return new ModelInspectionFixtureServiceStepPlan(
            source.Trigger.Kind,
            checkpoint,
            progress,
            terminal,
            deferred);
    }

    private static ModelInspectionProgress CreateProgress(
        ModelInspectionFixtureProgressDescriptor source)
    {
        ModelInspectionStage stage = source.Stage switch
        {
            ModelInspectionFixtureStage.CheckModelPackage =>
                ModelInspectionStage.CheckModelPackage,
            ModelInspectionFixtureStage.ReadModelConfiguration =>
                ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup =>
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
            ModelInspectionFixtureStage.ValidateModelStructure =>
                ModelInspectionStage.ValidateModelStructure,
            ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility =>
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            _ => throw new InvalidOperationException(
                "The validated fixture stage could not be mapped.")
        };
        ModelInspectionStageStatus status = source.Status switch
        {
            ModelInspectionFixtureStageStatus.Active =>
                ModelInspectionStageStatus.Active,
            ModelInspectionFixtureStageStatus.Completed =>
                ModelInspectionStageStatus.Completed,
            ModelInspectionFixtureStageStatus.Warning =>
                ModelInspectionStageStatus.Warning,
            ModelInspectionFixtureStageStatus.Failed =>
                ModelInspectionStageStatus.Failed,
            ModelInspectionFixtureStageStatus.Cancelled =>
                ModelInspectionStageStatus.Cancelled,
            _ => throw new InvalidOperationException(
                "The validated fixture stage status could not be mapped.")
        };
        string detail = source.DetailProfile switch
        {
            ModelInspectionFixtureProgressDetailProfile.Default => stage switch
            {
                ModelInspectionStage.CheckModelPackage =>
                    "Checking the model package.",
                ModelInspectionStage.ReadModelConfiguration =>
                    "Reading model configuration.",
                ModelInspectionStage.ValidateTokenizerAndChatSetup =>
                    "Validating tokenizer and chat setup.",
                ModelInspectionStage.ValidateModelStructure =>
                    "Validating model structure.",
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility =>
                    "Confirming core runtime compatibility.",
                _ => throw new InvalidOperationException(
                    "The mapped fixture stage has no detail.")
            },
            ModelInspectionFixtureProgressDetailProfile.Maximum =>
                MaximumProgressDetail,
            _ => throw new InvalidOperationException(
                "The validated progress detail profile could not be mapped.")
        };

        return new ModelInspectionProgress(
            stage,
            status,
            source.CompletedStageCount,
            totalStageCount: 5,
            source.Fraction,
            detail);
    }

    private static ModelInspectionResult CreateResult(
        ModelInspectionFixtureRequestDescriptor request,
        ModelInspectionFixtureOutcome outcomeProfile,
        ModelInspectionFixtureEvidenceProfile evidenceProfile)
    {
        ModelInspectionOutcome outcome = outcomeProfile switch
        {
            ModelInspectionFixtureOutcome.Ready => ModelInspectionOutcome.Ready,
            ModelInspectionFixtureOutcome.ReadyWithWarnings =>
                ModelInspectionOutcome.ReadyWithWarnings,
            ModelInspectionFixtureOutcome.ConversionRequired =>
                ModelInspectionOutcome.ConversionRequired,
            ModelInspectionFixtureOutcome.IncompletePackage =>
                ModelInspectionOutcome.IncompletePackage,
            ModelInspectionFixtureOutcome.Unsupported =>
                ModelInspectionOutcome.Unsupported,
            ModelInspectionFixtureOutcome.Invalid => ModelInspectionOutcome.Invalid,
            _ => throw new InvalidOperationException(
                "The validated fixture outcome could not be mapped.")
        };
        if (ExpectedOutcome(evidenceProfile) != outcome)
        {
            throw new InvalidOperationException(
                "The validated outcome and evidence profile do not agree.");
        }

        ModelInspectionFinding[] findings = outcome ==
            ModelInspectionOutcome.ReadyWithWarnings
                ?
                [
                    new ModelInspectionFinding(
                        SupportedWarningCode,
                        ModelInspectionFindingSeverity.Warning,
                        "Chat template not reported",
                        "The model does not report a chat template. Chat formatting may require manual configuration.",
                        "Review chat formatting before using this model.",
                        "The deterministic fixture reports no chat-template metadata.")
                ]
                : [];

        return new ModelInspectionResult(
            outcome,
            CreateEvidence(request, evidenceProfile),
            findings,
            SummaryFor(outcome),
            "Choose another model if needed.",
            outcome == ModelInspectionOutcome.ConversionRequired
                ? VerifiedConversionRoute
                : null,
            FixedUtc,
            FixedUtc.AddSeconds(2));
    }

    private static ModelInspectionEvidence CreateEvidence(
        ModelInspectionFixtureRequestDescriptor request,
        ModelInspectionFixtureEvidenceProfile profile)
    {
        bool missingOptionalMetadata = profile ==
            ModelInspectionFixtureEvidenceProfile.CompatibleMissingOptionalMetadata;
        bool missingChatTemplate = profile ==
            ModelInspectionFixtureEvidenceProfile.MissingChatTemplate;

        return new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                request.DisplayFileName,
                FixedCanonicalPathSha256,
                FixedLengthBytes,
                FixedUtc,
                FixedModelSha256,
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                ggufVersion: 3,
                modelName: request.DisplayName,
                architecture: "llama",
                fileType: 15,
                quantisationVersion: 2,
                declaredContextLength:
                    missingOptionalMetadata ? null : 8_192UL,
                embeddingSize: 4_096,
                layerCount: 32,
                attentionHeadCount: 32,
                kvHeadCount: 8,
                parameterCount: 8_000_000_000),
            new ModelInspectionTokenizerEvidence(
                tokenizerModel: "sentencepiece",
                vocabularyCount: 32_000,
                vocabularyType: "SPM",
                tokenizerSmokePassed: true,
                tokenizerSmokeTokenCount: 4,
                knownSpecialTokenIds: new Dictionary<string, int>(
                    StringComparer.Ordinal)
                {
                    ["bos"] = 1,
                    ["eos"] = 2
                }),
            missingChatTemplate
                ? new ModelInspectionChatTemplateEvidence(
                    present: false,
                    lengthCharacters: null,
                    sha256: null)
                : new ModelInspectionChatTemplateEvidence(
                    present: true,
                    lengthCharacters: 128,
                    sha256: FixedMetadataSha256),
            new ModelInspectionRuntimeIdentity(
                WorkerProtocol.WorkerId,
                workerVersion: "1.0.0",
                WorkerProtocol.Version,
                WorkerProtocol.RuntimeProfile,
                llamaSharpVersion: "0.27.0",
                backendPackageVersion: "0.27.0",
                mappedLlamaCppCommit: FixedLlamaCppCommit,
                nativeLibraryName: "llama.dll",
                processArchitecture: "X64",
                inspectionMode: "VocabOnly",
                usesCuda: false,
                usesVulkan: false,
                gpuLayerCount: 0),
            ObservationsFor(profile));
    }

    private static IReadOnlyList<ModelInspectionObservation> ObservationsFor(
        ModelInspectionFixtureEvidenceProfile profile) => profile switch
        {
            ModelInspectionFixtureEvidenceProfile.Compatible or
            ModelInspectionFixtureEvidenceProfile.CompatibleMissingOptionalMetadata =>
                [],
            ModelInspectionFixtureEvidenceProfile.MissingChatTemplate =>
                [Observation(
                    "MI-EVIDENCE-CHAT-TEMPLATE-MISSING",
                    "chat-template",
                    "non-blocking")],
            ModelInspectionFixtureEvidenceProfile.VerifiedIncompatible =>
                [Observation(
                    "MI-EVIDENCE-CONVERSION-REQUIRED",
                    "runtime-compatibility",
                    "conversion-required")],
            ModelInspectionFixtureEvidenceProfile.MissingPackageMember =>
                [Observation(
                    "MI-EVIDENCE-PACKAGE-MEMBER-MISSING",
                    "model-package",
                    "blocking")],
            ModelInspectionFixtureEvidenceProfile.UnsupportedArchitecture =>
                [Observation(
                    "MI-EVIDENCE-ARCHITECTURE-UNSUPPORTED",
                    "model-architecture",
                    "blocking")],
            ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction =>
                [Observation(
                    "MI-EVIDENCE-CROSS-SOURCE-CONTRADICTION",
                    "model-configuration",
                    "blocking")],
            _ => throw new InvalidOperationException(
                "The validated evidence profile could not be mapped.")
        };

    private static ModelInspectionObservation Observation(
        string code,
        string domain,
        string impact) => new(
            code,
            domain,
            impact,
            "The deterministic fixture supplies a fixed synthetic evidence witness.");

    private static ModelInspectionOutcome ExpectedOutcome(
        ModelInspectionFixtureEvidenceProfile profile) => profile switch
        {
            ModelInspectionFixtureEvidenceProfile.Compatible or
            ModelInspectionFixtureEvidenceProfile.CompatibleMissingOptionalMetadata =>
                ModelInspectionOutcome.Ready,
            ModelInspectionFixtureEvidenceProfile.MissingChatTemplate =>
                ModelInspectionOutcome.ReadyWithWarnings,
            ModelInspectionFixtureEvidenceProfile.VerifiedIncompatible =>
                ModelInspectionOutcome.ConversionRequired,
            ModelInspectionFixtureEvidenceProfile.MissingPackageMember =>
                ModelInspectionOutcome.IncompletePackage,
            ModelInspectionFixtureEvidenceProfile.UnsupportedArchitecture =>
                ModelInspectionOutcome.Unsupported,
            ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction =>
                ModelInspectionOutcome.Invalid,
            _ => throw new InvalidOperationException(
                "The validated evidence profile has no outcome mapping.")
        };

    private static string SummaryFor(ModelInspectionOutcome outcome) => outcome switch
    {
        ModelInspectionOutcome.Ready =>
            "The model passed all lightweight inspection checks.",
        ModelInspectionOutcome.ReadyWithWarnings =>
            "Inspection completed with a non-blocking warning.",
        ModelInspectionOutcome.ConversionRequired =>
            "The model requires the verified conversion route.",
        ModelInspectionOutcome.IncompletePackage =>
            "The model package is missing required content.",
        ModelInspectionOutcome.Unsupported =>
            "The model architecture is not supported.",
        ModelInspectionOutcome.Invalid =>
            "The model evidence is contradictory.",
        _ => throw new InvalidOperationException(
            "The mapped outcome has no summary.")
    };

    private static ModelInspectionOperationalFailure CreateFailure(
        ModelInspectionFixtureFailureProfile profile,
        ModelInspectionFixtureFailureDetailProfile detailProfile)
    {
        (string code, string userMessage, string technicalDetail) = profile switch
        {
            ModelInspectionFixtureFailureProfile.WorkerStartFailure => (
                "MI-OP-WORKER-START",
                "The inspection worker could not be started.",
                "The deterministic fixture simulates a worker start failure."),
            ModelInspectionFixtureFailureProfile.WorkerTimeout => (
                "MI-OP-WORKER-TIMEOUT",
                "The inspection worker did not respond in time.",
                "The deterministic fixture simulates a bounded worker timeout."),
            ModelInspectionFixtureFailureProfile.WorkerCrashEarlyExit => (
                "MI-OP-WORKER-EARLY-EXIT",
                "The inspection worker exited before completing.",
                "The deterministic fixture simulates an early worker exit."),
            ModelInspectionFixtureFailureProfile.MalformedWorkerResponse => (
                "MI-OP-WORKER-INVALID-RESPONSE",
                "The inspection worker returned an invalid response.",
                "The deterministic fixture simulates an invalid worker response."),
            ModelInspectionFixtureFailureProfile.CancellationUnconfirmed => (
                "MI-OP-CANCELLATION-UNCONFIRMED",
                "Cancellation could not be confirmed safely.",
                "The deterministic fixture supplies no cooperative cancellation evidence."),
            _ => throw new InvalidOperationException(
                "The validated failure profile could not be mapped.")
        };
        return detailProfile switch
        {
            ModelInspectionFixtureFailureDetailProfile.Default =>
                new ModelInspectionOperationalFailure(
                    code,
                    userMessage,
                    technicalDetail),
            ModelInspectionFixtureFailureDetailProfile.Maximum =>
                new ModelInspectionOperationalFailure(
                    code,
                    MaximumFailureDetail,
                    MaximumFailureDetail),
            _ => throw new InvalidOperationException(
                "The validated failure detail profile could not be mapped.")
        };
    }

    private static ModelInspectionFixtureDeferredEventKind MapDeferredKind(
        ModelInspectionFixtureServiceEffectKind kind) => kind switch
        {
            ModelInspectionFixtureServiceEffectKind.DeferStaleProgress =>
                ModelInspectionFixtureDeferredEventKind.StaleProgress,
            ModelInspectionFixtureServiceEffectKind.DeferStaleResultSnapshot =>
                ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot,
            ModelInspectionFixtureServiceEffectKind.DeferStaleMotion =>
                ModelInspectionFixtureDeferredEventKind.StaleMotion,
            ModelInspectionFixtureServiceEffectKind.DeferStaleAnnouncement =>
                ModelInspectionFixtureDeferredEventKind.StaleAnnouncement,
            _ => throw new InvalidOperationException(
                "The validated deferred effect kind could not be mapped.")
        };

    private static void ValidatePresentationPreconditions(
        ModelInspectionRequest request,
        IReadOnlyList<ModelInspectionFixtureAttemptPlan> attempts)
    {
        DelegateCommand command = new(_ => { }, _ => true);
        ModelInspectionPresentationCommands commands = new(
            command,
            command,
            command);
        long revision = 0;
        ModelInspectionPagePresentation initial =
            ModelInspectionPresentationFactory.Create(
                request,
                new ModelInspectionViewSnapshot(
                    new ModelInspectionRenderKey(1, revision++),
                    isRunActive: true,
                    isCancellationRequested: false,
                    progress: null,
                    terminalResult: null),
                commands,
                isDisclosureExpanded: false,
                new InspectionProgressRows());
        RequireState(
            initial,
            ModelInspectionFigmaState.InspectionProgress);

        foreach (ModelInspectionFixtureServiceStepPlan step in attempts
                     .SelectMany(attempt => attempt.ServiceSteps))
        {
            if (step.Progress is not null)
            {
                ModelInspectionPagePresentation progressPresentation =
                    ModelInspectionPresentationFactory.Create(
                        request,
                        new ModelInspectionViewSnapshot(
                            new ModelInspectionRenderKey(1, revision++),
                            isRunActive: true,
                            isCancellationRequested: false,
                            step.Progress,
                            terminalResult: null),
                        commands,
                        isDisclosureExpanded: false,
                        new InspectionProgressRows());
                RequireState(
                    progressPresentation,
                    ModelInspectionFigmaState.InspectionProgress);
                if (progressPresentation.RegionKeys.Progress.Stage !=
                        step.Progress.Stage ||
                    progressPresentation.RegionKeys.Progress.StageStatus !=
                        step.Progress.StageStatus ||
                    progressPresentation.RegionKeys.Progress.CompletedStageCount !=
                        step.Progress.CompletedStageCount ||
                    progressPresentation.RegionKeys.Progress.StageCount !=
                        step.Progress.TotalStageCount ||
                    progressPresentation.RegionKeys.Progress.StageFraction !=
                        step.Progress.StageFraction ||
                    !string.Equals(
                        progressPresentation.RegionKeys.Progress.Detail,
                        step.Progress.UserMessage,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The mapped fixture progress graph does not satisfy the production presentation preconditions.");
                }
            }

            if (step.TerminalResult is null)
            {
                continue;
            }

            ModelInspectionFigmaState collapsedState = InferTerminalState(
                step.TerminalResult,
                expanded: false);
            RequireState(
                CreateTerminalPresentation(
                    request,
                    step.TerminalResult,
                    commands,
                    revision++,
                    expanded: false),
                collapsedState);

            if (SupportsDisclosure(step.TerminalResult))
            {
                RequireState(
                    CreateTerminalPresentation(
                        request,
                        step.TerminalResult,
                        commands,
                        revision++,
                        expanded: true),
                    InferTerminalState(
                        step.TerminalResult,
                        expanded: true));
            }
        }
    }

    private static ModelInspectionPagePresentation CreateTerminalPresentation(
        ModelInspectionRequest request,
        ModelInspectionExecutionResult terminal,
        ModelInspectionPresentationCommands commands,
        long revision,
        bool expanded) => ModelInspectionPresentationFactory.Create(
            request,
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, revision),
                isRunActive: false,
                isCancellationRequested: false,
                progress: null,
                terminal),
            commands,
            expanded,
            new InspectionProgressRows());

    private static bool SupportsDisclosure(
        ModelInspectionExecutionResult terminal) =>
        terminal.Status == ModelInspectionExecutionStatus.Completed &&
        terminal.Result?.Outcome is ModelInspectionOutcome.Ready or
            ModelInspectionOutcome.ReadyWithWarnings or
            ModelInspectionOutcome.ConversionRequired or
            ModelInspectionOutcome.Invalid;

    private static ModelInspectionFigmaState InferTerminalState(
        ModelInspectionExecutionResult terminal,
        bool expanded) => terminal.Status switch
        {
            ModelInspectionExecutionStatus.Cancelled when !expanded =>
                ModelInspectionFigmaState.Cancelled,
            ModelInspectionExecutionStatus.OperationalFailure when !expanded =>
                ModelInspectionFigmaState.OperationalFailure,
            ModelInspectionExecutionStatus.Completed => terminal.Result?.Outcome switch
            {
                ModelInspectionOutcome.Ready => expanded
                    ? ModelInspectionFigmaState.ReadyExpanded
                    : ModelInspectionFigmaState.ReadyCollapsed,
                ModelInspectionOutcome.ReadyWithWarnings => expanded
                    ? ModelInspectionFigmaState.ReadyWithWarningsExpanded
                    : ModelInspectionFigmaState.ReadyWithWarningsCollapsed,
                ModelInspectionOutcome.ConversionRequired => expanded
                    ? ModelInspectionFigmaState.ConversionRequiredExpanded
                    : ModelInspectionFigmaState.ConversionRequiredCollapsed,
                ModelInspectionOutcome.IncompletePackage when !expanded =>
                    ModelInspectionFigmaState.IncompletePackage,
                ModelInspectionOutcome.Unsupported when !expanded =>
                    ModelInspectionFigmaState.Unsupported,
                ModelInspectionOutcome.Invalid => expanded
                    ? ModelInspectionFigmaState.InvalidExpanded
                    : ModelInspectionFigmaState.InvalidCollapsed,
                _ => throw new InvalidOperationException(
                    "The mapped fixture completion has no intended presentation state.")
            },
            _ => throw new InvalidOperationException(
                "The mapped fixture terminal has no intended presentation state.")
        };

    private static void RequireState(
        ModelInspectionPagePresentation presentation,
        ModelInspectionFigmaState intendedState)
    {
        if (presentation.State != intendedState)
        {
            throw new InvalidOperationException(
                "The mapped fixture graph does not satisfy the production presentation preconditions.");
        }
    }

    private static string CreateMaximumDetail(string phrase)
    {
        StringBuilder builder = new(MaximumDetailLength + phrase.Length);
        while (builder.Length < MaximumDetailLength)
        {
            builder.Append(phrase);
        }

        return builder.ToString(0, MaximumDetailLength);
    }
}
#endif
