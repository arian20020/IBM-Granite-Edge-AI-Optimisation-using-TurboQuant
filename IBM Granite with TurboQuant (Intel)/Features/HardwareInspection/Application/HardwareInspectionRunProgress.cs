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
    public long Sequence { get; }
    public HardwareInspectionRunStage Stage { get; }
}
