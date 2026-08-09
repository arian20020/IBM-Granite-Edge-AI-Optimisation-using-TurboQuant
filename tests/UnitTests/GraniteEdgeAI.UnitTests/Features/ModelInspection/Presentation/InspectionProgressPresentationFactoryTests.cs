using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
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
    public void Create_AlwaysReturnsTheFiveApprovedStagesInOrder()
    {
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ValidateModelStructure,
            ModelInspectionStageStatus.Active,
            completedStageCount: 3);

        InspectionContentCardPresentation presentation =
            InspectionProgressPresentationFactory.Create(progress);

        Assert.AreEqual(InspectionContentCardMode.Progress, presentation.Mode);
        Assert.HasCount(5, presentation.Items);
        CollectionAssert.AreEqual(
            ExpectedStageTitles,
            presentation.Items.Select(item => item.Title).ToArray());
        Assert.IsTrue(presentation.Items.Take(4).All(item => item.ShowConnector));
        Assert.IsFalse(presentation.Items[4].ShowConnector);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_MarksPriorStagesPassedAndLaterStagesWaiting()
    {
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            ModelInspectionStageStatus.Active,
            completedStageCount: 2);

        InspectionContentCardPresentation presentation =
            InspectionProgressPresentationFactory.Create(progress);

        foreach (InspectionContentItemPresentation completed in
                 presentation.Items.Take(2))
        {
            Assert.AreEqual(InspectionContentStatus.Passed, completed.Status);
            Assert.AreEqual("Passed", completed.StatusText);
            Assert.IsFalse(completed.IsActive);
            Assert.AreEqual(Visibility.Collapsed, completed.DetailVisibility);
        }

        foreach (InspectionContentItemPresentation waiting in
                 presentation.Items.Skip(3))
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
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ReadModelConfiguration,
            stageStatus,
            completedStageCount);

        InspectionContentCardPresentation presentation =
            InspectionProgressPresentationFactory.Create(progress);
        InspectionContentItemPresentation current = presentation.Items[1];

        Assert.AreEqual(expectedStatus, current.Status);
        Assert.AreEqual(expectedStatusText, current.StatusText);
        Assert.AreEqual(expectedActive, current.IsActive);
        Assert.AreEqual(Visibility.Visible, current.DetailVisibility);
        Assert.AreEqual("A safe, current-stage detail.", current.Detail);
        Assert.AreEqual(
            $"{current.Title}. {expectedStatusText}. A safe, current-stage detail.",
            current.AutomationName);
        Assert.IsTrue(presentation.Items.Count(item => item.IsActive) <= 1);
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
            InspectionProgressPresentationFactory.Create(progress);

        Assert.AreEqual(0.375, presentation.Items[3].StageFraction);
        Assert.IsTrue(
            presentation.Items
                .Where((_, index) => index != 3)
                .All(item => item.StageFraction is null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_PreservesAnUnknownStageFractionAsNull()
    {
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            ModelInspectionStageStatus.Active,
            completedStageCount: 4);

        InspectionContentCardPresentation presentation =
            InspectionProgressPresentationFactory.Create(progress);

        Assert.IsTrue(presentation.Items.All(item => item.StageFraction is null));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UsesTheReportedCompletedCountWithoutInventingProgress()
    {
        ModelInspectionProgress progress = CreateProgress(
            ModelInspectionStage.ValidateModelStructure,
            ModelInspectionStageStatus.Warning,
            completedStageCount: 4);

        InspectionContentCardPresentation presentation =
            InspectionProgressPresentationFactory.Create(progress);

        Assert.AreEqual("4 of 5 checks complete", presentation.ProgressSummary);
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
