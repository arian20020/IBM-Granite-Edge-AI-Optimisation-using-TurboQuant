using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityPresentationFactoryTests
{
    [TestMethod]
    public void OptimisationRequired_UsesApprovedCopyAndAllFrozenChoices()
    {
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(OptimizationScreen());

        Assert.AreEqual(
            "This model needs to be quantised to run on your computer",
            presentation.OutcomeTitle);
        Assert.AreEqual(
            "Your current model needs more memory than this computer can safely spare. A smaller version can run here.",
            presentation.OutcomeDetail);
        Assert.IsNotNull(presentation.Optimization);
        Assert.AreEqual(
            "You can choose how you want to balance memory use and expected quality.",
            presentation.Optimization.Instruction);
        CollectionAssert.AreEqual(
            new[]
            {
                "Automatic",
                "Maximum efficiency",
                "Efficient",
                "Balanced",
                "High capability",
                "Maximum capability"
            },
            presentation.Optimization.Modes.Select(mode => mode.Label).ToArray());
        Assert.IsTrue(presentation.Optimization.Modes.All(mode =>
            !string.IsNullOrWhiteSpace(mode.ExpectedQualityText)));
        Assert.AreEqual("Choose optimisation", presentation.PrimaryActionText);
        Assert.AreEqual("Back", presentation.SecondaryActionText);
        Assert.AreEqual("F16", presentation.Optimization.CurrentCacheFormat);
    }

    [TestMethod]
    public void Q2OnlyAndRequantisation_AreExplicitStrongWarnings()
    {
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(OptimizationScreen());

        CompatibilityOptimizationModePresentation q2 =
            presentation.Optimization!.Modes.Single(mode =>
                mode.WeightFormat == "Q2_K");

        Assert.IsTrue(q2.HasStrongQualityWarning);
        StringAssert.Contains(q2.WarningText, "significant quality reduction");
        StringAssert.Contains(q2.WarningText, "new copy");
        StringAssert.Contains(q2.WarningText, "original model remains unchanged");
    }

    [TestMethod]
    public void ManualSelection_UsesFlattenedModeWithoutRecalculating()
    {
        CompatibilityScreenModel screen = OptimizationScreen();

        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(
                screen,
                OptimizationPreferenceSelection.Manual(72));

        Assert.AreEqual(72, presentation.Optimization!.SliderValue);
        Assert.AreEqual("High capability", presentation.Optimization.SelectedMode.Label);
        Assert.IsFalse(presentation.Optimization.IsAutomatic);
        Assert.AreEqual("Q4_K_M", presentation.Optimization.SelectedMode.WeightFormat);
    }

    [TestMethod]
    public async Task FixtureOnlyOptimization_RemainsNonActionableWhenPreferenceChanges()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(OptimizationScreen()),
            continueDestinationAvailable: true);

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(72);

        Assert.AreEqual(OptimizationPreferenceKind.Manual,
            viewModel.SelectedPreference!.Kind);
        Assert.AreEqual(72, viewModel.SelectedPreference.PreferenceValue);
        Assert.IsFalse(viewModel.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public async Task ManualTicksWithinOneBand_UpdatePreferenceWithoutRepublishing()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(OptimizationScreen()),
            continueDestinationAvailable: true);
        await viewModel.StartAsync();
        int changes = 0;
        viewModel.PresentationChanged += (_, _) => changes++;

        viewModel.SelectManualPreference(21);
        CompatibilityPresentation firstBand = viewModel.Presentation;
        viewModel.SelectManualPreference(29);

        Assert.AreEqual(1, changes);
        Assert.AreSame(firstBand, viewModel.Presentation);
        Assert.AreEqual(29, viewModel.SelectedPreference!.PreferenceValue);

        viewModel.SelectManualPreference(41);
        Assert.AreEqual(2, changes);
        Assert.AreEqual("Balanced", viewModel.Presentation.Optimization!.SelectedMode.Label);
    }

    [TestMethod]
    public void SecondaryActionCopyAndKindStayInTheSameClosedStateMapping()
    {
        CompatibilityPresentation analysing =
            CompatibilityPresentationFactory.Analysing(0);
        Assert.AreEqual("Cancel", analysing.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Cancel,
            analysing.SecondaryActionKind);

        CompatibilityPresentation cancelled =
            CompatibilityPresentationFactory.Cancelled();
        Assert.AreEqual("Check again", cancelled.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Retry,
            cancelled.SecondaryActionKind);

        CompatibilityPresentation unanswered = CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [],
                [],
                BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false));
        Assert.AreEqual("Check again", unanswered.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Retry,
            unanswered.SecondaryActionKind);

        CompatibilityPresentation terminal = CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.EstimatedCompatible,
                [],
                [],
                BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: true));
        Assert.AreEqual("Back", terminal.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Back,
            terminal.SecondaryActionKind);
    }

    [TestMethod]
    public async Task SupersededOptimizationAttempt_CannotBecomePreferenceSource()
    {
        TaskCompletionSource<CompatibilityScreenModel> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        CompatibilityScreenModel newer = OptimizationScreen(WeightQuantisation.Q4_K_M);
        CompatibilityScreenModel older = OptimizationScreen(WeightQuantisation.Q5_K_M);
        int calls = 0;
        var viewModel = new CompatibilityViewModel(_ =>
            ++calls == 1 ? first.Task : Task.FromResult(newer));

        Task staleAttempt = viewModel.StartAsync();
        await viewModel.StartAsync();
        first.SetResult(older);
        await staleAttempt;

        viewModel.SelectManualPreference(72);
        Assert.AreEqual("Q4_K_M",
            viewModel.Presentation.Optimization!.CurrentWeightFormat);

        viewModel.SelectAutomaticPreference();
        Assert.AreEqual("Q4_K_M",
            viewModel.Presentation.Optimization!.CurrentWeightFormat);
    }

    [TestMethod]
    public void OpenVinoUnchangedWeight_WithoutExactCurrentPrecision_FailsClosed()
    {
        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            OpenVinoCacheOnlyScreen(WeightQuantisation.Unknown));

        Assert.IsNull(presentation.Optimization);
        Assert.AreEqual("We can't answer this yet", presentation.OutcomeTitle);
        Assert.IsFalse(presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public void OpenVinoUnchangedWeight_WithMismatchedCurrentRoute_FailsClosed()
    {
        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            OpenVinoCacheOnlyScreen(
                WeightQuantisation.Q8_0,
                RuntimeRouteId.LlamaCpp));

        Assert.IsNull(presentation.Optimization);
        Assert.AreEqual("We can't answer this yet", presentation.OutcomeTitle);
        Assert.IsFalse(presentation.PrimaryActionEnabled);
    }

    private static CompatibilityScreenModel OptimizationScreen(
        WeightQuantisation currentWeights = WeightQuantisation.Q4_K_M)
    {
        CompatibilityOptimizationModeView[] modes =
        [
            Mode(CompatibilityOptimizationLabelCode.Automatic, null,
                GgufWeightFormat.Q3KM, OptimizationAssessment.Good, false),
            Mode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10,
                GgufWeightFormat.Q2K, OptimizationAssessment.Poor, true),
            Mode(CompatibilityOptimizationLabelCode.Efficient, 30,
                GgufWeightFormat.Q3KM, OptimizationAssessment.Acceptable, true),
            Mode(CompatibilityOptimizationLabelCode.Balanced, 50,
                GgufWeightFormat.Q3KM, OptimizationAssessment.Good, false),
            Mode(CompatibilityOptimizationLabelCode.HighCapability, 70,
                GgufWeightFormat.Q4KM, OptimizationAssessment.Good, false),
            Mode(CompatibilityOptimizationLabelCode.MaximumCapability, 90,
                GgufWeightFormat.Q5KM, OptimizationAssessment.Excellent, false)
        ];

        CompatibilityOptimizationView optimization =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                modes,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None);

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.OptimisationRequired,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true,
            CurrentSetup(currentWeights),
            optimization);
    }

    private static CompatibilityOptimizationModeView Mode(
        CompatibilityOptimizationLabelCode label,
        int? slider,
        GgufWeightFormat weights,
        OptimizationAssessment quality,
        bool requantisation) =>
        CompatibilityOptimizationModeView.ForPresentation(
            label,
            slider,
            OptimizationRoute.Gguf,
            weights,
            GgufKvCacheFormat.Q8_0,
            null,
            null,
            DeviceRouteId.Cpu,
            quality,
            contextTokens: 4096,
            predictedPeakBytes: 3_221_225_472,
            safeBudgetBytes: 4_294_967_296,
            headroomBytes: 1_073_741_824,
            requiresPersistentArtifact: requantisation,
            requiresRequantisationAcknowledgement: requantisation,
            qualityNotice: requantisation
                ? OptimizationQualityNotice.SignificantQualityReduction
                : OptimizationQualityNotice.None,
            isExperimental: false,
            sharedWithAdjacentBand: false);

    private static CompatibilityScreenModel OpenVinoCacheOnlyScreen(
        WeightQuantisation currentWeights,
        RuntimeRouteId currentRoute = RuntimeRouteId.OpenVinoGenAi)
    {
        CompatibilityOptimizationLabelCode[] labels =
        [
            CompatibilityOptimizationLabelCode.Automatic,
            CompatibilityOptimizationLabelCode.MaximumEfficiency,
            CompatibilityOptimizationLabelCode.Efficient,
            CompatibilityOptimizationLabelCode.Balanced,
            CompatibilityOptimizationLabelCode.HighCapability,
            CompatibilityOptimizationLabelCode.MaximumCapability
        ];
        int?[] sliders = [null, 10, 30, 50, 70, 90];
        CompatibilityOptimizationModeView[] modes = labels
            .Select((label, index) => CompatibilityOptimizationModeView.ForPresentation(
                label,
                sliders[index],
                OptimizationRoute.OpenVino,
                null,
                null,
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U4,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Excellent,
                contextTokens: 4096,
                predictedPeakBytes: 3_221_225_472,
                safeBudgetBytes: 4_294_967_296,
                headroomBytes: 1_073_741_824,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                qualityNotice: OptimizationQualityNotice.None,
                isExperimental: false,
                sharedWithAdjacentBand: false))
            .ToArray();
        CompatibilityOptimizationView optimization =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                modes,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None);
        CompatibilitySetupView current = CompatibilitySetupView.ForPresentation(
            currentRoute,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            currentWeights,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: 5_368_709_120,
            safeBudgetBytes: 4_294_967_296,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: 268_435_456,
            isExperimental: false,
            requiresConversion: false,
            [],
            ggufKvCache: currentRoute == RuntimeRouteId.LlamaCpp
                ? GgufKvCacheFormat.F16
                : null,
            openVinoKvCache: currentRoute == RuntimeRouteId.OpenVinoGenAi
                ? OpenVinoKvCacheFormat.U8
                : null);

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.OptimisationRequired,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true,
            setup: current,
            optimization: optimization);
    }

    private static CompatibilitySetupView CurrentSetup(WeightQuantisation weights) =>
        CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            weights,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: 5_368_709_120,
            safeBudgetBytes: 4_294_967_296,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: 268_435_456,
            isExperimental: false,
            requiresConversion: false,
            [],
            ggufKvCache: GgufKvCacheFormat.F16);
}
