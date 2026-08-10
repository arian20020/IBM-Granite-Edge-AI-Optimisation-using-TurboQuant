using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

internal static partial class ModelInspectionFixtureValidator
{
    private const string ExpectedSchemaFileName =
        "model-inspection-fixture.schema.json";
    private const string MissingChatTemplateFindingId =
        "MI-WARN-CHAT-TEMPLATE-MISSING";
    private const string MissingChatTemplateTitleKey =
        "fixture.warning.chat-template-missing.title";
    private const string MissingChatTemplateTitle =
        "Chat template not reported";
    private const string MissingChatTemplateDetailKey =
        "fixture.warning.chat-template-missing.detail";
    private const string MissingChatTemplateDetail =
        "The model does not report a chat template. Chat formatting may require manual configuration.";

    private static readonly ModelInspectionFixtureStage[] OrderedStages =
        Enum.GetValues<ModelInspectionFixtureStage>();

    internal static ModelInspectionFixtureCoveragePolicy ValidatePolicy(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureCoveragePolicy policy,
        VerifiedModelInspectionFixtureSchema schema)
    {
        if (!source.FileName.Equals(
                "model-inspection-fixture-coverage-policy.json",
                StringComparison.Ordinal))
        {
            throw Failure(source, "$", "policy.filename");
        }

        Require(policy, source, "$", "policy.required");
        if (!string.Equals(policy.Schema, ExpectedSchemaFileName, StringComparison.Ordinal) ||
            policy.SchemaVersion != schema.SchemaVersion)
        {
            throw Failure(source, "$", "policy.schema-version");
        }

        Require(policy.Fixtures, source, "$.fixtures", "policy.fixtures-required");
        Require(policy.Presets, source, "$.presets", "policy.presets-required");
        Require(policy.DisclosurePairs, source, "$.disclosurePairs", "policy.pairs-required");
        Require(policy.GallerySwitchPairs, source, "$.gallerySwitchPairs", "policy.switch-pairs-required");
        Require(policy.CopyRegistry, source, "$.copyRegistry", "policy.copy-registry-required");
        Require(policy.ExternalEvidenceLinks, source, "$.externalEvidenceLinks", "policy.evidence-links-required");
        if (policy.Fixtures.Count is 0 or > StrictModelInspectionFixtureJson.MaximumCollectionLength)
        {
            throw Failure(source, "$.fixtures", "policy.fixture-count");
        }

        ValidateObjectGraph(source, policy, allowRepositorySourcePaths: true);

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> fileNames = new(StringComparer.Ordinal);
        foreach (ModelInspectionFixturePolicyEntry entry in policy.Fixtures)
        {
            Require(entry, source, "$.fixtures[]", "policy.fixture-null");
            ValidateId(entry.Id, source, "$.fixtures[].id");
            ValidateSlug(entry.TargetCondition, source, "$.fixtures[].targetCondition");
            if (entry.Variant is not null)
            {
                ValidateSlug(entry.Variant, source, "$.fixtures[].variant");
            }

            ValidateFileNameParts(
                source,
                entry.FileName,
                entry.Id,
                entry.TargetCondition,
                entry.Variant);
            if (!ids.Add(entry.Id))
            {
                throw Failure(source, "$.fixtures[].id", "policy.duplicate-id");
            }

            if (!fileNames.Add(entry.FileName))
            {
                throw Failure(source, "$.fixtures[].fileName", "policy.duplicate-filename");
            }

            Require(entry.RequiredCoverageTags, source, "$.fixtures[].requiredCoverageTags", "policy.coverage-required");
            Require(entry.RequiredInteractions, source, "$.fixtures[].requiredInteractions", "policy.interactions-required");
            Require(entry.RequiredPresets, source, "$.fixtures[].requiredPresets", "policy.presets-required");
            RequireUnique(entry.RequiredCoverageTags, source, "$.fixtures[].requiredCoverageTags", "policy.duplicate-coverage-tag");
            RequireUnique(entry.RequiredInteractions, source, "$.fixtures[].requiredInteractions", "policy.duplicate-interaction");
            RequireUnique(entry.RequiredPresets, source, "$.fixtures[].requiredPresets", "policy.duplicate-preset");
        }

        Dictionary<string, ModelInspectionFixturePreset> presets = new(StringComparer.Ordinal);
        foreach (ModelInspectionFixturePreset preset in policy.Presets)
        {
            Require(preset, source, "$.presets[]", "policy.preset-null");
            if (!PresetIdRegex().IsMatch(preset.Id) || !presets.TryAdd(preset.Id, preset))
            {
                throw Failure(source, "$.presets[].id", "policy.preset-id");
            }
        }

        foreach (ModelInspectionFixturePolicyEntry entry in policy.Fixtures)
        {
            if (entry.RequiredPresets.Count == 0 ||
                entry.RequiredPresets.Any(required => !presets.ContainsKey(required)))
            {
                throw Failure(source, "$.fixtures[].requiredPresets", "policy.unknown-preset");
            }
        }

        ValidateTargetPairs(source, policy);
        ValidateGallerySwitchPairs(source, policy, ids);
        ValidateCopyRegistry(source, policy.CopyRegistry);
        ValidateExternalLinks(source, policy.ExternalEvidenceLinks, ids);

        return Freeze(policy);
    }

    internal static ModelInspectionFixtureDescriptor ValidateDescriptor(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureDescriptor descriptor,
        ValidatedModelInspectionFixtureCoveragePolicy policy,
        VerifiedModelInspectionFixtureSchema schema)
    {
        Require(descriptor, source, "$", "fixture.required");
        ValidateFileNameParts(
            source,
            source.FileName,
            descriptor.Id,
            descriptor.TargetCondition,
            descriptor.Variant);
        if (!string.Equals(descriptor.Schema, ExpectedSchemaFileName, StringComparison.Ordinal) ||
            descriptor.SchemaVersion != schema.SchemaVersion ||
            descriptor.SchemaVersion != policy.SchemaVersion)
        {
            throw Failure(source, "$", "fixture.schema-version");
        }

        ModelInspectionFixturePolicyEntry policyEntry = policy.Value.Fixtures.SingleOrDefault(
            entry => entry.FileName.Equals(source.FileName, StringComparison.Ordinal))
            ?? throw Failure(source, "$", "fixture.not-in-policy");
        if (!policyEntry.Id.Equals(descriptor.Id, StringComparison.Ordinal) ||
            !policyEntry.TargetCondition.Equals(descriptor.TargetCondition, StringComparison.Ordinal) ||
            !string.Equals(policyEntry.Variant, descriptor.Variant, StringComparison.Ordinal))
        {
            throw Failure(source, "$", "fixture.policy-identity");
        }

        Require(descriptor.Title, source, "$.title", "fixture.title-required");
        if (string.IsNullOrWhiteSpace(descriptor.Title) ||
            descriptor.Title.Length > StrictModelInspectionFixtureJson.MaximumDisplayNameLength)
        {
            throw Failure(source, "$.title", "fixture.title-boundary");
        }
        Require(descriptor.Coverage, source, "$.coverage", "fixture.coverage-required");
        Require(descriptor.Input, source, "$.input", "fixture.input-required");
        Require(descriptor.Expected, source, "$.expected", "fixture.expected-required");
        Require(descriptor.PresetExpectations, source, "$.presetExpectations", "fixture.preset-expectations-required");
        Require(descriptor.Interactions, source, "$.interactions", "fixture.interactions-required");
        Require(descriptor.Presets, source, "$.presets", "fixture.presets-required");
        ValidateObjectGraph(source, descriptor, allowRepositorySourcePaths: false);
        ValidateCoverage(source, descriptor.Coverage, policyEntry);
        ModelInspectionFixtureReplayState replay = ValidateInput(
            source,
            descriptor.Input,
            descriptor.Interactions,
            policy.Value);
        ValidateExpected(source, descriptor.Expected, policyEntry, policy.Value.CopyRegistry);
        ValidatePresetExpectations(source, descriptor, policyEntry, policy.Value);
        ValidateInteractions(source, descriptor, policy.Value);
        ValidateCurrentScreenConsistency(source, descriptor, replay);

        return Freeze(descriptor);
    }

