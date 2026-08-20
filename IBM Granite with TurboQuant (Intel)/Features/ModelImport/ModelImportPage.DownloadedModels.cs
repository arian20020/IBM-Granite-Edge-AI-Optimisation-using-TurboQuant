using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport;

public sealed partial class ModelImportPage
{
    private readonly IDownloadedModelFinder _downloadedModelFinder = new BoundedDownloadedModelFinder();
    private CancellationTokenSource? _downloadedModelSearchCancellation;

    internal IReadOnlyList<string> DownloadedModelDisplayNames { get; private set; } = Array.Empty<string>();

    internal async Task FindDownloadedModelsAsync(bool consent)
    {
        CancelDownloadedModelSearch();
        if (!consent)
        {
            return;
        }

        var searchCancellation = new CancellationTokenSource();
        _downloadedModelSearchCancellation = searchCancellation;
        try
        {
            DownloadedModelSearchResult result = await _downloadedModelFinder.FindAsync(
                DownloadedModelSearchPolicy.Default,
                searchCancellation.Token);
            if (ReferenceEquals(searchCancellation, _downloadedModelSearchCancellation) &&
                !searchCancellation.IsCancellationRequested)
            {
                DownloadedModelDisplayNames = result.DisplayNames;
            }
        }
        catch (OperationCanceledException) when (searchCancellation.IsCancellationRequested)
        {
            // A cancelled or navigated-away page must expose no prior results.
        }
        finally
        {
            if (ReferenceEquals(searchCancellation, _downloadedModelSearchCancellation))
            {
                _downloadedModelSearchCancellation = null;
            }
            searchCancellation.Dispose();
        }
    }

    private async void FindDownloadedModelsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DownloadedModelConsentDialog { XamlRoot = Content.XamlRoot };
        await FindDownloadedModelsAsync(await dialog.RequestConsentAsync());
    }

    private void CancelDownloadedModelSearch()
    {
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref _downloadedModelSearchCancellation, null);
        previous?.Cancel();
        DownloadedModelDisplayNames = Array.Empty<string>();
        _downloadedModelFinder.ClearResults();
    }
}
