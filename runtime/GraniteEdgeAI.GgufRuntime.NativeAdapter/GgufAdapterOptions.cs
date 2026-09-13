using System.Globalization;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal enum GgufAdapterCacheType
{
    F16,
    Q8Zero,
    Q4Zero,
    Turbo2,
    Turbo3,
    Turbo4,
}

internal enum GgufAdapterBackend
{
    UpstreamCpu,
    Cpu,
    Vulkan,
}

internal sealed class GgufAdapterConfigurationException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

internal sealed class GgufContextLimitException() : Exception("The conversation has no remaining model context.");

internal sealed record GgufAdapterOptions(
    string ModelPath,
    uint ContextSize,
    GgufAdapterCacheType KeyCacheType,
    GgufAdapterCacheType ValueCacheType,
    int GpuLayerCount,
    int ThreadCount,
    uint BatchSize,
    bool FlashAttention,
    int MaximumGeneratedTokens = int.MaxValue,
    string? NativeRuntimePath = null,
    GgufAdapterBackend Backend = GgufAdapterBackend.UpstreamCpu)
{
    private static readonly string[] RequiredNames =
    [
        "--model",
        "--ctx-size",
        "--cache-type-k",
        "--cache-type-v",
        "--n-gpu-layers",
        "--threads",
        "--batch-size",
        "--flash-attention",
        "--max-tokens",
    ];

    internal static GgufAdapterOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count is not 18 and not 22)
        {
            throw Unsupported();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            bool known = RequiredNames.Contains(
                    arguments[index], StringComparer.Ordinal)
                || arguments[index] is "--native-runtime" or "--backend";
            if (!known || !values.TryAdd(arguments[index], arguments[index + 1]))
            {
                throw Unsupported();
            }
        }

        string modelPath = values["--model"];
        if (!Path.IsPathFullyQualified(modelPath) ||
            modelPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw Unsupported();
        }

        int gpuLayers = ParseNonNegative(values["--n-gpu-layers"]);
        bool hasNativeRuntime = values.TryGetValue(
            "--native-runtime", out string? nativeRuntime);
        bool hasBackend = values.TryGetValue("--backend", out string? backendValue);
        if (hasNativeRuntime != hasBackend)
        {
            throw Unsupported();
        }

        GgufAdapterBackend backend = GgufAdapterBackend.UpstreamCpu;
        if (hasNativeRuntime)
        {
            if (string.IsNullOrWhiteSpace(nativeRuntime)
                || !Path.IsPathFullyQualified(nativeRuntime)
                || nativeRuntime.StartsWith("\\\\", StringComparison.Ordinal))
            {
                throw Unsupported();
            }
            backend = backendValue switch
            {
                "cpu" => GgufAdapterBackend.Cpu,
                "vulkan" => GgufAdapterBackend.Vulkan,
                _ => throw Unsupported(),
            };
        }
        if ((!hasNativeRuntime && gpuLayers != 0)
            || backend == GgufAdapterBackend.Cpu && gpuLayers != 0
            || backend == GgufAdapterBackend.Vulkan && gpuLayers == 0)
        {
            throw Unsupported();
        }

        GgufAdapterCacheType keyCache = ParseCacheType(values["--cache-type-k"]);
        GgufAdapterCacheType valueCache = ParseCacheType(values["--cache-type-v"]);
        if (!hasNativeRuntime
            && (IsTurboQuant(keyCache) || IsTurboQuant(valueCache)))
        {
            throw new GgufAdapterConfigurationException(
                "turboquant-runtime-required");
        }

        return new GgufAdapterOptions(
            Path.GetFullPath(modelPath),
            ParsePositiveUInt(values["--ctx-size"]),
            keyCache,
            valueCache,
            gpuLayers,
            ParsePositiveInt(values["--threads"]),
            ParsePositiveUInt(values["--batch-size"]),
            ParseSwitch(values["--flash-attention"]),
            ParsePositiveInt(values["--max-tokens"]),
            hasNativeRuntime ? Path.GetFullPath(nativeRuntime!) : null,
            backend);
    }

    private static bool IsTurboQuant(GgufAdapterCacheType value) =>
        value is GgufAdapterCacheType.Turbo2
            or GgufAdapterCacheType.Turbo3
            or GgufAdapterCacheType.Turbo4;

    private static GgufAdapterCacheType ParseCacheType(string value)
    {
        return value switch
        {
            "f16" => GgufAdapterCacheType.F16,
            "q8_0" => GgufAdapterCacheType.Q8Zero,
            "q4_0" => GgufAdapterCacheType.Q4Zero,
            "turbo2" => GgufAdapterCacheType.Turbo2,
            "turbo3" => GgufAdapterCacheType.Turbo3,
            "turbo4" => GgufAdapterCacheType.Turbo4,
            _ => throw Unsupported(),
        };
    }

    private static int ParsePositiveInt(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) &&
        result > 0
            ? result
            : throw Unsupported();

    private static uint ParsePositiveUInt(string value) =>
        uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint result) &&
        result > 0
            ? result
            : throw Unsupported();

    private static int ParseNonNegative(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) &&
        result >= 0
            ? result
            : throw Unsupported();

    private static bool ParseSwitch(string value) => value switch
    {
        "on" => true,
        "off" => false,
        _ => throw Unsupported(),
    };

    private static GgufAdapterConfigurationException Unsupported() =>
        new("unsupported-configuration");
}
