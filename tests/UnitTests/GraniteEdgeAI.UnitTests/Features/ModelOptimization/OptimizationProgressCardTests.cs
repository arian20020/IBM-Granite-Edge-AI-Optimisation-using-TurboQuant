using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using Microsoft.UI.Xaml;
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
}
