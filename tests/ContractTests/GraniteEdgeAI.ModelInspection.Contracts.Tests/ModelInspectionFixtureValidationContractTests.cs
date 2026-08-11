using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureValidationContractTests
{
    private const string DebugX64Condition =
        "'$(Configuration)|$(Platform)' == 'Debug|x64'";
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
    private const string ViewModelDetailsKey =
        "fixture.automation.model-disclosure.view";
    private const string ViewModelDetails = "View model inspection details";
    private const string HideModelDetailsKey =
        "fixture.automation.model-disclosure.hide";
    private const string HideModelDetails = "Hide model inspection details";
    private const string WarningDetailsKey =
        "fixture.automation.warning-disclosure";
    private const string WarningDetails = "Inspection warning details";
    private const string ConversionDetailsKey =
        "fixture.automation.conversion-disclosure";
    private const string ConversionDetails = "Expected conversion output";
    private const string InvalidDetailsKey =
        "fixture.automation.invalid-disclosure";
    private const string InvalidDetails = "Model validation report";

    [TestMethod]
    public void Filename_RequiresExactIdTargetAndVariant()
    {
        string[] invalidFileNames =
        [
            "mi-001-ready-clean-compatible-model-collapsed.fixture.json",
            "MI-01-ready-clean-compatible-model-collapsed.fixture.json",
            "MI-0001-ready-clean-compatible-model-collapsed.fixture.json",
            "MI-001-Ready-clean-compatible-model-collapsed.fixture.json",
            "MI-001-ready_clean-compatible-model-collapsed.fixture.json",
            "MI-001-ready-clean-compatible-model-collapsed.json",
            "MI-001-ready-clean-compatible-model-collapsed.fixture.json.extra",
            "MI-002-ready-clean-compatible-model-collapsed.fixture.json",
            "MI-001-ready-clean-compatible-model-expanded.fixture.json",
            "MI-001-ready-clean-compatible-model-collapsed-extra.fixture.json"
        ];

        foreach (string fileName in invalidFileNames)
        {
            ModelInspectionFixtureJsonContractTests.AssertInvalid(
                FixtureContractDocuments.ValidDescriptorJson,
                fileName);
        }

        string wrongId = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"id\":\"MI-001\"",
            "\"id\":\"MI-002\"",
            StringComparison.Ordinal);
        string wrongTarget = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"targetCondition\":\"ready-clean-compatible-model\"",
            "\"targetCondition\":\"ready-compatible-model\"",
            StringComparison.Ordinal);
        string wrongVariant = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"variant\":\"collapsed\"",
            "\"variant\":\"expanded\"",
            StringComparison.Ordinal);

        ModelInspectionFixtureJsonContractTests.AssertInvalid(wrongId);
        ModelInspectionFixtureJsonContractTests.AssertInvalid(wrongTarget);
        ModelInspectionFixtureJsonContractTests.AssertInvalid(wrongVariant);
    }

    [TestMethod]
    public void Privacy_RejectsPathsUrisIdentityUnicodeAndUnsafeDisplayFilenames()
    {
        string[] unsafeDisplayNames =
        [
            "C:\\private\\model.gguf",
            "\\rooted.gguf",
            "/rooted.gguf",
            "folder/model.gguf",
            "folder\\model.gguf",
            "https://example.invalid/model.gguf",
            "C:model.gguf",
            "%USERNAME%-model.gguf",
            "${USER}-model.gguf",
            "control\u0001name",
            "bidi\u202Ename",
            "private\uE000name",
            "unassigned\u0378name",
            "Cafe\u0301"
        ];

        foreach (string unsafeValue in unsafeDisplayNames)
        {
            string descriptor = FixtureContractDocuments.MutateDescriptor(
                root => root["input"]!["request"]!["displayName"] = unsafeValue);
            ModelInspectionFixtureValidationException exception =
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    Load(descriptor));

            Assert.IsFalse(exception.Message.Contains(unsafeValue, StringComparison.Ordinal));
            Assert.IsFalse(string.IsNullOrWhiteSpace(exception.RuleCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(exception.JsonPath));
        }

        string oversized = new('a', StrictModelInspectionFixtureJson.MaximumStringLength + 1);
        AssertInvalid(root => root["title"] = oversized);

        string[] unsafeSourceNames =
        [
            "folder/MI-001-ready-clean-compatible-model-collapsed.fixture.json",
            "folder\\MI-001-ready-clean-compatible-model-collapsed.fixture.json",
            "C:\\MI-001-ready-clean-compatible-model-collapsed.fixture.json",
            "https://example.invalid/MI-001.fixture.json"
        ];
        foreach (string unsafeSourceName in unsafeSourceNames)
        {
            ModelInspectionFixtureValidationException exception =
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    Load(FixtureContractDocuments.ValidDescriptorJson, unsafeSourceName));
            Assert.AreEqual("<invalid-filename>", exception.FileName);
            Assert.IsFalse(exception.Message.Contains(unsafeSourceName, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void ModelInspectionFixtureGalleryBuildBoundary_EvaluatesOnboardingOnlyForDebugX64()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] projectPaths =
        [
            Path.Combine(
                repositoryRoot,
                "IBM Granite with TurboQuant (Intel)",
                "IBM Granite with TurboQuant (Intel).csproj"),
            Path.Combine(
                repositoryRoot,
                "tests",
                "UnitTests",
                "GraniteEdgeAI.UnitTests",
                "GraniteEdgeAI.UnitTests.csproj")
        ];

        foreach (string projectPath in projectPaths)
        {
            XDocument project = XDocument.Load(projectPath);
            AssertDebugFixtureBoundary(project, "Features\\ModelInspection\\DebugFixtures");
            AssertDebugFixtureBoundary(project, "Features\\Onboarding\\DebugFixtures");
            AssertSentinelEvaluation(projectPath);
        }
    }

    [TestMethod]
    public void SetupScript_RequiresDeclaredCheckpointsInteractionsAndOneFinalObservation()
    {
        AssertInvalid(root =>
            root["input"]!["setupSteps"]![0]!["checkpoint"] = "missing-checkpoint");
        AssertInvalid(root =>
            root["input"]!["setupSteps"]![0]!["attempt"] = 2);
        AssertInvalid(root =>
        {
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(1, SetupStep("invoke-cancel", null, null, "missing-interaction"));
        });
        AssertInvalid(root =>
        {
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(1, SetupStep("observe", null, "early-observation", null));
        });
        AssertInvalid(root =>
        {
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            JsonNode final = setup[^1]!.DeepClone();
            setup.RemoveAt(setup.Count - 1);
            setup.Insert(0, final);
        });
        AssertInvalid(root =>
            root["input"]!["observationCheckpoint"] = "other-observation");
    }

    [TestMethod]
    public void SetupScript_StaleReleaseRequiresMatchingDeferredCheckpointKind()
    {
        AssertInvalid(root =>
        {
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(1, SetupStep(
                "release-stale-progress",
                1,
                "undeclared-stale",
                null));
        });

        string wrongKind = FixtureContractDocuments.MutateDescriptor(root =>
        {
            AddDeferredStep(root, "deferStaleMotion", "old-motion");
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(1, SetupStep(
                "release-stale-announcement",
                1,
                "old-motion",
                null));
        });
        ModelInspectionFixtureJsonContractTests.AssertInvalid(wrongKind);
    }

    [TestMethod]
    public void SetupReplay_RejectsDuplicateOutOfOrderAndInactiveAttemptReleases()
    {
        AssertInvalid(root =>
        {
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(1, setup[0]!.DeepClone());
        });

        AssertInvalid(root =>
        {
            AddProgressStep(root, "readModelConfiguration", "active", 1, null);
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            JsonNode first = setup[0]!.DeepClone();
            setup.RemoveAt(0);
            setup.Insert(1, first);
        });

        AssertInvalid(root =>
        {
            AddSecondAttempt(root);
            root["input"]!["setupSteps"]!.AsArray().Insert(
                1,
                SetupStep("release-service-checkpoint", 2, "service-ready-2", null));
        });

        AssertInvalid(root =>
        {
            AddSecondAttempt(root);
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(1, SetupStep("invoke-retry", null, null, "retry-attempt"));
            setup.Insert(2, SetupStep("release-service-checkpoint", 1, "service-ready", null));
        });
    }

    [TestMethod]
    public void SetupReplay_StaleReleasesDoNotAdvanceCurrentInteractionScope()
    {
        string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            AddSecondAttempt(root);
            AddDeferredStep(root, "deferStaleProgress", "old-progress");
            root["interactions"]!.AsArray().Add(
                Interaction(
                    "choose-current",
                    "chooseAnother",
                    "service-ready-2",
                    "gallery:no-active-fixture",
                    "noActiveFixture"));
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(
                setup.Count - 1,
                SetupStep("invoke-retry", null, null, "retry-attempt"));
            setup.Insert(
                setup.Count - 1,
                SetupStep("release-service-checkpoint", 2, "service-ready-2", null));
            setup.Insert(
                setup.Count - 1,
                SetupStep("release-stale-progress", 1, "old-progress", null));
            setup.Insert(
                setup.Count - 1,
                SetupStep("invoke-choose-another", null, null, "choose-current"));
        });

        Load(descriptor);

        AssertInvalid(root =>
        {
            AddSecondAttempt(root);
            AddDeferredStep(root, "deferStaleProgress", "old-progress");
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
            setup.Insert(
                setup.Count - 1,
                SetupStep("invoke-retry", null, null, "retry-attempt"));
            JsonObject stale = SetupStep(
                "release-stale-progress",
                1,
                "old-progress",
                null);
            setup.Insert(setup.Count - 1, stale);
            setup.Insert(setup.Count - 1, stale.DeepClone());
        });
    }

    [TestMethod]
    public void SetupReplay_RequiresRetiredOwnershipForEveryStaleReleaseKind()
    {
        (string DeferKind, string ReleaseKind, string Checkpoint)[] cases =
        [
            ("deferStaleProgress", "release-stale-progress", "old-progress"),
            ("deferStaleResultSnapshot", "submit-stale-result-snapshot", "old-result"),
            ("deferStaleMotion", "release-stale-motion", "old-motion"),
            ("deferStaleAnnouncement", "release-stale-announcement", "old-announcement")
        ];

        foreach ((string deferKind, string releaseKind, string checkpoint) in cases)
        {
            AssertInvalid(root =>
            {
                AddDeferredStep(root, deferKind, checkpoint);
                root["input"]!["setupSteps"]!.AsArray().Insert(
                    1,
                    SetupStep(releaseKind, 1, checkpoint, null));
            });

            string afterRetirement = FixtureContractDocuments.MutateDescriptor(root =>
            {
                AddSecondAttempt(root);
                AddDeferredStep(root, deferKind, checkpoint);
                JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("invoke-retry", null, null, "retry-attempt"));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("release-service-checkpoint", 2, "service-ready-2", null));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep(releaseKind, 1, checkpoint, null));
            });
            Load(afterRetirement);

            AssertInvalid(root =>
            {
                AddSecondAttempt(root);
                JsonObject deferred = ServiceStep("capture-current", deferKind);
                deferred["effect"]!["deferredCheckpoint"] = checkpoint;
                root["input"]!["attempts"]![1]!["serviceSteps"]!.AsArray()
                    .Insert(0, deferred);
                JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("invoke-retry", null, null, "retry-attempt"));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("release-service-checkpoint", 2, "capture-current", null));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep(releaseKind, 2, checkpoint, null));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("release-service-checkpoint", 2, "service-ready-2", null));
            });
        }
    }

    [TestMethod]
    public void SetupReplay_CancelAndDisclosureCannotForgeOwnerRetirement()
    {
        (string SetupKind, string InteractionKind, string LifetimeEffect)[] cases =
        [
            ("invoke-cancel", "cancel", "retirePage"),
            ("invoke-disclosure", "expand", "noActiveFixture")
        ];

        foreach ((string setupKind, string interactionKind, string lifetimeEffect) in cases)
        {
            AssertInvalid(root =>
            {
                AddDeferredStep(root, "deferStaleProgress", "old-progress");
                root["interactions"]!.AsArray().Add(
                    Interaction(
                        "forged-retirement",
                        interactionKind,
                        "service-ready",
                        "service-ready",
                        lifetimeEffect));
                JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
                setup.Insert(
                    setup.Count - 1,
                    SetupStep(setupKind, null, null, "forged-retirement"));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("release-stale-progress", 1, "old-progress", null));
            });
        }
    }

    [TestMethod]
    public void ObservationInteractions_RejectUnavailableReadyActions()
    {
        foreach (string kind in new[] { "cancel", "retry", "restart" })
        {
            AssertInvalid(root =>
                root["interactions"] = new JsonArray(
                    Interaction(
                        "unavailable-action",
                        kind,
                        "ready-observed",
                        "ready-observed")));

            AssertInvalid(root =>
            {
                ConfigureExpectedActions(
                    root,
                    "result",
                    (kind, "fixture.action.choose", "Choose another model"));
                root["interactions"] = new JsonArray(
                    Interaction(
                        "wrong-state-action",
                        kind,
                        "ready-observed",
                        "ready-observed"));
            });
        }

        foreach (string kind in new[] { "cancel", "retry", "restart", "chooseAnother" })
        {
            AssertStateValidInteractionInvalid(kind, root =>
                root["expected"]!["actions"]!["visible"] = false);
            AssertStateValidInteractionInvalid(kind, root =>
                root["expected"]!["actions"]!["mode"] = "hidden");
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAction(root, InteractionActionId(kind))["visible"] = false);
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAction(root, InteractionActionId(kind))["enabled"] = false);
        }

        AssertStateValidInteractionInvalid("expand", root =>
            root["expected"]!["model"]!["visible"] = false);
        AssertStateValidInteractionInvalid("expand", root =>
            root["expected"]!["model"]!["disclosureExpanded"] = true);
        AssertStateValidInteractionInvalid("collapse", root =>
            root["expected"]!["model"]!["disclosureExpanded"] = false);

        AssertInvalid(root =>
            root["interactions"] = new JsonArray(
                Interaction(
                    "wrong-direction",
                    "collapse",
                    "ready-observed",
                    "ready-observed")));
        AssertStateValidInteractionInvalid("collapse", root =>
            root["interactions"] = new JsonArray(
                Interaction(
                    "wrong-direction",
                    "expand",
                    "ready-observed",
                    "ready-observed")));

        AssertInvalid(root =>
            root["interactions"] = new JsonArray(
                Interaction(
                    "wrong-reset-target",
                    "reset",
                    "ready-observed",
                    "ready-observed",
                    "retirePage")));
    }

    [TestMethod]
    public void ObservationInteractions_RequireCanonicalAutomationControls()
    {
        foreach (string kind in new[] { "cancel", "retry", "restart", "chooseAnother" })
        {
            string automationId = InteractionActionId(kind);
            AssertStateValidInteractionInvalid(kind, root =>
                RemoveExpectedAutomationControl(root, automationId));
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAutomationControl(root, automationId)["id"] = "wrong-control");
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAutomationControl(root, automationId)["controlType"] = "text");
            AssertStateValidInteractionInvalid(kind, root =>
                root["expected"]!["automation"]!["controls"] = new JsonArray());
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAutomationControl(root, automationId)["id"] =
                    kind == "cancel" ? "retry" : "cancel");
        }

        foreach (string kind in new[] { "expand", "collapse" })
        {
            AssertStateValidInteractionInvalid(kind, root =>
                RemoveExpectedAutomationControl(root, "inspection-details-disclosure"));
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAutomationControl(root, "inspection-details-disclosure")["id"] =
                    "wrong-control");
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAutomationControl(root, "inspection-details-disclosure")["controlType"] =
                    "button");
            AssertStateValidInteractionInvalid(kind, root =>
                FindExpectedAutomationControl(root, "inspection-details-disclosure")["id"] =
                    "findings-disclosure");
        }

        string contentDisclosure = FixtureContractDocuments.MutateDescriptor(root =>
        {
            ConfigureReadyWithWarnings(root, warningOrdinal: 3);
            AddExpectedAutomationControl(
                root,
                "findings-disclosure",
                "group",
                WarningDetailsKey,
                WarningDetails);
            root["interactions"] = new JsonArray(
                Interaction(
                    "expand-findings",
                    "expand",
                    "ready-observed",
                    "ready-observed"));
        });
        string contentPolicy = FixtureContractDocuments.MutatePolicy(ConfigureWarningPolicy);
        LoadMany([FixtureContractDocuments.DescriptorSource(contentDisclosure)], contentPolicy);

        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
                 {
                     root => RemoveExpectedAutomationControl(root, "findings-disclosure"),
                     root => FindExpectedAutomationControl(root, "findings-disclosure")["id"] =
                         "inspection-details-disclosure",
                     root => FindExpectedAutomationControl(root, "findings-disclosure")["controlType"] =
                         "button"
                 })
        {
            string invalid = FixtureContractDocuments.MutateDescriptor(root =>
            {
                ConfigureReadyWithWarnings(root, warningOrdinal: 3);
                AddExpectedAutomationControl(
                    root,
                    "findings-disclosure",
                    "group",
                    WarningDetailsKey,
                    WarningDetails);
                root["interactions"] = new JsonArray(
                    Interaction(
                        "expand-findings",
                        "expand",
                        "ready-observed",
                        "ready-observed"));
                mutation(root);
            });
            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                LoadMany([FixtureContractDocuments.DescriptorSource(invalid)], contentPolicy));
        }
    }

    [TestMethod]
    public void ObservationInteractions_RequireCanonicalAutomationNames()
    {
        (string Kind, string WrongKey, string WrongText)[] actionMutations =
        [
            ("cancel", "fixture.action.retry", "Retry inspection"),
            ("retry", "fixture.action.cancel", "Cancel inspection"),
            ("restart", "fixture.action.cancel", "Cancel inspection"),
            ("chooseAnother", "fixture.action.cancel", "Cancel inspection")
        ];
        foreach ((string kind, string wrongKey, string wrongText) in actionMutations)
        {
            AssertStateValidInteractionInvalid(kind, root =>
                SetExpectedAutomationName(
                    root,
                    InteractionActionId(kind),
                    wrongKey,
                    wrongText));
        }

        AssertStateValidInteractionInvalid("expand", root =>
            SetExpectedAutomationName(
                root,
                "inspection-details-disclosure",
                HideModelDetailsKey,
                HideModelDetails));
        AssertStateValidInteractionInvalid("collapse", root =>
            SetExpectedAutomationName(
                root,
                "inspection-details-disclosure",
                ViewModelDetailsKey,
                ViewModelDetails));
        AssertStateValidInteractionInvalid("expand", root =>
            SetExpectedAutomationName(
                root,
                "inspection-details-disclosure",
                WarningDetailsKey,
                WarningDetails));

        (string State, string Key, string Text, string WrongKey, string WrongText)[] contentContracts =
        [
            ("readyWithWarningsCollapsed", WarningDetailsKey, WarningDetails,
                ConversionDetailsKey, ConversionDetails),
            ("readyWithWarningsExpanded", WarningDetailsKey, WarningDetails,
                ConversionDetailsKey, ConversionDetails),
            ("conversionRequiredCollapsed", ConversionDetailsKey, ConversionDetails,
                InvalidDetailsKey, InvalidDetails),
            ("conversionRequiredExpanded", ConversionDetailsKey, ConversionDetails,
                InvalidDetailsKey, InvalidDetails),
            ("invalidCollapsed", InvalidDetailsKey, InvalidDetails,
                WarningDetailsKey, WarningDetails),
            ("invalidExpanded", InvalidDetailsKey, InvalidDetails,
                WarningDetailsKey, WarningDetails)
        ];
        foreach ((string state, string key, string text, string wrongKey, string wrongText)
                 in contentContracts)
        {
            string policy = FixtureContractDocuments.MutatePolicy(root =>
                ConfigureContentDisclosurePolicy(root, state));
            string valid = FixtureContractDocuments.MutateDescriptor(root =>
                ConfigureContentDisclosureInteraction(root, state, key, text));
            LoadMany([FixtureContractDocuments.DescriptorSource(valid)], policy);

            foreach ((string mismatchKey, string mismatchText) in new[]
                     {
                         (wrongKey, wrongText),
                         (ViewModelDetailsKey, ViewModelDetails)
                     })
            {
                string invalid = FixtureContractDocuments.MutateDescriptor(root =>
                {
                    ConfigureContentDisclosureInteraction(root, state, key, text);
                    SetExpectedAutomationName(
                        root,
                        "findings-disclosure",
                        mismatchKey,
                        mismatchText);
                });
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    LoadMany([FixtureContractDocuments.DescriptorSource(invalid)], policy));
            }
        }
    }

    [TestMethod]
    public void Interactions_RequireClosedKindLifetimeEffectMappings()
    {
        (string Kind, string Target, string AllowedLifetimeEffect)[] mappings =
        [
            ("expand", "ready-observed", "none"),
            ("collapse", "ready-observed", "none"),
            ("cancel", "ready-observed", "none"),
            ("retry", "service-ready", "none"),
            ("restart", "service-ready", "none"),
            ("chooseAnother", "gallery:no-active-fixture", "noActiveFixture"),
            ("reset", "MI-001", "retirePage")
        ];
        string[] lifetimeEffects = ["none", "retirePage", "noActiveFixture"];

        foreach ((string kind, string target, string allowedLifetimeEffect) in mappings)
        {
            string policy = FixtureContractDocuments.MutatePolicy(root =>
                ConfigureStateValidInteractionPolicy(root, kind));
            string valid = FixtureContractDocuments.MutateDescriptor(root =>
            {
                ConfigureStateValidInteractionScreen(root, kind);
                root["interactions"] = new JsonArray(
                    Interaction(
                        "mapped-action",
                        kind,
                        "ready-observed",
                        target,
                        allowedLifetimeEffect));
            });
            LoadMany([FixtureContractDocuments.DescriptorSource(valid)], policy);

            foreach (string invalidLifetimeEffect in lifetimeEffects.Where(
                         effect => !effect.Equals(allowedLifetimeEffect, StringComparison.Ordinal)))
            {
                string invalid = FixtureContractDocuments.MutateDescriptor(root =>
                {
                    ConfigureStateValidInteractionScreen(root, kind);
                    root["interactions"] = new JsonArray(
                        Interaction(
                            "mapped-action",
                            kind,
                            "ready-observed",
                            target,
                            invalidLifetimeEffect));
                });
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    LoadMany([FixtureContractDocuments.DescriptorSource(invalid)], policy));
            }
        }

        foreach ((string setupKind, string interactionKind) in new[]
                 {
                     ("invoke-retry", "retry"),
                     ("invoke-restart", "restart")
                 })
        {
            string transitioned = FixtureContractDocuments.MutateDescriptor(root =>
            {
                AddSecondAttempt(root);
                AddDeferredStep(root, "deferStaleProgress", "old-progress");
                root["interactions"]![0]!["kind"] = interactionKind;
                JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
                setup.Insert(
                    setup.Count - 1,
                    SetupStep(setupKind, null, null, "retry-attempt"));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("release-service-checkpoint", 2, "service-ready-2", null));
                setup.Insert(
                    setup.Count - 1,
                    SetupStep("release-stale-progress", 1, "old-progress", null));
            });

            Load(transitioned);
        }
    }

    [TestMethod]
    public void VisibleInteractions_AreOnlyThoseAtObservationCheckpoint()
    {
        string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            root["interactions"] = new JsonArray(
                Interaction("history", "retry", "service-ready", "service-ready"),
                Interaction(
                    "current",
                    "chooseAnother",
                    "ready-observed",
                    "gallery:no-active-fixture",
                    "noActiveFixture"));
        });

        ValidatedModelInspectionFixture fixture = Load(descriptor).Fixtures.Single();

        Assert.AreEqual(2, fixture.Interactions.Count);
        Assert.AreEqual(1, fixture.VisibleInteractions.Count);
        Assert.AreEqual("current", fixture.VisibleInteractions[0].Id);
    }

    [TestMethod]
    public void Progress_RequiresFiveStagesTruthfulCountAndActiveOnlyFraction()
    {
        Load(FixtureContractDocuments.MutateDescriptor(root =>
            AddProgressStep(root, "readModelConfiguration", "active", 1, 0.5)));

        AssertInvalid(root =>
            root["expected"]!["footer"]!["rows"]!.AsArray().RemoveAt(4));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "active", 2, null));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "completed", 1, null));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "failed", 2, null));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "cancelled", 2, null));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "completed", 2, 0.5));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "active", 1, -0.01));
        AssertInvalid(root =>
            AddProgressStep(root, "readModelConfiguration", "active", 1, 1.01));
    }

    [TestMethod]
    public void ProgressSequence_RejectsFractionStatusAndBlockingStateRegression()
    {
        AssertInvalid(root => AddProgressSequence(
            root,
            Progress("readModelConfiguration", "active", 1, 0.8),
            Progress("readModelConfiguration", "active", 1, 0.2)));
        AssertInvalid(root => AddProgressSequence(
            root,
            Progress("readModelConfiguration", "completed", 2, null),
            Progress("readModelConfiguration", "active", 1, null)));

        foreach (string blockingStatus in new[] { "failed", "cancelled" })
        {
            string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
            {
                AddProgressSequence(
                    root,
                    Progress("readModelConfiguration", blockingStatus, 1, null),
                    Progress("readModelConfiguration", blockingStatus, 1, null));
                if (blockingStatus == "failed")
                {
                    MutateTerminal(root, "invalid", "crossSourceContradiction", null);
                    root["input"]!["request"]!["evidenceProfile"] = "crossSourceContradiction";
                    root["coverage"]!["figmaStates"]![0] = "invalidCollapsed";
                    root["coverage"]!["outcomes"]![0] = "invalid";
                    root["expected"]!["figma"]!["state"] = "invalidCollapsed";
                    root["expected"]!["outcome"]!["kind"] = "invalid";
                }
                else
                {
                    MutateTerminal(root, null, null, null);
                    root["input"]!["attempts"]![0]!["serviceSteps"]![2]!["effect"]!["kind"] =
                        "cancelled";
                    root["coverage"]!["figmaStates"]![0] = "cancelled";
                    root["coverage"]!["outcomes"] = new JsonArray();
                    root["expected"]!["figma"]!["state"] = "cancelled";
                    root["expected"]!["outcome"]!["kind"] = "cancelled";
                }
            });
            string figma = blockingStatus == "failed" ? "invalidCollapsed" : "cancelled";
            string policy = FixtureContractDocuments.MutatePolicy(
                root => root["fixtures"]![0]!["canonicalFigmaState"] = figma);

            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                LoadMany([FixtureContractDocuments.DescriptorSource(descriptor)], policy));
        }

        Load(FixtureContractDocuments.MutateDescriptor(root => AddProgressSequence(
            root,
            Progress("readModelConfiguration", "active", 1, 0.2),
            Progress("readModelConfiguration", "active", 1, 0.8))));
        Load(FixtureContractDocuments.MutateDescriptor(root => AddProgressSequence(
            root,
            Progress("readModelConfiguration", "active", 1, null),
            Progress("readModelConfiguration", "completed", 2, null))));
    }

    [TestMethod]
    public void Attempts_AreMonotonicAndHaveAtMostOneTerminalWithNoLaterProgress()
    {
        AssertInvalid(root =>
        {
            JsonArray attempts = root["input"]!["attempts"]!.AsArray();
            JsonObject duplicateAttempt = attempts[0]!.DeepClone().AsObject();
            attempts.Add(duplicateAttempt);
        });
        AssertInvalid(root =>
        {
            JsonArray steps = root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
            steps.Add(steps[0]!.DeepClone());
        });
        AssertInvalid(root =>
        {
            JsonArray steps = root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
            steps.Add(ServiceStep(
                "after-terminal",
                "progress",
                Progress("confirmCoreRuntimeCompatibility", "active", 4, null)));
        });
    }

    [TestMethod]
    public void TerminalProfiles_EnforceReadyWarningsConversionFailureAndCancellationRules()
    {
        AssertInvalid(root => MutateTerminal(root, "readyWithWarnings", "compatible", null));
        AssertInvalid(root => MutateTerminal(root, "ready", "missingChatTemplate", null));
        AssertInvalid(root => MutateTerminal(root, "conversionRequired", "compatible", null));
        AssertInvalid(root => MutateTerminal(root, "ready", "verifiedIncompatible", null));
        AssertInvalid(root =>
        {
            MutateTerminal(root, null, null, "workerTimeout");
            JsonArray steps = root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
            steps[steps.Count - 1]!["effect"]!["outcome"] = "ready";
        });

        AssertInvalid(root =>
        {
            AddProgressStep(root, "validateTokenizerAndChatSetup", "warning", 3, null);
            MutateTerminal(root, "ready", "compatible", null);
        });
        AssertInvalid(root =>
        {
            AddProgressStep(root, "validateModelStructure", "failed", 3, null);
            MutateTerminal(root, "ready", "compatible", null);
        });
        AssertInvalid(root =>
        {
            AddProgressStep(root, "confirmCoreRuntimeCompatibility", "cancelled", 4, null);
            MutateTerminal(root, "ready", "compatible", null);
        });
    }

    [TestMethod]
    public void ReadyWithWarnings_RequiresExactChatTemplateStageAndApprovedFinding()
    {
        string policy = FixtureContractDocuments.MutatePolicy(ConfigureWarningPolicy);
        string valid = FixtureContractDocuments.MutateDescriptor(root =>
            ConfigureReadyWithWarnings(root, warningOrdinal: 3));
        LoadMany([FixtureContractDocuments.DescriptorSource(valid)], policy);

        AssertReadyWithWarningsInvalid(root => RemoveWarningProgress(root));
        AssertReadyWithWarningsInvalid(_ => { }, warningOrdinal: 1);
        AssertReadyWithWarningsInvalid(root =>
            root["input"]!["attempts"]![0]!["serviceSteps"]![1]!["effect"]!["progress"]!["status"] =
                "warning");
        AssertReadyWithWarningsInvalid(root =>
        {
            root["expected"]!["content"]!["rows"] = new JsonArray();
            root["expected"]!["rowsAndScroll"]!["orderedRowIds"] = new JsonArray();
        });
        AssertReadyWithWarningsInvalid(root =>
            root["expected"]!["content"]!["rows"]!.AsArray().Add(
                root["expected"]!["content"]!["rows"]![0]!.DeepClone()));
        AssertReadyWithWarningsInvalid(root =>
            root["expected"]!["content"]!["rows"]![0]!["id"] = "MI-WARN-ARBITRARY");
        AssertReadyWithWarningsInvalid(root =>
            root["expected"]!["content"]!["rows"]![0]!["status"] = "information");

        string arbitraryCopyDescriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            ConfigureReadyWithWarnings(root, warningOrdinal: 3);
            JsonObject row = root["expected"]!["content"]!["rows"]![0]!.AsObject();
            row["primaryText"] = new JsonObject
            {
                ["copyKey"] = "fixture.warning.arbitrary.title",
                ["defaultText"] = "Arbitrary warning"
            };
            row["secondaryText"] = new JsonObject
            {
                ["copyKey"] = "fixture.warning.arbitrary.detail",
                ["defaultText"] = "Arbitrary detail"
            };
        });
        string arbitraryCopyPolicy = FixtureContractDocuments.MutatePolicy(root =>
        {
            ConfigureWarningPolicy(root);
            root["copyRegistry"]!["fixture.warning.arbitrary.title"] = "Arbitrary warning";
            root["copyRegistry"]!["fixture.warning.arbitrary.detail"] = "Arbitrary detail";
        });
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadMany(
                [FixtureContractDocuments.DescriptorSource(arbitraryCopyDescriptor)],
                arbitraryCopyPolicy));

        string readyWithWarningFinding = FixtureContractDocuments.MutateDescriptor(root =>
            AddApprovedWarningFinding(root));
        string readyPolicyWithWarningCopy = FixtureContractDocuments.MutatePolicy(root =>
        {
            JsonObject registry = root["copyRegistry"]!.AsObject();
            registry[MissingChatTemplateTitleKey] = MissingChatTemplateTitle;
            registry[MissingChatTemplateDetailKey] = MissingChatTemplateDetail;
        });
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadMany(
                [FixtureContractDocuments.DescriptorSource(readyWithWarningFinding)],
                readyPolicyWithWarningCopy));
    }

    [TestMethod]
    public void TerminalScreens_JoinEveryReleasedTerminalAndAllowAtMostOneActiveFooterRow()
    {
        (string Effect, string? Outcome, string? Evidence, string? Failure, string Figma)[] terminals =
        [
            ("completed", "ready", "compatible", null, "readyCollapsed"),
            ("completed", "readyWithWarnings", "missingChatTemplate", null, "readyWithWarningsCollapsed"),
            ("completed", "conversionRequired", "verifiedIncompatible", null, "conversionRequiredCollapsed"),
            ("completed", "incompletePackage", "missingPackageMember", null, "incompletePackage"),
            ("completed", "unsupported", "unsupportedArchitecture", null, "unsupported"),
            ("completed", "invalid", "crossSourceContradiction", null, "invalidCollapsed"),
            ("cancelled", null, null, null, "cancelled"),
            ("operationalFailure", null, null, "workerTimeout", "operationalFailure")
        ];

        foreach ((string effect, string? outcome, string? evidence, string? failure, string figma) in terminals)
        {
            string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
            {
                JsonObject terminal = root["input"]!["attempts"]![0]!["serviceSteps"]![0]!["effect"]!
                    .AsObject();
                terminal["kind"] = effect;
                terminal["outcome"] = outcome;
                terminal["evidenceProfile"] = evidence;
                terminal["failureProfile"] = failure;
                root["input"]!["request"]!["evidenceProfile"] = evidence ?? "compatible";
                root["coverage"]!["figmaStates"]![0] = figma;
                root["coverage"]!["outcomes"] = outcome is null
                    ? new JsonArray()
                    : new JsonArray(outcome);
                root["expected"]!["figma"]!["state"] = figma;
                root["expected"]!["outcome"]!["kind"] = "hidden";
            });
            string policy = FixtureContractDocuments.MutatePolicy(
                root => root["fixtures"]![0]!["canonicalFigmaState"] = figma);

            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                LoadMany([FixtureContractDocuments.DescriptorSource(descriptor)], policy));
        }

        AssertInvalid(root =>
        {
            root["expected"]!["footer"]!["rows"]![0]!["status"] = "inProgress";
            root["expected"]!["footer"]!["rows"]![1]!["status"] = "inProgress";
        });
    }

    [TestMethod]
    public void CurrentProgress_RejectsTerminalExpectedSemanticsAndNoEffectTerminalClaims()
    {
        string progressWithReadyOutcome = FixtureContractDocuments.MutateDescriptor(root =>
        {
            AddProgressStep(root, "readModelConfiguration", "active", 1, 0.5);
            root["input"]!["setupSteps"]!.AsArray().RemoveAt(1);
            root["coverage"]!["figmaStates"]![0] = "inspectionProgress";
            root["expected"]!["figma"]!["state"] = "inspectionProgress";
            root["expected"]!["footer"]!["status"] = "inProgress";
            root["expected"]!["footer"]!["rows"]![0]!["status"] = "complete";
            root["expected"]!["footer"]!["rows"]![1]!["status"] = "inProgress";
            root["expected"]!["footer"]!["rows"]![2]!["status"] = "notComplete";
            root["expected"]!["footer"]!["rows"]![3]!["status"] = "notComplete";
            root["expected"]!["footer"]!["rows"]![4]!["status"] = "notComplete";
        });
        string progressPolicy = FixtureContractDocuments.MutatePolicy(
            root => root["fixtures"]![0]!["canonicalFigmaState"] = "inspectionProgress");
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadMany(
                [FixtureContractDocuments.DescriptorSource(progressWithReadyOutcome)],
                progressPolicy));

        AssertInvalid(root =>
        {
            root["input"]!["attempts"] = new JsonArray();
            root["input"]!["setupSteps"]!.AsArray().RemoveAt(0);
        });
        AssertInvalid(root => root["expected"]!["footer"]!["status"] = "inProgress");
        AssertInvalid(root => root["expected"]!["footer"]!["rows"]![0]!["status"] = "inProgress");

        string initialWaiting = FixtureContractDocuments.MutateDescriptor(root =>
        {
            root["input"]!["attempts"] = new JsonArray();
            root["input"]!["setupSteps"]!.AsArray().RemoveAt(0);
            root["coverage"]!["figmaStates"]![0] = "inspectionProgress";
            root["coverage"]!["outcomes"] = new JsonArray();
            root["expected"]!["figma"]!["state"] = "inspectionProgress";
            root["expected"]!["outcome"]!["visible"] = false;
            root["expected"]!["outcome"]!["kind"] = "hidden";
            root["expected"]!["footer"]!["status"] = "inProgress";
            foreach (JsonNode? row in root["expected"]!["footer"]!["rows"]!.AsArray())
            {
                row!["status"] = "notComplete";
            }
        });
        LoadMany(
            [FixtureContractDocuments.DescriptorSource(initialWaiting)],
            progressPolicy);
    }

    [TestMethod]
    public void NullCollectionElements_AlwaysProduceBrandedValidationFailures()
    {
        Action<JsonObject>[] descriptorMutations =
        [
            root => root["input"]!["attempts"]!.AsArray().Add(null),
            root => root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray().Add(null),
            root => root["input"]!["setupSteps"]!.AsArray().Insert(0, null),
            root => root["expected"]!["model"]!["metadata"]!.AsArray().Add(null),
            root => root["expected"]!["model"]!["checks"]!.AsArray().Add(null),
            root => root["expected"]!["content"]!["rows"]!.AsArray().Add(null),
            root => root["expected"]!["actions"]!["items"]!.AsArray().Add(null),
            root => root["expected"]!["footer"]!["rows"]!.AsArray().Add(null),
            root => root["expected"]!["automation"]!["controls"]!.AsArray().Add(null),
            root => root["expected"]!["announcements"]!["items"]!.AsArray().Add(null),
            root => root["presetExpectations"]!["P01"]!["textRoles"]!.AsArray().Add(null),
            root => root["interactions"]!.AsArray().Add(null),
            root => root["presets"]!.AsArray().Add(null)
        ];

        foreach (Action<JsonObject> mutation in descriptorMutations)
        {
            AssertInvalid(mutation);
        }

        Action<JsonObject>[] policyMutations =
        [
            root => root["fixtures"]!.AsArray().Add(null),
            root => root["presets"]!.AsArray().Add(null),
            root => root["disclosurePairs"]!.AsArray().Add(null),
            root => root["gallerySwitchPairs"]!.AsArray().Add(null),
            root => root["externalEvidenceLinks"]!.AsArray().Add(null),
            root => root["copyRegistry"]!.AsObject()["fixture.null"] = null
        ];

        foreach (Action<JsonObject> mutation in policyMutations)
        {
            VerifiedModelInspectionFixtureSchema schema =
                ModelInspectionFixtureCatalogue.VerifySchema(
                    FixtureContractDocuments.SchemaSource());
            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                ModelInspectionFixtureCatalogue.LoadPolicy(
                    FixtureContractDocuments.PolicySource(
                        FixtureContractDocuments.MutatePolicy(mutation)),
                    schema));
        }
    }

    [TestMethod]
    public void Privacy_RejectsEmbeddedEnvironmentIdentityAndScopesInternalUrisToTransitions()
    {
        string[] environmentIdentities =
            new[] { Environment.UserName, Environment.MachineName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string identity in environmentIdentities)
        {
            AssertInvalid(root => root["title"] = $"Synthetic {identity} fixture");
        }

        foreach (string internalUri in new[]
                 {
                     "gallery:no-active-fixture",
                     "fixture:MI-001",
                     "step:service-ready"
                 })
        {
            AssertInvalid(root => root["title"] = internalUri);
        }

        string identityFileName = "username-" + FixtureContractDocuments.ValidFileName;
        ModelInspectionFixtureValidationException exception =
            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                Load(FixtureContractDocuments.ValidDescriptorJson, identityFileName));
        Assert.AreEqual("<invalid-filename>", exception.FileName);
        Assert.IsFalse(exception.Message.Contains(identityFileName, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Privacy_RejectsEmbeddedUrisAndEverySlashShapedTextValue()
    {
        string[] unsafeValues =
        [
            " https://example.invalid",
            "prefix https://example.invalid",
            "prefix mailto:user@example.invalid",
            "prefix file:C:/secret",
            "folder//file",
            "folder/"
        ];

        foreach (string unsafeValue in unsafeValues)
        {
            ModelInspectionFixtureValidationException exception =
                Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                    Load(FixtureContractDocuments.MutateDescriptor(
                        root => root["title"] = unsafeValue)));
            Assert.IsFalse(
                exception.Message.Contains(unsafeValue, StringComparison.Ordinal));
        }

        Load(FixtureContractDocuments.MutateDescriptor(root =>
        {
            root["interactions"] = new JsonArray(
                Interaction(
                    "choose-current",
                    "chooseAnother",
                    "ready-observed",
                    "gallery:no-active-fixture",
                    "noActiveFixture"));
        }));

        LoadPolicyWithEvidencePath("release-evidence/model-inspection/fixture-proof.md");
        foreach (string unsafeSourcePath in new[]
                 {
                     "release-evidence//fixture-proof.md",
                     "release-evidence/model-inspection/"
                 })
        {
            Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
                LoadPolicyWithEvidencePath(unsafeSourcePath));
        }

        static ValidatedModelInspectionFixtureCoveragePolicy LoadPolicyWithEvidencePath(
            string sourcePath)
        {
            string policy = FixtureContractDocuments.MutatePolicy(root =>
                root["externalEvidenceLinks"]!.AsArray().Add(new JsonObject
                {
                    ["fixtureId"] = "MI-001",
                    ["evidenceId"] = "fixture-proof",
                    ["sourcePath"] = sourcePath,
                    ["journeyTest"] = "fixture-boundary-journey"
                }));
            VerifiedModelInspectionFixtureSchema schema =
                ModelInspectionFixtureCatalogue.VerifySchema(
                    FixtureContractDocuments.SchemaSource());
            return ModelInspectionFixtureCatalogue.LoadPolicy(
                FixtureContractDocuments.PolicySource(policy),
                schema);
        }
    }

    [TestMethod]
    public void PolicyAndDescriptor_RequireClosedPresetAndExactExpectationSets()
    {
        AssertInvalid(root => root["presets"]![0] = "P99");
        AssertInvalid(root => root["presetExpectations"]!.AsObject().Remove("P01"));
        AssertInvalid(root =>
            root["presetExpectations"]!.AsObject()["P99"] =
                root["presetExpectations"]!["P01"]!.DeepClone());
    }

    [TestMethod]
    public void Catalogue_DuplicateTargetRequiresExplicitPairedVariants()
    {
        string secondDescriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            root["id"] = "MI-002";
            root["variant"] = "expanded";
            root["coverage"]!["figmaStates"]![0] = "readyExpanded";
            root["expected"]!["figma"]!["state"] = "readyExpanded";
            root["expected"]!["model"]!["disclosureExpanded"] = true;
        });
        const string secondFileName =
            "MI-002-ready-clean-compatible-model-expanded.fixture.json";

        string unpairedPolicy = FixtureContractDocuments.MutatePolicy(root =>
        {
            JsonObject secondEntry = root["fixtures"]![0]!.DeepClone().AsObject();
            secondEntry["id"] = "MI-002";
            secondEntry["fileName"] = secondFileName;
            secondEntry["variant"] = "expanded";
            secondEntry["canonicalFigmaState"] = "readyExpanded";
            root["fixtures"]!.AsArray().Add(secondEntry);
        });

        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadMany(
                [
                    FixtureContractDocuments.DescriptorSource(),
                    FixtureContractDocuments.DescriptorSource(secondDescriptor, secondFileName)
                ],
                unpairedPolicy));

        string pairedPolicy = FixtureContractDocuments.MutatePolicy(root =>
        {
            JsonObject firstEntry = root["fixtures"]![0]!.AsObject();
            firstEntry["pairedWithId"] = "MI-002";
            JsonObject secondEntry = firstEntry.DeepClone().AsObject();
            secondEntry["id"] = "MI-002";
            secondEntry["fileName"] = secondFileName;
            secondEntry["variant"] = "expanded";
            secondEntry["pairedWithId"] = "MI-001";
            secondEntry["canonicalFigmaState"] = "readyExpanded";
            root["fixtures"]!.AsArray().Add(secondEntry);
            root["disclosurePairs"]!.AsArray().Add(new JsonObject
            {
                ["collapsedId"] = "MI-001",
                ["expandedId"] = "MI-002"
            });
        });

        Assert.AreEqual(
            2,
            LoadMany(
                [
                    FixtureContractDocuments.DescriptorSource(),
                    FixtureContractDocuments.DescriptorSource(secondDescriptor, secondFileName)
                ],
                pairedPolicy).Fixtures.Count);
    }

    private static void AssertDebugFixtureBoundary(XDocument project, string subtree)
    {
        string remove = subtree + "\\**";
        foreach (string itemType in new[]
                 {
                     "Compile", "Page", "None", "Content", "EmbeddedResource", "PRIResource"
                 })
        {
            Assert.AreEqual(
                1,
                project.Descendants(itemType).Count(item => string.Equals(
                    item.Attribute("Remove")?.Value,
                    remove,
                    StringComparison.Ordinal) &&
                    item.Attribute("Condition") is null &&
                    item.Parent?.Attribute("Condition") is null),
                $"{itemType} must unconditionally remove {remove} exactly once.");
        }

        XElement compile = project.Descendants("Compile").Single(item => string.Equals(
            item.Attribute("Include")?.Value,
            subtree + "\\**\\*.cs",
            StringComparison.Ordinal));
        XElement page = project.Descendants("Page").Single(item => string.Equals(
            item.Attribute("Include")?.Value,
            subtree + "\\**\\*.xaml",
            StringComparison.Ordinal));
        Assert.AreEqual(DebugX64Condition, compile.Parent?.Attribute("Condition")?.Value);
        Assert.AreEqual(DebugX64Condition, page.Parent?.Attribute("Condition")?.Value);
        Assert.AreEqual("MSBuild:Compile", page.Element("Generator")?.Value);
    }

    private static void AssertSentinelEvaluation(string projectPath)
    {
        const string csharpSentinel = "OnboardingShellPage.FixtureBoundarySentinel.cs";
        const string xamlSentinel = "OnboardingShellPage.FixtureBoundarySentinel.xaml";
        string projectDirectory = Path.GetDirectoryName(projectPath)!;
        string sentinelDirectory = Path.Combine(
            projectDirectory,
            "Features",
            "Onboarding",
            "DebugFixtures");
        string csharpPath = Path.Combine(sentinelDirectory, csharpSentinel);
        string xamlPath = Path.Combine(sentinelDirectory, xamlSentinel);
        Assert.IsFalse(File.Exists(csharpPath), $"Unexpected sentinel collision: {csharpPath}");
        Assert.IsFalse(File.Exists(xamlPath), $"Unexpected sentinel collision: {xamlPath}");
        try
        {
            Directory.CreateDirectory(sentinelDirectory);
            File.WriteAllText(
                csharpPath,
                "internal sealed class OnboardingFixtureSentinel { }");
            File.WriteAllText(
                xamlPath,
                "<Page xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" />");

            foreach (string configuration in new[] { "Debug", "Release" })
            {
                foreach (string platform in new[] { "x86", "x64", "ARM64" })
                {
                    JsonObject items = EvaluateProjectItems(
                        projectPath,
                        configuration,
                        platform);
                    bool intended = configuration == "Debug" && platform == "x64";
                    Assert.AreEqual(
                        intended ? 1 : 0,
                        CountItem(items, "Compile", csharpSentinel),
                        $"Compile ownership for {configuration}|{platform} in {projectPath}");
                    Assert.AreEqual(
                        intended ? 1 : 0,
                        CountItem(items, "Page", xamlSentinel),
                        $"Page ownership for {configuration}|{platform} in {projectPath}");

                    foreach (string itemType in new[]
                             {
                                 "Page", "None", "Content", "EmbeddedResource", "PRIResource"
                             })
                    {
                        Assert.AreEqual(
                            0,
                            CountItem(items, itemType, csharpSentinel),
                            $"C# sentinel leaked to {itemType} for {configuration}|{platform}.");
                    }

                    foreach (string itemType in new[]
                             {
                                 "Compile", "None", "Content", "EmbeddedResource", "PRIResource"
                             })
                    {
                        Assert.AreEqual(
                            0,
                            CountItem(items, itemType, xamlSentinel),
                            $"XAML sentinel leaked to {itemType} for {configuration}|{platform}.");
                    }
                }
            }
        }
        finally
        {
            File.Delete(csharpPath);
            File.Delete(xamlPath);
            if (Directory.Exists(sentinelDirectory) &&
                !Directory.EnumerateFileSystemEntries(sentinelDirectory).Any())
            {
                Directory.Delete(sentinelDirectory);
            }
        }
    }

    private static JsonObject EvaluateProjectItems(
        string projectPath,
        string configuration,
        string platform)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(projectPath)!
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add($"-p:Configuration={configuration}");
        startInfo.ArgumentList.Add($"-p:Platform={platform}");
        startInfo.ArgumentList.Add(
            "-getItem:Compile,Page,None,Content,EmbeddedResource,PRIResource");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("MSBuild fixture-boundary evaluation timed out.");
        }

        Task.WaitAll(outputTask, errorTask);
        string output = outputTask.Result;
        string error = errorTask.Result;
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"MSBuild fixture-boundary evaluation failed.{Environment.NewLine}{output}{error}");
        int jsonStart = output.IndexOf('{');
        int jsonEnd = output.LastIndexOf('}');
        Assert.IsTrue(jsonStart >= 0 && jsonEnd >= jsonStart, output + error);
        return JsonNode.Parse(output[jsonStart..(jsonEnd + 1)])!["Items"]!.AsObject();
    }

    private static int CountItem(JsonObject items, string itemType, string fileName) =>
        items[itemType]!.AsArray().Count(item => string.Equals(
            Path.GetFileName(item!["Identity"]!.GetValue<string>()),
            fileName,
            StringComparison.Ordinal));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static void AssertInvalid(Action<JsonObject> mutation) =>
        ModelInspectionFixtureJsonContractTests.AssertInvalid(
            FixtureContractDocuments.MutateDescriptor(mutation));

    private static ModelInspectionFixtureCatalogue Load(
        string descriptor,
        string fileName = FixtureContractDocuments.ValidFileName) =>
        LoadMany(
            [FixtureContractDocuments.DescriptorSource(descriptor, fileName)],
            FixtureContractDocuments.PolicyJson);

    private static ModelInspectionFixtureCatalogue LoadMany(
        IReadOnlyList<ModelInspectionFixtureDocumentSource> descriptors,
        string policyJson)
    {
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(
                FixtureContractDocuments.SchemaSource());
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(
                FixtureContractDocuments.PolicySource(policyJson),
                schema);
        return ModelInspectionFixtureCatalogue.LoadDescriptors(
            descriptors,
            policy,
            schema);
    }

    private static JsonObject SetupStep(
        string kind,
        int? attempt,
        string? checkpoint,
        string? interactionId) =>
        new()
        {
            ["kind"] = kind,
            ["attempt"] = attempt,
            ["checkpoint"] = checkpoint,
            ["interactionId"] = interactionId
        };

    private static JsonObject Interaction(
        string id,
        string kind,
        string sourceCheckpoint,
        string target,
        string lifetimeEffect = "none") =>
        new()
        {
            ["id"] = id,
            ["kind"] = kind,
            ["sourceCheckpoint"] = sourceCheckpoint,
            ["target"] = target,
            ["expectedFocus"] = null,
            ["expectedAnnouncementCount"] = 0,
            ["expectedFooterStatus"] = "complete",
            ["lifetimeEffect"] = lifetimeEffect
        };

    private static void AssertStateValidInteractionInvalid(
        string kind,
        Action<JsonObject> mutation)
    {
        string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            ConfigureStateValidInteractionScreen(root, kind);
            root["interactions"] = new JsonArray(
                Interaction(
                    "available-action",
                    kind,
                    "ready-observed",
                    kind == "chooseAnother"
                        ? "gallery:no-active-fixture"
                        : kind == "reset" ? "MI-001" : "ready-observed",
                    kind == "chooseAnother"
                        ? "noActiveFixture"
                        : kind == "reset" ? "retirePage" : "none"));
            mutation(root);
        });
        string policy = FixtureContractDocuments.MutatePolicy(root =>
            ConfigureStateValidInteractionPolicy(root, kind));

        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadMany([FixtureContractDocuments.DescriptorSource(descriptor)], policy));
    }

    private static void ConfigureStateValidInteractionScreen(JsonObject root, string kind)
    {
        switch (kind)
        {
            case "expand":
                AddExpectedAutomationControl(
                    root,
                    "inspection-details-disclosure",
                    "group",
                    ViewModelDetailsKey,
                    ViewModelDetails);
                return;

            case "chooseAnother":
            case "reset":
                return;

            case "collapse":
                root["coverage"]!["figmaStates"]![0] = "readyExpanded";
                root["expected"]!["figma"]!["state"] = "readyExpanded";
                root["expected"]!["model"]!["mode"] = "detailed";
                root["expected"]!["model"]!["disclosureExpanded"] = true;
                AddExpectedAutomationControl(
                    root,
                    "inspection-details-disclosure",
                    "group",
                    HideModelDetailsKey,
                    HideModelDetails);
                return;

            case "cancel":
                AddProgressStep(root, "readModelConfiguration", "active", 1, 0.5);
                root["input"]!["setupSteps"]!.AsArray().RemoveAt(1);
                root["coverage"]!["figmaStates"]![0] = "inspectionProgress";
                root["coverage"]!["outcomes"] = new JsonArray();
                root["expected"]!["figma"]!["state"] = "inspectionProgress";
                root["expected"]!["outcome"]!["visible"] = false;
                root["expected"]!["outcome"]!["kind"] = "hidden";
                root["expected"]!["content"]!["mode"] = "progress";
                root["expected"]!["footer"]!["status"] = "inProgress";
                root["expected"]!["footer"]!["rows"]![0]!["status"] = "complete";
                root["expected"]!["footer"]!["rows"]![1]!["status"] = "inProgress";
                for (int index = 2; index < 5; index++)
                {
                    root["expected"]!["footer"]!["rows"]![index]!["status"] = "notComplete";
                }

                ConfigureExpectedActions(
                    root,
                    "inspecting",
                    ("cancel", "fixture.action.cancel", "Cancel inspection"));
                return;

            case "retry":
                ConfigureTerminalRecoveryScreen(
                    root,
                    "operationalFailure",
                    "operationalFailure",
                    "workerTimeout",
                    "interrupted");
                ConfigureExpectedActions(
                    root,
                    "result",
                    ("choose-another", "fixture.action.choose", "Choose another model"),
                    ("retry", "fixture.action.retry", "Retry inspection"));
                return;

            case "restart":
                ConfigureTerminalRecoveryScreen(
                    root,
                    "cancelled",
                    "cancelled",
                    failureProfile: null,
                    "notComplete");
                ConfigureExpectedActions(
                    root,
                    "result",
                    ("choose-another", "fixture.action.choose", "Choose another model"),
                    ("restart", "fixture.action.restart", "Restart inspection"));
                return;

            default:
                Assert.Fail($"Unknown interaction kind: {kind}");
                return;
        }
    }

    private static void ConfigureTerminalRecoveryScreen(
        JsonObject root,
        string effectKind,
        string figmaState,
        string? failureProfile,
        string footerStatus)
    {
        JsonObject effect = root["input"]!["attempts"]![0]!["serviceSteps"]![0]!["effect"]!
            .AsObject();
        effect["kind"] = effectKind;
        effect["progress"] = null;
        effect["outcome"] = null;
        effect["evidenceProfile"] = null;
        effect["failureProfile"] = failureProfile;
        effect["deferredCheckpoint"] = null;
        root["coverage"]!["figmaStates"]![0] = figmaState;
        root["coverage"]!["outcomes"] = new JsonArray();
        root["coverage"]!["failureProfiles"] = failureProfile is null
            ? new JsonArray()
            : new JsonArray(failureProfile);
        root["expected"]!["figma"]!["state"] = figmaState;
        root["expected"]!["outcome"]!["kind"] = figmaState;
        root["expected"]!["outcome"]!["tone"] = figmaState == "cancelled" ? "neutral" : "error";
        root["expected"]!["content"]!["mode"] = figmaState;
        root["expected"]!["footer"]!["status"] = footerStatus;
        for (int index = 0; index < 5; index++)
        {
            root["expected"]!["footer"]!["rows"]![index]!["status"] = "notComplete";
        }
    }

    private static void ConfigureExpectedActions(
        JsonObject root,
        string mode,
        params (string Id, string CopyKey, string Text)[] specifications)
    {
        JsonArray items = new();
        JsonArray controls = new();
        JsonArray tabOrder = new();
        foreach ((string id, string copyKey, string text) in specifications)
        {
            items.Add(new JsonObject
            {
                ["id"] = id,
                ["label"] = new JsonObject
                {
                    ["copyKey"] = copyKey,
                    ["defaultText"] = text
                },
                ["visible"] = true,
                ["enabled"] = true,
                ["helpText"] = null
            });
            controls.Add(new JsonObject
            {
                ["id"] = id,
                ["accessibleName"] = new JsonObject
                {
                    ["copyKey"] = copyKey,
                    ["defaultText"] = text
                },
                ["controlType"] = "button",
                ["liveSetting"] = "off",
                ["helpText"] = null
            });
            tabOrder.Add(id);
        }

        root["expected"]!["actions"]!["visible"] = true;
        root["expected"]!["actions"]!["mode"] = mode;
        root["expected"]!["actions"]!["items"] = items;
        root["expected"]!["automation"]!["controls"] = controls;
        root["expected"]!["focus"]!["target"] = specifications[^1].Id;
        root["presetExpectations"]!["P01"]!["tabOrder"] = tabOrder.DeepClone();
        root["presetExpectations"]!["P01"]!["focusTarget"] = specifications[^1].Id;
    }

    private static JsonObject FindExpectedAction(JsonObject root, string id) =>
        root["expected"]!["actions"]!["items"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => string.Equals(item["id"]!.GetValue<string>(), id, StringComparison.Ordinal));

    private static JsonObject FindExpectedAutomationControl(JsonObject root, string id) =>
        root["expected"]!["automation"]!["controls"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => string.Equals(item["id"]!.GetValue<string>(), id, StringComparison.Ordinal));

    private static void RemoveExpectedAutomationControl(JsonObject root, string id)
    {
        JsonArray controls = root["expected"]!["automation"]!["controls"]!.AsArray();
        controls.Remove(FindExpectedAutomationControl(root, id));
    }

    private static void SetExpectedAutomationName(
        JsonObject root,
        string id,
        string copyKey,
        string text) =>
        FindExpectedAutomationControl(root, id)["accessibleName"] = new JsonObject
        {
            ["copyKey"] = copyKey,
            ["defaultText"] = text
        };

    private static void AddExpectedAutomationControl(
        JsonObject root,
        string id,
        string controlType,
        string copyKey,
        string text) =>
        root["expected"]!["automation"]!["controls"]!.AsArray().Add(new JsonObject
        {
            ["id"] = id,
            ["accessibleName"] = new JsonObject
            {
                ["copyKey"] = copyKey,
                ["defaultText"] = text
            },
            ["controlType"] = controlType,
            ["liveSetting"] = "off",
            ["helpText"] = null
        });

    private static string InteractionActionId(string kind) => kind switch
    {
        "cancel" => "cancel",
        "retry" => "retry",
        "restart" => "restart",
        "chooseAnother" => "choose-another",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static void ConfigureStateValidInteractionPolicy(JsonObject root, string kind)
    {
        string figmaState = kind switch
        {
            "collapse" => "readyExpanded",
            "cancel" => "inspectionProgress",
            "retry" => "operationalFailure",
            "restart" => "cancelled",
            _ => "readyCollapsed"
        };
        root["fixtures"]![0]!["canonicalFigmaState"] = figmaState;
        JsonObject registry = root["copyRegistry"]!.AsObject();
        registry["fixture.action.cancel"] = "Cancel inspection";
        registry["fixture.action.retry"] = "Retry inspection";
        registry["fixture.action.restart"] = "Restart inspection";
        AddAutomationCopyRegistry(registry);
    }

    private static void ConfigureContentDisclosureInteraction(
        JsonObject root,
        string state,
        string copyKey,
        string text)
    {
        switch (state)
        {
            case "readyWithWarningsCollapsed":
            case "readyWithWarningsExpanded":
                ConfigureReadyWithWarnings(root, warningOrdinal: 3);
                break;
            case "conversionRequiredCollapsed":
            case "conversionRequiredExpanded":
                ConfigureTerminalDisclosureScreen(
                    root,
                    state,
                    "conversionRequired",
                    "verifiedIncompatible",
                    "information",
                    "conversionRequired");
                break;
            case "invalidCollapsed":
            case "invalidExpanded":
                ConfigureTerminalDisclosureScreen(
                    root,
                    state,
                    "invalid",
                    "crossSourceContradiction",
                    "error",
                    "invalid");
                break;
            default:
                Assert.Fail($"Unknown disclosure state: {state}");
                return;
        }

        bool expanded = state.EndsWith("Expanded", StringComparison.Ordinal);
        root["coverage"]!["figmaStates"]![0] = state;
        root["expected"]!["figma"]!["state"] = state;
        root["expected"]!["model"]!["mode"] = expanded ? "detailed" : "compact";
        root["expected"]!["model"]!["disclosureExpanded"] = expanded;

        AddExpectedAutomationControl(
            root,
            "findings-disclosure",
            "group",
            copyKey,
            text);
        root["interactions"] = new JsonArray(
            Interaction(
                expanded ? "collapse-findings" : "expand-findings",
                expanded ? "collapse" : "expand",
                "ready-observed",
                "ready-observed"));
    }

    private static void ConfigureTerminalDisclosureScreen(
        JsonObject root,
        string figmaState,
        string outcome,
        string evidenceProfile,
        string tone,
        string contentMode)
    {
        root["input"]!["request"]!["evidenceProfile"] = evidenceProfile;
        MutateTerminal(root, outcome, evidenceProfile, null);
        root["coverage"]!["figmaStates"]![0] = figmaState;
        root["coverage"]!["outcomes"]![0] = outcome;
        root["expected"]!["figma"]!["state"] = figmaState;
        root["expected"]!["outcome"]!["kind"] = outcome;
        root["expected"]!["outcome"]!["tone"] = tone;
        root["expected"]!["content"]!["mode"] = contentMode;
    }

    private static void ConfigureContentDisclosurePolicy(JsonObject root, string state)
    {
        root["fixtures"]![0]!["canonicalFigmaState"] = state;
        JsonObject registry = root["copyRegistry"]!.AsObject();
        registry[MissingChatTemplateTitleKey] = MissingChatTemplateTitle;
        registry[MissingChatTemplateDetailKey] = MissingChatTemplateDetail;
        AddAutomationCopyRegistry(registry);
    }

    private static void AddAutomationCopyRegistry(JsonObject registry)
    {
        registry[ViewModelDetailsKey] = ViewModelDetails;
        registry[HideModelDetailsKey] = HideModelDetails;
        registry[WarningDetailsKey] = WarningDetails;
        registry[ConversionDetailsKey] = ConversionDetails;
        registry[InvalidDetailsKey] = InvalidDetails;
    }

    private static JsonObject Progress(
        string stage,
        string status,
        int completedStageCount,
        double? fraction) =>
        new()
        {
            ["stage"] = stage,
            ["status"] = status,
            ["completedStageCount"] = completedStageCount,
            ["fraction"] = fraction,
            ["detailProfile"] = "default"
        };

    private static JsonObject ServiceStep(
        string checkpoint,
        string effectKind,
        JsonObject? progress = null) =>
        new()
        {
            ["trigger"] = new JsonObject
            {
                ["kind"] = "checkpoint",
                ["checkpoint"] = checkpoint
            },
            ["effect"] = new JsonObject
            {
                ["kind"] = effectKind,
                ["progress"] = progress,
                ["outcome"] = null,
                ["evidenceProfile"] = null,
                ["failureProfile"] = null,
                ["deferredCheckpoint"] = null
            }
        };

    private static void AddProgressStep(
        JsonObject root,
        string stage,
        string status,
        int completedStageCount,
        double? fraction)
    {
        JsonArray serviceSteps =
            root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
        serviceSteps.Insert(
            0,
            ServiceStep(
                "progress-update",
                "progress",
                Progress(stage, status, completedStageCount, fraction)));
        root["input"]!["setupSteps"]!.AsArray().Insert(
            0,
            SetupStep("release-service-checkpoint", 1, "progress-update", null));
    }

    private static void AddProgressSequence(
        JsonObject root,
        JsonObject first,
        JsonObject second)
    {
        JsonArray serviceSteps =
            root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
        serviceSteps.Insert(0, ServiceStep("progress-second", "progress", second));
        serviceSteps.Insert(0, ServiceStep("progress-first", "progress", first));
        JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
        setup.Insert(
            0,
            SetupStep("release-service-checkpoint", 1, "progress-second", null));
        setup.Insert(
            0,
            SetupStep("release-service-checkpoint", 1, "progress-first", null));
    }

    private static void ConfigureReadyWithWarnings(JsonObject root, int warningOrdinal)
    {
        string[] stages =
        [
            "checkModelPackage",
            "readModelConfiguration",
            "validateTokenizerAndChatSetup",
            "validateModelStructure",
            "confirmCoreRuntimeCompatibility"
        ];
        JsonArray serviceSteps =
            root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
        JsonArray setupSteps = root["input"]!["setupSteps"]!.AsArray();
        int insertionIndex = 0;
        for (int ordinal = warningOrdinal; ordinal <= stages.Length; ordinal++)
        {
            string checkpoint = $"warning-path-{ordinal}";
            string status = ordinal == warningOrdinal ? "warning" : "completed";
            serviceSteps.Insert(
                insertionIndex,
                ServiceStep(
                    checkpoint,
                    "progress",
                    Progress(stages[ordinal - 1], status, ordinal, null)));
            setupSteps.Insert(
                insertionIndex,
                SetupStep("release-service-checkpoint", 1, checkpoint, null));
            insertionIndex++;
        }

        root["input"]!["request"]!["evidenceProfile"] = "missingChatTemplate";
        MutateTerminal(root, "readyWithWarnings", "missingChatTemplate", null);
        root["coverage"]!["figmaStates"]![0] = "readyWithWarningsCollapsed";
        root["coverage"]!["outcomes"]![0] = "readyWithWarnings";
        root["expected"]!["figma"]!["state"] = "readyWithWarningsCollapsed";
        root["expected"]!["outcome"]!["kind"] = "readyWithWarnings";
        root["expected"]!["outcome"]!["tone"] = "warning";
        root["expected"]!["content"]!["mode"] = "warnings";
        AddApprovedWarningFinding(root);
    }

    private static void ConfigureWarningPolicy(JsonObject root)
    {
        root["fixtures"]![0]!["canonicalFigmaState"] = "readyWithWarningsCollapsed";
        JsonObject registry = root["copyRegistry"]!.AsObject();
        registry[MissingChatTemplateTitleKey] = MissingChatTemplateTitle;
        registry[MissingChatTemplateDetailKey] = MissingChatTemplateDetail;
        AddAutomationCopyRegistry(registry);
    }

    private static void AddApprovedWarningFinding(JsonObject root)
    {
        root["expected"]!["content"]!["rows"] = new JsonArray(new JsonObject
        {
            ["id"] = MissingChatTemplateFindingId,
            ["primaryText"] = new JsonObject
            {
                ["copyKey"] = MissingChatTemplateTitleKey,
                ["defaultText"] = MissingChatTemplateTitle
            },
            ["secondaryText"] = new JsonObject
            {
                ["copyKey"] = MissingChatTemplateDetailKey,
                ["defaultText"] = MissingChatTemplateDetail
            },
            ["status"] = "warning"
        });
        root["expected"]!["rowsAndScroll"]!["orderedRowIds"] =
            new JsonArray(MissingChatTemplateFindingId);
    }

    private static void RemoveWarningProgress(JsonObject root)
    {
        JsonArray serviceSteps =
            root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
        for (int index = serviceSteps.Count - 1; index >= 0; index--)
        {
            if (string.Equals(
                    serviceSteps[index]!["effect"]!["kind"]!.GetValue<string>(),
                    "progress",
                    StringComparison.Ordinal))
            {
                serviceSteps.RemoveAt(index);
            }
        }

        JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
        for (int index = setup.Count - 1; index >= 0; index--)
        {
            string? checkpoint = setup[index]!["checkpoint"]?.GetValue<string>();
            if (checkpoint?.StartsWith("warning-path-", StringComparison.Ordinal) == true)
            {
                setup.RemoveAt(index);
            }
        }
    }

    private static void AssertReadyWithWarningsInvalid(
        Action<JsonObject> mutation,
        int warningOrdinal = 3)
    {
        string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            ConfigureReadyWithWarnings(root, warningOrdinal);
            mutation(root);
        });
        string policy = FixtureContractDocuments.MutatePolicy(ConfigureWarningPolicy);
        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            LoadMany([FixtureContractDocuments.DescriptorSource(descriptor)], policy));
    }

    private static void AddDeferredStep(
        JsonObject root,
        string effectKind,
        string deferredCheckpoint)
    {
        JsonObject step = ServiceStep("capture-stale", effectKind);
        step["effect"]!["deferredCheckpoint"] = deferredCheckpoint;
        root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray().Insert(0, step);
        root["input"]!["setupSteps"]!.AsArray().Insert(
            0,
            SetupStep("release-service-checkpoint", 1, "capture-stale", null));
    }

    private static void AddSecondAttempt(JsonObject root)
    {
        JsonObject secondAttempt = root["input"]!["attempts"]![0]!.DeepClone().AsObject();
        secondAttempt["attempt"] = 2;
        secondAttempt["serviceSteps"]![0]!["trigger"]!["checkpoint"] = "service-ready-2";
        root["input"]!["attempts"]!.AsArray().Add(secondAttempt);
        root["interactions"] = new JsonArray(
            Interaction("retry-attempt", "retry", "service-ready", "service-ready-2"));
    }

    private static void MutateTerminal(
        JsonObject root,
        string? outcome,
        string? evidenceProfile,
        string? failureProfile)
    {
        JsonArray steps = root["input"]!["attempts"]![0]!["serviceSteps"]!.AsArray();
        JsonObject effect = steps[steps.Count - 1]!["effect"]!.AsObject();
        effect["outcome"] = outcome;
        effect["evidenceProfile"] = evidenceProfile;
        effect["failureProfile"] = failureProfile;
        effect["kind"] = failureProfile is null ? "completed" : "operationalFailure";
    }
}
