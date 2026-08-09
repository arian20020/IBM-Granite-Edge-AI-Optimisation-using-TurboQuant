using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
[TestCategory("WinUI")]
public sealed class ModelInspectionFigmaStatePresentationTests
{
    public static IEnumerable<object[]> UnsafeIntegrationTextCases
    {
        get
        {
            yield return
            [
                "path",
                @"C:\Users\PRIVATE_PATH_SENTINEL\model.gguf",
                "PRIVATE_PATH_SENTINEL"
            ];
            yield return
            [
                "control",
                "CONTROL_SENTINEL\nhidden",
                "CONTROL_SENTINEL"
            ];
            yield return
            [
                "bidi",
                "BIDI_SENTINEL\u202Ehidden",
                "BIDI_SENTINEL"
            ];
            yield return
            [
                "oversize",
                "OVERSIZE_SENTINEL" + new string('x', 600),
                "OVERSIZE_SENTINEL"
            ];
            yield return
            [
                "private-use",
                "PRIVATE_USE_SENTINEL\uE000hidden",
                "PRIVATE_USE_SENTINEL"
            ];
        }
    }

    [TestMethod]
    [DataRow(1, InspectionOutcomePresentationKind.Hidden, InspectionOutcomeTone.Neutral, InspectionModelBadgeState.ModelSelected, InspectionContentCardMode.Progress, InspectionFooterStatus.InProgress, false, "")]
    [DataRow(2, InspectionOutcomePresentationKind.Ready, InspectionOutcomeTone.Success, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Hidden, InspectionFooterStatus.Complete, true, "Model inspection complete.")]
    [DataRow(3, InspectionOutcomePresentationKind.Ready, InspectionOutcomeTone.Success, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Hidden, InspectionFooterStatus.Complete, true, "Model inspection complete.")]
    [DataRow(4, InspectionOutcomePresentationKind.ReadyWithWarnings, InspectionOutcomeTone.Warning, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Warnings, InspectionFooterStatus.Complete, true, "Model inspected with warnings.")]
    [DataRow(5, InspectionOutcomePresentationKind.ReadyWithWarnings, InspectionOutcomeTone.Warning, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Warnings, InspectionFooterStatus.Complete, true, "Model inspected with warnings.")]
    [DataRow(6, InspectionOutcomePresentationKind.ConversionRequired, InspectionOutcomeTone.Information, InspectionModelBadgeState.SourceModel, InspectionContentCardMode.ConversionRequired, InspectionFooterStatus.NotComplete, true, "Model conversion is required.")]
    [DataRow(7, InspectionOutcomePresentationKind.ConversionRequired, InspectionOutcomeTone.Information, InspectionModelBadgeState.SourceModel, InspectionContentCardMode.ConversionRequired, InspectionFooterStatus.NotComplete, true, "Model conversion is required.")]
    [DataRow(8, InspectionOutcomePresentationKind.IncompletePackage, InspectionOutcomeTone.Warning, InspectionModelBadgeState.Incomplete, InspectionContentCardMode.IncompletePackage, InspectionFooterStatus.NotComplete, false, "Model package is incomplete.")]
    [DataRow(9, InspectionOutcomePresentationKind.Unsupported, InspectionOutcomeTone.Error, InspectionModelBadgeState.Unsupported, InspectionContentCardMode.Unsupported, InspectionFooterStatus.NotComplete, false, "Model is not supported.")]
    [DataRow(10, InspectionOutcomePresentationKind.Invalid, InspectionOutcomeTone.Error, InspectionModelBadgeState.Invalid, InspectionContentCardMode.Invalid, InspectionFooterStatus.NotComplete, true, "Model is invalid.")]
    [DataRow(11, InspectionOutcomePresentationKind.Invalid, InspectionOutcomeTone.Error, InspectionModelBadgeState.Invalid, InspectionContentCardMode.Invalid, InspectionFooterStatus.NotComplete, true, "Model is invalid.")]
    [DataRow(12, InspectionOutcomePresentationKind.Cancelled, InspectionOutcomeTone.Neutral, InspectionModelBadgeState.NotInspected, InspectionContentCardMode.Cancelled, InspectionFooterStatus.NotComplete, false, "Model inspection was cancelled.")]
    [DataRow(13, InspectionOutcomePresentationKind.OperationalFailure, InspectionOutcomeTone.Error, InspectionModelBadgeState.ResultUnknown, InspectionContentCardMode.OperationalFailure, InspectionFooterStatus.Interrupted, false, "Model inspection could not be completed.")]
    public void Create_MapsAllThirteenApprovedStates(
        int stateValue,
        InspectionOutcomePresentationKind expectedKind,
        InspectionOutcomeTone expectedTone,
        InspectionModelBadgeState expectedBadge,
        InspectionContentCardMode expectedContentMode,
        InspectionFooterStatus expectedFooter,
        bool expectedDisclosure,
        string expectedOutcomeAnnouncement)
    {
        ModelInspectionFigmaState expectedState = (ModelInspectionFigmaState)stateValue;
        ModelInspectionPagePresentation presentation = CreateForState(expectedState);

        Assert.AreEqual(stateValue, (int)presentation.State);
        Assert.AreEqual(expectedKind, presentation.OutcomeCard.Kind);
        Assert.AreEqual(expectedTone, presentation.OutcomeCard.Tone);
        Assert.AreEqual(expectedBadge, presentation.ModelCard.BadgeState);
        Assert.AreEqual(expectedContentMode, presentation.ContentCard.Mode);
        Assert.AreEqual(expectedFooter, presentation.FooterStatus);
        Assert.AreEqual(expectedDisclosure, HasDisclosure(presentation));
        Assert.AreEqual(expectedOutcomeAnnouncement, presentation.OutcomeAnnouncement);
        AssertActionPolicy(presentation);
    }

