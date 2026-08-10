using System.Text.Json.Nodes;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureValidationContractTests
{
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
            AddDeferredStep(root, "deferStaleProgress", "old-progress");
            root["interactions"] = new JsonArray(
                Interaction(
                    "choose-current",
                    "chooseAnother",
                    "service-ready",
                    "gallery:no-active-fixture"));
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
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
            AddDeferredStep(root, "deferStaleProgress", "old-progress");
            JsonArray setup = root["input"]!["setupSteps"]!.AsArray();
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
    public void VisibleInteractions_AreOnlyThoseAtObservationCheckpoint()
    {
        string descriptor = FixtureContractDocuments.MutateDescriptor(root =>
        {
            root["interactions"] = new JsonArray(
                Interaction("history", "retry", "service-ready", "service-ready"),
                Interaction("current", "chooseAnother", "ready-observed", "gallery:no-active-fixture"));
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
        string target) =>
        new()
        {
            ["id"] = id,
            ["kind"] = kind,
            ["sourceCheckpoint"] = sourceCheckpoint,
            ["target"] = target,
            ["expectedFocus"] = null,
            ["expectedAnnouncementCount"] = 0,
            ["expectedFooterStatus"] = "complete",
            ["lifetimeEffect"] = "none"
        };

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
