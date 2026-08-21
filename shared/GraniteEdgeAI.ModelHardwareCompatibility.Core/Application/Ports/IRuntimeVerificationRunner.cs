using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>The outcome of a runtime verification attempt.</summary>
internal sealed record VerificationOutcome(
    bool IsEstablished,
    bool Succeeded,
    PortUnavailableReason Reason);

/// <summary>
/// Runs the verification screens. Implemented by the runtime teams, never by C1 —
/// this feature selects a plan, it does not execute one.
/// </summary>
internal interface IRuntimeVerificationRunner
{
    VerificationOutcome Verify(CompatibilityRunId runId);
}
