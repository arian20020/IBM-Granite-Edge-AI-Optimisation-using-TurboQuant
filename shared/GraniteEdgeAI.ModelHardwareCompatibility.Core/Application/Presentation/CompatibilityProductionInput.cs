using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// Validated GGUF calculation facts supplied by the Model Inspection owner.
/// The boundary carries no display text, path, filename, or provider payload.
/// </summary>
public sealed record GgufCompatibilityModelInput
{
    private GgufCompatibilityModelInput(
        ulong fileLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion)
    {
        FileLengthBytes = fileLengthBytes;
        LayerCount = layerCount;
        EmbeddingSize = embeddingSize;
        AttentionHeadCount = attentionHeadCount;
        KeyValueHeadCount = keyValueHeadCount;
        DeclaredContextLimit = declaredContextLimit;
        FileType = fileType;
        QuantisationVersion = quantisationVersion;
    }

    public ulong FileLengthBytes { get; }
    public int? LayerCount { get; }
    public int? EmbeddingSize { get; }
    public int? AttentionHeadCount { get; }
    public int? KeyValueHeadCount { get; }
    public int? DeclaredContextLimit { get; }
    public int? FileType { get; }
    public int? QuantisationVersion { get; }

    public static GgufCompatibilityModelInput Create(
        ulong fileLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion)
    {
        if (fileLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileLengthBytes));
        }

        RequirePositive(layerCount, nameof(layerCount));
        RequirePositive(embeddingSize, nameof(embeddingSize));
        RequirePositive(attentionHeadCount, nameof(attentionHeadCount));
        RequirePositive(keyValueHeadCount, nameof(keyValueHeadCount));
        RequirePositive(declaredContextLimit, nameof(declaredContextLimit));
        RequireNonNegative(fileType, nameof(fileType));
        RequireNonNegative(quantisationVersion, nameof(quantisationVersion));

        return new GgufCompatibilityModelInput(
            fileLengthBytes,
            layerCount,
            embeddingSize,
            attentionHeadCount,
            keyValueHeadCount,
            declaredContextLimit,
            fileType,
            quantisationVersion);
    }

    private static void RequirePositive(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireNonNegative(int? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>Validated model/package facts for an inspected OpenVINO package.</summary>
public sealed record OpenVinoCompatibilityModelInput
{
    private OpenVinoCompatibilityModelInput(
        ulong packageLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit)
    {
        PackageLengthBytes = packageLengthBytes;
        LayerCount = layerCount;
        EmbeddingSize = embeddingSize;
        AttentionHeadCount = attentionHeadCount;
        KeyValueHeadCount = keyValueHeadCount;
        DeclaredContextLimit = declaredContextLimit;
    }

    public ulong PackageLengthBytes { get; }
    public int? LayerCount { get; }
    public int? EmbeddingSize { get; }
    public int? AttentionHeadCount { get; }
    public int? KeyValueHeadCount { get; }
    public int? DeclaredContextLimit { get; }

    public static OpenVinoCompatibilityModelInput Create(
        ulong packageLengthBytes,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit)
    {
        if (packageLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(packageLengthBytes));
        }
        RequirePositive(layerCount, nameof(layerCount));
        RequirePositive(embeddingSize, nameof(embeddingSize));
        RequirePositive(attentionHeadCount, nameof(attentionHeadCount));
        RequirePositive(keyValueHeadCount, nameof(keyValueHeadCount));
        RequirePositive(declaredContextLimit, nameof(declaredContextLimit));
        return new OpenVinoCompatibilityModelInput(
            packageLengthBytes, layerCount, embeddingSize, attentionHeadCount,
            keyValueHeadCount, declaredContextLimit);
    }

    private static void RequirePositive(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>
/// Closed route-discriminated current model. Its exact runtime representation
/// is the baseline; the engine never reconstructs one from a filename or a
/// default belonging to another route.
/// </summary>
public sealed record CompatibilityCurrentModelInput
{
    private CompatibilityCurrentModelInput(
        OptimizationRoute route,
        GgufCompatibilityModelInput? gguf,
        OpenVinoCompatibilityModelInput? openVino,
        GgufRouteConfiguration? ggufConfiguration,
        OpenVinoRouteConfiguration? openVinoConfiguration,
        OpenVinoWeightPrecision? openVinoSourcePrecision)
    {
        Route = route;
        Gguf = gguf;
        OpenVino = openVino;
        GgufConfiguration = ggufConfiguration;
        OpenVinoConfiguration = openVinoConfiguration;
        OpenVinoSourcePrecision = openVinoSourcePrecision;
    }

    public OptimizationRoute Route { get; }
    public GgufCompatibilityModelInput? Gguf { get; }
    public OpenVinoCompatibilityModelInput? OpenVino { get; }
    public GgufRouteConfiguration? GgufConfiguration { get; }
    public OpenVinoRouteConfiguration? OpenVinoConfiguration { get; }
    public OpenVinoWeightPrecision? OpenVinoSourcePrecision { get; }
    public ulong ModelLengthBytes =>
        Gguf?.FileLengthBytes ?? OpenVino!.PackageLengthBytes;

    internal RouteConfiguration BaselineConfiguration =>
        (RouteConfiguration?)GgufConfiguration ?? OpenVinoConfiguration!;

    public static CompatibilityCurrentModelInput ForGguf(
        GgufCompatibilityModelInput model,
        GgufRouteConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(configuration);
        return new CompatibilityCurrentModelInput(
            OptimizationRoute.Gguf, model, null, configuration, null, null);
    }

    public static CompatibilityCurrentModelInput ForOpenVino(
        OpenVinoCompatibilityModelInput model,
        OpenVinoRouteConfiguration configuration,
        OpenVinoWeightPrecision sourcePrecision)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!Enum.IsDefined(sourcePrecision))
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePrecision));
        }
        return new CompatibilityCurrentModelInput(
            OptimizationRoute.OpenVino, null, model, null, configuration,
            sourcePrecision);
    }
}

