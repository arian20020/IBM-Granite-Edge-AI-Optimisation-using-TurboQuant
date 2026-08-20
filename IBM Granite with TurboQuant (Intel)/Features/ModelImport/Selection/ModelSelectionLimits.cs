using System;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal static class ModelSelectionLimits
{
    internal const int MaximumDirectChildren = 512;
    internal const int MaximumRetainedNames = 32;
    internal const int MaximumMetadataBytes = 256 * 1024;
    internal static readonly TimeSpan MaximumElapsed = TimeSpan.FromSeconds(5);
}
