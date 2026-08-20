using GraniteEdgeAI.Features.HardwareInspection.Domain;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Application;

public sealed class HardwareInspectionHandoff
{
    private HardwareInspectionHandoff(Guid inspectionId, HardwareSnapshot snapshot)
    {
        InspectionId = inspectionId;
        Snapshot = snapshot;
    }

    public Guid InspectionId { get; }
    public HardwareSnapshot Snapshot { get; }

    public static HardwareInspectionHandoff Create(
        Guid inspectionId,
        HardwareInspectionOutcome outcome,
        HardwareSnapshot snapshot)
    {
        if (inspectionId == Guid.Empty)
        {
            throw new ArgumentException("Inspection identity cannot be empty.", nameof(inspectionId));
        }

        ArgumentNullException.ThrowIfNull(snapshot);
        if (outcome is not HardwareInspectionOutcome.Completed
            and not HardwareInspectionOutcome.CompletedWithWarnings)
        {
            throw new ArgumentException("Only completed outcomes can create a handoff.", nameof(outcome));
        }

        if (snapshot.Usability != HardwareSnapshotUsability.Usable)
        {
            throw new ArgumentException("Handoff requires a usable snapshot.", nameof(snapshot));
        }

        return new HardwareInspectionHandoff(inspectionId, snapshot);
    }
}
