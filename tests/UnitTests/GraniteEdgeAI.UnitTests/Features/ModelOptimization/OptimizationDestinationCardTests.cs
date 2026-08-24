using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationDestinationCardTests
{
    [UITestMethod]
    public void DestinationUsesTruthfulSaveAction()
    {
        OptimizationDestinationCard card = new();

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-persistent").Presentation);
        Assert.AreEqual("Chat with this model", card.PrimaryActionText);
        Assert.AreEqual("Save model to this computer", card.SecondaryActionText);

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-runtime-profile").Presentation);
        Assert.AreEqual("Chat with this model", card.PrimaryActionText);
        Assert.AreEqual("Save this setup", card.SecondaryActionText);
    }

    [UITestMethod]
    public void RecoveryStateShowsBoundedSupportCodeAndActions()
    {
        OptimizationRecoveryCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "failed").Presentation);

        Assert.AreEqual("ValidationFailed", card.VisibleSupportCode);
        CollectionAssert.AreEqual(
            new[] { "Try again", "Review configuration" },
            card.VisibleActionTexts.ToArray());
    }
}
