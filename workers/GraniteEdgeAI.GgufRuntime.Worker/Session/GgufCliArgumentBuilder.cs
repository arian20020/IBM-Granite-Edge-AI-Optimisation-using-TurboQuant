using System.Globalization;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal static class GgufCliArgumentBuilder
{
    internal static IReadOnlyList<string> Build(
        string modelPath,
        GgufRuntimeConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!Path.IsPathFullyQualified(modelPath) || modelPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The model location must be an absolute local path.",
                nameof(modelPath));
        }

        if (configuration.Backend != GgufRuntimeBackend.Cpu ||
            configuration.GpuLayerCount != 0)
        {
            throw new ArgumentException(
                "The MVP argument builder accepts only CPU configurations.",
                nameof(configuration));
        }

        return
        [
            "--model",
            modelPath,
            "--ctx-size",
            Format(configuration.ContextSize),
            "--cache-type-k",
            Format(configuration.KeyCacheType),
            "--cache-type-v",
            Format(configuration.ValueCacheType),
            "--n-gpu-layers",
            "0",
            "--threads",
            Format(configuration.ThreadCount),
            "--batch-size",
            Format(configuration.BatchSize),
            "--simple-io",
            "--conversation",
            "--no-display-prompt",
        ];
    }

    private static string Format(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Format(GgufCacheType cacheType)
    {
        return cacheType switch
        {
            GgufCacheType.F16 => "f16",
            GgufCacheType.Q8Zero => "q8_0",
            GgufCacheType.Q4Zero => "q4_0",
            _ => throw new ArgumentOutOfRangeException(nameof(cacheType)),
        };
    }
}
