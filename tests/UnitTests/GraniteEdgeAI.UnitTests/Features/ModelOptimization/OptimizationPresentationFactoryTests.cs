using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationPresentationFactoryTests
{
    [TestMethod]
    public void FixtureCatalogContainsEveryRequiredState()
    {
        string[] expected =
        [
            "selection-automatic",
            "selection-manual",
            "confirmation",
            "progress-preflight",
            "progress-staging",
            "progress-optimise",
            "progress-validate",
            "progress-smoke",
            "progress-reinspect",
            "progress-publish",
            "cancelled",
            "replan-required",
            "failed",
            "success-persistent",
            "success-runtime-profile"
        ];

        CollectionAssert.AreEquivalent(
            expected,
            OptimizationFixtureCatalog.All.Select(static item => item.Id).ToArray());
    }

    [TestMethod]
    public void RunningFixtureContainsExactlySevenOrderedStages()
    {
        OptimizationFixture fixture = OptimizationFixtureCatalog.All.Single(
            static item => item.Id == "progress-optimise");

        CollectionAssert.AreEqual(
            new[]
            {
                OptimizationStage.Preflight,
                OptimizationStage.PrepareStaging,
                OptimizationStage.Optimise,
                OptimizationStage.Validate,
                OptimizationStage.SmokeTest,
                OptimizationStage.Reinspect,
                OptimizationStage.Publish
            },
            fixture.Presentation.ProgressRows.Select(static row => row.Stage).ToArray());
    }
}
