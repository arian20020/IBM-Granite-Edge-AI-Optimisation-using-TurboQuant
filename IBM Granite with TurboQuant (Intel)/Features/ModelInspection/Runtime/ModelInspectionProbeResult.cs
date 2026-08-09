using System;
using GraniteEdgeAI.Features.ModelInspection.Contracts;

namespace GraniteEdgeAI.Features.ModelInspection.Runtime;

/// <summary>
/// Identifies the mutually exclusive terminal states returned by the runtime
/// probe before reliable evidence is classified into a model outcome.
/// </summary>
internal enum ModelInspectionProbeStatus
{
    Completed,
    Cancelled,
    OperationalFailure
}

/// <summary>
/// Carries either reliable application evidence, trusted cooperative
/// cancellation, or one application-safe operational failure.
/// </summary>
internal sealed record ModelInspectionProbeResult
{
    private ModelInspectionProbeResult(
        ModelInspectionProbeStatus status,
        ModelInspectionEvidence? evidence,
        ModelInspectionOperationalFailure? failure)
    {
        Status = status;
        Evidence = evidence;
        Failure = failure;
    }

    internal ModelInspectionProbeStatus Status { get; }

    internal ModelInspectionEvidence? Evidence { get; }

    internal ModelInspectionOperationalFailure? Failure { get; }

    internal static ModelInspectionProbeResult Completed(
        ModelInspectionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new ModelInspectionProbeResult(
            ModelInspectionProbeStatus.Completed,
            evidence,
            failure: null);
    }

    internal static ModelInspectionProbeResult Cancelled()
    {
        return new ModelInspectionProbeResult(
            ModelInspectionProbeStatus.Cancelled,
            evidence: null,
            failure: null);
    }

    internal static ModelInspectionProbeResult OperationalFailure(
        ModelInspectionOperationalFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ModelInspectionProbeResult(
            ModelInspectionProbeStatus.OperationalFailure,
            evidence: null,
            failure);
    }
}
