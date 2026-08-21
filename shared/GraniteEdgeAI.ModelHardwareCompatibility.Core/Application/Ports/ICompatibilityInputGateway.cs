using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>The outcome of atomically claiming the paired handoffs.</summary>
internal sealed record HandoffClaim(
    bool IsClaimed,
    string ModelInspectionRunId,
    string ProductHardwareRunId,
    PortUnavailableReason Reason);

/// <summary>
/// Owns which handoffs are current, claims them atomically, rolls back, and
/// commits exactly one transfer when the user confirms.
///
/// The claim is atomic because a run that read one handoff and then had the other
/// replaced underneath it would produce an assessment about two different states
/// of the world.
/// </summary>
internal interface ICompatibilityInputGateway
{
    HandoffClaim Claim();

    /// <summary>Safe to call whether or not a claim succeeded.</summary>
    void Rollback();

    bool Commit(CompatibilityRunId runId);
}
