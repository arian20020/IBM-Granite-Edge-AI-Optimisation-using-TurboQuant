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
        if (!plan.IsExecutableBy(ExecutorContractVersion))
        {
            return Replan(OptimizationSupportCode.ModelBindingMismatch);
        }

        return Adapt(
            plan,
            currentCapabilities,
            [currentEvidence],
            currentSourceSha256,
            currentSourceLengthBytes);
    }

    internal static OpenVinoOptimizationAdaptation Adapt(
        OptimizationExecutionPlan plan,
        OptimizationCapabilitySnapshot currentCapabilities,
        IReadOnlyList<OpenVinoOptimizationCapabilityEvidence> currentEvidenceSet,
        string currentSourceSha256,
        ulong currentSourceLengthBytes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentCapabilities);
        ArgumentNullException.ThrowIfNull(currentEvidenceSet);

        if (!plan.IsExecutableBy(ExecutorContractVersion) ||
            plan.ContractVersion is < 2 or > ExecutorContractVersion ||
            plan.Route != OptimizationRoute.OpenVino ||
            plan.Candidate.Route != OptimizationRoute.OpenVino ||
            plan.Candidate.Configuration is not OpenVinoRouteConfiguration configuration ||
            plan.CapabilitySnapshot.Route != OptimizationRoute.OpenVino ||
            plan.CapabilitySnapshot.OpenVino is not { } plannedCapabilities ||
            plan.ExecutionPayload is not { Route: OptimizationRoute.OpenVino } execution ||
            execution.OpenVino is not { } payload || execution.Gguf is not null ||
            !HasCoherentCacheFamily(payload))
        {
            return Replan(OptimizationSupportCode.ModelBindingMismatch);
        }

        // Preflight admits only the exact pinned build closure checked below.
        // Execution must independently validate and generate on that worker
        // before publishing either a package or a runtime profile. the worker
        // conversation requires fresh, codec-bound TurboQuant activation before
        // accepting a completed TurboQuant generation turn.

        if (currentCapabilities.Route != OptimizationRoute.OpenVino ||
            currentCapabilities.OpenVino is not { } currentPayload ||
            !string.Equals(currentCapabilities.SnapshotId,
                plan.CapabilitySnapshot.SnapshotId, StringComparison.Ordinal) ||
            !plan.MatchesCapability(currentCapabilities) ||
            !PayloadsMatch(plannedCapabilities, currentPayload))
        {
            return Replan(OptimizationSupportCode.CapabilityDrift);
        }

        if (!CurrentEvidenceMatches(payload, currentPayload, currentEvidenceSet))
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
            ExecutionWeightPrecision.MxFp4 => OpenVinoWeightFormat.MxFp4,
            _ => OpenVinoWeightFormat.Unspecified
        };
        OpenVinoKvCacheFormat kv = payload.KvCachePrecision switch
        {
            ExecutionKvPrecision.ReleasedDefault => OpenVinoKvCacheFormat.RouteDefault,
            ExecutionKvPrecision.U8 => OpenVinoKvCacheFormat.U8,
            ExecutionKvPrecision.U4 => OpenVinoKvCacheFormat.U4,
            ExecutionKvPrecision.Tbq4 => OpenVinoKvCacheFormat.TurboQuantTbq4,
            ExecutionKvPrecision.Tbq3 => OpenVinoKvCacheFormat.TurboQuantTbq3,
            _ => OpenVinoKvCacheFormat.Unspecified
        };
        ContractCompiledCachePolicy cache = payload.CompiledCacheEnabled
            ? ContractCompiledCachePolicy.Enabled
            : ContractCompiledCachePolicy.Disabled;
        bool runtimeOnlyConfiguration =
            configuration.Weights == OpenVinoWeightFormat.Original &&
            payload.SourceWeightPrecision == payload.TargetWeightPrecision;
        bool turboQuant = payload.KvCacheAlgorithm == OpenVinoKvCacheAlgorithm.TurboQuant;
        bool experimental = admission.Level == SupportLevel.Experimental;
        string expectedMaturity = experimental
            ? "Experimental candidate"
            : "Standard candidate";

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
            payload.Maturity == expectedMaturity &&
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
            admission.RequiresEvidence == experimental &&
            plan.Candidate.IsExperimental == experimental &&
            (turboQuant == experimental || turboQuant && !experimental
                && GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence.VerifiedOpenVinoOptimizationEvidence
                    .IsReleasedTurboQuantEvidence(plan.Candidate.Evidence, payload)) &&
            (payload.TurboQuantBuild is not null) == turboQuant;
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
            ExecutionKvPrecision.U4 => RouteKvPrecision.U4,
            ExecutionKvPrecision.Tbq4 => RouteKvPrecision.Tbq4,
            ExecutionKvPrecision.Tbq3 => RouteKvPrecision.Tbq3,
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
        ExecutionWeightPrecision.MxFp4 => RouteWeightPrecision.MxFp4,
        _ => (RouteWeightPrecision)(-1)
    };

    private static bool CurrentEvidenceMatches(
        OpenVinoExecutionPayload payload,
        OpenVinoCapabilityPayload currentPayload,
        IReadOnlyList<OpenVinoOptimizationCapabilityEvidence> currentEvidenceSet)
    {
        if (currentEvidenceSet.Count == 0 || currentEvidenceSet.Any(
                static evidence => evidence is null || evidence.Builds is null ||
                    evidence.Versions is null || evidence.Admitted is null))
        {
            return false;
        }

        try
        {
            OpenVinoOptimizationCapabilityEvidence[] matching = currentEvidenceSet
                .Where(evidence =>
                    BuildsMatch(payload.BuildIdentity, evidence.Builds) &&
                    ToolVersionsMatch(payload.OptimizerVersions, evidence.Versions) &&
                    TurboQuantBuildsMatch(payload, evidence.Builds))
                .ToArray();
            if (matching.Length != 1)
            {
                return false;
            }

            OpenVinoCapabilityPayload[] projected = currentEvidenceSet
                .Select(OpenVinoOptimizationCapabilityProjector.Project)
                .ToArray();
            return projected.Select((item, index) => string.Equals(
                    item.RuntimeVersion, currentPayload.RuntimeVersion,
                    StringComparison.Ordinal)
                    || OpenVinoOptimizationCapabilityProjector.HasExactTurboQuantBuild(
                        currentEvidenceSet[index].Builds)).All(static matches => matches) &&
                projected.Any(item => string.Equals(item.RuntimeVersion,
                    currentPayload.RuntimeVersion, StringComparison.Ordinal)) &&
                projected.SelectMany(static item => item.Admitted)
                    .SequenceEqual(currentPayload.Admitted);
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

    private static bool TurboQuantBuildsMatch(
        OpenVinoExecutionPayload payload,
        OpenVinoBuildEvidence current)
    {
        bool turbo = payload.KvCacheAlgorithm == OpenVinoKvCacheAlgorithm.TurboQuant;
        if (!turbo)
        {
            return payload.TurboQuantBuild is null && current.TurboQuantBuild is null;
        }
        if (payload.TurboQuantBuild is not { } expected ||
            current.TurboQuantBuild is not { } actual)
        {
            return false;
        }
        return string.Equals(expected.SourceCommit, actual.SourceCommit,
                   StringComparison.Ordinal) &&
            string.Equals(expected.ImplementationCommit, actual.ImplementationCommit,
                StringComparison.Ordinal) &&
            string.Equals(expected.PatchSeriesDigest, actual.PatchSeriesDigest,
                StringComparison.Ordinal) &&
            string.Equals(expected.RuntimeManifestDigest, actual.RuntimeManifestDigest,
                StringComparison.Ordinal);
    }

    private static bool HasCoherentCacheFamily(OpenVinoExecutionPayload payload)
    {
        bool turboPrecision = payload.KvCachePrecision is
            ExecutionKvPrecision.Tbq4 or ExecutionKvPrecision.Tbq3;
        bool turboAlgorithm =
            payload.KvCacheAlgorithm == OpenVinoKvCacheAlgorithm.TurboQuant;
        return turboPrecision == turboAlgorithm &&
            (payload.TurboQuantBuild is not null) == turboAlgorithm;
    }

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
