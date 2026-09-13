namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

public enum OptimizationEvidenceModelFamily
{
    Unspecified = 0,
    Granite
}

public enum OptimizationEvidenceBackend
{
    Unspecified = 0,
    Cpu,
    Vulkan,
    Sycl,
    OpenVinoCpu,
    OpenVinoGpu,
    OpenVinoNpu
}

public enum OptimizationEvidenceDeviceClass
{
    Unspecified = 0,
    Cpu,
    IntelIntegratedGpu,
    IntelDiscreteGpu,
    IntelNpu
}

/// <summary>
/// Every dimension needed to distinguish one measured configuration from another.
/// Equality is intentionally ordinal and exact; the catalog performs no fuzzy fallback.
/// </summary>
public sealed record OptimizationEvidenceKey(
    OptimizationEvidenceModelFamily ModelFamily,
    string ModelIdentitySha256,
    ulong ParameterCount,
    OptimizationRoute Route,
    string RuntimePackageIdentity,
    string SourceWeightRepresentation,
    string TargetWeightRepresentation,
    string CacheConfiguration,
    OptimizationEvidenceBackend Backend,
    OptimizationEvidenceDeviceClass DeviceClass,
    int ContextTokens,
    string Workload,
    string MethodologyIdentity,
    string MemoryPerformanceProtocol,
    string ExecutionProfile);

/// <summary>Measured quality plus the non-negotiable execution-health gates.</summary>
public sealed record OptimizationEvidenceRecord(
    string EvidenceId,
    OptimizationEvidenceKey Key,
    OptimizationQualityScore Quality,
    bool OutputHealthPassed,
    bool StabilityPassed,
    bool ActivationPassed,
    bool IntegrityPassed)
{
    public bool IsAdmitted => Quality.Value >= 4m
        && OutputHealthPassed
        && StabilityPassed
        && ActivationPassed
        && IntegrityPassed;
}
