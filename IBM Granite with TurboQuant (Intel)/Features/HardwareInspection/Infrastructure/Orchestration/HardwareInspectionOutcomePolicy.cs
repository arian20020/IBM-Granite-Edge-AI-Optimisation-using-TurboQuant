using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal static class HardwareInspectionOutcomePolicy
{
    internal static HardwareInspectionRunResult FromResolution(
        Guid inspectionId,
        HardwareEvidenceResolutionResult resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.IsResolved)
        {
            return HardwareInspectionRunResult.CreateFailed(
                inspectionId,
                HardwareInspectionFailureKind.CriticalEvidence,
                "HI-EVIDENCE-UNRESOLVED");
        }

        HardwareInspectionOutcome outcome = resolution.Diagnostics.Count == 0
            ? HardwareInspectionOutcome.Completed
            : HardwareInspectionOutcome.CompletedWithWarnings;
        return HardwareInspectionRunResult.CreateCompleted(
            inspectionId,
            outcome,
            resolution.Snapshot!);
    }

    internal static HardwareInspectionRunResult FromAcquisitionFailure(
        Guid inspectionId,
        HardwareToolAcquisitionDiagnosticCode diagnostic) => diagnostic switch
        {
            HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable => RepairFailure(
                inspectionId,
                "HI-TOOL-NOT-AVAILABLE"),
            HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure => RepairFailure(
                inspectionId,
                "HI-TOOL-INTEGRITY"),
            HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable => RepairFailure(
                inspectionId,
                "HI-RUNTIME-PACKAGE"),
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic)),
        };

    internal static HardwareInspectionRunResult FromCollectionFailure(
        Guid inspectionId,
        HardwareEvidenceCollectionFailureCode failureCode) => failureCode switch
        {
            HardwareEvidenceCollectionFailureCode.ProviderUnavailable =>
                HardwareInspectionRunResult.CreateFailed(
                    inspectionId,
                    HardwareInspectionFailureKind.TransientOperation,
                    "HI-PROVIDER-UNAVAILABLE"),
            HardwareEvidenceCollectionFailureCode.OrchestrationFailure => RepairFailure(
                inspectionId,
                "HI-ORCHESTRATION-FAILED"),
            HardwareEvidenceCollectionFailureCode.ProgressCallbackFailure => RepairFailure(
                inspectionId,
                "HI-PROGRESS-CALLBACK"),
            _ => throw new ArgumentOutOfRangeException(nameof(failureCode)),
        };

    private static HardwareInspectionRunResult RepairFailure(
        Guid inspectionId,
        string diagnosticCode) => HardwareInspectionRunResult.CreateFailed(
            inspectionId,
            HardwareInspectionFailureKind.ApplicationRepairRequired,
            diagnosticCode);
}