/// <summary>
/// Canonical, path-free digests over the exact model and hardware facts the
/// compatibility engine consumes. These are distinct from artifact hashes.
/// </summary>
public static class CompatibilityFactDigest
{
    public static string ComputeModel(CompatibilityCurrentModelInput model)
    {
        ArgumentNullException.ThrowIfNull(model);
        StringBuilder canonical = new();
        Append(canonical, "compatibility-model-facts-v1");
        Append(canonical, (int)model.Route);
        Append(canonical, model.ModelLengthBytes);
        if (model.Gguf is { } gguf)
        {
            Append(canonical, gguf.LayerCount);
            Append(canonical, gguf.EmbeddingSize);
            Append(canonical, gguf.AttentionHeadCount);
            Append(canonical, gguf.KeyValueHeadCount);
            Append(canonical, gguf.DeclaredContextLimit);
            Append(canonical, gguf.FileType);
            Append(canonical, gguf.QuantisationVersion);
            GgufRouteConfiguration route = model.GgufConfiguration!;
            Append(canonical, (int)route.Weights);
            Append(canonical, (int)route.KvCache);
            Append(canonical, (int)route.Backend);
            Append(canonical, (int)route.Device);
            Append(canonical, (int)route.Offload);
        }
        else
        {
            OpenVinoCompatibilityModelInput openVino = model.OpenVino!;
            Append(canonical, openVino.LayerCount);
            Append(canonical, openVino.EmbeddingSize);
            Append(canonical, openVino.AttentionHeadCount);
            Append(canonical, openVino.KeyValueHeadCount);
            Append(canonical, openVino.DeclaredContextLimit);
            OpenVinoRouteConfiguration route = model.OpenVinoConfiguration!;
            Append(canonical, (int)route.Weights);
            Append(canonical, (int)route.KvCache);
            Append(canonical, (int)route.Device);
            Append(canonical, (int)route.PerformanceHint);
            Append(canonical, (int)route.CompiledCache);
            Append(canonical, route.Streams);
            Append(canonical, (int)model.OpenVinoSourcePrecision!.Value);
        }
        return Digest(canonical);
    }

    public static string ComputeHardware(CompatibilityHardwareInput hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        StringBuilder canonical = new();
        Append(canonical, "compatibility-hardware-facts-v1");
        Append(canonical, hardware.InstalledSystemMemoryBytes);
        Append(canonical, hardware.InstalledDedicatedDeviceMemoryBytes);
        Append(canonical, hardware.FreeStorageBytes);
        Append(canonical, "devices");
        Append(canonical, hardware.PresentDevices.Count);
        foreach (DeviceRouteId device in hardware.PresentDevices.Order())
        {
            Append(canonical, (int)device);
        }
        Append(canonical, "backends");
        Append(canonical, hardware.VerifiedBackends.Count);
        foreach (CompatibilityBackend backend in hardware.VerifiedBackends.Order())
        {
            Append(canonical, (int)backend);
        }
        return Digest(canonical);
    }

