using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Projects one truthful application progress update into a bounded immutable
/// payload. It never mutates XAML-owned progress rows.
/// </summary>
internal static class InspectionProgressPresentationFactory
{
    internal static InspectionProgressRowsUpdate Create(
        ModelInspectionProgress progress,
        ModelInspectionRenderKey ownerKey)
    {
        ArgumentNullException.ThrowIfNull(progress);
        string safeDetail = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            progress.UserMessage,
            "Inspection progress updated.");
        ModelInspectionProgressRegionKey key = new(
            progress.Stage,
            progress.StageStatus,
            progress.CompletedStageCount,
            progress.TotalStageCount,
            progress.StageFraction,
            safeDetail);

        return new InspectionProgressRowsUpdate(
            key,
            ownerKey,
            $"{progress.CompletedStageCount} of " +
            $"{progress.TotalStageCount} checks complete");
    }
}
