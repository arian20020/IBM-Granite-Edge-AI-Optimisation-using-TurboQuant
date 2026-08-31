using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;

internal sealed record OptimizationFixture(
    string Id,
    string Title,
    OptimizationPresentationState Presentation);

internal static class OptimizationFixtureCatalog
{
    private static readonly OptimizationPreferenceSelection Automatic =
        OptimizationPreferenceSelection.Automatic();

    private static readonly OptimizationPreferenceSelection Manual =
        OptimizationPreferenceSelection.Manual(52);

    internal static IReadOnlyList<OptimizationFixture> All { get; } =
    [
        new("confirmation", "Review configuration", Confirmation()),
        Progress("progress-preflight", OptimizationStage.Preflight),
        Progress("progress-staging", OptimizationStage.PrepareStaging),
        Progress("progress-optimise", OptimizationStage.Optimise),
        Progress("progress-validate", OptimizationStage.Validate),
        Progress("progress-smoke", OptimizationStage.SmokeTest),
        Progress("progress-reinspect", OptimizationStage.Reinspect),
        Progress("progress-publish", OptimizationStage.Publish),
        new("cancelled", "Cancelled", OptimizationPresentationFactory.Cancelled(Manual, Persistent())),
        new("replan-required", "Replan required", OptimizationPresentationFactory.ReplanRequired(Manual, Persistent())),
        new("failed", "Failed", OptimizationPresentationFactory.Failed(Manual, Persistent())),
        new("success-persistent", "Persistent success", OptimizationPresentationFactory.Success(Manual, Persistent())),
        new("success-runtime-profile", "Runtime-only success", OptimizationPresentationFactory.Success(Automatic, RuntimeOnly()))
    ];

    private static OptimizationConfigurationPresentation Persistent() =>
        new(
            "Q5 K medium",
            "Q8",
            "llama.cpp",
            "Intel integrated graphics and CPU",
            "16,384 tokens",
            "Most layers on the integrated GPU",
            "On",
            "4.4 GB",
            "1.1 GB",
            "0.8 GB",
            "6.3 GB",
            "8.0 GB",
            "1.7 GB",
            "Verified for this backend and device",
            "Keeps high capability while leaving working memory free.",
            "Longer conversations will use more cache memory.",
            true,
            "GGUF model and validation manifest",
            "9.2 GB",
            "5.1 GB",
            "Validate, smoke test, and reinspect the new model");

    private static OptimizationConfigurationPresentation RuntimeOnly() =>
        Persistent() with
        {
            ProducesPersistentArtifact = false,
            Output = "Validated runtime profile",
            WorkingDisk = "No conversion workspace needed",
            FinalDisk = "Small setup document",
            RouteValidation = "Validate the profile and run a bounded smoke test"
        };

    private static OptimizationPresentationState Confirmation() =>
        OptimizationPresentationFactory.Confirmation(Manual, Persistent());

    private static OptimizationFixture Progress(string id, OptimizationStage stage) =>
        new(id, $"Progress: {stage}", OptimizationPresentationFactory.Running(Manual, Persistent(), stage));
}

internal sealed class OptimizationFixtureGalleryPage : Page
{
    internal OptimizationFixtureGalleryPage()
    {
        RequestedTheme = ElementTheme.Light;
        PreviewPage = new OptimizationPage();
        PreviewPage.ApplyPresentation(OptimizationFixtureCatalog.All[0].Presentation);
        Content = PreviewPage;
    }

    private OptimizationPage PreviewPage { get; }

    internal int FixtureCount => OptimizationFixtureCatalog.All.Count;

    internal OptimizationPresentationState? PreviewPresentation =>
        PreviewPage.Presentation;
}
