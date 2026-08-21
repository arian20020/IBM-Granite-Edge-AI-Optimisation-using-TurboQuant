namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Whether a mode could pick anything. A mode is never silently missing: it is
/// available with a candidate, unavailable with a reason, or not established.
/// </summary>
internal enum ModeAvailability
{
    NotEstablished = 0,
    Available,
    Unavailable
}
