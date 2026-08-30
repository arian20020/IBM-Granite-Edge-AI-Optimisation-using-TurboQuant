using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
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
    public void PreferenceSlider_UpdatesPinnedArtifactDetails()
    {
        var card = new ModelDownloadCard();
        var slider = (Slider)card.FindName("ModelScaleSlider");
        var quantisation = (TextBlock)card.FindName("QuantizationValueText");
        var summary = (TextBlock)card.FindName("ModelPackageSummaryText");

        slider.Value = 0;
        Assert.AreEqual("Q2_K", quantisation.Text);
        Assert.IsTrue(summary.Text.Contains("1.23 GB", StringComparison.Ordinal));

        slider.Value = 100;
        Assert.AreEqual("Q8_0", quantisation.Text);
        Assert.IsTrue(summary.Text.Contains("3.40 GB", StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ProvidesAccessibleDownloadStatusControls()
    {
        var card = new ModelDownloadCard();

        Assert.IsNotNull(card.FindName("DownloadStatusRegion"));
        Assert.IsNotNull(card.FindName("DownloadStatusText"));
        Assert.IsNotNull(card.FindName("DownloadProgressBar"));
        Assert.IsNotNull(card.FindName("DownloadProgressText"));
        Assert.IsNotNull(card.FindName("DownloadActivityRing"));
        Assert.IsNotNull(card.FindName("DiscardDownloadButton"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DeterminateDownloadHidesIndeterminateRingAndNamesCancelAction()
    {
        var service = new BlockingDownloadService();
        var coordinator = new ModelDownloadCoordinator(
            service,
            new UnrestrictedNetworkPolicy());
        var card = new ModelDownloadCard();
        card.Attach(coordinator);

        Task operation = coordinator.StartAsync(
            50,
            allowMetered: false,
            CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await DrainDispatcherAsync(card);

        var ring = (ProgressRing)card.FindName("DownloadActivityRing");
        var button = (Button)card.FindName("DownloadModelButton");
        Assert.IsFalse(ring.IsActive);
        Assert.AreEqual(Visibility.Collapsed, ring.Visibility);
        Assert.AreEqual("Cancel the model download", AutomationProperties.GetName(button));

        await coordinator.CancelAsync(false, CancellationToken.None);
        await operation;
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PrimaryCancelPreservesResumablePartialForRetry()
    {
        var service = new BlockingDownloadService();
        var coordinator = new ModelDownloadCoordinator(service, new UnrestrictedNetworkPolicy());
        var card = new ModelDownloadCard();
        card.Attach(coordinator);
        Task operation = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await DrainDispatcherAsync(card);

        var button = (Button)card.FindName("DownloadModelButton");
        var peer = new ButtonAutomationPeer(button);
        var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!;
        invoke.Invoke();
        await operation.WaitAsync(TimeSpan.FromSeconds(2));
        await DrainDispatcherAsync(card);

        Assert.AreEqual(0, service.DiscardCalls);
        Assert.AreEqual(ModelDownloadStage.Interrupted, coordinator.State.Stage);
        Assert.AreEqual(100, coordinator.State.DownloadedBytes);
    }

    [UITestMethod]
    public async Task CleanupTimeoutDisablesUnavailableRetryAction()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new NeverSettlingCancellationService(release);
        var coordinator = new ModelDownloadCoordinator(
            service,
            new UnrestrictedNetworkPolicy(),
            TimeSpan.FromMilliseconds(50));
        var card = new ModelDownloadCard();
        card.Attach(coordinator);
        _ = coordinator.StartAsync(50, false, CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await coordinator.CancelAsync(false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
            await DrainDispatcherAsync(card);
            var button = (Button)card.FindName("DownloadModelButton");
            Assert.IsFalse(button.IsEnabled);
            Assert.AreEqual("Cleanup pending", button.Content);
            Assert.AreEqual("Model download cleanup in progress", AutomationProperties.GetName(button));
        }
        finally { release.Set(); }
    }

    [UITestMethod]
    public void TerminalCleanupFailureIsNotPresentedAsIntegrityFailure()
    {
        var card = new ModelDownloadCard();
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(50);
        var state = new ModelDownloadCoordinatorState(
            ModelDownloadOperationId.CreateNew(),
            ModelDownloadStage.Failed,
            entry.PreferenceLabel,
            entry.Quantisation,
            entry.DownloadSizeText,
            100,
            entry.ExpectedByteLength,
            "download-cancellation-cleanup-failed");
        typeof(ModelDownloadCard).GetMethod(
            "Render",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(card, [state]);

        var status = (TextBlock)card.FindName("DownloadStatusText");
        var button = (Button)card.FindName("DownloadModelButton");
        StringAssert.Contains(status.Text, "cleanup could not be confirmed");
        Assert.IsFalse(status.Text.Contains("integrity", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("Try again", button.Content);
        Assert.IsTrue(button.IsEnabled);
        Assert.AreEqual(
            "Retry the model download after a cleanup failure",
            AutomationProperties.GetName(button));
    }

    private static async Task DrainDispatcherAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(() => drained.TrySetResult(true)));
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class UnrestrictedNetworkPolicy : IModelDownloadNetworkPolicy
    {
        public ModelDownloadConnectionKind GetCurrentConnectionKind() => ModelDownloadConnectionKind.Unrestricted;
    }

    private sealed class BlockingDownloadService : IModelDownloadService
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int DiscardCalls { get; private set; }

        public async Task<ModelDownloadResult> DownloadAsync(
            ModelDownloadCatalogEntry entry,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            progress.Report(new(ModelDownloadStage.Downloading, 100, entry.ExpectedByteLength));
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new(ModelDownloadResultKind.Interrupted, null, "download-cancelled");
            }
            throw new InvalidOperationException("The blocking service must be cancelled.");
        }

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken) => Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(
            ModelDownloadCatalogEntry entry,
            CancellationToken cancellationToken)
        {
            DiscardCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class NeverSettlingCancellationService(ManualResetEventSlim release) : IModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ModelDownloadResult> DownloadAsync(ModelDownloadCatalogEntry entry, IProgress<ModelDownloadProgress> progress, CancellationToken cancellationToken)
        {
            cancellationToken.Register(release.Wait);
            Started.TrySetResult();
            return _never.Task;
        }

        public Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResumeInfo?>(null);

        public Task DiscardPartialAsync(ModelDownloadCatalogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
