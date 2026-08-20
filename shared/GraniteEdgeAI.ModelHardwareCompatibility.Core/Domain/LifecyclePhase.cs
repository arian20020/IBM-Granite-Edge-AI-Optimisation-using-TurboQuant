namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// When a component occupies memory. Components that never coexist must not
/// be added together, so each declares the phases it is live in.
/// </summary>
internal enum LifecyclePhase
{
    Unspecified = 0,
    Load,
    Compile,
    SteadyStateGeneration
}
