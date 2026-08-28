using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelDownloadCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_DisplaysBalancedPreference()
    {
        var card = new ModelDownloadCard();

        Assert.AreEqual(
            50d,
            ((Slider)card.FindName("ModelScaleSlider")).Value);
        Assert.AreEqual(
            "Balanced",
            ((TextBlock)card.FindName("ModelScaleValueText")).Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresTextForNarrowLayouts()
    {
        var card = new ModelDownloadCard();
        string[] wrappingTextNames =
        [
            "ModelNameText",
            "ModelPackageSummaryText",
            "ModelDescriptionText",
            "EstimatedMemoryValueText",
            "ContextLengthValueText",
            "EfficiencyEndpointText",
            "CapabilityEndpointText"
        ];

        foreach (string elementName in wrappingTextNames)
        {
            var text = (TextBlock)card.FindName(elementName);
            Assert.AreEqual(TextWrapping.WrapWholeWords, text.TextWrapping);
            Assert.AreEqual(TextTrimming.None, text.TextTrimming);
        }

        Assert.IsNotNull(card.FindName("ModelHeaderGrid"));
        Assert.IsNotNull(card.FindName("ModelFactsGrid"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PreferenceSlider_MapsEveryBoundaryToExpectedLabel()
    {
        var card = new ModelDownloadCard();
        var slider = (Slider)card.FindName("ModelScaleSlider");
        var label = (TextBlock)card.FindName("ModelScaleValueText");
        (double Value, string Label)[] expectations =
        [
            (0, "Maximum efficiency"),
            (20, "Efficient"),
            (40, "Balanced"),
            (60, "High capability"),
            (80, "Maximum capability"),
            (100, "Maximum capability")
        ];

        foreach ((double value, string expectedLabel) in expectations)
        {
            slider.Value = value;
            Assert.AreEqual(expectedLabel, label.Text);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_IsFailClosedAndExposesAccessibleOperationStates()
    {
        var card = new ModelDownloadCard();
        var start = (Button)card.FindName("DownloadModelButton");
        var cancel = (Button)card.FindName("CancelDownloadButton");
        var retry = (Button)card.FindName("RetryDownloadButton");
        var status = (TextBlock)card.FindName("DownloadStatusText");
        var progress = (ProgressBar)card.FindName("DownloadProgressBar");

        Assert.IsFalse(start.IsEnabled);
        Assert.AreEqual(
            "RecommendedModelDownload.Start",
            AutomationProperties.GetAutomationId(start));
        Assert.AreEqual(
            "RecommendedModelDownload.Cancel",
            AutomationProperties.GetAutomationId(cancel));
        Assert.AreEqual(
            "RecommendedModelDownload.Retry",
            AutomationProperties.GetAutomationId(retry));
        Assert.AreEqual(
            "RecommendedModelDownload.Progress",
            AutomationProperties.GetAutomationId(progress));
        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(status));
        Assert.AreEqual(Visibility.Collapsed, cancel.Visibility);
        Assert.AreEqual(Visibility.Collapsed, retry.Visibility);
        Assert.AreEqual(Visibility.Collapsed, progress.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ExplicitOfferAndServiceDriveSuccessWithoutDisplayingPath()
    {
        var service = new ImmediateDownloadService(
            ModelDownloadResult.Succeeded(Completion()));
        var card = new ModelDownloadCard();
        CompletedModelDownload? completed = null;
        card.VerifiedDownloadCompleted += (_, value) => completed = value;

        card.BindDownload(Offer(), service);
        bool accepted = await card.TryStartDownloadAsync();

        Assert.IsTrue(accepted);
        Assert.IsNotNull(completed);
        Assert.AreEqual(
            "Granite recommended model",
            ((TextBlock)card.FindName("ModelNameText")).Text);
        Assert.AreEqual(
            ModelDownloadStateKind.Succeeded,
            card.DownloadState.Kind);
        string status = ((TextBlock)card.FindName("DownloadStatusText")).Text;
        Assert.IsFalse(status.Contains(@"C:\", StringComparison.Ordinal));
        Assert.IsFalse(status.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(((Button)card.FindName("DownloadModelButton")).IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RunningStateRejectsDuplicateActivationAndAllowsCancellation()
    {
        var service = new BlockingDownloadService();
        var card = new ModelDownloadCard();
        card.BindDownload(Offer(), service);

        Task<bool> first = card.TryStartDownloadAsync();
        Assert.IsFalse(await card.TryStartDownloadAsync());
        Assert.IsFalse(((Button)card.FindName("DownloadModelButton")).IsEnabled);
        Assert.AreEqual(
            Visibility.Visible,
            ((Button)card.FindName("CancelDownloadButton")).Visibility);

        Assert.IsTrue(card.TryCancelDownload());
        Assert.IsTrue(await first);
        Assert.AreEqual(ModelDownloadStateKind.Cancelled, card.DownloadState.Kind);
        Assert.AreEqual(
            Visibility.Visible,
            ((Button)card.FindName("RetryDownloadButton")).Visibility);
    }

    private static RecommendedModelOffer Offer() => new(
        "granite-offer-1",
        "Granite recommended model",
        "1.94 GB download",
        "Verified instruction model",
        "GGUF",
        "Q4_K_M",
        "2.8 GB estimated memory",
        "128K tokens");

    private static CompletedModelDownload Completion() => new(
        new ModelSelectionInput(
            @"C:\private\granite.gguf", "granite.gguf", isFolder: false),
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        1024,
        "publication-1");

    private sealed class ImmediateDownloadService(ModelDownloadResult result)
        : IRecommendedModelDownloadService
    {
        public Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class BlockingDownloadService : IRecommendedModelDownloadService
    {
        public async Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new AssertFailedException("Cancellation was not observed.");
            }
            catch (OperationCanceledException)
            {
                return ModelDownloadResult.Cancelled();
            }
        }
    }
}
