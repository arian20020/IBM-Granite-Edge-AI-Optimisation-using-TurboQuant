namespace GraniteEdgeAI.Features.HardwareInspection.Domain;

public enum NpuDetectionState
{
    Present,
    NotPresent,
    DetectionUnavailable,
}

public enum HardwareSnapshotUsability
{
    Usable,
    DisplayOnly,
    NotUsable,
}

public enum EvidenceSourceKind
{
    LlmFit,
    Windows,
    Dxgi,
    NeuralProcessorProbe,
    LlamaCpp,
}

public enum EvidenceResolutionState
{
    ResolvedPrimary,
    ResolvedCorroborated,
    ResolvedFallback,
    Conflict,
    Unavailable,
}

public enum EvidenceConfidence
{
    High,
    Medium,
    Low,
}

public enum LocalRuntimeBackend
{
    Cpu,
    Sycl,
    Vulkan,
}
