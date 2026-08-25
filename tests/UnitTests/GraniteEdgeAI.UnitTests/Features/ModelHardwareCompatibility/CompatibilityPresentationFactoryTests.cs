using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
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

    private static CompatibilityScreenModel OptimizationScreen()
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
            CurrentSetup(),
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

    private static CompatibilitySetupView CurrentSetup() =>
        CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.Q4_K_M,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: 5_368_709_120,
            safeBudgetBytes: 4_294_967_296,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: 268_435_456,
            isExperimental: false,
            requiresConversion: false,
            []);
}
