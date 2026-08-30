using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportPageCompositionTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresHeadingForNarrowLayouts()
    {
        var page = new ModelImportPage();
        var heading = page.FindName("ImportPageHeading") as TextBlock;

        Assert.IsNotNull(heading);
        Assert.AreEqual(
            HorizontalAlignment.Stretch,
            heading.HorizontalAlignment);
        Assert.AreEqual(TextWrapping.WrapWholeWords, heading.TextWrapping);
        Assert.AreEqual(TextTrimming.None, heading.TextTrimming);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_EmbedsRecommendedModelCardWithoutChangingReadiness()
    {
        var page = new ModelImportPage();

        var downloadCard = page.FindName("RecommendedModelDownloadCard")
            as ModelDownloadCard;
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsNotNull(downloadCard);
        Assert.AreEqual(
            "Recommended model download option",
            AutomationProperties.GetName(downloadCard));
        Assert.IsNull(page.FindName("RecommendedModelDownloadButton"));
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(continueButton.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EmbeddedCardInteraction_DoesNotChangeLocalModelReadiness()
    {
        var page = new ModelImportPage();
        var downloadCard = (ModelDownloadCard)page.FindName(
            "RecommendedModelDownloadCard");
        var slider = (Slider)downloadCard.FindName("ModelScaleSlider");

        slider.Value = 85;

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.ValidatedScanResult);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DownloadedModelSearch_RendersFilenameOnlyResults()
    {
        var page = CreatePage(new StubDownloadedModelFinder(@"C:\Models\granite.gguf"));

        await page.FindDownloadedModelsAsync(consent: true);

        var results = page.FindName("DownloadedModelsResults") as ListView;
        var status = page.FindName("DownloadedModelsSearchStatus") as TextBlock;

        Assert.IsNotNull(results);
        Assert.IsNotNull(status);
        Assert.AreEqual(Visibility.Visible, results.Visibility);
        Assert.AreEqual("1 downloaded model found.", status.Text);
        Assert.AreEqual("granite.gguf", results.Items[0]);
        Assert.IsFalse(results.Items[0]?.ToString()?.Contains(@"C:\", StringComparison.Ordinal) ?? true);
        Assert.IsFalse(results.IsItemClickEnabled);
        Assert.AreEqual(ListViewSelectionMode.None, results.SelectionMode);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DownloadedModelSearch_RendersNoResultsWithoutPaths()
    {
        var page = CreatePage(new StubDownloadedModelFinder());

        await page.FindDownloadedModelsAsync(consent: true);

        var results = page.FindName("DownloadedModelsResults") as ListView;
        var status = page.FindName("DownloadedModelsSearchStatus") as TextBlock;

        Assert.IsNotNull(results);
        Assert.IsNotNull(status);
        Assert.AreEqual(Visibility.Collapsed, results.Visibility);
        Assert.AreEqual("No downloaded models were found.", status.Text);
        Assert.IsFalse(status.Text.Contains(@"C:\", StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DownloadedModelSearch_DeclinedConsentHidesPriorResults()
    {
        var page = CreatePage(new StubDownloadedModelFinder("granite.gguf"));
        await page.FindDownloadedModelsAsync(consent: true);

        await page.FindDownloadedModelsAsync(consent: false);

        var panel = page.FindName("DownloadedModelsResultsPanel") as StackPanel;
        var results = page.FindName("DownloadedModelsResults") as ListView;

        Assert.IsNotNull(panel);
        Assert.IsNotNull(results);
        Assert.AreEqual(Visibility.Collapsed, panel.Visibility);
        Assert.AreEqual(0, results.Items.Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PickerCompletionAfterNavigationRetirement_CannotRestoreSelection()
    {
        var pickerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePicker = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            async () =>
            {
                pickerStarted.TrySetResult();
                return await releasePicker.Task;
            },
            (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateSuccess(
                "Granite",
                "granite",
                "3B",
                "Q4_K_M",
                4,
                4096,
                3,
                DateTimeOffset.UnixEpoch)),
            classifier: new AcceptedGgufClassifier());
        Task browsing = page.BrowseFilesAsync();
        await pickerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        page.RetireSelectionForNavigation();
        releasePicker.TrySetResult(@"C:\private\late.gguf");
        await browsing.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.CurrentRoute);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetireForNavigationAsync_ReturnsOneStableTaskAndDisablesDownloadActions()
    {
        var page = new ModelImportPage();
        var card = (ModelDownloadCard)page.FindName("RecommendedModelDownloadCard");
        var download = (Button)card.FindName("DownloadModelButton");

        Task first = page.RetireForNavigationAsync();
        Task second = page.RetireForNavigationAsync();
        await first;

        Assert.AreSame(first, second);
        Assert.IsFalse(download.IsEnabled);
    }

    private static ModelImportPage CreatePage(IDownloadedModelFinder finder) => new(
        () => Task.FromResult(ModelFormatSelection.None),
        () => Task.FromResult<string?>(null),
        (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateCancelled()),
        downloadedModelFinder: finder);

    private sealed class StubDownloadedModelFinder : IDownloadedModelFinder
    {
        private readonly IReadOnlyList<string> _displayNames;

        internal StubDownloadedModelFinder(params string[] displayNames)
        {
            _displayNames = displayNames;
        }

        public Task<DownloadedModelSearchResult> FindAsync(
            DownloadedModelSearchPolicy policy,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DownloadedModelSearchResult(_displayNames));

        public void ClearResults() { }
    }

    private sealed class AcceptedGgufClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken token) =>
            Task.FromResult(ModelSelectionResult.Accepted(
                id,
                ModelSelectionRoute.Gguf,
                input.DisplayName));
    }
}
