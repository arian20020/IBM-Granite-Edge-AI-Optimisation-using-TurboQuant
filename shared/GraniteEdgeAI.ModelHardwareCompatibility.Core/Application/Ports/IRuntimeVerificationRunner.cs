using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// The outcome of a runtime verification attempt. Built only through the two
/// factories, so an unestablished outcome can never also claim success.
/// </summary>
internal sealed record VerificationOutcome
{
    private VerificationOutcome(
        bool isEstablished, bool succeeded, PortUnavailableReason reason)
    {
        IsEstablished = isEstablished;
        Succeeded = succeeded;
        Reason = reason;
    }

    internal bool IsEstablished { get; }

    /// <summary>Meaningful only when <see cref="IsEstablished"/> is true.</summary>
    internal bool Succeeded { get; }

    internal PortUnavailableReason Reason { get; }

    internal static VerificationOutcome Established(bool succeeded) =>
        new(true, succeeded, PortUnavailableReason.None);

    internal static VerificationOutcome Unavailable(PortUnavailableReason reason)
    {
        if (reason == PortUnavailableReason.None)
        {
            throw new ArgumentException(
                "An unavailable outcome must name why.", nameof(reason));
        }

        return new VerificationOutcome(false, false, reason);
    }
}

/// <summary>
/// Runs the verification screens. Implemented by the runtime teams, never by C1 —
/// this feature selects a plan, it does not execute one.
/// </summary>
internal interface IRuntimeVerificationRunner
{
    VerificationOutcome Verify(CompatibilityRunId runId);
}
