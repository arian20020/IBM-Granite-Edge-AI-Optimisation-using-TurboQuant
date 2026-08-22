namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Why a candidate never reached a mode's comparison. Stable codes, never free
/// text, so a disabled mode can explain itself without leaking anything.
/// </summary>
public enum ModeAdmissionReason
{
    None = 0,

    /// <summary>No estimate exists, so there is no requirement to compare.</summary>
    EstimateNotEstablished,

    /// <summary>The candidate does not fit, or fitting could not be established.</summary>
    FitStateNotSafeOrNarrow,

    /// <summary>
    /// The entry declares a quality, performance or stability threshold and no
    /// evidence exists to test it against.
    /// </summary>
    EvidenceBelowAdmissionLevel,

    /// <summary>
    /// The requested backend is not the one the candidate binds.
    ///
    /// Deferred, not forgotten: nothing produces this reason yet because nothing
    /// upstream of mode selection carries a <em>requested</em> backend to
    /// compare against — that arrives with the orchestrator (spec section 11's
    /// fourth hard gate). The member exists now so <see cref="ModeAdmission"/>
    /// and <see cref="ModeSelector"/> have somewhere to report it the day the
    /// orchestrator supplies one.
    /// </summary>
    BackendMismatch
}
