using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class HardwareInspectionDetailsSummaryTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void SummaryFactory_PreservesDistinctMemoryAndGraphicsValues()
    {
        HardwareSummaryPresentation summary = HardwareSummaryPresentationFactory.Create(
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        Assert.AreEqual("32 GiB", summary.Facts.Single(f => f.Label == "Installed memory").Value);
        Assert.AreEqual("31 GiB", summary.Facts.Single(f => f.Label == "Windows-usable memory").Value);
        Assert.AreEqual("20 GiB", summary.Facts.Single(f => f.Label == "Available now").Value);
        Assert.AreEqual("8 GiB", summary.Facts.Single(f => f.Label == "Dedicated graphics memory").Value);
        Assert.AreEqual("16 GiB", summary.Facts.Single(f => f.Label == "Shared system memory").Value);
        Assert.IsFalse(summary.Facts.Any(f => f.Label.Contains("total graphics", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(summary.Facts.Any(f => f.Label.Contains("path", StringComparison.OrdinalIgnoreCase)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SummaryCard_RendersTypedFactsWithoutInventingValues()
    {
        HardwareSummaryPresentation summary = HardwareSummaryPresentationFactory.Create(
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());
        HardwareInspectionSummaryCard card = new();
        card.Apply(summary);

        Assert.AreEqual("This computer", ((TextBlock)card.FindName("TitleTextBlock")).Text);
        Assert.AreEqual(summary.Facts.Count, card.FactItems.Count);
        Assert.AreEqual(summary.Facts[0].Value, card.FactItems[0].Value);
        Grid factGrid = (Grid)card.FindName("FactsPanel");
        Assert.AreEqual(2, factGrid.ColumnDefinitions.Count);
        Assert.AreEqual(
            summary.Facts.Select(fact => fact.Group).Distinct().Count(),
            factGrid.Children.OfType<Border>().Count());

        card.ApplyAvailableWidth(448);
        Assert.AreEqual(1, factGrid.ColumnDefinitions.Count);
        card.ApplyAvailableWidth(549);
        Assert.AreEqual(2, factGrid.ColumnDefinitions.Count);
        card.ApplyAvailableWidth(840);
        Assert.AreEqual(2, factGrid.ColumnDefinitions.Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DetailsCard_RendersSevenRowsAndTwoCollapsedLevels()
    {
        HardwareInspectionDetailsState state = CreateDetails();
        HardwareInspectionDetailsCard card = new();
        card.Apply(state, preserveDisclosureState: false);

        Assert.AreEqual("Inspection details", ((TextBlock)card.FindName("DetailsTitleTextBlock")).Text);
        Assert.AreEqual("Technical information for IT", ((TextBlock)card.FindName("TechnicalTitleTextBlock")).Text);
        Assert.AreEqual(7, card.DetailRows.Count);
        Assert.AreEqual(1, card.TechnicalGroups.Count);
        Assert.IsFalse(((Expander)card.FindName("DetailsExpander")).IsExpanded);
        Assert.IsFalse(((Expander)card.FindName("TechnicalExpander")).IsExpanded);
        Assert.AreEqual("Report created", ((TextBlock)card.FindName("ReportBadgeTextBlock")).Text);
        Assert.IsNotNull(card.FindName("DetailsSurfaceBorder"));
        Assert.IsNotNull(card.FindName("StageRowsBorder"));
        Assert.AreEqual(0, Descendants<ScrollViewer>(card).Count());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DetailsCard_PreservesSameRunAndResetsNewRunDisclosures()
    {
        HardwareInspectionDetailsCard card = new();
        HardwareInspectionDetailsState state = CreateDetails();
        card.Apply(state, preserveDisclosureState: false);
        ((Expander)card.FindName("DetailsExpander")).IsExpanded = true;
        ((Expander)card.FindName("TechnicalExpander")).IsExpanded = true;

        card.Apply(state, preserveDisclosureState: true);
        Assert.IsTrue(((Expander)card.FindName("DetailsExpander")).IsExpanded);
        Assert.IsTrue(((Expander)card.FindName("TechnicalExpander")).IsExpanded);

        card.Apply(state, preserveDisclosureState: false);
        Assert.IsFalse(((Expander)card.FindName("DetailsExpander")).IsExpanded);
        Assert.IsFalse(((Expander)card.FindName("TechnicalExpander")).IsExpanded);
    }

    internal static HardwareInspectionDetailsState CreateDetails()
    {
        HardwareInspectionDetailRow[] rows = Enum.GetValues<HardwareInspectionStage>()
            .Select(stage => new HardwareInspectionDetailRow(
                HardwareInspectionCopyCatalog.Stage(stage).Title,
                HardwareInspectionCopyCatalog.Stage(stage).CompletedSentence,
                "Completed"))
            .ToArray();
        return new HardwareInspectionDetailsState(
            "Seven completed stages and safe support information",
            "Inspection completed with no details to review.",
            "A hardware report was created from the reliable information collected in all seven stages.",
            "Report created",
            rows,
            [
                new HardwareInspectionTechnicalGroup(
                    "Run and tool information",
                    "Safe run metadata",
                    [new HardwareInspectionTechnicalItem("Policy", "hardware-policy-v1")]),
            ]);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
