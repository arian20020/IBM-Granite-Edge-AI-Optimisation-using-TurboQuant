using System.Text;
using System.Text.Json.Nodes;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureLifecycleBatchContractTests
{
    private static readonly string[] ExpectedLifecycleFileNames =
    [
        "MI-029-cancellation-requested-cooperative-cancelled.fixture.json",
        "MI-030-cancellation-forced-operational-failure.fixture.json",
        "MI-031-retry-after-cancellation.fixture.json",
        "MI-032-retry-after-operational-failure.fixture.json",
        "MI-033-retry-stale-progress-rejected.fixture.json",
        "MI-034-retry-stale-result-rejected.fixture.json",
        "MI-035-retry-stale-motion-completion-rejected.fixture.json",
        "MI-036-retry-stale-announcement-rejected.fixture.json",
        "MI-037-choose-another-page-retired.fixture.json",
        "MI-038-gallery-switch-old-session-retired.fixture.json"
    ];

    private static readonly string Root = FindRepositoryRoot();

    private static readonly LifecycleOracle[] Oracles =
    [
        new(
            "MI-029",
            ModelInspectionExpectedFigmaState.Cancelled,
            [ModelInspectionFixtureStage.CheckModelPackage],
            [ModelInspectionFixtureStageStatus.Active],
            [],
            [ModelInspectionFixtureInteractionKind.Cancel,
                ModelInspectionFixtureInteractionKind.Restart,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset],
            [ModelInspectionFixtureLifecycleTag.CancellationRequested,
                ModelInspectionFixtureLifecycleTag.CooperativeCancellation],
            [],
            [new(1, [Progress("progress"), Cancelled("cancelled")]),
                new(2, [Ready("attempt-2-ready")])],
            [Release(1, "progress"), Invoke(
                ModelInspectionFixtureSetupStepKind.InvokeCancel, "cancel-request"),
                Release(1, "cancelled"), Observe()],
            ScreenProfile.Cancelled,
            null,
            [new("cancel-request", ModelInspectionFixtureInteractionKind.Cancel,
                    "progress", "cancelled", "model-card", 0,
                    ModelInspectionExpectedFooterStatus.InProgress,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                Choose(ModelInspectionExpectedFooterStatus.NotComplete),
                new("restart", ModelInspectionFixtureInteractionKind.Restart,
                    "observed", "MI-002", "choose-another", 1,
                    ModelInspectionExpectedFooterStatus.Complete,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                Reset("MI-029", ModelInspectionExpectedFooterStatus.NotComplete)]),
        new(
            "MI-030",
            ModelInspectionExpectedFigmaState.OperationalFailure,
            [ModelInspectionFixtureStage.CheckModelPackage],
            [ModelInspectionFixtureStageStatus.Active],
            [],
            [ModelInspectionFixtureInteractionKind.Cancel,
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset],
            [ModelInspectionFixtureLifecycleTag.CancellationRequested,
                ModelInspectionFixtureLifecycleTag.ForcedCancellation],
            [ModelInspectionFixtureFailureProfile.CancellationUnconfirmed],
            [new(1, [Progress("progress"), Failure(
                "cancellation-unconfirmed",
                ModelInspectionFixtureFailureProfile.CancellationUnconfirmed)]),
                new(2, [Ready("attempt-2-ready")])],
            [Release(1, "progress"), Invoke(
                ModelInspectionFixtureSetupStepKind.InvokeCancel, "cancel-request"),
                Release(1, "cancellation-unconfirmed"), Observe()],
            ScreenProfile.Failure,
            new("fixture.failure.cancellation-unconfirmed",
                "Cancellation could not be confirmed safely."),
            [new("cancel-request", ModelInspectionFixtureInteractionKind.Cancel,
                    "progress", "cancellation-unconfirmed", "model-card", 0,
                    ModelInspectionExpectedFooterStatus.InProgress,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                Choose(ModelInspectionExpectedFooterStatus.Interrupted),
                new("retry", ModelInspectionFixtureInteractionKind.Retry,
                    "observed", "MI-002", "choose-another", 1,
                    ModelInspectionExpectedFooterStatus.Complete,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                Reset("MI-030", ModelInspectionExpectedFooterStatus.Interrupted)]),
        ReadyAfter(
            "MI-031",
            ModelInspectionFixtureLifecycleTag.RetryAfterCancellation,
            [],
            new(1, [Cancelled("cancelled")]),
            ModelInspectionFixtureSetupStepKind.InvokeRestart,
            "restart-attempt",
            ModelInspectionFixtureInteractionKind.Restart),
        ReadyAfter(
            "MI-032",
            ModelInspectionFixtureLifecycleTag.RetryAfterOperationalFailure,
            [ModelInspectionFixtureFailureProfile.WorkerStartFailure],
            new(1, [Failure("failure",
                ModelInspectionFixtureFailureProfile.WorkerStartFailure)]),
            ModelInspectionFixtureSetupStepKind.InvokeRetry,
            "retry-attempt",
            ModelInspectionFixtureInteractionKind.Retry),
        Stale(
            "MI-033",
            ModelInspectionFixtureLifecycleTag.StaleProgressRejected,
            ModelInspectionFixtureFailureProfile.WorkerTimeout,
            ModelInspectionFixtureServiceEffectKind.DeferStaleProgress,
            "old-progress",
            ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress),
        Stale(
            "MI-034",
            ModelInspectionFixtureLifecycleTag.StaleResultRejected,
            ModelInspectionFixtureFailureProfile.WorkerStartFailure,
            ModelInspectionFixtureServiceEffectKind.DeferStaleResultSnapshot,
            "old-result",
            ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot),
        Stale(
            "MI-035",
            ModelInspectionFixtureLifecycleTag.StaleMotionRejected,
            ModelInspectionFixtureFailureProfile.WorkerStartFailure,
            ModelInspectionFixtureServiceEffectKind.DeferStaleMotion,
            "old-motion",
            ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion),
        Stale(
            "MI-036",
            ModelInspectionFixtureLifecycleTag.StaleAnnouncementRejected,
            ModelInspectionFixtureFailureProfile.WorkerStartFailure,
            ModelInspectionFixtureServiceEffectKind.DeferStaleAnnouncement,
            "old-announcement",
            ModelInspectionFixtureSetupStepKind.ReleaseStaleAnnouncement),
        ReadyTerminal("MI-037",
            ModelInspectionFixtureLifecycleTag.ChooseAnotherRetired),
        ReadyTerminal("MI-038",
            ModelInspectionFixtureLifecycleTag.GallerySwitchRetired)
    ];

    [TestMethod]
    public void LifecycleBatchDescriptorsMatchIndependentPerIdOracle()
    {
        string directory = ScenarioDirectory();
        string[] missing = ExpectedLifecycleFileNames
            .Where(fileName => !File.Exists(Path.Combine(directory, fileName)))
            .Select(fileName => fileName[..6])
            .ToArray();

        Assert.AreEqual(
            0,
            missing.Length,
            $"Missing lifecycle batch descriptors ({missing.Length}): " +
            string.Join(", ", missing));

        ModelInspectionFixtureCatalogue catalogue = LoadThroughLifecycleBatch();
        Assert.AreEqual(38, catalogue.Fixtures.Count);
        foreach (LifecycleOracle oracle in Oracles)
        {
            ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
                item => item.Id == oracle.Id);
            AssertFixture(fixture, oracle);
        }

        JsonObject fullPolicy = JsonNode.Parse(File.ReadAllText(Path.Combine(
            ScenarioDirectory(),
            "model-inspection-fixture-coverage-policy.json")))!.AsObject();
        JsonObject switchPair = fullPolicy["gallerySwitchPairs"]!.AsArray()
            .Single()!.AsObject();
        Assert.AreEqual("MI-038", switchPair["sourceId"]!.GetValue<string>());
        Assert.AreEqual("MI-039", switchPair["destinationId"]!.GetValue<string>());
    }

    [TestMethod]
    public void LifecycleBatchLoaderDiagnosticReportsOnlyTheTwelveLaterIdsMissing()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadThroughLifecycleBatch();
        Assert.AreEqual(38, catalogue.Fixtures.Count);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 38).Select(value => $"MI-{value:000}").ToArray(),
            catalogue.Fixtures.Select(fixture => fixture.Id).ToArray());

        string[] available =
            ModelInspectionFixtureCatalogueContractTests.ExpectedFixtureFileNames
            .Take(38)
            .ToArray();
        CollectionAssert.AreEqual(
            Enumerable.Range(39, 12).Select(value => $"MI-{value:000}").ToArray(),
            ModelInspectionFixtureCoverageValidator.GetMissingFixtureIds(available)
                .ToArray());
    }

    [TestMethod]
    public void LifecycleOracleRejectsColludingTagsAndUnprovenStaleOrdering()
    {
        LifecycleOracle progressOracle = Oracles.Single(oracle => oracle.Id == "MI-033");
        ModelInspectionFixtureCatalogue colludingTags = LoadMutatedLifecycleBatch(
            policy =>
            {
                JsonObject entry = policy["fixtures"]!.AsArray()
                    .Select(node => node!.AsObject())
                    .Single(node => node["id"]!.GetValue<string>() == "MI-033");
                JsonArray tags = entry["requiredCoverageTags"]!.AsArray();
                int index = tags.Select((node, offset) => (node, offset))
                    .Single(item => item.node!.GetValue<string>() ==
                        "lifecycle.stale-progress-rejected").offset;
                tags[index] = "lifecycle.stale-announcement-rejected";
            },
            descriptors => descriptors["MI-033"]["coverage"]![
                "lifecycleTags"]![0] = "staleAnnouncementRejected");
        Assert.ThrowsExactly<AssertFailedException>(() => AssertFixture(
            colludingTags.Fixtures.Single(fixture => fixture.Id == "MI-033"),
            progressOracle));

        ModelInspectionFixtureCatalogue staleBeforeCurrentTerminal =
            LoadMutatedLifecycleBatch(
                _ => { },
                descriptors =>
                {
                    JsonArray setup = descriptors["MI-033"]["input"]![
                        "setupSteps"]!.AsArray();
                    JsonNode currentTerminalRelease = setup[3]!.DeepClone();
                    setup[3] = setup[4]!.DeepClone();
                    setup[4] = currentTerminalRelease;
                });
        Assert.ThrowsExactly<AssertFailedException>(() => AssertFixture(
            staleBeforeCurrentTerminal.Fixtures.Single(
                fixture => fixture.Id == "MI-033"),
            progressOracle));
    }

    private static void AssertFixture(
        ValidatedModelInspectionFixture fixture,
        LifecycleOracle oracle)
    {
        Assert.AreEqual(ModelInspectionFixtureCategory.Lifecycle,
            fixture.Category, oracle.Id);
        CollectionAssert.AreEqual(
            new[] { (ModelInspectionFixtureFigmaState)(int)oracle.FigmaState },
            fixture.Coverage.FigmaStates.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(oracle.Stages,
            fixture.Coverage.Stages.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(oracle.StageStatuses,
            fixture.Coverage.StageStatuses.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(oracle.Outcomes,
            fixture.Coverage.Outcomes.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(oracle.CoverageInteractions,
            fixture.Coverage.Interactions.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(oracle.LifecycleTags,
            fixture.Coverage.LifecycleTags.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(oracle.FailureProfiles,
            fixture.Coverage.FailureProfiles.ToArray(), oracle.Id);
        Assert.HasCount(0, fixture.Coverage.StressTags, oracle.Id);
        CollectionAssert.AreEqual(new[] { "P01" }, fixture.Presets.ToArray(), oracle.Id);
        CollectionAssert.AreEquivalent(new[] { "P01" },
            fixture.PresetExpectations.Keys.ToArray(), oracle.Id);

        Assert.AreEqual("Granite Fixture Model", fixture.Input.Request.DisplayName, oracle.Id);
        Assert.AreEqual("granite-fixture.gguf", fixture.Input.Request.DisplayFileName, oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureEvidenceProfile.Compatible,
            fixture.Input.Request.EvidenceProfile, oracle.Id);

        AssertAttempts(fixture, oracle);
        AssertSetupAndReplay(fixture, oracle);
        AssertExpectedScreen(fixture, oracle);
        AssertInteractions(fixture, oracle);
    }

    private static void AssertAttempts(
        ValidatedModelInspectionFixture fixture,
        LifecycleOracle oracle)
    {
        Assert.AreEqual(oracle.Attempts.Length, fixture.Input.Attempts.Count, oracle.Id);
        for (int attemptIndex = 0; attemptIndex < oracle.Attempts.Length; attemptIndex++)
        {
            AttemptOracle expectedAttempt = oracle.Attempts[attemptIndex];
            ModelInspectionFixtureAttemptDescriptor actualAttempt =
                fixture.Input.Attempts[attemptIndex];
            Assert.AreEqual(attemptIndex + 1, actualAttempt.Attempt, oracle.Id);
            Assert.AreEqual(expectedAttempt.Attempt, actualAttempt.Attempt, oracle.Id);
            Assert.AreEqual(expectedAttempt.Steps.Length,
                actualAttempt.ServiceSteps.Count, oracle.Id);

            for (int stepIndex = 0; stepIndex < expectedAttempt.Steps.Length; stepIndex++)
            {
                ServiceStepOracle expected = expectedAttempt.Steps[stepIndex];
                ModelInspectionFixtureServiceStepDescriptor actual =
                    actualAttempt.ServiceSteps[stepIndex];
                Assert.AreEqual(ModelInspectionFixtureServiceTriggerKind.Checkpoint,
                    actual.Trigger.Kind, oracle.Id);
                Assert.AreEqual(expected.Checkpoint, actual.Trigger.Checkpoint, oracle.Id);
                Assert.AreEqual(expected.Kind, actual.Effect.Kind, oracle.Id);
                Assert.AreEqual(expected.Outcome, actual.Effect.Outcome, oracle.Id);
                Assert.AreEqual(expected.EvidenceProfile,
                    actual.Effect.EvidenceProfile, oracle.Id);
                Assert.AreEqual(expected.FailureProfile,
                    actual.Effect.FailureProfile, oracle.Id);
                Assert.AreEqual(expected.FailureDetailProfile,
                    actual.Effect.FailureDetailProfile, oracle.Id);
                Assert.AreEqual(expected.DeferredCheckpoint,
                    actual.Effect.DeferredCheckpoint, oracle.Id);
                if (expected.Progress is null)
                {
                    Assert.IsNull(actual.Effect.Progress, oracle.Id);
                }
                else
                {
                    Assert.IsNotNull(actual.Effect.Progress, oracle.Id);
                    Assert.AreEqual(expected.Progress.Stage,
                        actual.Effect.Progress.Stage, oracle.Id);
                    Assert.AreEqual(expected.Progress.Status,
                        actual.Effect.Progress.Status, oracle.Id);
                    Assert.AreEqual(expected.Progress.CompletedStageCount,
                        actual.Effect.Progress.CompletedStageCount, oracle.Id);
                    Assert.AreEqual(expected.Progress.Fraction,
                        actual.Effect.Progress.Fraction, oracle.Id);
                    Assert.AreEqual(expected.Progress.DetailProfile,
                        actual.Effect.Progress.DetailProfile, oracle.Id);
                }
            }

            ModelInspectionFixtureServiceEffectDescriptor[] terminals = actualAttempt.ServiceSteps
                .Select(step => step.Effect)
                .Where(IsTerminal)
                .ToArray();
            Assert.HasCount(1, terminals, oracle.Id);
            Assert.AreSame(actualAttempt.ServiceSteps[^1].Effect, terminals[0], oracle.Id);
        }
    }

    private static void AssertSetupAndReplay(
        ValidatedModelInspectionFixture fixture,
        LifecycleOracle oracle)
    {
        Assert.AreEqual(oracle.Setup.Length, fixture.Input.SetupSteps.Count, oracle.Id);
        int activeAttempt = 1;
        int[] nextServiceStep = new int[fixture.Input.Attempts.Count];
        var obsoleteAttempts = new HashSet<int>();
        var capturedDeferred = new HashSet<(int Attempt, string Checkpoint)>();
        ModelInspectionFixtureServiceEffectDescriptor? currentEffect = null;
        string? currentCheckpoint = null;

        for (int index = 0; index < oracle.Setup.Length; index++)
        {
            SetupStepOracle expected = oracle.Setup[index];
            ModelInspectionFixtureSetupStepDescriptor actual = fixture.Input.SetupSteps[index];
            Assert.AreEqual(expected.Kind, actual.Kind, oracle.Id);
            Assert.AreEqual(expected.Attempt, actual.Attempt, oracle.Id);
            Assert.AreEqual(expected.Checkpoint, actual.Checkpoint, oracle.Id);
            Assert.AreEqual(expected.InteractionId, actual.InteractionId, oracle.Id);

            if (actual.Kind == ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint)
            {
                Assert.AreEqual(activeAttempt, actual.Attempt, oracle.Id);
                ModelInspectionFixtureAttemptDescriptor owner =
                    fixture.Input.Attempts[activeAttempt - 1];
                ModelInspectionFixtureServiceStepDescriptor released =
                    owner.ServiceSteps[nextServiceStep[activeAttempt - 1]++];
                Assert.AreEqual(actual.Checkpoint, released.Trigger.Checkpoint, oracle.Id);
                if (released.Effect.DeferredCheckpoint is { } deferred)
                {
                    capturedDeferred.Add((activeAttempt, deferred));
                }

                if (IsTerminal(released.Effect))
                {
                    currentEffect = released.Effect;
                }

                currentCheckpoint = actual.Checkpoint;
            }
            else if (actual.Kind is ModelInspectionFixtureSetupStepKind.InvokeRetry or
                     ModelInspectionFixtureSetupStepKind.InvokeRestart)
            {
                Assert.IsNotNull(currentEffect, oracle.Id);
                Assert.IsTrue(IsTerminal(currentEffect), oracle.Id);
                ModelInspectionFixtureInteraction interaction = fixture.Interactions.Single(
                    item => item.Id == actual.InteractionId);
                Assert.AreEqual(currentCheckpoint, interaction.SourceCheckpoint, oracle.Id);
                currentCheckpoint = interaction.Target;
                obsoleteAttempts.Add(activeAttempt++);
                currentEffect = null;
            }
            else if (actual.Kind == ModelInspectionFixtureSetupStepKind.InvokeCancel)
            {
                ModelInspectionFixtureInteraction interaction = fixture.Interactions.Single(
                    item => item.Id == actual.InteractionId);
                Assert.AreEqual(currentCheckpoint, interaction.SourceCheckpoint, oracle.Id);
                currentCheckpoint = interaction.Target;
            }
            else if (actual.Kind is
                     ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress or
                     ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot or
                     ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion or
                     ModelInspectionFixtureSetupStepKind.ReleaseStaleAnnouncement)
            {
                Assert.IsTrue(obsoleteAttempts.Contains(actual.Attempt!.Value), oracle.Id);
                Assert.IsTrue(capturedDeferred.Contains(
                    (actual.Attempt.Value, actual.Checkpoint!)), oracle.Id);
                Assert.IsNotNull(currentEffect, oracle.Id);
                Assert.AreEqual(activeAttempt,
                    AttemptOwning(fixture.Input.Attempts, currentEffect!), oracle.Id);
            }
            else if (actual.Kind == ModelInspectionFixtureSetupStepKind.Observe)
            {
                Assert.AreEqual(oracle.Setup.Length - 1, index, oracle.Id);
                Assert.AreEqual(fixture.Input.ObservationCheckpoint,
                    actual.Checkpoint, oracle.Id);
                currentCheckpoint = actual.Checkpoint;
            }
        }

        Assert.AreEqual("observed", fixture.Input.ObservationCheckpoint, oracle.Id);
        Assert.AreEqual("observed", currentCheckpoint, oracle.Id);
        Assert.IsNotNull(currentEffect, oracle.Id);
        Assert.IsTrue(IsTerminal(currentEffect), oracle.Id);
    }

    private static void AssertExpectedScreen(
        ValidatedModelInspectionFixture fixture,
        LifecycleOracle oracle)
    {
        ModelInspectionExpectedScreen screen = fixture.Expected;
        Assert.AreEqual(oracle.FigmaState, screen.Figma.State, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedGeometryProfile.Canonical,
            screen.Figma.GeometryProfile, oracle.Id);
        Assert.IsTrue(screen.Outcome.Visible, oracle.Id);
        Assert.IsTrue(screen.Model.Visible, oracle.Id);
        Assert.IsFalse(screen.Model.DisclosureExpanded, oracle.Id);
        Assert.IsFalse(screen.Content.DisclosureExpanded, oracle.Id);
        AssertCommonModel(screen.Model, oracle.Screen, oracle.Id);

        switch (oracle.Screen)
        {
            case ScreenProfile.Ready:
                AssertOutcome(screen.Outcome, ModelInspectionExpectedOutcomeKind.Ready,
                    ModelInspectionExpectedOutcomeTone.Success,
                    "fixture.outcome.ready.title", "Model inspection complete",
                    "fixture.outcome.ready.supporting",
                    "The model passed all lightweight inspection checks.", oracle.Id);
                Assert.IsFalse(screen.Content.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedContentMode.Hidden,
                    screen.Content.Mode, oracle.Id);
                Assert.IsNull(screen.Content.Heading, oracle.Id);
                Assert.HasCount(0, screen.Content.Rows, oracle.Id);
                AssertActions(screen.Actions, ReadyActions, oracle.Id);
                AssertAutomation(screen.Automation, ReadyAutomation, oracle.Id);
                AssertFooter(screen.Footer, ModelInspectionExpectedFooterStatus.Complete,
                    oracle.Id);
                AssertAnnouncements(screen.Announcements,
                    "fixture.announcement.ready", "Model inspection complete.", oracle.Id);
                AssertRows(screen.RowsAndScroll, [], null, oracle.Id);
                CollectionAssert.AreEqual(ReadyRetained,
                    screen.RetainedIdentities.Ids.ToArray(), oracle.Id);
                AssertPreset(fixture.PresetExpectations["P01"], null,
                    ReadyReadingOrder, ["inspection-details-disclosure", "choose-another"],
                    oracle.Id);
                break;

            case ScreenProfile.Cancelled:
                AssertOutcome(screen.Outcome, ModelInspectionExpectedOutcomeKind.Cancelled,
                    ModelInspectionExpectedOutcomeTone.Neutral,
                    "fixture.outcome.cancelled.title", "Inspection cancelled",
                    "fixture.outcome.cancelled.supporting",
                    "No model result was produced because inspection was cancelled.", oracle.Id);
                AssertContent(screen.Content, ModelInspectionExpectedContentMode.Cancelled,
                    "fixture.content.cancelled.heading", "Inspection cancelled",
                    "cancelled-row", "fixture.content.cancelled.row", "Inspection stopped",
                    "fixture.outcome.cancelled.supporting",
                    "No model result was produced because inspection was cancelled.",
                    ModelInspectionExpectedRowStatus.Information, oracle.Id);
                AssertActions(screen.Actions, CancelledActions, oracle.Id);
                AssertAutomation(screen.Automation, CancelledAutomation, oracle.Id);
                AssertFooter(screen.Footer,
                    ModelInspectionExpectedFooterStatus.NotComplete, oracle.Id);
                AssertAnnouncements(screen.Announcements,
                    "fixture.announcement.cancelled",
                    "Model inspection was cancelled.", oracle.Id);
                AssertRows(screen.RowsAndScroll, ["cancelled-row"], "content-list", oracle.Id);
                CollectionAssert.AreEqual(CancelledRetained,
                    screen.RetainedIdentities.Ids.ToArray(), oracle.Id);
                AssertPreset(fixture.PresetExpectations["P01"], "content-list",
                    CancelledReadingOrder, ["choose-another", "restart"], oracle.Id);
                break;

            case ScreenProfile.Failure:
                Assert.IsNotNull(oracle.FailureCopy, oracle.Id);
                FailureCopy copy = oracle.FailureCopy;
                AssertOutcome(screen.Outcome,
                    ModelInspectionExpectedOutcomeKind.OperationalFailure,
                    ModelInspectionExpectedOutcomeTone.Error,
                    "fixture.outcome.failure.title", "Inspection could not be completed",
                    copy.CopyKey, copy.DefaultText, oracle.Id);
                AssertContent(screen.Content,
                    ModelInspectionExpectedContentMode.OperationalFailure,
                    "fixture.content.failure.heading", "Inspection did not complete",
                    "operational-failure-row", "fixture.content.failure.row",
                    "Model result unavailable", copy.CopyKey, copy.DefaultText,
                    ModelInspectionExpectedRowStatus.Error, oracle.Id);
                AssertActions(screen.Actions, FailureActions, oracle.Id);
                AssertAutomation(screen.Automation,
                    FailureAutomation(copy), oracle.Id);
                AssertFooter(screen.Footer,
                    ModelInspectionExpectedFooterStatus.Interrupted, oracle.Id);
                AssertAnnouncements(screen.Announcements,
                    "fixture.announcement.failure",
                    "Model inspection could not be completed.", oracle.Id);
                AssertRows(screen.RowsAndScroll, ["operational-failure-row"],
                    "content-list", oracle.Id);
                CollectionAssert.AreEqual(FailureRetained,
                    screen.RetainedIdentities.Ids.ToArray(), oracle.Id);
                AssertPreset(fixture.PresetExpectations["P01"], "content-list",
                    FailureReadingOrder, ["choose-another", "retry"], oracle.Id);
                break;
        }

        Assert.AreEqual("choose-another", screen.Focus.Target, oracle.Id);
    }

    private static void AssertCommonModel(
        ModelInspectionExpectedModelRegion model,
        ScreenProfile profile,
        string id)
    {
        Assert.AreEqual(profile == ScreenProfile.Ready
                ? ModelInspectionExpectedModelMode.Detailed
                : ModelInspectionExpectedModelMode.Compact,
            model.Mode, id);
        Assert.AreEqual(profile switch
        {
            ScreenProfile.Ready => null,
            ScreenProfile.Cancelled => ModelInspectionExpectedModelBadge.NotInspected,
            _ => ModelInspectionExpectedModelBadge.ResultUnknown
        }, model.Badge, id);
        AssertCopy(model.DisplayName, "fixture.model.name", "Granite Fixture Model", id);
        AssertCopy(model.DisplayFileName, "fixture.model.file", "granite-fixture.gguf", id);

        if (profile != ScreenProfile.Ready)
        {
            Assert.HasCount(0, model.Metadata, id);
            Assert.HasCount(0, model.Checks, id);
            return;
        }

        (string Id, string LabelKey, string Label, string ValueKey, string Value)[] metadata =
        [
            ("metadata-publisher", "fixture.metadata.publisher.label", "PUBLISHER",
                "fixture.not-reported", "Not reported"),
            ("metadata-format", "fixture.metadata.format.label", "FORMAT",
                "fixture.metadata.format.value", "GGUF"),
            ("metadata-quantisation", "fixture.metadata.quantisation.label", "QUANTISATION",
                "fixture.metadata.quantisation.value", "Q4_K_M"),
            ("metadata-parameters", "fixture.metadata.parameters.label", "PARAMETERS",
                "fixture.metadata.parameters.value", "8 billion"),
            ("metadata-model-type", "fixture.metadata.model-type.label", "MODEL TYPE",
                "fixture.not-reported", "Not reported"),
            ("metadata-context", "fixture.metadata.context.label", "DECLARED MAX CONTEXT",
                "fixture.metadata.context.value", "8,192 tokens"),
            ("metadata-file-size", "fixture.metadata.file-size.label", "FILE SIZE",
                "fixture.metadata.file-size.value", "1.50 GB")
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

        Assert.HasCount(0, model.Checks, id);
    }

    private static void AssertOutcome(
        ModelInspectionExpectedOutcomeRegion outcome,
        ModelInspectionExpectedOutcomeKind kind,
        ModelInspectionExpectedOutcomeTone tone,
        string titleKey,
        string title,
        string supportingKey,
        string supporting,
        string id)
    {
        Assert.AreEqual(kind, outcome.Kind, id);
        Assert.AreEqual(tone, outcome.Tone, id);
        Assert.IsNull(outcome.Badge, id);
        AssertCopy(outcome.Title!, titleKey, title, id);
        AssertCopy(outcome.SupportingText!, supportingKey, supporting, id);
    }

    private static void AssertContent(
        ModelInspectionExpectedContentRegion content,
        ModelInspectionExpectedContentMode mode,
        string headingKey,
        string heading,
        string rowId,
        string primaryKey,
        string primary,
        string secondaryKey,
        string secondary,
        ModelInspectionExpectedRowStatus status,
        string id)
    {
        Assert.IsTrue(content.Visible, id);
        Assert.AreEqual(mode, content.Mode, id);
        AssertCopy(content.Heading!, headingKey, heading, id);
        Assert.HasCount(1, content.Rows, id);
        Assert.AreEqual(rowId, content.Rows[0].Id, id);
        AssertCopy(content.Rows[0].PrimaryText, primaryKey, primary, id);
        AssertCopy(content.Rows[0].SecondaryText!, secondaryKey, secondary, id);
        Assert.AreEqual(status, content.Rows[0].Status, id);
    }

    private static void AssertActions(
        ModelInspectionExpectedActionRegion actions,
        ActionOracle[] expected,
        string id)
    {
        Assert.IsTrue(actions.Visible, id);
        Assert.AreEqual(ModelInspectionExpectedActionMode.Result, actions.Mode, id);
        Assert.AreEqual(expected.Length, actions.Items.Count, id);
        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionExpectedAction actual = actions.Items[index];
            ActionOracle oracle = expected[index];
            Assert.AreEqual(oracle.Id, actual.Id, id);
            AssertCopy(actual.Label, oracle.LabelKey, oracle.Label, id);
            Assert.AreEqual(oracle.Visible, actual.Visible, id);
            Assert.AreEqual(oracle.Enabled, actual.Enabled, id);
            AssertOptionalCopy(actual.HelpText, oracle.HelpKey, oracle.Help, id);
        }
    }

    private static void AssertAutomation(
        ModelInspectionExpectedAutomation automation,
        AutomationOracle[] expected,
        string id)
    {
        Assert.AreEqual(expected.Length, automation.Controls.Count, id);
        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionExpectedAutomationControl actual = automation.Controls[index];
            AutomationOracle oracle = expected[index];
            Assert.AreEqual(oracle.Id, actual.Id, id);
            AssertCopy(actual.AccessibleName, oracle.NameKey, oracle.Name, id);
            Assert.AreEqual(oracle.ControlType, actual.ControlType, id);
            Assert.AreEqual(oracle.LiveSetting, actual.LiveSetting, id);
            AssertOptionalCopy(actual.HelpText, oracle.HelpKey, oracle.Help, id);
        }
    }

    private static void AssertFooter(
        ModelInspectionExpectedFooter footer,
        ModelInspectionExpectedFooterStatus status,
        string id)
    {
        Assert.AreEqual(status, footer.Status, id);
    }

    private static void AssertAnnouncements(
        ModelInspectionExpectedAnnouncements announcements,
        string key,
        string text,
        string id)
    {
        Assert.AreEqual(1, announcements.Count, id);
        Assert.HasCount(1, announcements.Items, id);
        AssertCopy(announcements.Items[0], key, text, id);
    }

    private static void AssertRows(
        ModelInspectionExpectedRowsAndScroll rows,
        string[] ids,
        string? scrollOwner,
        string fixtureId)
    {
        CollectionAssert.AreEqual(ids, rows.OrderedRowIds.ToArray(), fixtureId);
        Assert.AreEqual(scrollOwner, rows.ScrollOwner, fixtureId);
    }

    private static void AssertPreset(
        ModelInspectionPresetExpectation preset,
        string? scrollOwner,
        string[] readingOrder,
        string[] tabOrder,
        string id)
    {
        Assert.AreEqual(ModelInspectionFixtureResponsiveLayout.Desktop,
            preset.ResponsiveLayout, id);
        Assert.AreEqual(480d, preset.MinimumContentColumnWidth, id);
        Assert.AreEqual(960d, preset.MaximumContentColumnWidth, id);
        Assert.IsTrue(preset.NoClipping, id);
        Assert.IsTrue(preset.NoOverlap, id);
        Assert.IsTrue(preset.AllRequiredContentReachable, id);
        Assert.AreEqual(scrollOwner, preset.ScrollOwner, id);
        Assert.AreEqual(44d, preset.MinimumPointerTargetWidth, id);
        Assert.AreEqual(44d, preset.MinimumPointerTargetHeight, id);
        CollectionAssert.AreEqual(readingOrder,
            preset.LogicalReadingOrder.ToArray(), id);
        CollectionAssert.AreEqual(tabOrder, preset.TabOrder.ToArray(), id);
        Assert.AreEqual("choose-another", preset.FocusTarget, id);
        Assert.AreEqual(ModelInspectionFixtureResourceProfile.Light,
            preset.Resources, id);
        Assert.AreEqual(ModelInspectionFixtureTextProfile.Standard100,
            preset.TextScale, id);
        Assert.AreEqual(ModelInspectionFixtureMotionProfile.Normal,
            preset.Motion, id);
        (int minimum, int maximum) = id switch
        {
            "MI-029" or "MI-030" => (3, 3),
            "MI-031" or "MI-032" or "MI-033" or "MI-034" or
                "MI-035" or "MI-036" => (2, 2),
            "MI-037" or "MI-038" => (1, 1),
            _ => throw new AssertFailedException($"Unknown lifecycle fixture {id}.")
        };
        Assert.AreEqual(minimum, preset.MinimumAnimationStarts, id);
        Assert.AreEqual(maximum, preset.MaximumAnimationStarts, id);
        if (scrollOwner is null)
        {
            Assert.HasCount(0, preset.TextRoles, id);
        }
        else
        {
            Assert.HasCount(1, preset.TextRoles, id);
            Assert.AreEqual(readingOrder[^1], preset.TextRoles[0].Id, id);
            Assert.AreEqual(ModelInspectionFixtureTextBehavior.Wrap,
                preset.TextRoles[0].Behavior, id);
        }
    }

    private static void AssertInteractions(
        ValidatedModelInspectionFixture fixture,
        LifecycleOracle oracle)
    {
        Assert.AreEqual(oracle.Interactions.Length, fixture.Interactions.Count, oracle.Id);
        for (int index = 0; index < oracle.Interactions.Length; index++)
        {
            InteractionOracle expected = oracle.Interactions[index];
            ModelInspectionFixtureInteraction actual = fixture.Interactions[index];
            Assert.AreEqual(expected.Id, actual.Id, oracle.Id);
            Assert.AreEqual(expected.Kind, actual.Kind, oracle.Id);
            Assert.AreEqual(expected.SourceCheckpoint, actual.SourceCheckpoint, oracle.Id);
            Assert.AreEqual(expected.Target, actual.Target, oracle.Id);
            Assert.AreEqual(expected.ExpectedFocus, actual.ExpectedFocus, oracle.Id);
            Assert.AreEqual(expected.ExpectedAnnouncementCount,
                actual.ExpectedAnnouncementCount, oracle.Id);
            Assert.AreEqual(expected.ExpectedFooterStatus,
                actual.ExpectedFooterStatus, oracle.Id);
            Assert.AreEqual(expected.LifetimeEffect, actual.LifetimeEffect, oracle.Id);
        }

        CollectionAssert.AreEqual(
            oracle.Interactions.Where(interaction =>
                    interaction.SourceCheckpoint == "observed")
                .Select(interaction => interaction.Kind).ToArray(),
            fixture.VisibleInteractions.Select(interaction => interaction.Kind).ToArray(),
            oracle.Id);
        Assert.IsFalse(fixture.Input.SetupSteps.Any(step =>
            step.Kind == ModelInspectionFixtureSetupStepKind.InvokeChooseAnother), oracle.Id);
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
        }
        else
        {
            Assert.IsNotNull(copy, id);
            AssertCopy(copy, key, text!, id);
        }
    }

    private static ModelInspectionFixtureCatalogue LoadThroughLifecycleBatch()
    {
        string directory = ScenarioDirectory();
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                File.ReadAllBytes(Path.Combine(directory,
                    "model-inspection-fixture.schema.json"))));
        JsonObject policyDocument = JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json")))!.AsObject();
        JsonArray policyFixtures = policyDocument["fixtures"]!.AsArray();
        while (policyFixtures.Count > 38)
        {
            policyFixtures.RemoveAt(policyFixtures.Count - 1);
        }

        policyDocument["gallerySwitchPairs"] = new JsonArray();
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                Encoding.UTF8.GetBytes(policyDocument.ToJsonString())), schema);
        ModelInspectionFixtureDocumentSource[] descriptors = policy.Value.Fixtures
            .Select(entry => new ModelInspectionFixtureDocumentSource(
                entry.FileName,
                File.ReadAllBytes(Path.Combine(directory, entry.FileName))))
            .ToArray();
        return ModelInspectionFixtureCatalogue.LoadDescriptors(descriptors, policy, schema);
    }

    private static ModelInspectionFixtureCatalogue LoadMutatedLifecycleBatch(
        Action<JsonObject> mutatePolicy,
        Action<IDictionary<string, JsonObject>> mutateDescriptors)
    {
        string directory = ScenarioDirectory();
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                File.ReadAllBytes(Path.Combine(directory,
                    "model-inspection-fixture.schema.json"))));
        JsonObject policyDocument = JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json")))!.AsObject();
        JsonArray policyFixtures = policyDocument["fixtures"]!.AsArray();
        while (policyFixtures.Count > 38)
        {
            policyFixtures.RemoveAt(policyFixtures.Count - 1);
        }

        policyDocument["gallerySwitchPairs"] = new JsonArray();
        var descriptors = policyFixtures
            .Select(node => node!.AsObject())
            .ToDictionary(
                entry => entry["id"]!.GetValue<string>(),
                entry => JsonNode.Parse(File.ReadAllText(Path.Combine(
                    directory,
                    entry["fileName"]!.GetValue<string>())))!.AsObject(),
                StringComparer.Ordinal);
        mutatePolicy(policyDocument);
        mutateDescriptors(descriptors);

        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                Encoding.UTF8.GetBytes(policyDocument.ToJsonString())), schema);
        ModelInspectionFixtureDocumentSource[] sources = policy.Value.Fixtures
            .Select(entry => new ModelInspectionFixtureDocumentSource(
                entry.FileName,
                Encoding.UTF8.GetBytes(descriptors[entry.Id].ToJsonString())))
            .ToArray();
        return ModelInspectionFixtureCatalogue.LoadDescriptors(sources, policy, schema);
    }

    private static int AttemptOwning(
        IReadOnlyList<ModelInspectionFixtureAttemptDescriptor> attempts,
        ModelInspectionFixtureServiceEffectDescriptor effect)
    {
        for (int index = 0; index < attempts.Count; index++)
        {
            if (attempts[index].ServiceSteps.Any(step =>
                    ReferenceEquals(step.Effect, effect)))
            {
                return index + 1;
            }
        }

        return 0;
    }

    private static bool IsTerminal(ModelInspectionFixtureServiceEffectDescriptor effect) =>
        effect.Kind is ModelInspectionFixtureServiceEffectKind.Completed or
            ModelInspectionFixtureServiceEffectKind.Cancelled or
            ModelInspectionFixtureServiceEffectKind.OperationalFailure;

    private static LifecycleOracle ReadyAfter(
        string id,
        ModelInspectionFixtureLifecycleTag lifecycleTag,
        ModelInspectionFixtureFailureProfile[] failureProfiles,
        AttemptOracle firstAttempt,
        ModelInspectionFixtureSetupStepKind invokeKind,
        string interactionId,
        ModelInspectionFixtureInteractionKind interactionKind) => new(
            id,
            ModelInspectionExpectedFigmaState.ReadyCollapsed,
            [],
            [],
            [ModelInspectionFixtureOutcome.Ready],
            [interactionKind, ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset],
            [lifecycleTag],
            failureProfiles,
            [firstAttempt, new(2, [Ready("ready")])],
            [Release(1, firstAttempt.Steps[^1].Checkpoint),
                Invoke(invokeKind, interactionId), Release(2, "ready"), Observe()],
            ScreenProfile.Ready,
            null,
            [new(interactionId, interactionKind, firstAttempt.Steps[^1].Checkpoint,
                    "ready", "page-heading", 0,
                    ModelInspectionExpectedFooterStatus.InProgress,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                Expand(), Choose(ModelInspectionExpectedFooterStatus.Complete),
                Reset(id, ModelInspectionExpectedFooterStatus.Complete)]);

    private static LifecycleOracle Stale(
        string id,
        ModelInspectionFixtureLifecycleTag lifecycleTag,
        ModelInspectionFixtureFailureProfile failureProfile,
        ModelInspectionFixtureServiceEffectKind deferredKind,
        string deferredCheckpoint,
        ModelInspectionFixtureSetupStepKind releaseKind) => new(
            id,
            ModelInspectionExpectedFigmaState.ReadyCollapsed,
            [],
            [],
            [ModelInspectionFixtureOutcome.Ready],
            [ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset],
            [lifecycleTag],
            [failureProfile],
            [new(1,
                [Deferred($"capture-{deferredCheckpoint}", deferredKind,
                        deferredCheckpoint),
                    Failure("failure", failureProfile)]),
                new(2, [Ready("ready")])],
            [Release(1, $"capture-{deferredCheckpoint}"), Release(1, "failure"),
                Invoke(ModelInspectionFixtureSetupStepKind.InvokeRetry, "retry-attempt"),
                Release(2, "ready"),
                new(releaseKind, 1, deferredCheckpoint, null), Observe()],
            ScreenProfile.Ready,
            null,
            [new("retry-attempt", ModelInspectionFixtureInteractionKind.Retry,
                    "failure", "ready", "page-heading", 0,
                    ModelInspectionExpectedFooterStatus.InProgress,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                Expand(), Choose(ModelInspectionExpectedFooterStatus.Complete),
                Reset(id, ModelInspectionExpectedFooterStatus.Complete)]);

    private static LifecycleOracle ReadyTerminal(
        string id,
        ModelInspectionFixtureLifecycleTag lifecycleTag) => new(
            id,
            ModelInspectionExpectedFigmaState.ReadyCollapsed,
            [],
            [],
            [ModelInspectionFixtureOutcome.Ready],
            [ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset],
            [lifecycleTag],
            [],
            [new(1, [Ready("terminal")])],
            [Release(1, "terminal"), Observe()],
            ScreenProfile.Ready,
            null,
            [Expand(), Choose(ModelInspectionExpectedFooterStatus.Complete),
                Reset(id, ModelInspectionExpectedFooterStatus.Complete)]);

    private static ServiceStepOracle Progress(string checkpoint) => new(
        checkpoint,
        ModelInspectionFixtureServiceEffectKind.Progress,
        null,
        null,
        null,
        null,
        null,
        new(ModelInspectionFixtureStage.CheckModelPackage,
            ModelInspectionFixtureStageStatus.Active, 0, null,
            ModelInspectionFixtureProgressDetailProfile.Default));

    private static ServiceStepOracle Ready(string checkpoint) => new(
        checkpoint,
        ModelInspectionFixtureServiceEffectKind.Completed,
        ModelInspectionFixtureOutcome.Ready,
        ModelInspectionFixtureEvidenceProfile.Compatible,
        null,
        null,
        null,
        null);

    private static ServiceStepOracle Cancelled(string checkpoint) => new(
        checkpoint,
        ModelInspectionFixtureServiceEffectKind.Cancelled,
        null,
        null,
        null,
        null,
        null,
        null);

    private static ServiceStepOracle Failure(
        string checkpoint,
        ModelInspectionFixtureFailureProfile profile) => new(
            checkpoint,
            ModelInspectionFixtureServiceEffectKind.OperationalFailure,
            null,
            null,
            profile,
            ModelInspectionFixtureFailureDetailProfile.Default,
            null,
            null);

    private static ServiceStepOracle Deferred(
        string checkpoint,
        ModelInspectionFixtureServiceEffectKind kind,
        string deferredCheckpoint) => new(
            checkpoint,
            kind,
            null,
            null,
            null,
            null,
            deferredCheckpoint,
            null);

    private static SetupStepOracle Release(int attempt, string checkpoint) => new(
        ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint,
        attempt,
        checkpoint,
        null);

    private static SetupStepOracle Invoke(
        ModelInspectionFixtureSetupStepKind kind,
        string interactionId) => new(kind, null, null, interactionId);

    private static SetupStepOracle Observe() => new(
        ModelInspectionFixtureSetupStepKind.Observe,
        null,
        "observed",
        null);

    private static InteractionOracle Expand() => new(
        "expand",
        ModelInspectionFixtureInteractionKind.Expand,
        "observed",
        "MI-003",
        "inspection-details-disclosure",
        0,
        ModelInspectionExpectedFooterStatus.Complete,
        ModelInspectionFixtureInteractionLifetimeEffect.None);

    private static InteractionOracle Choose(
        ModelInspectionExpectedFooterStatus footer) => new(
            "choose-another",
            ModelInspectionFixtureInteractionKind.ChooseAnother,
            "observed",
            "gallery:no-active-fixture",
            null,
            0,
            footer,
            ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture);

    private static InteractionOracle Reset(
        string id,
        ModelInspectionExpectedFooterStatus footer) => new(
            "reset",
            ModelInspectionFixtureInteractionKind.Reset,
            "observed",
            id,
            "choose-another",
            0,
            footer,
            ModelInspectionFixtureInteractionLifetimeEffect.RetirePage);

    private static readonly ActionOracle[] ReadyActions =
    [
        new("choose-another", "fixture.action.choose-another",
            "Choose another model", true, true, null, null),
        new("technical-report", "fixture.action.technical-report",
            "View technical report", true, false,
            "fixture.action.coming-later", "Coming later"),
        new("hardware-fit", "fixture.action.hardware-fit",
            "Check hardware fit", true, false,
            "fixture.action.coming-later", "Coming later")
    ];

    private static readonly ActionOracle[] CancelledActions =
    [
        new("choose-another", "fixture.action.choose-another",
            "Choose another model", true, true, null, null),
        new("restart", "fixture.action.restart", "Restart inspection",
            true, true, null, null)
    ];

    private static readonly ActionOracle[] FailureActions =
    [
        new("choose-another", "fixture.action.choose-another",
            "Choose another model", true, true, null, null),
        new("technical-report", "fixture.action.technical-report",
            "View technical report", true, false,
            "fixture.action.coming-later", "Coming later"),
        new("retry", "fixture.action.retry", "Retry inspection",
            true, true, null, null)
    ];

    private static readonly AutomationOracle[] ReadyAutomation =
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
        new("hardware-fit", "fixture.automation.action.hardware-fit",
            "Check model hardware fit", ModelInspectionExpectedControlType.Button,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.action.coming-later", "Coming later"),
        new("inspection-details-disclosure",
            "fixture.automation.model-disclosure.view",
            "View model inspection details", ModelInspectionExpectedControlType.Group,
            ModelInspectionExpectedLiveSetting.Off, null, null)
    ];

    private static readonly AutomationOracle[] CancelledAutomation =
    [
        new("model-card", "fixture.model.name", "Granite Fixture Model",
            ModelInspectionExpectedControlType.Group,
            ModelInspectionExpectedLiveSetting.Off, null, null),
        new("choose-another", "fixture.automation.action.choose-another",
            "Choose another model", ModelInspectionExpectedControlType.Button,
            ModelInspectionExpectedLiveSetting.Off, null, null),
        new("restart", "fixture.automation.action.restart",
            "Restart model inspection", ModelInspectionExpectedControlType.Button,
            ModelInspectionExpectedLiveSetting.Off, null, null),
        new("content-list", "fixture.content.cancelled.heading",
            "Inspection cancelled", ModelInspectionExpectedControlType.List,
            ModelInspectionExpectedLiveSetting.Polite, null, null),
        new("cancelled-row", "fixture.content.cancelled.row",
            "Inspection stopped", ModelInspectionExpectedControlType.ListItem,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.outcome.cancelled.supporting",
            "No model result was produced because inspection was cancelled.")
    ];

    private static AutomationOracle[] FailureAutomation(FailureCopy copy) =>
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
            ModelInspectionExpectedLiveSetting.Off, copy.CopyKey, copy.DefaultText)
    ];

    private static readonly string[] ReadyRetained =
    [
        "model-card", "metadata-publisher", "metadata-format",
        "metadata-quantisation", "metadata-parameters", "metadata-model-type",
        "metadata-context", "metadata-file-size", "choose-another",
        "technical-report", "hardware-fit", "inspection-details-disclosure"
    ];

    private static readonly string[] CancelledRetained =
    [
        "model-card", "content-list", "cancelled-row", "choose-another",
        "restart"
    ];

    private static readonly string[] FailureRetained =
    [
        "model-card", "content-list", "operational-failure-row",
        "choose-another", "technical-report", "retry"
    ];

    private static readonly string[] ReadyReadingOrder =
    [
        "model-card", "choose-another", "technical-report", "hardware-fit",
        "inspection-details-disclosure"
    ];

    private static readonly string[] CancelledReadingOrder =
    [
        "model-card", "choose-another", "restart", "content-list", "cancelled-row"
    ];

    private static readonly string[] FailureReadingOrder =
    [
        "model-card", "choose-another", "technical-report", "retry",
        "content-list", "operational-failure-row"
    ];

    private enum ScreenProfile
    {
        Ready,
        Cancelled,
        Failure
    }

    private sealed record LifecycleOracle(
        string Id,
        ModelInspectionExpectedFigmaState FigmaState,
        ModelInspectionFixtureStage[] Stages,
        ModelInspectionFixtureStageStatus[] StageStatuses,
        ModelInspectionFixtureOutcome[] Outcomes,
        ModelInspectionFixtureInteractionKind[] CoverageInteractions,
        ModelInspectionFixtureLifecycleTag[] LifecycleTags,
        ModelInspectionFixtureFailureProfile[] FailureProfiles,
        AttemptOracle[] Attempts,
        SetupStepOracle[] Setup,
        ScreenProfile Screen,
        FailureCopy? FailureCopy,
        InteractionOracle[] Interactions);

    private sealed record AttemptOracle(
        int Attempt,
        ServiceStepOracle[] Steps);

    private sealed record ServiceStepOracle(
        string Checkpoint,
        ModelInspectionFixtureServiceEffectKind Kind,
        ModelInspectionFixtureOutcome? Outcome,
        ModelInspectionFixtureEvidenceProfile? EvidenceProfile,
        ModelInspectionFixtureFailureProfile? FailureProfile,
        ModelInspectionFixtureFailureDetailProfile? FailureDetailProfile,
        string? DeferredCheckpoint,
        ProgressOracle? Progress);

    private sealed record ProgressOracle(
        ModelInspectionFixtureStage Stage,
        ModelInspectionFixtureStageStatus Status,
        int CompletedStageCount,
        double? Fraction,
        ModelInspectionFixtureProgressDetailProfile DetailProfile);

    private sealed record SetupStepOracle(
        ModelInspectionFixtureSetupStepKind Kind,
        int? Attempt,
        string? Checkpoint,
        string? InteractionId);

    private sealed record InteractionOracle(
        string Id,
        ModelInspectionFixtureInteractionKind Kind,
        string SourceCheckpoint,
        string Target,
        string? ExpectedFocus,
        int ExpectedAnnouncementCount,
        ModelInspectionExpectedFooterStatus ExpectedFooterStatus,
        ModelInspectionFixtureInteractionLifetimeEffect LifetimeEffect);

    private sealed record FailureCopy(string CopyKey, string DefaultText);

    private sealed record ActionOracle(
        string Id,
        string LabelKey,
        string Label,
        bool Visible,
        bool Enabled,
        string? HelpKey,
        string? Help);

    private sealed record AutomationOracle(
        string Id,
        string NameKey,
        string Name,
        ModelInspectionExpectedControlType ControlType,
        ModelInspectionExpectedLiveSetting LiveSetting,
        string? HelpKey,
        string? Help);

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
}
