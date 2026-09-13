using GraniteEdgeAI.Features.ModelImport.Selection;
using System;

namespace GraniteEdgeAI.Features.ModelImport;

/// <summary>
/// immutable, path-private intent to continue a validated source-model route.
/// this is a notification only; it cannot start a converter or process
/// </summary>
internal sealed class SourceModelConversionRequestedEventArgs : EventArgs
{
    internal SourceModelConversionRequestedEventArgs(ModelSelectionResult selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!selection.IsAccepted || selection.Route != ModelSelectionRoute.SourceModelDirectory)
        {
            throw new ArgumentException(
                "A validated source-model selection is required.",
                nameof(selection));
        }

        Selection = selection;
    }

    internal ModelSelectionResult Selection { get; }
}
