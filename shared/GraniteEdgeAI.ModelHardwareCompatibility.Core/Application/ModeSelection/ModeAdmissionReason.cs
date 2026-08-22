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
    /// <summary>
    /// The candidate's backend is not the one that was asked for.
    ///
    /// Declared and never produced, and that is not an oversight. Spec section
    /// 11 lists "no requested-versus-actual backend mismatch" as one of four
    /// hard gates, but nothing can request a backend yet: the run request
    /// carries the user's context intent and nothing else, so there is no
    /// requested value for an actual one to differ from.
    ///
    /// Kept rather than deleted so the gate is visibly missing instead of
    /// invisibly absent. When a requested backend becomes an input, this is the
    /// reason that gate reports - and until then, nothing should be read into
    /// its silence, because it is not enforcing anything.
    /// </summary>
    BackendMismatch
}
