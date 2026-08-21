namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Why a candidate never reached a mode's comparison. Stable codes, never free
/// text, so a disabled mode can explain itself without leaking anything.
/// </summary>
internal enum ModeAdmissionReason
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

    /// <summary>The requested backend is not the one the candidate binds.</summary>
    BackendMismatch
}
