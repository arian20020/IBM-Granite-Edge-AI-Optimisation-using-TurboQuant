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
    public void RunningStateRendersSevenEqualOrderedRows()
    {
        OptimizationProgressCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "progress-optimise").Presentation);

        Assert.AreEqual(7, card.ProgressRowElements.Count);
        Assert.IsTrue(card.ProgressRowElements.All(row => row.MinHeight == 64));
        Assert.IsTrue(card.ProgressRowElements.All(row => row.VerticalAlignment == VerticalAlignment.Stretch));
        CollectionAssert.AreEqual(
            new[] { "Preflight", "Prepare staging", "Optimise", "Validate", "Smoke test", "Reinspect", "Publish" },
            card.ProgressRowTitles.ToArray());
    }

    [UITestMethod]
    public void RunningStateAnimatesOnlyTheCoordinatorActiveStage()
    {
        OptimizationProgressCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "progress-optimise").Presentation);

        ProgressRing[] rings = Descendants(card)
            .OfType<ProgressRing>()
            .ToArray();

        Assert.AreEqual(1, rings.Length);
        Assert.IsTrue(rings[0].IsActive);
        StringAssert.Contains(
            AutomationProperties.GetName(rings[0]),
            "Optimise");
        StringAssert.Contains(
            AutomationProperties.GetName(rings[0]),
            "in progress");
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
