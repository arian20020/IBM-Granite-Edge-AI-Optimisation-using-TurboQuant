using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Carries only bounded display-safe progress semantics across the pure
/// presentation-factory boundary. It never retains a raw worker message.
/// </summary>
internal sealed record InspectionProgressRowsUpdate
{
    internal InspectionProgressRowsUpdate(
        ModelInspectionProgressRegionKey key,
        ModelInspectionRenderKey ownerKey,
        string progressSummary)
    {
        ArgumentNullException.ThrowIfNull(progressSummary);
        string safeSummary = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            progressSummary,
            "0 of 5 checks complete");
        if (!string.Equals(
                safeSummary,
                progressSummary,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Progress summary must already be bounded display text.",
                nameof(progressSummary));
        }

        string safeDetail = key.Detail.Length == 0
            ? string.Empty
            : ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                key.Detail,
                "Inspection progress updated.");
        Key = new ModelInspectionProgressRegionKey(
            key.Stage,
            key.StageStatus,
            key.CompletedStageCount,
            key.StageCount,
            key.StageFraction,
            safeDetail);
        OwnerKey = ownerKey;
        ProgressSummary = safeSummary;
    }

    internal ModelInspectionProgressRegionKey Key { get; }

    internal ModelInspectionRenderKey OwnerKey { get; }

    internal string ProgressSummary { get; }
}