    internal static void ValidateCatalogue(
        IReadOnlyList<ValidatedModelInspectionFixture> fixtures,
        ValidatedModelInspectionFixtureCoveragePolicy policy,
        ModelInspectionFixtureDocumentSource diagnosticSource)
    {
        if (fixtures.Count != policy.Value.Fixtures.Count)
        {
            throw Failure(diagnosticSource, "$", "catalogue.policy-count");
        }

        string[] expected = policy.Value.Fixtures
            .Select(entry => entry.FileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actual = fixtures
            .Select(fixture => fixture.FileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw Failure(diagnosticSource, "$", "catalogue.policy-set");
        }

        if (fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count() != fixtures.Count ||
            fixtures.Select(fixture => fixture.FileName).Distinct(StringComparer.Ordinal).Count() != fixtures.Count)
        {
            throw Failure(diagnosticSource, "$", "catalogue.duplicate-identity");
        }
    }

    internal static void ValidateFileNameParts(
        ModelInspectionFixtureDocumentSource source,
        string? fileName,
        string? id,
        string? targetCondition,
        string? variant)
    {
        if (StrictModelInspectionFixtureJson.SafeDiagnosticFileName(fileName) == "<invalid-filename>" ||
            fileName is null ||
            !FixtureFileNameRegex().IsMatch(fileName))
        {
            throw Failure(source, "$", "fixture.filename-shape");
        }

        ValidateId(id, source, "$.id");
        ValidateSlug(targetCondition, source, "$.targetCondition");
        if (variant is not null)
        {
            ValidateSlug(variant, source, "$.variant");
        }

        string expected = $"{id}-{targetCondition}" +
            (variant is null ? string.Empty : $"-{variant}") +
            ".fixture.json";
        if (!fileName.Equals(expected, StringComparison.Ordinal))
        {
            throw Failure(source, "$", "fixture.filename-identity");
        }
    }

    private static void ValidateCoverage(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureCoverage coverage,
        ModelInspectionFixturePolicyEntry policyEntry)
    {
        Require(coverage.FigmaStates, source, "$.coverage.figmaStates", "coverage.required");
        Require(coverage.Stages, source, "$.coverage.stages", "coverage.required");
        Require(coverage.StageStatuses, source, "$.coverage.stageStatuses", "coverage.required");
        Require(coverage.Outcomes, source, "$.coverage.outcomes", "coverage.required");
        Require(coverage.Interactions, source, "$.coverage.interactions", "coverage.required");
        Require(coverage.LifecycleTags, source, "$.coverage.lifecycleTags", "coverage.required");
        Require(coverage.FailureProfiles, source, "$.coverage.failureProfiles", "coverage.required");
        Require(coverage.StressTags, source, "$.coverage.stressTags", "coverage.required");
        if (!coverage.FigmaStates.Contains(policyEntry.CanonicalFigmaState))
        {
            throw Failure(source, "$.coverage.figmaStates", "coverage.canonical-state");
        }

        RequireUnique(coverage.FigmaStates, source, "$.coverage.figmaStates", "coverage.duplicate");
        RequireUnique(coverage.Stages, source, "$.coverage.stages", "coverage.duplicate");
        RequireUnique(coverage.StageStatuses, source, "$.coverage.stageStatuses", "coverage.duplicate");
        RequireUnique(coverage.Outcomes, source, "$.coverage.outcomes", "coverage.duplicate");
        RequireUnique(coverage.Interactions, source, "$.coverage.interactions", "coverage.duplicate");
        RequireUnique(coverage.LifecycleTags, source, "$.coverage.lifecycleTags", "coverage.duplicate");
        RequireUnique(coverage.FailureProfiles, source, "$.coverage.failureProfiles", "coverage.duplicate");
        RequireUnique(coverage.StressTags, source, "$.coverage.stressTags", "coverage.duplicate");
    }

    private static ModelInspectionFixtureReplayState ValidateInput(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureInput input,
        IReadOnlyList<ModelInspectionFixtureInteraction> interactions,
        ModelInspectionFixtureCoveragePolicy policy)
    {
        Require(input.Request, source, "$.input.request", "input.request-required");
        Require(input.Attempts, source, "$.input.attempts", "input.attempts-required");
        Require(input.SetupSteps, source, "$.input.setupSteps", "input.setup-required");
        ValidateIdentifier(input.ObservationCheckpoint, source, "$.input.observationCheckpoint");
        if (string.IsNullOrWhiteSpace(input.Request.DisplayName) ||
            input.Request.DisplayName.Length > StrictModelInspectionFixtureJson.MaximumDisplayNameLength)
        {
            throw Failure(source, "$.input.request.displayName", "request.display-name");
        }

        ValidateDisplayFileName(input.Request.DisplayFileName, source);

        int expectedAttempt = 1;
        Dictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> serviceCheckpoints = new();
        Dictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> deferredCheckpoints = new();
        foreach (ModelInspectionFixtureAttemptDescriptor attempt in input.Attempts)
        {
            Require(attempt, source, "$.input.attempts[]", "input.attempt-null");
            if (attempt.Attempt != expectedAttempt++)
            {
                throw Failure(source, "$.input.attempts[].attempt", "attempt.monotonic");
            }

            Require(attempt.ServiceSteps, source, "$.input.attempts[].serviceSteps", "attempt.steps-required");
            ValidateAttempt(
                source,
                attempt,
                input.Request,
                serviceCheckpoints,
                deferredCheckpoints);
        }

        return ValidateSetup(
            source,
            input,
            interactions,
            policy,
            serviceCheckpoints,
            deferredCheckpoints);
    }

    private static void ValidateAttempt(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureAttemptDescriptor attempt,
        ModelInspectionFixtureRequestDescriptor request,
        IDictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> serviceCheckpoints,
        IDictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> deferredCheckpoints)
    {
        bool terminalSeen = false;
        int priorStageOrdinal = 0;
        int warningOrdinal = 0;
        int failedOrdinal = 0;
        int cancelledOrdinal = 0;
        List<ModelInspectionFixtureProgressDescriptor> progresses = [];
        ModelInspectionFixtureServiceEffectDescriptor? terminal = null;
        ModelInspectionFixtureProgressDescriptor? priorProgress = null;
        bool blockingProgressSeen = false;

        foreach (ModelInspectionFixtureServiceStepDescriptor step in attempt.ServiceSteps)
        {
            Require(step, source, "$.input.attempts[].serviceSteps[]", "service-step.null");
            Require(step.Trigger, source, "$.input.attempts[].serviceSteps[].trigger", "service-trigger.required");
            Require(step.Effect, source, "$.input.attempts[].serviceSteps[].effect", "service-effect.required");

            string? checkpoint = null;
            if (step.Trigger.Kind == ModelInspectionFixtureServiceTriggerKind.Checkpoint)
            {
                ValidateIdentifier(step.Trigger.Checkpoint, source, "$.input.attempts[].serviceSteps[].trigger.checkpoint");
                checkpoint = step.Trigger.Checkpoint!;
            }
            else
            {
                if (step.Trigger.Checkpoint is not null)
                {
                    throw Failure(source, "$.input.attempts[].serviceSteps[].trigger", "service-trigger.payload");
                }

            }

            if (checkpoint is not null &&
                !serviceCheckpoints.TryAdd((attempt.Attempt, checkpoint), step.Effect.Kind))
            {
                throw Failure(source, "$.input.attempts[].serviceSteps[].trigger.checkpoint", "service-checkpoint.duplicate");
            }

            bool isTerminal = step.Effect.Kind is
                ModelInspectionFixtureServiceEffectKind.Completed or
                ModelInspectionFixtureServiceEffectKind.Cancelled or
                ModelInspectionFixtureServiceEffectKind.OperationalFailure;
            if (terminalSeen && step.Effect.Kind == ModelInspectionFixtureServiceEffectKind.Progress)
            {
                throw Failure(source, "$.input.attempts[].serviceSteps[]", "attempt.progress-after-terminal");
            }

            if (isTerminal)
            {
                if (terminalSeen)
                {
                    throw Failure(source, "$.input.attempts[].serviceSteps[]", "attempt.multiple-terminal");
                }

                terminalSeen = true;
                terminal = step.Effect;
            }

            ValidateEffectPayload(source, step.Effect);
            if (step.Effect.Progress is { } progress)
            {
                ValidateProgress(source, progress);
                int ordinal = (int)progress.Stage;
                if (blockingProgressSeen)
                {
                    throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress", "progress.after-blocking-state");
                }

                if (ordinal < priorStageOrdinal)
                {
                    throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress.stage", "progress.nonmonotonic");
                }

                if (priorProgress is not null && ordinal == priorStageOrdinal)
                {
                    if (priorProgress.Status != ModelInspectionFixtureStageStatus.Active)
                    {
                        throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress.status", "progress.status-regression");
                    }

                    if (priorProgress.Status == ModelInspectionFixtureStageStatus.Active &&
                        progress.Status == ModelInspectionFixtureStageStatus.Active &&
                        priorProgress.Fraction is { } priorFraction &&
                        progress.Fraction is { } currentFraction &&
                        currentFraction < priorFraction)
                    {
                        throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress.fraction", "progress.fraction-regression");
                    }
                }

                priorStageOrdinal = ordinal;
                priorProgress = progress;
                blockingProgressSeen = progress.Status is
                    ModelInspectionFixtureStageStatus.Failed or
                    ModelInspectionFixtureStageStatus.Cancelled;
                progresses.Add(progress);
                warningOrdinal = progress.Status == ModelInspectionFixtureStageStatus.Warning
                    ? ordinal
                    : warningOrdinal;
                failedOrdinal = progress.Status == ModelInspectionFixtureStageStatus.Failed
                    ? ordinal
                    : failedOrdinal;
                cancelledOrdinal = progress.Status == ModelInspectionFixtureStageStatus.Cancelled
                    ? ordinal
                    : cancelledOrdinal;
            }

            if (step.Effect.DeferredCheckpoint is { } deferred)
            {
                ValidateIdentifier(deferred, source, "$.input.attempts[].serviceSteps[].effect.deferredCheckpoint");
                if (!deferredCheckpoints.TryAdd((attempt.Attempt, deferred), step.Effect.Kind))
                {
                    throw Failure(source, "$.input.attempts[].serviceSteps[].effect.deferredCheckpoint", "deferred-checkpoint.duplicate");
                }
            }
        }

        ValidateTerminalProfile(source, request, terminal);
        ValidateExceptionalProgressPaths(
            source,
            progresses,
            terminal,
            warningOrdinal,
            failedOrdinal,
            cancelledOrdinal);
    }

    private static void ValidateEffectPayload(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureServiceEffectDescriptor effect)
    {
        bool valid = effect.Kind switch
        {
            ModelInspectionFixtureServiceEffectKind.Progress =>
                effect.Progress is not null &&
                effect.Outcome is null &&
                effect.EvidenceProfile is null &&
                effect.FailureProfile is null &&
                effect.DeferredCheckpoint is null,
            ModelInspectionFixtureServiceEffectKind.Completed =>
                effect.Progress is null &&
                effect.Outcome is not null &&
                effect.EvidenceProfile is not null &&
                effect.FailureProfile is null &&
                effect.DeferredCheckpoint is null,
            ModelInspectionFixtureServiceEffectKind.Cancelled =>
                effect.Progress is null &&
                effect.Outcome is null &&
                effect.EvidenceProfile is null &&
                effect.FailureProfile is null &&
                effect.DeferredCheckpoint is null,
            ModelInspectionFixtureServiceEffectKind.OperationalFailure =>
                effect.Progress is null &&
                effect.Outcome is null &&
                effect.EvidenceProfile is null &&
                effect.FailureProfile is not null &&
                effect.DeferredCheckpoint is null,
            ModelInspectionFixtureServiceEffectKind.DeferStaleProgress or
            ModelInspectionFixtureServiceEffectKind.DeferStaleResultSnapshot or
            ModelInspectionFixtureServiceEffectKind.DeferStaleMotion or
            ModelInspectionFixtureServiceEffectKind.DeferStaleAnnouncement =>
                effect.Progress is null &&
                effect.Outcome is null &&
                effect.EvidenceProfile is null &&
                effect.FailureProfile is null &&
                effect.DeferredCheckpoint is not null,
            _ => false
        };
        if (!valid)
        {
            throw Failure(source, "$.input.attempts[].serviceSteps[].effect", "service-effect.payload");
        }
    }

    private static void ValidateProgress(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureProgressDescriptor progress)
    {
        if (!OrderedStages.Contains(progress.Stage))
        {
            throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress.stage", "progress.stage");
        }

        int ordinal = (int)progress.Stage;
        int expectedCompleted = progress.Status is
            ModelInspectionFixtureStageStatus.Completed or
            ModelInspectionFixtureStageStatus.Warning
                ? ordinal
                : ordinal - 1;
        if (progress.CompletedStageCount != expectedCompleted)
        {
            throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress.completedStageCount", "progress.completed-count");
        }

        if (progress.Fraction is { } fraction &&
            (progress.Status != ModelInspectionFixtureStageStatus.Active ||
             !double.IsFinite(fraction) ||
             fraction is < 0 or > 1))
        {
            throw Failure(source, "$.input.attempts[].serviceSteps[].effect.progress.fraction", "progress.fraction");
        }
    }

    private static void ValidateTerminalProfile(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureRequestDescriptor request,
        ModelInspectionFixtureServiceEffectDescriptor? terminal)
    {
        if (terminal?.Kind != ModelInspectionFixtureServiceEffectKind.Completed)
        {
            return;
        }

        ModelInspectionFixtureOutcome expectedOutcome = terminal.EvidenceProfile switch
        {
            ModelInspectionFixtureEvidenceProfile.Compatible => ModelInspectionFixtureOutcome.Ready,
            ModelInspectionFixtureEvidenceProfile.CompatibleMissingOptionalMetadata => ModelInspectionFixtureOutcome.Ready,
            ModelInspectionFixtureEvidenceProfile.MissingChatTemplate => ModelInspectionFixtureOutcome.ReadyWithWarnings,
            ModelInspectionFixtureEvidenceProfile.VerifiedIncompatible => ModelInspectionFixtureOutcome.ConversionRequired,
            ModelInspectionFixtureEvidenceProfile.MissingPackageMember => ModelInspectionFixtureOutcome.IncompletePackage,
            ModelInspectionFixtureEvidenceProfile.UnsupportedArchitecture => ModelInspectionFixtureOutcome.Unsupported,
            ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction => ModelInspectionFixtureOutcome.Invalid,
            _ => throw Failure(source, "$.input.attempts[].serviceSteps[].effect.evidenceProfile", "terminal.evidence-profile")
        };
        if (terminal.Outcome != expectedOutcome || terminal.EvidenceProfile != request.EvidenceProfile)
        {
            throw Failure(source, "$.input.attempts[].serviceSteps[].effect", "terminal.outcome-profile");
        }
    }

    private static void ValidateExceptionalProgressPaths(
        ModelInspectionFixtureDocumentSource source,
        IReadOnlyList<ModelInspectionFixtureProgressDescriptor> progresses,
        ModelInspectionFixtureServiceEffectDescriptor? terminal,
        int warningOrdinal,
        int failedOrdinal,
        int cancelledOrdinal)
    {
        ModelInspectionFixtureProgressDescriptor[] warnings = progresses
            .Where(progress => progress.Status == ModelInspectionFixtureStageStatus.Warning)
            .ToArray();
        bool warningTerminal = terminal?.Kind == ModelInspectionFixtureServiceEffectKind.Completed &&
            terminal.Outcome == ModelInspectionFixtureOutcome.ReadyWithWarnings &&
            terminal.EvidenceProfile == ModelInspectionFixtureEvidenceProfile.MissingChatTemplate;
        if (warnings.Length > 0 || warningTerminal)
        {
            const int approvedWarningOrdinal =
                (int)ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup;
            int[] subsequent = progresses
                .Where(progress => (int)progress.Stage > approvedWarningOrdinal)
                .Select(progress => (int)progress.Stage)
                .Distinct()
                .ToArray();
            int[] expected = Enumerable.Range(
                    approvedWarningOrdinal + 1,
                    OrderedStages.Length - approvedWarningOrdinal)
                .ToArray();
            bool remainingCompleted = progresses
                .Where(progress => (int)progress.Stage > approvedWarningOrdinal)
                .All(progress => progress.Status == ModelInspectionFixtureStageStatus.Completed);
            if (warnings.Length != 1 ||
                warningOrdinal != approvedWarningOrdinal ||
                warnings[0].Stage != ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup ||
                !subsequent.SequenceEqual(expected) ||
                !remainingCompleted ||
                !warningTerminal)
            {
                throw Failure(source, "$.input.attempts[].serviceSteps", "progress.warning-terminal");
            }
        }

        if (failedOrdinal > 0 &&
            (terminal?.Kind != ModelInspectionFixtureServiceEffectKind.Completed ||
             terminal.Outcome != ModelInspectionFixtureOutcome.Invalid ||
             terminal.EvidenceProfile != ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction ||
             progresses.Any(progress => (int)progress.Stage > failedOrdinal)))
        {
            throw Failure(source, "$.input.attempts[].serviceSteps", "progress.failed-terminal");
        }

        if (cancelledOrdinal > 0 &&
            (terminal?.Kind != ModelInspectionFixtureServiceEffectKind.Cancelled ||
             progresses.Any(progress => (int)progress.Stage > cancelledOrdinal)))
        {
            throw Failure(source, "$.input.attempts[].serviceSteps", "progress.cancelled-terminal");
        }
    }

    private static ModelInspectionFixtureReplayState ValidateSetup(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureInput input,
        IReadOnlyList<ModelInspectionFixtureInteraction> interactions,
        ModelInspectionFixtureCoveragePolicy policy,
        IReadOnlyDictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> serviceCheckpoints,
        IReadOnlyDictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> deferredCheckpoints)
    {
        if (input.SetupSteps.Count == 0)
        {
            throw Failure(source, "$.input.setupSteps", "setup.empty");
        }

        Dictionary<string, ModelInspectionFixtureInteraction> interactionById =
            new(StringComparer.Ordinal);
        foreach (ModelInspectionFixtureInteraction interaction in interactions)
        {
            if (interaction is null ||
                string.IsNullOrEmpty(interaction.Id) ||
                !interactionById.TryAdd(interaction.Id, interaction))
            {
                throw Failure(source, "$.interactions[]", "interaction.duplicate-or-null");
            }
        }

        HashSet<(int Attempt, string Checkpoint)> releasedServiceCheckpoints = [];
        HashSet<(int Attempt, string Checkpoint)> availableDeferredCheckpoints = [];
        HashSet<(int Attempt, string Checkpoint)> releasedDeferredCheckpoints = [];
        HashSet<int> obsoleteAttempts = [];
        int activeAttempt = input.Attempts.Count == 0 ? 0 : 1;
        int nextServiceStep = 0;
        bool terminalReleased = false;
        ModelInspectionFixtureServiceEffectDescriptor? currentEffect = null;
        string? currentCheckpoint = null;
        int observationCount = 0;

        ApplyAutomaticSteps();
        for (int index = 0; index < input.SetupSteps.Count; index++)
        {
            ModelInspectionFixtureSetupStepDescriptor step = input.SetupSteps[index];
            Require(step, source, "$.input.setupSteps[]", "setup.null");
            switch (step.Kind)
            {
                case ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint:
                    RequireAttemptCheckpointOnly(source, step);
                    (int Attempt, string Checkpoint) serviceKey =
                        (step.Attempt!.Value, step.Checkpoint!);
                    if (!serviceCheckpoints.ContainsKey(serviceKey))
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.unknown-service-checkpoint");
                    }

                    if (step.Attempt.Value != activeAttempt)
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.inactive-attempt");
                    }

                    if (!releasedServiceCheckpoints.Add(serviceKey))
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.duplicate-service-release");
                    }

                    ModelInspectionFixtureAttemptDescriptor active = input.Attempts[activeAttempt - 1];
                    if (nextServiceStep >= active.ServiceSteps.Count ||
                        active.ServiceSteps[nextServiceStep].Trigger.Kind !=
                            ModelInspectionFixtureServiceTriggerKind.Checkpoint ||
                        !string.Equals(
                            active.ServiceSteps[nextServiceStep].Trigger.Checkpoint,
                            step.Checkpoint,
                            StringComparison.Ordinal))
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.service-release-order");
                    }

