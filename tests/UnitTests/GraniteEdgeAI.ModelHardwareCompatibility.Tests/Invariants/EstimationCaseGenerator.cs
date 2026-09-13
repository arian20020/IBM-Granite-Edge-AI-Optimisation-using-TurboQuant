using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

internal sealed record EstimationCase(
    ulong Seed,
    InspectedModelFacts Facts,
    GgufRouteConfiguration Configuration,
    ContextTokenCount Context);

/// <summary>
/// Produces structurally valid facts and configurations across the admitted
/// routes. Every case is estimable, so a NotEstablished result during a property
/// sweep is itself a failure rather than an expected outcome.
/// </summary>
internal static class EstimationCaseGenerator
{
    private static readonly int[] HeadDimensions = [64, 80, 96, 128];
    private static readonly int[] AttentionHeadCounts = [8, 16, 32, 40];
    private static readonly int[] GroupDivisors = [1, 2, 4, 8];
    private static readonly int[] LayerCounts = [16, 24, 32, 48, 80];
    private static readonly int[] Contexts = [1024, 2048, 4096, 8192, 16384];

    private static readonly GgufKvCacheFormat[] KvFormats =
        [GgufKvCacheFormat.F16, GgufKvCacheFormat.Q8_0, GgufKvCacheFormat.TurboQuant3Bit];

    // Every generated case sets fileType 15 / quantisationVersion 2 (Q4_K_M), so
    // any non-Imported target here scales rather than refusing. Varying this is
    // what gives ChangingTheKvFormatCanNeverAlterTheWeightPayload teeth: with a
    // single constant weight format, both branches of that property called the
    // same pure function with identical inputs
    private static readonly GgufWeightFormat[] WeightFormats =
    [
        GgufWeightFormat.Imported,
        GgufWeightFormat.Q8_0,
        GgufWeightFormat.Q6K,
        GgufWeightFormat.Q4KM,
        GgufWeightFormat.Q3KM
    ];

    private static readonly (DeviceRouteId Device, CompatibilityBackend Backend, GpuOffloadLevel Offload)[] Routes =
    [
        (DeviceRouteId.Cpu, CompatibilityBackend.Cpu, GpuOffloadLevel.None),
        (DeviceRouteId.IntelDiscreteGpu, CompatibilityBackend.IntelSycl, GpuOffloadLevel.Full),
        (DeviceRouteId.IntelIntegratedGpu, CompatibilityBackend.IntelVulkan, GpuOffloadLevel.Full)
    ];

    internal static IEnumerable<EstimationCase> Cases(int count, ulong seed)
    {
        DeterministicRandom random = new(seed);

        for (int index = 0; index < count; index++)
        {
            yield return FromSeed(random.NextUInt64());
        }
    }

    /// <summary>
    /// Builds the one case a seed deterministically produces. Cases(count, seed)
    /// treats its seed as the outer generator seed and derives a different
    /// per-case seed for each element, so a seed printed from a failing
    /// assertion is only replayable through this method - calling
    /// Cases(1, printedSeed) reproduces a different case entirely.
    /// </summary>
    internal static EstimationCase FromSeed(ulong caseSeed)
    {
        DeterministicRandom local = new(caseSeed);

        int attentionHeads = local.Pick(AttentionHeadCounts);
        int headDimension = local.Pick(HeadDimensions);
        int divisor = local.Pick(GroupDivisors);
        int keyValueHeads = Math.Max(1, attentionHeads / divisor);

        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes((ulong)local.Next(200, 60_000) * 1024 * 1024),
            layerCount: local.Pick(LayerCounts),
            embeddingSize: attentionHeads * headDimension,
            attentionHeadCount: attentionHeads,
            keyValueHeadCount: keyValueHeads,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

        (DeviceRouteId device, CompatibilityBackend backend, GpuOffloadLevel offload) =
            local.Pick(Routes);

        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            local.Pick(WeightFormats),
            local.Pick(KvFormats),
            backend,
            device,
            offload);

        return new EstimationCase(
            caseSeed,
            facts,
            configuration,
            ContextTokenCount.FromTokens(local.Pick(Contexts)));
    }

    internal static CompatibilityCandidate Candidate(
        EstimationCase generated,
        GgufKvCacheFormat? kvOverride = null,
        ContextTokenCount? contextOverride = null)
    {
        GgufRouteConfiguration configuration = kvOverride is { } kv
            ? GgufRouteConfiguration.Create(
                generated.Configuration.Weights,
                kv,
                generated.Configuration.Backend,
                generated.Configuration.Device,
                generated.Configuration.Offload)
            : generated.Configuration;

        return CompatibilityCandidate.Create(
            configuration,
            contextOverride ?? generated.Context,
            CandidatePreparation.None,
            supportEntryId: "entry-generated",
            isExperimental: false,
            isBaseline: true);
    }
}