    [TestMethod]
    [DataRow(1, "Cancel inspection", "Cancel", null, "Hidden", null, "Hidden", null, "Hidden")]
    [DataRow(2, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Check hardware fit", "Future")]
    [DataRow(3, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Check hardware fit", "Future")]
    [DataRow(4, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Continue to hardware check", "Future")]
    [DataRow(5, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Continue to hardware check", "Future")]
    [DataRow(6, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Choose conversion format", "Future")]
    [DataRow(7, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Choose conversion format", "Future")]
    [DataRow(8, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Locate missing file", "Choose")]
    [DataRow(9, null, "Hidden", "View technical report", "Future", null, "Hidden", "Choose another model", "Choose")]
    [DataRow(10, null, "Hidden", "View technical report", "Future", null, "Hidden", "Choose another model", "Choose")]
    [DataRow(11, null, "Hidden", "View technical report", "Future", null, "Hidden", "Choose another model", "Choose")]
    [DataRow(12, null, "Hidden", "Choose another model", "Choose", null, "Hidden", "Restart inspection", "Retry")]
    [DataRow(13, null, "Hidden", "Choose another model", "Choose", "View technical report", "Future", "Retry inspection", "Retry")]
    public void Create_MapsExactActionSlotsLabelsCommandsAndHelp(
        int stateValue,
        string? cancelLabel,
        string cancelRole,
        string? secondaryOneLabel,
        string secondaryOneRole,
        string? secondaryTwoLabel,
        string secondaryTwoRole,
        string? primaryLabel,
        string primaryRole)
    {
        ModelInspectionFigmaState state = (ModelInspectionFigmaState)stateValue;
        ModelInspectionPresentationCommands commands = CreateCommands();
        bool expanded = IsExpandedState(state);
        ModelInspectionPagePresentation presentation = Create(
            CreateSnapshotForState(state),
            commands: commands,
            isDisclosureExpanded: expanded);

        AssertActionSlot(
            presentation.ActionCard.CancelAction,
            cancelLabel,
            cancelRole,
            commands,
            expectedEnabled: false);
        AssertActionSlot(
            presentation.ActionCard.SecondaryActionOne,
            secondaryOneLabel,
            secondaryOneRole,
            commands,
            expectedEnabled: true);
        AssertActionSlot(
            presentation.ActionCard.SecondaryActionTwo,
            secondaryTwoLabel,
            secondaryTwoRole,
            commands,
            expectedEnabled: true);
        AssertActionSlot(
            presentation.ActionCard.PrimaryAction,
            primaryLabel,
            primaryRole,
            commands,
            expectedEnabled: true);
    }

    [TestMethod]
    [DataRow(2, 3, "Model")]
    [DataRow(4, 5, "Content")]
    [DataRow(6, 7, "Content")]
    [DataRow(10, 11, "Content")]
    public void Create_CollapsedExpandedPairsChangeOnlyTheirDisclosureRegion(
        int collapsedValue,
        int expandedValue,
        string changedRegion)
    {
        ModelInspectionPagePresentation collapsed =
            CreateForState((ModelInspectionFigmaState)collapsedValue);
        ModelInspectionPagePresentation expanded =
            CreateForState((ModelInspectionFigmaState)expandedValue);

        Assert.AreNotEqual(collapsed.State, expanded.State);
        Assert.AreEqual(collapsed.RegionKeys.Outcome, expanded.RegionKeys.Outcome);
        Assert.AreEqual(
            changedRegion != "Model",
            collapsed.RegionKeys.Model == expanded.RegionKeys.Model);
        Assert.AreEqual(
            changedRegion != "Content",
            collapsed.RegionKeys.Content == expanded.RegionKeys.Content);
        Assert.AreEqual(collapsed.RegionKeys.Actions, expanded.RegionKeys.Actions);
        Assert.AreEqual(collapsed.RegionKeys.Footer, expanded.RegionKeys.Footer);
        Assert.AreEqual(collapsed.RegionKeys.Progress, expanded.RegionKeys.Progress);
        Assert.AreEqual(
            collapsed.RegionKeys.Announcements,
            expanded.RegionKeys.Announcements);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(12)]
    [DataRow(13)]
    public void Create_RejectsExpansionForStatesWithoutApprovedPair(int stateValue)
    {
        ModelInspectionFigmaState state = (ModelInspectionFigmaState)stateValue;

        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateForState(state, forceExpanded: true));
    }

    [TestMethod]
    public void Constructors_RejectUndefinedEnumsNullsAndUnsafeRegionValues()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ModelInspectionPresentationCommands(
                null!,
                PresentationTestData.CreateCommand(),
                PresentationTestData.CreateCommand()));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRegionKey(" "));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRegionKey("unsafe\nkey"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionRegionKey(new string('x', 257)));

        ModelInspectionPagePresentation valid = CreateForState(
            ModelInspectionFigmaState.ReadyCollapsed);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionPagePresentation(
                valid.RenderKey,
                (ModelInspectionFigmaState)99,
                valid.ModelCard,
                valid.ContentCard,
                valid.OutcomeCard,
                valid.ActionCard,
                valid.FooterStatus,
                valid.RegionKeys,
                valid.ProgressAnnouncement,
                valid.OutcomeAnnouncement,
                valid.ProgressRowsUpdate));
    }

    [TestMethod]
    [DoNotParallelize]
    [DataRow("en-US")]
    [DataRow("hu-HU")]
    public void Create_ReadyUsesCultureAwareOverviewAndFiveOrderedChecks(
        string cultureName)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            ModelInspectionResult result = PresentationTestData.CreateResult(
                ModelInspectionOutcome.Ready,
                duration: TimeSpan.FromSeconds(2.5));
            ModelInspectionPagePresentation presentation = Create(
                TerminalSnapshot(ModelInspectionExecutionResult.Completed(result)),
                isDisclosureExpanded: true,
                request: PresentationTestData.CreateRequest(
                    parameterSizeLabel: null));
            InspectionModelCardPresentation model = presentation.ModelCard;

            Assert.AreEqual(InspectionModelCardMode.Detailed, model.DisplayMode);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Not reported",
                    "GGUF",
                    "Q4_K_M",
                    $"{3_000_000_000UL.ToString("N0", culture)} parameters",
                    "Not reported",
                    $"{4_096UL.ToString("N0", culture)} tokens",
                    $"{4m.ToString("N0", culture)} KB",
                    "All 5 inspection checks passed"
                },
                new[]
                {
                    model.Publisher,
                    model.FormatName,
                    model.Quantisation,
                    model.ParameterCount,
                    model.ModelType,
                    model.DeclaredContext,
                    model.FileSize,
                    model.InspectionChecksSummary
                });
            CollectionAssert.AreEqual(
                new[]
                {
                    "Model package",
                    "Model configuration",
                    "Tokenizer and chat setup",
                    "Model structure",
                    "Core runtime support"
                },
                model.InspectionChecks.Select(check => check.Title).ToArray());
            Assert.AreEqual(5, model.InspectionChecks.Count);
            Assert.IsTrue(model.InspectionChecks.All(
                check => check.Status == InspectionCheckStatus.Passed &&
                    check.StatusText == "Passed"));
            StringAssert.Contains(
                model.InspectionChecks[4].Detail,
                $"in {2.5.ToString("0.###", culture)} seconds.",
                StringComparison.Ordinal);
            Assert.IsTrue(model.IsInspectionDetailsExpanded);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        Assert.AreEqual(originalCulture, CultureInfo.CurrentCulture);
        Assert.AreEqual(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    [TestMethod]
    public void Create_UnsupportedFindingCodeFailsClosedWithoutEchoingInput()
    {
        const string UnsupportedCode = "MI-PRIVATE-REQUEST-ID-123";
        ModelInspectionResult result = PresentationTestData.CreateResult(
            ModelInspectionOutcome.ReadyWithWarnings,
            technicalDetail: "private stdout stderr user raw-template digest",
            warningCode: UnsupportedCode);
        ModelInspectionViewSnapshot snapshot = TerminalSnapshot(
            ModelInspectionExecutionResult.Completed(result));

        ModelInspectionPagePresentation presentation = Create(snapshot);
        string allText = FlattenText(presentation);

        Assert.AreEqual(ModelInspectionFigmaState.OperationalFailure, presentation.State);
        Assert.AreEqual(
            "MI-OP-PRESENTATION-UNSUPPORTED-EVIDENCE",
            presentation.ContentCard.DiagnosticCode);
        StringAssert.DoesNotContain(allText, UnsupportedCode, StringComparison.Ordinal);
        StringAssert.DoesNotContain(allText, "private stdout", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow((int)ModelInspectionOutcome.ConversionRequired, 6)]
    [DataRow((int)ModelInspectionOutcome.IncompletePackage, 8)]
    [DataRow((int)ModelInspectionOutcome.Unsupported, 9)]
    [DataRow((int)ModelInspectionOutcome.Invalid, 10)]
    public void Create_ApprovedSyntheticNoFindingUsesGenericRowAndNotReportedDiagnostic(
        int outcomeValue,
        int expectedStateValue)
    {
        ModelInspectionResult result = PresentationTestData.CreateResult(
            (ModelInspectionOutcome)outcomeValue);
        ModelInspectionPagePresentation presentation = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(result)));

        Assert.AreEqual(
            (ModelInspectionFigmaState)expectedStateValue,
            presentation.State);
        Assert.AreEqual(1, presentation.ContentCard.Items.Count);
        Assert.AreEqual("Not reported", presentation.ContentCard.DiagnosticCode);
        Assert.AreEqual(
            Visibility.Visible,
            presentation.ContentCard.DiagnosticCodeVisibility);
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            presentation.ContentCard.Items[0].Detail));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void Create_ReadyWithWarningsRequiresExactlyOneApprovedFinding(
        int warningCount)
    {
        ModelInspectionResult result = PresentationTestData.CreateResult(
            ModelInspectionOutcome.ReadyWithWarnings,
            warningCount: warningCount);

        ModelInspectionPagePresentation presentation = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(result)));

        Assert.AreEqual(
            ModelInspectionFigmaState.OperationalFailure,
            presentation.State);
        Assert.AreEqual(
            "MI-OP-PRESENTATION-UNSUPPORTED-EVIDENCE",
            presentation.ContentCard.DiagnosticCode);
    }

    [TestMethod]
    [DataRow("worker-id")]
    [DataRow("protocol-version")]
    [DataRow("runtime-profile")]
    [DataRow("llamasharp-version")]
    [DataRow("backend-version")]
    [DataRow("llama-commit")]
    [DataRow("native-library")]
    [DataRow("process-architecture")]
    [DataRow("inspection-mode")]
    [DataRow("tokenizer-smoke-failed")]
    [DataRow("tokenizer-smoke-unknown")]
    [DataRow("ready-template-absent")]
    [DataRow("warning-template-present")]
    [DataRow("cuda-enabled")]
    [DataRow("vulkan-enabled")]
    [DataRow("gpu-layer-enabled")]
    public void Create_ReadyEnvelopeContradictionFailsClosed(string mutation)
    {
        ModelInspectionResult result = CreateEnvelopeContradiction(mutation);

        ModelInspectionPagePresentation presentation = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(result)));

        Assert.AreEqual(
            ModelInspectionFigmaState.OperationalFailure,
            presentation.State,
            mutation);
        Assert.AreEqual(
            "MI-OP-PRESENTATION-UNSUPPORTED-EVIDENCE",
            presentation.ContentCard.DiagnosticCode,
            mutation);
    }

    [TestMethod]
    public void Create_DoesNotExposePrivateEvidenceOrTechnicalSentinels()
    {
        const string Sentinels =
            "RAW-TEMPLATE EXCEPTION-CHAIN REQUEST-ID DIGEST STDOUT STDERR USERNAME";
        ModelInspectionPagePresentation warning = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(
                    ModelInspectionOutcome.ReadyWithWarnings,
                    technicalDetail: Sentinels))));
        ModelInspectionPagePresentation failure = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.OperationalFailure(
                PresentationTestData.CreateFailure(Sentinels))));

        string allText = FlattenText(warning) + FlattenText(failure);
        StringAssert.DoesNotContain(
            allText,
            PresentationTestData.PrivateDirectoryMarker,
            StringComparison.Ordinal);
        foreach (string sentinel in Sentinels.Split(' '))
        {
            StringAssert.DoesNotContain(allText, sentinel, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    [DynamicData(nameof(UnsafeIntegrationTextCases))]
    public void Create_UnsafeOptionalSurfacesUseSafeFallbackWithoutEcho(
        string caseName,
        string unsafeText,
        string sentinel)
    {
        ModelInspectionRequest unsafeRequest = PresentationTestData.CreateRequest(
            modelName: unsafeText,
            architecture: unsafeText,
            parameterSizeLabel: unsafeText,
            quantisation: unsafeText);
        var unsafeTokenIds = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [unsafeText] = 1
        };
        string evidenceFileName = caseName == "path"
            ? "granite.gguf"
            : $"{unsafeText}.gguf";
        ModelInspectionEvidence unsafeEvidence = PresentationTestData.CreateEvidence(
            fileName: evidenceFileName,
            configurationModelName: unsafeText,
            configurationArchitecture: unsafeText,
            workerVersion: unsafeText,
            tokenizerModel: unsafeText,
            vocabularyType: unsafeText,
            knownSpecialTokenIds: unsafeTokenIds);
        ModelInspectionPagePresentation ready = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(
                    ModelInspectionOutcome.Ready,
                    evidence: unsafeEvidence))),
            request: unsafeRequest);
        ModelInspectionPagePresentation warning = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(
                    ModelInspectionOutcome.ReadyWithWarnings,
                    technicalDetail: unsafeText,
                    evidence: PresentationTestData.CreateEvidence(
                        chatTemplatePresent: false),
                    findingTitle: unsafeText,
                    findingExplanation: unsafeText,
                    findingRecommendedAction: unsafeText))));
        ModelInspectionPagePresentation runtime = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(
                    ModelInspectionOutcome.Ready,
                    evidence: PresentationTestData.CreateEvidence(
                        runtimeProfile: unsafeText)))));
        ModelInspectionPagePresentation failure = Create(
            TerminalSnapshot(ModelInspectionExecutionResult.OperationalFailure(
                PresentationTestData.CreateFailure(
                    technicalDetail: unsafeText,
                    code: unsafeText,
                    userMessage: unsafeText))));
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completed: 1,
            unsafeText);
        InspectionProgressRows progressRows = CreateProgressRows(attempt: 3);
        ModelInspectionPagePresentation progressPage = Create(
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(3, 1),
                isRunActive: true,
                isCancellationRequested: false,
                progress,
                terminalResult: null),
            progressRows,
            request: unsafeRequest);
        progressRows.Apply(progressPage.ProgressRowsUpdate);

        string allOutput = string.Join(
            "\n",
            new[] { ready, warning, runtime, failure, progressPage }
                .SelectMany(presentation => new[]
                {
                    FlattenText(presentation),
                    FlattenRegionKeys(presentation)
                }));
        Assert.IsFalse(
            allOutput.Contains(sentinel, StringComparison.Ordinal),
            caseName);
        Assert.AreEqual(
            "Inspection progress updated.",
            progressPage.ProgressAnnouncement,
            caseName);
        Assert.AreEqual(
            "2",
            progressPage.ContentCard.Items[1].StageNumber,
            caseName);
        Assert.AreEqual(
            "Read model configuration",
            progressPage.ContentCard.Items[1].Title,
            caseName);
        Assert.AreEqual(
            "Inspection progress updated.",
            progressPage.ContentCard.Items[1].Detail,
            caseName);
        Assert.AreEqual(
            "Checking",
            progressPage.ContentCard.Items[1].StatusText,
            caseName);
        Assert.AreEqual(
            "Read model configuration. Checking. Inspection progress updated.",
            progressPage.ContentCard.Items[1].AutomationName,
            caseName);
    }

    [TestMethod]
    public void RegionKeys_UseSemanticEqualityInsteadOfObjectIdentity()
    {
        ModelInspectionViewSnapshot snapshot = TerminalSnapshot(
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)));

        ModelInspectionPagePresentation first = Create(snapshot);
        ModelInspectionPagePresentation second = Create(
            snapshot,
            commands: CreateCommands());

        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.ModelCard, second.ModelCard);
        Assert.AreEqual(first.RegionKeys, second.RegionKeys);
    }

    [TestMethod]
    public void RegionKeys_SameOutcomeNewAttemptChangedFailureChangesVisibleRegions()
    {
        ModelInspectionPagePresentation first = Create(
            TerminalSnapshot(
                ModelInspectionExecutionResult.OperationalFailure(
                    PresentationTestData.CreateFailure(
                        code: "MI-OP-FIRST",
                        userMessage: "First safe failure message.")),
                attempt: 4));
        ModelInspectionPagePresentation second = Create(
            TerminalSnapshot(
                ModelInspectionExecutionResult.OperationalFailure(
                    PresentationTestData.CreateFailure(
                        code: "MI-OP-SECOND",
                        userMessage: "Second safe failure message.")),
                attempt: 5));

        Assert.AreNotEqual(first.OutcomeCard.Message, second.OutcomeCard.Message);
        Assert.AreNotEqual(
            first.ContentCard.DiagnosticCode,
            second.ContentCard.DiagnosticCode);
        Assert.AreNotEqual(first.RegionKeys.Outcome, second.RegionKeys.Outcome);
        Assert.AreNotEqual(first.RegionKeys.Content, second.RegionKeys.Content);
        Assert.AreEqual(first.RegionKeys.Model, second.RegionKeys.Model);
        Assert.AreEqual(first.RegionKeys.Actions, second.RegionKeys.Actions);
        Assert.AreEqual(first.RegionKeys.Footer, second.RegionKeys.Footer);
        Assert.AreEqual(first.RegionKeys.Progress, second.RegionKeys.Progress);
        Assert.AreNotEqual(
            first.RegionKeys.Announcements,
            second.RegionKeys.Announcements);
    }

    [TestMethod]
    public void RegionKeys_SameOutcomeNewAttemptChangedReadyDetailsChangesModelRegion()
    {
        ModelInspectionResult firstResult = PresentationTestData.CreateResult(
            ModelInspectionOutcome.Ready);
        ModelInspectionResult secondResult = PresentationTestData.CreateResult(
            ModelInspectionOutcome.Ready,
            evidence: PresentationTestData.CreateEvidence(
                tokenizerModel: "sentencepiece",
                layerCount: 32),
            duration: TimeSpan.FromSeconds(7));
        ModelInspectionPagePresentation first = Create(
            TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(firstResult),
                attempt: 8));
        ModelInspectionPagePresentation second = Create(
            TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(secondResult),
                attempt: 9));

        Assert.AreNotEqual(
            FlattenText(first.ModelCard),
            FlattenText(second.ModelCard));
        Assert.AreNotEqual(first.RegionKeys.Model, second.RegionKeys.Model);
        Assert.AreEqual(first.RegionKeys.Outcome, second.RegionKeys.Outcome);
        Assert.AreEqual(first.RegionKeys.Content, second.RegionKeys.Content);
        Assert.AreEqual(first.RegionKeys.Actions, second.RegionKeys.Actions);
        Assert.AreEqual(first.RegionKeys.Footer, second.RegionKeys.Footer);
        Assert.AreEqual(first.RegionKeys.Progress, second.RegionKeys.Progress);
        Assert.AreNotEqual(
            first.RegionKeys.Announcements,
            second.RegionKeys.Announcements);
    }

    [TestMethod]
    public void RegionKeys_CancelAvailabilityChangesActionsOnly()
    {
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completed: 1,
            "Reading configuration.");
        ModelInspectionViewSnapshot snapshot = new(
            new ModelInspectionRenderKey(4, 2),
            isRunActive: true,
            isCancellationRequested: false,
            progress,
            terminalResult: null);
        ModelInspectionPagePresentation enabled = Create(
            snapshot,
            commands: CreateCommands(cancelEnabled: true));
        ModelInspectionPagePresentation disabled = Create(
            snapshot,
            commands: CreateCommands(cancelEnabled: false));

        Assert.AreNotEqual(enabled.RegionKeys.Actions, disabled.RegionKeys.Actions);
        Assert.AreEqual(enabled.RegionKeys.Outcome, disabled.RegionKeys.Outcome);
        Assert.AreEqual(enabled.RegionKeys.Model, disabled.RegionKeys.Model);
        Assert.AreEqual(enabled.RegionKeys.Content, disabled.RegionKeys.Content);
        Assert.AreEqual(enabled.RegionKeys.Footer, disabled.RegionKeys.Footer);
        Assert.AreEqual(enabled.RegionKeys.Progress, disabled.RegionKeys.Progress);
        Assert.AreEqual(enabled.RegionKeys.Announcements, disabled.RegionKeys.Announcements);
    }

    [TestMethod]
    public void RegionKeys_CancellationRequestSnapshotChangesActionsOnly()
    {
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completed: 1,
            "Reading configuration.");
        ModelInspectionViewSnapshot active = new(
            new ModelInspectionRenderKey(8, 2),
            isRunActive: true,
            isCancellationRequested: false,
            progress,
            terminalResult: null);
        ModelInspectionViewSnapshot cancellationRequested = new(
            new ModelInspectionRenderKey(8, 3),
            isRunActive: true,
            isCancellationRequested: true,
            progress,
            terminalResult: null);
        ModelInspectionPresentationCommands commands = CreateCommands(
            cancelEnabled: true);

        ModelInspectionPagePresentation before = Create(
            active,
            commands: commands);
        ModelInspectionPagePresentation after = Create(
            cancellationRequested,
            commands: commands);

        Assert.IsTrue(before.ActionCard.CancelAction.IsEnabled);
        Assert.IsFalse(after.ActionCard.CancelAction.IsEnabled);
        Assert.AreNotEqual(before.RegionKeys.Actions, after.RegionKeys.Actions);
        Assert.AreEqual(before.RegionKeys.Outcome, after.RegionKeys.Outcome);
        Assert.AreEqual(before.RegionKeys.Model, after.RegionKeys.Model);
        Assert.AreEqual(before.RegionKeys.Content, after.RegionKeys.Content);
        Assert.AreEqual(before.RegionKeys.Footer, after.RegionKeys.Footer);
        Assert.AreEqual(before.RegionKeys.Progress, after.RegionKeys.Progress);
        Assert.AreEqual(
            before.RegionKeys.Announcements,
            after.RegionKeys.Announcements);
    }

    [TestMethod]
    public void RegionKeys_InitialToActiveWithoutProgressChangesActionsOnly()
    {
        ModelInspectionViewSnapshot active = new(
            new ModelInspectionRenderKey(0, 1),
            isRunActive: true,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);
        ModelInspectionPresentationCommands commands = CreateCommands(
            cancelEnabled: true);

        ModelInspectionPagePresentation initial = Create(
            ModelInspectionViewSnapshot.Initial,
            commands: commands);
        ModelInspectionPagePresentation running = Create(
            active,
            commands: commands);

        Assert.AreEqual(
            initial.ModelCard.CompactSummary,
            running.ModelCard.CompactSummary);
        Assert.AreNotEqual(initial.RegionKeys.Actions, running.RegionKeys.Actions);
        Assert.AreEqual(initial.RegionKeys.Outcome, running.RegionKeys.Outcome);
        Assert.AreEqual(initial.RegionKeys.Model, running.RegionKeys.Model);
        Assert.AreEqual(initial.RegionKeys.Content, running.RegionKeys.Content);
        Assert.AreEqual(initial.RegionKeys.Footer, running.RegionKeys.Footer);
        Assert.AreEqual(initial.RegionKeys.Progress, running.RegionKeys.Progress);
        Assert.AreEqual(
            initial.RegionKeys.Announcements,
            running.RegionKeys.Announcements);
    }

    [TestMethod]
    public void RegionKeys_ProgressChangesProgressAndAnnouncementOnly()
    {
        ModelInspectionViewSnapshot firstSnapshot = new(
            new ModelInspectionRenderKey(7, 1),
            isRunActive: true,
            isCancellationRequested: false,
            CreateProgress(
                ModelInspectionStage.ReadModelConfiguration,
                ModelInspectionStageStatus.Active,
                completed: 1,
                "Reading configuration."),
            terminalResult: null);
        ModelInspectionViewSnapshot secondSnapshot = new(
            new ModelInspectionRenderKey(7, 2),
            isRunActive: true,
            isCancellationRequested: false,
            CreateProgress(
                ModelInspectionStage.ReadModelConfiguration,
                ModelInspectionStageStatus.Completed,
                completed: 2,
                "Configuration checked."),
            terminalResult: null);
        InspectionProgressRows progressRows = CreateProgressRows(attempt: 7);
        ModelInspectionPagePresentation first = Create(firstSnapshot, progressRows);
        ModelInspectionPagePresentation second = Create(secondSnapshot, progressRows);

        Assert.AreNotEqual(first.RegionKeys.Progress, second.RegionKeys.Progress);
        Assert.AreNotEqual(
            first.RegionKeys.Announcements,
            second.RegionKeys.Announcements);
        Assert.AreNotEqual(first.ProgressAnnouncement, second.ProgressAnnouncement);
        Assert.AreSame(progressRows, first.ContentCard.ProgressRows);
        Assert.AreSame(progressRows, second.ContentCard.ProgressRows);
        Assert.AreSame(first.ContentCard.Items, second.ContentCard.Items);
        Assert.AreEqual(first.RegionKeys.Outcome, second.RegionKeys.Outcome);
        Assert.AreEqual(first.RegionKeys.Model, second.RegionKeys.Model);
        Assert.AreEqual(first.RegionKeys.Content, second.RegionKeys.Content);
        Assert.AreEqual(first.RegionKeys.Actions, second.RegionKeys.Actions);
        Assert.AreEqual(first.RegionKeys.Footer, second.RegionKeys.Footer);
    }

    [TestMethod]
    public void RegionKeys_TerminalOutcomeChangesExactlyTerminalRegions()
    {
        ModelInspectionPagePresentation ready = Create(
            TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)),
                revision: 3));
        ModelInspectionPagePresentation invalid = Create(
            TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(ModelInspectionOutcome.Invalid)),
                revision: 4));

        Assert.AreNotEqual(ready.RegionKeys.Outcome, invalid.RegionKeys.Outcome);
        Assert.AreNotEqual(ready.RegionKeys.Model, invalid.RegionKeys.Model);
        Assert.AreNotEqual(ready.RegionKeys.Content, invalid.RegionKeys.Content);
        Assert.AreNotEqual(ready.RegionKeys.Actions, invalid.RegionKeys.Actions);
        Assert.AreNotEqual(ready.RegionKeys.Footer, invalid.RegionKeys.Footer);
        Assert.AreNotEqual(
            ready.RegionKeys.Announcements,
            invalid.RegionKeys.Announcements);
        Assert.AreEqual(ready.RegionKeys.Progress, invalid.RegionKeys.Progress);
    }

    [TestMethod]
    public void RegionKeys_TerminalAnnouncementDeduplicatesRevisionButNotAttempt()
    {
        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));
        ModelInspectionPagePresentation revisionOne = Create(
            TerminalSnapshot(execution, attempt: 4, revision: 1));
        ModelInspectionPagePresentation revisionTwo = Create(
            TerminalSnapshot(execution, attempt: 4, revision: 2));
        ModelInspectionPagePresentation nextAttempt = Create(
            TerminalSnapshot(execution, attempt: 5, revision: 1));

        Assert.AreEqual(
            revisionOne.RegionKeys.Announcements,
            revisionTwo.RegionKeys.Announcements);
        Assert.AreNotEqual(
            revisionOne.RegionKeys.Announcements,
            nextAttempt.RegionKeys.Announcements);
    }

    private static ModelInspectionPagePresentation CreateForState(
        ModelInspectionFigmaState state,
        bool forceExpanded = false)
    {
        bool expanded = forceExpanded || IsExpandedState(state);
        return Create(
            CreateSnapshotForState(state),
            isDisclosureExpanded: expanded);
    }

    private static ModelInspectionViewSnapshot CreateSnapshotForState(
        ModelInspectionFigmaState state)
    {
        return state switch
        {
            ModelInspectionFigmaState.InspectionProgress =>
                ModelInspectionViewSnapshot.Initial,
            ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded => TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(ModelInspectionOutcome.Ready))),
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded => TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.ReadyWithWarnings))),
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded => TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.ConversionRequired))),
            ModelInspectionFigmaState.IncompletePackage => TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.IncompletePackage))),
            ModelInspectionFigmaState.Unsupported => TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(ModelInspectionOutcome.Unsupported))),
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded => TerminalSnapshot(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(ModelInspectionOutcome.Invalid))),
            ModelInspectionFigmaState.Cancelled => TerminalSnapshot(
                ModelInspectionExecutionResult.Cancelled(cooperative: true)),
            ModelInspectionFigmaState.OperationalFailure => TerminalSnapshot(
                ModelInspectionExecutionResult.OperationalFailure(
                    PresentationTestData.CreateFailure())),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }

    private static bool IsExpandedState(ModelInspectionFigmaState state) => state is
        ModelInspectionFigmaState.ReadyExpanded or
        ModelInspectionFigmaState.ReadyWithWarningsExpanded or
        ModelInspectionFigmaState.ConversionRequiredExpanded or
        ModelInspectionFigmaState.InvalidExpanded;

    private static ModelInspectionPagePresentation Create(
        ModelInspectionViewSnapshot snapshot,
        InspectionProgressRows? progressRows = null,
        ModelInspectionPresentationCommands? commands = null,
        bool isDisclosureExpanded = false,
        ModelInspectionRequest? request = null)
    {
        return ModelInspectionPresentationFactory.Create(
            request ?? PresentationTestData.CreateRequest(),
            snapshot,
            commands ?? CreateCommands(),
            isDisclosureExpanded,
            progressRows ?? CreateProgressRows(
                snapshot.RenderKey.AttemptGeneration));
    }

    private static InspectionProgressRows CreateProgressRows(long attempt)
    {
        InspectionProgressRows rows = new();
        if (attempt > 0)
        {
            rows.Reset(new ModelInspectionRenderKey(attempt, 0));
        }

        return rows;
    }

    private static ModelInspectionPresentationCommands CreateCommands(
        bool cancelEnabled = true)
    {
        return new ModelInspectionPresentationCommands(
            PresentationTestData.CreateCommand(cancelEnabled),
            PresentationTestData.CreateCommand(),
            PresentationTestData.CreateCommand());
    }

    private static ModelInspectionViewSnapshot TerminalSnapshot(
        ModelInspectionExecutionResult execution,
        long attempt = 1,
        long revision = 1)
    {
        return new ModelInspectionViewSnapshot(
            new ModelInspectionRenderKey(attempt, revision),
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: execution);
    }

    private static ModelInspectionProgress CreateProgress(
        ModelInspectionStage stage,
        ModelInspectionStageStatus status,
        int completed,
        string message)
    {
        return new ModelInspectionProgress(
            stage,
            status,
            completed,
            totalStageCount: 5,
            stageFraction: status == ModelInspectionStageStatus.Active ? 0.5 : 1,
            message);
    }

    private static ModelInspectionResult CreateEnvelopeContradiction(
        string mutation)
    {
        ModelInspectionOutcome outcome = mutation == "warning-template-present"
            ? ModelInspectionOutcome.ReadyWithWarnings
            : ModelInspectionOutcome.Ready;
        ModelInspectionEvidence evidence = mutation switch
        {
            "worker-id" => PresentationTestData.CreateEvidence(
                workerId: "Unapproved.Worker"),
            "protocol-version" => PresentationTestData.CreateEvidence(
                protocolVersion: 2),
            "runtime-profile" => PresentationTestData.CreateEvidence(
                runtimeProfile: "unapproved-profile"),
            "llamasharp-version" => PresentationTestData.CreateEvidence(
                llamaSharpVersion: "0.28.0"),
            "backend-version" => PresentationTestData.CreateEvidence(
                backendPackageVersion: "0.28.0"),
            "llama-commit" => PresentationTestData.CreateEvidence(
                mappedLlamaCppCommit:
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            "native-library" => PresentationTestData.CreateEvidence(
                nativeLibraryName: "not-llama.dll"),
            "process-architecture" => PresentationTestData.CreateEvidence(
                processArchitecture: "Arm64"),
            "inspection-mode" => PresentationTestData.CreateEvidence(
                inspectionMode: "Full"),
            "tokenizer-smoke-failed" => PresentationTestData.CreateEvidence(
                tokenizerSmokePassed: false,
                tokenizerSmokeTokenCount: 0),
            "tokenizer-smoke-unknown" => PresentationTestData.CreateEvidence(
                tokenizerSmokePassed: null,
                tokenizerSmokeTokenCount: null),
            "ready-template-absent" => PresentationTestData.CreateEvidence(
                chatTemplatePresent: false),
            "warning-template-present" => PresentationTestData.CreateEvidence(
                chatTemplatePresent: true),
            "cuda-enabled" => PresentationTestData.CreateEvidence(
                usesCuda: true),
            "vulkan-enabled" => PresentationTestData.CreateEvidence(
                usesVulkan: true),
            "gpu-layer-enabled" => PresentationTestData.CreateEvidence(
                gpuLayerCount: 1),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        return PresentationTestData.CreateResult(outcome, evidence: evidence);
    }

    private static bool HasDisclosure(ModelInspectionPagePresentation presentation)
    {
        return presentation.ModelCard.InspectionDetailsVisibility == Visibility.Visible ||
            presentation.ContentCard.DisclosureVisibility == Visibility.Visible;
    }

    private static void AssertActionSlot(
        InspectionActionPresentation action,
        string? expectedLabel,
        string expectedRole,
        ModelInspectionPresentationCommands commands,
        bool expectedEnabled)
    {
        if (expectedRole == "Hidden")
        {
            Assert.AreEqual(Visibility.Collapsed, action.Visibility);
            Assert.IsNull(action.Command);
            return;
        }

        Assert.AreEqual(Visibility.Visible, action.Visibility);
        Assert.AreEqual(expectedLabel, action.Text);
        switch (expectedRole)
        {
            case "Future":
                Assert.IsNull(action.Command);
                Assert.IsFalse(action.IsEnabled);
                Assert.AreEqual("Coming later", action.AutomationHelpText);
                break;
            case "Cancel":
                Assert.AreSame(commands.Cancel, action.Command);
                Assert.AreEqual(expectedEnabled, action.IsEnabled);
                Assert.AreEqual(string.Empty, action.AutomationHelpText);
                break;
            case "Choose":
                Assert.AreSame(commands.ChooseAnother, action.Command);
                Assert.IsTrue(action.IsEnabled);
                Assert.AreEqual(string.Empty, action.AutomationHelpText);
                break;
            case "Retry":
                Assert.AreSame(commands.Retry, action.Command);
                Assert.IsTrue(action.IsEnabled);
                Assert.AreEqual(string.Empty, action.AutomationHelpText);
                break;
            default:
                Assert.Fail($"Unknown action role '{expectedRole}'.");
                break;
        }
    }

    private static void AssertActionPolicy(ModelInspectionPagePresentation presentation)
    {
        InspectionActionPresentation[] actions =
        [
            presentation.ActionCard.CancelAction,
            presentation.ActionCard.SecondaryActionOne,
            presentation.ActionCard.SecondaryActionTwo,
            presentation.ActionCard.PrimaryAction
        ];
        InspectionActionPresentation[] visible = actions
            .Where(action => action.Visibility == Visibility.Visible)
            .ToArray();

        Assert.IsTrue(visible.Length > 0);
        foreach (InspectionActionPresentation futureAction in visible.Where(
            action => action.Command is null))
        {
            Assert.IsFalse(futureAction.IsEnabled);
            Assert.AreEqual("Coming later", futureAction.AutomationHelpText);
        }
    }

    private static string FlattenText(ModelInspectionPagePresentation presentation)
    {
        IEnumerable<string> model =
        [
            presentation.ModelCard.ModelName,
            presentation.ModelCard.CompactSummary,
            presentation.ModelCard.FormatShortName,
            presentation.ModelCard.OverviewFormatBadgeText,
            presentation.ModelCard.Publisher,
            presentation.ModelCard.FormatName,
            presentation.ModelCard.Quantisation,
            presentation.ModelCard.ParameterCount,
            presentation.ModelCard.ModelType,
            presentation.ModelCard.DeclaredContext,
            presentation.ModelCard.FileSize,
            presentation.ModelCard.InspectionChecksSummary
        ];
        IEnumerable<string> checks = presentation.ModelCard.InspectionChecks
            .SelectMany(check => new[]
            {
                check.Title,
                check.Detail,
                check.StatusText,
                check.AutomationName
            });
        IEnumerable<string> content = presentation.ContentCard.Items
            .Concat(presentation.ContentCard.ExpandedItems)
                .SelectMany(item => new[]
            {
                item.StageNumber,
                item.Title,
                item.Detail,
                item.StatusText,
                item.AutomationName
            });
        IEnumerable<InspectionActionPresentation> actions =
        [
            presentation.ActionCard.CancelAction,
            presentation.ActionCard.SecondaryActionOne,
            presentation.ActionCard.SecondaryActionTwo,
            presentation.ActionCard.PrimaryAction
        ];

        return string.Join(
            "\n",
            model
                .Concat(checks)
                .Concat(content)
                .Concat(
                [
                    presentation.OutcomeCard.Title,
                    presentation.OutcomeCard.Message,
                    presentation.OutcomeCard.AutomationName,
                    presentation.ContentCard.SectionTitle,
                    presentation.ContentCard.ProgressSummary,
                    presentation.ContentCard.SupportingText,
                    presentation.ContentCard.TertiaryText,
                    presentation.ContentCard.DiagnosticCode,
                    presentation.ContentCard.DisclosureSummary,
                    presentation.ContentCard.CollapsedDisclosureText,
                    presentation.ContentCard.ExpandedDisclosureText,
                    presentation.ContentCard.DisclosureAutomationName,
                    presentation.ContentCard.TechnicalDetailsActionText,
                    presentation.ContentCard.TechnicalDetailsAutomationName,
                    presentation.ContentCard.TechnicalDetailsAutomationHelpText,
                    presentation.ActionCard.Title,
                    presentation.ActionCard.Message,
                    presentation.ActionCard.AutomationName,
                    presentation.ProgressAnnouncement,
                    presentation.OutcomeAnnouncement
                ])
                .Concat(actions.SelectMany(action => new[]
                {
                    action.Text,
                    action.AutomationName,
                    action.AutomationHelpText
                })));
    }

    private static string FlattenRegionKeys(
        ModelInspectionPagePresentation presentation)
    {
        return string.Join(
            "\n",
            presentation.RegionKeys.Outcome.Value,
            presentation.RegionKeys.Model.Value,
            presentation.RegionKeys.Content.Value,
            presentation.RegionKeys.Actions.Value,
            presentation.RegionKeys.Footer.Value,
            presentation.RegionKeys.Progress.Detail,
            presentation.RegionKeys.Announcements.Value);
    }

    private static string FlattenText(InspectionModelCardPresentation model)
    {
        return string.Join(
            "\n",
            model.InspectionChecks.SelectMany(check => new[]
            {
                check.Title,
                check.Detail,
                check.StatusText,
                check.AutomationName
            }));
    }
}
