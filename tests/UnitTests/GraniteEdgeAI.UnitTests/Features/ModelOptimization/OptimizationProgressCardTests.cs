using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationProgressCardTests
{
    [UITestMethod]
    [TestCategory("EstimatedProgress")]
    public void OpaqueOptimisationRowDisclosesEstimatedPercentage()
    {
        OptimizationProgressCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "progress-optimise").Presentation);
        Assert.IsTrue(card.ProgressRowElements.Any(row => AutomationProperties.GetItemStatus(row).Contains("Estimated 0%")));
    }

    [UITestMethod]
    public void RunningStateRendersSevenEqualOrderedRows()
    {
        OptimizationProgressCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "progress-optimise").Presentation);

        Assert.AreEqual(7, card.ProgressRowElements.Count);
        Assert.IsTrue(card.ProgressRowElements.All(row => row.MinHeight == 56));
        Assert.IsTrue(card.ProgressRowElements.All(row => row.VerticalAlignment == VerticalAlignment.Stretch));
        CollectionAssert.AreEqual(
            new[] { "Preflight", "Prepare staging", "Optimise", "Validate", "Smoke test", "Reinspect", "Publish" },
            card.ProgressRowTitles.ToArray());
    }

    [UITestMethod]
    public async System.Threading.Tasks.Task RunningStateAnimatesOnlyTheCoordinatorActiveStage()
    {
        OptimizationProgressCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "progress-optimise").Presentation);

        await using var host = await GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.WinUiRenderHost.ShowAsync(card, 1000, 650);

        ProgressRing[] rings = card.ProgressRowElements.Cast<ListViewItem>()
            .SelectMany(row => ((Grid)row.Content).Children.OfType<ProgressRing>())
            .ToArray();

        Assert.AreEqual(1, rings.Length);
        Assert.IsTrue(rings[0].IsActive);
        StringAssert.Contains(AutomationProperties.GetName(card.ProgressRowElements[2]), "Optimise. Active");
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(
        DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
