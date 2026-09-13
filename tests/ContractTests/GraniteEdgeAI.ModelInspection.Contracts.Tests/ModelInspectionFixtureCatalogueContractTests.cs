using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed partial class ModelInspectionFixtureCatalogueContractTests
{
    internal static readonly string[] ExpectedFixtureFileNames =
    [
        "MI-001-inspection-progress-initial.fixture.json",
        "MI-002-ready-clean-compatible-model-collapsed.fixture.json",
        "MI-003-ready-clean-compatible-model-expanded.fixture.json",
        "MI-004-ready-with-warnings-chat-template-missing-collapsed.fixture.json",
        "MI-005-ready-with-warnings-chat-template-missing-expanded.fixture.json",
        "MI-006-conversion-required-verified-incompatible-route-collapsed.fixture.json",
        "MI-007-conversion-required-verified-incompatible-route-expanded.fixture.json",
        "MI-008-incomplete-package-missing-package-member.fixture.json",
        "MI-009-unsupported-model-architecture.fixture.json",
        "MI-010-invalid-cross-source-evidence-contradiction-collapsed.fixture.json",
        "MI-011-invalid-cross-source-evidence-contradiction-expanded.fixture.json",
        "MI-012-cancelled-cooperative-user-cancellation.fixture.json",
        "MI-013-operational-failure-worker-start-failure.fixture.json",
        "MI-014-progress-check-model-package-active-fractionless.fixture.json",
        "MI-015-progress-check-model-package-completed.fixture.json",
        "MI-016-progress-read-model-configuration-active.fixture.json",
        "MI-017-progress-read-model-configuration-completed.fixture.json",
        "MI-018-progress-validate-tokenizer-chat-setup-active.fixture.json",
        "MI-019-progress-validate-tokenizer-chat-setup-completed.fixture.json",
        "MI-020-progress-validate-model-structure-active.fixture.json",
        "MI-021-progress-validate-model-structure-completed.fixture.json",
        "MI-022-progress-confirm-runtime-compatibility-active.fixture.json",
        "MI-023-progress-confirm-runtime-compatibility-completed.fixture.json",
        "MI-024-progress-cancel-requested.fixture.json",
        "MI-025-progress-chat-setup-warning.fixture.json",
        "MI-026-progress-model-structure-failed.fixture.json",
        "MI-027-progress-runtime-compatibility-cancelled.fixture.json",
        "MI-028-progress-read-model-configuration-active-bounded-fraction.fixture.json",
        "MI-029-cancellation-requested-cooperative-cancelled.fixture.json",
        "MI-030-cancellation-forced-operational-failure.fixture.json",
        "MI-031-retry-after-cancellation.fixture.json",
        "MI-032-retry-after-operational-failure.fixture.json",
        "MI-033-retry-stale-progress-rejected.fixture.json",
        "MI-034-retry-stale-result-rejected.fixture.json",
        "MI-035-retry-stale-motion-completion-rejected.fixture.json",
        "MI-036-retry-stale-announcement-rejected.fixture.json",
        "MI-037-choose-another-page-retired.fixture.json",
        "MI-038-gallery-switch-old-session-retired.fixture.json",
        "MI-039-operational-failure-worker-timeout.fixture.json",
        "MI-040-operational-failure-worker-crash-early-exit.fixture.json",
        "MI-041-operational-failure-malformed-worker-response.fixture.json",
        "MI-042-operational-failure-cancellation-unconfirmed.fixture.json",
        "MI-043-ready-model-name-maximum-collapsed.fixture.json",
        "MI-044-ready-missing-optional-metadata-not-reported-collapsed.fixture.json",
        "MI-045-ready-check-rows-current-maximum-expanded.fixture.json",
        "MI-046-ready-with-warnings-finding-rows-current-maximum-expanded.fixture.json",
        "MI-047-invalid-report-rows-current-maximum-expanded.fixture.json",
        "MI-048-progress-detail-copy-maximum.fixture.json",
        "MI-049-operational-failure-detail-copy-maximum.fixture.json",
        "MI-050-progress-starting-secure-inspection.fixture.json"
    ];

    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void BatchATerminalFixturesMatchProductionFooterContentAndActionContracts()
    {
        JsonObject ReadFixture(string fileName) => JsonNode.Parse(File.ReadAllText(
            Path.Combine(ScenarioDirectory(), fileName)))!.AsObject();

        static JsonObject Expected(JsonObject fixture) =>
            fixture["expected"]!.AsObject();

        static void AssertFooterNotComplete(JsonObject fixture)
        {
            JsonObject footer = Expected(fixture)["footer"]!.AsObject();
            Assert.AreEqual("notComplete", footer["status"]!.GetValue<string>());
        }

        static void AssertActions(
            JsonObject fixture,
            params (string Id, string Label, bool Visible, bool Enabled,
                string? HelpText)[] expected)
        {
            (string Id, string Label, bool Visible, bool Enabled, string? HelpText)[]
                actual = Expected(fixture)["actions"]!["items"]!.AsArray()
                    .Select(action => (
                        action!["id"]!.GetValue<string>(),
                        action["label"]!["defaultText"]!.GetValue<string>(),
                        action["visible"]!.GetValue<bool>(),
                        action["enabled"]!.GetValue<bool>(),
                        action["helpText"]?["defaultText"]?.GetValue<string>()))
                    .ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        static void AssertCanonicalCompletionInteraction(
            JsonObject fixture,
            string interactionId)
        {
            JsonObject interaction = fixture["interactions"]!.AsArray()
                .Select(node => node!.AsObject())
                .Single(item => item["id"]!.GetValue<string>() == interactionId);
            Assert.AreEqual("observed",
                interaction["sourceCheckpoint"]!.GetValue<string>());
            Assert.AreEqual("MI-002", interaction["target"]!.GetValue<string>());
            Assert.AreEqual("choose-another",
                interaction["expectedFocus"]!.GetValue<string>());
            Assert.AreEqual(1,
                interaction["expectedAnnouncementCount"]!.GetValue<int>());
            Assert.AreEqual("complete",
                interaction["expectedFooterStatus"]!.GetValue<string>());
            Assert.AreEqual("none",
                interaction["lifetimeEffect"]!.GetValue<string>());
        }

        JsonObject conversionCollapsed = ReadFixture(
            "MI-006-conversion-required-verified-incompatible-route-collapsed.fixture.json");
        JsonObject conversionExpanded = ReadFixture(
            "MI-007-conversion-required-verified-incompatible-route-expanded.fixture.json");
        AssertFooterNotComplete(conversionCollapsed);
        AssertFooterNotComplete(conversionExpanded);
        Assert.IsTrue(new[] { conversionCollapsed, conversionExpanded }.All(fixture =>
            fixture["interactions"]!.AsArray().All(interaction =>
                interaction!["expectedFooterStatus"]!.GetValue<string>() ==
                    "notComplete")));

        JsonObject cancelled = ReadFixture(
            "MI-012-cancelled-cooperative-user-cancellation.fixture.json");
        AssertFooterNotComplete(cancelled);
        JsonObject cancelledRow =
            Expected(cancelled)["content"]!["rows"]![0]!.AsObject();
        Assert.AreEqual("information", cancelledRow["status"]!.GetValue<string>());
        Assert.AreEqual(
            "No model result was produced because inspection was cancelled.",
            cancelledRow["secondaryText"]!["defaultText"]!.GetValue<string>());
        AssertCanonicalCompletionInteraction(cancelled, "restart");
        Assert.IsTrue(cancelled["interactions"]!.AsArray()
            .Where(interaction => interaction!["sourceCheckpoint"]!.GetValue<string>() ==
                "observed" && interaction["id"]!.GetValue<string>() != "restart")
            .All(interaction =>
                interaction!["expectedFooterStatus"]!.GetValue<string>() ==
                    "notComplete"));

        JsonObject incomplete = ReadFixture(
            "MI-008-incomplete-package-missing-package-member.fixture.json");
        Assert.AreEqual(
            "warning",
            Expected(incomplete)["content"]!["rows"]![0]!["status"]!
                .GetValue<string>());
        AssertActions(
            incomplete,
            ("choose-another", "Choose another model", true, true, null),
            ("technical-report", "View technical report", true, false,
                "Coming later"),
            ("locate-missing", "Locate missing file", true, true, null));
        JsonObject locateInteraction = incomplete["interactions"]!.AsArray()
            .Select(interaction => interaction!.AsObject())
            .Single(interaction =>
                interaction["id"]!.GetValue<string>() == "locate-missing");
        Assert.AreEqual(
            "chooseAnother",
            locateInteraction["kind"]!.GetValue<string>());
        Assert.AreEqual(
            "gallery:no-active-fixture",
            locateInteraction["target"]!.GetValue<string>());
        Assert.AreEqual(
            "noActiveFixture",
            locateInteraction["lifetimeEffect"]!.GetValue<string>());

        JsonObject operationalFailure = ReadFixture(
            "MI-013-operational-failure-worker-start-failure.fixture.json");
        AssertActions(
            operationalFailure,
            ("choose-another", "Choose another model", true, true, null),
            ("technical-report", "View technical report", true, false,
                "Coming later"),
            ("retry", "Retry inspection", true, true, null));
        AssertCanonicalCompletionInteraction(operationalFailure, "retry");
    }

    [TestMethod]
    public void AuthoritativeCatalogueFilesExistAndMatchExactStableOrder()
    {
        string directory = Path.Combine(
            Root,
            "tests",
            "TestFixtures",
            "ModelInspectionScenarios");
        Assert.IsTrue(
            Directory.Exists(directory),
            "The authoritative policy and 50 fixture descriptors are absent.");
        Assert.IsTrue(File.Exists(Path.Combine(
            directory,
            "model-inspection-fixture.schema.json")));
        Assert.IsTrue(File.Exists(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory, "README.md")));

        string[] actual = Directory.GetFiles(directory, "*.fixture.json")
            .Select(Path.GetFileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray()!;
        CollectionAssert.AreEqual(ExpectedFixtureFileNames, actual);
    }

    [TestMethod]
    public void AuthoritativeJsonFilesAreBomFreeLfOnlyAndWhitespaceClean()
    {
        string[] files = Directory.GetFiles(ScenarioDirectory(), "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.HasCount(52, files);
        var strictUtf8 = new UTF8Encoding(false, true);
        foreach (string file in files)
        {
            byte[] bytes = File.ReadAllBytes(file);
            Assert.IsFalse(
                bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF,
                Path.GetFileName(file));
            string text = strictUtf8.GetString(bytes);
            Assert.IsFalse(text.Contains('\r'), Path.GetFileName(file));
            Assert.IsTrue(text.EndsWith('\n'), Path.GetFileName(file));
            Assert.IsFalse(text.EndsWith("\n\n", StringComparison.Ordinal),
                Path.GetFileName(file));
            Assert.IsFalse(
                text.Split('\n').Any(line =>
                    line.EndsWith(' ') || line.EndsWith('\t')),
                Path.GetFileName(file));
        }
    }

    [TestMethod]
    public void InternalApplicationEnumsRemainTheExactCatalogueContract()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "InspectionProgress", "ReadyCollapsed", "ReadyExpanded",
                "ReadyWithWarningsCollapsed", "ReadyWithWarningsExpanded",
                "ConversionRequiredCollapsed", "ConversionRequiredExpanded",
                "IncompletePackage", "Unsupported", "InvalidCollapsed",
                "InvalidExpanded", "Cancelled", "OperationalFailure"
            },
            ParseEnumMembers(
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionFigmaState.cs",
                "ModelInspectionFigmaState"));
        CollectionAssert.AreEqual(
            new[]
            {
                "Ready", "ReadyWithWarnings", "ConversionRequired",
                "IncompletePackage", "Unsupported", "Invalid"
            },
            ParseEnumMembers(
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/ModelInspectionEnums.cs",
                "ModelInspectionOutcome"));
        CollectionAssert.AreEqual(
            new[]
            {
                "CheckModelPackage", "ReadModelConfiguration",
                "ValidateTokenizerAndChatSetup", "ValidateModelStructure",
                "ConfirmCoreRuntimeCompatibility"
            },
            ParseEnumMembers(
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/ModelInspectionEnums.cs",
                "ModelInspectionStage"));
        CollectionAssert.AreEqual(
            new[] { "Active", "Completed", "Warning", "Failed", "Cancelled" },
            ParseEnumMembers(
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/ModelInspectionEnums.cs",
                "ModelInspectionStageStatus"));
        CollectionAssert.AreEqual(
            new[]
            {
                "Expand", "Collapse", "Cancel", "Retry", "Restart",
                "ChooseAnother", "Reset"
            },
            ParseEnumMembers(
                "shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureEnums.cs",
                "ModelInspectionFixtureInteractionKind"));
    }

    [TestMethod]
    public void N001ExternalEvidencePrerequisitesRemainPresentAndAsserted()
    {
        Assert.IsTrue(File.Exists(Path.Combine(
            Root,
            "tests",
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf")));
        string source = File.ReadAllText(Path.Combine(
            Root,
            "tests",
            "UnitTests",
            "GraniteEdgeAI.UnitTests",
            "Features",
            "ModelInspection",
            "ModelInspectionPageNavigationTests.cs"));
        string body = ExtractMethodBody(
            source,
            "PackagedN001_PageJourneyCompletesAllFiveStagesAsReady");
        string[] orderedAssertions =
        [
            "Enum.GetValues<ModelInspectionStage>()",
            "ModelInspectionStageStatus.Active",
            "ModelInspectionStageStatus.Completed",
            "CollectionAssert.AreEqual",
            "ModelInspectionOutcome.Ready",
            "ModelInspectionFigmaState.ReadyExpanded",
            "ModelInspectionFigmaState.ReadyCollapsed"
        ];
        int cursor = -1;
        foreach (string assertion in orderedAssertions)
        {
            cursor = body.IndexOf(assertion, cursor + 1, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(
                0,
                cursor,
                $"The N-001 journey no longer proves '{assertion}' in order.");
        }
    }

    [TestMethod]
    public void AuthoritativeSchemaIsStrictAtEveryNestedBoundary()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        JsonObject contentDefinition = documents.Schema["$defs"]!["content"]!
            .AsObject();
        string[] contentRequired = contentDefinition["required"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();
        foreach (string optional in new[]
                 {
                     "startupStatus", "startupVisible", "startupActive"
                 })
        {
            CollectionAssert.DoesNotContain(contentRequired, optional);
            Assert.IsTrue(contentDefinition["properties"]!.AsObject()
                .ContainsKey(optional));
        }

        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(documents.SchemaSource());

        ModelInspectionFixtureCoverageValidator.ValidateSchemaContract(schema);

        Action<JsonObject>[] mutations =
        [
            root => RemoveStringValue(root["required"]!.AsArray(), "expected"),
            root => root["$defs"]!["expectedScreen"]!["additionalProperties"] = true,
            root => RemoveStringValue(
                root["$defs"]!["fixtureFigmaState"]!["enum"]!.AsArray(),
                "operationalFailure"),
            root => root["$defs"]!["coverage"]!["properties"]!["figmaStates"]!
                .AsObject().Remove("maxItems"),
            root => root["properties"]!["variant"]!["type"] = "string",
            root => RemoveStringValue(
                root["$defs"]!["content"]!["required"]!.AsArray(),
                "disclosureExpanded"),
            root => root["$defs"]!["content"]!["properties"]![
                "disclosureExpanded"]!["type"] = "string",
            root => root["$defs"]!["effect"]!["properties"]![
                "failureDetailProfile"]!["anyOf"]!.AsArray().RemoveAt(1),
            root => root["$defs"]!["copy"]!["properties"]!["defaultText"]!
                .AsObject().Remove("maxLength"),
            root =>
            {
                RemoveStringValue(root["required"]!.AsArray(), "coverage");
                root["$defs"]!["content"]!["additionalProperties"] = true;
                root["$defs"]!["effect"]!["properties"]![
                    "failureDetailProfile"]!["anyOf"]!.AsArray().RemoveAt(1);
                root["$defs"]!["presetExpectation"]!["properties"]![
                    "minimumPointerTargetWidth"]!.AsObject().Remove("minimum");
            }
        ];
        foreach (Action<JsonObject> mutate in mutations)
        {
            documents = ReadDocuments();
            mutate(documents.Schema);
            schema = ModelInspectionFixtureCatalogue.VerifySchema(
                documents.SchemaSource());
            AssertRule(
                "schema.authoritative-contract",
                () => ModelInspectionFixtureCoverageValidator
                    .ValidateSchemaContract(schema));
        }
    }

    [TestMethod]
    public void AuthoritativeSchemaRequiresSemanticArrayUniqueness()
    {
        (string Path, string[] Segments)[] uniqueArrays =
        [
            ("$.interactions", ["properties", "interactions"]),
            ("$.presets", ["properties", "presets"]),
            ("$.coverage.figmaStates",
                ["$defs", "coverage", "properties", "figmaStates"]),
            ("$.coverage.stages",
                ["$defs", "coverage", "properties", "stages"]),
            ("$.coverage.stageStatuses",
                ["$defs", "coverage", "properties", "stageStatuses"]),
            ("$.coverage.outcomes",
                ["$defs", "coverage", "properties", "outcomes"]),
            ("$.coverage.interactions",
                ["$defs", "coverage", "properties", "interactions"]),
            ("$.coverage.lifecycleTags",
                ["$defs", "coverage", "properties", "lifecycleTags"]),
            ("$.coverage.failureProfiles",
                ["$defs", "coverage", "properties", "failureProfiles"]),
            ("$.coverage.stressTags",
                ["$defs", "coverage", "properties", "stressTags"]),
            ("$.input.attempts",
                ["$defs", "input", "properties", "attempts"]),
            ("$.expected.model.metadata",
                ["$defs", "model", "properties", "metadata"]),
            ("$.expected.model.checks",
                ["$defs", "model", "properties", "checks"]),
            ("$.expected.content.rows",
                ["$defs", "content", "properties", "rows"]),
            ("$.expected.actions.items",
                ["$defs", "actions", "properties", "items"]),
            ("$.expected.automation.controls",
                ["$defs", "automation", "properties", "controls"]),
            ("$.expected.rowsAndScroll.orderedRowIds",
                ["$defs", "rowsAndScroll", "properties", "orderedRowIds"]),
            ("$.expected.retainedIdentities.ids",
                ["$defs", "retainedIdentities", "properties", "ids"]),
            ("$.presetExpectations.*.textRoles",
                ["$defs", "presetExpectation", "properties", "textRoles"]),
            ("$.presetExpectations.*.logicalReadingOrder",
                ["$defs", "presetExpectation", "properties",
                    "logicalReadingOrder"]),
            ("$.presetExpectations.*.tabOrder",
                ["$defs", "presetExpectation", "properties", "tabOrder"])
        ];

        MutableCatalogueDocuments documents = ReadDocuments();
        foreach ((string path, string[] segments) in uniqueArrays)
        {
            JsonObject arraySchema = ResolveObject(documents.Schema, segments);
            Assert.IsTrue(
                arraySchema["uniqueItems"]?.GetValue<bool>() == true,
                $"{path} must reject duplicate array items in schema tooling.");

            MutableCatalogueDocuments mutation = ReadDocuments();
            ResolveObject(mutation.Schema, segments).Remove("uniqueItems");
            VerifiedModelInspectionFixtureSchema mutatedSchema =
                ModelInspectionFixtureCatalogue.VerifySchema(
                    mutation.SchemaSource());
            AssertRule(
                "schema.authoritative-contract",
                () => ModelInspectionFixtureCoverageValidator
                    .ValidateSchemaContract(mutatedSchema));
        }

        static JsonObject ResolveObject(JsonObject root, string[] segments)
        {
            JsonNode current = root;
            foreach (string segment in segments)
            {
                current = current[segment] ?? throw new AssertFailedException(
                    $"Schema path segment '{segment}' is missing.");
            }

            return current.AsObject();
        }
    }

    [TestMethod]
    public void CatalogueCompletenessDiagnosticReportsNoMissingManifestEntries()
    {
        string directory = ScenarioDirectory();
        string[] available = Directory.GetFiles(directory, "*.fixture.json")
            .Select(Path.GetFileName)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToArray();

        IReadOnlyList<string> missing =
            ModelInspectionFixtureCoverageValidator.GetMissingFixtureIds(available);

        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                File.ReadAllBytes(Path.Combine(
                    directory,
                    "model-inspection-fixture.schema.json"))));
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                File.ReadAllBytes(Path.Combine(
                    directory,
                    "model-inspection-fixture-coverage-policy.json"))),
                schema);
        ModelInspectionFixtureDocumentSource[] sources = available
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(fileName => new ModelInspectionFixtureDocumentSource(
                fileName,
                File.ReadAllBytes(Path.Combine(directory, fileName))))
            .ToArray();
        if (missing.Count == 0)
        {
            _ = ModelInspectionFixtureCatalogue.LoadDescriptors(
                sources,
                policy,
                schema);
        }
        else
        {
            ModelInspectionFixtureValidationException exception =
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    ModelInspectionFixtureCatalogue.LoadDescriptors(
                        sources,
                        policy,
                        schema));
            Assert.AreEqual("catalogue.policy-count", exception.RuleCode);
        }

        Assert.AreEqual(
            0,
            missing.Count,
            $"Missing policy entries ({missing.Count}): {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void ProgressBatchDescriptorsMatchIndependentPerIdOracle()
    {
        ProgressBatchExpectation[] expectations =
        [
            new("MI-014", ModelInspectionFixtureStage.CheckModelPackage,
                ModelInspectionFixtureStageStatus.Active, 0, null),
            new("MI-015", ModelInspectionFixtureStage.CheckModelPackage,
                ModelInspectionFixtureStageStatus.Completed, 1, null),
            new("MI-016", ModelInspectionFixtureStage.ReadModelConfiguration,
                ModelInspectionFixtureStageStatus.Active, 1, null),
            new("MI-017", ModelInspectionFixtureStage.ReadModelConfiguration,
                ModelInspectionFixtureStageStatus.Completed, 2, null),
            new("MI-018", ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup,
                ModelInspectionFixtureStageStatus.Active, 2, null),
            new("MI-019", ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup,
                ModelInspectionFixtureStageStatus.Completed, 3, null),
            new("MI-020", ModelInspectionFixtureStage.ValidateModelStructure,
                ModelInspectionFixtureStageStatus.Active, 3, null),
            new("MI-021", ModelInspectionFixtureStage.ValidateModelStructure,
                ModelInspectionFixtureStageStatus.Completed, 4, null),
            new("MI-022", ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionFixtureStageStatus.Active, 4, null),
            new("MI-023", ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionFixtureStageStatus.Completed, 5, null),
            new("MI-024", ModelInspectionFixtureStage.CheckModelPackage,
                ModelInspectionFixtureStageStatus.Active, 0, null,
                CancelRequested: true),
            new("MI-025", ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup,
                ModelInspectionFixtureStageStatus.Warning, 3, null,
                TerminalOutcome: ModelInspectionFixtureOutcome.ReadyWithWarnings),
            new("MI-026", ModelInspectionFixtureStage.ValidateModelStructure,
                ModelInspectionFixtureStageStatus.Failed, 3, null,
                TerminalOutcome: ModelInspectionFixtureOutcome.Invalid),
            new("MI-027", ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionFixtureStageStatus.Cancelled, 4, null,
                TerminalIsCancellation: true),
            new("MI-028", ModelInspectionFixtureStage.ReadModelConfiguration,
                ModelInspectionFixtureStageStatus.Active, 1, 0.75,
                ReleasedFractions: [0.25, 0.75])
        ];
        string directory = ScenarioDirectory();
        string[] expectedFiles = ExpectedFixtureFileNames.Skip(13).Take(15).ToArray();
        string[] missing = expectedFiles
            .Where(fileName => !File.Exists(Path.Combine(directory, fileName)))
            .Select(fileName => fileName[..6])
            .ToArray();
        Assert.AreEqual(
            0,
            missing.Length,
            $"Missing progress batch descriptors ({missing.Length}): " +
            string.Join(", ", missing));

        ModelInspectionFixtureCatalogue catalogue = LoadThroughProgressBatch();
        string[] rowIds =
        [
            "progress-1", "progress-2", "progress-3", "progress-4", "progress-5"
        ];
        string[] retainedIds = ["model-card", "progress-list", .. rowIds, "cancel"];
        foreach (ProgressBatchExpectation oracle in expectations)
        {
            ValidatedModelInspectionFixture fixture = Fixture(catalogue, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureCategory.Progress, fixture.Category, oracle.Id);
            CollectionAssert.AreEqual(
                new[] { ModelInspectionFixtureFigmaState.InspectionProgress },
                fixture.Coverage.FigmaStates.ToArray(), oracle.Id);
            CollectionAssert.AreEqual(new[] { oracle.Stage },
                fixture.Coverage.Stages.ToArray(), oracle.Id);
            CollectionAssert.AreEqual(new[] { oracle.Status },
                fixture.Coverage.StageStatuses.ToArray(), oracle.Id);
            CollectionAssert.AreEqual(
                oracle.TerminalOutcome is ModelInspectionFixtureOutcome.ReadyWithWarnings or
                    ModelInspectionFixtureOutcome.Invalid
                    ? new[] { oracle.TerminalOutcome.Value }
                    : Array.Empty<ModelInspectionFixtureOutcome>(),
                fixture.Coverage.Outcomes.ToArray(), oracle.Id);
            CollectionAssert.AreEqual(
                new[] { ModelInspectionFixtureInteractionKind.Cancel,
                    ModelInspectionFixtureInteractionKind.Reset },
                fixture.Coverage.Interactions.ToArray(), oracle.Id);
            CollectionAssert.AreEqual(
                oracle.CancelRequested
                    ? new[] { ModelInspectionFixtureLifecycleTag.CancellationRequested }
                    : Array.Empty<ModelInspectionFixtureLifecycleTag>(),
                fixture.Coverage.LifecycleTags.ToArray(), oracle.Id);
            Assert.HasCount(0, fixture.Coverage.FailureProfiles, oracle.Id);
            Assert.HasCount(0, fixture.Coverage.StressTags, oracle.Id);
            CollectionAssert.AreEqual(new[] { "P01" }, fixture.Presets.ToArray(), oracle.Id);
            CollectionAssert.AreEquivalent(
                new[] { "P01" }, fixture.PresetExpectations.Keys.ToArray(), oracle.Id);

            Assert.HasCount(1, fixture.Input.Attempts, oracle.Id);
            ModelInspectionFixtureAttemptDescriptor attempt = fixture.Input.Attempts[0];
            Assert.AreEqual(1, attempt.Attempt, oracle.Id);
            ModelInspectionFixtureServiceEffectDescriptor[] terminals = attempt.ServiceSteps
                .Select(step => step.Effect)
                .Where(effect => effect.Kind is ModelInspectionFixtureServiceEffectKind.Completed or
                    ModelInspectionFixtureServiceEffectKind.Cancelled or
                    ModelInspectionFixtureServiceEffectKind.OperationalFailure)
                .ToArray();
            Assert.HasCount(1, terminals, oracle.Id);
            Assert.AreSame(attempt.ServiceSteps[^1].Effect, terminals[0], oracle.Id);
            Assert.IsNull(terminals[0].FailureProfile, oracle.Id);
            Assert.IsNull(terminals[0].FailureDetailProfile, oracle.Id);
            if (oracle.TerminalIsCancellation || oracle.CancelRequested)
            {
                Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Cancelled,
                    terminals[0].Kind, oracle.Id);
                Assert.IsNull(terminals[0].Outcome, oracle.Id);
                Assert.IsNull(terminals[0].EvidenceProfile, oracle.Id);
            }
            else
            {
                Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Completed,
                    terminals[0].Kind, oracle.Id);
                Assert.AreEqual(oracle.TerminalOutcome ?? ModelInspectionFixtureOutcome.Ready,
                    terminals[0].Outcome, oracle.Id);
                Assert.AreEqual(
                    oracle.TerminalOutcome switch
                    {
                        ModelInspectionFixtureOutcome.ReadyWithWarnings =>
                            ModelInspectionFixtureEvidenceProfile.MissingChatTemplate,
                        ModelInspectionFixtureOutcome.Invalid =>
                            ModelInspectionFixtureEvidenceProfile.CrossSourceContradiction,
                        _ => ModelInspectionFixtureEvidenceProfile.Compatible
                    },
                    terminals[0].EvidenceProfile, oracle.Id);
            }

            Dictionary<string, ModelInspectionFixtureServiceEffectDescriptor> byCheckpoint =
                attempt.ServiceSteps.ToDictionary(
                    step => step.Trigger.Checkpoint!, step => step.Effect,
                    StringComparer.Ordinal);
            ModelInspectionFixtureSetupStepDescriptor[] releases = fixture.Input.SetupSteps
                .Where(step => step.Kind ==
                    ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint)
                .ToArray();
            Assert.IsTrue(releases.All(step =>
                byCheckpoint[step.Checkpoint!].Kind ==
                    ModelInspectionFixtureServiceEffectKind.Progress), oracle.Id);
            Assert.IsFalse(releases.Any(step => ReferenceEquals(
                byCheckpoint[step.Checkpoint!], terminals[0])), oracle.Id);
            ModelInspectionFixtureProgressDescriptor observed =
                byCheckpoint[releases[^1].Checkpoint!].Progress!;
            Assert.AreEqual(oracle.Stage, observed.Stage, oracle.Id);
            Assert.AreEqual(oracle.Status, observed.Status, oracle.Id);
            Assert.AreEqual(oracle.CompletedStageCount, observed.CompletedStageCount, oracle.Id);
            Assert.AreEqual(oracle.ObservedFraction, observed.Fraction, oracle.Id);
            double?[] releasedFractions = releases
                .Select(step => byCheckpoint[step.Checkpoint!].Progress!.Fraction)
                .ToArray();
            CollectionAssert.AreEqual(
                oracle.ReleasedFractions ?? new double?[] { oracle.ObservedFraction },
                releasedFractions, oracle.Id);
            Assert.AreEqual(oracle.CancelRequested ? 1 : 0,
                fixture.Input.SetupSteps.Count(step =>
                    step.Kind == ModelInspectionFixtureSetupStepKind.InvokeCancel), oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureSetupStepKind.Observe,
                fixture.Input.SetupSteps[^1].Kind, oracle.Id);
            Assert.AreEqual("observed", fixture.Input.ObservationCheckpoint, oracle.Id);
            Assert.AreEqual("observed", fixture.Input.SetupSteps[^1].Checkpoint, oracle.Id);

            ModelInspectionExpectedScreen expected = fixture.Expected;
            Assert.AreEqual(ModelInspectionExpectedFigmaState.InspectionProgress,
                expected.Figma.State, oracle.Id);
            Assert.IsFalse(expected.Outcome.Visible, oracle.Id);
            Assert.AreEqual(ModelInspectionExpectedOutcomeKind.Hidden,
                expected.Outcome.Kind, oracle.Id);
            Assert.AreEqual(ModelInspectionExpectedModelMode.Compact,
                expected.Model.Mode, oracle.Id);
            Assert.AreEqual(ModelInspectionExpectedModelBadge.ModelSelected,
                expected.Model.Badge, oracle.Id);
            Assert.IsFalse(expected.Model.DisclosureExpanded, oracle.Id);
            Assert.HasCount(0, expected.Model.Metadata, oracle.Id);
            Assert.HasCount(0, expected.Model.Checks, oracle.Id);
            Assert.AreEqual(ModelInspectionExpectedContentMode.Progress,
                expected.Content.Mode, oracle.Id);
            Assert.IsFalse(expected.Content.DisclosureExpanded, oracle.Id);
            CollectionAssert.AreEqual(rowIds,
                expected.Content.Rows.Select(row => row.Id).ToArray(), oracle.Id);
            CollectionAssert.AreEqual(
                ExpectedProgressRowStatuses(oracle).ToArray(),
                expected.Content.Rows.Select(row => row.Status).ToArray(), oracle.Id);

            Assert.IsTrue(expected.Actions.Visible, oracle.Id);
            Assert.AreEqual(ModelInspectionExpectedActionMode.Inspecting,
                expected.Actions.Mode, oracle.Id);
            Assert.HasCount(1, expected.Actions.Items, oracle.Id);
            ModelInspectionExpectedAction cancel = expected.Actions.Items[0];
            Assert.AreEqual("cancel", cancel.Id, oracle.Id);
            Assert.AreEqual("fixture.action.cancel", cancel.Label.CopyKey, oracle.Id);
            Assert.AreEqual("Cancel inspection", cancel.Label.DefaultText, oracle.Id);
            Assert.AreEqual(!oracle.CancelRequested, cancel.Enabled, oracle.Id);
            ModelInspectionExpectedAutomationControl cancelAutomation =
                expected.Automation.Controls.Single(control => control.Id == "cancel");
            Assert.AreEqual("fixture.automation.action.cancel",
                cancelAutomation.AccessibleName.CopyKey, oracle.Id);
            Assert.AreEqual("Cancel model inspection",
                cancelAutomation.AccessibleName.DefaultText, oracle.Id);
            Assert.AreEqual(ModelInspectionExpectedControlType.Button,
                cancelAutomation.ControlType, oracle.Id);

            Assert.AreEqual(ModelInspectionExpectedFooterStatus.InProgress,
                expected.Footer.Status, oracle.Id);
            Assert.AreEqual(
                oracle.CancelRequested ? "model-card" : "page-heading",
                expected.Focus.Target,
                oracle.Id);
            Assert.AreEqual(2, expected.Announcements.Count, oracle.Id);
            Assert.HasCount(2, expected.Announcements.Items, oracle.Id);
            Assert.AreEqual(
                "fixture.announcement.starting",
                expected.Announcements.Items[0].CopyKey,
                oracle.Id);
            Assert.AreEqual(
                "Model inspection is starting.",
                expected.Announcements.Items[0].DefaultText,
                oracle.Id);
            (string detailKey, string detailText) = ProgressDetail(oracle.Stage);
            Assert.AreEqual(detailKey, expected.Announcements.Items[1].CopyKey, oracle.Id);
            Assert.AreEqual(detailText, expected.Announcements.Items[1].DefaultText, oracle.Id);
            CollectionAssert.AreEqual(rowIds,
                expected.RowsAndScroll.OrderedRowIds.ToArray(), oracle.Id);
            Assert.AreEqual("progress-list", expected.RowsAndScroll.ScrollOwner, oracle.Id);
            CollectionAssert.AreEqual(retainedIds,
                expected.RetainedIdentities.Ids.ToArray(), oracle.Id);

            ModelInspectionPresetExpectation p01 = fixture.PresetExpectations["P01"];
            Assert.AreEqual(ModelInspectionFixtureResponsiveLayout.Desktop,
                p01.ResponsiveLayout, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureResourceProfile.Light,
                p01.Resources, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureTextProfile.Standard100,
                p01.TextScale, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureMotionProfile.Normal,
                p01.Motion, oracle.Id);
            Assert.AreEqual("progress-list", p01.ScrollOwner, oracle.Id);
            CollectionAssert.AreEqual(
                oracle.CancelRequested ? Array.Empty<string>() : new[] { "cancel" },
                p01.TabOrder.ToArray(), oracle.Id);
            Assert.AreEqual(
                oracle.CancelRequested ? "model-card" : "page-heading",
                p01.FocusTarget,
                oracle.Id);

            Assert.HasCount(2, fixture.Interactions, oracle.Id);
            ModelInspectionFixtureInteraction cancelInteraction = fixture.Interactions
                .Single(interaction =>
                    interaction.Kind == ModelInspectionFixtureInteractionKind.Cancel);
            Assert.AreEqual(oracle.CancelRequested ? "progress" : "observed",
                cancelInteraction.SourceCheckpoint, oracle.Id);
            Assert.AreEqual(oracle.CancelRequested ? "cancelled" : "MI-012",
                cancelInteraction.Target, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureInteractionLifetimeEffect.None,
                cancelInteraction.LifetimeEffect, oracle.Id);
            ModelInspectionFixtureInteraction reset = fixture.Interactions.Single(
                interaction => interaction.Kind == ModelInspectionFixtureInteractionKind.Reset);
            Assert.AreEqual("observed", reset.SourceCheckpoint, oracle.Id);
            Assert.AreEqual(oracle.Id, reset.Target, oracle.Id);
            Assert.AreEqual(ModelInspectionFixtureInteractionLifetimeEffect.RetirePage,
                reset.LifetimeEffect, oracle.Id);
            string expectedResetFocus = oracle.CancelRequested
                ? "model-card"
                : "page-heading";
            Assert.AreEqual(expectedResetFocus, reset.ExpectedFocus);
        }

        ValidatedModelInspectionFixture warning = Fixture(catalogue, "MI-025");
        CollectionAssert.AreEqual(
            new[]
            {
                ModelInspectionFixtureStageStatus.Warning,
                ModelInspectionFixtureStageStatus.Completed,
                ModelInspectionFixtureStageStatus.Completed
            },
            warning.Input.Attempts[0].ServiceSteps
                .Where(step => step.Effect.Progress is not null)
                .Select(step => step.Effect.Progress!.Status).ToArray());
        ValidatedModelInspectionFixture fractional = Fixture(catalogue, "MI-028");
        Assert.AreEqual(2, fractional.Input.Attempts[0].ServiceSteps.Count(step =>
            step.Effect.Progress?.Stage ==
                ModelInspectionFixtureStage.ReadModelConfiguration));
        Assert.AreEqual(2, fractional.Expected.Announcements.Count);

        ModelInspectionFixtureCatalogue completeCatalogue = LoadCatalogue();
        ValidatedModelInspectionFixture startup = Fixture(
            completeCatalogue,
            "MI-050");
        Assert.AreEqual(ModelInspectionFixtureCategory.Progress, startup.Category);
        Assert.HasCount(0, startup.Coverage.Stages);
        Assert.HasCount(0, startup.Coverage.StageStatuses);
        Assert.HasCount(1, startup.Input.Attempts);
        Assert.HasCount(1, startup.Input.Attempts[0].ServiceSteps);
        Assert.AreEqual("terminal",
            startup.Input.Attempts[0].ServiceSteps[0].Trigger.Checkpoint);
        Assert.AreEqual(ModelInspectionFixtureServiceEffectKind.Completed,
            startup.Input.Attempts[0].ServiceSteps[0].Effect.Kind);
        Assert.HasCount(1, startup.Input.SetupSteps);
        Assert.AreEqual(ModelInspectionFixtureSetupStepKind.Observe,
            startup.Input.SetupSteps[0].Kind);
        Assert.AreEqual("Starting secure inspection…",
            startup.Expected.Content.StartupStatus?.DefaultText);
        Assert.AreEqual(true, startup.Expected.Content.StartupVisible);
        Assert.AreEqual(true, startup.Expected.Content.StartupActive);
        Assert.IsTrue(startup.Expected.Content.Rows.All(row =>
            row.Status == ModelInspectionExpectedRowStatus.Waiting));
        Assert.AreEqual(ModelInspectionExpectedFooterStatus.InProgress,
            startup.Expected.Footer.Status);
        Assert.AreEqual("page-heading", startup.Expected.Focus.Target);
        Assert.AreEqual(1, startup.Expected.Announcements.Count);
        Assert.IsTrue(startup.Expected.Actions.Items.Single(action =>
            action.Id == "cancel").Enabled);

        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
                 {
                     root => root["expected"]!["figma"]!["state"] =
                         "readyCollapsed",
                     root => root["input"]!["attempts"] = new JsonArray(),
                     root => root["expected"]!["content"]!["rows"]![0]![
                         "status"] = "active",
                     root => root["expected"]!["content"]!["rows"]![0]![
                         "status"] = "passed",
                     root => root["input"]!["setupSteps"]!.AsArray().Insert(
                         0,
                         new JsonObject
                         {
                             ["kind"] = "release-service-checkpoint",
                             ["attempt"] = 1,
                             ["checkpoint"] = "terminal",
                             ["interactionId"] = null
                         })
                 })
        {
            MutableCatalogueDocuments mutated = ReadDocuments();
            mutation(Descriptor(mutated, "MI-050").Document);
            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(
                () => LoadCatalogue(mutated));
        }
    }

    [TestMethod]
    public void CatalogueMatchesIndependentManifestAndSemanticCoverage()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();

        ValidatedModelInspectionFixtureCoverageCatalogue validated =
            ModelInspectionFixtureCoverageValidator.Validate(catalogue);
        Assert.AreSame(catalogue, validated.Catalogue);

        CollectionAssert.AreEqual(
            Enumerable.Range(1, 50).Select(value => $"MI-{value:000}").ToArray(),
            catalogue.Fixtures.Select(fixture => fixture.Id).ToArray());
        CollectionAssert.AreEqual(
            ExpectedFixtureFileNames,
            catalogue.Fixtures.Select(fixture => fixture.FileName).ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureFigmaState>(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Coverage.FigmaStates)
                .Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureInteractionKind>(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Interactions)
                .Select(interaction => interaction.Kind).Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureLifecycleTag>(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Coverage.LifecycleTags)
                .Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureStressTag>(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Coverage.StressTags)
                .Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureFailureProfile>(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Coverage.FailureProfiles)
                .Distinct().ToArray());

        (ModelInspectionFixtureStage Stage, ModelInspectionFixtureStageStatus Status)[]
            progressPairs = catalogue.Fixtures
                .SelectMany(fixture => fixture.Input.Attempts)
                .SelectMany(attempt => attempt.ServiceSteps)
                .Where(step => step.Effect.Progress is not null)
                .Select(step => (
                    step.Effect.Progress!.Stage,
                    step.Effect.Progress.Status))
                .Distinct()
                .ToArray();
        foreach (ModelInspectionFixtureStage stage in
                 Enum.GetValues<ModelInspectionFixtureStage>())
        {
            Assert.IsTrue(progressPairs.Contains(
                (stage, ModelInspectionFixtureStageStatus.Active)));
            Assert.IsTrue(progressPairs.Contains(
                (stage, ModelInspectionFixtureStageStatus.Completed)));
        }

        Assert.IsTrue(progressPairs.Contains((
            ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup,
            ModelInspectionFixtureStageStatus.Warning)));
        Assert.IsTrue(progressPairs.Contains((
            ModelInspectionFixtureStage.ValidateModelStructure,
            ModelInspectionFixtureStageStatus.Failed)));
        Assert.IsTrue(progressPairs.Contains((
            ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility,
            ModelInspectionFixtureStageStatus.Cancelled)));
    }

    [TestMethod]
    public void PolicyOwnsExactPresetMatrixAssignmentsPairsAndExternalEvidence()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ModelInspectionFixtureCoveragePolicy policy = catalogue.Policy.Value;
        (string Id, ModelInspectionFixtureWidthProfile Width,
            ModelInspectionFixtureResourceProfile Resources,
            ModelInspectionFixtureTextProfile Text,
            ModelInspectionFixtureMotionProfile Motion)[] expectedPresets =
        [
            ("P01", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal),
            ("P02", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P03", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P04", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Normal),
            ("P05", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P06", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P07", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ("P08", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Normal),
            ("P09", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal)
        ];
        CollectionAssert.AreEqual(
            expectedPresets,
            policy.Presets.Select(preset => (
                preset.Id,
                preset.Width,
                preset.Resources,
                preset.Text,
                preset.Motion)).ToArray());

        foreach (ModelInspectionFixturePolicyEntry entry in policy.Fixtures)
        {
            var expected = new List<string> { "P01" };
            int id = int.Parse(entry.Id.AsSpan(3));
            if (id is >= 43 and <= 49)
            {
                expected.Add($"P{id - 41:00}");
            }

            if (id == 3)
            {
                expected.Add("P09");
            }

            CollectionAssert.AreEqual(expected, entry.RequiredPresets.ToArray(), entry.Id);
        }

        CollectionAssert.AreEqual(
            new[] { ("MI-002", "MI-003"), ("MI-004", "MI-005"),
                ("MI-006", "MI-007"), ("MI-010", "MI-011") },
            policy.DisclosurePairs.Select(pair =>
                (pair.CollapsedId, pair.ExpandedId)).ToArray());
        CollectionAssert.AreEqual(
            new[] { ("MI-038", "MI-039") },
            policy.GallerySwitchPairs.Select(pair =>
                (pair.SourceId, pair.DestinationId)).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                ("MI-002", "N-001", "tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf",
                    "PackagedN001_PageJourneyCompletesAllFiveStagesAsReady"),
                ("MI-003", "N-001", "tests/TestFixtures/GGUF/N-001-vocab-only-spm.gguf",
                    "PackagedN001_PageJourneyCompletesAllFiveStagesAsReady")
            },
            policy.ExternalEvidenceLinks.Select(link => (
                link.FixtureId,
                link.EvidenceId,
                link.SourcePath,
                link.JourneyTest)).ToArray());

        Assert.AreEqual(37, CountCoveredCrossDimensionPairs(policy.Presets));
    }

    [TestMethod]
    public void EveryDescriptorHasOneFinalObservationAndTruthfulSpecialSetup()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        {
            Assert.IsGreaterThanOrEqualTo(1, fixture.Input.SetupSteps.Count, fixture.Id);
            Assert.AreEqual(
                ModelInspectionFixtureSetupStepKind.Observe,
                fixture.Input.SetupSteps[^1].Kind,
                fixture.Id);
            Assert.AreEqual(
                1,
                fixture.Input.SetupSteps.Count(step =>
                    step.Kind == ModelInspectionFixtureSetupStepKind.Observe),
                fixture.Id);
            Assert.AreEqual(
                fixture.Input.ObservationCheckpoint,
                fixture.Input.SetupSteps[^1].Checkpoint,
                fixture.Id);
        }

        ValidatedModelInspectionFixture initial = catalogue.Index.ById.ContainsKey("MI-001")
            ? catalogue.Fixtures.Single(fixture => fixture.Id == "MI-001")
            : throw new AssertFailedException();
        Assert.AreEqual(0, initial.Input.Attempts.Count);
        Assert.AreEqual(0, initial.Input.SetupSteps.Count(step =>
            step.Kind != ModelInspectionFixtureSetupStepKind.Observe));

        AssertSetupContains(catalogue, "MI-003", ModelInspectionFixtureSetupStepKind.InvokeDisclosure);
        AssertSetupContains(catalogue, "MI-005", ModelInspectionFixtureSetupStepKind.InvokeDisclosure);
        AssertSetupContains(catalogue, "MI-007", ModelInspectionFixtureSetupStepKind.InvokeDisclosure);
        AssertSetupContains(catalogue, "MI-011", ModelInspectionFixtureSetupStepKind.InvokeDisclosure);
        AssertSetupContains(catalogue, "MI-024", ModelInspectionFixtureSetupStepKind.InvokeCancel);
        AssertSetupContains(catalogue, "MI-034", ModelInspectionFixtureSetupStepKind.InvokeRetry);
        AssertSetupContains(catalogue, "MI-034", ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot);
        Assert.IsFalse(catalogue.Fixtures.Single(fixture => fixture.Id == "MI-037")
            .Input.SetupSteps.Any(step =>
                step.Kind == ModelInspectionFixtureSetupStepKind.InvokeChooseAnother));
        Assert.IsFalse(catalogue.Fixtures.Single(fixture => fixture.Id == "MI-038")
            .Input.SetupSteps.Any(step =>
                step.Kind == ModelInspectionFixtureSetupStepKind.InvokeChooseAnother));
    }

    [TestMethod]
    public void StressDescriptorsAssertOnlyCurrentSupportedMaxima()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        Assert.AreEqual(160, Fixture(catalogue, "MI-043").Input.Request.DisplayName.Length);
        Assert.IsTrue(Fixture(catalogue, "MI-044").Expected.Model.Metadata.Any(field =>
            field.Value.DefaultText == "Not reported"));
        Assert.AreEqual(5, Fixture(catalogue, "MI-045").Expected.Model.Checks.Count);
        Assert.AreEqual(1, Fixture(catalogue, "MI-046").Expected.Content.Rows.Count);
        Assert.AreEqual(1, Fixture(catalogue, "MI-047").Expected.Content.Rows.Count);
        Assert.IsTrue(AllCopies(Fixture(catalogue, "MI-048").Expected)
            .Any(copy => copy.DefaultText.Length == 512));
        Assert.IsTrue(AllCopies(Fixture(catalogue, "MI-049").Expected)
            .Any(copy => copy.DefaultText.Length == 512));
    }

    [TestMethod]
    public void ExpandedReadyCheckTextRolesMatchRenderedRowsInEveryRequiredPreset()
    {
        string[] expectedIds =
        [
            "check-package",
            "check-configuration",
            "check-tokenizer",
            "check-structure",
            "check-runtime"
        ];
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture expanded = Fixture(catalogue, "MI-003");

        foreach (string presetId in new[] { "P01", "P09" })
        {
            ModelInspectionPresetExpectation preset =
                expanded.PresetExpectations[presetId];
            CollectionAssert.AreEqual(
                expectedIds,
                preset.TextRoles.Select(role => role.Id).ToArray(),
                $"MI-003/{presetId}");
            Assert.IsTrue(
                preset.TextRoles.All(role =>
                    role.Behavior == ModelInspectionFixtureTextBehavior.Wrap),
                $"MI-003/{presetId}");
        }

        CollectionAssert.AreEqual(
            Fixture(catalogue, "MI-045").PresetExpectations["P01"].TextRoles
                .Select(role => $"{role.Id}:{role.Behavior}")
                .ToArray(),
            expanded.PresetExpectations["P01"].TextRoles
                .Select(role => $"{role.Id}:{role.Behavior}")
                .ToArray());
    }

    [TestMethod]
    public void FailureDetailProfileIsClosedRequiredAndBoundToFailureEffects()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ModelInspectionFixtureServiceEffectDescriptor[] effects = catalogue.Fixtures
            .SelectMany(fixture => fixture.Input.Attempts)
            .SelectMany(attempt => attempt.ServiceSteps)
            .Select(step => step.Effect)
            .ToArray();
        foreach (ModelInspectionFixtureServiceEffectDescriptor effect in effects)
        {
            if (effect.Kind == ModelInspectionFixtureServiceEffectKind.OperationalFailure)
            {
                Assert.IsNotNull(effect.FailureProfile);
                Assert.IsNotNull(effect.FailureDetailProfile);
            }
            else
            {
                Assert.IsNull(effect.FailureDetailProfile);
            }
        }

        ModelInspectionFixtureServiceEffectDescriptor maximum =
            Fixture(catalogue, "MI-049").Input.Attempts
                .SelectMany(attempt => attempt.ServiceSteps)
                .Select(step => step.Effect)
                .Single(effect =>
                    effect.Kind == ModelInspectionFixtureServiceEffectKind.OperationalFailure);
        Assert.AreEqual(
            ModelInspectionFixtureFailureDetailProfile.Maximum,
            maximum.FailureDetailProfile);
        Assert.IsTrue(catalogue.Fixtures
            .Where(fixture => fixture.Id != "MI-049")
            .SelectMany(fixture => fixture.Input.Attempts)
            .SelectMany(attempt => attempt.ServiceSteps)
            .Select(step => step.Effect)
            .Where(effect =>
                effect.Kind == ModelInspectionFixtureServiceEffectKind.OperationalFailure)
            .All(effect => effect.FailureDetailProfile ==
                ModelInspectionFixtureFailureDetailProfile.Default));

        MutableCatalogueDocuments documents = ReadDocuments();
        OperationalFailureEffect(documents, "MI-013")
            .Remove("failureDetailProfile");
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadCatalogue(documents));

        documents = ReadDocuments();
        OperationalFailureEffect(documents, "MI-013")["failureDetailProfile"] =
            "unknown";
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadCatalogue(documents));

        documents = ReadDocuments();
        OperationalFailureEffect(documents, "MI-013")["failureDetailProfile"] = null;
        AssertRule("service-effect.payload", () => LoadCatalogue(documents));

        documents = ReadDocuments();
        JsonObject readyEffect = Descriptor(documents, "MI-002").Document[
            "input"]!["attempts"]![0]!["serviceSteps"]![0]!["effect"]!.AsObject();
        readyEffect["failureDetailProfile"] = "default";
        AssertRule("service-effect.payload", () => LoadCatalogue(documents));
    }

    [TestMethod]
    public void DeleteAndRenameMutationsFailAtomically()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        documents.Descriptors.RemoveAt(10);
        AssertRule("catalogue.policy-count", () => LoadCatalogue(documents));

        documents = ReadDocuments();
        documents.Descriptors[10] = (
            "MI-011-invalid-cross-source-evidence-contradiction-expanded-renamed.fixture.json",
            documents.Descriptors[10].Document);
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadCatalogue(documents));
    }

    [TestMethod]
    public void JointPolicyAndDescriptorTargetSlugMutationStillFailsIndependentManifest()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        const string newFile =
            "MI-013-operational-failure-generic.fixture.json";
        JsonObject policyEntry = PolicyEntry(documents, "MI-013");
        policyEntry["fileName"] = newFile;
        policyEntry["targetCondition"] = "operational-failure-generic";
        (string FileName, JsonObject Document) descriptor =
            Descriptor(documents, "MI-013");
        descriptor.Document["targetCondition"] = "operational-failure-generic";
        ReplaceDescriptor(documents, "MI-013", newFile, descriptor.Document);

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.manifest",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));
    }

    [TestMethod]
    public void JointPolicyAndDescriptorCoverageTagRemovalStillFailsSemanticBinding()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        RemoveStringValue(
            PolicyEntry(documents, "MI-013")["requiredCoverageTags"]!.AsArray(),
            "failure.worker-start-failure");
        RemoveStringValue(
            Descriptor(documents, "MI-013").Document["coverage"]![
                "failureProfiles"]!.AsArray(),
            "workerStartFailure");

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.semantic-binding",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));
    }

    [TestMethod]
    public void UnreleasedFutureTerminalDoesNotSatisfyCoverageTags()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        PolicyEntry(documents, "MI-014")["requiredCoverageTags"]!.AsArray()
            .Add("outcome.ready");
        Descriptor(documents, "MI-014").Document["coverage"]!["outcomes"]!
            .AsArray().Add("ready");

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.semantic-binding",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));
    }

    [TestMethod]
    public void InteractionTransitionAndUnpairedDuplicateTargetMutationsFail()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        Descriptor(documents, "MI-002").Document["interactions"]![0]!["target"] =
            "MI-004";
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.transition",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));

        documents = ReadDocuments();
        JsonObject policyEntry = PolicyEntry(documents, "MI-039");
        policyEntry["targetCondition"] =
            "operational-failure-worker-start-failure";
        policyEntry["fileName"] =
            "MI-039-operational-failure-worker-start-failure.fixture.json";
        JsonObject descriptor = Descriptor(documents, "MI-039").Document;
        descriptor["targetCondition"] =
            "operational-failure-worker-start-failure";
        ReplaceDescriptor(
            documents,
            "MI-039",
            "MI-039-operational-failure-worker-start-failure.fixture.json",
            descriptor);
        AssertRule("policy.duplicate-target-unpaired", () => LoadCatalogue(documents));
    }

    [TestMethod]
    public void CopyKeyAndTextMutationsFailIndependently()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        Descriptor(documents, "MI-002").Document["expected"]!["outcome"]!["title"]![
            "copyKey"] = "fixture.unknown.copy";
        AssertRule("expected.copy-registry", () => LoadCatalogue(documents));

        documents = ReadDocuments();
        Descriptor(documents, "MI-002").Document["expected"]!["outcome"]!["title"]![
            "defaultText"] = "Changed without approval";
        AssertRule("expected.copy-registry", () => LoadCatalogue(documents));
    }

    [TestMethod]
    public void JointCopyRegistryAndDescriptorMutationStillFailsIndependentClosure()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        const string key = "fixture.outcome.ready.title";
        const string changed = "Ready after an unapproved copy change";
        documents.Policy["copyRegistry"]![key] = changed;
        foreach ((_, JsonObject descriptor) in documents.Descriptors)
        {
            ReplaceCopyDefaultText(descriptor, key, changed);
        }

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.copy-registry",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));
    }

    [TestMethod]
    public void UnusedCopyRegistryEntryFailsIndependentClosure()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        documents.Policy["copyRegistry"]!["fixture.unused"] = "Unused";

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.copy-registry",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));
    }

    [TestMethod]
    public void JointPresetMutationFailsExactMatrixAndPairwiseClosure()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        JsonObject p09 = documents.Policy["presets"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(preset => preset["id"]!.GetValue<string>() == "P09");
        p09["resources"] = "dark";
        Descriptor(documents, "MI-003").Document["presetExpectations"]!["P09"]![
            "resources"] = "dark";

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue(documents);
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(catalogue));
    }

    [TestMethod]
    public void PresetExpectationsBindLayoutReachabilityMotionAndReferences()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        Descriptor(documents, "MI-002").Document["presetExpectations"]!["P01"]![
            "responsiveLayout"] = "narrow";
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));

        documents = ReadDocuments();
        Descriptor(documents, "MI-002").Document["presetExpectations"]!["P01"]![
            "noClipping"] = false;
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));

        documents = ReadDocuments();
        Descriptor(documents, "MI-043").Document["presetExpectations"]!["P02"]![
            "maximumAnimationStarts"] = 1;
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));

        documents = ReadDocuments();
        Descriptor(documents, "MI-002").Document["presetExpectations"]!["P01"]![
            "focusTarget"] = "missing-control";
        AssertRule(
            "coverage.identity-closure",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));
    }

    [TestMethod]
    public void PresetSemanticAndMotionOraclesCoverEveryDescriptorPresetPair()
    {
        const string semanticBrushes =
            "semanticBrushesResolvedWithoutColorOnlyMeaning";
        const string normalMotionEquivalence =
            "finalGeometryAndSemanticsEquivalentToNormalMotion";
        MutableCatalogueDocuments documents = ReadDocuments();
        int expectationCount = 0;

        foreach ((_, JsonObject descriptor) in documents.Descriptors)
        {
            string fixtureId = descriptor["id"]!.GetValue<string>();
            foreach ((string presetId, JsonNode? node) in
                     descriptor["presetExpectations"]!.AsObject())
            {
                JsonObject expectation = node!.AsObject();
                JsonNode? semanticBrushesValue = expectation[semanticBrushes];
                JsonNode? normalMotionEquivalenceValue =
                    expectation[normalMotionEquivalence];
                Assert.IsNotNull(
                    semanticBrushesValue,
                    $"{fixtureId}/{presetId}/{semanticBrushes}");
                Assert.IsNotNull(
                    normalMotionEquivalenceValue,
                    $"{fixtureId}/{presetId}/{normalMotionEquivalence}");
                Assert.IsTrue(
                    semanticBrushesValue.GetValue<bool>(),
                    $"{fixtureId}/{presetId}/{semanticBrushes}");
                Assert.IsTrue(
                    normalMotionEquivalenceValue.GetValue<bool>(),
                    $"{fixtureId}/{presetId}/{normalMotionEquivalence}");
                expectationCount++;
            }
        }

        Assert.AreEqual(58, expectationCount);
    }

    [TestMethod]
    public void JointPresetSemanticAndMotionOracleMutationFailsClosed()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        JsonObject expectation = Descriptor(documents, "MI-003").Document[
            "presetExpectations"]!["P09"]!.AsObject();
        expectation["semanticBrushesResolvedWithoutColorOnlyMeaning"] = false;
        expectation["finalGeometryAndSemanticsEquivalentToNormalMotion"] =
            false;

        AssertRule(
            "fixture.preset-expectation",
            () => LoadCatalogue(documents));
    }

    [TestMethod]
    public void MotionExpectationDtoUsesExplicitBounds()
    {
        Type type = typeof(ModelInspectionPresetExpectation);

        Assert.IsNotNull(type.GetProperty("MinimumAnimationStarts"));
        Assert.IsNotNull(type.GetProperty("MaximumAnimationStarts"));
        Assert.IsNull(type.GetProperty("ExpectedAnimationStarts"));
    }

    [TestMethod]
    public void AuthoritativeSchemaRequiresBoundedMotionMembers()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        JsonObject preset = documents.Schema["$defs"]!["presetExpectation"]!
            .AsObject();
        JsonArray required = preset["required"]!.AsArray();
        JsonObject properties = preset["properties"]!.AsObject();

        CollectionAssert.Contains(
            required.Select(node => node!.GetValue<string>()).ToArray(),
            "minimumAnimationStarts");
        CollectionAssert.Contains(
            required.Select(node => node!.GetValue<string>()).ToArray(),
            "maximumAnimationStarts");
        CollectionAssert.DoesNotContain(
            required.Select(node => node!.GetValue<string>()).ToArray(),
            "expectedAnimationStarts");
        Assert.IsFalse(properties.ContainsKey("expectedAnimationStarts"));
        AssertMotionBound(properties["minimumAnimationStarts"]);
        AssertMotionBound(properties["maximumAnimationStarts"]);

        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
                 {
                     root => RemoveStringValue(
                         root["$defs"]!["presetExpectation"]!["required"]!
                             .AsArray(),
                         "minimumAnimationStarts"),
                     root => root["$defs"]!["presetExpectation"]!["properties"]![
                         "maximumAnimationStarts"]!["maximum"] = 129,
                     root => root["$defs"]!["presetExpectation"]!["properties"]![
                         "expectedAnimationStarts"] = new JsonObject
                         {
                             ["type"] = "integer",
                             ["minimum"] = 0,
                             ["maximum"] = 128
                         }
                 })
        {
            documents = ReadDocuments();
            mutation(documents.Schema);
            VerifiedModelInspectionFixtureSchema schema =
                ModelInspectionFixtureCatalogue.VerifySchema(
                    documents.SchemaSource());
            AssertRule(
                "schema.authoritative-contract",
                () => ModelInspectionFixtureCoverageValidator
                    .ValidateSchemaContract(schema));
        }

        static void AssertMotionBound(JsonNode? node)
        {
            Assert.IsNotNull(node);
            JsonObject bound = node.AsObject();
            Assert.AreEqual("integer", bound["type"]!.GetValue<string>());
            Assert.AreEqual(0, bound["minimum"]!.GetValue<int>());
            Assert.AreEqual(128, bound["maximum"]!.GetValue<int>());
        }
    }

    [TestMethod]
    public void MotionExpectationBoundsMatchIndependentReleasedTraceOracle()
    {
        (int Minimum, int Maximum)[] p01 =
        [
            (0, 0), (1, 1), (2, 2), (6, 7), (7, 8), (1, 1), (2, 2),
            (1, 1), (1, 1), (1, 1), (2, 2), (3, 3), (1, 1), (2, 2),
            (1, 1), (3, 3), (2, 2), (4, 4), (3, 3), (5, 5), (4, 4),
            (6, 6), (5, 5), (2, 2), (3, 3), (4, 4), (5, 5), (3, 3),
            (3, 3), (3, 3), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2),
            (2, 2), (1, 1), (1, 1), (1, 1), (1, 1), (1, 1), (1, 1),
            (1, 1), (1, 1), (2, 2), (7, 8), (2, 2), (3, 3), (1, 1),
            (0, 0)
        ];
        Assert.HasCount(50, p01);
        MutableCatalogueDocuments documents = ReadDocuments();
        for (int index = 0; index < p01.Length; index++)
        {
            string id = $"MI-{index + 1:000}";
            AssertBounds(
                Descriptor(documents, id).Document,
                "P01",
                p01[index]);
        }

        (string Id, string Preset, int Minimum, int Maximum)[] extras =
        [
            ("MI-003", "P09", 2, 2),
            ("MI-043", "P02", 0, 0),
            ("MI-044", "P03", 0, 0),
            ("MI-045", "P04", 2, 2),
            ("MI-046", "P05", 0, 0),
            ("MI-047", "P06", 0, 0),
            ("MI-048", "P07", 0, 0),
            ("MI-049", "P08", 1, 1)
        ];
        foreach ((string id, string preset, int minimum, int maximum) in extras)
        {
            AssertBounds(
                Descriptor(documents, id).Document,
                preset,
                (minimum, maximum));
        }

        static void AssertBounds(
            JsonObject descriptor,
            string presetId,
            (int Minimum, int Maximum) expected)
        {
            string id = descriptor["id"]!.GetValue<string>();
            JsonObject expectation = descriptor["presetExpectations"]![presetId]!
                .AsObject();
            Assert.IsFalse(
                expectation.ContainsKey("expectedAnimationStarts"),
                $"{id}/{presetId}");
            Assert.AreEqual(
                expected.Minimum,
                expectation["minimumAnimationStarts"]?.GetValue<int>(),
                $"{id}/{presetId}");
            Assert.AreEqual(
                expected.Maximum,
                expectation["maximumAnimationStarts"]?.GetValue<int>(),
                $"{id}/{presetId}");
        }
    }

    [TestMethod]
    public void MotionExpectationBoundMutationsFailIndependentTraceBinding()
    {
        MutableCatalogueDocuments documents = ReadDocuments();
        Descriptor(documents, "MI-014").Document["presetExpectations"]!["P01"]![
            "minimumAnimationStarts"] = 1;
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));

        documents = ReadDocuments();
        Descriptor(documents, "MI-001").Document["presetExpectations"]!["P01"]![
            "maximumAnimationStarts"] = 1;
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));

        documents = ReadDocuments();
        Descriptor(documents, "MI-004").Document["presetExpectations"]!["P01"]![
            "maximumAnimationStarts"] = 6;
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));

        documents = ReadDocuments();
        Descriptor(documents, "MI-043").Document["presetExpectations"]!["P02"]![
            "maximumAnimationStarts"] = 1;
        AssertRule(
            "coverage.preset-matrix",
            () => ModelInspectionFixtureCoverageValidator.Validate(
                LoadCatalogue(documents)));
    }

    [TestMethod]
    public void AddedSupportedEnumMemberIsDetectedBySourceContract()
    {
        string path = Path.Combine(
            Root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "Presentation",
            "ModelInspectionFigmaState.cs");
        string source = File.ReadAllText(path);
        string mutated = source.Replace(
            "OperationalFailure = 13",
            "OperationalFailure = 13,\n    FutureState = 14",
            StringComparison.Ordinal);

        CollectionAssert.AreNotEqual(
            ParseEnumMembersFromSource(source, "ModelInspectionFigmaState"),
            ParseEnumMembersFromSource(mutated, "ModelInspectionFigmaState"));
    }

    internal static string FindRepositoryRoot()
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

    internal static ModelInspectionFixtureCatalogue LoadCatalogue() =>
        LoadCatalogue(ReadDocuments());

    internal static IReadOnlyList<VerifiedModelInspectionFixtureExternalEvidence>
        VerifyExternalEvidenceJoin()
    {
        N001ExternalEvidencePrerequisitesRemainPresentAndAssertedStatic();
        return
        [
            new("MI-002", "N-001"),
            new("MI-003", "N-001")
        ];
    }

    private static void N001ExternalEvidencePrerequisitesRemainPresentAndAssertedStatic()
    {
        Assert.IsTrue(File.Exists(Path.Combine(
            Root,
            "tests",
            "TestFixtures",
            "GGUF",
            "N-001-vocab-only-spm.gguf")));
        string source = File.ReadAllText(Path.Combine(
            Root,
            "tests",
            "UnitTests",
            "GraniteEdgeAI.UnitTests",
            "Features",
            "ModelInspection",
            "ModelInspectionPageNavigationTests.cs"));
        string body = ExtractMethodBody(
            source,
            "PackagedN001_PageJourneyCompletesAllFiveStagesAsReady");
        string[] ordered =
        [
            "Enum.GetValues<ModelInspectionStage>()",
            "ModelInspectionStageStatus.Active",
            "ModelInspectionStageStatus.Completed",
            "CollectionAssert.AreEqual",
            "ModelInspectionOutcome.Ready",
            "ModelInspectionFigmaState.ReadyExpanded",
            "ModelInspectionFigmaState.ReadyCollapsed"
        ];
        int cursor = -1;
        foreach (string value in ordered)
        {
            cursor = body.IndexOf(value, cursor + 1, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, cursor);
        }
    }

    private static string ScenarioDirectory() => Path.Combine(
        Root,
        "tests",
        "TestFixtures",
        "ModelInspectionScenarios");

    private static MutableCatalogueDocuments ReadDocuments()
    {
        string directory = ScenarioDirectory();
        JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory,
            "model-inspection-fixture.schema.json")))!.AsObject();
        JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json")))!.AsObject();
        var descriptors = new List<(string FileName, JsonObject Document)>();
        foreach (string fileName in ExpectedFixtureFileNames)
        {
            descriptors.Add((
                fileName,
                JsonNode.Parse(File.ReadAllText(Path.Combine(directory, fileName)))!
                    .AsObject()));
        }

        return new(schema, policy, descriptors);
    }

    private static ModelInspectionFixtureCatalogue LoadCatalogue(
        MutableCatalogueDocuments documents)
    {
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(documents.SchemaSource());
        ModelInspectionFixtureCoverageValidator.ValidateSchemaContract(schema);
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(documents.PolicySource(), schema);
        return ModelInspectionFixtureCatalogue.LoadDescriptors(
            documents.DescriptorSources(),
            policy,
            schema);
    }

    internal static ValidatedModelInspectionFixtureCoverageCatalogue
        LoadValidatedCoverageCatalogue() =>
        ModelInspectionFixtureCoverageValidator.Validate(LoadCatalogue());

    private static JsonObject PolicyEntry(
        MutableCatalogueDocuments documents,
        string id) => documents.Policy["fixtures"]!.AsArray()
        .Select(node => node!.AsObject())
        .Single(entry => entry["id"]!.GetValue<string>() == id);

    private static (string FileName, JsonObject Document) Descriptor(
        MutableCatalogueDocuments documents,
        string id) => documents.Descriptors.Single(item =>
            item.Document["id"]!.GetValue<string>() == id);

    private static void ReplaceDescriptor(
        MutableCatalogueDocuments documents,
        string id,
        string fileName,
        JsonObject descriptor)
    {
        int index = documents.Descriptors.FindIndex(item =>
            item.Document["id"]!.GetValue<string>() == id);
        documents.Descriptors[index] = (fileName, descriptor);
    }

    private static JsonObject OperationalFailureEffect(
        MutableCatalogueDocuments documents,
        string id) => Descriptor(documents, id).Document["input"]!["attempts"]!
        .AsArray().SelectMany(attempt => attempt!["serviceSteps"]!.AsArray())
        .Select(step => step!["effect"]!.AsObject())
        .Single(effect => effect["kind"]!.GetValue<string>() ==
            "operationalFailure");

    private static void ReplaceCopyDefaultText(
        JsonNode? node,
        string copyKey,
        string defaultText)
    {
        if (node is JsonObject value &&
            value["copyKey"]?.GetValue<string>() == copyKey &&
            value.ContainsKey("defaultText"))
        {
            value["defaultText"] = defaultText;
        }

        if (node is JsonObject objectValue)
        {
            foreach ((_, JsonNode? child) in objectValue.ToArray())
            {
                ReplaceCopyDefaultText(child, copyKey, defaultText);
            }
        }
        else if (node is JsonArray arrayValue)
        {
            foreach (JsonNode? child in arrayValue)
            {
                ReplaceCopyDefaultText(child, copyKey, defaultText);
            }
        }
    }

    private static void RemoveStringValue(JsonArray values, string expected)
    {
        int index = values
            .Select((node, offset) => (node, offset))
            .Single(item => item.node?.GetValue<string>() == expected)
            .offset;
        values.RemoveAt(index);
    }

    private static void AssertRule(string rule, Action action)
    {
        ModelInspectionFixtureValidationException exception =
            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(action);
        Assert.AreEqual(rule, exception.RuleCode);
    }

    private static int CountCoveredCrossDimensionPairs(
        IReadOnlyList<ModelInspectionFixturePreset> presets)
    {
        HashSet<string> pairs = [];
        foreach (ModelInspectionFixturePreset preset in presets)
        {
            string[] values =
            [
                $"width:{preset.Width}",
                $"resources:{preset.Resources}",
                $"text:{preset.Text}",
                $"motion:{preset.Motion}"
            ];
            for (int left = 0; left < values.Length; left++)
            {
                for (int right = left + 1; right < values.Length; right++)
                {
                    pairs.Add($"{values[left]}|{values[right]}");
                }
            }
        }

        return pairs.Count;
    }

    private static void AssertSetupContains(
        ModelInspectionFixtureCatalogue catalogue,
        string id,
        ModelInspectionFixtureSetupStepKind kind) =>
        Assert.IsTrue(Fixture(catalogue, id).Input.SetupSteps.Any(step =>
            step.Kind == kind), id);

    private static ValidatedModelInspectionFixture Fixture(
        ModelInspectionFixtureCatalogue catalogue,
        string id) => catalogue.Fixtures.Single(fixture => fixture.Id == id);

    private static IEnumerable<ModelInspectionExpectedCopy> AllCopies(
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
        return copies.Where(copy => copy is not null).Select(copy => copy!)
            .Concat(expected.Model.Metadata.SelectMany(field =>
                new[] { field.Label, field.Value }))
            .Concat(expected.Model.Checks.Select(check => check.Text))
            .Concat(expected.Content.Rows.SelectMany(row =>
                new[] { row.PrimaryText, row.SecondaryText }
                    .Where(copy => copy is not null).Select(copy => copy!)))
            .Concat(expected.Actions.Items.SelectMany(action =>
                new[] { action.Label, action.HelpText }
                    .Where(copy => copy is not null).Select(copy => copy!)))
            .Concat(expected.Automation.Controls.SelectMany(control =>
                new[] { control.AccessibleName, control.HelpText }
                    .Where(copy => copy is not null).Select(copy => copy!)))
            .Concat(expected.Announcements.Items);
    }

    private static string[] ParseEnumMembers(string relativePath, string enumName)
    {
        string source = File.ReadAllText(Path.Combine(
            Root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return ParseEnumMembersFromSource(source, enumName);
    }

    private static string[] ParseEnumMembersFromSource(
        string source,
        string enumName)
    {
        Match match = Regex.Match(
            source,
            $@"\benum\s+{Regex.Escape(enumName)}\s*\{{(?<body>[^}}]+)\}}",
            RegexOptions.CultureInvariant);
        Assert.IsTrue(match.Success, $"Missing enum declaration {enumName}.");
        return match.Groups["body"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(member => Regex.Match(member.Trim(), @"^[A-Za-z][A-Za-z0-9]*").Value)
            .Where(member => member.Length > 0)
            .ToArray();
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        int method = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, method);
        int start = source.IndexOf('{', method);
        Assert.IsGreaterThanOrEqualTo(0, start);
        int depth = 0;
        for (int index = start; index < source.Length; index++)
        {
            depth += source[index] == '{' ? 1 : source[index] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source[start..(index + 1)];
            }
        }

        Assert.Fail($"Method {methodName} has no complete body.");
        return string.Empty;
    }

    private static ModelInspectionFixtureCatalogue LoadThroughProgressBatch()
    {
        string directory = ScenarioDirectory();
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                File.ReadAllBytes(Path.Combine(
                    directory,
                    "model-inspection-fixture.schema.json"))));
        JsonObject policyDocument = JsonNode.Parse(File.ReadAllText(Path.Combine(
            directory,
            "model-inspection-fixture-coverage-policy.json")))!.AsObject();
        JsonArray policyFixtures = policyDocument["fixtures"]!.AsArray();
        while (policyFixtures.Count > 28)
        {
            policyFixtures.RemoveAt(policyFixtures.Count - 1);
        }

        policyDocument["gallerySwitchPairs"] = new JsonArray();
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                Encoding.UTF8.GetBytes(policyDocument.ToJsonString())),
                schema);
        ModelInspectionFixtureDocumentSource[] descriptors =
            ExpectedFixtureFileNames.Take(28).Select(fileName =>
                new ModelInspectionFixtureDocumentSource(
                    fileName,
                    File.ReadAllBytes(Path.Combine(directory, fileName))))
                .ToArray();
        return ModelInspectionFixtureCatalogue.LoadDescriptors(
            descriptors,
            policy,
            schema);
    }

    private static IEnumerable<ModelInspectionExpectedRowStatus>
        ExpectedProgressRowStatuses(ProgressBatchExpectation oracle)
    {
        int current = (int)oracle.Stage - 1;
        for (int index = 0; index < 5; index++)
        {
            if (index < current)
            {
                yield return ModelInspectionExpectedRowStatus.Passed;
            }
            else if (index > current)
            {
                yield return ModelInspectionExpectedRowStatus.Waiting;
            }
            else
            {
                yield return oracle.Status switch
                {
                    ModelInspectionFixtureStageStatus.Active =>
                        ModelInspectionExpectedRowStatus.Active,
                    ModelInspectionFixtureStageStatus.Completed =>
                        ModelInspectionExpectedRowStatus.Passed,
                    ModelInspectionFixtureStageStatus.Warning =>
                        ModelInspectionExpectedRowStatus.Warning,
                    ModelInspectionFixtureStageStatus.Failed =>
                        ModelInspectionExpectedRowStatus.Error,
                    ModelInspectionFixtureStageStatus.Cancelled =>
                        ModelInspectionExpectedRowStatus.Information,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }

    private static (string CopyKey, string DefaultText) ProgressDetail(
        ModelInspectionFixtureStage stage) => stage switch
        {
            ModelInspectionFixtureStage.CheckModelPackage =>
                ("fixture.progress.check-model-package.detail",
                 "Checking the model package."),
            ModelInspectionFixtureStage.ReadModelConfiguration =>
                ("fixture.progress.read-configuration.detail",
                 "Reading model configuration."),
            ModelInspectionFixtureStage.ValidateTokenizerAndChatSetup =>
                ("fixture.progress.validate-tokenizer.detail",
                 "Validating tokenizer and chat setup."),
            ModelInspectionFixtureStage.ValidateModelStructure =>
                ("fixture.progress.validate-structure.detail",
                 "Validating model structure."),
            ModelInspectionFixtureStage.ConfirmCoreRuntimeCompatibility =>
                ("fixture.progress.confirm-runtime.detail",
                 "Confirming core runtime compatibility."),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

    private sealed record ProgressBatchExpectation(
        string Id,
        ModelInspectionFixtureStage Stage,
        ModelInspectionFixtureStageStatus Status,
        int CompletedStageCount,
        double? ObservedFraction,
        bool CancelRequested = false,
        ModelInspectionFixtureOutcome? TerminalOutcome = null,
        bool TerminalIsCancellation = false,
        double?[]? ReleasedFractions = null);

    private sealed record MutableCatalogueDocuments(
        JsonObject Schema,
        JsonObject Policy,
        List<(string FileName, JsonObject Document)> Descriptors)
    {
        internal ModelInspectionFixtureDocumentSource SchemaSource() => new(
            "model-inspection-fixture.schema.json",
            Encoding.UTF8.GetBytes(Schema.ToJsonString()));

        internal ModelInspectionFixtureDocumentSource PolicySource() => new(
            "model-inspection-fixture-coverage-policy.json",
            Encoding.UTF8.GetBytes(Policy.ToJsonString()));

        internal IReadOnlyList<ModelInspectionFixtureDocumentSource>
            DescriptorSources() => Descriptors.Select(item =>
                new ModelInspectionFixtureDocumentSource(
                    item.FileName,
                    Encoding.UTF8.GetBytes(item.Document.ToJsonString())))
                .ToArray();
    }
}
