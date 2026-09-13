using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

[Flags]
internal enum ModelInspectionPresentationRegions
{
    None = 0,
    Outcome = 1,
    Model = 2,
    Content = 4,
    Actions = 8,
    Footer = 16,
    ProgressRows = 32,
    LiveRegions = 64
}

internal sealed class ModelInspectionPresentationDelta
{
    private const ModelInspectionPresentationRegions AllRegions =
        ModelInspectionPresentationRegions.Outcome |
        ModelInspectionPresentationRegions.Model |
        ModelInspectionPresentationRegions.Content |
        ModelInspectionPresentationRegions.Actions |
        ModelInspectionPresentationRegions.Footer |
        ModelInspectionPresentationRegions.ProgressRows |
        ModelInspectionPresentationRegions.LiveRegions;

    internal ModelInspectionPresentationDelta(
        ModelInspectionRenderKey renderKey,
        ModelInspectionPagePresentation presentation,
        ModelInspectionPresentationRegions changedRegions,
        InspectionProgressRowsUpdate? progressRowsUpdate,
        ModelInspectionVisualOperationKey visualOperationKey)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.RenderKey != renderKey ||
            visualOperationKey.RenderKey != renderKey)
        {
            throw new ArgumentException(
                "Delta keys must identify the supplied presentation.",
                nameof(renderKey));
        }

        if (changedRegions == ModelInspectionPresentationRegions.None)
        {
            throw new ArgumentException(
                "An applied presentation must change at least one region.",
                nameof(changedRegions));
        }

        if ((changedRegions & ~AllRegions) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedRegions));
        }

        bool changesProgressRows = changedRegions.HasFlag(
            ModelInspectionPresentationRegions.ProgressRows);
        if (changesProgressRows != (progressRowsUpdate is not null))
        {
            throw new ArgumentException(
                "Progress-row changes and their update payload must agree.",
                nameof(progressRowsUpdate));
        }

        if (progressRowsUpdate is not null &&
            presentation.State != ModelInspectionFigmaState.InspectionProgress)
        {
            throw new ArgumentException(
                "Only an inspection-progress presentation may carry a progress update.",
                nameof(progressRowsUpdate));
        }

        if (progressRowsUpdate is not null &&
            (progressRowsUpdate.OwnerKey != renderKey ||
             progressRowsUpdate.OwnerKey != presentation.ProgressRowsUpdate.OwnerKey ||
             progressRowsUpdate.Key != presentation.ProgressRowsUpdate.Key ||
             !string.Equals(
                 progressRowsUpdate.ProgressSummary,
                 presentation.ProgressRowsUpdate.ProgressSummary,
                 StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "The progress update must match the presentation payload and render key.",
                nameof(progressRowsUpdate));
        }

        RenderKey = renderKey;
        Presentation = presentation;
        ChangedRegions = changedRegions;
        ProgressRowsUpdate = progressRowsUpdate;
        VisualOperationKey = visualOperationKey;
    }

    internal ModelInspectionRenderKey RenderKey { get; }

    internal ModelInspectionPagePresentation Presentation { get; }

    internal ModelInspectionPresentationRegions ChangedRegions { get; }

    internal InspectionProgressRowsUpdate? ProgressRowsUpdate { get; }

    internal ModelInspectionVisualOperationKey VisualOperationKey { get; }
}
