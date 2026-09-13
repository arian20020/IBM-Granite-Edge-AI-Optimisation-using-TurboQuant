using System;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// records the complete approved worker and native-runtime identity used to
/// produce one inspection result
/// </summary>
internal sealed record ModelInspectionRuntimeIdentity
{
    /// <summary>
    /// creates immutable runtime identity evidence without native handles
    /// </summary>
    internal ModelInspectionRuntimeIdentity(
        string workerId,
        string workerVersion,
        int protocolVersion,
        string runtimeProfile,
        string llamaSharpVersion,
        string backendPackageVersion,
        string mappedLlamaCppCommit,
        string nativeLibraryName,
        string processArchitecture,
        string inspectionMode,
        bool usesCuda,
        bool usesVulkan,
        int gpuLayerCount)
    {
        WorkerId = ModelInspectionContractValidation.RequireText(
            workerId,
            nameof(workerId));
        WorkerVersion = ModelInspectionContractValidation.RequireText(
            workerVersion,
            nameof(workerVersion));

        if (protocolVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(protocolVersion),
                protocolVersion,
                "Protocol version must be positive.");
        }

        ProtocolVersion = protocolVersion;
        RuntimeProfile = ModelInspectionContractValidation.RequireText(
            runtimeProfile,
            nameof(runtimeProfile));
        LLamaSharpVersion = ModelInspectionContractValidation.RequireText(
            llamaSharpVersion,
            nameof(llamaSharpVersion));
        BackendPackageVersion = ModelInspectionContractValidation.RequireText(
            backendPackageVersion,
            nameof(backendPackageVersion));
        MappedLlamaCppCommit =
            ModelInspectionContractValidation.RequireHexDigest(
                mappedLlamaCppCommit,
                expectedLength: 40,
                nameof(mappedLlamaCppCommit));
        NativeLibraryName =
            ModelInspectionContractValidation.RequireFinalFileName(
                nativeLibraryName,
                nameof(nativeLibraryName));
        ProcessArchitecture = ModelInspectionContractValidation.RequireText(
            processArchitecture,
            nameof(processArchitecture));
        InspectionMode = ModelInspectionContractValidation.RequireText(
            inspectionMode,
            nameof(inspectionMode));

        if (gpuLayerCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gpuLayerCount),
                gpuLayerCount,
                "GPU layer count must not be negative.");
        }

        UsesCuda = usesCuda;
        UsesVulkan = usesVulkan;
        GpuLayerCount = gpuLayerCount;
    }

    internal string WorkerId { get; }

    internal string WorkerVersion { get; }

    internal int ProtocolVersion { get; }

    internal string RuntimeProfile { get; }

    internal string LLamaSharpVersion { get; }

    internal string BackendPackageVersion { get; }

    internal string MappedLlamaCppCommit { get; }

    internal string NativeLibraryName { get; }

    internal string ProcessArchitecture { get; }

    internal string InspectionMode { get; }

    internal bool UsesCuda { get; }

    internal bool UsesVulkan { get; }

    internal int GpuLayerCount { get; }
}
