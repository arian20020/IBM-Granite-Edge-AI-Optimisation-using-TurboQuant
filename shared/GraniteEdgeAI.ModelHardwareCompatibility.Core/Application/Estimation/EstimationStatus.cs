namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// Whether a resource estimate could be produced at all.
/// </summary>
internal enum EstimationStatus
{
    Unspecified = 0,
    Established,
    NotEstablished
}
