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
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
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
    private readonly IReadOnlyList<OpenVinoOptimizationCapabilityEvidence>
        _capabilityEvidenceSet;
    private readonly OptimizationEvidenceCatalog _qualityEvidence;
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
        bool optimizationAvailable,
        IReadOnlyList<OpenVinoOptimizationCapabilityEvidence> capabilityEvidenceSet,
        OptimizationEvidenceCatalog qualityEvidence)
    {
        _prepared = prepared;
        CapabilityEvidence = evidence;
        _snapshot = snapshot;
        _workload = workload;
        _binding = binding;
        _composer = composer;
        _currentPayload = currentPayload;
        _optimizationAvailable = optimizationAvailable;
        _capabilityEvidenceSet = capabilityEvidenceSet;
        _qualityEvidence = qualityEvidence;
    }

    internal OpenVinoOptimizationCapabilityEvidence CapabilityEvidence { get; }
    internal OptimizationCapabilitySnapshot CapabilitySnapshot => _snapshot;

    internal static bool TryCreate(
        PreparedOpenVinoCompatibilityInput prepared,
        OpenVinoBuildEvidence builds,
        bool optimizationAvailable,
        out OpenVinoOptimizationProductionAuthority? authority)
        => TryCreate(
            prepared,
            builds,
            turboQuantBuilds: null,
            optimizationAvailable,
            out authority);

    internal static bool TryCreate(
        PreparedOpenVinoCompatibilityInput prepared,
        OpenVinoBuildEvidence builds,
        OpenVinoBuildEvidence? turboQuantBuilds,
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
                Admission("OV-STD-CPU-INT8-U4-01", RouteWeight.EightBit,
                    RouteKv.U4),
                Admission("OV-STD-CPU-INT4-DEFAULT-01", RouteWeight.FourBit,
                    RouteKv.ReleasedDefault),
                Admission("OV-STD-CPU-INT4-U8-01", RouteWeight.FourBit,
                    RouteKv.U8),
                Admission("OV-STD-CPU-INT4-U4-01", RouteWeight.FourBit,
                    RouteKv.U4),
                Admission("OV-STD-CPU-MXFP4-DEFAULT-01", RouteWeight.MxFp4,
                    RouteKv.ReleasedDefault),
            ];
            OpenVinoOptimizationCapabilityAdmission[] admissions =
                [.. allAdmissions.Where(item => IsReachableFrom(
                    prepared.SourceWeightPrecision,
                    item.WeightPrecision))];
            var evidence = new OpenVinoOptimizationCapabilityEvidence(
                builds, versions, admissions);
            OpenVinoCapabilityPayload released =
                OpenVinoOptimizationCapabilityProjector.Project(evidence);
            OpenVinoOptimizationCapabilityEvidence? turboEvidence =
                turboQuantBuilds is null
                ? null
                : new OpenVinoOptimizationCapabilityEvidence(
                    turboQuantBuilds,
                    versions,
                    [
                        .. TurboAdmissions().Where(item => IsReachableFrom(
                            prepared.SourceWeightPrecision,
                            item.WeightPrecision))
                    ]);
            OpenVinoCapabilityPayload? turbo = turboEvidence is null
                ? null
                : OpenVinoOptimizationCapabilityProjector.Project(turboEvidence);
            OpenVinoAdmittedConfiguration[] projectedAdmissions =
                turbo is null
                    ? [.. released.Admitted]
                    : [.. released.Admitted, .. turbo.Admitted];
            IReadOnlyDictionary<string, string> versionMap = VersionMap(versions);
            OpenVinoExecutionAuthority[] execution =
            [
                .. CreateExecutionAuthorities(
                    released.Admitted,
                    builds,
                    prepared.SourceWeightPrecision,
                    versionMap),
                .. (turbo is null
                    ? []
                    : CreateExecutionAuthorities(
                        turbo.Admitted,
                        turboQuantBuilds!,
                        prepared.SourceWeightPrecision,
                        versionMap))
            ];
            string currentEvidenceId = CurrentEvidenceId(prepared.Configuration.Weights);
            if (prepared.Configuration.KvCache != OpenVinoKvCacheFormat.RouteDefault)
            {
                bool needsTurbo = prepared.Configuration.KvCache is
                    OpenVinoKvCacheFormat.TurboQuantTbq3 or OpenVinoKvCacheFormat.TurboQuantTbq4;
                OpenVinoBuildEvidence? currentBuild = needsTurbo ? turboQuantBuilds : builds;
                if (currentBuild is null) return false;
                currentEvidenceId = "OV-CURRENT-RESTORED-01";
                var currentAdmission = OpenVinoAdmittedConfiguration.Create(currentEvidenceId,
                    DeviceRouteId.Cpu, OpenVinoWeightFormat.Original, prepared.Configuration.KvCache,
                    OpenVinoPerformanceHint.Latency, ContractCache.Disabled, 1, 4096, 4096,
                    needsTurbo ? SupportLevel.Experimental : SupportLevel.DeclaredSupported,
                    requiresEvidence: needsTurbo);
                projectedAdmissions = [.. projectedAdmissions, currentAdmission];
                execution = [.. execution, .. CreateExecutionAuthorities([currentAdmission], currentBuild,
                    prepared.SourceWeightPrecision, versionMap)];
            }
            OpenVinoCapabilityPayload projected = OpenVinoCapabilityPayload.Create(
                builds.RuntimeBuild, projectedAdmissions, execution);
            OptimizationEvidenceCatalog qualityEvidence =
                VerifiedOpenVinoOptimizationEvidence.CreateCatalog(
                    prepared.ModelSha256,
                    prepared.Model.ParameterCount,
                    execution,
                    prepared.PackageManifestSha256);
            string digest = Digest(
                builds, turboQuantBuilds, versions, projected.Admitted);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    $"openvino-cpu-{digest[..12]}", digest, projected);
            int maximumContext = Math.Clamp(
                prepared.Model.DeclaredContextLimit ?? 4096, 512, 32768);
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "local-chat", 512, OptimizationAssessment.Acceptable,
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
            var composer = new OpenVinoComposer(execution, currentEvidenceId,
                prepared.SourceWeightPrecision, prepared.Configuration.KvCache);
            authority = new OpenVinoOptimizationProductionAuthority(
                prepared, evidence, snapshot, workload, binding, composer,
                composer.ComposeCurrent(), optimizationAvailable,
                turboEvidence is null ? [evidence] : [evidence, turboEvidence],
                qualityEvidence);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or OpenVinoOptimizationException)
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
        HashSet<string> admittedEvidence = new(
            optedInEvidence,
            StringComparer.Ordinal);
        // Keep the exact, packaged TurboQuant candidates on the preference
        // frontier so stage 4 can preview their real weight/cache trade-offs.
        // Preview eligibility is not consent. Pass actual user consent separately
        // so both the planning session and view model reject unconsented execution
        foreach (OpenVinoAdmittedConfiguration admission in
            _snapshot.OpenVino!.Admitted)
        {
            if (admission.RequiresEvidence
                && admission.Level == SupportLevel.Experimental)
            {
                admittedEvidence.Add(admission.EvidenceId);
            }
        }
        CompatibilityOptimizationProductionInput optimization =
            CompatibilityOptimizationProductionInput.Create(
                _snapshot, _workload, _binding, admittedEvidence,
                _qualityEvidence, executionConsentedEvidenceIds: optedInEvidence);
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
        _prepared.HardwareSnapshotSha256)
    {
        CapabilityEvidenceSet = _capabilityEvidenceSet
    };

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
        // Identical runtime settings can belong to different inspections. Keep
        // each validated source/hardware journey distinct in the launch registry.
        string decisionSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{_prepared.ModelInspectionHandoffId:N}:{_prepared.ProductHardwareRunId:N}:{configurationSha}")))
            .ToLowerInvariant();
        var current = new CurrentCompatibleConfiguration(
            OptimizationRoute.OpenVino,
            _currentPayload,
            configurationSha,
            $"compat-{decisionSha}");
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

    private static IReadOnlyList<OpenVinoOptimizationCapabilityAdmission>
        TurboAdmissions() =>
    [
        TurboAdmission("OV-TBQ4-CPU-FP16-01", RouteWeight.Fp16, RouteKv.Tbq4),
        TurboAdmission("OV-TBQ3-CPU-FP16-01", RouteWeight.Fp16, RouteKv.Tbq3),
        TurboAdmission("OV-TBQ4-CPU-INT8-01", RouteWeight.EightBit, RouteKv.Tbq4),
        TurboAdmission("OV-TBQ3-CPU-INT8-01", RouteWeight.EightBit, RouteKv.Tbq3),
        TurboAdmission("OV-TBQ4-CPU-INT4-01", RouteWeight.FourBit, RouteKv.Tbq4),
        TurboAdmission("OV-TBQ3-CPU-INT4-01", RouteWeight.FourBit, RouteKv.Tbq3),
        TurboAdmission("OV-TBQ4-CPU-MXFP4-01", RouteWeight.MxFp4, RouteKv.Tbq4),
        TurboAdmission("OV-TBQ3-CPU-MXFP4-01", RouteWeight.MxFp4, RouteKv.Tbq3),
    ];

    private static OpenVinoOptimizationCapabilityAdmission TurboAdmission(
        string evidenceId, RouteWeight weights, RouteKv cache) => new(
        evidenceId, "CPU", weights,
        new OpenVinoRuntimeOptimization(cache, RouteCache.Disabled),
        OpenVinoCapabilityPerformanceHint.Latency, 1,
        weights == RouteWeight.FourBit ? 4096 : 512,
        weights == RouteWeight.FourBit ? 4096 : 32768,
        weights == RouteWeight.FourBit ? OpenVinoCapabilityMaturity.Released : OpenVinoCapabilityMaturity.Experimental);

    private static IEnumerable<OpenVinoExecutionAuthority>
        CreateExecutionAuthorities(
            IEnumerable<OpenVinoAdmittedConfiguration> admissions,
            OpenVinoBuildEvidence builds,
            ContractWeight sourceWeightPrecision,
            IReadOnlyDictionary<string, string> versionMap)
    {
        OpenVinoBuildIdentity buildIdentity = OpenVinoBuildIdentity.Create(
            builds.RuntimeBuild,
            builds.GenAiBuild,
            builds.TokenizersBuild,
            builds.WorkerManifestDigest);
        TurboQuantBuildIdentity? turboIdentity = builds.TurboQuantBuild is { } turbo
            ? TurboQuantBuildIdentity.Create(
                turbo.SourceCommit,
                turbo.ImplementationCommit,
                turbo.PatchSeriesDigest,
                turbo.RuntimeManifestDigest)
            : null;
        return admissions.Select(item => OpenVinoExecutionAuthority.Create(
            item.EvidenceId,
            ConfigurationId(item.Weights, item.KvCache),
            sourceWeightPrecision,
            buildIdentity,
            versionMap,
            compiledCacheIsDisposable: true,
            turboIdentity));
    }

    private static bool IsReachableFrom(
        ContractWeight source,
        RouteWeight target) => source switch
        {
            ContractWeight.Fp16 => true,
            ContractWeight.EightBit => target is RouteWeight.Original or RouteWeight.EightBit or RouteWeight.FourBit,
            ContractWeight.FourBit => target is RouteWeight.Original or RouteWeight.FourBit,
            ContractWeight.MxFp4 => target is RouteWeight.Original or RouteWeight.MxFp4,
            _ => false,
        };

    private static string CurrentEvidenceId(OpenVinoWeightFormat weights) =>
        weights switch
        {
            OpenVinoWeightFormat.Original => "OV-STD-CPU-ORIGINAL-01",
            OpenVinoWeightFormat.Fp16 => "OV-STD-CPU-FP16-01",
            OpenVinoWeightFormat.Int8 => "OV-STD-CPU-AUTO-01",
            OpenVinoWeightFormat.Int4 => "OV-STD-CPU-INT4-DEFAULT-01",
            OpenVinoWeightFormat.MxFp4 => "OV-STD-CPU-MXFP4-DEFAULT-01",
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
            (OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U4) =>
                "openvino.standard.cpu.original.u4.v1",
            (OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.original.u8.v1",
            (OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.TurboQuantTbq3) =>
                "openvino.turboquant.cpu.original.tbq3.v1",
            (OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.TurboQuantTbq4) =>
                "openvino.turboquant.cpu.original.tbq4.v1",
            (OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.fp16.default.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.int8.default.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.int8.u8.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U4) =>
                "openvino.standard.cpu.int8.u4.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.int4.default.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8) =>
                "openvino.standard.cpu.int4.u8.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U4) =>
                "openvino.standard.cpu.int4.u4.v1",
            (OpenVinoWeightFormat.MxFp4, OpenVinoKvCacheFormat.RouteDefault) =>
                "openvino.standard.cpu.mxfp4.default.v1",
            (OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.TurboQuantTbq4) =>
                "openvino.turboquant.cpu.fp16.tbq4.v1",
            (OpenVinoWeightFormat.Fp16, OpenVinoKvCacheFormat.TurboQuantTbq3) =>
                "openvino.turboquant.cpu.fp16.tbq3.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.TurboQuantTbq4) =>
                "openvino.turboquant.cpu.int8.tbq4.v1",
            (OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.TurboQuantTbq3) =>
                "openvino.turboquant.cpu.int8.tbq3.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.TurboQuantTbq4) =>
                "openvino.turboquant.cpu.int4.tbq4.v1",
            (OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.TurboQuantTbq3) =>
                "openvino.turboquant.cpu.int4.tbq3.v1",
            (OpenVinoWeightFormat.MxFp4, OpenVinoKvCacheFormat.TurboQuantTbq4) =>
                "openvino.turboquant.cpu.mxfp4.tbq4.v1",
            (OpenVinoWeightFormat.MxFp4, OpenVinoKvCacheFormat.TurboQuantTbq3) =>
                "openvino.turboquant.cpu.mxfp4.tbq3.v1",
            _ => throw new ArgumentOutOfRangeException(nameof(weights)),
        };

    private static string Digest(
        OpenVinoBuildEvidence builds,
        OpenVinoBuildEvidence? turboQuantBuilds,
        OpenVinoOptimizationToolVersions versions,
        IEnumerable<OpenVinoAdmittedConfiguration> admitted)
    {
        var canonical = new StringBuilder("openvino-capability-v1|")
            .Append(builds.RuntimeBuild).Append('|')
            .Append(builds.GenAiBuild).Append('|')
            .Append(builds.TokenizersBuild).Append('|')
            .Append(builds.WorkerManifestDigest);
        if (turboQuantBuilds is not null)
        {
            canonical.Append("|turbo-runtime=")
                .Append(turboQuantBuilds.RuntimeBuild)
                .Append("|turbo-genai=")
                .Append(turboQuantBuilds.GenAiBuild)
                .Append("|turbo-tokenizers=")
                .Append(turboQuantBuilds.TokenizersBuild)
                .Append("|turbo-worker=")
                .Append(turboQuantBuilds.WorkerManifestDigest);
        }
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
        ContractWeight currentWeightPrecision,
        OpenVinoKvCacheFormat currentCache)
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
                OpenVinoWeightFormat.MxFp4 => ContractWeight.MxFp4,
                _ => throw new ArgumentOutOfRangeException(nameof(candidate)),
            };
            ContractKv cache = config.KvCache switch
            {
                OpenVinoKvCacheFormat.RouteDefault => ContractKv.ReleasedDefault,
                OpenVinoKvCacheFormat.U8 => ContractKv.U8,
                OpenVinoKvCacheFormat.U4 => ContractKv.U4,
                OpenVinoKvCacheFormat.TurboQuantTbq4 => ContractKv.Tbq4,
                OpenVinoKvCacheFormat.TurboQuantTbq3 => ContractKv.Tbq3,
                _ => throw new ArgumentOutOfRangeException(nameof(candidate)),
            };
            return CreatePayload(exact, target, cache,
                config.CompiledCache == ContractCache.Enabled, candidate.IsExperimental);
        }

        internal OptimizationExecutionPayload ComposeCurrent()
        {
            OpenVinoExecutionAuthority exact =
                _authorities[currentEvidenceId];
            return CreatePayload(exact, currentWeightPrecision,
                currentCache switch
                {
                    OpenVinoKvCacheFormat.RouteDefault => ContractKv.ReleasedDefault,
                    OpenVinoKvCacheFormat.U4 => ContractKv.U4,
                    OpenVinoKvCacheFormat.U8 => ContractKv.U8,
                    OpenVinoKvCacheFormat.TurboQuantTbq3 => ContractKv.Tbq3,
                    OpenVinoKvCacheFormat.TurboQuantTbq4 => ContractKv.Tbq4,
                    _ => throw new ArgumentOutOfRangeException(nameof(currentCache))
                }, compiledCache: false,
                experimental: currentCache is OpenVinoKvCacheFormat.TurboQuantTbq3 or OpenVinoKvCacheFormat.TurboQuantTbq4);
        }

        private static OptimizationExecutionPayload CreatePayload(
            OpenVinoExecutionAuthority exact,
            ContractWeight target,
            ContractKv cache,
            bool compiledCache,
            bool experimental = false)
        {
            bool persistent = target != exact.SourceWeightPrecision;
            bool turboQuant = cache is ContractKv.Tbq4 or ContractKv.Tbq3;
            return OptimizationExecutionPayload.ForOpenVino(
                OpenVinoExecutionPayload.Create(
                    exact.ConfigurationId,
                    "CPU",
                    experimental ? "Experimental candidate" : "Standard candidate",
                    exact.EvidenceId, exact.SourceWeightPrecision, target, cache,
                    compiledCache,
                    exact.CompiledCacheIsDisposable,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: persistent,
                    exact.BuildIdentity,
                    exact.OptimizerVersions,
                    exact.TurboQuantBuild,
                    turboQuant
                        ? OpenVinoKvCacheAlgorithm.TurboQuant
                        : OpenVinoKvCacheAlgorithm.Released));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
