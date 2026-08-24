namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// Why a seam produced nothing. Stable codes, never text — a reason reaches the
/// user through presentation, and section 14 forbids a provider payload getting
/// that far.
/// </summary>
internal enum PortUnavailableReason
{
    None = 0,

    /// <summary>
    /// No adapter exists yet. This is the shipping state until the owner
    /// contracts are published, and it is why the production page reports that
    /// no compatibility conclusion can be drawn.
    /// </summary>
    AdapterNotImplemented,

    HandoffUnavailable,
    HandoffStale,
    IdentityMismatch,
    RunnerNotRegistered
}
