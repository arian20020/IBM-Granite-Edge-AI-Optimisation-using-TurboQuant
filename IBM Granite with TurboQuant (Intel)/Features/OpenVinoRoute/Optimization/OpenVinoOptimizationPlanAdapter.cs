using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

public enum OpenVinoOptimizationAdaptationStatus
{
    Ready,
    ReplanRequired
}

public sealed record OpenVinoOptimizationAdaptation(
    OpenVinoOptimizationAdaptationStatus Status,
    OpenVinoOptimizationCandidate? Candidate,
    OptimizationSupportCode SupportCode);

public static class OpenVinoOptimizationPlanAdapter
{
    public static OpenVinoOptimizationAdaptation Adapt(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot currentCapabilities,
        string currentSourceSha256,
        ulong currentSourceLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentCapabilities);

        if (plan.ContractVersion != OptimizationExecutionPlan.CurrentContractVersion ||
            plan.Route != OptimizationRoute.OpenVino ||
            plan.Candidate.Configuration is not OpenVinoRouteConfiguration configuration ||
            plan.CapabilitySnapshot.Route != OptimizationRoute.OpenVino ||
            plan.CapabilitySnapshot.OpenVino is not { } plannedPayload)
        {
            return Replan(OptimizationSupportCode.ModelBindingMismatch);
        }

        if (currentCapabilities.Route != OptimizationRoute.OpenVino ||
            currentCapabilities.OpenVino is not { } currentPayload ||
            !string.Equals(
                currentCapabilities.SnapshotId,
                plan.CapabilitySnapshot.SnapshotId,
                StringComparison.Ordinal) ||
            !plan.MatchesCapability(currentCapabilities) ||
            !PayloadsMatch(plannedPayload, currentPayload))
        {
            return Replan(OptimizationSupportCode.CapabilityDrift);
        }

        if (!plan.MatchesSource(
            currentSourceSha256,
            currentSourceLengthBytes))
        {
            return Replan(OptimizationSupportCode.SourceIdentityMismatch);
        }

        if (!ConfigurationIdentityMatches(plan))
        {
            return Replan(OptimizationSupportCode.ModelBindingMismatch);
        }

        OpenVinoAdmittedConfiguration[] matchingEvidence = currentPayload.Admitted
            .Where(admission => string.Equals(
                admission.EvidenceId,
                plan.Candidate.EvidenceId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingEvidence.Length != 1 ||
            !AdmissionMatches(
                matchingEvidence[0],
                configuration,
                plan.Candidate.Metrics.ContextTokens,
                plan.Candidate.IsExperimental))
        {
            return Replan(OptimizationSupportCode.ToolNotAdmitted);
        }

        OpenVinoOptimizationCandidate? candidate = Map(plan, configuration);
        return candidate is null
            ? Replan(OptimizationSupportCode.ToolNotAdmitted)
            : new OpenVinoOptimizationAdaptation(
                OpenVinoOptimizationAdaptationStatus.Ready,
                candidate,
                OptimizationSupportCode.None);
    }