    private static string Digest(StringBuilder canonical) =>
        Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical.ToString())))
            .ToLowerInvariant();

    private static void Append<T>(StringBuilder canonical, T? value)
    {
        string text = value is null
            ? "null"
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        canonical.Append(text.Length.ToString(CultureInfo.InvariantCulture));
        canonical.Append(':');
        canonical.Append(text);
    }
}

/// <summary>Independent journey identities supplied outside the optimization binding.</summary>
public sealed record CompatibilityJourneyAuthorityInput
{
    private CompatibilityJourneyAuthorityInput(
        Guid modelInspectionHandoffId,
        string modelSha256,
        string? modelFactsSha256,
        string hardwareSnapshotSha256,
        string? hardwareFactsSha256)
    {
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelSha256 = modelSha256;
        ModelFactsSha256 = modelFactsSha256;
        HardwareSnapshotSha256 = hardwareSnapshotSha256;
        HardwareFactsSha256 = hardwareFactsSha256;
    }

    public Guid ModelInspectionHandoffId { get; }
    public string ModelSha256 { get; }
    public string? ModelFactsSha256 { get; }
    public string HardwareSnapshotSha256 { get; }
    public string? HardwareFactsSha256 { get; }

    public static CompatibilityJourneyAuthorityInput Create(
        Guid modelInspectionHandoffId,
        string modelSha256,
        string hardwareSnapshotSha256)
    {
        RequireUuidV4(modelInspectionHandoffId, nameof(modelInspectionHandoffId));
        RequireDigest(modelSha256, nameof(modelSha256));
        RequireDigest(hardwareSnapshotSha256, nameof(hardwareSnapshotSha256));
        return new CompatibilityJourneyAuthorityInput(
            modelInspectionHandoffId, modelSha256, null,
            hardwareSnapshotSha256, null);
    }

    public static CompatibilityJourneyAuthorityInput Create(
        Guid modelInspectionHandoffId,
        string modelSha256,
        string modelFactsSha256,
        string hardwareSnapshotSha256,
        string hardwareFactsSha256)
    {
        RequireUuidV4(modelInspectionHandoffId, nameof(modelInspectionHandoffId));
        RequireDigest(modelSha256, nameof(modelSha256));
        RequireDigest(modelFactsSha256, nameof(modelFactsSha256));
        RequireDigest(hardwareSnapshotSha256, nameof(hardwareSnapshotSha256));
        RequireDigest(hardwareFactsSha256, nameof(hardwareFactsSha256));
        return new CompatibilityJourneyAuthorityInput(
            modelInspectionHandoffId, modelSha256, modelFactsSha256,
            hardwareSnapshotSha256, hardwareFactsSha256);
    }

    private static void RequireUuidV4(Guid value, string parameterName)
    {
        string canonical = value.ToString("D");
        if (value == Guid.Empty || canonical[14] != '4'
            || canonical[19] is not ('8' or '9' or 'a' or 'b'))
        {
            throw new ArgumentException(
                "Handoff identity must be an RFC 4122 UUID version 4.", parameterName);
        }
    }

