using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationOutcomeCardTests
{
    [UITestMethod]
    public void SuccessActionsDescribeTheArtifactThatActuallyExists()
    {
        OptimizationOutcomeCard card = new();

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-persistent").Presentation);
        CollectionAssert.AreEqual(
            new[] { "Chat with this model", "Save model to this computer" },
            card.VisibleActionTexts.ToArray());

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-runtime-profile").Presentation);
        CollectionAssert.AreEqual(
            new[] { "Chat with this model", "Save this setup" },
            card.VisibleActionTexts.ToArray());
    }

    [UITestMethod]
    public void RecoveryStateShowsBoundedSupportCodeAndActions()
    {
        OptimizationOutcomeCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "failed").Presentation);

        Assert.AreEqual("ValidationFailed", card.VisibleSupportCode);
        CollectionAssert.AreEqual(
            new[] { "Try again", "Review configuration" },
            card.VisibleActionTexts.ToArray());
    }
}
