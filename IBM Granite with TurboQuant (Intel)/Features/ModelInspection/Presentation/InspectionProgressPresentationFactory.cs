using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Projects one truthful application progress update into the fixed five-row
/// Model Inspection tracker.
/// </summary>
internal static class InspectionProgressPresentationFactory
{
    /// <summary>
    /// Creates a complete progress-card snapshot without inferring fractional
    /// progress or backend work that the inspection service did not report.
    /// </summary>
    internal static InspectionContentCardPresentation Create(
        ModelInspectionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        InspectionContentCardPresentation initial =
            InitialInspectionProgressPresentationFactory.Create();
        int currentIndex = (int)progress.Stage - 1;
        List<InspectionContentItemPresentation> items =
            new(initial.Items.Count);

        for (int index = 0; index < initial.Items.Count; index++)
        {
            InspectionContentItemPresentation initialItem =
                initial.Items[index];

            items.Add(index switch
            {
                _ when index < currentIndex => CreateNonCurrentStage(
                    initialItem,
                    InspectionContentStatus.Passed,
                    "Passed"),
                _ when index > currentIndex => CreateNonCurrentStage(
                    initialItem,
                    InspectionContentStatus.Waiting,
                    "Waiting"),
                _ => CreateCurrentStage(initialItem, progress)
            });
        }

        return new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            SectionTitle = "Inspection progress",
            ProgressSummary =
                $"{progress.CompletedStageCount} of " +
                $"{progress.TotalStageCount} checks complete",
            Items = items
        };
    }

    private static InspectionContentItemPresentation CreateNonCurrentStage(
        InspectionContentItemPresentation source,
        InspectionContentStatus status,
        string statusText)
    {
        return new InspectionContentItemPresentation
        {
            StageNumber = source.StageNumber,
            Title = source.Title,
            Detail = source.Detail,
            DetailVisibility = Visibility.Collapsed,
            Status = status,
            StatusText = statusText,
            IsActive = false,
            StageFraction = null,
            ShowConnector = source.ShowConnector,
            AutomationName = $"{source.Title}. {statusText}."
        };
    }

    private static InspectionContentItemPresentation CreateCurrentStage(
        InspectionContentItemPresentation source,
        ModelInspectionProgress progress)
    {
        (InspectionContentStatus status, string statusText, bool isActive) =
            progress.StageStatus switch
            {
                ModelInspectionStageStatus.Active =>
                    (InspectionContentStatus.Active, "Checking", true),
                ModelInspectionStageStatus.Completed =>
                    (InspectionContentStatus.Passed, "Passed", false),
                ModelInspectionStageStatus.Warning =>
                    (InspectionContentStatus.Warning, "Warning", false),
                ModelInspectionStageStatus.Failed =>
                    (InspectionContentStatus.Error, "Failed", false),
                ModelInspectionStageStatus.Cancelled =>
                    (InspectionContentStatus.Information, "Cancelled", false),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(progress),
                    progress.StageStatus,
                    "Unknown Model Inspection stage status.")
            };

        return new InspectionContentItemPresentation
        {
            StageNumber = source.StageNumber,
            Title = source.Title,
            Detail = progress.UserMessage,
            DetailVisibility = Visibility.Visible,
            Status = status,
            StatusText = statusText,
            IsActive = isActive,
            StageFraction = progress.StageFraction,
            ShowConnector = source.ShowConnector,
            AutomationName =
                $"{source.Title}. {statusText}. {progress.UserMessage}"
        };
    }
}