    private static void RequireDigest(string value, string parameterName)
    {
        if (value is null || value.Length != 64
            || value.Any(character => character is not (
                >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Journey digests must be lowercase SHA-256 values.", parameterName);
        }
    }
}

/// <summary>Route-neutral machine capacities and verified execution capability.</summary>
public sealed record CompatibilityHardwareInput
{
    private CompatibilityHardwareInput(
        ulong installedSystemMemoryBytes,
        ulong installedDedicatedDeviceMemoryBytes,
        ulong freeStorageBytes,
        IReadOnlySet<DeviceRouteId> presentDevices,
        IReadOnlySet<CompatibilityBackend> verifiedBackends)
    {
        InstalledSystemMemoryBytes = installedSystemMemoryBytes;
        InstalledDedicatedDeviceMemoryBytes = installedDedicatedDeviceMemoryBytes;
        FreeStorageBytes = freeStorageBytes;
        PresentDevices = presentDevices;
        VerifiedBackends = verifiedBackends;
    }

    public ulong InstalledSystemMemoryBytes { get; }
    public ulong InstalledDedicatedDeviceMemoryBytes { get; }
    public ulong FreeStorageBytes { get; }
    public IReadOnlySet<DeviceRouteId> PresentDevices { get; }
    public IReadOnlySet<CompatibilityBackend> VerifiedBackends { get; }

    public static CompatibilityHardwareInput Create(
        ulong installedSystemMemoryBytes,
        ulong installedDedicatedDeviceMemoryBytes,
        ulong freeStorageBytes,
        IEnumerable<DeviceRouteId> presentDevices,
        IEnumerable<CompatibilityBackend> verifiedBackends)
    {
        if (installedSystemMemoryBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installedSystemMemoryBytes));
        }

        ArgumentNullException.ThrowIfNull(presentDevices);
        ArgumentNullException.ThrowIfNull(verifiedBackends);
        List<DeviceRouteId> deviceSnapshot = [];
        foreach (DeviceRouteId device in presentDevices)
        {
            if (!Enum.IsDefined(device) || device == DeviceRouteId.Unspecified)
            {
                throw new ArgumentException(
                    "Every device must be a defined established value.",
                    nameof(presentDevices));
            }
            deviceSnapshot.Add(device);
        }

        List<CompatibilityBackend> backendSnapshot = [];
        foreach (CompatibilityBackend backend in verifiedBackends)
        {
            if (!Enum.IsDefined(backend)
                || backend == CompatibilityBackend.Unspecified)
            {
                throw new ArgumentException(
                    "Every backend must be a defined verified value.",
                    nameof(verifiedBackends));
            }
            backendSnapshot.Add(backend);
        }

        FrozenSet<DeviceRouteId> devices = deviceSnapshot.ToFrozenSet();
        FrozenSet<CompatibilityBackend> backends = backendSnapshot.ToFrozenSet();
        if (devices.Count == 0)
        {
            throw new ArgumentException("At least one established device is required.", nameof(presentDevices));
        }

        return new CompatibilityHardwareInput(
            installedSystemMemoryBytes,
            installedDedicatedDeviceMemoryBytes,
            freeStorageBytes,
            devices,
            backends);
    }
}

/// <summary>One resource observation captured immediately before the safety gate.</summary>
public sealed record CompatibilityFreshResourcesInput
{
    private CompatibilityFreshResourcesInput(
        ulong availableSystemMemoryBytes,
        ulong availableDedicatedDeviceMemoryBytes,
        bool dedicatedDeviceMemoryEstablished,
        ulong availableStorageBytes,
        DateTimeOffset observedAtUtc)
    {
        AvailableSystemMemoryBytes = availableSystemMemoryBytes;
        AvailableDedicatedDeviceMemoryBytes = availableDedicatedDeviceMemoryBytes;
        DedicatedDeviceMemoryEstablished = dedicatedDeviceMemoryEstablished;
        AvailableStorageBytes = availableStorageBytes;
        ObservedAtUtc = observedAtUtc;
    }

    public ulong AvailableSystemMemoryBytes { get; }
    public ulong AvailableDedicatedDeviceMemoryBytes { get; }
    public bool DedicatedDeviceMemoryEstablished { get; }
    public ulong AvailableStorageBytes { get; }
    public DateTimeOffset ObservedAtUtc { get; }

    public static CompatibilityFreshResourcesInput Create(
        ulong availableSystemMemoryBytes,
        ulong? availableDedicatedDeviceMemoryBytes,
        ulong availableStorageBytes,
        DateTimeOffset observedAtUtc)
    {
        if (observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Observation timestamp must use UTC.", nameof(observedAtUtc));
        }

        return new CompatibilityFreshResourcesInput(
            availableSystemMemoryBytes,
            availableDedicatedDeviceMemoryBytes.GetValueOrDefault(),
            availableDedicatedDeviceMemoryBytes.HasValue,
            availableStorageBytes,
            observedAtUtc);
    }
}

/// <summary>The paired, identity-bound inputs for one production compatibility run.</summary>
public sealed record CompatibilityProductionInput
{
    private CompatibilityProductionInput(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        CompatibilityCurrentModelInput currentModel,
        CompatibilityJourneyAuthorityInput? journeyAuthority,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources,
        CompatibilityOptimizationProductionInput? optimization)
    {
        ModelInspectionRunId = modelInspectionRunId;
        ProductHardwareRunId = productHardwareRunId;
        CurrentModel = currentModel;
        JourneyAuthority = journeyAuthority;
        Hardware = hardware;
        FreshResources = freshResources;
        Optimization = optimization;
    }