                    if (terminalReleased)
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.release-after-terminal");
                    }

                    ApplyEffect(active.ServiceSteps[nextServiceStep].Effect);
                    nextServiceStep++;
                    currentCheckpoint = step.Checkpoint;
                    ApplyAutomaticSteps();
                    break;

                case ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress:
                    ReleaseDeferred(
                        step,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleProgress);
                    break;

                case ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot:
                    ReleaseDeferred(
                        step,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleResultSnapshot);
                    break;

                case ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion:
                    ReleaseDeferred(
                        step,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleMotion);
                    break;

                case ModelInspectionFixtureSetupStepKind.ReleaseStaleAnnouncement:
                    ReleaseDeferred(
                        step,
                        ModelInspectionFixtureServiceEffectKind.DeferStaleAnnouncement);
                    break;

                case ModelInspectionFixtureSetupStepKind.InvokeDisclosure:
                case ModelInspectionFixtureSetupStepKind.InvokeCancel:
                case ModelInspectionFixtureSetupStepKind.InvokeRetry:
                case ModelInspectionFixtureSetupStepKind.InvokeRestart:
                case ModelInspectionFixtureSetupStepKind.InvokeChooseAnother:
                    RequireInteractionOnly(source, step);
                    if (!interactionById.TryGetValue(step.InteractionId!, out ModelInspectionFixtureInteraction? interaction) ||
                        !MatchesSetupKind(step.Kind, interaction.Kind) ||
                        !string.Equals(interaction.SourceCheckpoint, currentCheckpoint, StringComparison.Ordinal))
                    {
                        throw Failure(source, "$.input.setupSteps[].interactionId", "setup.interaction-scope");
                    }

                    currentCheckpoint = interaction.Target;
                    if (step.Kind is ModelInspectionFixtureSetupStepKind.InvokeRetry or
                        ModelInspectionFixtureSetupStepKind.InvokeRestart)
                    {
                        if (activeAttempt <= 0 || activeAttempt >= input.Attempts.Count)
                        {
                            throw Failure(source, "$.input.setupSteps[]", "setup.dangling-attempt-transition");
                        }

                        obsoleteAttempts.Add(activeAttempt);
                        activeAttempt++;
                        nextServiceStep = 0;
                        terminalReleased = false;
                        currentEffect = null;
                        ApplyAutomaticSteps();
                    }
                    else if (step.Kind == ModelInspectionFixtureSetupStepKind.InvokeChooseAnother)
                    {
                        if (activeAttempt > 0)
                        {
                            obsoleteAttempts.Add(activeAttempt);
                            activeAttempt = 0;
                        }
                    }

                    break;

                case ModelInspectionFixtureSetupStepKind.Observe:
                    if (step.Attempt is not null || step.InteractionId is not null)
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.observe-payload");
                    }

                    ValidateIdentifier(step.Checkpoint, source, "$.input.setupSteps[].checkpoint");
                    observationCount++;
                    if (index != input.SetupSteps.Count - 1 ||
                        !string.Equals(step.Checkpoint, input.ObservationCheckpoint, StringComparison.Ordinal))
                    {
                        throw Failure(source, "$.input.setupSteps[]", "setup.final-observation");
                    }

                    currentCheckpoint = step.Checkpoint;
                    break;

                default:
                    throw Failure(source, "$.input.setupSteps[].kind", "setup.kind");
            }
        }

        if (observationCount != 1 ||
            !string.Equals(currentCheckpoint, input.ObservationCheckpoint, StringComparison.Ordinal))
        {
            throw Failure(source, "$.input.setupSteps", "setup.observation-count");
        }

        _ = policy;
        return new ModelInspectionFixtureReplayState(currentEffect);

        void ApplyAutomaticSteps()
        {
            if (activeAttempt <= 0)
            {
                return;
            }

            ModelInspectionFixtureAttemptDescriptor attempt = input.Attempts[activeAttempt - 1];
            while (nextServiceStep < attempt.ServiceSteps.Count &&
                   attempt.ServiceSteps[nextServiceStep].Trigger.Kind ==
                       ModelInspectionFixtureServiceTriggerKind.Automatic)
            {
                if (terminalReleased)
                {
                    throw Failure(source, "$.input.attempts[].serviceSteps[]", "setup.automatic-after-terminal");
                }

                ApplyEffect(attempt.ServiceSteps[nextServiceStep].Effect);
                nextServiceStep++;
            }
        }

        void ApplyEffect(ModelInspectionFixtureServiceEffectDescriptor effect)
        {
            if (effect.DeferredCheckpoint is { } deferred)
            {
                availableDeferredCheckpoints.Add((activeAttempt, deferred));
            }

            if (effect.Kind is ModelInspectionFixtureServiceEffectKind.Progress or
                ModelInspectionFixtureServiceEffectKind.Completed or
                ModelInspectionFixtureServiceEffectKind.Cancelled or
                ModelInspectionFixtureServiceEffectKind.OperationalFailure)
            {
                currentEffect = effect;
            }

            terminalReleased = effect.Kind is
                ModelInspectionFixtureServiceEffectKind.Completed or
                ModelInspectionFixtureServiceEffectKind.Cancelled or
                ModelInspectionFixtureServiceEffectKind.OperationalFailure;
        }

        void ReleaseDeferred(
            ModelInspectionFixtureSetupStepDescriptor deferredStep,
            ModelInspectionFixtureServiceEffectKind expectedKind)
        {
            ValidateDeferredSetup(
                source,
                deferredStep,
                deferredCheckpoints,
                expectedKind);
            (int Attempt, string Checkpoint) deferredKey =
                (deferredStep.Attempt!.Value, deferredStep.Checkpoint!);
            if (!availableDeferredCheckpoints.Contains(deferredKey))
            {
                throw Failure(source, "$.input.setupSteps[]", "setup.deferred-not-captured");
            }

            if (!obsoleteAttempts.Contains(deferredKey.Attempt))
            {
                throw Failure(source, "$.input.setupSteps[]", "setup.deferred-owner-active");
            }

            if (!releasedDeferredCheckpoints.Add(deferredKey))
            {
                throw Failure(source, "$.input.setupSteps[]", "setup.duplicate-stale-release");
            }
        }
    }

    private static void ValidateExpected(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionExpectedScreen expected,
        ModelInspectionFixturePolicyEntry policyEntry,
        IReadOnlyDictionary<string, string> copyRegistry)
    {
        Require(expected.Figma, source, "$.expected.figma", "expected.required");
        Require(expected.Outcome, source, "$.expected.outcome", "expected.required");
        Require(expected.Model, source, "$.expected.model", "expected.required");
        Require(expected.Content, source, "$.expected.content", "expected.required");
        Require(expected.Actions, source, "$.expected.actions", "expected.required");
        Require(expected.Footer, source, "$.expected.footer", "expected.required");
        Require(expected.Focus, source, "$.expected.focus", "expected.required");
        Require(expected.Automation, source, "$.expected.automation", "expected.required");
        Require(expected.Announcements, source, "$.expected.announcements", "expected.required");
        Require(expected.RowsAndScroll, source, "$.expected.rowsAndScroll", "expected.required");
        Require(expected.RetainedIdentities, source, "$.expected.retainedIdentities", "expected.required");

        if ((int)expected.Figma.State != (int)policyEntry.CanonicalFigmaState)
        {
            throw Failure(source, "$.expected.figma.state", "expected.canonical-state");
        }

        Require(expected.Footer.Rows, source, "$.expected.footer.rows", "expected.footer-required");
        if (expected.Footer.Rows.Count != OrderedStages.Length ||
            !expected.Footer.Rows.Select(row => (int)row.Stage)
                .SequenceEqual(Enumerable.Range(1, OrderedStages.Length)))
        {
            throw Failure(source, "$.expected.footer.rows", "expected.footer-five-stages");
        }

        Require(expected.Model.Metadata, source, "$.expected.model.metadata", "expected.rows-required");
        Require(expected.Model.Checks, source, "$.expected.model.checks", "expected.rows-required");
        Require(expected.Content.Rows, source, "$.expected.content.rows", "expected.rows-required");
        Require(expected.Actions.Items, source, "$.expected.actions.items", "expected.actions-required");
        Require(expected.Automation.Controls, source, "$.expected.automation.controls", "expected.controls-required");
        Require(expected.Announcements.Items, source, "$.expected.announcements.items", "expected.announcements-required");
        Require(expected.RowsAndScroll.OrderedRowIds, source, "$.expected.rowsAndScroll.orderedRowIds", "expected.rows-required");
        Require(expected.RetainedIdentities.Ids, source, "$.expected.retainedIdentities.ids", "expected.identities-required");
        if (expected.Announcements.Count != expected.Announcements.Items.Count)
        {
            throw Failure(source, "$.expected.announcements", "expected.announcement-count");
        }

        foreach (ModelInspectionExpectedCopy copy in EnumerateExpectedCopies(expected))
        {
            Require(copy, source, "$.expected", "expected.copy-null");
            if (!copyRegistry.TryGetValue(copy.CopyKey, out string? approved) ||
                !approved.Equals(copy.DefaultText, StringComparison.Ordinal))
            {
                throw Failure(source, "$.expected", "expected.copy-registry");
            }
        }

        ValidateWarningFinding(source, expected);
    }

    private static void ValidateWarningFinding(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionExpectedScreen expected)
    {
        if (expected.Outcome.Kind == ModelInspectionExpectedOutcomeKind.ReadyWithWarnings)
        {
            ModelInspectionExpectedContentRow? finding =
                expected.Content.Rows.Count == 1 ? expected.Content.Rows[0] : null;
            if (!expected.Content.Visible ||
                expected.Content.Mode != ModelInspectionExpectedContentMode.Warnings ||
                finding is null ||
                !string.Equals(finding.Id, MissingChatTemplateFindingId, StringComparison.Ordinal) ||
                finding.Status != ModelInspectionExpectedRowStatus.Warning ||
                !MatchesCopy(
                    finding.PrimaryText,
                    MissingChatTemplateTitleKey,
                    MissingChatTemplateTitle) ||
                !MatchesCopy(
                    finding.SecondaryText,
                    MissingChatTemplateDetailKey,
                    MissingChatTemplateDetail) ||
                !expected.RowsAndScroll.OrderedRowIds.SequenceEqual(
                    [MissingChatTemplateFindingId],
                    StringComparer.Ordinal))
            {
                throw Failure(source, "$.expected.content.rows", "expected.warning-finding");
            }

            return;
        }

        if (expected.Outcome.Kind == ModelInspectionExpectedOutcomeKind.Ready &&
            (expected.Content.Mode == ModelInspectionExpectedContentMode.Warnings ||
             expected.Content.Rows.Any(row =>
                 row.Status == ModelInspectionExpectedRowStatus.Warning ||
                 string.Equals(
                     row.Id,
                     MissingChatTemplateFindingId,
                     StringComparison.Ordinal))))
        {
            throw Failure(source, "$.expected.content.rows", "expected.ready-warning");
        }

        static bool MatchesCopy(
            ModelInspectionExpectedCopy? copy,
            string copyKey,
            string defaultText) =>
            copy is not null &&
            string.Equals(copy.CopyKey, copyKey, StringComparison.Ordinal) &&
            string.Equals(copy.DefaultText, defaultText, StringComparison.Ordinal);
    }

    private static void ValidatePresetExpectations(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureDescriptor descriptor,
        ModelInspectionFixturePolicyEntry policyEntry,
        ModelInspectionFixtureCoveragePolicy policy)
    {
        RequireUnique(descriptor.Presets, source, "$.presets", "fixture.duplicate-preset");
        if (!descriptor.Presets.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(policyEntry.RequiredPresets.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal) ||
            !descriptor.PresetExpectations.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(descriptor.Presets.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw Failure(source, "$.presetExpectations", "fixture.preset-set");
        }

        Dictionary<string, ModelInspectionFixturePreset> policyPresets = policy.Presets
            .ToDictionary(preset => preset.Id, StringComparer.Ordinal);
        foreach ((string id, ModelInspectionPresetExpectation expectation) in descriptor.PresetExpectations)
        {
            Require(expectation, source, "$.presetExpectations{}", "fixture.preset-expectation-null");
            if (!policyPresets.TryGetValue(id, out ModelInspectionFixturePreset? preset) ||
                expectation.Resources != preset.Resources ||
                expectation.TextScale != preset.Text ||
                expectation.Motion != preset.Motion ||
                expectation.MinimumContentColumnWidth < 0 ||
                expectation.MaximumContentColumnWidth < expectation.MinimumContentColumnWidth ||
                expectation.MinimumPointerTargetWidth < 44 ||
                expectation.MinimumPointerTargetHeight < 44 ||
                expectation.ExpectedAnimationStarts < 0)
            {
                throw Failure(source, "$.presetExpectations{}", "fixture.preset-expectation");
            }
        }
    }

    private static void ValidateInteractions(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureDescriptor descriptor,
        ModelInspectionFixtureCoveragePolicy policy)
    {
        IReadOnlyList<ModelInspectionFixtureInteraction> interactions = descriptor.Interactions;
        ModelInspectionFixtureInput input = descriptor.Input;
        HashSet<string> knownCheckpoints = input.Attempts
            .SelectMany(attempt => attempt.ServiceSteps)
            .SelectMany(step => new[] { step.Trigger.Checkpoint, step.Effect.DeferredCheckpoint })
            .Where(value => value is not null)
            .Select(value => value!)
            .Append(input.ObservationCheckpoint)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> policyIds = policy.Fixtures
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (ModelInspectionFixtureInteraction interaction in interactions)
        {
            Require(interaction, source, "$.interactions[]", "interaction.null");
            ValidateIdentifier(interaction.Id, source, "$.interactions[].id");
            ValidateIdentifier(interaction.SourceCheckpoint, source, "$.interactions[].sourceCheckpoint");
            if (!ids.Add(interaction.Id) || !knownCheckpoints.Contains(interaction.SourceCheckpoint))
            {
                throw Failure(source, "$.interactions[]", "interaction.scope");
            }

            if (!HasValidLifetimeEffect(interaction))
            {
                throw Failure(
                    source,
                    "$.interactions[].lifetimeEffect",
                    "interaction.lifetime-effect");
            }

            bool targetKnown = knownCheckpoints.Contains(interaction.Target) ||
                policyIds.Contains(interaction.Target) ||
                interaction.Target.Equals("gallery:no-active-fixture", StringComparison.Ordinal);
            if (!targetKnown || interaction.ExpectedAnnouncementCount < 0)
            {
                throw Failure(source, "$.interactions[].target", "interaction.dangling-target");
            }

            bool isObservationScoped = string.Equals(
                interaction.SourceCheckpoint,
                input.ObservationCheckpoint,
                StringComparison.Ordinal);
            if (interaction.Kind == ModelInspectionFixtureInteractionKind.Reset &&
                (!isObservationScoped ||
                 !string.Equals(interaction.Target, descriptor.Id, StringComparison.Ordinal)))
            {
                throw Failure(source, "$.interactions[].target", "interaction.reset-target");
            }

            if (isObservationScoped &&
                !IsAvailableAtExpectedScreen(interaction.Kind, descriptor.Expected))
            {
                throw Failure(source, "$.interactions[]", "interaction.unavailable");
            }
        }
    }

    private static bool IsAvailableAtExpectedScreen(
        ModelInspectionFixtureInteractionKind kind,
        ModelInspectionExpectedScreen expected) =>
        kind switch
        {
            ModelInspectionFixtureInteractionKind.Expand =>
                expected.Model.Visible &&
                !expected.Model.DisclosureExpanded &&
                expected.Figma.State is
                    ModelInspectionExpectedFigmaState.ReadyCollapsed or
                    ModelInspectionExpectedFigmaState.ReadyWithWarningsCollapsed or
                    ModelInspectionExpectedFigmaState.ConversionRequiredCollapsed or
                    ModelInspectionExpectedFigmaState.InvalidCollapsed,
            ModelInspectionFixtureInteractionKind.Collapse =>
                expected.Model.Visible &&
                expected.Model.DisclosureExpanded &&
                expected.Figma.State is
                    ModelInspectionExpectedFigmaState.ReadyExpanded or
                    ModelInspectionExpectedFigmaState.ReadyWithWarningsExpanded or
                    ModelInspectionExpectedFigmaState.ConversionRequiredExpanded or
                    ModelInspectionExpectedFigmaState.InvalidExpanded,
            ModelInspectionFixtureInteractionKind.Cancel =>
                expected.Figma.State == ModelInspectionExpectedFigmaState.InspectionProgress &&
                HasAvailableAction(expected.Actions, ModelInspectionExpectedActionMode.Inspecting, "cancel"),
            ModelInspectionFixtureInteractionKind.Retry =>
                expected.Figma.State == ModelInspectionExpectedFigmaState.OperationalFailure &&
                HasAvailableAction(expected.Actions, ModelInspectionExpectedActionMode.Result, "retry"),
            ModelInspectionFixtureInteractionKind.Restart =>
                expected.Figma.State == ModelInspectionExpectedFigmaState.Cancelled &&
                HasAvailableAction(expected.Actions, ModelInspectionExpectedActionMode.Result, "restart"),
            ModelInspectionFixtureInteractionKind.ChooseAnother =>
                expected.Figma.State != ModelInspectionExpectedFigmaState.InspectionProgress &&
                HasAvailableAction(
                    expected.Actions,
                    ModelInspectionExpectedActionMode.Result,
                    "choose-another"),
            ModelInspectionFixtureInteractionKind.Reset => true,
            _ => false
        };

    private static bool HasAvailableAction(
        ModelInspectionExpectedActionRegion actions,
        ModelInspectionExpectedActionMode requiredMode,
        string requiredId) =>
        actions.Visible &&
        actions.Mode == requiredMode &&
        actions.Items.Any(action =>
            action.Visible &&
            action.Enabled &&
            string.Equals(action.Id, requiredId, StringComparison.Ordinal));

    private static bool HasValidLifetimeEffect(
        ModelInspectionFixtureInteraction interaction) =>
        interaction.Kind switch
        {
            ModelInspectionFixtureInteractionKind.Expand or
            ModelInspectionFixtureInteractionKind.Collapse or
            ModelInspectionFixtureInteractionKind.Cancel or
            ModelInspectionFixtureInteractionKind.Retry or
            ModelInspectionFixtureInteractionKind.Restart =>
                interaction.LifetimeEffect == ModelInspectionFixtureInteractionLifetimeEffect.None,
            ModelInspectionFixtureInteractionKind.ChooseAnother =>
                interaction.LifetimeEffect ==
                    ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture,
            ModelInspectionFixtureInteractionKind.Reset =>
                interaction.LifetimeEffect ==
                    ModelInspectionFixtureInteractionLifetimeEffect.RetirePage,
            _ => false
        };

    private static void ValidateCurrentScreenConsistency(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureDescriptor descriptor,
        ModelInspectionFixtureReplayState replay)
    {
        if (descriptor.Expected.Footer.Rows.Count(row =>
                row.Status == ModelInspectionExpectedFooterStatus.InProgress) > 1)
        {
            throw Failure(source, "$.expected.footer.rows", "expected.multiple-active-stages");
        }

        ModelInspectionFixtureServiceEffectDescriptor? effect = replay.CurrentEffect;
        if (effect is null || effect.Kind == ModelInspectionFixtureServiceEffectKind.Progress)
        {
            if (descriptor.Expected.Figma.State !=
                    ModelInspectionExpectedFigmaState.InspectionProgress ||
                descriptor.Expected.Outcome.Visible ||
                descriptor.Expected.Outcome.Kind != ModelInspectionExpectedOutcomeKind.Hidden ||
                descriptor.Expected.Footer.Status != ModelInspectionExpectedFooterStatus.InProgress)
            {
                throw Failure(source, "$.expected", "fixture.current-progress-screen");
            }

            return;
        }

        if (descriptor.Expected.Footer.Status == ModelInspectionExpectedFooterStatus.InProgress ||
            descriptor.Expected.Footer.Rows.Any(row =>
                row.Status == ModelInspectionExpectedFooterStatus.InProgress))
        {
            throw Failure(source, "$.expected.footer", "fixture.current-terminal-footer");
        }

        ModelInspectionExpectedOutcomeKind expectedOutcome;
        bool figmaMatches;
        switch (effect.Kind)
        {
            case ModelInspectionFixtureServiceEffectKind.Completed:
                (expectedOutcome, figmaMatches) = effect.Outcome switch
                {
                    ModelInspectionFixtureOutcome.Ready =>
                        (ModelInspectionExpectedOutcomeKind.Ready,
                         descriptor.Expected.Figma.State is
                             ModelInspectionExpectedFigmaState.ReadyCollapsed or
                             ModelInspectionExpectedFigmaState.ReadyExpanded),
                    ModelInspectionFixtureOutcome.ReadyWithWarnings =>
                        (ModelInspectionExpectedOutcomeKind.ReadyWithWarnings,
                         descriptor.Expected.Figma.State is
                             ModelInspectionExpectedFigmaState.ReadyWithWarningsCollapsed or
                             ModelInspectionExpectedFigmaState.ReadyWithWarningsExpanded),
                    ModelInspectionFixtureOutcome.ConversionRequired =>
                        (ModelInspectionExpectedOutcomeKind.ConversionRequired,
                         descriptor.Expected.Figma.State is
                             ModelInspectionExpectedFigmaState.ConversionRequiredCollapsed or
                             ModelInspectionExpectedFigmaState.ConversionRequiredExpanded),
                    ModelInspectionFixtureOutcome.IncompletePackage =>
                        (ModelInspectionExpectedOutcomeKind.IncompletePackage,
                         descriptor.Expected.Figma.State ==
                             ModelInspectionExpectedFigmaState.IncompletePackage),
                    ModelInspectionFixtureOutcome.Unsupported =>
                        (ModelInspectionExpectedOutcomeKind.Unsupported,
                         descriptor.Expected.Figma.State ==
                             ModelInspectionExpectedFigmaState.Unsupported),
                    ModelInspectionFixtureOutcome.Invalid =>
                        (ModelInspectionExpectedOutcomeKind.Invalid,
                         descriptor.Expected.Figma.State is
                             ModelInspectionExpectedFigmaState.InvalidCollapsed or
                             ModelInspectionExpectedFigmaState.InvalidExpanded),
                    _ => throw Failure(source, "$.input", "fixture.current-terminal-outcome")
                };
                break;

            case ModelInspectionFixtureServiceEffectKind.Cancelled:
                expectedOutcome = ModelInspectionExpectedOutcomeKind.Cancelled;
                figmaMatches = descriptor.Expected.Figma.State ==
                    ModelInspectionExpectedFigmaState.Cancelled;
                break;

            case ModelInspectionFixtureServiceEffectKind.OperationalFailure:
                expectedOutcome = ModelInspectionExpectedOutcomeKind.OperationalFailure;
                figmaMatches = descriptor.Expected.Figma.State ==
                    ModelInspectionExpectedFigmaState.OperationalFailure;
                break;

            default:
                return;
        }

        if (!figmaMatches || descriptor.Expected.Outcome.Kind != expectedOutcome)
        {
            throw Failure(source, "$.expected", "fixture.current-terminal-screen");
        }
    }

    private static void ValidateTargetPairs(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureCoveragePolicy policy)
    {
        Dictionary<string, ModelInspectionFixturePolicyEntry> byId = policy.Fixtures
            .ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        HashSet<(string, string)> declaredPairs = policy.DisclosurePairs
            .Select(pair => (pair.CollapsedId, pair.ExpandedId))
            .ToHashSet();
        foreach (IGrouping<string, ModelInspectionFixturePolicyEntry> group in
                 policy.Fixtures.GroupBy(entry => entry.TargetCondition, StringComparer.Ordinal))
        {
            ModelInspectionFixturePolicyEntry[] entries = group.ToArray();
            if (entries.Length == 1)
            {
                if (entries[0].PairedWithId is not null)
                {
                    throw Failure(source, "$.fixtures[].pairedWithId", "policy.dangling-pair");
                }

                continue;
            }

            if (entries.Length != 2 ||
                entries.Any(entry => entry.Variant is null || entry.PairedWithId is null) ||
                entries[0].Variant!.Equals(entries[1].Variant, StringComparison.Ordinal) ||
                !entries[0].PairedWithId!.Equals(entries[1].Id, StringComparison.Ordinal) ||
                !entries[1].PairedWithId!.Equals(entries[0].Id, StringComparison.Ordinal) ||
                !(declaredPairs.Contains((entries[0].Id, entries[1].Id)) ||
                  declaredPairs.Contains((entries[1].Id, entries[0].Id))))
            {
                throw Failure(source, "$.fixtures", "policy.duplicate-target-unpaired");
            }
        }

        foreach (ModelInspectionFixtureDisclosurePair pair in policy.DisclosurePairs)
        {
            if (!byId.ContainsKey(pair.CollapsedId) || !byId.ContainsKey(pair.ExpandedId) ||
                pair.CollapsedId.Equals(pair.ExpandedId, StringComparison.Ordinal))
            {
                throw Failure(source, "$.disclosurePairs[]", "policy.dangling-pair");
            }
        }
    }

    private static void ValidateGallerySwitchPairs(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureCoveragePolicy policy,
        IReadOnlySet<string> ids)
    {
        foreach (ModelInspectionFixtureGallerySwitchPair pair in policy.GallerySwitchPairs)
        {
            if (!ids.Contains(pair.SourceId) ||
                !ids.Contains(pair.DestinationId) ||
                pair.SourceId.Equals(pair.DestinationId, StringComparison.Ordinal))
            {
                throw Failure(source, "$.gallerySwitchPairs[]", "policy.gallery-switch-pair");
            }
        }
    }

    private static void ValidateCopyRegistry(
        ModelInspectionFixtureDocumentSource source,
        IReadOnlyDictionary<string, string> registry)
    {
        if (registry.Count > StrictModelInspectionFixtureJson.MaximumCollectionLength)
        {
            throw Failure(source, "$.copyRegistry", "policy.copy-registry-limit");
        }

        foreach ((string key, string value) in registry)
        {
            if (!CopyKeyRegex().IsMatch(key) || string.IsNullOrWhiteSpace(value))
            {
                throw Failure(source, "$.copyRegistry{}", "policy.copy-registry-entry");
            }
        }
    }

    private static void ValidateExternalLinks(
        ModelInspectionFixtureDocumentSource source,
        IReadOnlyList<ModelInspectionFixtureExternalEvidenceLink> links,
        IReadOnlySet<string> ids)
    {
        foreach (ModelInspectionFixtureExternalEvidenceLink link in links)
        {
            if (!ids.Contains(link.FixtureId) ||
                string.IsNullOrWhiteSpace(link.EvidenceId) ||
                string.IsNullOrWhiteSpace(link.SourcePath) ||
                link.SourcePath.StartsWith('/') ||
                link.SourcePath.StartsWith('\\') ||
                link.SourcePath.Contains('\\') ||
                link.SourcePath.EndsWith('/') ||
                link.SourcePath.Contains("//", StringComparison.Ordinal) ||
                link.SourcePath.Contains("..", StringComparison.Ordinal) ||
                link.SourcePath.Contains("://", StringComparison.Ordinal) ||
                Path.IsPathRooted(link.SourcePath))
            {
                throw Failure(source, "$.externalEvidenceLinks[]", "policy.external-evidence-link");
            }
        }
    }

    private static IEnumerable<ModelInspectionExpectedCopy> EnumerateExpectedCopies(
        ModelInspectionExpectedScreen expected)
    {
        IEnumerable<ModelInspectionExpectedCopy?> copies =
        [
            expected.Outcome.Badge,
            expected.Outcome.Title,
            expected.Outcome.SupportingText,
            expected.Model.DisplayName,
            expected.Model.DisplayFileName,
            expected.Content.Heading
        ];
        foreach (ModelInspectionExpectedCopy copy in copies.Where(copy => copy is not null)!)
        {
            yield return copy;
        }

        foreach (ModelInspectionExpectedMetadataField field in expected.Model.Metadata)
        {
            yield return field.Label;
            yield return field.Value;
        }

        foreach (ModelInspectionExpectedCheckRow row in expected.Model.Checks)
        {
            yield return row.Text;
        }

        foreach (ModelInspectionExpectedContentRow row in expected.Content.Rows)
        {
            yield return row.PrimaryText;
            if (row.SecondaryText is not null)
            {
                yield return row.SecondaryText;
            }
        }

        foreach (ModelInspectionExpectedAction action in expected.Actions.Items)
        {
            yield return action.Label;
            if (action.HelpText is not null)
            {
                yield return action.HelpText;
            }
        }

        foreach (ModelInspectionExpectedAutomationControl control in expected.Automation.Controls)
        {
            yield return control.AccessibleName;
            if (control.HelpText is not null)
            {
                yield return control.HelpText;
            }
        }

        foreach (ModelInspectionExpectedCopy announcement in expected.Announcements.Items)
        {
            yield return announcement;
        }
    }

    private static void ValidateObjectGraph(
        ModelInspectionFixtureDocumentSource source,
        object root,
        bool allowRepositorySourcePaths)
    {
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        Visit(root, "$", visited);

        void Visit(object? value, string path, ISet<object> seen)
        {
            if (value is null || value is Enum || value is bool ||
                value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal)
            {
                return;
            }

            if (value is string text)
            {
                ValidateSafeText(
                    text,
                    source,
                    path,
                    StrictModelInspectionFixtureJson.MaximumStringLength,
                    allowRepositorySourcePaths && path.EndsWith(".SourcePath", StringComparison.Ordinal),
                    path.StartsWith("$.Interactions[", StringComparison.Ordinal) &&
                    path.EndsWith(".Target", StringComparison.Ordinal));
                return;
            }

            if (value is double number)
            {
                if (!double.IsFinite(number))
                {
                    throw Failure(source, path, "value.nonfinite");
                }

                return;
            }

            if (value is float single)
            {
                if (!float.IsFinite(single))
                {
                    throw Failure(source, path, "value.nonfinite");
                }

                return;
            }

            if (!value.GetType().IsValueType && !seen.Add(value))
            {
                return;
            }

            if (value is IDictionary dictionary)
            {
                if (dictionary.Count > StrictModelInspectionFixtureJson.MaximumCollectionLength)
                {
                    throw Failure(source, path, "value.collection-limit");
                }

                int index = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is null || entry.Value is null)
                    {
                        throw Failure(source, $"{path}[{index}]", "value.null-element");
                    }

                    Visit(entry.Key, $"{path}.key[{index}]", seen);
                    Visit(entry.Value, $"{path}.value[{index}]", seen);
                    index++;
                }

                return;
            }

            if (value is IEnumerable enumerable)
            {
                int index = 0;
                foreach (object? item in enumerable)
                {
                    if (index >= StrictModelInspectionFixtureJson.MaximumCollectionLength)
                    {
                        throw Failure(source, path, "value.collection-limit");
                    }

                    if (item is null)
                    {
                        throw Failure(source, $"{path}[{index}]", "value.null-element");
                    }

                    Visit(item, $"{path}[{index}]", seen);
                    index++;
                }

                return;
            }

            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0)
                {
                    Visit(property.GetValue(value), $"{path}.{property.Name}", seen);
                }
            }
        }
    }

    private static void ValidateSafeText(
        string text,
        ModelInspectionFixtureDocumentSource source,
        string path,
        int maximumLength,
        bool allowRepositoryPath,
        bool allowInternalTransitionReference)
    {
        if (text.Length > maximumLength || !text.IsNormalized(NormalizationForm.FormC))
        {
            throw Failure(source, path, "value.text-boundary");
        }

        foreach (Rune rune in text.EnumerateRunes())
        {
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.PrivateUse or
                UnicodeCategory.OtherNotAssigned or
                UnicodeCategory.Surrogate)
            {
                throw Failure(source, path, "value.unsafe-scalar");
            }
        }

        string userName = Environment.UserName;
        string machineName = Environment.MachineName;
        bool environmentIdentity =
            (!string.IsNullOrEmpty(userName) &&
             text.Contains(userName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(machineName) &&
             text.Contains(machineName, StringComparison.OrdinalIgnoreCase));
        if (text.StartsWith('/') ||
            text.StartsWith('\\') ||
            (!allowRepositoryPath && text.IndexOfAny(['/', '\\']) >= 0) ||
            IsUnsafeUri(text, allowInternalTransitionReference) ||
            DriveTokenRegex().IsMatch(text) ||
            IdentityTokenRegex().IsMatch(text) ||
            environmentIdentity)
        {
            throw Failure(source, path, "value.path-or-identity");
        }
    }

    private static void ValidateDisplayFileName(
        string? value,
        ModelInspectionFixtureDocumentSource source)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > StrictModelInspectionFixtureJson.MaximumDisplayFileNameLength ||
            value.IndexOfAny(['/', '\\', ':']) >= 0 ||
            !DisplayFileNameRegex().IsMatch(value))
        {
            throw Failure(source, "$.input.request.displayFileName", "request.display-filename");
        }
    }

    private static bool IsUnsafeUri(
        string text,
        bool allowInternalTransitionReference)
    {
        MatchCollection schemeTokens = UriSchemeTokenRegex().Matches(text);
        if (schemeTokens.Count == 0)
        {
            return false;
        }

        return !allowInternalTransitionReference ||
            schemeTokens.Count != 1 ||
            schemeTokens[0].Index != 0 ||
            !(text.StartsWith("gallery:", StringComparison.Ordinal) ||
              text.StartsWith("fixture:", StringComparison.Ordinal) ||
              text.StartsWith("step:", StringComparison.Ordinal));
    }

    private static void ValidateId(
        string? id,
        ModelInspectionFixtureDocumentSource source,
        string path)
    {
        if (id is null || !IdRegex().IsMatch(id) || id.Equals("MI-000", StringComparison.Ordinal))
        {
            throw Failure(source, path, "fixture.id");
        }
    }

    private static void ValidateSlug(
        string? slug,
        ModelInspectionFixtureDocumentSource source,
        string path)
    {
        if (slug is null || !SlugRegex().IsMatch(slug))
        {
            throw Failure(source, path, "fixture.slug");
        }
    }

    private static void ValidateIdentifier(
        string? value,
        ModelInspectionFixtureDocumentSource source,
        string path)
    {
        if (value is null || !IdentifierRegex().IsMatch(value))
        {
            throw Failure(source, path, "value.identifier");
        }
    }

    private static void RequireAttemptCheckpointOnly(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureSetupStepDescriptor step)
    {
        if (step.Attempt is null || step.Attempt <= 0 ||
            step.InteractionId is not null)
        {
            throw Failure(source, "$.input.setupSteps[]", "setup.release-payload");
        }

        ValidateIdentifier(step.Checkpoint, source, "$.input.setupSteps[].checkpoint");
    }

    private static void RequireInteractionOnly(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureSetupStepDescriptor step)
    {
        if (step.Attempt is not null || step.Checkpoint is not null)
        {
            throw Failure(source, "$.input.setupSteps[]", "setup.interaction-payload");
        }

        ValidateIdentifier(step.InteractionId, source, "$.input.setupSteps[].interactionId");
    }

    private static void ValidateDeferredSetup(
        ModelInspectionFixtureDocumentSource source,
        ModelInspectionFixtureSetupStepDescriptor step,
        IReadOnlyDictionary<(int Attempt, string Checkpoint), ModelInspectionFixtureServiceEffectKind> deferred,
        ModelInspectionFixtureServiceEffectKind expectedKind)
    {
        RequireAttemptCheckpointOnly(source, step);
        if (!deferred.TryGetValue((step.Attempt!.Value, step.Checkpoint!), out ModelInspectionFixtureServiceEffectKind actual) ||
            actual != expectedKind)
        {
            throw Failure(source, "$.input.setupSteps[]", "setup.unknown-deferred-checkpoint");
        }
    }

    private static bool MatchesSetupKind(
        ModelInspectionFixtureSetupStepKind setup,
        ModelInspectionFixtureInteractionKind interaction) =>
        setup switch
        {
            ModelInspectionFixtureSetupStepKind.InvokeDisclosure => interaction is
                ModelInspectionFixtureInteractionKind.Expand or
                ModelInspectionFixtureInteractionKind.Collapse,
            ModelInspectionFixtureSetupStepKind.InvokeCancel => interaction == ModelInspectionFixtureInteractionKind.Cancel,
            ModelInspectionFixtureSetupStepKind.InvokeRetry => interaction == ModelInspectionFixtureInteractionKind.Retry,
            ModelInspectionFixtureSetupStepKind.InvokeRestart => interaction == ModelInspectionFixtureInteractionKind.Restart,
            ModelInspectionFixtureSetupStepKind.InvokeChooseAnother => interaction == ModelInspectionFixtureInteractionKind.ChooseAnother,
            _ => false
        };

    private static void Require<T>(
        T? value,
        ModelInspectionFixtureDocumentSource source,
        string path,
        string ruleCode)
        where T : class
    {
        if (value is null)
        {
            throw Failure(source, path, ruleCode);
        }
    }

    private static void RequireUnique<T>(
        IReadOnlyList<T> values,
        ModelInspectionFixtureDocumentSource source,
        string path,
        string ruleCode)
    {
        if (values.Count != values.Distinct().Count())
        {
            throw Failure(source, path, ruleCode);
        }
    }

    private static ModelInspectionFixtureValidationException Failure(
        ModelInspectionFixtureDocumentSource source,
        string path,
        string ruleCode) =>
        StrictModelInspectionFixtureJson.Failure(source, path, ruleCode);

    private static ModelInspectionFixtureCoveragePolicy Freeze(
        ModelInspectionFixtureCoveragePolicy policy) =>
        policy with
        {
            Fixtures = policy.Fixtures.Select(entry => entry with
            {
                RequiredCoverageTags = entry.RequiredCoverageTags.ToImmutableArray(),
                RequiredInteractions = entry.RequiredInteractions.ToImmutableArray(),
                RequiredPresets = entry.RequiredPresets.ToImmutableArray()
            }).ToImmutableArray(),
            Presets = policy.Presets.ToImmutableArray(),
            DisclosurePairs = policy.DisclosurePairs.ToImmutableArray(),
            GallerySwitchPairs = policy.GallerySwitchPairs.ToImmutableArray(),
            CopyRegistry = policy.CopyRegistry.ToImmutableDictionary(StringComparer.Ordinal),
            ExternalEvidenceLinks = policy.ExternalEvidenceLinks.ToImmutableArray()
        };

    private static ModelInspectionFixtureDescriptor Freeze(
        ModelInspectionFixtureDescriptor descriptor)
    {
        ModelInspectionFixtureCoverage coverage = descriptor.Coverage with
        {
            FigmaStates = descriptor.Coverage.FigmaStates.ToImmutableArray(),
            Stages = descriptor.Coverage.Stages.ToImmutableArray(),
            StageStatuses = descriptor.Coverage.StageStatuses.ToImmutableArray(),
            Outcomes = descriptor.Coverage.Outcomes.ToImmutableArray(),
            Interactions = descriptor.Coverage.Interactions.ToImmutableArray(),
            LifecycleTags = descriptor.Coverage.LifecycleTags.ToImmutableArray(),
            FailureProfiles = descriptor.Coverage.FailureProfiles.ToImmutableArray(),
            StressTags = descriptor.Coverage.StressTags.ToImmutableArray()
        };
        ModelInspectionFixtureInput input = descriptor.Input with
        {
            Attempts = descriptor.Input.Attempts.Select(attempt => attempt with
            {
                ServiceSteps = attempt.ServiceSteps.ToImmutableArray()
            }).ToImmutableArray(),
            SetupSteps = descriptor.Input.SetupSteps.ToImmutableArray()
        };
        ModelInspectionExpectedScreen expected = Freeze(descriptor.Expected);
        return descriptor with
        {
            Coverage = coverage,
            Input = input,
            Expected = expected,
            PresetExpectations = descriptor.PresetExpectations.ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value with
                {
                    TextRoles = pair.Value.TextRoles.ToImmutableArray(),
                    LogicalReadingOrder = pair.Value.LogicalReadingOrder.ToImmutableArray(),
                    TabOrder = pair.Value.TabOrder.ToImmutableArray()
                },
                StringComparer.Ordinal),
            Interactions = descriptor.Interactions.ToImmutableArray(),
            Presets = descriptor.Presets.ToImmutableArray()
        };
    }

    private static ModelInspectionExpectedScreen Freeze(ModelInspectionExpectedScreen expected) =>
        expected with
        {
            Model = expected.Model with
            {
                Metadata = expected.Model.Metadata.ToImmutableArray(),
                Checks = expected.Model.Checks.ToImmutableArray()
            },
            Content = expected.Content with { Rows = expected.Content.Rows.ToImmutableArray() },
            Actions = expected.Actions with { Items = expected.Actions.Items.ToImmutableArray() },
            Footer = expected.Footer with { Rows = expected.Footer.Rows.ToImmutableArray() },
            Automation = expected.Automation with { Controls = expected.Automation.Controls.ToImmutableArray() },
            Announcements = expected.Announcements with { Items = expected.Announcements.Items.ToImmutableArray() },
            RowsAndScroll = expected.RowsAndScroll with
            {
                OrderedRowIds = expected.RowsAndScroll.OrderedRowIds.ToImmutableArray()
            },
            RetainedIdentities = expected.RetainedIdentities with
            {
                Ids = expected.RetainedIdentities.Ids.ToImmutableArray()
            }
        };

    private sealed record ModelInspectionFixtureReplayState(
        ModelInspectionFixtureServiceEffectDescriptor? CurrentEffect);

    [GeneratedRegex(@"^MI-[0-9]{3}-[a-z0-9]+(?:-[a-z0-9]+)*\.fixture\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex FixtureFileNameRegex();

    [GeneratedRegex(@"^MI-[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:[-:.][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"^P0[1-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex PresetIdRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\.gguf$", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayFileNameRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CopyKeyRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9+.-])[A-Za-z][A-Za-z0-9+.-]*:",
        RegexOptions.CultureInvariant)]
    private static partial Regex UriSchemeTokenRegex();

    [GeneratedRegex(@"^[A-Za-z]:", RegexOptions.CultureInvariant)]
    private static partial Regex DriveTokenRegex();

    [GeneratedRegex(@"(%[^%]+%|\$\{[^}]+\}|\$env:|username|userprofile|computername|machine[_-]?name)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdentityTokenRegex();
}
