using System.Globalization;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal static class AtomicBotServerArguments
{
    internal static IReadOnlyList<string> Build(GgufAdapterOptions options, int port)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Backend == GgufAdapterBackend.UpstreamCpu
            || string.IsNullOrWhiteSpace(options.NativeRuntimePath)
            || port is < 1 or > 65535)
        {
            throw new ArgumentException("An admitted native server configuration is required.");
        }

        return
        [
            "--host", "127.0.0.1",
            "--port", Format(port),
            "--no-ui",
            "--no-context-shift",
            "--model", options.ModelPath,
            "--ctx-size", Format(options.ContextSize),
            "--cache-type-k", Format(options.KeyCacheType),
            "--cache-type-v", Format(options.ValueCacheType),
            "--n-gpu-layers", Format(options.GpuLayerCount),
            "--threads", Format(options.ThreadCount),
            "--batch-size", Format(options.BatchSize),
            "--flash-attn", options.FlashAttention ? "on" : "off",
            "--parallel", "1",
            "--cache-ram", "0",
            "--fit", "off",
            "--offline",
        ];
    }

    private static string Format(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(uint value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(GgufAdapterCacheType value) => value switch
    {
        GgufAdapterCacheType.F16 => "f16",
        GgufAdapterCacheType.Q8Zero => "q8_0",
        GgufAdapterCacheType.Q4Zero => "q4_0",
        GgufAdapterCacheType.Turbo2 => "turbo2",
        GgufAdapterCacheType.Turbo3 => "turbo3",
        GgufAdapterCacheType.Turbo4 => "turbo4",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
