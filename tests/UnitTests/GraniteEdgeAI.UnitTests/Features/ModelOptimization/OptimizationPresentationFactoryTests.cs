using System.Linq;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
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

    [TestMethod]
    public void RuntimeOnlySuccessHasChatAndDoneButNoSave()
    {
        OptimizationPresentationState state = OptimizationFixtureCatalog.All.Single(
            static item => item.Id == "success-runtime-profile").Presentation;

        CollectionAssert.AreEqual(
            new[] { OptimizationCommand.Chat, OptimizationCommand.Done },
            state.Actions.Select(static action => action.Command).ToArray());
        StringAssert.Contains(
            state.Summary,
            "does not create a new model file");
    }

    [TestMethod]
    public void OptionalRecoveryRetainsOriginalChatButRequiredRecoveryDoesNot()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            static item => item.Id == "confirmation").Presentation;
        OptimizationPresentationState optional =
            OptimizationPresentationFactory.Cancelled(
                seed.Preference!,
                seed.Configuration,
                OptimizationJourneyOrigin.Optional);
        OptimizationPresentationState required =
            OptimizationPresentationFactory.Cancelled(
                seed.Preference!,
                seed.Configuration,
                OptimizationJourneyOrigin.Required);

        CollectionAssert.Contains(
            optional.Actions.Select(static action => action.Command).ToArray(),
            OptimizationCommand.ChatWithOriginal);
        CollectionAssert.DoesNotContain(
            required.Actions.Select(static action => action.Command).ToArray(),
            OptimizationCommand.ChatWithOriginal);
    }

    [TestMethod]
    public void PresentationVocabularyContainsNoPreselectionCommandsOrState()
    {
        CollectionAssert.DoesNotContain(
            Enum.GetNames<OptimizationPageStateKind>(),
            "Selecting");
        CollectionAssert.DoesNotContain(
            Enum.GetNames<OptimizationCommand>(),
            "PreferenceChanged");
        CollectionAssert.DoesNotContain(
            Enum.GetNames<OptimizationCommand>(),
            "ReviewConfigurationRequested");
    }
}
