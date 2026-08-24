namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Where a policy's numbers came from. This is what separates a documented
/// default from a fabricated constant: a provisional value is used, but it is
/// labelled, and it caps the evidence grade of every result built on it.
/// </summary>
internal enum PolicyProvenance
{
    Unspecified = 0,

    /// <summary>No values available. Evaluation must return NotEstablished.</summary>
    Absent,

    /// <summary>Documented defaults, not yet validated against measured runs.</summary>
    Provisional,

    /// <summary>Backed by a recorded predicted-versus-measured dataset.</summary>
    Calibrated
}
