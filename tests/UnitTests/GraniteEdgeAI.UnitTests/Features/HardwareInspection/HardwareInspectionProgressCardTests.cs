using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class HardwareInspectionProgressCardTests
{
    private readonly HardwareInspectionPresentationFactory _factory = new();

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_InitialStageRendersExactApprovedFrame()
    {
        HardwareInspectionProgressCard card = new();
        card.Apply(_factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection));

        Assert.AreEqual("Inspection in progress", Text(card, "KickerTextBlock").Text);
        Assert.AreEqual("Starting hardware inspection", Text(card, "TitleTextBlock").Text);
        Assert.AreEqual(
            "Preparing the approved local inspection tools and a safe run context.",
            Text(card, "BodyTextBlock").Text);
        Assert.AreEqual("0 of 7", Text(card, "CountTextBlock").Text);
        Assert.AreEqual("checks complete", Text(card, "CountLabelTextBlock").Text);
        Assert.AreEqual(7, card.Rows.Count);
        Assert.AreEqual(1, card.Rows.Count(row => row.ActiveVisibility == Visibility.Visible));
        Assert.AreEqual(0, card.Rows.Count(row => row.CompletedVisibility == Visibility.Visible));
        Assert.AreEqual(6, card.Rows.Count(row => row.WaitingVisibility == Visibility.Visible));
        Assert.AreEqual("Active", card.Rows[0].Status);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_LaterStageReplacesRowsWithoutRetainingStaleState()
    {
        HardwareInspectionProgressCard card = new();
        card.Apply(_factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection));
        card.Apply(_factory.CreateActive(HardwareInspectionStage.NormalisingHardwareInformation));

        Assert.AreEqual("Normalising hardware information", Text(card, "TitleTextBlock").Text);
        Assert.AreEqual("5 of 7", Text(card, "CountTextBlock").Text);
        Assert.AreEqual(7, card.Rows.Count);
        Assert.AreEqual(5, card.Rows.Count(row => row.CompletedVisibility == Visibility.Visible));
        Assert.AreEqual(1, card.Rows.Count(row => row.ActiveVisibility == Visibility.Visible));
        Assert.AreEqual(1, card.Rows.Count(row => row.WaitingVisibility == Visibility.Visible));
        Assert.AreEqual("Active", card.Rows[5].Status);
        Assert.AreEqual("Waiting", card.Rows[6].Status);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RejectsNonActivePresentation()
    {
        HardwareInspectionProgressCard card = new();

        Assert.Throws<ArgumentException>(() => card.Apply(
            _factory.CreateTerminal(HardwareInspectionOutcome.Completed)));
    }

    private static TextBlock Text(HardwareInspectionProgressCard card, string name) =>
        (TextBlock)card.FindName(name);
}
