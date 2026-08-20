namespace GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

public enum GgufRuntimeBackend
{
    Cpu,
    Vulkan,
    Sycl,
}

public enum GgufCacheType
{
    F16,
    Q8Zero,
    Q4Zero,
}

public sealed record GgufRuntimeConfiguration
{
    public GgufRuntimeConfiguration(
        string modelId,
        string modelSha256,
        string runtimeBuildId,
        string runtimeSourceCommit,
        GgufRuntimeBackend backend,
        string deviceId,
        int contextSize,
        GgufCacheType keyCacheType,
        GgufCacheType valueCacheType,
        int gpuLayerCount,
        bool flashAttention,
        int threadCount,
        int batchSize,
        string evidenceGrade,
        string profileId)
    {
        ModelId = RequireText(modelId, nameof(modelId));
        ModelSha256 = RequireExactLength(modelSha256, 64, nameof(modelSha256));
        RuntimeBuildId = RequireText(runtimeBuildId, nameof(runtimeBuildId));
        RuntimeSourceCommit = RequireExactLength(
            runtimeSourceCommit,
            40,
            nameof(runtimeSourceCommit));
        Backend = RequireDefined(backend, nameof(backend));
        DeviceId = RequireText(deviceId, nameof(deviceId));
        ContextSize = RequirePositive(contextSize, nameof(contextSize));
        KeyCacheType = RequireDefined(keyCacheType, nameof(keyCacheType));
        ValueCacheType = RequireDefined(valueCacheType, nameof(valueCacheType));
        ArgumentOutOfRangeException.ThrowIfNegative(gpuLayerCount);

        GpuLayerCount = gpuLayerCount;
        FlashAttention = flashAttention;
        ThreadCount = RequirePositive(threadCount, nameof(threadCount));
        BatchSize = RequirePositive(batchSize, nameof(batchSize));
        EvidenceGrade = RequireText(evidenceGrade, nameof(evidenceGrade));
        ProfileId = RequireText(profileId, nameof(profileId));
    }

    public string ModelId { get; }

    public string ModelSha256 { get; }

    public string RuntimeBuildId { get; }

    public string RuntimeSourceCommit { get; }

    public GgufRuntimeBackend Backend { get; }

    public string DeviceId { get; }

    public int ContextSize { get; }

    public GgufCacheType KeyCacheType { get; }

    public GgufCacheType ValueCacheType { get; }

    public int GpuLayerCount { get; }

    public bool FlashAttention { get; }

    public int ThreadCount { get; }

    public int BatchSize { get; }

    public string EvidenceGrade { get; }

    public string ProfileId { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value;
    }

    private static string RequireExactLength(string value, int length, string parameterName)
    {
        RequireText(value, parameterName);
        if (value.Length != length)
        {
            throw new ArgumentException(
                $"The value must contain exactly {length} characters.",
                parameterName);
        }

        return value;
    }

    private static int RequirePositive(int value, string parameterName)
    {
        return value > 0 ? value : throw new ArgumentOutOfRangeException(parameterName);
    }

    private static T RequireDefined<T>(T value, string parameterName)
        where T : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);
    }
}
