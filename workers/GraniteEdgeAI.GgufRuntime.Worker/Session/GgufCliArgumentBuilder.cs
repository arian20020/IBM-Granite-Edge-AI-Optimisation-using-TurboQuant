using System.Globalization;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.Worker.Session;

internal static class GgufCliArgumentBuilder
{
    private const string AtomicBotRuntimeBuildId =
        "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan";
    private const string AtomicBotRuntimeSourceCommit =
        "519f0c594a8e31467d2e2f2cf17054c9e7e11536";

    internal static IReadOnlyList<string> Build(
        string modelPath,
        GgufRuntimeConfiguration configuration,
        string? nativeRuntimePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!Path.IsPathFullyQualified(modelPath) || modelPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The model location must be an absolute local path.",
                nameof(modelPath));
        }

        bool turboQuant = configuration.KeyCacheType is GgufCacheType.Turbo2
                or GgufCacheType.Turbo3 or GgufCacheType.Turbo4
            || configuration.ValueCacheType is GgufCacheType.Turbo2
                or GgufCacheType.Turbo3 or GgufCacheType.Turbo4;
        bool pinnedAtomicBotCpu = configuration.Backend == GgufRuntimeBackend.Cpu
            && string.Equals(
                configuration.RuntimeBuildId,
                AtomicBotRuntimeBuildId,
                StringComparison.Ordinal)
            && string.Equals(
                configuration.RuntimeSourceCommit,
                AtomicBotRuntimeSourceCommit,
                StringComparison.Ordinal);
        bool nativeRequired = turboQuant
            || configuration.Backend == GgufRuntimeBackend.Vulkan
            || pinnedAtomicBotCpu;
        if (configuration.Backend == GgufRuntimeBackend.Sycl
            || configuration.Backend == GgufRuntimeBackend.Cpu
                && configuration.GpuLayerCount != 0
            || configuration.Backend == GgufRuntimeBackend.Vulkan
                && configuration.GpuLayerCount == 0
            || nativeRequired && !ValidLocalExecutable(nativeRuntimePath)
            || !nativeRequired && nativeRuntimePath is not null)
        {
            throw new ArgumentException(
                "The configuration does not match an admitted runtime executable.",
                nameof(configuration));
        }

        List<string> arguments =
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
            Format(configuration.GpuLayerCount),
            "--threads",
            Format(configuration.ThreadCount),
            "--batch-size",
            Format(configuration.BatchSize),
            "--flash-attention",
            configuration.FlashAttention ? "on" : "off",
            "--max-tokens",
            configuration.MaximumGeneratedTokens.ToString(CultureInfo.InvariantCulture),
        ];
        if (nativeRequired)
        {
            arguments.Add("--native-runtime");
            arguments.Add(Path.GetFullPath(nativeRuntimePath!));
            arguments.Add("--backend");
            arguments.Add(configuration.Backend == GgufRuntimeBackend.Vulkan
                ? "vulkan"
                : "cpu");
        }
        return arguments;
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
            GgufCacheType.Turbo3 => "turbo3",
            GgufCacheType.Turbo4 => "turbo4",
            GgufCacheType.Turbo2 => "turbo2",
            _ => throw new ArgumentOutOfRangeException(nameof(cacheType)),
        };
    }

    private static bool ValidLocalExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && !path.StartsWith("\\\\", StringComparison.Ordinal);
}
