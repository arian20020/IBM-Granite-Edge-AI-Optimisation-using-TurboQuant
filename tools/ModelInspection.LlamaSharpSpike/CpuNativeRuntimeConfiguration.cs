using System.Collections.Concurrent;
using LLama.Abstractions;
using LLama.Native;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Configures and describes the exact CPU-only LLamaSharp native-library
/// selection used by both feasibility modes.
/// </summary>
internal static class CpuNativeRuntimeConfiguration
{
    /// <summary>
    /// Configures LLamaSharp before any native API is called.
    /// </summary>
    internal static void Configure(
        ConcurrentQueue<NativeBackendLogEntry> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        NativeLibraryConfig.LLama
            .WithCuda(enable: false)
            .WithVulkan(enable: false)
            .WithAutoFallback(enable: true)
            .WithLogCallback(
                (level, message) =>
                {
                    logs.Enqueue(
                        new NativeBackendLogEntry(
                            level.ToString(),
                            message.TrimEnd('\r', '\n')));
                });
    }

    /// <summary>
    /// Converts LLamaSharp's native-library descriptor into project-owned
    /// evidence.
    /// </summary>
    internal static SelectedNativeBackend? Describe(
        INativeLibrary? selectedLibrary)
    {
        if (selectedLibrary is null)
        {
            return null;
        }

        NativeLibraryMetadata? metadata = selectedLibrary.Metadata;

        return new SelectedNativeBackend
        {
            ImplementationType = selectedLibrary.GetType().FullName,
            NativeLibraryName =
                metadata?.NativeLibraryName.ToString(),
            UsesCuda = metadata?.UseCuda,
            UsesVulkan = metadata?.UseVulkan,
            AvxLevel = metadata?.AvxLevel.ToString()
        };
    }
}
