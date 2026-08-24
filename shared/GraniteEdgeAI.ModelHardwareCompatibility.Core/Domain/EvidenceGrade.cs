namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// How much is actually known about a result, ordered weakest to strongest.
///
/// A provisional policy pins everything built on it to Estimated. Higher grades
/// become reachable only when a recorded measurement exists, which is what stops
/// a calculated number being presented as an observed one.
/// </summary>
public enum EvidenceGrade
{
    Unknown = 0,

    /// <summary>Calculated from documented defaults, not measured.</summary>
    Estimated,

    /// <summary>Backed by a recorded predicted-versus-measured dataset.</summary>
    Measured,

    /// <summary>Confirmed by a runtime verification run on this machine.</summary>
    Verified
}
