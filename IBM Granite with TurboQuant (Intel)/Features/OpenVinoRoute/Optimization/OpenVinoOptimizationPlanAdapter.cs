using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
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
    private const int ExecutorContractVersion =
        OptimizationExecutionPlan.CurrentContractVersion;

    public static OpenVinoOptimizationAdaptation Adapt(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot currentCapabilities,
        OpenVinoOptimizationCapabilityEvidence currentEvidence,
        string currentSourceSha256,
        ulong currentSourceLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentCapabilities);
        ArgumentNullException.ThrowIfNull(currentEvidence);

        if (!plan.IsExecutableBy(ExecutorContractVersion) ||
            plan.ContractVersion is < 2 or > ExecutorContractVersion ||
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

        if (!CurrentEvidenceMatches(payload, currentPayload, currentEvidence))
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
            return plan.MatchesExecutionPayload(executionPayload);
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

    private static bool CurrentEvidenceMatches(
        OpenVinoExecutionPayload payload,
        OpenVinoCapabilityPayload currentPayload,
        OpenVinoOptimizationCapabilityEvidence currentEvidence)
    {
        if (currentEvidence.Builds is null || currentEvidence.Versions is null ||
            currentEvidence.Admitted is null ||
            currentEvidence.Builds.TurboQuantBuild is not null ||
            !BuildsMatch(payload.BuildIdentity, currentEvidence.Builds) ||
            !ToolVersionsMatch(payload.OptimizerVersions, currentEvidence.Versions))
        {
            return false;
        }

        try
        {
            OpenVinoCapabilityPayload projected =
                OpenVinoOptimizationCapabilityProjector.Project(currentEvidence);
            return PayloadsMatch(projected, currentPayload);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          OpenVinoOptimizationException)
        {
            return false;
        }
    }

    private static bool BuildsMatch(
        OpenVinoBuildIdentity payload,
        OpenVinoBuildEvidence current) =>
        string.Equals(payload.RuntimeBuild, current.RuntimeBuild,
            StringComparison.Ordinal) &&
        string.Equals(payload.GenAiBuild, current.GenAiBuild,
            StringComparison.Ordinal) &&
        string.Equals(payload.TokenizersBuild, current.TokenizersBuild,
            StringComparison.Ordinal) &&
        string.Equals(payload.WorkerManifestDigest, current.WorkerManifestDigest,
            StringComparison.Ordinal);

    private static bool ToolVersionsMatch(
        IReadOnlyDictionary<string, string> payload,
        OpenVinoOptimizationToolVersions current) =>
        payload.Count == 6 &&
        Matches(payload, "openvino", current.OpenVino) &&
        Matches(payload, "openvino-genai", current.OpenVinoGenAi) &&
        Matches(payload, "nncf", current.Nncf) &&
        Matches(payload, "optimum", current.Optimum) &&
        Matches(payload, "optimum-intel", current.OptimumIntel) &&
        Matches(payload, "transformers", current.Transformers);

    private static bool Matches(
        IReadOnlyDictionary<string, string> payload,
        string key,
        string current) =>
        payload.TryGetValue(key, out string? value) &&
        string.Equals(value, current, StringComparison.Ordinal);

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
