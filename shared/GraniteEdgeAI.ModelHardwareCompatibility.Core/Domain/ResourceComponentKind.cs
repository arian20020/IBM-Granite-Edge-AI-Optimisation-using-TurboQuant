namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// What a component represents. Each mandatory kind has exactly one owning
/// provider so a requirement can never be counted twice.
/// </summary>
internal enum ResourceComponentKind
{
    Unspecified = 0,
    Weights,
    KvCache,
    ComputeBuffer,
    BackendAllocation,
    StagingBuffer,
    ModelState,
    ApplicationOverhead,
    PersistentArtifact
}
