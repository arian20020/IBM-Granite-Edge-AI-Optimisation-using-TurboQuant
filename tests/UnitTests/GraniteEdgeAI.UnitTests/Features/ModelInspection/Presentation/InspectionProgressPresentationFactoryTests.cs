using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
public sealed class InspectionProgressPresentationFactoryTests
{
    private static readonly string[] ExpectedStageTitles =
    [
        "Check model package",
        "Read model configuration",
        "Validate tokenizer and chat setup",
        "Validate model structure",
        "Confirm core runtime compatibility"
    ];

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_AlwaysUsesTheFiveApprovedStagesInOrder()
    {
        InspectionContentCardPresentation presentation = CreatePresentation(
            CreateProgress(
                ModelInspectionStage.ValidateModelStructure,
                ModelInspectionStageStatus.Active,
                completedStageCount: 3));

        Assert.AreEqual(InspectionContentCardMode.Progress, presentation.Mode);
        Assert.HasCount(5, presentation.ProgressRows.Items);
        CollectionAssert.AreEqual(
            ExpectedStageTitles,
            presentation.ProgressRows.Items.Select(item => item.Title).ToArray());
        Assert.IsTrue(
            presentation.ProgressRows.Items.Take(4)
                .All(item => item.ShowConnector));
        Assert.IsFalse(presentation.ProgressRows.Items[4].ShowConnector);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_ProducesAnImmutableSafeUpdateWithoutMutatingRows()
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            ModelInspectionStageStatus.Active,
            completedStageCount: 2);

        InspectionProgressRowsUpdate update =
            InspectionProgressPresentationFactory.Create(
                progress,
                new ModelInspectionRenderKey(1, 1));

        Assert.IsTrue(rows.Items.All(item =>
            item.Status == InspectionContentStatus.Waiting));
        Assert.AreEqual(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            update.Key.Stage);
        Assert.AreEqual(ModelInspectionStageStatus.Active, update.Key.StageStatus);
        Assert.AreEqual(2, update.Key.CompletedStageCount);
        Assert.AreEqual(5, update.Key.StageCount);
        Assert.AreEqual("A safe, current-stage detail.", update.Key.Detail);
        Assert.AreEqual("2 of 5 checks complete", update.ProgressSummary);

