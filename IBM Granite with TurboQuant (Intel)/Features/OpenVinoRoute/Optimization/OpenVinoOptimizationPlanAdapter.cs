using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using ExecutionKvPrecision = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision;
using ExecutionWeightPrecision = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;
using RouteKvPrecision = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision;
using RouteWeightPrecision = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoWeightPrecision;

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
    private const int ExecutorContractVersion = 2;

    public static OpenVinoOptimizationAdaptation Adapt(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot currentCapabilities,
        string currentSourceSha256,
        ulong currentSourceLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentCapabilities);

        if (!plan.IsExecutableBy(ExecutorContractVersion) ||
            plan.ContractVersion != ExecutorContractVersion ||
            plan.Route != OptimizationRoute.OpenVino ||
            plan.Candidate.Route != OptimizationRoute.OpenVino ||
            plan.Candidate.Configuration is not OpenVinoRouteConfiguration configuration ||
            plan.CapabilitySnapshot.Route != OptimizationRoute.OpenVino ||
            plan.CapabilitySnapshot.OpenVino is not { } plannedCapabilities ||
            plan.ExecutionPayload is not { Route: OptimizationRoute.OpenVino } execution ||
            execution.OpenVino is not { } payload || execution.Gguf is not null ||
            payload.TurboQuantBuild is not null)
        {
            return Replan(OptimizationSupportCode.ModelBindingMismatch);
        }

        if (currentCapabilities.Route != OptimizationRoute.OpenVino ||
            currentCapabilities.OpenVino is not { } currentPayload ||
            !string.Equals(currentCapabilities.SnapshotId,
                plan.CapabilitySnapshot.SnapshotId, StringComparison.Ordinal) ||
            !plan.MatchesCapability(currentCapabilities) ||
            !PayloadsMatch(plannedCapabilities, currentPayload))
        {
            return Replan(OptimizationSupportCode.CapabilityDrift);
        }

        if (!ToolVersionsMatch(payload.OptimizerVersions, currentPayload.RuntimeVersion))
        {
            return Replan(OptimizationSupportCode.ToolNotAdmitted);
        }

        if (!plan.MatchesSource(currentSourceSha256, currentSourceLengthBytes))
        {
            return Replan(OptimizationSupportCode.SourceIdentityMismatch);
        }

        if (!ConfigurationIdentityMatches(plan, execution))
        {
            return Replan(OptimizationSupportCode.ModelBindingMismatch);
        }

        OpenVinoAdmittedConfiguration[] admissions = currentPayload.Admitted
            .Where(admission => string.Equals(admission.EvidenceId,
                payload.EvidenceId, StringComparison.Ordinal)).ToArray();
        if (admissions.Length != 1 ||
            !CandidateAndAdmissionMatch(plan, configuration, payload, admissions[0]))
        {
            return Replan(OptimizationSupportCode.ToolNotAdmitted);
        }

        OpenVinoOptimizationCandidate? candidate = Map(plan, payload);
        return candidate is null
            ? Replan(OptimizationSupportCode.ToolNotAdmitted)
            : new(OpenVinoOptimizationAdaptationStatus.Ready, candidate,
                OptimizationSupportCode.None);
    }

    private static bool ConfigurationIdentityMatches(
        OptimizationExecutionPlan plan,
        OptimizationExecutionPayload executionPayload)
    {
        try
        {
            OptimizationSelection? selection = OptimizationPreferenceResolver.Resolve(
                [plan.Candidate], plan.Preference);
            if (selection is null)
            {
                return false;
            }

            OptimizationExecutionPlan recomputed = OptimizationPlanIssuer.Issue(
                selection, executionPayload, plan.CapabilitySnapshot, plan.Workload,
                plan.Binding, modelLayerCount: 1, plan.CreatedAtUtc);
            return string.Equals(recomputed.ConfigurationSha256,
                plan.ConfigurationSha256, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          InvalidOperationException)
        {
            return false;
        }
    }

    private static bool CandidateAndAdmissionMatch(
        OptimizationExecutionPlan plan,
        OpenVinoRouteConfiguration configuration,
        OpenVinoExecutionPayload payload,
        OpenVinoAdmittedConfiguration admission)
    {
        OpenVinoWeightFormat target = payload.TargetWeightPrecision switch
        {
            ExecutionWeightPrecision.Fp16 => OpenVinoWeightFormat.Fp16,
            ExecutionWeightPrecision.EightBit => OpenVinoWeightFormat.Int8,
            ExecutionWeightPrecision.FourBit => OpenVinoWeightFormat.Int4,
            _ => OpenVinoWeightFormat.Unspecified
        };
        OpenVinoKvCacheFormat kv = payload.KvCachePrecision switch
        {
            ExecutionKvPrecision.ReleasedDefault => OpenVinoKvCacheFormat.RouteDefault,
            ExecutionKvPrecision.U8 => OpenVinoKvCacheFormat.U8,
            _ => OpenVinoKvCacheFormat.Unspecified
        };
        ContractCompiledCachePolicy cache = payload.CompiledCacheEnabled
            ? ContractCompiledCachePolicy.Enabled
            : ContractCompiledCachePolicy.Disabled;
        bool runtimeOnlyConfiguration =
            configuration.Weights == OpenVinoWeightFormat.Original &&
            payload.SourceWeightPrecision == payload.TargetWeightPrecision;

        return (runtimeOnlyConfiguration || configuration.Weights == target) &&
            configuration.KvCache == kv &&
            configuration.Device == DeviceRouteId.Cpu &&
            payload.Device == "CPU" &&
            configuration.PerformanceHint == OpenVinoPerformanceHint.Latency &&
            configuration.CompiledCache == cache &&
            plan.Candidate.Metrics.RequiresPersistentChange ==
                payload.RequiresPersistentConversion &&
            plan.ProducesPersistentArtifact == payload.RequiresPersistentConversion &&
            payload.CreatesCompletePackage == payload.RequiresPersistentConversion &&
            payload.CompiledCacheIsDisposable && !payload.CompiledCacheIsModelArtifact &&
            payload.Maturity == "Standard candidate" &&
            string.Equals(
                plan.Candidate.EvidenceId,
                payload.EvidenceId,
                StringComparison.Ordinal) &&
            admission.Device == configuration.Device &&
            admission.Weights == configuration.Weights &&
            admission.KvCache == configuration.KvCache &&
            admission.PerformanceHint == configuration.PerformanceHint &&
            admission.CompiledCache == configuration.CompiledCache &&
            admission.Streams == configuration.Streams &&
            plan.Candidate.Metrics.ContextTokens >= admission.MinimumContextTokens &&
            plan.Candidate.Metrics.ContextTokens <= admission.MaximumContextTokens &&
            admission.Level == SupportLevel.DeclaredSupported &&
            !admission.RequiresEvidence && !plan.Candidate.IsExperimental;
    }

    private static OpenVinoOptimizationCandidate? Map(
        OptimizationExecutionPlan plan,
        OpenVinoExecutionPayload payload)
    {
        RouteWeightPrecision source = MapWeight(payload.SourceWeightPrecision);
        RouteWeightPrecision target = MapWeight(payload.TargetWeightPrecision);
        RouteKvPrecision kv = payload.KvCachePrecision switch
        {
            ExecutionKvPrecision.ReleasedDefault => RouteKvPrecision.ReleasedDefault,
            ExecutionKvPrecision.U8 => RouteKvPrecision.U8,
            _ => (RouteKvPrecision)(-1)
        };
        if (!Enum.IsDefined(source) || !Enum.IsDefined(target) || !Enum.IsDefined(kv))
        {
            return null;
        }

        RouteCompiledCachePolicy cache = payload.CompiledCacheEnabled
            ? RouteCompiledCachePolicy.Disposable
            : RouteCompiledCachePolicy.Disabled;
        OpenVinoOptimizationCandidate candidate = new(
            payload.ConfigurationId, payload.Device, target,
            payload.RequiresPersistentConversion
                ? OpenVinoPersistentArtifact.Create(target, source) : null!,
            new OpenVinoRuntimeOptimization(kv, cache),
            OpenVinoCapabilityPerformanceHint.Latency,
            ((OpenVinoRouteConfiguration)plan.Candidate.Configuration).Streams,
            plan.Candidate.Metrics.ContextTokens,
            payload.Maturity,
            payload.EvidenceId)
        {
            SourceWeightPrecision = source,
            ExecutionPayload = payload
        };
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

    private static RouteWeightPrecision MapWeight(ExecutionWeightPrecision value) => value switch
    {
        ExecutionWeightPrecision.Fp16 => RouteWeightPrecision.Fp16,
        ExecutionWeightPrecision.EightBit => RouteWeightPrecision.EightBit,
        ExecutionWeightPrecision.FourBit => RouteWeightPrecision.FourBit,
        _ => (RouteWeightPrecision)(-1)
    };

    private static bool ToolVersionsMatch(
        IReadOnlyDictionary<string, string> optimizerVersions,
        string capabilityRuntimeVersion)
    {
        if (optimizerVersions.Count != 6 ||
            !optimizerVersions.TryGetValue("openvino", out string? openVino) ||
            !optimizerVersions.TryGetValue("openvino-genai", out string? openVinoGenAi) ||
            !optimizerVersions.TryGetValue("nncf", out string? nncf) ||
            !optimizerVersions.TryGetValue("optimum", out string? optimum) ||
            !optimizerVersions.TryGetValue("optimum-intel", out string? optimumIntel) ||
            !optimizerVersions.TryGetValue("transformers", out string? transformers))
        {
            return false;
        }

        try
        {
            string payloadRuntimeVersion =
                OpenVinoOptimizationCapabilityProjector.RuntimeVersion(new(
                    openVino,
                    openVinoGenAi,
                    nncf,
                    optimum,
                    optimumIntel,
                    transformers));
            return string.Equals(
                capabilityRuntimeVersion,
                payloadRuntimeVersion,
                StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool PayloadsMatch(
        OpenVinoCapabilityPayload planned,
        OpenVinoCapabilityPayload current) =>
        string.Equals(planned.RuntimeVersion, current.RuntimeVersion,
            StringComparison.Ordinal) &&
        planned.Admitted.SequenceEqual(current.Admitted);

    private static OpenVinoOptimizationAdaptation Replan(
        OptimizationSupportCode supportCode) =>
        new(OpenVinoOptimizationAdaptationStatus.ReplanRequired, null, supportCode);
}
