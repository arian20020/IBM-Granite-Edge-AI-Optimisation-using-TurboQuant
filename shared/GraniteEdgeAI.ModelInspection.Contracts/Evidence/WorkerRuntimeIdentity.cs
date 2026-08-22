namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Records the exact managed and native runtime identity used for inspection.
/// </summary>
public sealed record WorkerRuntimeIdentity
{
    public string WorkerVersion { get; init; } = string.Empty;

    public int ProtocolVersion { get; init; }

    public string RuntimeProfile { get; init; } = string.Empty;

    public string LLamaSharpVersion { get; init; } = string.Empty;

    public string BackendPackageVersion { get; init; } = string.Empty;

    public string MappedLlamaCppCommit { get; init; } = string.Empty;

    public string NativeLibraryName { get; init; } = string.Empty;

    public string ProcessArchitecture { get; init; } = string.Empty;

    public string InspectionMode { get; init; } = string.Empty;

    public bool UsesCuda { get; init; }

    public bool UsesVulkan { get; init; }

    public int GpuLayerCount { get; init; }

    /// <summary>
    /// Verifies the first approved x64 CPU-only runtime profile.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(WorkerVersion),
            nameof(WorkerVersion),
            "must not be empty");
        WorkerProtocolValidation.Require(
            string.Equals(
                RuntimeProfile,
                WorkerProtocol.RuntimeProfile,
                StringComparison.Ordinal),
            nameof(RuntimeProfile),
            "must match the approved runtime profile");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(LLamaSharpVersion),
            nameof(LLamaSharpVersion),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(BackendPackageVersion),
            nameof(BackendPackageVersion),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(MappedLlamaCppCommit),
            nameof(MappedLlamaCppCommit),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(NativeLibraryName),
            nameof(NativeLibraryName),
            "must not be empty");
        WorkerProtocolValidation.Require(
            string.Equals(ProcessArchitecture, "X64", StringComparison.Ordinal),
            nameof(ProcessArchitecture),
            "must equal X64");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(InspectionMode),
            nameof(InspectionMode),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !UsesCuda,
            nameof(UsesCuda),
            "must be false for the first runtime profile");
        WorkerProtocolValidation.Require(
            !UsesVulkan,
            nameof(UsesVulkan),
            "must be false for the first runtime profile");
        WorkerProtocolValidation.Require(
            GpuLayerCount == 0,
            nameof(GpuLayerCount),
            "must be zero for the first runtime profile");
    }
}
