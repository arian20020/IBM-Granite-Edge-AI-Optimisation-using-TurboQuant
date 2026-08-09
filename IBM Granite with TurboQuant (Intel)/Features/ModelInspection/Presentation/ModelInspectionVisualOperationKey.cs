using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal readonly record struct ModelInspectionVisualOperationKey
{
    internal ModelInspectionVisualOperationKey(
        ModelInspectionRenderKey renderKey,
        long interactionRevision)
    {
        if (interactionRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interactionRevision));
        }

        RenderKey = renderKey;
        InteractionRevision = interactionRevision;
    }

    internal ModelInspectionRenderKey RenderKey { get; }

    internal long InteractionRevision { get; }
}
