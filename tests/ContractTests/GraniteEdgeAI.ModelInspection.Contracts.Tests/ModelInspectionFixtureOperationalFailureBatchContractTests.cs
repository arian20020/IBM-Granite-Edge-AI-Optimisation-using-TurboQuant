using System.Text;
using System.Text.Json.Nodes;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureOperationalFailureBatchContractTests
{
    private static readonly FailureOracle[] Oracles =
    [
        new(
            "MI-039",
            "MI-039-operational-failure-worker-timeout.fixture.json",
            "operational-failure-worker-timeout",
            ModelInspectionFixtureFailureProfile.WorkerTimeout,
            "failure.worker-timeout",
            "fixture.failure.worker-timeout",
            "The inspection worker did not respond in time."),
        new(
            "MI-040",
            "MI-040-operational-failure-worker-crash-early-exit.fixture.json",
            "operational-failure-worker-crash-early-exit",
            ModelInspectionFixtureFailureProfile.WorkerCrashEarlyExit,
            "failure.worker-crash-early-exit",
            "fixture.failure.worker-crash-early-exit",
            "The inspection worker exited before completing."),
        new(
            "MI-041",
            "MI-041-operational-failure-malformed-worker-response.fixture.json",
            "operational-failure-malformed-worker-response",
            ModelInspectionFixtureFailureProfile.MalformedWorkerResponse,
            "failure.malformed-worker-response",
            "fixture.failure.malformed-worker-response",
            "The inspection worker returned an invalid response."),
        new(
            "MI-042",
            "MI-042-operational-failure-cancellation-unconfirmed.fixture.json",
            "operational-failure-cancellation-unconfirmed",
            ModelInspectionFixtureFailureProfile.CancellationUnconfirmed,
            "failure.cancellation-unconfirmed",
            "fixture.failure.cancellation-unconfirmed",
            "Cancellation could not be confirmed safely.")
    ];

    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void OperationalFailureBatchDescriptorsMatchIndependentPerIdOracle()
    {
        string[] missing = Oracles
            .Where(oracle => !File.Exists(Path.Combine(
                ScenarioDirectory(), oracle.FileName)))
            .Select(oracle => oracle.Id)
            .ToArray();

        Assert.AreEqual(
            0,
            missing.Length,
            $"Missing operational-failure batch descriptors ({missing.Length}): " +
            string.Join(", ", missing));

        FailureBatch batch = LoadFailureBatch();
        Assert.AreEqual(42, batch.Catalogue.Fixtures.Count);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 42).Select(value => $"MI-{value:000}").ToArray(),
            batch.Catalogue.Fixtures.Select(fixture => fixture.Id).ToArray());

        foreach (FailureOracle oracle in Oracles)
        {
            ModelInspectionFixturePolicyEntry policyEntry =
                batch.Policy.Value.Fixtures.Single(entry => entry.Id == oracle.Id);
            ValidatedModelInspectionFixture fixture = batch.Catalogue.Fixtures.Single(
                candidate => candidate.Id == oracle.Id);
            AssertPolicyEntry(policyEntry, oracle);
            AssertFixture(fixture, batch.Documents[oracle.Id], oracle);
        }

        ModelInspectionFixtureFailureProfile[] profiles = Oracles
            .Select(oracle => oracle.FailureProfile)
            .ToArray();
        Assert.HasCount(4, profiles.Distinct().ToArray());
        Assert.IsFalse(profiles.Contains(
            ModelInspectionFixtureFailureProfile.WorkerStartFailure));
    }

    [TestMethod]
    public void OperationalFailureBatchLoaderValidatesFortyTwoAndReportsOnlySevenLaterIdsMissing()
    {
        FailureBatch batch = LoadFailureBatch();
        Assert.AreEqual(42, batch.Catalogue.Fixtures.Count);

        string[] fixtureFiles =
            ModelInspectionFixtureCatalogueContractTests.ExpectedFixtureFileNames
            .Take(42)
            .ToArray();
        Assert.HasCount(42, fixtureFiles);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 42)
                .Select(value => $"MI-{value:000}")
                .ToArray(),
            fixtureFiles.Select(fileName => fileName[..6]).ToArray());
        CollectionAssert.AreEqual(
            Enumerable.Range(43, 7)
                .Select(value => $"MI-{value:000}")
                .ToArray(),
            ModelInspectionFixtureCoverageValidator
                .GetMissingFixtureIds(fixtureFiles)
                .ToArray());
    }

    [TestMethod]
    public void OperationalFailureOracleRejectsJointProfileTagSwapAndDuplicate()
    {
        FailureBatch swapped = LoadMutatedFailureBatch((policy, descriptors) =>
        {
            ApplyFailureProfile(policy, descriptors, Oracles[0], Oracles[1]);
            ApplyFailureProfile(policy, descriptors, Oracles[1], Oracles[0]);
        });
        Assert.ThrowsExactly<AssertFailedException>(() => AssertFixtureAndPolicy(
            swapped,
            Oracles[0]));

        FailureBatch duplicated = LoadMutatedFailureBatch((policy, descriptors) =>
            ApplyFailureProfile(policy, descriptors, Oracles[0], Oracles[1]));
        Assert.ThrowsExactly<AssertFailedException>(() => AssertFixtureAndPolicy(
            duplicated,
            Oracles[0]));
    }

    private static void AssertFixtureAndPolicy(
        FailureBatch batch,
        FailureOracle oracle)
    {
        AssertPolicyEntry(
            batch.Policy.Value.Fixtures.Single(entry => entry.Id == oracle.Id),
            oracle);
        AssertFixture(
            batch.Catalogue.Fixtures.Single(fixture => fixture.Id == oracle.Id),
            batch.Documents[oracle.Id],
            oracle);
    }

    private static void AssertPolicyEntry(
        ModelInspectionFixturePolicyEntry entry,
        FailureOracle oracle)
    {
        Assert.AreEqual(oracle.Id, entry.Id, oracle.Id);
        Assert.AreEqual(oracle.FileName, entry.FileName, oracle.Id);
        Assert.AreEqual(oracle.TargetCondition, entry.TargetCondition, oracle.Id);
        Assert.IsNull(entry.Variant, oracle.Id);
        Assert.IsNull(entry.PairedWithId, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureFigmaState.OperationalFailure,
            entry.CanonicalFigmaState, oracle.Id);
        CollectionAssert.AreEqual(
            new[] { "figma.operational-failure", oracle.FailureCoverageTag },
            entry.RequiredCoverageTags.ToArray(),
            oracle.Id);
        CollectionAssert.AreEqual(
            new[]
            {
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            },
            entry.RequiredInteractions.ToArray(),
            oracle.Id);
        CollectionAssert.AreEqual(new[] { "P01" },
            entry.RequiredPresets.ToArray(), oracle.Id);
    }

    private static void AssertFixture(
        ValidatedModelInspectionFixture fixture,
        JsonObject document,
        FailureOracle oracle)
    {
        Assert.AreEqual(oracle.FileName, fixture.FileName, oracle.Id);
        Assert.AreEqual(oracle.Id, fixture.Id, oracle.Id);
        Assert.AreEqual(oracle.TargetCondition, fixture.TargetCondition, oracle.Id);
        Assert.IsNull(fixture.Variant, oracle.Id);
        Assert.AreEqual($"{oracle.Id} {oracle.TargetCondition.Replace('-', ' ')}",
            fixture.Title, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureCategory.Failure,
            fixture.Category, oracle.Id);

        CollectionAssert.AreEqual(
            new[] { ModelInspectionFixtureFigmaState.OperationalFailure },
            fixture.Coverage.FigmaStates.ToArray(), oracle.Id);
        Assert.HasCount(0, fixture.Coverage.Stages, oracle.Id);
        Assert.HasCount(0, fixture.Coverage.StageStatuses, oracle.Id);
        Assert.HasCount(0, fixture.Coverage.Outcomes, oracle.Id);
        CollectionAssert.AreEqual(
            new[]
            {
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            },
            fixture.Coverage.Interactions.ToArray(), oracle.Id);
        Assert.HasCount(0, fixture.Coverage.LifecycleTags, oracle.Id);
        CollectionAssert.AreEqual(new[] { oracle.FailureProfile },
            fixture.Coverage.FailureProfiles.ToArray(), oracle.Id);
        Assert.HasCount(0, fixture.Coverage.StressTags, oracle.Id);
        CollectionAssert.AreEqual(new[] { "P01" }, fixture.Presets.ToArray(), oracle.Id);
        CollectionAssert.AreEquivalent(new[] { "P01" },
            fixture.PresetExpectations.Keys.ToArray(), oracle.Id);

        AssertClosedSafeInput(fixture, document, oracle);
        AssertExpectedScreen(fixture.Expected, oracle);
        AssertPreset(fixture.PresetExpectations["P01"], oracle.Id);
        AssertInteractions(fixture, oracle);
    }

    private static void AssertClosedSafeInput(
        ValidatedModelInspectionFixture fixture,
        JsonObject document,
        FailureOracle oracle)
    {
        Assert.AreEqual("Granite Fixture Model",
            fixture.Input.Request.DisplayName, oracle.Id);
        Assert.AreEqual("granite-fixture.gguf",
            fixture.Input.Request.DisplayFileName, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureEvidenceProfile.Compatible,
            fixture.Input.Request.EvidenceProfile, oracle.Id);
        Assert.HasCount(2, fixture.Input.Attempts, oracle.Id);
        ModelInspectionFixtureAttemptDescriptor attempt1 = fixture.Input.Attempts[0];
        Assert.AreEqual(1, attempt1.Attempt, oracle.Id);
        Assert.HasCount(1, attempt1.ServiceSteps, oracle.Id);
        ModelInspectionFixtureServiceStepDescriptor step = attempt1.ServiceSteps[0];
        Assert.AreEqual(ModelInspectionFixtureServiceTriggerKind.Checkpoint,
            step.Trigger.Kind, oracle.Id);
        Assert.AreEqual("terminal", step.Trigger.Checkpoint, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.OperationalFailure,
            step.Effect.Kind, oracle.Id);
        Assert.IsNull(step.Effect.Progress, oracle.Id);
        Assert.IsNull(step.Effect.Outcome, oracle.Id);
        Assert.IsNull(step.Effect.EvidenceProfile, oracle.Id);
        Assert.AreEqual(oracle.FailureProfile, step.Effect.FailureProfile, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureFailureDetailProfile.Default,
            step.Effect.FailureDetailProfile, oracle.Id);
        Assert.IsNull(step.Effect.DeferredCheckpoint, oracle.Id);
        Assert.AreEqual(1, attempt1.ServiceSteps.Count(serviceStep =>
            serviceStep.Effect.Kind is
                ModelInspectionFixtureServiceEffectKind.Completed or
                ModelInspectionFixtureServiceEffectKind.Cancelled or
                ModelInspectionFixtureServiceEffectKind.OperationalFailure), oracle.Id);

        ModelInspectionFixtureAttemptDescriptor attempt2 = fixture.Input.Attempts[1];
        Assert.AreEqual(2, attempt2.Attempt, oracle.Id);
        Assert.HasCount(1, attempt2.ServiceSteps, oracle.Id);
        ModelInspectionFixtureServiceStepDescriptor completed =
            attempt2.ServiceSteps[0];
        Assert.AreEqual(ModelInspectionFixtureServiceTriggerKind.Checkpoint,
            completed.Trigger.Kind, oracle.Id);
        Assert.AreEqual("attempt-2-ready", completed.Trigger.Checkpoint, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Completed,
            completed.Effect.Kind, oracle.Id);
        Assert.IsNull(completed.Effect.Progress, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureOutcome.Ready,
            completed.Effect.Outcome, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureEvidenceProfile.Compatible,
            completed.Effect.EvidenceProfile, oracle.Id);
        Assert.IsNull(completed.Effect.FailureProfile, oracle.Id);
        Assert.IsNull(completed.Effect.FailureDetailProfile, oracle.Id);
        Assert.IsNull(completed.Effect.DeferredCheckpoint, oracle.Id);

        Assert.HasCount(2, fixture.Input.SetupSteps, oracle.Id);
        AssertSetup(fixture.Input.SetupSteps[0],
            ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint,
            1, "terminal", null, oracle.Id);
        AssertSetup(fixture.Input.SetupSteps[1],
            ModelInspectionFixtureSetupStepKind.Observe,
            null, "observed", null, oracle.Id);
        Assert.AreEqual("observed", fixture.Input.ObservationCheckpoint, oracle.Id);

        JsonObject input = document["input"]!.AsObject();
        AssertExactProperties(input["request"]!.AsObject(),
            ["displayName", "displayFileName", "evidenceProfile"], oracle.Id);
        JsonObject rawTrigger = input["attempts"]![0]!["serviceSteps"]![0]![
            "trigger"]!.AsObject();
        AssertExactProperties(rawTrigger, ["kind", "checkpoint"], oracle.Id);
        JsonObject rawEffect = input["attempts"]![0]!["serviceSteps"]![0]![
            "effect"]!.AsObject();
        AssertExactProperties(rawEffect,
            ["kind", "progress", "outcome", "evidenceProfile", "failureProfile",
                "failureDetailProfile", "deferredCheckpoint"], oracle.Id);
        Assert.IsNull(rawEffect["progress"], oracle.Id);
        Assert.IsNull(rawEffect["outcome"], oracle.Id);
        Assert.IsNull(rawEffect["evidenceProfile"], oracle.Id);
        Assert.IsNull(rawEffect["deferredCheckpoint"], oracle.Id);
        JsonObject rawAttempt2 = input["attempts"]![1]!.AsObject();
        AssertExactProperties(rawAttempt2, ["attempt", "serviceSteps"], oracle.Id);
        JsonObject rawAttempt2Step = rawAttempt2["serviceSteps"]![0]!.AsObject();
        AssertExactProperties(rawAttempt2Step, ["trigger", "effect"], oracle.Id);
        AssertExactProperties(rawAttempt2Step["trigger"]!.AsObject(),
            ["kind", "checkpoint"], oracle.Id);
        JsonObject rawAttempt2Effect = rawAttempt2Step["effect"]!.AsObject();
        AssertExactProperties(rawAttempt2Effect,
            ["kind", "progress", "outcome", "evidenceProfile", "failureProfile",
                "failureDetailProfile", "deferredCheckpoint"], oracle.Id);
        Assert.IsNull(rawAttempt2Effect["progress"], oracle.Id);
        Assert.IsNull(rawAttempt2Effect["failureProfile"], oracle.Id);
        Assert.IsNull(rawAttempt2Effect["failureDetailProfile"], oracle.Id);
        Assert.IsNull(rawAttempt2Effect["deferredCheckpoint"], oracle.Id);
        foreach (JsonNode? setupNode in input["setupSteps"]!.AsArray())
        {
            AssertExactProperties(setupNode!.AsObject(),
                ["kind", "attempt", "checkpoint", "interactionId"], oracle.Id);
        }
    }

    private static void AssertExpectedScreen(
        ModelInspectionExpectedScreen screen,
        FailureOracle oracle)
    {
        Assert.AreEqual(ModelInspectionExpectedFigmaState.OperationalFailure,
            screen.Figma.State, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedGeometryProfile.Canonical,
            screen.Figma.GeometryProfile, oracle.Id);

        Assert.IsTrue(screen.Outcome.Visible, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedOutcomeKind.OperationalFailure,
            screen.Outcome.Kind, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedOutcomeTone.Error,
            screen.Outcome.Tone, oracle.Id);
        Assert.IsNull(screen.Outcome.Badge, oracle.Id);
        AssertCopy(screen.Outcome.Title!, "fixture.outcome.failure.title",
            "Inspection could not be completed", oracle.Id);
        AssertCopy(screen.Outcome.SupportingText!, oracle.CopyKey,
            oracle.DefaultText, oracle.Id);

        Assert.IsTrue(screen.Model.Visible, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedModelMode.Compact,
            screen.Model.Mode, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedModelBadge.ResultUnknown,
            screen.Model.Badge, oracle.Id);
        Assert.IsFalse(screen.Model.DisclosureExpanded, oracle.Id);
        AssertCopy(screen.Model.DisplayName, "fixture.model.name",
            "Granite Fixture Model", oracle.Id);
        AssertCopy(screen.Model.DisplayFileName, "fixture.model.file",
            "granite-fixture.gguf", oracle.Id);
        AssertCommonModelRows(screen.Model, oracle.Id);

        Assert.IsTrue(screen.Content.Visible, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedContentMode.OperationalFailure,
            screen.Content.Mode, oracle.Id);
        Assert.IsFalse(screen.Content.DisclosureExpanded, oracle.Id);
        AssertCopy(screen.Content.Heading!, "fixture.content.failure.heading",
            "Inspection did not complete", oracle.Id);
        Assert.HasCount(1, screen.Content.Rows, oracle.Id);
        ModelInspectionExpectedContentRow row = screen.Content.Rows[0];
        Assert.AreEqual("operational-failure-row", row.Id, oracle.Id);
        AssertCopy(row.PrimaryText, "fixture.content.failure.row",
            "Model result unavailable", oracle.Id);
        AssertCopy(row.SecondaryText!, oracle.CopyKey, oracle.DefaultText, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedRowStatus.Error, row.Status, oracle.Id);

        AssertActions(screen.Actions, oracle.Id);
        AssertAutomation(screen.Automation, oracle);
        AssertFooter(screen.Footer, oracle.Id);
        Assert.AreEqual("choose-another", screen.Focus.Target, oracle.Id);

        Assert.AreEqual(1, screen.Announcements.Count, oracle.Id);
        Assert.HasCount(1, screen.Announcements.Items, oracle.Id);
        AssertCopy(screen.Announcements.Items[0], "fixture.announcement.failure",
            "Model inspection could not be completed.", oracle.Id);
        CollectionAssert.AreEqual(new[] { "operational-failure-row" },
            screen.RowsAndScroll.OrderedRowIds.ToArray(), oracle.Id);
        Assert.AreEqual("content-list", screen.RowsAndScroll.ScrollOwner, oracle.Id);
        CollectionAssert.AreEqual(ExpectedRetainedIdentities,
            screen.RetainedIdentities.Ids.ToArray(), oracle.Id);
    }

    private static void AssertCommonModelRows(
        ModelInspectionExpectedModelRegion model,
        string id)
    {
        (string Id, string LabelKey, string Label, string ValueKey, string Value)[] metadata =
        [
            ("metadata-format", "fixture.metadata.format.label", "Format",
                "fixture.metadata.format.value", "GGUF"),
            ("metadata-file-size", "fixture.metadata.file-size.label", "File size",
                "fixture.metadata.file-size.value", "1.50 GB"),
            ("metadata-quantisation", "fixture.metadata.quantisation.label", "Quantisation",
                "fixture.metadata.quantisation.value", "Q4_K_M"),
            ("metadata-parameters", "fixture.metadata.parameters.label", "Parameters",
                "fixture.metadata.parameters.value", "8 billion"),
            ("metadata-context", "fixture.metadata.context.label", "Context",
                "fixture.metadata.context.value", "8,192 tokens")
        ];
        Assert.AreEqual(metadata.Length, model.Metadata.Count, id);
        for (int index = 0; index < metadata.Length; index++)
        {
            Assert.AreEqual(metadata[index].Id, model.Metadata[index].Id, id);
            AssertCopy(model.Metadata[index].Label, metadata[index].LabelKey,
                metadata[index].Label, id);
            AssertCopy(model.Metadata[index].Value, metadata[index].ValueKey,
                metadata[index].Value, id);
        }

        (string Id, string Key, string Text)[] checks =
        [
            ("check-package", "fixture.check.package",
                "GGUF version 3; package boundaries validated and source integrity preserved."),
            ("check-configuration", "fixture.check.configuration",
                "Architecture llama; 32 layers; quantisation Q4_K_M; 8 billion parameters."),
            ("check-tokenizer", "fixture.check.tokenizer",
                "Tokenizer sentencepiece; smoke check passed with 4 tokens; chat template present."),
            ("check-structure", "fixture.check.structure",
                "Model structure validation completed using reported configuration evidence."),
            ("check-runtime", "fixture.check.runtime",
                "CPU X64 VocabOnly inspection completed with llama.dll using the approved profile.")
        ];
        Assert.AreEqual(checks.Length, model.Checks.Count, id);
        for (int index = 0; index < checks.Length; index++)
        {
            Assert.AreEqual(checks[index].Id, model.Checks[index].Id, id);
            AssertCopy(model.Checks[index].Text, checks[index].Key,
                checks[index].Text, id);
            Assert.AreEqual(ModelInspectionExpectedRowStatus.Passed,
                model.Checks[index].Status, id);
        }
    }

    private static void AssertActions(
        ModelInspectionExpectedActionRegion actions,
        string id)
    {
        Assert.IsTrue(actions.Visible, id);
        Assert.AreEqual(ModelInspectionExpectedActionMode.Result, actions.Mode, id);
        (string Id, string Key, string Text, bool Enabled,
            string? HelpKey, string? HelpText)[] expected =
        [
            ("choose-another", "fixture.action.choose-another",
                "Choose another model", true, null, null),
            ("technical-report", "fixture.action.technical-report",
                "View technical report", false,
                "fixture.action.coming-later", "Coming later"),
            ("retry", "fixture.action.retry", "Retry inspection", true, null, null)
        ];
        Assert.AreEqual(expected.Length, actions.Items.Count, id);
        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionExpectedAction actual = actions.Items[index];
            Assert.AreEqual(expected[index].Id, actual.Id, id);
            AssertCopy(actual.Label, expected[index].Key, expected[index].Text, id);
            Assert.IsTrue(actual.Visible, id);
            Assert.AreEqual(expected[index].Enabled, actual.Enabled, id);
            AssertOptionalCopy(actual.HelpText, expected[index].HelpKey,
                expected[index].HelpText, id);
        }
    }

    private static void AssertAutomation(
        ModelInspectionExpectedAutomation automation,
        FailureOracle oracle)
    {
        AutomationOracle[] expected =
        [
            new("model-card", "fixture.model.name", "Granite Fixture Model",
                ModelInspectionExpectedControlType.Group,
                ModelInspectionExpectedLiveSetting.Off, null, null),
            new("choose-another", "fixture.automation.action.choose-another",
                "Choose another model", ModelInspectionExpectedControlType.Button,
                ModelInspectionExpectedLiveSetting.Off, null, null),
            new("technical-report", "fixture.automation.action.technical-report",
                "View technical inspection report", ModelInspectionExpectedControlType.Button,
                ModelInspectionExpectedLiveSetting.Off,
                "fixture.action.coming-later", "Coming later"),
            new("retry", "fixture.automation.action.retry", "Retry model inspection",
                ModelInspectionExpectedControlType.Button,
                ModelInspectionExpectedLiveSetting.Off, null, null),
            new("content-list", "fixture.content.failure.heading",
                "Inspection did not complete", ModelInspectionExpectedControlType.List,
                ModelInspectionExpectedLiveSetting.Polite, null, null),
            new("operational-failure-row", "fixture.content.failure.row",
                "Model result unavailable", ModelInspectionExpectedControlType.ListItem,
                ModelInspectionExpectedLiveSetting.Off,
                oracle.CopyKey, oracle.DefaultText)
        ];
        Assert.AreEqual(expected.Length, automation.Controls.Count, oracle.Id);
        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionExpectedAutomationControl actual =
                automation.Controls[index];
            AutomationOracle item = expected[index];
            Assert.AreEqual(item.Id, actual.Id, oracle.Id);
            AssertCopy(actual.AccessibleName, item.NameKey, item.Name, oracle.Id);
            Assert.AreEqual(item.ControlType, actual.ControlType, oracle.Id);
            Assert.AreEqual(item.LiveSetting, actual.LiveSetting, oracle.Id);
            AssertOptionalCopy(actual.HelpText, item.HelpKey, item.HelpText, oracle.Id);
        }
    }

    private static void AssertFooter(
        ModelInspectionExpectedFooter footer,
        string id)
    {
        Assert.AreEqual(ModelInspectionExpectedFooterStatus.Interrupted,
            footer.Status, id);
        CollectionAssert.AreEqual(Enum.GetValues<ModelInspectionExpectedStage>(),
            footer.Rows.Select(row => row.Stage).ToArray(), id);
        CollectionAssert.AreEqual(
            Enumerable.Repeat(ModelInspectionExpectedFooterStatus.Interrupted, 5)
                .ToArray(),
            footer.Rows.Select(row => row.Status).ToArray(), id);
    }

    private static void AssertPreset(
        ModelInspectionPresetExpectation preset,
        string id)
    {
        Assert.AreEqual(ModelInspectionFixtureResponsiveLayout.Desktop,
            preset.ResponsiveLayout, id);
        Assert.AreEqual(480d, preset.MinimumContentColumnWidth, id);
        Assert.AreEqual(960d, preset.MaximumContentColumnWidth, id);
        Assert.IsTrue(preset.NoClipping, id);
        Assert.IsTrue(preset.NoOverlap, id);
        Assert.IsTrue(preset.AllRequiredContentReachable, id);
        Assert.HasCount(1, preset.TextRoles, id);
        Assert.AreEqual("operational-failure-row", preset.TextRoles[0].Id, id);
        Assert.AreEqual(ModelInspectionFixtureTextBehavior.Wrap,
            preset.TextRoles[0].Behavior, id);
        Assert.AreEqual("content-list", preset.ScrollOwner, id);
        Assert.AreEqual(44d, preset.MinimumPointerTargetWidth, id);
        Assert.AreEqual(44d, preset.MinimumPointerTargetHeight, id);
        CollectionAssert.AreEqual(ExpectedReadingOrder,
            preset.LogicalReadingOrder.ToArray(), id);
        CollectionAssert.AreEqual(new[] { "choose-another", "retry" },
            preset.TabOrder.ToArray(), id);
        Assert.AreEqual("choose-another", preset.FocusTarget, id);
        Assert.AreEqual(ModelInspectionFixtureResourceProfile.Light,
            preset.Resources, id);
        Assert.AreEqual(ModelInspectionFixtureTextProfile.Standard100,
            preset.TextScale, id);
        Assert.AreEqual(ModelInspectionFixtureMotionProfile.Normal,
            preset.Motion, id);
        Assert.AreEqual(1, preset.MinimumAnimationStarts, id);
        Assert.AreEqual(1, preset.MaximumAnimationStarts, id);
    }

    private static void AssertInteractions(
        ValidatedModelInspectionFixture fixture,
        FailureOracle oracle)
    {
        InteractionOracle[] expected =
        [
            new("choose-another", ModelInspectionFixtureInteractionKind.ChooseAnother,
                "gallery:no-active-fixture", null, 0,
                ModelInspectionExpectedFooterStatus.Interrupted,
                ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture),
            new("retry", ModelInspectionFixtureInteractionKind.Retry,
                "MI-002", "choose-another", 1,
                ModelInspectionExpectedFooterStatus.Complete,
                ModelInspectionFixtureInteractionLifetimeEffect.None),
            new("reset", ModelInspectionFixtureInteractionKind.Reset,
                oracle.Id, "choose-another", 0,
                ModelInspectionExpectedFooterStatus.Interrupted,
                ModelInspectionFixtureInteractionLifetimeEffect.RetirePage)
        ];
        Assert.AreEqual(expected.Length, fixture.Interactions.Count, oracle.Id);
        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionFixtureInteraction actual = fixture.Interactions[index];
            InteractionOracle item = expected[index];
            Assert.AreEqual(item.Id, actual.Id, oracle.Id);
            Assert.AreEqual(item.Kind, actual.Kind, oracle.Id);
            Assert.AreEqual("observed", actual.SourceCheckpoint, oracle.Id);
            Assert.AreEqual(item.Target, actual.Target, oracle.Id);
            Assert.AreEqual(item.ExpectedFocus, actual.ExpectedFocus, oracle.Id);
            Assert.AreEqual(item.ExpectedAnnouncementCount,
                actual.ExpectedAnnouncementCount, oracle.Id);
            Assert.AreEqual(item.ExpectedFooterStatus,
                actual.ExpectedFooterStatus, oracle.Id);
            Assert.AreEqual(item.LifetimeEffect, actual.LifetimeEffect, oracle.Id);
        }

        CollectionAssert.AreEqual(
            new[]
            {
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.Reset
            },
            fixture.VisibleInteractions.Select(item => item.Kind).ToArray(),
            oracle.Id);
    }

    private static void AssertSetup(
        ModelInspectionFixtureSetupStepDescriptor actual,
        ModelInspectionFixtureSetupStepKind kind,
        int? attempt,
        string? checkpoint,
        string? interactionId,
        string id)
    {
        Assert.AreEqual(kind, actual.Kind, id);
        Assert.AreEqual(attempt, actual.Attempt, id);
        Assert.AreEqual(checkpoint, actual.Checkpoint, id);
        Assert.AreEqual(interactionId, actual.InteractionId, id);
    }

    private static void AssertCopy(
        ModelInspectionExpectedCopy copy,
        string key,
        string text,
        string id)
    {
        Assert.AreEqual(key, copy.CopyKey, id);
        Assert.AreEqual(text, copy.DefaultText, id);
    }

    private static void AssertOptionalCopy(
        ModelInspectionExpectedCopy? copy,
        string? key,
        string? text,
        string id)
    {
        if (key is null)
        {
            Assert.IsNull(copy, id);
            return;
        }

        Assert.IsNotNull(copy, id);
        AssertCopy(copy, key, text!, id);
    }

    private static void AssertExactProperties(
        JsonObject value,
        string[] expected,
        string id)
    {
        CollectionAssert.AreEquivalent(expected, value.Select(item => item.Key).ToArray(), id);
    }

    private static FailureBatch LoadFailureBatch() =>
        LoadMutatedFailureBatch((_, _) => { });

    private static FailureBatch LoadMutatedFailureBatch(
        Action<JsonObject, IDictionary<string, JsonObject>> mutate)
    {
        string directory = ScenarioDirectory();
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                File.ReadAllBytes(Path.Combine(
                    directory, "model-inspection-fixture.schema.json"))));
        JsonObject policyDocument = JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json")))!.AsObject();
        JsonArray policyFixtures = policyDocument["fixtures"]!.AsArray();
        while (policyFixtures.Count > 42)
        {
            policyFixtures.RemoveAt(policyFixtures.Count - 1);
        }

        Dictionary<string, JsonObject> descriptors = policyFixtures
            .Select(node => node!.AsObject())
            .ToDictionary(
                entry => entry["id"]!.GetValue<string>(),
                entry => JsonNode.Parse(File.ReadAllText(Path.Combine(
                    directory,
                    entry["fileName"]!.GetValue<string>())))!.AsObject(),
                StringComparer.Ordinal);
        mutate(policyDocument, descriptors);

        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                Encoding.UTF8.GetBytes(policyDocument.ToJsonString())), schema);
        ModelInspectionFixtureDocumentSource[] sources = policy.Value.Fixtures
            .Select(entry => new ModelInspectionFixtureDocumentSource(
                entry.FileName,
                Encoding.UTF8.GetBytes(descriptors[entry.Id].ToJsonString())))
            .ToArray();
        ModelInspectionFixtureCatalogue catalogue =
            ModelInspectionFixtureCatalogue.LoadDescriptors(sources, policy, schema);
        return new(catalogue, policy, descriptors);
    }

    private static void ApplyFailureProfile(
        JsonObject policy,
        IDictionary<string, JsonObject> descriptors,
        FailureOracle target,
        FailureOracle source)
    {
        JsonObject policyEntry = policy["fixtures"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(entry => entry["id"]!.GetValue<string>() == target.Id);
        policyEntry["requiredCoverageTags"]![1] = source.FailureCoverageTag;

        JsonObject descriptor = descriptors[target.Id];
        descriptor["coverage"]!["failureProfiles"]![0] =
            FailureProfileJson(source.FailureProfile);
        descriptor["input"]!["attempts"]![0]!["serviceSteps"]![0]!["effect"]![
            "failureProfile"] = FailureProfileJson(source.FailureProfile);
        SetCopy(descriptor["expected"]!["outcome"]!["supportingText"]!.AsObject(), source);
        SetCopy(descriptor["expected"]!["content"]!["rows"]![0]![
            "secondaryText"]!.AsObject(), source);
        SetCopy(descriptor["expected"]!["automation"]!["controls"]![5]![
            "helpText"]!.AsObject(), source);
    }

    private static void SetCopy(JsonObject copy, FailureOracle oracle)
    {
        copy["copyKey"] = oracle.CopyKey;
        copy["defaultText"] = oracle.DefaultText;
    }

    private static string FailureProfileJson(
        ModelInspectionFixtureFailureProfile profile) => profile switch
    {
        ModelInspectionFixtureFailureProfile.WorkerTimeout => "workerTimeout",
        ModelInspectionFixtureFailureProfile.WorkerCrashEarlyExit =>
            "workerCrashEarlyExit",
        ModelInspectionFixtureFailureProfile.MalformedWorkerResponse =>
            "malformedWorkerResponse",
        ModelInspectionFixtureFailureProfile.CancellationUnconfirmed =>
            "cancellationUnconfirmed",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
    };

    private static readonly string[] ExpectedReadingOrder =
    [
        "model-card", "choose-another", "technical-report", "retry",
        "content-list", "operational-failure-row"
    ];

    private static readonly string[] ExpectedRetainedIdentities =
    [
        "model-card", "metadata-format", "metadata-file-size",
        "metadata-quantisation", "metadata-parameters", "metadata-context",
        "check-package", "check-configuration", "check-tokenizer",
        "check-structure", "check-runtime", "content-list",
        "operational-failure-row", "choose-another", "technical-report", "retry"
    ];

    private static string ScenarioDirectory() => Path.Combine(
        Root,
        "tests",
        "TestFixtures",
        "ModelInspectionScenarios");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record FailureBatch(
        ModelInspectionFixtureCatalogue Catalogue,
        ValidatedModelInspectionFixtureCoveragePolicy Policy,
        Dictionary<string, JsonObject> Documents);

    private sealed record FailureOracle(
        string Id,
        string FileName,
        string TargetCondition,
        ModelInspectionFixtureFailureProfile FailureProfile,
        string FailureCoverageTag,
        string CopyKey,
        string DefaultText);

    private sealed record AutomationOracle(
        string Id,
        string NameKey,
        string Name,
        ModelInspectionExpectedControlType ControlType,
        ModelInspectionExpectedLiveSetting LiveSetting,
        string? HelpKey,
        string? HelpText);

    private sealed record InteractionOracle(
        string Id,
        ModelInspectionFixtureInteractionKind Kind,
        string Target,
        string? ExpectedFocus,
        int ExpectedAnnouncementCount,
        ModelInspectionExpectedFooterStatus ExpectedFooterStatus,
        ModelInspectionFixtureInteractionLifetimeEffect LifetimeEffect);
}
