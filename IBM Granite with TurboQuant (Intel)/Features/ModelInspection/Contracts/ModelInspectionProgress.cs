using System;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Reports truthful application-level progress through the five lightweight
/// Model Inspection stages.
/// </summary>
internal sealed record ModelInspectionProgress
{
    private const int ExpectedStageCount = 5;

    /// <summary>
    /// creates one immutable progress update
    /// </summary>
    internal ModelInspectionProgress(
        ModelInspectionStage stage,
        ModelInspectionStageStatus stageStatus,
        int completedStageCount,
        int totalStageCount,
        double? stageFraction,
        string userMessage)
    {
        Stage = ModelInspectionContractValidation.RequireDefinedEnum(
            stage,
            nameof(stage));
        StageStatus = ModelInspectionContractValidation.RequireDefinedEnum(
            stageStatus,
            nameof(stageStatus));

        // The current Model Inspection workflow has exactly five stages.
        if (totalStageCount != ExpectedStageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalStageCount),
                totalStageCount,
                $"Total stage count must equal {ExpectedStageCount}.");
        }

        if (completedStageCount < 0 ||
            completedStageCount > totalStageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedStageCount),
                completedStageCount,
                "Completed stage count must be within the total stage range.");
        }

        int expectedCompletedStageCount = stageStatus is
            ModelInspectionStageStatus.Completed or
            ModelInspectionStageStatus.Warning
                ? (int)stage
                : (int)stage - 1;
        if (completedStageCount != expectedCompletedStageCount)
        {
            throw new ArgumentException(
                "Completed stage count must agree with stage and status.",
                nameof(completedStageCount));
        }

        // a fraction is nullable unless the runtime can measure genuine work
        if (stageFraction is double fraction &&
            (!double.IsFinite(fraction) || fraction < 0 || fraction > 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageFraction),
                stageFraction,
                "Stage fraction must be null or a finite value from zero to one.");
        }

        CompletedStageCount = completedStageCount;
        TotalStageCount = totalStageCount;
        StageFraction = stageFraction;
        UserMessage = ModelInspectionContractValidation.RequireText(
            userMessage,
            nameof(userMessage));
    }

    internal ModelInspectionStage Stage { get; }

    internal ModelInspectionStageStatus StageStatus { get; }

    internal int CompletedStageCount { get; }

    internal int TotalStageCount { get; }

    internal double? StageFraction { get; }

    internal string UserMessage { get; }
}
