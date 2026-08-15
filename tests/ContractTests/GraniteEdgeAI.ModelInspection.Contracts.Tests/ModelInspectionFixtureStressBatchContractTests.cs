using System.Text;
using System.Text.Json.Nodes;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureStressBatchContractTests
{
    private const string StandardModelName = "Granite Fixture Model";
    private const string ModelFileName = "granite-fixture.gguf";
    private const string WarningRowId = "MI-WARN-CHAT-TEMPLATE-MISSING";
    private const string InvalidRowId = "invalid-report-row";
    private const string FailureRowId = "operational-failure-row";

    private static readonly string[] ExpectedStressFileNames =
    [
        "MI-043-ready-model-name-maximum-collapsed.fixture.json",
        "MI-044-ready-missing-optional-metadata-not-reported-collapsed.fixture.json",
        "MI-045-ready-check-rows-current-maximum-expanded.fixture.json",
        "MI-046-ready-with-warnings-finding-rows-current-maximum-expanded.fixture.json",
        "MI-047-invalid-report-rows-current-maximum-expanded.fixture.json",
        "MI-048-progress-detail-copy-maximum.fixture.json",
        "MI-049-operational-failure-detail-copy-maximum.fixture.json"
    ];

    private static readonly string Root = FindRepositoryRoot();

    private static readonly StressOracle[] Oracles =
    [
        new(
            "MI-043",
            ExpectedStressFileNames[0],
            "ready-model-name-maximum",
            "collapsed",
            ModelInspectionFixtureFigmaState.ReadyCollapsed,
            ModelInspectionFixtureEvidenceProfile.Compatible,
            ModelInspectionFixtureOutcome.Ready,
            ModelInspectionFixtureStressTag.MaximumModelName,
            "stress.maximum-model-name",
            "P02",
            StressScreen.MaximumModelName),
        new(
            "MI-044",
            ExpectedStressFileNames[1],
            "ready-missing-optional-metadata-not-reported",
            "collapsed",
            ModelInspectionFixtureFigmaState.ReadyCollapsed,
            ModelInspectionFixtureEvidenceProfile.CompatibleMissingOptionalMetadata,
            ModelInspectionFixtureOutcome.Ready,
            ModelInspectionFixtureStressTag.MissingOptionalMetadata,
            "stress.missing-optional-metadata",
            "P03",
            StressScreen.MissingOptionalMetadata),
        new(
            "MI-045",
            ExpectedStressFileNames[2],
            "ready-check-rows-current-maximum",
            "expanded",
            ModelInspectionFixtureFigmaState.ReadyExpanded,
            ModelInspectionFixtureEvidenceProfile.Compatible,
            ModelInspectionFixtureOutcome.Ready,
            ModelInspectionFixtureStressTag.MaximumCheckRows,
            "stress.maximum-check-rows",
            "P04",
            StressScreen.MaximumCheckRows),
        new(
            "MI-046",
            ExpectedStressFileNames[3],
            "ready-with-warnings-finding-rows-current-maximum",
            "expanded",
            ModelInspectionFixtureFigmaState.ReadyWithWarningsExpanded,
            ModelInspectionFixtureEvidenceProfile.MissingChatTemplate,
            ModelInspectionFixtureOutcome.ReadyWithWarnings,
            ModelInspectionFixtureStressTag.MaximumFindingRows,
            "stress.maximum-finding-rows",
            "P05",
            StressScreen.MaximumFindingRows),
        new(
            "MI-047",
            ExpectedStressFileNames[4],
            "invalid-report-rows-current-maximum",
            "expanded",
            ModelInspectionFixtureFigmaState.InvalidExpanded,
            ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction,
            ModelInspectionFixtureOutcome.Invalid,
            ModelInspectionFixtureStressTag.MaximumReportRows,
            "stress.maximum-report-rows",
            "P06",
            StressScreen.MaximumReportRows),
        new(
            "MI-048",
            ExpectedStressFileNames[5],
            "progress-detail-copy-maximum",
            null,
            ModelInspectionFixtureFigmaState.InspectionProgress,
            ModelInspectionFixtureEvidenceProfile.Compatible,
            null,
            ModelInspectionFixtureStressTag.MaximumDetailCopy,
            "stress.maximum-detail-copy",
            "P07",
            StressScreen.MaximumProgressDetail),
        new(
            "MI-049",
            ExpectedStressFileNames[6],
            "operational-failure-detail-copy-maximum",
            null,
            ModelInspectionFixtureFigmaState.OperationalFailure,
            ModelInspectionFixtureEvidenceProfile.Compatible,
            null,
            ModelInspectionFixtureStressTag.MaximumDetailCopy,
            "stress.maximum-detail-copy",
            "P08",
            StressScreen.MaximumFailureDetail)
    ];

    private static readonly string MaximumModelName = new('M', 160);

    private static readonly string MaximumProgressDetail =
        string.Concat(Enumerable.Repeat(
            "Inspection progress detail remains bounded for deterministic maximum-copy validation. ",
            6))[..512];

    private static readonly string MaximumFailureDetail =
        string.Concat(Enumerable.Repeat(
            "Operational failure detail remains bounded for deterministic maximum-copy validation. ",
            6))[..512];

    [TestMethod]
    public void StressBatchContainsTheSevenExactDescriptorFiles()
    {
        string[] missing = ExpectedStressFileNames
            .Where(fileName => !File.Exists(Path.Combine(
                ScenarioDirectory(), fileName)))
            .Select(fileName => fileName[..6])
            .ToArray();

        Assert.AreEqual(
            0,
            missing.Length,
            $"Missing stress batch descriptors ({missing.Length}): " +
            string.Join(", ", missing));
    }

    [TestMethod]
    public void ExpectedFooterRetainsOnlyAggregateStatus()
    {
        foreach (string path in Directory.GetFiles(
                     ScenarioDirectory(),
                     "*.fixture.json",
                     SearchOption.TopDirectoryOnly))
        {
            JsonObject document = JsonNode.Parse(File.ReadAllText(path))!
                .AsObject();
            string id = document["id"]!.GetValue<string>();
            JsonObject footer = document["expected"]!["footer"]!.AsObject();

            AssertExactProperties(footer, ["status"], id);
        }
    }

    [TestMethod]
    public void StressBatchDescriptorsMatchIndependentPerIdOracle()
    {
        StressBatch batch = LoadStressBatch();
        Assert.AreEqual(49, batch.Catalogue.Fixtures.Count);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 49).Select(value => $"MI-{value:000}").ToArray(),
            batch.Catalogue.Fixtures.Select(fixture => fixture.Id).ToArray());

        foreach (StressOracle oracle in Oracles)
        {
            AssertFixtureAndPolicy(batch, oracle);
        }

        AssertStressMaxima(batch);
    }

    [TestMethod]
    public void CompleteCatalogueStrictLoadsFortyNineInExactFlatDirectory()
    {
        StressBatch batch = LoadStressBatch();
        Assert.AreEqual(49, batch.Catalogue.Fixtures.Count);

        string directory = ScenarioDirectory();
        string[] topLevelFiles = Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null)
            .Select(fileName => fileName!)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();
        Assert.HasCount(52, topLevelFiles);
        CollectionAssert.AreEqual(
            batch.Policy.Value.Fixtures
                .Select(entry => entry.FileName)
                .Append("README.md")
                .Append("model-inspection-fixture-coverage-policy.json")
                .Append("model-inspection-fixture.schema.json")
                .OrderBy(fileName => fileName, StringComparer.Ordinal)
                .ToArray(),
            topLevelFiles);

        Assert.HasCount(0, Directory.GetDirectories(directory));
        Assert.AreEqual(52,
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length);

        string[] fixtureFileNames = topLevelFiles
            .Where(fileName => fileName.EndsWith(
                ".fixture.json",
                StringComparison.Ordinal))
            .ToArray();
        Assert.HasCount(49, fixtureFileNames);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 49)
                .Select(value => $"MI-{value:000}")
                .ToArray(),
            fixtureFileNames.Select(fileName => fileName[..6]).ToArray());
        Assert.HasCount(0,
            ModelInspectionFixtureCoverageValidator
                .GetMissingFixtureIds(fixtureFileNames));
        ModelInspectionFixtureCoverageValidator.Validate(batch.Catalogue);
    }

    [TestMethod]
    public void StressOracleRejectsJointPolicyAndDescriptorStressMutation()
    {
        StressBatch mutated = LoadMutatedStressBatch((policy, descriptors) =>
        {
            JsonObject entry = PolicyEntry(policy, "MI-043");
            entry["requiredCoverageTags"]![2] =
                "stress.missing-optional-metadata";
            descriptors["MI-043"]["coverage"]!["stressTags"]![0] =
                "missingOptionalMetadata";
        });

        Assert.ThrowsExactly<AssertFailedException>(() =>
            AssertFixtureAndPolicy(mutated, Oracles[0]));
    }

    [TestMethod]
    public void StressOracleRejectsJointPolicyAndDescriptorPresetMutation()
    {
        StressBatch mutated = LoadMutatedStressBatch((policy, descriptors) =>
        {
            PolicyEntry(policy, "MI-043")["requiredPresets"]![1] = "P03";
            JsonObject descriptor = descriptors["MI-043"];
            JsonObject expectations = descriptor["presetExpectations"]!.AsObject();
            JsonObject colluding = expectations["P02"]!.DeepClone().AsObject();
            colluding["resources"] = "highContrastPreview";
            colluding["textScale"] = "standard100";
            expectations.Remove("P02");
            expectations["P03"] = colluding;
            descriptor["presets"]![1] = "P03";
        });

        Assert.ThrowsExactly<AssertFailedException>(() =>
            AssertFixtureAndPolicy(mutated, Oracles[0]));
    }

    private static void AssertFixtureAndPolicy(
        StressBatch batch,
        StressOracle oracle)
    {
        ModelInspectionFixturePolicyEntry policyEntry =
            batch.Policy.Value.Fixtures.Single(entry => entry.Id == oracle.Id);
        ValidatedModelInspectionFixture fixture = batch.Catalogue.Fixtures.Single(
            candidate => candidate.Id == oracle.Id);

        AssertPolicyEntry(policyEntry, oracle);
        AssertPolicyPreset(batch.Policy.Value.Presets.Single(
            preset => preset.Id == "P01"), "P01", oracle.Id);
        AssertPolicyPreset(batch.Policy.Value.Presets.Single(
            preset => preset.Id == oracle.ExtraPreset),
            oracle.ExtraPreset,
            oracle.Id);
        AssertPolicyJsonMembers(batch.PolicyDocument, oracle);
        AssertFixture(fixture, batch.Documents[oracle.Id], oracle);
    }

    private static void AssertPolicyEntry(
        ModelInspectionFixturePolicyEntry entry,
        StressOracle oracle)
    {
        Assert.AreEqual(oracle.Id, entry.Id, oracle.Id);
        Assert.AreEqual(oracle.FileName, entry.FileName, oracle.Id);
        Assert.AreEqual(oracle.TargetCondition, entry.TargetCondition, oracle.Id);
        Assert.AreEqual(oracle.Variant, entry.Variant, oracle.Id);
        Assert.IsNull(entry.PairedWithId, oracle.Id);
        Assert.AreEqual(oracle.FigmaState, entry.CanonicalFigmaState, oracle.Id);
        CollectionAssert.AreEqual(ExpectedCoverageTags(oracle),
            entry.RequiredCoverageTags.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(ExpectedInteractionKinds(oracle),
            entry.RequiredInteractions.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(new[] { "P01", oracle.ExtraPreset },
            entry.RequiredPresets.ToArray(), oracle.Id);
    }

    private static void AssertFixture(
        ValidatedModelInspectionFixture fixture,
        JsonObject document,
        StressOracle oracle)
    {
        Assert.AreEqual(oracle.FileName, fixture.FileName, oracle.Id);
        Assert.AreEqual("model-inspection-fixture.schema.json",
            fixture.Schema, oracle.Id);
        Assert.AreEqual(1, fixture.SchemaVersion, oracle.Id);
        Assert.AreEqual(oracle.Id, fixture.Id, oracle.Id);
        Assert.AreEqual(oracle.TargetCondition, fixture.TargetCondition, oracle.Id);
        Assert.AreEqual(oracle.Variant, fixture.Variant, oracle.Id);
        Assert.AreEqual(
            $"{oracle.Id} {oracle.TargetCondition.Replace('-', ' ')}",
            fixture.Title,
            oracle.Id);
        Assert.AreEqual(ModelInspectionFixtureCategory.Stress,
            fixture.Category, oracle.Id);

        AssertCoverage(fixture, oracle);
        AssertInput(fixture, oracle);
        AssertExpectedScreen(fixture.Expected, oracle);
        AssertPreset(fixture.PresetExpectations["P01"], "P01", oracle);
        AssertPreset(
            fixture.PresetExpectations[oracle.ExtraPreset],
            oracle.ExtraPreset,
            oracle);
        AssertInteractions(fixture, oracle);
        AssertStrictJsonMembers(document, oracle);
    }

    private static void AssertCoverage(
        ValidatedModelInspectionFixture fixture,
        StressOracle oracle)
    {
        CollectionAssert.AreEqual(new[] { oracle.FigmaState },
            fixture.Coverage.FigmaStates.ToArray(), oracle.Id);
        ModelInspectionFixtureStage[] expectedStages = oracle.Screen switch
        {
            StressScreen.MaximumFindingRows =>
            [
                ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup,
                ModelInspectionFixtureStage.ValidateModelStructure,
                ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility
            ],
            StressScreen.MaximumProgressDetail =>
                [ModelInspectionFixtureStage.ReadModelConfiguration],
            _ => []
        };
        CollectionAssert.AreEqual(expectedStages,
            fixture.Coverage.Stages.ToArray(), oracle.Id);
        ModelInspectionFixtureStageStatus[] expectedStageStatuses =
            oracle.Screen switch
            {
                StressScreen.MaximumFindingRows =>
                [
                    ModelInspectionFixtureStageStatus.Warning,
                    ModelInspectionFixtureStageStatus.Completed
                ],
                StressScreen.MaximumProgressDetail =>
                    [ModelInspectionFixtureStageStatus.Active],
                _ => []
            };
        CollectionAssert.AreEqual(expectedStageStatuses,
            fixture.Coverage.StageStatuses.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(
            oracle.Outcome is { } outcome
                ? new[] { outcome }
                : [],
            fixture.Coverage.Outcomes.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(ExpectedInteractionKinds(oracle),
            fixture.Coverage.Interactions.ToArray(), oracle.Id);
        Assert.HasCount(0, fixture.Coverage.LifecycleTags, oracle.Id);
        CollectionAssert.AreEqual(
            oracle.Screen == StressScreen.MaximumFailureDetail
                ? new[] { ModelInspectionFixtureFailureProfile.WorkerStartFailure }
                : [],
            fixture.Coverage.FailureProfiles.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(new[] { oracle.StressTag },
            fixture.Coverage.StressTags.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(new[] { "P01", oracle.ExtraPreset },
            fixture.Presets.ToArray(), oracle.Id);
        CollectionAssert.AreEquivalent(new[] { "P01", oracle.ExtraPreset },
            fixture.PresetExpectations.Keys.ToArray(), oracle.Id);
    }

    private static void AssertInput(
        ValidatedModelInspectionFixture fixture,
        StressOracle oracle)
    {
        Assert.AreEqual(
            oracle.Screen == StressScreen.MaximumModelName
                ? MaximumModelName
                : StandardModelName,
            fixture.Input.Request.DisplayName,
            oracle.Id);
        Assert.AreEqual(ModelFileName,
            fixture.Input.Request.DisplayFileName, oracle.Id);
        Assert.AreEqual(oracle.EvidenceProfile,
            fixture.Input.Request.EvidenceProfile, oracle.Id);
        Assert.HasCount(
            oracle.Screen == StressScreen.MaximumFailureDetail ? 2 : 1,
            fixture.Input.Attempts,
            oracle.Id);
        ModelInspectionFixtureAttemptDescriptor attempt = fixture.Input.Attempts[0];
        Assert.AreEqual(1, attempt.Attempt, oracle.Id);
        Assert.AreEqual(1, attempt.ServiceSteps.Count(serviceStep =>
            serviceStep.Effect.Kind is
                ModelInspectionFixtureServiceEffectKind.Completed or
                ModelInspectionFixtureServiceEffectKind.Cancelled or
                ModelInspectionFixtureServiceEffectKind.OperationalFailure),
            oracle.Id);

        if (oracle.Screen == StressScreen.MaximumProgressDetail)
        {
            Assert.HasCount(2, attempt.ServiceSteps, oracle.Id);
            ModelInspectionFixtureServiceStepDescriptor progress =
                attempt.ServiceSteps[0];
            AssertTrigger(progress, "progress", oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Progress,
                progress.Effect.Kind, oracle.Id);
            Assert.IsNotNull(progress.Effect.Progress, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureStage.ReadModelConfiguration,
                progress.Effect.Progress.Stage, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureStageStatus.Active,
                progress.Effect.Progress.Status, oracle.Id);
            Assert.AreEqual(1, progress.Effect.Progress.CompletedStageCount,
                oracle.Id);
            Assert.IsNull(progress.Effect.Progress.Fraction, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureProgressDetailProfile.Maximum,
                progress.Effect.Progress.DetailProfile, oracle.Id);
            AssertNonFailurePayload(progress.Effect, oracle.Id);

            ModelInspectionFixtureServiceStepDescriptor terminal =
                attempt.ServiceSteps[1];
            AssertTrigger(terminal, "terminal", oracle.Id);
            AssertCompletedEffect(terminal.Effect,
                ModelInspectionFixtureOutcome.Ready,
                ModelInspectionFixtureEvidenceProfile.Compatible,
                oracle.Id);
            AssertSetup(fixture.Input.SetupSteps,
                [(ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint,
                    1, "progress", null),
                 (ModelInspectionFixtureSetupStepKind.Observe,
                    null, "observed", null)],
                oracle.Id);
            Assert.AreEqual("observed", fixture.Input.ObservationCheckpoint,
                oracle.Id);
            return;
        }

        if (oracle.Screen == StressScreen.MaximumFindingRows)
        {
            Assert.HasCount(4, attempt.ServiceSteps, oracle.Id);
            AssertAutomaticProgress(
                attempt.ServiceSteps[0],
                ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup,
                ModelInspectionFixtureStageStatus.Warning,
                3,
                oracle.Id);
            AssertAutomaticProgress(
                attempt.ServiceSteps[1],
                ModelInspectionFixtureStage.ValidateModelStructure,
                ModelInspectionFixtureStageStatus.Completed,
                4,
                oracle.Id);
            AssertAutomaticProgress(
                attempt.ServiceSteps[2],
                ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionFixtureStageStatus.Completed,
                5,
                oracle.Id);
            ModelInspectionFixtureServiceStepDescriptor terminal =
                attempt.ServiceSteps[3];
            AssertTrigger(terminal, "terminal", oracle.Id);
            AssertCompletedEffect(
                terminal.Effect,
                ModelInspectionFixtureOutcome.ReadyWithWarnings,
                ModelInspectionFixtureEvidenceProfile.MissingChatTemplate,
                oracle.Id);
            AssertSetup(fixture.Input.SetupSteps,
                [(ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint,
                        1, "terminal", null),
                 (ModelInspectionFixtureSetupStepKind.InvokeDisclosure,
                        null, null, "expand"),
                 (ModelInspectionFixtureSetupStepKind.Observe,
                        null, "expanded", null)],
                oracle.Id);
            Assert.AreEqual("expanded", fixture.Input.ObservationCheckpoint,
                oracle.Id);
            return;
        }

        Assert.HasCount(1, attempt.ServiceSteps, oracle.Id);
        ModelInspectionFixtureServiceStepDescriptor step = attempt.ServiceSteps[0];
        AssertTrigger(step, "terminal", oracle.Id);
        if (oracle.Screen == StressScreen.MaximumFailureDetail)
        {
            Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.OperationalFailure,
                step.Effect.Kind, oracle.Id);
            Assert.IsNull(step.Effect.Progress, oracle.Id);
            Assert.IsNull(step.Effect.Outcome, oracle.Id);
            Assert.IsNull(step.Effect.EvidenceProfile, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureFailureProfile.WorkerStartFailure,
                step.Effect.FailureProfile, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureFailureDetailProfile.Maximum,
                step.Effect.FailureDetailProfile, oracle.Id);
            Assert.IsNull(step.Effect.DeferredCheckpoint, oracle.Id);

            ModelInspectionFixtureAttemptDescriptor attempt2 =
                fixture.Input.Attempts[1];
            Assert.AreEqual(2, attempt2.Attempt, oracle.Id);
            Assert.HasCount(1, attempt2.ServiceSteps, oracle.Id);
            ModelInspectionFixtureServiceStepDescriptor completed =
                attempt2.ServiceSteps[0];
            AssertTrigger(completed, "attempt-2-ready", oracle.Id);
            AssertCompletedEffect(
                completed.Effect,
                ModelInspectionFixtureOutcome.Ready,
                ModelInspectionFixtureEvidenceProfile.Compatible,
                oracle.Id);
        }
        else
        {
            AssertCompletedEffect(
                step.Effect,
                oracle.Outcome!.Value,
                oracle.EvidenceProfile,
                oracle.Id);
        }

        bool expanded = oracle.Screen is
            StressScreen.MaximumCheckRows or
            StressScreen.MaximumFindingRows or
            StressScreen.MaximumReportRows;
        AssertSetup(
            fixture.Input.SetupSteps,
            expanded
                ? [(ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint,
                        1, "terminal", null),
                   (ModelInspectionFixtureSetupStepKind.InvokeDisclosure,
                        null, null, "expand"),
                   (ModelInspectionFixtureSetupStepKind.Observe,
                        null, "expanded", null)]
                : [(ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint,
                        1, "terminal", null),
                   (ModelInspectionFixtureSetupStepKind.Observe,
                        null, "observed", null)],
            oracle.Id);
        Assert.AreEqual(expanded ? "expanded" : "observed",
            fixture.Input.ObservationCheckpoint, oracle.Id);
    }

    private static void AssertTrigger(
        ModelInspectionFixtureServiceStepDescriptor step,
        string checkpoint,
        string id)
    {
        Assert.AreEqual(ModelInspectionFixtureServiceTriggerKind.Checkpoint,
            step.Trigger.Kind, id);
        Assert.AreEqual(checkpoint, step.Trigger.Checkpoint, id);
    }

    private static void AssertAutomaticProgress(
        ModelInspectionFixtureServiceStepDescriptor step,
        ModelInspectionFixtureStage stage,
        ModelInspectionFixtureStageStatus status,
        int completedStageCount,
        string id)
    {
        Assert.AreEqual(ModelInspectionFixtureServiceTriggerKind.Automatic,
            step.Trigger.Kind, id);
        Assert.IsNull(step.Trigger.Checkpoint, id);
        Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Progress,
            step.Effect.Kind, id);
        Assert.IsNotNull(step.Effect.Progress, id);
        Assert.AreEqual(stage, step.Effect.Progress.Stage, id);
        Assert.AreEqual(status, step.Effect.Progress.Status, id);
        Assert.AreEqual(completedStageCount,
            step.Effect.Progress.CompletedStageCount, id);
        Assert.IsNull(step.Effect.Progress.Fraction, id);
        Assert.AreEqual(ModelInspectionFixtureProgressDetailProfile.Default,
            step.Effect.Progress.DetailProfile, id);
        AssertNonFailurePayload(step.Effect, id);
    }

    private static void AssertCompletedEffect(
        ModelInspectionFixtureServiceEffectDescriptor effect,
        ModelInspectionFixtureOutcome outcome,
        ModelInspectionFixtureEvidenceProfile evidenceProfile,
        string id)
    {
        Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Completed,
            effect.Kind, id);
        Assert.IsNull(effect.Progress, id);
        Assert.AreEqual(outcome, effect.Outcome, id);
        Assert.AreEqual(evidenceProfile, effect.EvidenceProfile, id);
        Assert.IsNull(effect.FailureProfile, id);
        Assert.IsNull(effect.FailureDetailProfile, id);
        Assert.IsNull(effect.DeferredCheckpoint, id);
    }

    private static void AssertNonFailurePayload(
        ModelInspectionFixtureServiceEffectDescriptor effect,
        string id)
    {
        Assert.IsNull(effect.Outcome, id);
        Assert.IsNull(effect.EvidenceProfile, id);
        Assert.IsNull(effect.FailureProfile, id);
        Assert.IsNull(effect.FailureDetailProfile, id);
        Assert.IsNull(effect.DeferredCheckpoint, id);
    }

    private static void AssertExpectedScreen(
        ModelInspectionExpectedScreen screen,
        StressOracle oracle)
    {
        Assert.AreEqual(ExpectedFigmaState(oracle),
            screen.Figma.State, oracle.Id);
        Assert.AreEqual(ModelInspectionExpectedGeometryProfile.Canonical,
            screen.Figma.GeometryProfile, oracle.Id);

        AssertOutcome(screen.Outcome, oracle);
        AssertModel(screen.Model, oracle);
        AssertContent(screen.Content, oracle);
        AssertActions(screen.Actions, oracle);
        AssertAutomation(screen.Automation, oracle);
        AssertFooter(screen.Footer, oracle);
        Assert.AreEqual(
            ExpectedFocusTarget(oracle),
            screen.Focus.Target,
            oracle.Id);
        AssertAnnouncements(screen.Announcements, oracle);

        string[] rows = ExpectedRowIds(oracle);
        CollectionAssert.AreEqual(rows,
            screen.RowsAndScroll.OrderedRowIds.ToArray(), oracle.Id);
        Assert.AreEqual(ExpectedScrollOwner(oracle),
            screen.RowsAndScroll.ScrollOwner, oracle.Id);
        CollectionAssert.AreEqual(ExpectedRetainedIds(oracle),
            screen.RetainedIdentities.Ids.ToArray(), oracle.Id);
    }

    private static void AssertOutcome(
        ModelInspectionExpectedOutcomeRegion outcome,
        StressOracle oracle)
    {
        Assert.IsNull(outcome.Badge, oracle.Id);
        switch (oracle.Screen)
        {
            case StressScreen.MaximumModelName:
            case StressScreen.MissingOptionalMetadata:
            case StressScreen.MaximumCheckRows:
                Assert.IsTrue(outcome.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeKind.Ready,
                    outcome.Kind, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeTone.Success,
                    outcome.Tone, oracle.Id);
                AssertCopy(outcome.Title!, "fixture.outcome.ready.title",
                    "Model inspection complete", oracle.Id);
                AssertCopy(outcome.SupportingText!,
                    "fixture.outcome.ready.supporting",
                    "The model passed all lightweight inspection checks.",
                    oracle.Id);
                break;
            case StressScreen.MaximumFindingRows:
                Assert.IsTrue(outcome.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeKind.ReadyWithWarnings,
                    outcome.Kind, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeTone.Warning,
                    outcome.Tone, oracle.Id);
                AssertCopy(outcome.Title!, "fixture.outcome.warning.title",
                    "Model inspected with warnings", oracle.Id);
                AssertCopy(outcome.SupportingText!,
                    "fixture.outcome.warning.supporting",
                    "Inspection completed with a non-blocking warning.",
                    oracle.Id);
                break;
            case StressScreen.MaximumReportRows:
                Assert.IsTrue(outcome.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeKind.Invalid,
                    outcome.Kind, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeTone.Error,
                    outcome.Tone, oracle.Id);
                AssertCopy(outcome.Title!, "fixture.outcome.invalid.title",
                    "Model is invalid", oracle.Id);
                AssertCopy(outcome.SupportingText!,
                    "fixture.outcome.invalid.supporting",
                    "The model package did not pass structural validation.",
                    oracle.Id);
                break;
            case StressScreen.MaximumProgressDetail:
                Assert.IsFalse(outcome.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeKind.Hidden,
                    outcome.Kind, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeTone.Neutral,
                    outcome.Tone, oracle.Id);
                Assert.IsNull(outcome.Title, oracle.Id);
                Assert.IsNull(outcome.SupportingText, oracle.Id);
                break;
            case StressScreen.MaximumFailureDetail:
                Assert.IsTrue(outcome.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeKind.OperationalFailure,
                    outcome.Kind, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedOutcomeTone.Error,
                    outcome.Tone, oracle.Id);
                AssertCopy(outcome.Title!, "fixture.outcome.failure.title",
                    "Inspection could not be completed", oracle.Id);
                AssertCopy(outcome.SupportingText!,
                    "fixture.failure.detail.maximum",
                    MaximumFailureDetail,
                    oracle.Id);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(oracle));
        }
    }

    private static void AssertModel(
        ModelInspectionExpectedModelRegion model,
        StressOracle oracle)
    {
        Assert.IsTrue(model.Visible, oracle.Id);
        Assert.AreEqual(
            oracle.Screen is StressScreen.MaximumModelName or
                StressScreen.MissingOptionalMetadata or
                StressScreen.MaximumCheckRows
                ? ModelInspectionExpectedModelMode.Detailed
                : ModelInspectionExpectedModelMode.Compact,
            model.Mode,
            oracle.Id);
        Assert.AreEqual(
            oracle.Screen switch
            {
                StressScreen.MaximumProgressDetail =>
                    ModelInspectionExpectedModelBadge.ModelSelected,
                StressScreen.MaximumFailureDetail =>
                    ModelInspectionExpectedModelBadge.ResultUnknown,
                StressScreen.MaximumFindingRows =>
                    ModelInspectionExpectedModelBadge.Inspected,
                StressScreen.MaximumReportRows =>
                    ModelInspectionExpectedModelBadge.Invalid,
                _ => null
            },
            model.Badge,
            oracle.Id);
        Assert.AreEqual(oracle.Screen == StressScreen.MaximumCheckRows,
            model.DisclosureExpanded, oracle.Id);
        AssertCopy(
            model.DisplayName,
            oracle.Screen == StressScreen.MaximumModelName
                ? "fixture.model.name.maximum"
                : "fixture.model.name",
            oracle.Screen == StressScreen.MaximumModelName
                ? MaximumModelName
                : StandardModelName,
            oracle.Id);
        AssertCopy(model.DisplayFileName, "fixture.model.file",
            ModelFileName, oracle.Id);

        if (oracle.Screen is
            StressScreen.MaximumFindingRows or
            StressScreen.MaximumReportRows or
            StressScreen.MaximumProgressDetail or
            StressScreen.MaximumFailureDetail)
        {
            Assert.HasCount(0, model.Metadata, oracle.Id);
            Assert.HasCount(0, model.Checks, oracle.Id);
            return;
        }

        AssertMetadata(model.Metadata, oracle);
        if (oracle.Screen == StressScreen.MaximumCheckRows)
        {
            AssertChecks(model.Checks, oracle.Id);
        }
        else
        {
            Assert.HasCount(0, model.Checks, oracle.Id);
        }
    }

    private static void AssertMetadata(
        IReadOnlyList<ModelInspectionExpectedMetadataField> metadata,
        StressOracle oracle)
    {
        var expected = new List<(string Id, string LabelKey, string Label,
            string ValueKey, string Value)>
        {
            ("metadata-publisher", "fixture.metadata.publisher.label", "PUBLISHER",
                "fixture.not-reported", "Not reported"),
            ("metadata-format", "fixture.metadata.format.label", "FORMAT",
                "fixture.metadata.format.value", "GGUF"),
            ("metadata-quantisation", "fixture.metadata.quantisation.label",
                "QUANTISATION", "fixture.metadata.quantisation.value", "Q4_K_M"),
            ("metadata-parameters", "fixture.metadata.parameters.label", "PARAMETERS",
                "fixture.metadata.parameters.value", "8 billion"),
            ("metadata-model-type", "fixture.metadata.model-type.label", "MODEL TYPE",
                "fixture.not-reported", "Not reported"),
            ("metadata-context", "fixture.metadata.context.label", "DECLARED MAX CONTEXT",
                oracle.Screen == StressScreen.MissingOptionalMetadata
                    ? "fixture.not-reported"
                    : "fixture.metadata.context.value",
                oracle.Screen == StressScreen.MissingOptionalMetadata
                    ? "Not reported"
                    : "8,192 tokens"),
            ("metadata-file-size", "fixture.metadata.file-size.label", "FILE SIZE",
                "fixture.metadata.file-size.value", "1.50 GB")
        };

        Assert.AreEqual(expected.Count, metadata.Count, oracle.Id);
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Id, metadata[index].Id, oracle.Id);
            AssertCopy(metadata[index].Label, expected[index].LabelKey,
                expected[index].Label, oracle.Id);
            AssertCopy(metadata[index].Value, expected[index].ValueKey,
                expected[index].Value, oracle.Id);
        }
    }

    private static void AssertChecks(
        IReadOnlyList<ModelInspectionExpectedCheckRow> checks,
        string id)
    {
        (string Id, string Key, string Text)[] expected =
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
        Assert.AreEqual(5, checks.Count, id);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index].Id, checks[index].Id, id);
            AssertCopy(checks[index].Text, expected[index].Key,
                expected[index].Text, id);
            Assert.AreEqual(ModelInspectionExpectedRowStatus.Passed,
                checks[index].Status, id);
        }
    }

    private static void AssertContent(
        ModelInspectionExpectedContentRegion content,
        StressOracle oracle)
    {
        switch (oracle.Screen)
        {
            case StressScreen.MaximumModelName:
            case StressScreen.MissingOptionalMetadata:
            case StressScreen.MaximumCheckRows:
                Assert.IsFalse(content.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedContentMode.Hidden,
                    content.Mode, oracle.Id);
                Assert.IsFalse(content.DisclosureExpanded, oracle.Id);
                Assert.IsNull(content.Heading, oracle.Id);
                Assert.HasCount(0, content.Rows, oracle.Id);
                break;
            case StressScreen.MaximumFindingRows:
                Assert.IsTrue(content.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedContentMode.Warnings,
                    content.Mode, oracle.Id);
                Assert.IsTrue(content.DisclosureExpanded, oracle.Id);
                AssertCopy(content.Heading!, "fixture.content.warning.heading",
                    "Inspection warnings", oracle.Id);
                Assert.HasCount(1, content.Rows, oracle.Id);
                AssertContentRow(content.Rows[0], WarningRowId,
                    "fixture.warning.chat-template-missing.title",
                    "Chat template not reported",
                    "fixture.warning.chat-template-missing.detail",
                    "The model does not report a chat template. Chat formatting may require manual configuration.",
                    ModelInspectionExpectedRowStatus.Warning,
                    oracle.Id);
                break;
            case StressScreen.MaximumReportRows:
                Assert.IsTrue(content.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedContentMode.Invalid,
                    content.Mode, oracle.Id);
                Assert.IsTrue(content.DisclosureExpanded, oracle.Id);
                AssertCopy(content.Heading!, "fixture.content.invalid.heading",
                    "Invalid model", oracle.Id);
                Assert.HasCount(1, content.Rows, oracle.Id);
                AssertContentRow(content.Rows[0], InvalidRowId,
                    "fixture.content.invalid.row",
                    "Structural validation did not produce a usable model result.",
                    "fixture.content.invalid.detail",
                    "Choose another model.",
                    ModelInspectionExpectedRowStatus.Error,
                    oracle.Id);
                break;
            case StressScreen.MaximumProgressDetail:
                Assert.IsTrue(content.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedContentMode.Progress,
                    content.Mode, oracle.Id);
                Assert.IsFalse(content.DisclosureExpanded, oracle.Id);
                AssertCopy(content.Heading!, "fixture.content.progress.heading",
                    "Inspection progress", oracle.Id);
                AssertProgressRows(content.Rows, oracle.Id);
                break;
            case StressScreen.MaximumFailureDetail:
                Assert.IsTrue(content.Visible, oracle.Id);
                Assert.AreEqual(ModelInspectionExpectedContentMode.OperationalFailure,
                    content.Mode, oracle.Id);
                Assert.IsFalse(content.DisclosureExpanded, oracle.Id);
                AssertCopy(content.Heading!, "fixture.content.failure.heading",
                    "Inspection did not complete", oracle.Id);
                Assert.HasCount(1, content.Rows, oracle.Id);
                AssertContentRow(content.Rows[0], FailureRowId,
                    "fixture.content.failure.row", "Model result unavailable",
                    "fixture.failure.detail.maximum", MaximumFailureDetail,
                    ModelInspectionExpectedRowStatus.Error,
                    oracle.Id);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(oracle));
        }
    }

    private static void AssertProgressRows(
        IReadOnlyList<ModelInspectionExpectedContentRow> rows,
        string id)
    {
        (string Id, string LabelKey, string Label, string DetailKey,
            string Detail, ModelInspectionExpectedRowStatus Status)[] expected =
        [
            ("progress-1", "fixture.progress.check-model-package.label",
                "Check model package", "fixture.progress.check-model-package.detail",
                "Checking the model package.", ModelInspectionExpectedRowStatus.Passed),
            ("progress-2", "fixture.progress.read-configuration.label",
                "Read model configuration", "fixture.progress.detail.maximum",
                MaximumProgressDetail, ModelInspectionExpectedRowStatus.Active),
            ("progress-3", "fixture.progress.validate-tokenizer.label",
                "Validate tokenizer and chat setup",
                "fixture.progress.validate-tokenizer.detail",
                "Validating tokenizer and chat setup.",
                ModelInspectionExpectedRowStatus.Waiting),
            ("progress-4", "fixture.progress.validate-structure.label",
                "Validate model structure", "fixture.progress.validate-structure.detail",
                "Validating model structure.", ModelInspectionExpectedRowStatus.Waiting),
            ("progress-5", "fixture.progress.confirm-runtime.label",
                "Confirm core runtime compatibility",
                "fixture.progress.confirm-runtime.detail",
                "Confirming core runtime compatibility.",
                ModelInspectionExpectedRowStatus.Waiting)
        ];
        Assert.AreEqual(5, rows.Count, id);
        for (int index = 0; index < expected.Length; index++)
        {
            AssertContentRow(rows[index], expected[index].Id,
                expected[index].LabelKey, expected[index].Label,
                expected[index].DetailKey, expected[index].Detail,
                expected[index].Status, id);
        }
    }

    private static void AssertContentRow(
        ModelInspectionExpectedContentRow row,
        string rowId,
        string primaryKey,
        string primaryText,
        string secondaryKey,
        string secondaryText,
        ModelInspectionExpectedRowStatus status,
        string id)
    {
        Assert.AreEqual(rowId, row.Id, id);
        AssertCopy(row.PrimaryText, primaryKey, primaryText, id);
        AssertCopy(row.SecondaryText!, secondaryKey, secondaryText, id);
        Assert.AreEqual(status, row.Status, id);
    }

    private static void AssertActions(
        ModelInspectionExpectedActionRegion actions,
        StressOracle oracle)
    {
        Assert.IsTrue(actions.Visible, oracle.Id);
        Assert.AreEqual(
            oracle.Screen == StressScreen.MaximumProgressDetail
                ? ModelInspectionExpectedActionMode.Inspecting
                : ModelInspectionExpectedActionMode.Result,
            actions.Mode,
            oracle.Id);
        ActionOracle[] expected = ExpectedActions(oracle);
        Assert.AreEqual(expected.Length, actions.Items.Count, oracle.Id);
        for (int index = 0; index < expected.Length; index++)
        {
            ModelInspectionExpectedAction actual = actions.Items[index];
            ActionOracle item = expected[index];
            Assert.AreEqual(item.Id, actual.Id, oracle.Id);
            AssertCopy(actual.Label, item.LabelKey, item.Label, oracle.Id);
            Assert.IsTrue(actual.Visible, oracle.Id);
            Assert.AreEqual(item.Enabled, actual.Enabled, oracle.Id);
            AssertOptionalCopy(actual.HelpText, item.HelpKey,
                item.HelpText, oracle.Id);
        }
    }

    private static void AssertAutomation(
        ModelInspectionExpectedAutomation automation,
        StressOracle oracle)
    {
        AutomationOracle[] expected = ExpectedAutomation(oracle);
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
            AssertOptionalCopy(actual.HelpText, item.HelpKey,
                item.HelpText, oracle.Id);
        }
    }

    private static ActionOracle[] ExpectedActions(StressOracle oracle) =>
        oracle.Screen switch
        {
            StressScreen.MaximumModelName or
            StressScreen.MissingOptionalMetadata or
            StressScreen.MaximumCheckRows =>
            [
                new("choose-another", "fixture.action.choose-another",
                    "Choose another model", true, null, null),
                new("technical-report", "fixture.action.technical-report",
                    "View technical report", false,
                    "fixture.action.coming-later", "Coming later"),
                new("hardware-fit", "fixture.action.hardware-fit",
                    "Check hardware fit", false,
                    "fixture.action.coming-later", "Coming later")
            ],
            StressScreen.MaximumFindingRows =>
            [
                new("choose-another", "fixture.action.choose-another",
                    "Choose another model", true, null, null),
                new("technical-report", "fixture.action.technical-report",
                    "View technical report", false,
                    "fixture.action.coming-later", "Coming later"),
                new("continue-hardware", "fixture.action.continue-hardware",
                    "Continue to hardware check", false,
                    "fixture.action.coming-later", "Coming later")
            ],
            StressScreen.MaximumReportRows =>
            [
                new("technical-report", "fixture.action.technical-report",
                    "View technical report", false,
                    "fixture.action.coming-later", "Coming later"),
                new("choose-another", "fixture.action.choose-another",
                    "Choose another model", true, null, null)
            ],
            StressScreen.MaximumProgressDetail =>
            [
                new("cancel", "fixture.action.cancel", "Cancel inspection",
                    true, null, null)
            ],
            StressScreen.MaximumFailureDetail =>
            [
                new("choose-another", "fixture.action.choose-another",
                    "Choose another model", true, null, null),
                new("technical-report", "fixture.action.technical-report",
                    "View technical report", false,
                    "fixture.action.coming-later", "Coming later"),
                new("retry", "fixture.action.retry", "Retry inspection",
                    true, null, null)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static AutomationOracle[] ExpectedAutomation(StressOracle oracle)
    {
        string modelKey = oracle.Screen == StressScreen.MaximumModelName
            ? "fixture.model.name.maximum"
            : "fixture.model.name";
        string modelName = oracle.Screen == StressScreen.MaximumModelName
            ? MaximumModelName
            : StandardModelName;
        AutomationOracle model = new(
            "model-card", modelKey, modelName,
            ModelInspectionExpectedControlType.Group,
            ModelInspectionExpectedLiveSetting.Off,
            null, null);
        AutomationOracle choose = new(
            "choose-another", "fixture.automation.action.choose-another",
            "Choose another model", ModelInspectionExpectedControlType.Button,
            ModelInspectionExpectedLiveSetting.Off, null, null);
        AutomationOracle report = new(
            "technical-report", "fixture.automation.action.technical-report",
            "View technical inspection report",
            ModelInspectionExpectedControlType.Button,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.action.coming-later", "Coming later");

        return oracle.Screen switch
        {
            StressScreen.MaximumModelName or
            StressScreen.MissingOptionalMetadata or
            StressScreen.MaximumCheckRows =>
            [
                model,
                choose,
                report,
                new("hardware-fit", "fixture.automation.action.hardware-fit",
                    "Check model hardware fit",
                    ModelInspectionExpectedControlType.Button,
                    ModelInspectionExpectedLiveSetting.Off,
                    "fixture.action.coming-later", "Coming later"),
                new("inspection-details-disclosure",
                    oracle.Screen == StressScreen.MaximumCheckRows
                        ? "fixture.automation.model-disclosure.hide"
                        : "fixture.automation.model-disclosure.view",
                    oracle.Screen == StressScreen.MaximumCheckRows
                        ? "Hide model inspection details"
                        : "View model inspection details",
                    ModelInspectionExpectedControlType.Group,
                    ModelInspectionExpectedLiveSetting.Off, null, null)
            ],
            StressScreen.MaximumFindingRows =>
            [
                model,
                choose,
                report,
                new("continue-hardware",
                    "fixture.automation.action.continue-hardware",
                    "Continue to model hardware check",
                    ModelInspectionExpectedControlType.Button,
                    ModelInspectionExpectedLiveSetting.Off,
                    "fixture.action.coming-later", "Coming later"),
                new("findings-disclosure",
                    "fixture.automation.warning-disclosure",
                    "Inspection warning details",
                    ModelInspectionExpectedControlType.Group,
                    ModelInspectionExpectedLiveSetting.Off, null, null),
                new("content-list", "fixture.content.warning.heading",
                    "Inspection warnings", ModelInspectionExpectedControlType.List,
                    ModelInspectionExpectedLiveSetting.Polite, null, null),
                new(WarningRowId,
                    "fixture.warning.chat-template-missing.title",
                    "Chat template not reported",
                    ModelInspectionExpectedControlType.ListItem,
                    ModelInspectionExpectedLiveSetting.Off,
                    "fixture.warning.chat-template-missing.detail",
                    "The model does not report a chat template. Chat formatting may require manual configuration.")
            ],
            StressScreen.MaximumReportRows =>
            [
                model,
                report,
                choose,
                new("findings-disclosure",
                    "fixture.automation.invalid-disclosure",
                    "Model validation report",
                    ModelInspectionExpectedControlType.Group,
                    ModelInspectionExpectedLiveSetting.Off, null, null),
                new("content-list", "fixture.content.invalid.heading",
                    "Invalid model", ModelInspectionExpectedControlType.List,
                    ModelInspectionExpectedLiveSetting.Polite, null, null),
                new(InvalidRowId, "fixture.content.invalid.row",
                    "Structural validation did not produce a usable model result.",
                    ModelInspectionExpectedControlType.ListItem,
                    ModelInspectionExpectedLiveSetting.Off,
                    "fixture.content.invalid.detail", "Choose another model.")
            ],
            StressScreen.MaximumProgressDetail => ProgressAutomation(model),
            StressScreen.MaximumFailureDetail =>
            [
                model,
                choose,
                report,
                new("retry", "fixture.automation.action.retry",
                    "Retry model inspection", ModelInspectionExpectedControlType.Button,
                    ModelInspectionExpectedLiveSetting.Off, null, null),
                new("content-list", "fixture.content.failure.heading",
                    "Inspection did not complete",
                    ModelInspectionExpectedControlType.List,
                    ModelInspectionExpectedLiveSetting.Polite, null, null),
                new(FailureRowId, "fixture.content.failure.row",
                    "Model result unavailable",
                    ModelInspectionExpectedControlType.ListItem,
                    ModelInspectionExpectedLiveSetting.Off,
                    "fixture.failure.detail.maximum", MaximumFailureDetail)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };
    }

    private static AutomationOracle[] ProgressAutomation(AutomationOracle model) =>
    [
        model,
        new("progress-list", "fixture.content.progress.heading",
            "Inspection progress", ModelInspectionExpectedControlType.List,
            ModelInspectionExpectedLiveSetting.Polite, null, null),
        new("progress-1", "fixture.progress.check-model-package.label",
            "Check model package", ModelInspectionExpectedControlType.ListItem,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.progress.check-model-package.detail",
            "Checking the model package."),
        new("progress-2", "fixture.progress.read-configuration.label",
            "Read model configuration", ModelInspectionExpectedControlType.ListItem,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.progress.detail.maximum", MaximumProgressDetail),
        new("progress-3", "fixture.progress.validate-tokenizer.label",
            "Validate tokenizer and chat setup",
            ModelInspectionExpectedControlType.ListItem,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.progress.validate-tokenizer.detail",
            "Validating tokenizer and chat setup."),
        new("progress-4", "fixture.progress.validate-structure.label",
            "Validate model structure", ModelInspectionExpectedControlType.ListItem,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.progress.validate-structure.detail",
            "Validating model structure."),
        new("progress-5", "fixture.progress.confirm-runtime.label",
            "Confirm core runtime compatibility",
            ModelInspectionExpectedControlType.ListItem,
            ModelInspectionExpectedLiveSetting.Off,
            "fixture.progress.confirm-runtime.detail",
            "Confirming core runtime compatibility."),
        new("cancel", "fixture.automation.action.cancel",
            "Cancel model inspection", ModelInspectionExpectedControlType.Button,
            ModelInspectionExpectedLiveSetting.Off, null, null)
    ];

    private static void AssertFooter(
        ModelInspectionExpectedFooter footer,
        StressOracle oracle)
    {
        ModelInspectionExpectedFooterStatus overall = oracle.Screen switch
        {
            StressScreen.MaximumProgressDetail =>
                ModelInspectionExpectedFooterStatus.InProgress,
            StressScreen.MaximumFailureDetail =>
                ModelInspectionExpectedFooterStatus.Interrupted,
            StressScreen.MaximumReportRows =>
                ModelInspectionExpectedFooterStatus.NotComplete,
            _ => ModelInspectionExpectedFooterStatus.Complete
        };
        Assert.AreEqual(overall, footer.Status, oracle.Id);
    }

    private static void AssertAnnouncements(
        ModelInspectionExpectedAnnouncements announcements,
        StressOracle oracle)
    {
        int expectedCount = oracle.Screen == StressScreen.MaximumProgressDetail
            ? 2
            : 1;
        Assert.AreEqual(expectedCount, announcements.Count, oracle.Id);
        Assert.HasCount(expectedCount, announcements.Items, oracle.Id);
        int detailIndex = 0;
        if (oracle.Screen == StressScreen.MaximumProgressDetail)
        {
            AssertCopy(
                announcements.Items[0],
                "fixture.announcement.starting",
                "Model inspection is starting.",
                oracle.Id);
            detailIndex = 1;
        }

        (string Key, string Text) expected = oracle.Screen switch
        {
            StressScreen.MaximumModelName or
            StressScreen.MissingOptionalMetadata or
            StressScreen.MaximumCheckRows =>
                ("fixture.announcement.ready", "Model inspection complete."),
            StressScreen.MaximumFindingRows =>
                ("fixture.announcement.warning", "Model inspected with warnings."),
            StressScreen.MaximumReportRows =>
                ("fixture.announcement.invalid", "Model is invalid."),
            StressScreen.MaximumProgressDetail =>
                ("fixture.progress.detail.maximum", MaximumProgressDetail),
            StressScreen.MaximumFailureDetail =>
                ("fixture.announcement.failure",
                    "Model inspection could not be completed."),
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };
        AssertCopy(
            announcements.Items[detailIndex],
            expected.Key,
            expected.Text,
            oracle.Id);
    }

    private static void AssertPreset(
        ModelInspectionPresetExpectation preset,
        string presetId,
        StressOracle oracle)
    {
        PresetOracle expected = ExpectedPreset(presetId);
        Assert.AreEqual(expected.Layout, preset.ResponsiveLayout, oracle.Id);
        Assert.AreEqual(expected.MinimumWidth,
            preset.MinimumContentColumnWidth, oracle.Id);
        Assert.AreEqual(expected.MaximumWidth,
            preset.MaximumContentColumnWidth, oracle.Id);
        Assert.IsTrue(preset.NoClipping, oracle.Id);
        Assert.IsTrue(preset.NoOverlap, oracle.Id);
        Assert.IsTrue(preset.AllRequiredContentReachable, oracle.Id);
        Assert.AreEqual(44d, preset.MinimumPointerTargetWidth, oracle.Id);
        Assert.AreEqual(44d, preset.MinimumPointerTargetHeight, oracle.Id);
        Assert.AreEqual(expected.Resources, preset.Resources, oracle.Id);
        Assert.AreEqual(expected.TextScale, preset.TextScale, oracle.Id);
        Assert.AreEqual(expected.Motion, preset.Motion, oracle.Id);
        Assert.IsTrue(
            preset.SemanticBrushesResolvedWithoutColorOnlyMeaning,
            oracle.Id);
        Assert.IsTrue(
            preset.FinalGeometryAndSemanticsEquivalentToNormalMotion,
            oracle.Id);
        (int minimumAnimationStarts, int maximumAnimationStarts) =
            expected.Motion == ModelInspectionFixtureMotionProfile.Reduced
                ? (0, 0)
                : oracle.Screen switch
                {
                    StressScreen.MaximumModelName or
                    StressScreen.MissingOptionalMetadata => (1, 1),
                    StressScreen.MaximumCheckRows or
                    StressScreen.MaximumReportRows => (2, 2),
                    StressScreen.MaximumFindingRows => (7, 8),
                    StressScreen.MaximumProgressDetail => (3, 3),
                    StressScreen.MaximumFailureDetail => (1, 1),
                    _ => throw new ArgumentOutOfRangeException(nameof(oracle))
                };
        Assert.AreEqual(minimumAnimationStarts,
            preset.MinimumAnimationStarts, oracle.Id);
        Assert.AreEqual(maximumAnimationStarts,
            preset.MaximumAnimationStarts, oracle.Id);

        string[] textRoleIds = ExpectedTextRoleIds(oracle);
        CollectionAssert.AreEqual(textRoleIds,
            preset.TextRoles.Select(role => role.Id).ToArray(), oracle.Id);
        Assert.IsTrue(preset.TextRoles.All(role =>
            role.Behavior == ModelInspectionFixtureTextBehavior.Wrap), oracle.Id);
        Assert.AreEqual(ExpectedScrollOwner(oracle), preset.ScrollOwner, oracle.Id);
        CollectionAssert.AreEqual(ExpectedReadingOrder(oracle),
            preset.LogicalReadingOrder.ToArray(), oracle.Id);
        CollectionAssert.AreEqual(ExpectedTabOrder(oracle),
            preset.TabOrder.ToArray(), oracle.Id);
        Assert.AreEqual(
            ExpectedFocusTarget(oracle),
            preset.FocusTarget,
            oracle.Id);

        HashSet<string> retained = oracle.Screen == StressScreen.MaximumProgressDetail
            ? ExpectedRetainedIds(oracle).ToHashSet(StringComparer.Ordinal)
            : ExpectedRetainedIds(oracle).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(textRoleIds.All(retained.Contains), oracle.Id);
        if (preset.ScrollOwner is { } scrollOwner)
        {
            Assert.IsTrue(retained.Contains(scrollOwner), oracle.Id);
        }

        HashSet<string> automationIds = ExpectedAutomation(oracle)
            .Select(control => control.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(preset.LogicalReadingOrder.All(automationIds.Contains), oracle.Id);
        Assert.IsTrue(preset.TabOrder.All(automationIds.Contains), oracle.Id);
        Assert.IsTrue(
            automationIds.Contains(preset.FocusTarget) ||
            oracle.Screen == StressScreen.MaximumProgressDetail &&
            preset.FocusTarget.Equals(
                "page-heading",
                StringComparison.Ordinal),
            oracle.Id);
    }

    private static void AssertStressMaxima(StressBatch batch)
    {
        ValidatedModelInspectionFixture maximumName = batch.Catalogue.Fixtures
            .Single(fixture => fixture.Id == "MI-043");
        Assert.AreEqual(160, MaximumModelName.Length, "MI-043");
        Assert.AreEqual(160, maximumName.Input.Request.DisplayName.Length,
            "MI-043");

        ValidatedModelInspectionFixture missingMetadata = batch.Catalogue.Fixtures
            .Single(fixture => fixture.Id == "MI-044");
        string[] missingMetadataIds =
            ["metadata-publisher", "metadata-model-type", "metadata-context"];
        CollectionAssert.AreEqual(
            missingMetadataIds,
            missingMetadata.Expected.Model.Metadata
                .Where(field => field.Value.DefaultText == "Not reported")
                .Select(field => field.Id)
                .ToArray(),
            "MI-044");
        Assert.IsFalse(missingMetadata.Expected.Content.Visible, "MI-044");
        Assert.HasCount(0, missingMetadata.Expected.Content.Rows, "MI-044");

        Assert.AreEqual(5, batch.Catalogue.Fixtures.Single(
            fixture => fixture.Id == "MI-045").Expected.Model.Checks.Count,
            "MI-045");
        Assert.AreEqual(1, batch.Catalogue.Fixtures.Single(
            fixture => fixture.Id == "MI-046").Expected.Content.Rows.Count,
            "MI-046");
        Assert.AreEqual(1, batch.Catalogue.Fixtures.Single(
            fixture => fixture.Id == "MI-047").Expected.Content.Rows.Count,
            "MI-047");

        string progressPolicy = batch.Policy.Value.CopyRegistry[
            "fixture.progress.detail.maximum"];
        string progressDescriptor = batch.Catalogue.Fixtures.Single(
            fixture => fixture.Id == "MI-048").Expected.Content.Rows[1]
            .SecondaryText!.DefaultText;
        Assert.AreEqual(512, progressDescriptor.Length, "MI-048");
        Assert.AreEqual(MaximumProgressDetail, progressDescriptor, "MI-048");
        Assert.AreEqual(progressPolicy, progressDescriptor, "MI-048");

        string failurePolicy = batch.Policy.Value.CopyRegistry[
            "fixture.failure.detail.maximum"];
        ValidatedModelInspectionFixture maximumFailure = batch.Catalogue.Fixtures
            .Single(fixture => fixture.Id == "MI-049");
        string failureDescriptor = maximumFailure.Expected.Outcome.SupportingText!
            .DefaultText;
        Assert.AreEqual(512, failureDescriptor.Length, "MI-049");
        Assert.AreEqual(MaximumFailureDetail, failureDescriptor, "MI-049");
        Assert.AreEqual(failurePolicy, failureDescriptor, "MI-049");
        Assert.AreEqual(failureDescriptor,
            maximumFailure.Expected.Content.Rows[0].SecondaryText!.DefaultText,
            "MI-049");
    }

    private static void AssertPolicyPreset(
        ModelInspectionFixturePreset preset,
        string presetId,
        string fixtureId)
    {
        PresetOracle expected = ExpectedPreset(presetId);
        Assert.AreEqual(presetId, preset.Id, fixtureId);
        Assert.AreEqual(presetId switch
        {
            "P01" or "P02" or "P03" =>
                ModelInspectionFixtureWidthProfile.Desktop1440,
            "P04" or "P05" or "P06" =>
                ModelInspectionFixtureWidthProfile.Medium600,
            "P07" or "P08" =>
                ModelInspectionFixtureWidthProfile.Narrow360,
            _ => throw new ArgumentOutOfRangeException(
                nameof(presetId), presetId, null)
        }, preset.Width, fixtureId);
        Assert.AreEqual(expected.Resources, preset.Resources, fixtureId);
        Assert.AreEqual(expected.TextScale, preset.Text, fixtureId);
        Assert.AreEqual(expected.Motion, preset.Motion, fixtureId);
    }

    private static void AssertSetup(
        IReadOnlyList<ModelInspectionFixtureSetupStepDescriptor> actual,
        IReadOnlyList<(ModelInspectionFixtureSetupStepKind Kind, int? Attempt,
            string? Checkpoint, string? InteractionId)> expected,
        string id)
    {
        Assert.AreEqual(expected.Count, actual.Count, id);
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Kind, actual[index].Kind, id);
            Assert.AreEqual(expected[index].Attempt, actual[index].Attempt, id);
            Assert.AreEqual(expected[index].Checkpoint,
                actual[index].Checkpoint, id);
            Assert.AreEqual(expected[index].InteractionId,
                actual[index].InteractionId, id);
        }
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

    private static string[] ExpectedCoverageTags(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" =>
                ["figma.ready-collapsed", "outcome.ready",
                    "stress.maximum-model-name"],
            "MI-044" =>
                ["figma.ready-collapsed", "outcome.ready",
                    "stress.missing-optional-metadata"],
            "MI-045" =>
                ["figma.ready-expanded", "outcome.ready",
                    "stress.maximum-check-rows"],
            "MI-046" =>
                ["figma.ready-with-warnings-expanded",
                    "outcome.ready-with-warnings",
                    "stress.maximum-finding-rows"],
            "MI-047" =>
                ["figma.invalid-expanded", "outcome.invalid",
                    "stress.maximum-report-rows"],
            "MI-048" =>
                ["figma.inspection-progress",
                    "progress.read-model-configuration.active",
                    "stress.maximum-detail-copy"],
            "MI-049" =>
                ["figma.operational-failure",
                    "failure.worker-start-failure",
                    "stress.maximum-detail-copy"],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static ModelInspectionExpectedFigmaState ExpectedFigmaState(
        StressOracle oracle) => oracle.Id switch
        {
            "MI-043" or "MI-044" =>
                ModelInspectionExpectedFigmaState.ReadyCollapsed,
            "MI-045" => ModelInspectionExpectedFigmaState.ReadyExpanded,
            "MI-046" =>
                ModelInspectionExpectedFigmaState.ReadyWithWarningsExpanded,
            "MI-047" => ModelInspectionExpectedFigmaState.InvalidExpanded,
            "MI-048" => ModelInspectionExpectedFigmaState.InspectionProgress,
            "MI-049" => ModelInspectionExpectedFigmaState.OperationalFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static ModelInspectionFixtureInteractionKind[]
        ExpectedInteractionKinds(StressOracle oracle) => oracle.Id switch
        {
            "MI-043" or "MI-044" =>
            [
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            "MI-045" or "MI-046" or "MI-047" =>
            [
                ModelInspectionFixtureInteractionKind.Expand,
                ModelInspectionFixtureInteractionKind.Collapse,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            "MI-048" =>
            [
                ModelInspectionFixtureInteractionKind.Cancel,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            "MI-049" =>
            [
                ModelInspectionFixtureInteractionKind.Retry,
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                ModelInspectionFixtureInteractionKind.Reset
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static string[] ExpectedRowIds(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" or "MI-044" => [],
            "MI-045" =>
                ["check-package", "check-configuration", "check-tokenizer",
                    "check-structure", "check-runtime"],
            "MI-046" => [WarningRowId],
            "MI-047" => [InvalidRowId],
            "MI-048" =>
                ["progress-1", "progress-2", "progress-3", "progress-4",
                    "progress-5"],
            "MI-049" => [FailureRowId],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static string? ExpectedScrollOwner(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" or "MI-044" => null,
            "MI-045" => "model-card",
            "MI-046" or "MI-047" or "MI-049" => "content-list",
            "MI-048" => "progress-list",
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static string[] ExpectedTextRoleIds(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" => ["model-card"],
            "MI-044" =>
                ["metadata-context", "metadata-publisher",
                    "metadata-model-type"],
            "MI-045" =>
                ["check-package", "check-configuration", "check-tokenizer",
                    "check-structure", "check-runtime"],
            "MI-046" => [WarningRowId],
            "MI-047" => [InvalidRowId],
            "MI-048" =>
                ["progress-1", "progress-2", "progress-3", "progress-4",
                    "progress-5"],
            "MI-049" => [FailureRowId],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static string[] ExpectedReadingOrder(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" or "MI-044" or "MI-045" =>
                ["model-card", "choose-another", "technical-report",
                    "hardware-fit", "inspection-details-disclosure"],
            "MI-046" =>
                ["model-card", "choose-another", "technical-report",
                    "continue-hardware", "findings-disclosure", "content-list",
                    WarningRowId],
            "MI-047" =>
                ["model-card", "technical-report", "choose-another",
                    "findings-disclosure", "content-list", InvalidRowId],
            "MI-048" =>
                ["model-card", "progress-list", "progress-1", "progress-2",
                    "progress-3", "progress-4", "progress-5", "cancel"],
            "MI-049" =>
                ["model-card", "choose-another", "technical-report", "retry",
                    "content-list", FailureRowId],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static string[] ExpectedTabOrder(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" or "MI-044" or "MI-045" =>
                ["inspection-details-disclosure", "choose-another"],
            "MI-046" or "MI-047" =>
                ["findings-disclosure", "choose-another"],
            "MI-048" => ["cancel"],
            "MI-049" => ["choose-another", "retry"],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static string ExpectedFocusTarget(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-045" => "inspection-details-disclosure",
            "MI-046" or "MI-047" => "findings-disclosure",
            "MI-048" => "page-heading",
            _ => "choose-another"
        };

    private static string[] ExpectedRetainedIds(StressOracle oracle) =>
        oracle.Id switch
        {
            "MI-043" =>
            [
                "model-card", "metadata-publisher", "metadata-format",
                "metadata-quantisation", "metadata-parameters",
                "metadata-model-type", "metadata-context", "metadata-file-size",
                "choose-another", "technical-report", "hardware-fit",
                "inspection-details-disclosure"
            ],
            "MI-044" =>
            [
                "model-card", "metadata-publisher", "metadata-format",
                "metadata-quantisation", "metadata-parameters",
                "metadata-model-type", "metadata-context", "metadata-file-size",
                "choose-another",
                "technical-report", "hardware-fit",
                "inspection-details-disclosure"
            ],
            "MI-045" =>
            [
                "model-card", "metadata-publisher", "metadata-format",
                "metadata-quantisation", "metadata-parameters",
                "metadata-model-type", "metadata-context", "metadata-file-size",
                "check-package", "check-configuration", "check-tokenizer",
                "check-structure", "check-runtime", "choose-another",
                "technical-report", "hardware-fit",
                "inspection-details-disclosure"
            ],
            "MI-046" =>
            [
                "model-card", "content-list", WarningRowId, "choose-another",
                "technical-report", "continue-hardware", "findings-disclosure"
            ],
            "MI-047" =>
            [
                "model-card", "content-list", InvalidRowId, "technical-report",
                "choose-another", "findings-disclosure"
            ],
            "MI-048" =>
                ["model-card", "progress-list", "progress-1", "progress-2",
                    "progress-3", "progress-4", "progress-5", "cancel"],
            "MI-049" =>
            [
                "model-card", "content-list", FailureRowId, "choose-another",
                "technical-report", "retry"
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static void AssertInteractions(
        ValidatedModelInspectionFixture fixture,
        StressOracle oracle)
    {
        InteractionOracle[] expected = ExpectedInteractions(oracle);
        Assert.AreEqual(expected.Length, fixture.Interactions.Count, oracle.Id);
        for (int index = 0; index < expected.Length; index++)
        {
            InteractionOracle item = expected[index];
            ModelInspectionFixtureInteraction actual = fixture.Interactions[index];
            Assert.AreEqual(item.Id, actual.Id, oracle.Id);
            Assert.AreEqual(item.Kind, actual.Kind, oracle.Id);
            Assert.AreEqual(item.SourceCheckpoint,
                actual.SourceCheckpoint, oracle.Id);
            Assert.AreEqual(item.Target, actual.Target, oracle.Id);
            Assert.AreEqual(item.ExpectedFocus, actual.ExpectedFocus, oracle.Id);
            Assert.AreEqual(item.ExpectedAnnouncementCount,
                actual.ExpectedAnnouncementCount, oracle.Id);
            Assert.AreEqual(item.ExpectedFooterStatus,
                actual.ExpectedFooterStatus, oracle.Id);
            Assert.AreEqual(item.LifetimeEffect,
                actual.LifetimeEffect, oracle.Id);
        }

        CollectionAssert.AreEqual(
            expected
                .Where(item => item.SourceCheckpoint.Equals(
                    fixture.Input.ObservationCheckpoint,
                    StringComparison.Ordinal))
                .Select(item => item.Id)
                .ToArray(),
            fixture.VisibleInteractions.Select(item => item.Id).ToArray(),
            oracle.Id);
    }

    private static InteractionOracle[] ExpectedInteractions(
        StressOracle oracle) => oracle.Id switch
        {
            "MI-043" or "MI-044" =>
            [
                new("expand", ModelInspectionFixtureInteractionKind.Expand,
                    "observed", "MI-003",
                    "inspection-details-disclosure", 0,
                    ModelInspectionExpectedFooterStatus.Complete,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                new("choose-another",
                    ModelInspectionFixtureInteractionKind.ChooseAnother,
                    "observed", "gallery:no-active-fixture", null, 0,
                    ModelInspectionExpectedFooterStatus.Complete,
                    ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture),
                new("reset", ModelInspectionFixtureInteractionKind.Reset,
                    "observed", oracle.Id, "choose-another", 0,
                    ModelInspectionExpectedFooterStatus.Complete,
                    ModelInspectionFixtureInteractionLifetimeEffect.RetirePage)
            ],
            "MI-045" => ExpandedInteractions(
                oracle.Id,
                "MI-002",
                ModelInspectionExpectedFooterStatus.Complete),
            "MI-046" => ExpandedInteractions(
                oracle.Id,
                "MI-004",
                ModelInspectionExpectedFooterStatus.Complete),
            "MI-047" => ExpandedInteractions(
                oracle.Id,
                "MI-010",
                ModelInspectionExpectedFooterStatus.NotComplete),
            "MI-048" =>
            [
                new("cancel", ModelInspectionFixtureInteractionKind.Cancel,
                    "observed", "MI-012", "model-card", 0,
                    ModelInspectionExpectedFooterStatus.InProgress,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                new("reset", ModelInspectionFixtureInteractionKind.Reset,
                    "observed", "MI-048", "page-heading", 0,
                    ModelInspectionExpectedFooterStatus.InProgress,
                    ModelInspectionFixtureInteractionLifetimeEffect.RetirePage)
            ],
            "MI-049" =>
            [
                new("choose-another",
                    ModelInspectionFixtureInteractionKind.ChooseAnother,
                    "observed", "gallery:no-active-fixture", null, 0,
                    ModelInspectionExpectedFooterStatus.Interrupted,
                    ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture),
                new("retry", ModelInspectionFixtureInteractionKind.Retry,
                    "observed", "MI-002", "choose-another", 1,
                    ModelInspectionExpectedFooterStatus.Complete,
                    ModelInspectionFixtureInteractionLifetimeEffect.None),
                new("reset", ModelInspectionFixtureInteractionKind.Reset,
                    "observed", "MI-049", "choose-another", 0,
                    ModelInspectionExpectedFooterStatus.Interrupted,
                    ModelInspectionFixtureInteractionLifetimeEffect.RetirePage)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(oracle))
        };

    private static InteractionOracle[] ExpandedInteractions(
        string id,
        string collapsedTarget,
        ModelInspectionExpectedFooterStatus footerStatus)
    {
        string disclosureFocus = id.Equals("MI-045", StringComparison.Ordinal)
            ? "inspection-details-disclosure"
            : "findings-disclosure";
        return
        [
            new("expand", ModelInspectionFixtureInteractionKind.Expand,
                "terminal", "expanded", disclosureFocus, 0, footerStatus,
                ModelInspectionFixtureInteractionLifetimeEffect.None),
            new("collapse", ModelInspectionFixtureInteractionKind.Collapse,
                "expanded", collapsedTarget, disclosureFocus, 0, footerStatus,
                ModelInspectionFixtureInteractionLifetimeEffect.None),
            new("choose-another",
                ModelInspectionFixtureInteractionKind.ChooseAnother,
                "expanded", "gallery:no-active-fixture", null, 0, footerStatus,
                ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture),
            new("reset", ModelInspectionFixtureInteractionKind.Reset,
                "expanded", id, disclosureFocus, 0, footerStatus,
                ModelInspectionFixtureInteractionLifetimeEffect.RetirePage)
        ];
    }

    private static void AssertPolicyJsonMembers(
        JsonObject policy,
        StressOracle oracle)
    {
        AssertExactProperties(policy,
            ["$schema", "schemaVersion", "fixtures", "presets",
                "disclosurePairs", "gallerySwitchPairs", "copyRegistry",
                "externalEvidenceLinks"],
            oracle.Id);
        Assert.AreEqual("model-inspection-fixture.schema.json",
            policy["$schema"]!.GetValue<string>(), oracle.Id);
        Assert.AreEqual(1, policy["schemaVersion"]!.GetValue<int>(), oracle.Id);
        AssertExactProperties(PolicyEntry(policy, oracle.Id),
            ["id", "fileName", "targetCondition", "variant", "pairedWithId",
                "canonicalFigmaState", "requiredCoverageTags",
                "requiredInteractions", "requiredPresets"],
            oracle.Id);

        JsonObject[] presetDefinitions = policy["presets"]!.AsArray()
            .Select(node => node!.AsObject())
            .Where(node => node["id"]!.GetValue<string>() is "P01" ||
                node["id"]!.GetValue<string>() == oracle.ExtraPreset)
            .ToArray();
        Assert.HasCount(2, presetDefinitions, oracle.Id);
        foreach (JsonObject definition in presetDefinitions)
        {
            AssertExactProperties(definition,
                ["id", "width", "resources", "text", "motion"],
                oracle.Id);
        }
    }

    private static void AssertStrictJsonMembers(
        JsonObject document,
        StressOracle oracle)
    {
        AssertExactProperties(document,
            ["$schema", "schemaVersion", "id", "targetCondition", "variant",
                "title", "category", "coverage", "input", "expected",
                "presetExpectations", "interactions", "presets"],
            oracle.Id);

        AssertExactProperties(document["coverage"]!.AsObject(),
            ["figmaStates", "stages", "stageStatuses", "outcomes",
                "interactions", "lifecycleTags", "failureProfiles",
                "stressTags"],
            oracle.Id);

        JsonObject input = document["input"]!.AsObject();
        AssertExactProperties(input,
            ["request", "attempts", "setupSteps", "observationCheckpoint"],
            oracle.Id);
        AssertExactProperties(input["request"]!.AsObject(),
            ["displayName", "displayFileName", "evidenceProfile"],
            oracle.Id);
        foreach (JsonNode? attemptNode in input["attempts"]!.AsArray())
        {
            JsonObject attempt = attemptNode!.AsObject();
            AssertExactProperties(attempt, ["attempt", "serviceSteps"],
                oracle.Id);
            foreach (JsonNode? stepNode in attempt["serviceSteps"]!.AsArray())
            {
                JsonObject step = stepNode!.AsObject();
                AssertExactProperties(step, ["trigger", "effect"], oracle.Id);
                AssertExactProperties(step["trigger"]!.AsObject(),
                    ["kind", "checkpoint"], oracle.Id);
                JsonObject effect = step["effect"]!.AsObject();
                AssertExactProperties(effect,
                    ["kind", "progress", "outcome", "evidenceProfile",
                        "failureProfile", "failureDetailProfile",
                        "deferredCheckpoint"],
                    oracle.Id);
                if (effect["progress"] is JsonNode progress)
                {
                    AssertExactProperties(progress.AsObject(),
                        ["stage", "status", "completedStageCount", "fraction",
                            "detailProfile"],
                        oracle.Id);
                }
            }
        }

        foreach (JsonNode? setupNode in input["setupSteps"]!.AsArray())
        {
            AssertExactProperties(setupNode!.AsObject(),
                ["kind", "attempt", "checkpoint", "interactionId"],
                oracle.Id);
        }

        JsonObject expected = document["expected"]!.AsObject();
        AssertExactProperties(expected,
            ["figma", "outcome", "model", "content", "actions", "footer",
                "focus", "automation", "announcements", "rowsAndScroll",
                "retainedIdentities"],
            oracle.Id);
        AssertExactProperties(expected["figma"]!.AsObject(),
            ["state", "geometryProfile"], oracle.Id);

        JsonObject outcome = expected["outcome"]!.AsObject();
        AssertExactProperties(outcome,
            ["visible", "kind", "tone", "badge", "title", "supportingText"],
            oracle.Id);
        AssertCopyMembers(outcome["badge"], oracle.Id);
        AssertCopyMembers(outcome["title"], oracle.Id);
        AssertCopyMembers(outcome["supportingText"], oracle.Id);

        JsonObject model = expected["model"]!.AsObject();
        AssertExactProperties(model,
            ["visible", "mode", "badge", "displayName", "displayFileName",
                "metadata", "checks", "disclosureExpanded"],
            oracle.Id);
        AssertCopyMembers(model["displayName"], oracle.Id);
        AssertCopyMembers(model["displayFileName"], oracle.Id);
        foreach (JsonNode? metadataNode in model["metadata"]!.AsArray())
        {
            JsonObject metadata = metadataNode!.AsObject();
            AssertExactProperties(metadata, ["id", "label", "value"],
                oracle.Id);
            AssertCopyMembers(metadata["label"], oracle.Id);
            AssertCopyMembers(metadata["value"], oracle.Id);
        }

        foreach (JsonNode? checkNode in model["checks"]!.AsArray())
        {
            JsonObject check = checkNode!.AsObject();
            AssertExactProperties(check, ["id", "text", "status"], oracle.Id);
            AssertCopyMembers(check["text"], oracle.Id);
        }

        JsonObject content = expected["content"]!.AsObject();
        AssertExactProperties(content,
            ["visible", "mode", "heading", "rows", "disclosureExpanded"],
            oracle.Id);
        AssertCopyMembers(content["heading"], oracle.Id);
        foreach (JsonNode? rowNode in content["rows"]!.AsArray())
        {
            JsonObject row = rowNode!.AsObject();
            AssertExactProperties(row,
                ["id", "primaryText", "secondaryText", "status"],
                oracle.Id);
            AssertCopyMembers(row["primaryText"], oracle.Id);
            AssertCopyMembers(row["secondaryText"], oracle.Id);
        }

        JsonObject actions = expected["actions"]!.AsObject();
        AssertExactProperties(actions, ["visible", "mode", "items"], oracle.Id);
        foreach (JsonNode? itemNode in actions["items"]!.AsArray())
        {
            JsonObject item = itemNode!.AsObject();
            AssertExactProperties(item,
                ["id", "label", "visible", "enabled", "helpText"],
                oracle.Id);
            AssertCopyMembers(item["label"], oracle.Id);
            AssertCopyMembers(item["helpText"], oracle.Id);
        }

        JsonObject footer = expected["footer"]!.AsObject();
        AssertExactProperties(footer, ["status"], oracle.Id);

        AssertExactProperties(expected["focus"]!.AsObject(), ["target"],
            oracle.Id);
        JsonObject automation = expected["automation"]!.AsObject();
        AssertExactProperties(automation, ["controls"], oracle.Id);
        foreach (JsonNode? controlNode in automation["controls"]!.AsArray())
        {
            JsonObject control = controlNode!.AsObject();
            AssertExactProperties(control,
                ["id", "accessibleName", "controlType", "liveSetting",
                    "helpText"],
                oracle.Id);
            AssertCopyMembers(control["accessibleName"], oracle.Id);
            AssertCopyMembers(control["helpText"], oracle.Id);
        }

        JsonObject announcements = expected["announcements"]!.AsObject();
        AssertExactProperties(announcements, ["count", "items"], oracle.Id);
        foreach (JsonNode? itemNode in announcements["items"]!.AsArray())
        {
            AssertCopyMembers(itemNode, oracle.Id);
        }

        AssertExactProperties(expected["rowsAndScroll"]!.AsObject(),
            ["orderedRowIds", "scrollOwner"], oracle.Id);
        AssertExactProperties(expected["retainedIdentities"]!.AsObject(),
            ["ids"], oracle.Id);

        JsonObject presets = document["presetExpectations"]!.AsObject();
        CollectionAssert.AreEquivalent(new[] { "P01", oracle.ExtraPreset },
            presets.Select(item => item.Key).ToArray(), oracle.Id);
        foreach ((string _, JsonNode? presetNode) in presets)
        {
            JsonObject preset = presetNode!.AsObject();
            AssertExactProperties(preset,
                ["responsiveLayout", "minimumContentColumnWidth",
                    "maximumContentColumnWidth", "noClipping", "noOverlap",
                    "allRequiredContentReachable", "textRoles", "scrollOwner",
                    "minimumPointerTargetWidth", "minimumPointerTargetHeight",
                    "logicalReadingOrder", "tabOrder", "focusTarget",
                    "resources", "textScale", "motion",
                    "semanticBrushesResolvedWithoutColorOnlyMeaning",
                    "finalGeometryAndSemanticsEquivalentToNormalMotion",
                    "minimumAnimationStarts", "maximumAnimationStarts"],
                oracle.Id);
            foreach (JsonNode? roleNode in preset["textRoles"]!.AsArray())
            {
                AssertExactProperties(roleNode!.AsObject(), ["id", "behavior"],
                    oracle.Id);
            }
        }

        foreach (JsonNode? interactionNode in document["interactions"]!.AsArray())
        {
            AssertExactProperties(interactionNode!.AsObject(),
                ["id", "kind", "sourceCheckpoint", "target", "expectedFocus",
                    "expectedAnnouncementCount", "expectedFooterStatus",
                    "lifetimeEffect"],
                oracle.Id);
        }
    }

    private static void AssertCopyMembers(JsonNode? copy, string id)
    {
        if (copy is null)
        {
            return;
        }

        AssertExactProperties(copy.AsObject(), ["copyKey", "defaultText"], id);
    }

    private static void AssertExactProperties(
        JsonObject value,
        string[] expected,
        string id)
    {
        CollectionAssert.AreEquivalent(expected,
            value.Select(item => item.Key).ToArray(), id);
    }

    private static PresetOracle ExpectedPreset(string id) => id switch
    {
        "P01" => new(ModelInspectionFixtureResponsiveLayout.Desktop,
            480, 960, ModelInspectionFixtureResourceProfile.Light,
            ModelInspectionFixtureTextProfile.Standard100,
            ModelInspectionFixtureMotionProfile.Normal),
        "P02" => new(ModelInspectionFixtureResponsiveLayout.Desktop,
            480, 960, ModelInspectionFixtureResourceProfile.Dark,
            ModelInspectionFixtureTextProfile.Preview200,
            ModelInspectionFixtureMotionProfile.Reduced),
        "P03" => new(ModelInspectionFixtureResponsiveLayout.Desktop,
            480, 960, ModelInspectionFixtureResourceProfile.HighContrastPreview,
            ModelInspectionFixtureTextProfile.Standard100,
            ModelInspectionFixtureMotionProfile.Reduced),
        "P04" => new(ModelInspectionFixtureResponsiveLayout.Medium,
            360, 600, ModelInspectionFixtureResourceProfile.Light,
            ModelInspectionFixtureTextProfile.Preview200,
            ModelInspectionFixtureMotionProfile.Normal),
        "P05" => new(ModelInspectionFixtureResponsiveLayout.Medium,
            360, 600, ModelInspectionFixtureResourceProfile.Dark,
            ModelInspectionFixtureTextProfile.Standard100,
            ModelInspectionFixtureMotionProfile.Reduced),
        "P06" => new(ModelInspectionFixtureResponsiveLayout.Medium,
            360, 600, ModelInspectionFixtureResourceProfile.HighContrastPreview,
            ModelInspectionFixtureTextProfile.Preview200,
            ModelInspectionFixtureMotionProfile.Reduced),
        "P07" => new(ModelInspectionFixtureResponsiveLayout.Narrow,
            280, 360, ModelInspectionFixtureResourceProfile.Light,
            ModelInspectionFixtureTextProfile.Standard100,
            ModelInspectionFixtureMotionProfile.Reduced),
        "P08" => new(ModelInspectionFixtureResponsiveLayout.Narrow,
            280, 360, ModelInspectionFixtureResourceProfile.Dark,
            ModelInspectionFixtureTextProfile.Preview200,
            ModelInspectionFixtureMotionProfile.Normal),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    private static StressBatch LoadStressBatch()
    {
        string directory = ScenarioDirectory();
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                File.ReadAllBytes(Path.Combine(
                    directory, "model-inspection-fixture.schema.json"))));
        byte[] policyBytes = File.ReadAllBytes(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json"));
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                policyBytes), schema);
        ModelInspectionFixtureDocumentSource[] sources = policy.Value.Fixtures
            .Select(entry => new ModelInspectionFixtureDocumentSource(
                entry.FileName,
                File.ReadAllBytes(Path.Combine(directory, entry.FileName))))
            .ToArray();
        ModelInspectionFixtureCatalogue catalogue =
            ModelInspectionFixtureCatalogue.LoadDescriptors(
                sources, policy, schema);
        Dictionary<string, JsonObject> documents = policy.Value.Fixtures
            .ToDictionary(
                entry => entry.Id,
                entry => JsonNode.Parse(File.ReadAllText(Path.Combine(
                    directory, entry.FileName)))!.AsObject(),
                StringComparer.Ordinal);
        JsonObject policyDocument = JsonNode.Parse(policyBytes)!.AsObject();
        return new(catalogue, policy, documents, policyDocument);
    }

    private static StressBatch LoadMutatedStressBatch(
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
        Dictionary<string, JsonObject> documents = policyDocument["fixtures"]!
            .AsArray()
            .Select(node => node!.AsObject())
            .ToDictionary(
                entry => entry["id"]!.GetValue<string>(),
                entry => JsonNode.Parse(File.ReadAllText(Path.Combine(
                    directory,
                    entry["fileName"]!.GetValue<string>())))!.AsObject(),
                StringComparer.Ordinal);
        mutate(policyDocument, documents);

        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                Encoding.UTF8.GetBytes(policyDocument.ToJsonString())),
                schema);
        ModelInspectionFixtureDocumentSource[] sources = policy.Value.Fixtures
            .Select(entry => new ModelInspectionFixtureDocumentSource(
                entry.FileName,
                Encoding.UTF8.GetBytes(documents[entry.Id].ToJsonString())))
            .ToArray();
        ModelInspectionFixtureCatalogue catalogue =
            ModelInspectionFixtureCatalogue.LoadDescriptors(
                sources, policy, schema);
        return new(catalogue, policy, documents, policyDocument);
    }

    private static JsonObject PolicyEntry(JsonObject policy, string id) =>
        policy["fixtures"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(entry => entry["id"]!.GetValue<string>() == id);

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

    private enum StressScreen
    {
        MaximumModelName,
        MissingOptionalMetadata,
        MaximumCheckRows,
        MaximumFindingRows,
        MaximumReportRows,
        MaximumProgressDetail,
        MaximumFailureDetail
    }

    private sealed record StressBatch(
        ModelInspectionFixtureCatalogue Catalogue,
        ValidatedModelInspectionFixtureCoveragePolicy Policy,
        Dictionary<string, JsonObject> Documents,
        JsonObject PolicyDocument);

    private sealed record StressOracle(
        string Id,
        string FileName,
        string TargetCondition,
        string? Variant,
        ModelInspectionFixtureFigmaState FigmaState,
        ModelInspectionFixtureEvidenceProfile EvidenceProfile,
        ModelInspectionFixtureOutcome? Outcome,
        ModelInspectionFixtureStressTag StressTag,
        string StressCoverageTag,
        string ExtraPreset,
        StressScreen Screen);

    private sealed record ActionOracle(
        string Id,
        string LabelKey,
        string Label,
        bool Enabled,
        string? HelpKey,
        string? HelpText);

    private sealed record AutomationOracle(
        string Id,
        string NameKey,
        string Name,
        ModelInspectionExpectedControlType ControlType,
        ModelInspectionExpectedLiveSetting LiveSetting,
        string? HelpKey,
        string? HelpText);

    private sealed record PresetOracle(
        ModelInspectionFixtureResponsiveLayout Layout,
        double MinimumWidth,
        double MaximumWidth,
        ModelInspectionFixtureResourceProfile Resources,
        ModelInspectionFixtureTextProfile TextScale,
        ModelInspectionFixtureMotionProfile Motion);

    private sealed record InteractionOracle(
        string Id,
        ModelInspectionFixtureInteractionKind Kind,
        string SourceCheckpoint,
        string Target,
        string? ExpectedFocus,
        int ExpectedAnnouncementCount,
        ModelInspectionExpectedFooterStatus ExpectedFooterStatus,
        ModelInspectionFixtureInteractionLifetimeEffect LifetimeEffect);
}