    public Guid ModelInspectionRunId { get; }
    public Guid ProductHardwareRunId { get; }
    public CompatibilityCurrentModelInput CurrentModel { get; }
    /// <summary>Legacy GGUF accessor; route-neutral code uses CurrentModel.</summary>
    public GgufCompatibilityModelInput Model => CurrentModel.Gguf
        ?? throw new InvalidOperationException(
            "The current package is OpenVINO and has no GGUF model facts.");
    public CompatibilityJourneyAuthorityInput? JourneyAuthority { get; }
    public CompatibilityHardwareInput Hardware { get; }
    public CompatibilityFreshResourcesInput FreshResources { get; }
    public CompatibilityOptimizationProductionInput? Optimization { get; }

    /// <summary>
    /// Reissues the immutable production input with a newly snapshotted set of
    /// exact experimental evidence identifiers. All model, Hardware, journey,
    /// capability and workload authority remains unchanged and is revalidated.
    /// </summary>
    public CompatibilityProductionInput WithOptedInExperimentalEvidenceIds(
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);
        CompatibilityOptimizationProductionInput optimization = Optimization
            ?? throw new InvalidOperationException(
                "Experimental consent requires sealed optimization authority.");
        CompatibilityOptimizationProductionInput revised =
            CompatibilityOptimizationProductionInput.Create(
                optimization.Snapshot,
                optimization.Workload,
                optimization.Binding,
                optedInExperimentalEvidenceIds);
        return CreateValidated(
            ModelInspectionRunId,
            ProductHardwareRunId,
            CurrentModel,
            JourneyAuthority,
            Hardware,
            FreshResources,
            revised);
    }

    /// <summary>
    /// Legacy compatibility factory. It carries no optimization authority and
    /// the engine therefore cannot return an optimization recommendation.
    /// New production integrations must use the sealed-authority overload.
    /// </summary>
    public static CompatibilityProductionInput Create(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        GgufCompatibilityModelInput model,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources)
        => CreateLegacy(
            modelInspectionRunId, productHardwareRunId, model, hardware,
            freshResources);

    private static CompatibilityProductionInput CreateLegacy(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        GgufCompatibilityModelInput model,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources)
    {
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None);
        return CreateValidated(
            modelInspectionRunId, productHardwareRunId,
            CompatibilityCurrentModelInput.ForGguf(model, configuration),
            journeyAuthority: null, hardware, freshResources, optimization: null);
    }

    /// <summary>
    /// Creates the production path with the complete, sealed optimization
    /// authority. The legacy overload is retained for callers that have not yet
    /// published capability evidence; it cannot produce optimization choices.
    /// </summary>
    public static CompatibilityProductionInput Create(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        GgufCompatibilityModelInput model,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources,
        CompatibilityOptimizationProductionInput? optimization)
        => optimization is null
            ? CreateLegacy(
                modelInspectionRunId, productHardwareRunId, model, hardware,
                freshResources)
            : throw new ArgumentException(
                "Optimization requires independent journey authority and an exact route baseline.",
                nameof(optimization));

    /// <summary>
    /// Creates an inspected OpenVINO compatibility run before optional
    /// optimization authority has been assembled.
    /// </summary>
    public static CompatibilityProductionInput Create(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        OpenVinoCompatibilityModelInput model,
        OpenVinoRouteConfiguration configuration,
        OpenVinoWeightPrecision sourcePrecision,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(configuration);
        return CreateValidated(
            modelInspectionRunId,
            productHardwareRunId,
            CompatibilityCurrentModelInput.ForOpenVino(
                model, configuration, sourcePrecision),
            journeyAuthority: null,
            hardware,
            freshResources,
            optimization: null);
    }

    public static CompatibilityProductionInput Create(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        CompatibilityCurrentModelInput currentModel,
        CompatibilityJourneyAuthorityInput journeyAuthority,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources,
        CompatibilityOptimizationProductionInput optimization)
        => CreateValidated(
            modelInspectionRunId, productHardwareRunId, currentModel,
            journeyAuthority, hardware, freshResources, optimization);

    private static CompatibilityProductionInput CreateValidated(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        CompatibilityCurrentModelInput currentModel,
        CompatibilityJourneyAuthorityInput? journeyAuthority,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources,
        CompatibilityOptimizationProductionInput? optimization)
    {
        ArgumentNullException.ThrowIfNull(currentModel);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(freshResources);
        RequireUuidV4(modelInspectionRunId, nameof(modelInspectionRunId));
        RequireUuidV4(productHardwareRunId, nameof(productHardwareRunId));
        if (modelInspectionRunId == productHardwareRunId)
        {
            throw new ArgumentException("Model and Hardware run identities must be distinct.", nameof(productHardwareRunId));
        }

        if (freshResources.AvailableSystemMemoryBytes
                > hardware.InstalledSystemMemoryBytes
            || freshResources.DedicatedDeviceMemoryEstablished
                && freshResources.AvailableDedicatedDeviceMemoryBytes
                    > hardware.InstalledDedicatedDeviceMemoryBytes
            || freshResources.AvailableStorageBytes > hardware.FreeStorageBytes)
        {
            throw new ArgumentException(
                "Fresh resource availability cannot exceed the bound hardware facts.",
                nameof(freshResources));
        }

        if (optimization is not null
            && (journeyAuthority is null
                || optimization.Snapshot.Route != currentModel.Route
                || !string.Equals(
                    optimization.Binding.ModelInspectionRunId,
                    modelInspectionRunId.ToString("N"),
                    StringComparison.Ordinal)
                || !string.Equals(
                    optimization.Binding.ProductHardwareRunId,
                    productHardwareRunId.ToString("N"),
                    StringComparison.Ordinal)
                || !string.Equals(
                    optimization.Binding.ModelInspectionHandoffId,
                    journeyAuthority.ModelInspectionHandoffId.ToString("N"),
                    StringComparison.Ordinal)
                || !string.Equals(
                    optimization.Binding.ModelSha256,
                    journeyAuthority.ModelSha256,
                    StringComparison.Ordinal)
                || optimization.Binding.ModelLengthBytes != currentModel.ModelLengthBytes
                || !string.Equals(
                    optimization.Binding.HardwareSnapshotSha256,
                    journeyAuthority.HardwareSnapshotSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    journeyAuthority.ModelFactsSha256,
                    CompatibilityFactDigest.ComputeModel(currentModel),
                    StringComparison.Ordinal)
                || !string.Equals(
                    journeyAuthority.HardwareFactsSha256,
                    CompatibilityFactDigest.ComputeHardware(hardware),
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Optimization authority must bind this exact model and Hardware run.",
                nameof(optimization));
        }

        return new CompatibilityProductionInput(
            modelInspectionRunId,
            productHardwareRunId,
            currentModel,
            journeyAuthority,
            hardware,
            freshResources,
            optimization);
    }

    private static void RequireUuidV4(Guid value, string parameterName)
    {
        string canonical = value.ToString("D");
        if (value == Guid.Empty || canonical[14] != '4' || canonical[19] is not ('8' or '9' or 'a' or 'b'))
        {
            throw new ArgumentException("Run identity must be an RFC 4122 UUID version 4.", parameterName);
        }
    }
}

