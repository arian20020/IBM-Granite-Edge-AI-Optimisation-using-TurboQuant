namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;

/// <summary>
/// Whether a planning context could be established at all.
/// </summary>
internal enum PlanningContextStatus
{
    Unspecified = 0,
    Resolved,
    NotEstablished
}
