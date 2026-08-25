using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// What an OpenVINO configuration would cost to run.
///
/// Built to the same rules as the GGUF estimator, because the two results are
/// compared against each other and a difference in method would show up as a
/// difference in the answer. Every component is named, every unknown refuses,
/// and nothing is charged to two pools.
///
/// The pool a component lands in is decided by the device, not by the
/// component. An integrated GPU has no memory of its own: what it uses is
/// system RAM reached through a different path. Charging it to a dedicated pool
/// that does not exist would halve the apparent requirement and admit a setup
/// the machine cannot hold.
/// </summary>
internal static class OpenVinoResourceEstimator
{
    private static readonly IReadOnlySet<LifecyclePhase> AllPhases =
        new HashSet<LifecyclePhase>
        {
            LifecyclePhase.Load,
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        };

    private static readonly IReadOnlySet<LifecyclePhase> SteadyState =
        new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration };

    internal static ResourceEstimate Estimate(
        InspectedModelFacts facts,
        OpenVinoRouteConfiguration configuration,
        ContextTokenCount context,
        EstimatorPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Provenance is PolicyProvenance.Absent or PolicyProvenance.Unspecified)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.EstimatorPolicyUnavailable);
        }

        // The cache needs all four. Estimating around a missing one would put a
        // confident number in front of a safety gate with nothing behind it.
        if (facts.LayerCount is not { } layers
            || facts.EmbeddingSize is not { } embedding
            || facts.AttentionHeadCount is not { } heads
            || facts.KeyValueHeadCount is not { } keyValueHeads)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.UnknownArchitecture);
        }

        if (layers < 1 || embedding < 1 || heads < 1 || keyValueHeads < 1)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.UnknownArchitecture);
        }

        EstimatorTerms terms = policy.Terms;

        // Where memory is charged. Shared is the integrated case: real system
        // RAM, addressed by the GPU, and the phase composer folds it into
        // system pressure exactly once.
        ResourceTarget pool = configuration.Device switch
        {
            DeviceRouteId.IntelDiscreteGpu => ResourceTarget.DedicatedDeviceMemory,
            DeviceRouteId.IntelIntegratedGpu or DeviceRouteId.IntelNpu =>
                ResourceTarget.SharedDeviceMemory,
            _ => ResourceTarget.SystemMemory
        };

        List<ResourceComponent> components = [];

        // Recorded, not hidden. The weights figure is scaled from the source
        // file rather than counted, and one sequence is assumed; a reader of
        // this estimate is entitled to know both.
        HashSet<EstimationLimitation> limitations =
        [
            EstimationLimitation.WeightsDerivedFromFileLength,
            EstimationLimitation.SingleSequenceAssumed
        ];

        if (policy.Provenance == PolicyProvenance.Provisional)
        {
            limitations.Add(EstimationLimitation.UncalibratedEstimatorPolicy);
        }

        ByteCount weights = WeightBytes(facts, configuration, terms);
        components.Add(ResourceComponent.Create(
            ResourceComponentKind.Weights, pool, weights, AllPhases));

        components.Add(ResourceComponent.Create(
            ResourceComponentKind.KvCache,
            pool,
            CacheBytes(configuration, context, layers, embedding, heads, keyValueHeads, terms),
            SteadyState));

        // Each stream carries its own working set, so throughput configurations
        // cost more than latency ones by more than a rounding difference.
        ByteCount compute = policy.ComputeBufferFor(context)
            .MultiplyByFraction(configuration.Streams);
        components.Add(ResourceComponent.Create(
            ResourceComponentKind.ComputeBuffer, pool, compute, SteadyState));

        components.Add(ResourceComponent.Create(
            ResourceComponentKind.BackendAllocation,
            pool,
            pool == ResourceTarget.SystemMemory
                ? terms.CpuBackendAllocation
                : terms.GpuBackendAllocation,
            AllPhases));

        // Charged to system memory whatever the device, because the process
        // hosting the runtime lives there regardless of where inference runs.
        components.Add(ResourceComponent.Create(
            ResourceComponentKind.ApplicationOverhead,
            ResourceTarget.SystemMemory,
            terms.ApplicationOverhead,
            AllPhases));

        if (OpenVinoFormatMap.RequiresPersistentConversion(configuration.Weights))
        {
            // A conversion writes a package. The plan has to state that space
            // before a user agrees to it, so it is part of the estimate rather
            // than a surprise at execution.
            components.Add(ResourceComponent.Create(
                ResourceComponentKind.PersistentArtifact,
                ResourceTarget.Storage,
                weights,
                AllPhases));

            components.Add(ResourceComponent.Create(
                ResourceComponentKind.StagingBuffer,
                ResourceTarget.Storage,
                Staging(facts, terms),
                new HashSet<LifecyclePhase> { LifecyclePhase.Load }));

            limitations.Add(EstimationLimitation.ConversionSourceStorageNotCounted);
        }

        if (configuration.CompiledCache == OpenVinoCompiledCachePolicy.Enabled)
        {
            // Charged as model state, not as a persistent artifact. The blob is
            // a real file that survives between runs, but it is not a model
            // copy - and PersistentArtifact is what decides whether the page
            // tells the user a new model was created. Conflating the two would
            // make a runtime-only result claim an artifact it never wrote.
            components.Add(ResourceComponent.Create(
                ResourceComponentKind.ModelState,
                ResourceTarget.Storage,
                weights.MultiplyByFraction(0.05m),
                AllPhases));
        }

        return ResourceEstimate.Established(components, limitations);
    }

    /// <summary>
    /// Original weights are the file that already exists. Every other
    /// representation is derived from the parameter count implied by the model
    /// shape, plus the runtime's allocation overhead.
    /// </summary>
    private static ByteCount WeightBytes(
        InspectedModelFacts facts,
        OpenVinoRouteConfiguration configuration,
        EstimatorTerms terms)
    {
        if (!OpenVinoFormatMap.HasBitWidth(configuration.Weights))
        {
            return Grown(facts.FileLength, terms);
        }

        // Scaled from the source file by the ratio of representations, which
        // keeps the estimate anchored to a measured value rather than to a
        // parameter count nobody counted.
        decimal ratio = OpenVinoFormatMap.BitsPerWeight(configuration.Weights) / 16m;

        return Grown(facts.FileLength.MultiplyByFraction(ratio), terms);
    }

    private static ByteCount Grown(ByteCount raw, EstimatorTerms terms) =>
        raw.MultiplyByFraction(1m + terms.WeightOverheadFraction)
            .AlignUpTo(terms.AllocationAlignment);

    private static ByteCount CacheBytes(
        OpenVinoRouteConfiguration configuration,
        ContextTokenCount context,
        int layers,
        int embedding,
        int heads,
        int keyValueHeads,
        EstimatorTerms terms)
    {
        // Grouped-query attention: the cache is sized by the key/value heads,
        // not by the attention heads. Using the larger count would overstate
        // every model that groups them, which is most modern ones.
        decimal headDimension = (decimal)embedding / heads;
        decimal perTokenPerLayer = ConservativeCacheBytesPerTokenPerLayer(
            configuration.KvCache, headDimension, keyValueHeads);

        decimal total = perTokenPerLayer * layers * context.Tokens;

        return ByteCount.FromBytes(checked((ulong)Math.Ceiling(total)))
            .AlignUpTo(terms.AllocationAlignment);
    }

    /// <summary>
    /// Physical cache payload with a conservative monotonic floor.
    ///
    /// Quantised layouts pad every K/V head to complete 128-value records. Near
    /// a block boundary that padding can exceed the scalar estimate of a
    /// nominally wider format. The frontier contract still
    /// requires that reducing precision never increases estimated memory, so a
    /// wider format is conservatively floored at every lower-precision physical
    /// layout. This never understates either representation and does not invent
    /// a block layout for the non-TurboQuant formats.
    /// </summary>
    private static decimal ConservativeCacheBytesPerTokenPerLayer(
        OpenVinoKvCacheFormat format,
        decimal headDimension,
        int keyValueHeads)
    {
        OpenVinoKvCacheFormat[] precisionFloor = format switch
        {
            OpenVinoKvCacheFormat.RouteDefault
                or OpenVinoKvCacheFormat.F16
                or OpenVinoKvCacheFormat.Bf16 =>
                [format, OpenVinoKvCacheFormat.U8, OpenVinoKvCacheFormat.U4,
                    OpenVinoKvCacheFormat.TurboQuantTbq4,
                    OpenVinoKvCacheFormat.TurboQuantTbq3],
            OpenVinoKvCacheFormat.U8 =>
                [format, OpenVinoKvCacheFormat.U4,
                    OpenVinoKvCacheFormat.TurboQuantTbq4,
                    OpenVinoKvCacheFormat.TurboQuantTbq3],
            OpenVinoKvCacheFormat.U4 =>
                [format, OpenVinoKvCacheFormat.TurboQuantTbq4,
                    OpenVinoKvCacheFormat.TurboQuantTbq3],
            OpenVinoKvCacheFormat.TurboQuantTbq4 =>
                [format, OpenVinoKvCacheFormat.TurboQuantTbq3],
            OpenVinoKvCacheFormat.TurboQuantTbq3 => [format],
            _ => throw new ArgumentOutOfRangeException(
                nameof(format), format, "Unknown cache precision cannot be estimated.")
        };

        return precisionFloor.Max(candidate =>
            PhysicalCacheBytesPerTokenPerLayer(
                candidate, headDimension, keyValueHeads));
    }

    private static decimal PhysicalCacheBytesPerTokenPerLayer(
        OpenVinoKvCacheFormat format,
        decimal headDimension,
        int keyValueHeads)
    {
        if (!OpenVinoFormatMap.TryGetCacheBlockLayout(
            format, out int valuesPerBlock, out int bytesPerBlock))
        {
            return headDimension * keyValueHeads * 2m
                * OpenVinoFormatMap.CacheBytesPerElement(format);
        }

        decimal blocksPerHead = decimal.Ceiling(headDimension / valuesPerBlock);

        // Key and value are separate records for every KV head. Padding cannot
        // be shared across heads or across K/V without describing a packed
        // layout the pinned codec does not have.
        return blocksPerHead * bytesPerBlock * keyValueHeads * 2m;
    }

    private static ByteCount Staging(InspectedModelFacts facts, EstimatorTerms terms)
    {
        ByteCount scaled = facts.FileLength.MultiplyByFraction(terms.StagingBufferFraction);

        return scaled > terms.StagingBufferFloor ? scaled : terms.StagingBufferFloor;
    }
}
