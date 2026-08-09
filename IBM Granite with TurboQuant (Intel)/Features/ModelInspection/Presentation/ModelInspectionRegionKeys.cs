using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Globalization;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal readonly record struct ModelInspectionRegionKey
{
    private const int MaximumLength = 256;

    internal ModelInspectionRegionKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Region key cannot be blank.", nameof(value));
        }

        if (value.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Length,
                $"Region key cannot exceed {MaximumLength} UTF-16 code units.");
        }

        foreach (char codeUnit in value)
        {
            UnicodeCategory category = char.GetUnicodeCategory(codeUnit);
            if (category is UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or
                UnicodeCategory.PrivateUse or
                UnicodeCategory.OtherNotAssigned or
                UnicodeCategory.Surrogate)
            {
                throw new ArgumentException(
                    "Region key contains a disallowed character.",
                    nameof(value));
            }
        }

        Value = value;
    }

    internal string Value { get; }

    public override string ToString() => Value;
}

internal readonly record struct ModelInspectionProgressRegionKey
{
    internal ModelInspectionProgressRegionKey(
        ModelInspectionStage? stage,
        ModelInspectionStageStatus? stageStatus,
        int completedStageCount,
        int stageCount,
        double? stageFraction,
        string detail)
    {
        if (stage.HasValue && !Enum.IsDefined(stage.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        if (stageStatus.HasValue && !Enum.IsDefined(stageStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(stageStatus));
        }

        if (stage.HasValue != stageStatus.HasValue)
        {
            throw new ArgumentException(
                "Stage and stage status must either both be present or both be absent.",
                nameof(stageStatus));
        }

        if (stageCount < 0 || completedStageCount < 0 ||
            completedStageCount > stageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(completedStageCount));
        }

        if (stageFraction is double fraction &&
            (!double.IsFinite(fraction) || fraction < 0 || fraction > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(stageFraction));
        }

        ArgumentNullException.ThrowIfNull(detail);
        if (detail.Length > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(detail));
        }

        Stage = stage;
        StageStatus = stageStatus;
        CompletedStageCount = completedStageCount;
        StageCount = stageCount;
        StageFraction = stageFraction;
        Detail = detail;
    }

    internal ModelInspectionStage? Stage { get; }

    internal ModelInspectionStageStatus? StageStatus { get; }

    internal int CompletedStageCount { get; }

    internal int StageCount { get; }

    internal double? StageFraction { get; }

    internal string Detail { get; }
}

internal sealed record ModelInspectionRegionKeys(
    ModelInspectionRegionKey Outcome,
    ModelInspectionRegionKey Model,
    ModelInspectionRegionKey Content,
    ModelInspectionRegionKey Actions,
    ModelInspectionRegionKey Footer,
    ModelInspectionProgressRegionKey Progress,
    ModelInspectionRegionKey Announcements);
