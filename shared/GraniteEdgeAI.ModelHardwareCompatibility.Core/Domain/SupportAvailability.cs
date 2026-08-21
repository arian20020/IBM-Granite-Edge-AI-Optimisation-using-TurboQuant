namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// The resolved answer to "may this configuration be offered?". Unsupported is
/// the zero value so that a value which was never resolved cannot read as
/// available.
/// </summary>
internal enum SupportAvailability
{
    Unsupported = 0,
    Unavailable,
    Available,
    ExperimentalAvailable
}
