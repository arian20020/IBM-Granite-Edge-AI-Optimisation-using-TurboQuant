using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Application;

public sealed record HardwareInspectionRunProgress
{
    public HardwareInspectionRunProgress(
        Guid inspectionId,
        long sequence,
        HardwareInspectionRunStage stage)
    {
        if (inspectionId == Guid.Empty)
        {
            throw new ArgumentException("Inspection identity cannot be empty.", nameof(inspectionId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        InspectionId = inspectionId;
        Sequence = sequence;
        Stage = stage;
    }

    public Guid InspectionId { get; }
    internal HardwareInspectionRunProgress(Guid inspectionId, long sequence, HardwareInspectionRunStage stage, int completedChecks, int totalChecks)
        : this(inspectionId, sequence, stage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completedChecks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalChecks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(completedChecks, totalChecks);
        CompletedChecks = completedChecks;
        TotalChecks = totalChecks;
    }

    internal int? CompletedChecks { get; }
    internal int? TotalChecks { get; }
    internal HardwareInspectionRunStage? CheckGroup { get; private init; }
    internal int? GroupCompleted { get; private init; }
    internal int? GroupTotal { get; private init; }
    internal HardwareInspectionRunProgress WithGroup(HardwareInspectionRunStage group, int completed, int total)
    {
        int expected = group == HardwareInspectionRunStage.ReadingProcessorInformation ? 1 : 2;
        if (group < HardwareInspectionRunStage.ReadingProcessorInformation || group > HardwareInspectionRunStage.CheckingLocalInferenceRuntimes ||
            total != expected || completed < 0 || completed > total) throw new ArgumentOutOfRangeException(nameof(group));
        return this with { CheckGroup = group, GroupCompleted = completed, GroupTotal = total };
    }
    public long Sequence { get; }
    public HardwareInspectionRunStage Stage { get; }
}
