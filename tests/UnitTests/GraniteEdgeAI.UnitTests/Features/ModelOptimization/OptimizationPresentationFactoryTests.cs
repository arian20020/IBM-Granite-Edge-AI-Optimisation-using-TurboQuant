using System.Linq;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationPresentationFactoryTests
{
    [TestMethod]
    public void OnlyOpenVinoReplanOffersOneReimportAction()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            item => item.Id == "replan-required").Presentation;
        OptimizationPresentationState openVino = OptimizationPresentationFactory.ReplanRequired(
            seed.Preference!, seed.Configuration, OptimizationRoute.OpenVino);
        OptimizationPresentationState gguf = OptimizationPresentationFactory.ReplanRequired(
            seed.Preference!, seed.Configuration, OptimizationRoute.Gguf);

        Assert.AreEqual("Import the model again", openVino.Title);
        Assert.AreEqual(
            "This setup could not be revalidated. If storage is low, free up disk space, then import the model again and repeat the checks. Keep the model folder in the same location.",
            openVino.Summary);
        Assert.AreEqual(1, openVino.Actions.Count);
        Assert.AreEqual(new OptimizationActionPresentation(
            OptimizationCommand.ImportAnotherModel, "Import model again", true), openVino.Actions[0]);
        Assert.AreEqual("Configuration needs another review", gguf.Title);
        Assert.AreEqual(seed.Summary, gguf.Summary);
        CollectionAssert.AreEqual(seed.Actions.ToArray(), gguf.Actions.ToArray());
        Assert.AreEqual(OptimizationCommand.BackToCompatibility, gguf.Actions.Single().Command);
        Assert.AreEqual(gguf.Kind, openVino.Kind);
        Assert.AreEqual(gguf.SupportCode, openVino.SupportCode);
        Assert.AreSame(seed.Configuration, openVino.Configuration);
    }

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
    public void GgufRuntimeOnlySuccessHasTruthfulCopyAndBundleSaveAction()
    {
        OptimizationPresentationState state = OptimizationFixtureCatalog.All.Single(
            static item => item.Id == "success-runtime-profile").Presentation;

        CollectionAssert.AreEqual(
            new[]
            {
                OptimizationCommand.Chat,
                OptimizationCommand.ImportAnotherModel,
                OptimizationCommand.Save
            },
            state.Actions.Select(static action => action.Command).ToArray());
        Assert.AreEqual("Your optimised setup is ready", state.Title);
        Assert.AreEqual(
            "The model weights are unchanged. The verified runtime settings optimise how this model runs.",
            state.Summary);
        Assert.AreEqual("Save model and settings", state.Actions.Single(
            static action => action.Command == OptimizationCommand.Save).Text);
        Assert.IsFalse(state.Configuration.ProducesPersistentArtifact);
    }

    [TestMethod]
    public void OpenVinoRuntimeOnlySuccessRetainsDoneAndExistingCopy()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            static item => item.Id == "success-runtime-profile").Presentation;
        OptimizationPresentationState state = OptimizationPresentationFactory.Success(
            seed.Preference!,
            seed.Configuration,
            route: OptimizationRoute.OpenVino);

        CollectionAssert.AreEqual(
            new[]
            {
                OptimizationCommand.Chat,
                OptimizationCommand.ImportAnotherModel,
                OptimizationCommand.Done
            },
            state.Actions.Select(static action => action.Command).ToArray());
        Assert.AreEqual(
            "This optimisation changes how the model runs; it does not create a new model file.",
            state.Summary);
        Assert.IsFalse(state.Configuration.ProducesPersistentArtifact);
    }

    [TestMethod]
    public void PersistentSuccessCopyAndSaveActionAreRouteIndependent()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            static item => item.Id == "success-persistent").Presentation;
        OptimizationPresentationState gguf = OptimizationPresentationFactory.Success(
            seed.Preference!, seed.Configuration, route: OptimizationRoute.Gguf);
        OptimizationPresentationState openVino = OptimizationPresentationFactory.Success(
            seed.Preference!, seed.Configuration, route: OptimizationRoute.OpenVino);

        Assert.AreEqual(openVino.Kind, gguf.Kind);
        Assert.AreEqual(openVino.Title, gguf.Title);
        Assert.AreEqual(openVino.Summary, gguf.Summary);
        Assert.AreEqual(openVino.Tone, gguf.Tone);
        Assert.AreEqual(openVino.Preference, gguf.Preference);
        Assert.AreEqual(openVino.PreferenceLabel, gguf.PreferenceLabel);
        Assert.AreEqual(openVino.Configuration, gguf.Configuration);
        CollectionAssert.AreEqual(
            openVino.ProgressRows.ToArray(), gguf.ProgressRows.ToArray());
        CollectionAssert.AreEqual(
            openVino.Actions.ToArray(), gguf.Actions.ToArray());
        Assert.AreEqual(openVino.CanCancel, gguf.CanCancel);
        Assert.AreEqual(openVino.SupportCode, gguf.SupportCode);
        Assert.AreEqual(openVino.OptimizationPlanId, gguf.OptimizationPlanId);
        Assert.AreEqual(openVino.ConfigurationSha256, gguf.ConfigurationSha256);
        Assert.AreEqual("Your optimised model is ready", gguf.Title);
        Assert.AreEqual("Save model to this computer", gguf.Actions.Single(
            static action => action.Command == OptimizationCommand.Save).Text);
    }

    [TestMethod]
    public void BothSuccessKindsOfferImportWithoutChangingThePrimaryChatAction()
    {
        foreach (OptimizationPresentationState state in OptimizationFixtureCatalog.All
            .Where(static item => item.Id is "success-persistent" or "success-runtime-profile")
            .Select(static item => item.Presentation))
        {
            OptimizationActionPresentation import = state.Actions.Single(
                static action => action.Command == OptimizationCommand.ImportAnotherModel);
            OptimizationActionPresentation chat = state.Actions.Single(
                static action => action.Command == OptimizationCommand.Chat);

            Assert.AreEqual("Import another model", import.Text);
            Assert.IsFalse(import.IsPrimary);
            Assert.IsTrue(chat.IsPrimary);
        }
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