    private static bool ConfigurationIdentityMatches(OptimizationExecutionPlan plan)
    {
        try
        {
            OptimizationSelection? selection = OptimizationPreferenceResolver.Resolve(
                [plan.Candidate],
                plan.Preference);
            if (selection is null)
            {
                return false;
            }

            OptimizationExecutionPlan recomputed = OptimizationPlanIssuer.Issue(
                selection,
                plan.CapabilitySnapshot,
                plan.Workload,
                plan.Binding,
                plan.CreatedAtUtc);
            return string.Equals(
                recomputed.ConfigurationSha256,
                plan.ConfigurationSha256,
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          InvalidOperationException)
        {
            return false;
        }
    }

    private static OpenVinoOptimizationCandidate? Map(
        OptimizationExecutionPlan plan,
        OpenVinoRouteConfiguration configuration)
    {
        OpenVinoWeightPrecision weights = configuration.Weights switch
        {
            OpenVinoWeightFormat.Original => OpenVinoWeightPrecision.Original,
            OpenVinoWeightFormat.Fp16 => OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightFormat.Int8 => OpenVinoWeightPrecision.EightBit,
            OpenVinoWeightFormat.Int4 => OpenVinoWeightPrecision.FourBit,
            OpenVinoWeightFormat.Unspecified or
            OpenVinoWeightFormat.TurboQuantTbq4 or
            OpenVinoWeightFormat.TurboQuantTbq3 => (OpenVinoWeightPrecision)(-1),
            _ => (OpenVinoWeightPrecision)(-1)
        };
        if (!Enum.IsDefined(weights))
        {
            return null;
        }

        OpenVinoKvCachePrecision kvCache = configuration.KvCache switch
        {
            OpenVinoKvCacheFormat.RouteDefault =>
                OpenVinoKvCachePrecision.ReleasedDefault,
            OpenVinoKvCacheFormat.U8 => OpenVinoKvCachePrecision.U8,
            OpenVinoKvCacheFormat.Unspecified or OpenVinoKvCacheFormat.F16 or
            OpenVinoKvCacheFormat.Bf16 or OpenVinoKvCacheFormat.U4 =>
                (OpenVinoKvCachePrecision)(-1),
            _ => (OpenVinoKvCachePrecision)(-1)
        };
        if (!Enum.IsDefined(kvCache) || configuration.Device != DeviceRouteId.Cpu ||
            configuration.PerformanceHint != OpenVinoPerformanceHint.Latency)
        {
            return null;
        }

        RouteCompiledCachePolicy compiledCache = configuration.CompiledCache switch
        {
            ContractCompiledCachePolicy.Disabled =>
                RouteCompiledCachePolicy.Disabled,
            ContractCompiledCachePolicy.Enabled =>
                RouteCompiledCachePolicy.Disposable,
            ContractCompiledCachePolicy.Unspecified => null!,
            _ => null!
        };
        if (compiledCache is null)
        {
            return null;
        }

        bool persistent = weights != OpenVinoWeightPrecision.Original;
        if (persistent != plan.ProducesPersistentArtifact)
        {
            return null;
        }

        OpenVinoOptimizationCandidate candidate = new(
            ConfigurationId(configuration, plan.ConfigurationSha256),
            "CPU",
            weights,
            persistent ? OpenVinoPersistentArtifact.Create(weights) : null!,
            new OpenVinoRuntimeOptimization(kvCache, compiledCache),
            OpenVinoCapabilityPerformanceHint.Latency,
            configuration.Streams,
            plan.Candidate.Metrics.ContextTokens,
            "Standard candidate",
            plan.Candidate.EvidenceId);
        try
        {
            candidate.Validate();
            return candidate;
        }
        catch (OpenVinoOptimizationException)
        {
            return null;
        }
    }

    private static string ConfigurationId(
        OpenVinoRouteConfiguration configuration,
        string configurationSha256) => configuration switch
        {
            {
                Weights: OpenVinoWeightFormat.Fp16,
                KvCache: OpenVinoKvCacheFormat.RouteDefault,
                CompiledCache: ContractCompiledCachePolicy.Disabled,
                Streams: 1
            } => "openvino.standard.cpu.fp16.default.v1",
            {
                Weights: OpenVinoWeightFormat.Int8,
                KvCache: OpenVinoKvCacheFormat.RouteDefault,
                CompiledCache: ContractCompiledCachePolicy.Disabled,
                Streams: 1
            } => "openvino.standard.cpu.int8.default.v1",
            {
                Weights: OpenVinoWeightFormat.Int8,
                KvCache: OpenVinoKvCacheFormat.U8,
                CompiledCache: ContractCompiledCachePolicy.Enabled,
                Streams: 1
            } => "openvino.standard.cpu.int8.u8.v1",
            {
                Weights: OpenVinoWeightFormat.Int4,
                KvCache: OpenVinoKvCacheFormat.U8,
                CompiledCache: ContractCompiledCachePolicy.Enabled,
                Streams: 1
            } => "openvino.standard.cpu.int4.u8.v1",
            _ => "openvino.plan.v1." + configurationSha256
        };

    private static bool AdmissionMatches(
        OpenVinoAdmittedConfiguration admission,
        OpenVinoRouteConfiguration configuration,
        int contextTokens,
        bool isExperimental) =>
        admission.Device == configuration.Device &&
        admission.Weights == configuration.Weights &&
        admission.KvCache == configuration.KvCache &&
        admission.PerformanceHint == configuration.PerformanceHint &&
        admission.CompiledCache == configuration.CompiledCache &&
        admission.Streams == configuration.Streams &&
        contextTokens >= admission.MinimumContextTokens &&
        contextTokens <= admission.MaximumContextTokens &&
        admission.Level == SupportLevel.DeclaredSupported &&
        !admission.RequiresEvidence &&
        !isExperimental;

    private static bool PayloadsMatch(
        OpenVinoCapabilityPayload planned,
        OpenVinoCapabilityPayload current)
    {
        if (!string.Equals(
                planned.RuntimeVersion,
                current.RuntimeVersion,
                StringComparison.Ordinal) ||
            planned.Admitted.Count != current.Admitted.Count)
        {
            return false;
        }

        for (int index = 0; index < planned.Admitted.Count; index++)
        {
            if (planned.Admitted[index] != current.Admitted[index])
            {
                return false;
            }
        }

        return true;
    }

    private static OpenVinoOptimizationAdaptation Replan(
        OptimizationSupportCode supportCode) =>
        new(
            OpenVinoOptimizationAdaptationStatus.ReplanRequired,
            Candidate: null,
            supportCode);
}