/// <summary>
/// Sealed generation inputs supplied by the compatibility integration owner.
/// No file path, provider payload, or display wording crosses this boundary.
/// </summary>
public sealed record CompatibilityOptimizationProductionInput
{
    private CompatibilityOptimizationProductionInput(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        Snapshot = snapshot;
        Workload = workload;
        Binding = binding;
        OptedInExperimentalEvidenceIds = optedInExperimentalEvidenceIds;
    }

    public OptimizationCapabilitySnapshot Snapshot { get; }
    public OptimizationWorkload Workload { get; }
    public OptimizationJourneyBinding Binding { get; }
    public IReadOnlySet<string> OptedInExperimentalEvidenceIds { get; }

    public static CompatibilityOptimizationProductionInput Create(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        IReadOnlySet<string> optedInExperimentalEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);
        List<string> evidenceSnapshot = [];
        foreach (string evidenceId in optedInExperimentalEvidenceIds)
        {
            OptimizationIdentifier.Require(
                evidenceId,
                nameof(optedInExperimentalEvidenceIds),
                "An experimental capability opt-in");
            evidenceSnapshot.Add(evidenceId);
        }

        return new CompatibilityOptimizationProductionInput(
            snapshot, workload, binding,
            evidenceSnapshot.ToFrozenSet(StringComparer.Ordinal));
    }
}