        rows.Apply(update);
        Assert.AreEqual(InspectionContentStatus.Active, rows.Items[2].Status);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void UnifiedCreate_CarriesUpdateAndNeverMutatesRetainedRows()
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1);
        ModelInspectionViewSnapshot snapshot = new(
            new ModelInspectionRenderKey(1, 1),
            isRunActive: true,
            isCancellationRequested: false,
            progress,
            terminalResult: null);

        ModelInspectionPagePresentation presentation =
            ModelInspectionPresentationFactory.Create(
                PresentationTestData.CreateRequest(),
                snapshot,
                new ModelInspectionPresentationCommands(
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                rows);

        Assert.AreSame(rows, presentation.ContentCard.ProgressRows);
        Assert.IsTrue(rows.Items.All(item =>
            item.Status == InspectionContentStatus.Waiting));
        Assert.AreEqual(snapshot.RenderKey, presentation.ProgressRowsUpdate.OwnerKey);
        Assert.AreEqual(
            presentation.RegionKeys.Progress,
            presentation.ProgressRowsUpdate.Key);

        rows.Apply(presentation.ProgressRowsUpdate);
        Assert.AreEqual(InspectionContentStatus.Active, rows.Items[1].Status);
        Assert.AreEqual(
            "1 of 5 checks complete",
            presentation.ContentCard.ProgressRows.ProgressSummary);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_MarksPriorStagesPassedAndLaterStagesWaiting()
    {
        InspectionContentCardPresentation presentation = CreatePresentation(
            CreateProgress(
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                ModelInspectionStageStatus.Active,
                completedStageCount: 2));

        foreach (InspectionContentItemPresentation completed in
                 presentation.ProgressRows.Items.Take(2))
        {
            Assert.AreEqual(InspectionContentStatus.Passed, completed.Status);
            Assert.AreEqual("Passed", completed.StatusText);
            Assert.IsFalse(completed.IsActive);
            Assert.AreEqual(Visibility.Collapsed, completed.DetailVisibility);
        }

        foreach (InspectionContentItemPresentation waiting in
                 presentation.ProgressRows.Items.Skip(3))
        {
            Assert.AreEqual(InspectionContentStatus.Waiting, waiting.Status);
            Assert.AreEqual("Waiting", waiting.StatusText);
            Assert.IsFalse(waiting.IsActive);
            Assert.AreEqual(Visibility.Collapsed, waiting.DetailVisibility);
        }
    }

    [TestMethod]
    [TestCategory("WinUI")]
    [DataRow(
        (int)ModelInspectionStageStatus.Active,
        InspectionContentStatus.Active,
        "Checking",
        true)]
    [DataRow(
        (int)ModelInspectionStageStatus.Completed,
        InspectionContentStatus.Passed,
        "Passed",
        false)]
    [DataRow(
        (int)ModelInspectionStageStatus.Warning,
        InspectionContentStatus.Warning,
        "Warning",
        false)]
    [DataRow(
        (int)ModelInspectionStageStatus.Failed,
        InspectionContentStatus.Error,
        "Failed",
        false)]
    [DataRow(
        (int)ModelInspectionStageStatus.Cancelled,
        InspectionContentStatus.Information,
        "Cancelled",
        false)]
    public void Create_MapsTheCurrentExplicitStatus(
        int stageStatusValue,
        InspectionContentStatus expectedStatus,
        string expectedStatusText,
        bool expectedActive)
    {
        ModelInspectionStageStatus stageStatus =
            (ModelInspectionStageStatus)stageStatusValue;
        int completedStageCount = stageStatus is
            ModelInspectionStageStatus.Completed or
            ModelInspectionStageStatus.Warning
                ? 2
                : 1;
        InspectionContentCardPresentation presentation = CreatePresentation(
            CreateProgress(
                ModelInspectionStage.ReadModelConfiguration,
                stageStatus,
                completedStageCount));
        InspectionContentItemPresentation current =
            presentation.ProgressRows.Items[1];

        Assert.AreEqual(expectedStatus, current.Status);
        Assert.AreEqual(expectedStatusText, current.StatusText);
        Assert.AreEqual(expectedActive, current.IsActive);
        Assert.AreEqual(Visibility.Visible, current.DetailVisibility);
        Assert.AreEqual("A safe, current-stage detail.", current.Detail);
        Assert.AreEqual(
            $"{current.Title}. {expectedStatusText}. A safe, current-stage detail.",
            current.AutomationName);
        Assert.IsTrue(
            presentation.ProgressRows.Items.Count(item => item.IsActive) <= 1);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_PreservesOnlyTheGenuineCurrentStageFraction()
    {
        ModelInspectionProgress progress = new(
            ModelInspectionStage.ValidateModelStructure,
            ModelInspectionStageStatus.Active,
            completedStageCount: 3,
            totalStageCount: 5,
            stageFraction: 0.375,
            userMessage: "Measured native work.");

        InspectionContentCardPresentation presentation =
            CreatePresentation(progress);

        Assert.AreEqual(
            0.375,
            presentation.ProgressRows.Items[3].StageFraction);
        Assert.IsTrue(
            presentation.ProgressRows.Items
                .Where((_, index) => index != 3)
                .All(item => item.StageFraction is null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_PreservesAnUnknownStageFractionAsNull()
    {
        InspectionContentCardPresentation presentation = CreatePresentation(
            CreateProgress(
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionStageStatus.Active,
                completedStageCount: 4));

        Assert.IsTrue(
            presentation.ProgressRows.Items
                .All(item => item.StageFraction is null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UsesTheReportedCompletedCountWithoutInventingProgress()
    {
        InspectionContentCardPresentation presentation = CreatePresentation(
            CreateProgress(
                ModelInspectionStage.ValidateModelStructure,
                ModelInspectionStageStatus.Warning,
                completedStageCount: 4));

        Assert.AreEqual(
            "4 of 5 checks complete",
            presentation.ProgressRows.ProgressSummary);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UnsafeProgressTextNeverEntersUpdateRowsOrAutomation()
    {
        string[] unsafeMessages =
        [
            @"C:\Users\private-user\Models\secret.gguf",
            "controlsentinel",
            "bidi‮sentinel",
            "oversize-" + new string('x', 513)
        ];

        foreach (string unsafeMessage in unsafeMessages)
        {
            ModelInspectionProgress progress = new(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Active,
                completedStageCount: 0,
                totalStageCount: 5,
                stageFraction: null,
                userMessage: unsafeMessage);
            InspectionProgressRows rows = new();
            rows.Reset(new ModelInspectionRenderKey(1, 0));

            InspectionProgressRowsUpdate update =
                InspectionProgressPresentationFactory.Create(
                    progress,
                    new ModelInspectionRenderKey(1, 1));
            rows.Apply(update);
            string visibleAndAutomationText = string.Join(
                "\n",
                new[] { update.Key.Detail, update.ProgressSummary }
                    .Concat(rows.Items.SelectMany(item => new[]
                    {
                        item.Detail,
                        item.StatusText,
                        item.AutomationName
                    })));

            Assert.AreEqual("Inspection progress updated.", update.Key.Detail);
            Assert.IsFalse(
                visibleAndAutomationText.Contains(
                    unsafeMessage,
                    StringComparison.Ordinal));
        }
    }

    private static InspectionContentCardPresentation CreatePresentation(
        ModelInspectionProgress progress)
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        InspectionProgressRowsUpdate update =
            InspectionProgressPresentationFactory.Create(
                progress,
                new ModelInspectionRenderKey(1, 1));
        rows.Apply(update);
        return InitialInspectionProgressPresentationFactory.Create(rows);
    }

    private static ModelInspectionProgress CreateProgress(
        ModelInspectionStage stage,
        ModelInspectionStageStatus stageStatus,
        int completedStageCount)
    {
        return new ModelInspectionProgress(
            stage,
            stageStatus,
            completedStageCount,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "A safe, current-stage detail.");
    }
}
