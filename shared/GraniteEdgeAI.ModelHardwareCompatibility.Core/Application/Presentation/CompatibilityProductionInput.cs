using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

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
        HashSet<DeviceRouteId> devices = [.. presentDevices];
        HashSet<CompatibilityBackend> backends = [.. verifiedBackends];
        if (devices.Count == 0 || devices.Contains(DeviceRouteId.Unspecified))
        {
            throw new ArgumentException("At least one established device is required.", nameof(presentDevices));
        }

        if (backends.Contains(CompatibilityBackend.Unspecified))
        {
            throw new ArgumentException("An unspecified backend is not verified.", nameof(verifiedBackends));
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
        ulong availableStorageBytes,
        DateTimeOffset observedAtUtc)
    {
        AvailableSystemMemoryBytes = availableSystemMemoryBytes;
        AvailableDedicatedDeviceMemoryBytes = availableDedicatedDeviceMemoryBytes;
        AvailableStorageBytes = availableStorageBytes;
        ObservedAtUtc = observedAtUtc;
    }

    public ulong AvailableSystemMemoryBytes { get; }
    public ulong AvailableDedicatedDeviceMemoryBytes { get; }
    public ulong AvailableStorageBytes { get; }
    public DateTimeOffset ObservedAtUtc { get; }

    public static CompatibilityFreshResourcesInput Create(
        ulong availableSystemMemoryBytes,
        ulong availableDedicatedDeviceMemoryBytes,
        ulong availableStorageBytes,
        DateTimeOffset observedAtUtc)
    {
        if (observedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Observation timestamp must use UTC.", nameof(observedAtUtc));
        }

        return new CompatibilityFreshResourcesInput(
            availableSystemMemoryBytes,
            availableDedicatedDeviceMemoryBytes,
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
        GgufCompatibilityModelInput model,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources)
    {
        ModelInspectionRunId = modelInspectionRunId;
        ProductHardwareRunId = productHardwareRunId;
        Model = model;
        Hardware = hardware;
        FreshResources = freshResources;
    }

    public Guid ModelInspectionRunId { get; }
    public Guid ProductHardwareRunId { get; }
    public GgufCompatibilityModelInput Model { get; }
    public CompatibilityHardwareInput Hardware { get; }
    public CompatibilityFreshResourcesInput FreshResources { get; }

    public static CompatibilityProductionInput Create(
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        GgufCompatibilityModelInput model,
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput freshResources)
    {
        RequireUuidV4(modelInspectionRunId, nameof(modelInspectionRunId));
        RequireUuidV4(productHardwareRunId, nameof(productHardwareRunId));
        if (modelInspectionRunId == productHardwareRunId)
        {
            throw new ArgumentException("Model and Hardware run identities must be distinct.", nameof(productHardwareRunId));
        }

        return new CompatibilityProductionInput(
            modelInspectionRunId,
            productHardwareRunId,
            model ?? throw new ArgumentNullException(nameof(model)),
            hardware ?? throw new ArgumentNullException(nameof(hardware)),
            freshResources ?? throw new ArgumentNullException(nameof(freshResources)));
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
