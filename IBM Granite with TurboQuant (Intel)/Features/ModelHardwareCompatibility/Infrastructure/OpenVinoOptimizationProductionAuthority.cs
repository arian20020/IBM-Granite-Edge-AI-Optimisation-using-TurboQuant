using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using ContractCache = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using ContractKv = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision;
using ContractWeight = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;
using RouteCache = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;
using RouteKv = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoKvCachePrecision;
using RouteWeight = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoWeightPrecision;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal sealed class OpenVinoOptimizationProductionAuthority
    : ICompatibilityActionAuthority
{
    private readonly object _gate = new();
    private readonly PreparedOpenVinoCompatibilityInput _prepared;
    private readonly OptimizationCapabilitySnapshot _snapshot;
    private readonly OptimizationWorkload _workload;
    private readonly OptimizationJourneyBinding _binding;
    private readonly OpenVinoComposer _composer;
    private readonly OptimizationExecutionPayload _currentPayload;
    private readonly bool _optimizationAvailable;
    private OptimizationIssuanceAuthority? _issuance;

    private OpenVinoOptimizationProductionAuthority(
        PreparedOpenVinoCompatibilityInput prepared,
        OpenVinoOptimizationCapabilityEvidence evidence,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        OpenVinoComposer composer,
        OptimizationExecutionPayload currentPayload,
        bool optimizationAvailable)
    {
        _prepared = prepared;
        CapabilityEvidence = evidence;
        _snapshot = snapshot;
        _workload = workload;
        _binding = binding;
        _composer = composer;
        _currentPayload = currentPayload;
        _optimizationAvailable = optimizationAvailable;
    }

    internal OpenVinoOptimizationCapabilityEvidence CapabilityEvidence { get; }
    internal OptimizationCapabilitySnapshot CapabilitySnapshot => _snapshot;

    internal static bool TryCreate(
        PreparedOpenVinoCompatibilityInput prepared,
        OpenVinoBuildEvidence builds,
        bool optimizationAvailable,
        out OpenVinoOptimizationProductionAuthority? authority)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(builds);
        authority = null;
        try
        {
            OpenVinoOptimizationToolVersions versions = new(
                "2026.3.0", "2026.3.0.0", "3.3.0", "2.3.0", "2.1.0", "5.5.4");
            OpenVinoOptimizationCapabilityAdmission[] allAdmissions =
            [
                Admission("OV-STD-CPU-ORIGINAL-01", RouteWeight.Original,
                    RouteKv.ReleasedDefault),
                Admission("OV-STD-CPU-FP16-01", RouteWeight.Fp16,
                    RouteKv.ReleasedDefault),
                Admission("OV-STD-CPU-AUTO-01", RouteWeight.EightBit,
                    RouteKv.ReleasedDefault),
                Admission("OV-STD-CPU-INT8-U8-01", RouteWeight.EightBit,
                    RouteKv.U8),
                Admission("OV-STD-CPU-INT4-DEFAULT-01", RouteWeight.FourBit,
                    RouteKv.ReleasedDefault),
                Admission("OV-STD-CPU-INT4-U8-01", RouteWeight.FourBit,
                    RouteKv.U8),
            ];
            OpenVinoOptimizationCapabilityAdmission[] admissions =
                [.. allAdmissions.Where(item => IsReachableFrom(
                    prepared.SourceWeightPrecision,
                    item.WeightPrecision))];
            var evidence = new OpenVinoOptimizationCapabilityEvidence(
                builds, versions, admissions);
            OpenVinoCapabilityPayload projected =
                OpenVinoOptimizationCapabilityProjector.Project(evidence);
            OpenVinoBuildIdentity buildIdentity = OpenVinoBuildIdentity.Create(
                builds.RuntimeBuild, builds.GenAiBuild, builds.TokenizersBuild,
                builds.WorkerManifestDigest);
            IReadOnlyDictionary<string, string> versionMap = VersionMap(versions);
            OpenVinoExecutionAuthority[] execution =
            [
                .. projected.Admitted.Select(item => OpenVinoExecutionAuthority.Create(
                    item.EvidenceId,
                    ConfigurationId(item.Weights, item.KvCache),
                    prepared.SourceWeightPrecision,
                    buildIdentity,
                    versionMap,
                    compiledCacheIsDisposable: true))
            ];
            OpenVinoCapabilityPayload payload = OpenVinoCapabilityPayload.Create(
                projected.RuntimeVersion, projected.Admitted, execution);
            string digest = Digest(builds, versions, projected.Admitted);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    $"openvino-cpu-{digest[..12]}", digest, payload);
            int maximumContext = Math.Clamp(
                prepared.Model.DeclaredContextLimit ?? 4096, 512, 32768);
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "local-chat", 512, OptimizationAssessment.Poor,
                [.. new[] { 512, 2048, 4096, maximumContext }
                    .Where(value => value <= maximumContext)
                    .Distinct().Order()
                    .Select(ContextTokenCount.FromTokens)]);
            OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
                prepared.ModelInspectionRunId.ToString("N"),
                prepared.ModelInspectionHandoffId.ToString("N"),
                prepared.ModelSha256,
                prepared.Model.PackageLengthBytes,
                prepared.ProductHardwareRunId.ToString("N"),
                prepared.HardwareSnapshotSha256);
            string currentEvidenceId = CurrentEvidenceId(prepared.Configuration.Weights);
            var composer = new OpenVinoComposer(execution, currentEvidenceId,
                prepared.SourceWeightPrecision);
            authority = new OpenVinoOptimizationProductionAuthority(
                prepared, evidence, snapshot, workload, binding, composer,
                composer.ComposeCurrent(), optimizationAvailable);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            authority = null;
            return false;
        }
    }

    internal CompatibilityEvaluation Evaluate(
        CompatibilityFreshResourcesInput fresh,
        IReadOnlySet<string> optedInEvidence,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken)
    {
        fresh = CompatibilityFreshResourceNormalizer.ConstrainTo(
            _prepared.Hardware,
            fresh);
        CompatibilityOptimizationProductionInput optimization =
            CompatibilityOptimizationProductionInput.Create(
                _snapshot, _workload, _binding, optedInEvidence);
        CompatibilityJourneyAuthorityInput journey =
            CompatibilityJourneyAuthorityInput.Create(
                _prepared.ModelInspectionHandoffId,
                _prepared.ModelSha256,
                CompatibilityFactDigest.ComputeModel(_prepared.CurrentModel),
                _prepared.HardwareSnapshotSha256,
                CompatibilityFactDigest.ComputeHardware(_prepared.Hardware));
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            _prepared.ModelInspectionRunId,
            _prepared.ProductHardwareRunId,
            _prepared.CurrentModel,
            journey,
            _prepared.Hardware,
            fresh,
            optimization);
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            input, new FixedTimeProvider(evaluatedAtUtc), cancellationToken);
        OptimizationIssuanceAuthority? issuance = null;
        try
        {
            issuance = CompatibilityEngine.CreateOptimizationIssuanceAuthority(
                input, evaluatedAtUtc);
        }
        catch (ArgumentException) { }
        lock (_gate) { _issuance = issuance; }
        return evaluation;
    }

    internal OpenVinoOptimizationCurrentState CurrentState => new(
        _snapshot,
        CapabilityEvidence,
        _prepared.ModelInspectionRunId.ToString("N"),
        _prepared.ModelInspectionHandoffId.ToString("N"),
        _prepared.ProductHardwareRunId.ToString("N"),
        _prepared.HardwareSnapshotSha256);

    internal bool MatchesCapability(OptimizationExecutionPlan plan) =>
        plan.Route == OptimizationRoute.OpenVino
        && plan.MatchesCapability(_snapshot)
        && plan.Binding == _binding;

    internal CurrentModelLaunchHandoff? ResolveCurrentModel(
        CompatibilityEvaluation evaluation,
        CurrentModelChatLaunchRegistry registry)
    {
        if (evaluation.Screen.State != CompatibilityScreenState.EstimatedCompatible)
        {
            return null;
        }
        string configurationSha =
            _currentPayload.ComputeRuntimeConfigurationSha256();
        var current = new CurrentCompatibleConfiguration(
            OptimizationRoute.OpenVino,
            _currentPayload,
            configurationSha,
            $"compat-{configurationSha[..24]}");
        CurrentModelLaunchHandoff handoff = CurrentModelLaunchHandoff.Create(
            OptimizationRoute.OpenVino,
            _prepared.ModelInspectionRunId,
            _prepared.ModelInspectionHandoffId,
            _prepared.ModelSha256,
            checked((long)_prepared.Model.PackageLengthBytes),
            _prepared.ProductHardwareRunId,
            _prepared.HardwareSnapshotSha256,
            current);
        ModelSourceCustodyKey sourceKey = new(
            _prepared.ModelInspectionHandoffId,
            _prepared.ModelSha256,
            checked((long)_prepared.Model.PackageLengthBytes),
            OptimizationRoute.OpenVino);
        return registry.RegisterContext(CurrentModelLaunchContext.Create(
            handoff, _currentPayload, sourceKey)) ? handoff : null;
    }

    public bool TryGetOptimizationAuthority(
        OptimizationRoute route,
        out IOptimizationExecutionPayloadComposer? composer,
        out OptimizationIssuanceAuthority? issuanceAuthority)
    {
        lock (_gate)
        {
            bool available = _optimizationAvailable
                && route == OptimizationRoute.OpenVino;
            composer = available ? _composer : null;
            issuanceAuthority = available ? _issuance : null;
            return composer is not null && issuanceAuthority is not null;
        }
    }

    public bool IsCurrentModelChatAvailable(OptimizationRoute route) =>
        route == OptimizationRoute.OpenVino;

    private static OpenVinoOptimizationCapabilityAdmission Admission(
        string evidenceId, RouteWeight weights, RouteKv cache) => new(
        evidenceId, "CPU", weights,
        new OpenVinoRuntimeOptimization(cache, RouteCache.Disabled),
        OpenVinoCapabilityPerformanceHint.Latency, 1, 512, 32768,
        OpenVinoCapabilityMaturity.Released);

    private static bool IsReachableFrom(
        ContractWeight source,
        RouteWeight target) => source switch
        {
            ContractWeight.Fp16 => true,
            ContractWeight.EightBit => target is RouteWeight.EightBit or RouteWeight.FourBit,
            ContractWeight.FourBit => target == RouteWeight.FourBit,
            _ => false,
        };

    private static string CurrentEvidenceId(OpenVinoWeightFormat weights) =>
        weights switch
        {
            OpenVinoWeightFormat.Original => "OV-STD-CPU-ORIGINAL-01",
            OpenVinoWeightFormat.Fp16 => "OV-STD-CPU-FP16-01",
            OpenVinoWeightFormat.Int8 => "OV-STD-CPU-AUTO-01",
            OpenVinoWeightFormat.Int4 => "OV-STD-CPU-INT4-DEFAULT-01",
            _ => throw new ArgumentOutOfRangeException(nameof(weights)),
        };

    private static IReadOnlyDictionary<string, string> VersionMap(
        OpenVinoOptimizationToolVersions value) =>
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["openvino"] = value.OpenVino,
            ["openvino-genai"] = value.OpenVinoGenAi,
            ["nncf"] = value.Nncf,
            ["optimum"] = value.Optimum,
            ["optimum-intel"] = value.OptimumIntel,
            ["transformers"] = value.Transformers,
        };

    private static string ConfigurationId(
        OpenVinoWeightFormat weights, OpenVinoKvCacheFormat cache) =>
        (weights, cache) switch
        {
            (OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.original.default.v1",
            (OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.fp16.default.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.int8.default.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.int8.u8.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.int4.default.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.int4.u8.v1",
            _ => throw new ArgumentOutOfRangeException(nameof(weights)),
        };

    private static string Digest(
        OpenVinoBuildEvidence builds,
        OpenVinoOptimizationToolVersions versions,
        IEnumerable<OpenVinoAdmittedConfiguration> admitted)
    {
        var canonical = new StringBuilder("openvino-capability-v1|")
            .Append(builds.RuntimeBuild).Append('|')
            .Append(builds.GenAiBuild).Append('|')
            .Append(builds.TokenizersBuild).Append('|')
            .Append(builds.WorkerManifestDigest);
        foreach (KeyValuePair<string, string> version in VersionMap(versions))
        {
            canonical.Append('|').Append(version.Key).Append('=').Append(version.Value);
        }
        foreach (OpenVinoAdmittedConfiguration item in admitted)
        {
            canonical.Append('|').Append(item.EvidenceId).Append(':')
                .Append((int)item.Weights).Append(':').Append((int)item.KvCache);
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private sealed class OpenVinoComposer(
        IEnumerable<OpenVinoExecutionAuthority> authorities,
        string currentEvidenceId,
        ContractWeight currentWeightPrecision)
        : IOptimizationExecutionPayloadComposer
    {
        private readonly IReadOnlyDictionary<string, OpenVinoExecutionAuthority>
            _authorities = authorities.ToDictionary(
                value => value.EvidenceId, StringComparer.Ordinal);

        public OptimizationRoute Route => OptimizationRoute.OpenVino;

        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            if (candidate.Route != Route
                || candidate.Configuration is not OpenVinoRouteConfiguration config
                || !_authorities.TryGetValue(
                    candidate.EvidenceId, out OpenVinoExecutionAuthority? exact))
            {
                throw new ArgumentException("The OpenVINO candidate is not admitted.",
                    nameof(candidate));
            }
            ContractWeight target = config.Weights switch
            {
                OpenVinoWeightFormat.Original or OpenVinoWeightFormat.Fp16 =>
                    ContractWeight.Fp16,
                OpenVinoWeightFormat.Int8 => ContractWeight.EightBit,
                OpenVinoWeightFormat.Int4 => ContractWeight.FourBit,
                _ => throw new ArgumentOutOfRangeException(nameof(candidate)),
            };
            ContractKv cache = config.KvCache switch
            {
                OpenVinoKvCacheFormat.RouteDefault => ContractKv.ReleasedDefault,
                OpenVinoKvCacheFormat.U8 => ContractKv.U8,
                _ => throw new ArgumentOutOfRangeException(nameof(candidate)),
            };
            return CreatePayload(exact, target, cache,
                config.CompiledCache == ContractCache.Enabled);
        }

        internal OptimizationExecutionPayload ComposeCurrent()
        {
            OpenVinoExecutionAuthority exact =
                _authorities[currentEvidenceId];
            return CreatePayload(exact, currentWeightPrecision,
                ContractKv.ReleasedDefault, compiledCache: false);
        }

        private static OptimizationExecutionPayload CreatePayload(
            OpenVinoExecutionAuthority exact,
            ContractWeight target,
            ContractKv cache,
            bool compiledCache)
        {
            bool persistent = target != exact.SourceWeightPrecision;
            return OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    exact.ConfigurationId, "CPU", "Standard candidate",
                    exact.EvidenceId, exact.SourceWeightPrecision, target, cache,
                    compiledCache,
                    exact.CompiledCacheIsDisposable,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: persistent,
                    exact.BuildIdentity,
                    exact.OptimizerVersions,
                    exact.TurboQuantBuild));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
