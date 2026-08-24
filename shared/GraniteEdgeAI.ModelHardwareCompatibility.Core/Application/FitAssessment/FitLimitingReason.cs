namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// The single fact that determined a non-safe result. Stable codes, never
/// free-form text, so presentation can explain the outcome without leaking
/// paths, native errors or tool output.
/// </summary>
internal enum FitLimitingReason
{
    None = 0,
    InsufficientSystemMemory,
    InsufficientDedicatedDeviceMemory,
    InsufficientStorage,
    ContextExceedsModelLimit,
    EvidenceBelowAdmissionLevel,
    SafetyPolicyUnavailable,
    FreshAvailabilityUnavailable
}
