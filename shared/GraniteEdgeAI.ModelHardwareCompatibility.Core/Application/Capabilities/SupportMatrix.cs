using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// The versioned set of admitted configuration shapes.
///
/// Like the safety and estimator policies this carries an explicit provenance:
/// an absent matrix admits nothing at all rather than falling back to a guess at
/// what the machine might support.
/// </summary>
internal sealed record SupportMatrix
{
    private SupportMatrix(
        PolicyProvenance provenance,
        string matrixVersion,
        IReadOnlyList<CompatibilitySupportEntry> entries)
    {
        Provenance = provenance;
        MatrixVersion = matrixVersion;
        Entries = entries;
    }

    internal PolicyProvenance Provenance { get; }

    internal string MatrixVersion { get; }

    internal IReadOnlyList<CompatibilitySupportEntry> Entries { get; }

    internal static SupportMatrix FromEntries(
        string matrixVersion,
        PolicyProvenance provenance,
        IReadOnlyList<CompatibilitySupportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrWhiteSpace(matrixVersion))
        {
            throw new ArgumentException(
                "A matrix must be versioned so a result can name what admitted it.",
                nameof(matrixVersion));
        }

        if (entries.Select(entry => entry.EntryId).Distinct().Count() != entries.Count)
        {
            throw new ArgumentException(
                "Entry ids must be unique; a candidate carries one as provenance, "
                + "so a duplicate makes two configurations indistinguishable.",
                nameof(entries));
        }

        // Copy so a later caller mutation cannot change an already-published matrix.
        return new SupportMatrix(provenance, matrixVersion, [.. entries]);
    }

    /// <summary>
    /// Version one. These shapes are the ones the workflow documents describe as
    /// admitted; the installation state of each is decided at runtime, not here.
    /// </summary>
    internal static SupportMatrix ProvisionalV1() => FromEntries(
        "support-matrix-v1",
        PolicyProvenance.Provisional,
        [
            Entry("gguf-cpu-imported-f16",
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None,
                GgufWeightFormat.Imported, GgufKvCacheFormat.F16),

            Entry("gguf-cpu-imported-q8kv",
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None,
                GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0),

            Entry("gguf-igpu-sycl-imported-f16",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.F16),

            Entry("gguf-igpu-sycl-imported-q8kv",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0),

            Entry("gguf-dgpu-sycl-imported-f16",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.F16),

            Entry("gguf-dgpu-sycl-imported-q8kv",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0),

            Entry("gguf-cpu-q4km-f16",
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None,
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16),

            // TurboQuant replaces the standard KV component set rather than
            // joining it, and it is not yet backed by measured evidence.
            Entry("gguf-dgpu-sycl-imported-tq3",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported,
                GgufKvCacheFormat.TurboQuant3Bit,
                level: SupportLevel.Experimental, requiresEvidence: true)
        ]);

    internal static SupportMatrix Absent() =>
        new(PolicyProvenance.Absent, "absent", []);

    private static CompatibilitySupportEntry Entry(
        string entryId,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        SupportLevel level = SupportLevel.DeclaredSupported,
        bool requiresEvidence = false) =>
        CompatibilitySupportEntry.Create(
            entryId,
            RuntimeRouteId.LlamaCpp,
            backend,
            device,
            offload,
            weights,
            kvCache,
            minimumContextTokens: 1024,
            maximumContextTokens: 32768,
            level,
            requiresEvidence);
}
