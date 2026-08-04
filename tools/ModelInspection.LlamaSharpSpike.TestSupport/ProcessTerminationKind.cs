namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Describes how a child feasibility process ended from the parent test's
/// perspective.
/// </summary>
public enum ProcessTerminationKind
{
    Exited,
    TimedOut,
    StartFailed
}
