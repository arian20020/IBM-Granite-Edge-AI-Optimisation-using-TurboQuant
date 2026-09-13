using GraniteEdgeAI.Features.HardwareInspection.Domain;
using System;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Application;

public sealed class HardwareInspectionRunResult
{
    private HardwareInspectionRunResult(
        Guid inspectionId,
        HardwareInspectionOutcome outcome,
        HardwareSnapshot? snapshot,
        HardwareInspectionFailureKind? failureKind,
        string? safeDiagnosticCode,
        HardwareInspectionHandoff? handoff)
    {
        InspectionId = inspectionId;
        Outcome = outcome;
        Snapshot = snapshot;
        FailureKind = failureKind;
        SafeDiagnosticCode = safeDiagnosticCode;
        Handoff = handoff;
    }

    public Guid InspectionId { get; }
    public HardwareInspectionOutcome Outcome { get; }
    public HardwareSnapshot? Snapshot { get; }
    public HardwareInspectionFailureKind? FailureKind { get; }
    public string? SafeDiagnosticCode { get; }
    public HardwareInspectionHandoff? Handoff { get; }

    public static HardwareInspectionRunResult CreateCompleted(
        Guid inspectionId,
        HardwareInspectionOutcome outcome,
        HardwareSnapshot snapshot)
    {
        RequireInspectionId(inspectionId);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (outcome is not HardwareInspectionOutcome.Completed
            and not HardwareInspectionOutcome.CompletedWithWarnings)
        {
            throw new ArgumentException(
                "Completed result requires a completed outcome.",
                nameof(outcome));
        }
        if (!Enum.IsDefined(snapshot.Usability)
            || snapshot.Usability == HardwareSnapshotUsability.NotUsable)
        {
            throw new ArgumentException(
                "Completed result requires a usable or display-only snapshot.",
                nameof(snapshot));
        }
        if (snapshot.Usability == HardwareSnapshotUsability.DisplayOnly
            && outcome != HardwareInspectionOutcome.CompletedWithWarnings)
        {
            throw new ArgumentException(
                "Display-only snapshot requires a warning outcome.",
                nameof(outcome));
        }

        HardwareInspectionHandoff? handoff = snapshot.Usability == HardwareSnapshotUsability.Usable
            ? HardwareInspectionHandoff.Create(inspectionId, outcome, snapshot)
            : null;
        return new HardwareInspectionRunResult(
            inspectionId,
            outcome,
            snapshot,
            null,
            null,
            handoff);
    }

    public static HardwareInspectionRunResult CreateFailed(
        Guid inspectionId,
        HardwareInspectionFailureKind failureKind,
        string safeDiagnosticCode)
    {
        RequireInspectionId(inspectionId);
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(safeDiagnosticCode);
        if (safeDiagnosticCode.Length > 64
            || safeDiagnosticCode.Any(character =>
                character is not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9')
                and not '-'))
        {
            throw new ArgumentException(
                "Diagnostic code must be a fixed uppercase token.",
                nameof(safeDiagnosticCode));
        }

        return new HardwareInspectionRunResult(
            inspectionId,
            HardwareInspectionOutcome.Failed,
            null,
            failureKind,
            safeDiagnosticCode,
            null);
    }

    public static HardwareInspectionRunResult CreateCancelled(Guid inspectionId)
    {
        RequireInspectionId(inspectionId);
        return new HardwareInspectionRunResult(
            inspectionId,
            HardwareInspectionOutcome.Cancelled,
            null,
            null,
            null,
            null);
    }

    private static void RequireInspectionId(Guid inspectionId)
    {
        if (inspectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inspection identity cannot be empty.",
                nameof(inspectionId));
        }
    }
}
