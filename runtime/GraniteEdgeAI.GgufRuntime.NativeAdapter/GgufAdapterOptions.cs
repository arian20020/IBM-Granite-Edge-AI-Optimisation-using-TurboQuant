using System.Globalization;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal enum GgufAdapterCacheType
{
    F16,
    Q8Zero,
    Q4Zero,
}

internal sealed class GgufAdapterConfigurationException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

internal sealed record GgufAdapterOptions(
    string ModelPath,
    uint ContextSize,
    GgufAdapterCacheType KeyCacheType,
    GgufAdapterCacheType ValueCacheType,
    int GpuLayerCount,
    int ThreadCount,
    uint BatchSize,
    bool FlashAttention,
    int MaximumGeneratedTokens = 512)
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
        if (arguments.Count != RequiredNames.Length * 2)
        {
            throw Unsupported();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            if (!RequiredNames.Contains(arguments[index], StringComparer.Ordinal) ||
                !values.TryAdd(arguments[index], arguments[index + 1]))
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
        if (gpuLayers != 0)
        {
            throw Unsupported();
        }

        return new GgufAdapterOptions(
            Path.GetFullPath(modelPath),
            ParsePositiveUInt(values["--ctx-size"]),
            ParseCacheType(values["--cache-type-k"]),
            ParseCacheType(values["--cache-type-v"]),
            gpuLayers,
            ParsePositiveInt(values["--threads"]),
            ParsePositiveUInt(values["--batch-size"]),
            ParseSwitch(values["--flash-attention"]),
            ParsePositiveInt(values["--max-tokens"]));
    }

    private static GgufAdapterCacheType ParseCacheType(string value)
    {
        if (value is "turbo2" or "turbo3" or "turbo4")
        {
            throw new GgufAdapterConfigurationException(
                "turboquant-runtime-required");
        }

        return value switch
        {
            "f16" => GgufAdapterCacheType.F16,
            "q8_0" => GgufAdapterCacheType.Q8Zero,
            "q4_0" => GgufAdapterCacheType.Q4Zero,
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
