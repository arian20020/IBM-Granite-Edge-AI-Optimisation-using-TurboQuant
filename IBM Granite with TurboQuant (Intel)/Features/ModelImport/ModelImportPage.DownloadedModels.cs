using GraniteEdgeAI.Features.ModelImport.DownloadedModels;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                DownloadedModelDisplayNames = result.DisplayNames
                    .Select(static displayName => Path.GetFileName(displayName))
                    .Where(static displayName => !string.IsNullOrWhiteSpace(displayName))
                    .ToArray();
                UpdateDownloadedModelResults();
            }
        }
        catch (OperationCanceledException) when (searchCancellation.IsCancellationRequested)
        {
            // a cancelled or navigated-away page must expose no prior results
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

    private void CancelDownloadedModelSearch()
    {
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref _downloadedModelSearchCancellation, null);
        previous?.Cancel();
        DownloadedModelDisplayNames = Array.Empty<string>();
        HideDownloadedModelResults();
        _downloadedModelFinder.ClearResults();
    }

    private void HideDownloadedModelResults()
    {
        DownloadedModelsResults.ItemsSource = null;
        DownloadedModelsResults.Visibility = Visibility.Collapsed;
        DownloadedModelsSearchStatus.Text = string.Empty;
        DownloadedModelsResultsPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateDownloadedModelResults()
    {
        int resultCount = DownloadedModelDisplayNames.Count;
        DownloadedModelsResultsPanel.Visibility = Visibility.Visible;
        DownloadedModelsResults.ItemsSource = DownloadedModelDisplayNames;
        DownloadedModelsResults.Visibility = resultCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        DownloadedModelsSearchStatus.Text = resultCount switch
        {
            0 => "No downloaded models were found.",
            1 => "1 downloaded model found.",
            _ => $"{resultCount} downloaded models found."
        };
    }
}
