using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.DownloadedModels;

internal interface IDownloadedModelFinder
{
    Task<DownloadedModelSearchResult> FindAsync(
        DownloadedModelSearchPolicy policy,
        CancellationToken cancellationToken);

    void ClearResults();
}

internal sealed record DownloadedModelSearchResult(IReadOnlyList<string> DisplayNames);
