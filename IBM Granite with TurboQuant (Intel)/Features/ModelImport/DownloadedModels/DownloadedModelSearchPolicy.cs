using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelImport.DownloadedModels;

internal enum KnownFolderId
{
    Downloads,
    Documents,
    Desktop
}

internal sealed record DownloadedModelSearchPolicy(
    IReadOnlyList<KnownFolderId> AllowedFolders,
    int MaximumLocations,
    int MaximumVisitedItems,
    TimeSpan MaximumElapsed)
{
    internal static DownloadedModelSearchPolicy Default { get; } = new(
        [KnownFolderId.Downloads, KnownFolderId.Documents, KnownFolderId.Desktop],
        MaximumLocations: 3,
        MaximumVisitedItems: 250,
        MaximumElapsed: TimeSpan.FromSeconds(10));
}
