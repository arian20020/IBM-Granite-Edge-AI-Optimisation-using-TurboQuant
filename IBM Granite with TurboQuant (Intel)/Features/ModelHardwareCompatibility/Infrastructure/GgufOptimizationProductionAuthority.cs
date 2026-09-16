using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.GgufQuantization.WorkerClient;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal sealed class GgufOptimizationProductionAuthority
    : ICompatibilityActionAuthority
{
    private const string RuntimeManifestResource =
        "GraniteEdgeAI.GgufRuntime.Manifest.json";
    private const string ExpectedQuantizerManifestSha256 =
        "2d039fdba954b7c9b17f4e552e7a69d5082f988861cc08db9bee3263f293b1b8";
    private const string QuantizerPackageId =
        "granite-edge-ai-atomicbot-llama-quantize-x64";
    private const string QuantizerVersion =
        "atomicbot-llama-quantize-519f0c594a8e31467d2e2f2cf17054c9e7e11536";
    internal const string PackagedCpuDeviceId = "CPU";
    private readonly object _gate = new();
    private readonly PreparedGgufCompatibilityInput _prepared;
    private readonly CompatibilityHardwareInput _hardware;
    private readonly OptimizationCapabilitySnapshot _snapshot;
    private readonly OptimizationWorkload _workload;
    private readonly OptimizationJourneyBinding _binding;
    private readonly GgufExecutionPayloadComposer _composer;
    private readonly OptimizationExecutionPayload _currentPayload;
    private readonly WeightQuantisation _sourceWeights;
    private readonly OptimizationEvidenceCatalog _qualityEvidence;
    private OptimizationIssuanceAuthority? _issuanceAuthority;
    private CompatibilityEvaluation? _latestEvaluation;

    private GgufOptimizationProductionAuthority(
        PreparedGgufCompatibilityInput prepared,
        CompatibilityHardwareInput hardware,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        GgufExecutionPayloadComposer composer,
        OptimizationExecutionPayload currentPayload,
        WeightQuantisation sourceWeights,
        OptimizationEvidenceCatalog qualityEvidence)
    {
        _prepared = prepared;
        _hardware = hardware;
        _snapshot = snapshot;
        _workload = workload;
        _binding = binding;
        _composer = composer;
        _currentPayload = currentPayload;
        _sourceWeights = sourceWeights;
        _qualityEvidence = qualityEvidence;
    }

    internal string RuntimePackageRoot { get; private init; } = string.Empty;
    internal ReadOnlyMemory<byte> TrustedRuntimeManifest { get; private init; }
    internal string? QuantizerPackageRoot { get; private init; }
    internal string QuantizerManifestSha256 => ExpectedQuantizerManifestSha256;

    internal static bool TryCreate(
        PreparedGgufCompatibilityInput prepared,
        out GgufOptimizationProductionAuthority? authority)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        authority = null;
        try
        {
            byte[] manifestBytes = ReadRuntimeManifest();
            GgufRuntimeManifest manifest =
                GgufRuntimeManifestJson.Deserialize(manifestBytes);
            string runtimeManifestSha256 = Convert.ToHexString(
                SHA256.HashData(manifestBytes));
            string runtimeRoot = Path.Combine(
                AppContext.BaseDirectory,
                "GgufRuntime");
            if (!Directory.Exists(runtimeRoot))
            {
                return false;
            }

            CompatibilityHardwareInput hardware = WithPackagedCpuRuntime(
                prepared.Hardware);
            CompatibilityCurrentModelInput current =
                CompatibilityCurrentModelInput.ForGguf(
                    prepared.Model,
                    GgufRouteConfiguration.Create(
                        GgufWeightFormat.Imported,
                        GgufKvCacheFormat.F16,
                        CompatibilityBackend.Cpu,
                        DeviceRouteId.Cpu,
                        GpuOffloadLevel.None));
            string hardwareFacts = CompatibilityFactDigest.ComputeHardware(hardware);
            OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
                prepared.ModelInspectionRunId.ToString("N"),
                prepared.ModelInspectionHandoffId.ToString("N"),
                prepared.ModelSha256,
                prepared.Model.FileLengthBytes,
                prepared.ProductHardwareRunId.ToString("N"),
                prepared.HardwareSnapshotSha256);

            GgufConversionSourceBinding? source = null;
            _ = GgufConversionSourceBinding.TryCreateFromGgufInspection(
                prepared.Model.FileType,
                prepared.Model.QuantisationVersion,
                binding,
                out source);
            VerifiedQuantizer? verifiedQuantizer =
                TryVerifyQuantizer();
            GgufQuantiserIdentity? quantizer = verifiedQuantizer is null
                ? null
                : GgufQuantiserIdentity.Create(
                    QuantizerPackageId,
                    QuantizerVersion,
                    verifiedQuantizer.Package.ExecutableSha256);

            int declaredContext = prepared.Model.DeclaredContextLimit ?? 4096;
            if (declaredContext < 512)
            {
                return false;
            }
            int maximumContext = Math.Min(declaredContext, 32768);
            int evaluatedContext = Math.Min(4096, declaredContext);
            bool exactVerifiedSource =
                VerifiedGgufOptimizationEvidence.MatchesInspectedSource(
                    prepared.ModelSha256,
                    prepared.Model.FileLengthBytes,
                    prepared.Model.LayerCount,
                    prepared.Model.EmbeddingSize,
                    prepared.Model.AttentionHeadCount,
                    prepared.Model.KeyValueHeadCount,
                    prepared.Model.DeclaredContextLimit,
                    prepared.Model.FileType,
                    prepared.Model.QuantisationVersion);
            OptimizationEvidenceRecord[] verifiedRecords = exactVerifiedSource
                ? [.. VerifiedGgufOptimizationEvidence.Records(
                    prepared.ModelSha256,
                    prepared.Model.FileLengthBytes,
                    prepared.Model.ParameterCount,
                    runtimeManifestSha256,
                    manifest.RuntimeBuildId,
                    manifest.RuntimeSourceCommit)]
                : [];
            if (!VerifiedGgufOptimizationEvidence.MatchesBf16Q3Quantizer(quantizer))
            {
                verifiedRecords =
                [
                    .. verifiedRecords.Where(static record => record.EvidenceId is not
                        ("GGUF-V5-BF16-Q3-CPU-F16-01" or "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01")),
                ];
            }
            HashSet<string> verifiedEvidenceIds = verifiedRecords
                .Select(static record => record.EvidenceId)
                .ToHashSet(StringComparer.Ordinal);
            bool usesVerifiedEvidence = verifiedEvidenceIds.Count > 0;
            List<GgufAdmittedConfiguration> publishedAdmissions;
            CompatibilityBackend publishedBackend;
            DeviceRouteId publishedDevice;
            if (usesVerifiedEvidence)
            {
                publishedAdmissions =
                    [.. VerifiedGgufOptimizationEvidence.Admissions(
                        verifiedEvidenceIds)];
                if (publishedAdmissions.Count != verifiedRecords.Length)
                {
                    return false;
                }
                publishedBackend = CompatibilityBackend.Cpu;
                publishedDevice = DeviceRouteId.Cpu;
            }
            else
            {
                publishedAdmissions = PublishedAdmissions(
                    prepared.ModelSha256,
                    maximumContext,
                    prepared.Model.LayerCount ?? 0,
                    hardware,
                    out publishedBackend,
                    out publishedDevice);
            }
            List<GgufAdmittedConfiguration> admissions =
                ComposeCurrentCpuAdmissions(
                    maximumContext,
                    publishedAdmissions);
            admissions.AddRange(publishedAdmissions);
            bool usesPublishedAtomicBotEvidence = publishedAdmissions.Count > 0;
            GgufRequantisationPolicy? requantisation = null;
            if (!usesPublishedAtomicBotEvidence
                && source is not null
                && quantizer is not null)
            {
                GgufWeightFormat[] targets = DownwardTargets(source.Precision);
                foreach (GgufWeightFormat target in targets)
                {
                    admissions.Add(Admission(
                        $"gguf-{target.ToString().ToLowerInvariant()}-cpu",
                        target,
                        maximumContext));
                }

                if (IsAlreadyQuantized(source.Precision)
                    && admissions.Count > 1)
                {
                    // The V3 contract intentionally authorizes one exact
                    // requantisation target. use the smallest supported target;
                    // the UI labels and warns about its quality trade-off.
                    GgufAdmittedConfiguration target = admissions[^1];
                    admissions.RemoveRange(1, admissions.Count - 1);
                    admissions.Add(target);
                    requantisation = GgufRequantisationPolicy.Create(
                        explicitlyAcknowledged: true,
                        preserveOriginal: true,
                        requireNewOutput: true,
                        target,
                        quantizer,
                        source);
                }
            }

            int threads = Math.Clamp(Environment.ProcessorCount, 1, 16);
            IReadOnlyDictionary<string, GgufExecutionProfileAuthority>
                verifiedProfiles = VerifiedGgufOptimizationEvidence
                    .ExecutionProfiles(verifiedEvidenceIds)
                    .ToDictionary(static item => item.EvidenceId,
                        StringComparer.Ordinal);
            if (verifiedProfiles.Count != verifiedRecords.Length)
            {
                return false;
            }
            GgufExecutionProfileAuthority[] profiles =
            [
                .. admissions.Select(admission =>
                    usesVerifiedEvidence
                        && verifiedProfiles.TryGetValue(
                            admission.EvidenceId,
                            out GgufExecutionProfileAuthority? verifiedProfile)
                    ? verifiedProfile
                    : GgufExecutionProfileAuthority.Create(
                        admission.EvidenceId,
                        publishedAdmissions.Any(published => string.Equals(
                            published.EvidenceId,
                            admission.EvidenceId,
                            StringComparison.Ordinal))
                            ? EvidenceGrade.Measured
                            : EvidenceGrade.Estimated,
                        $"cpu-{admission.Weights.ToString().ToLowerInvariant()}",
                        flashAttention: false,
                        threadCount: threads,
                        batchSize: 128,
                        maximumGeneratedTokens: 512))
            ];
            GgufRuntimeAuthority runtimeAuthority = GgufRuntimeAuthority.Create(
                manifest.RuntimeBuildId,
                manifest.RuntimeSourceCommit,
                profiles);
            GgufCapabilityPayload payload = GgufCapabilityPayload.Create(
                manifest.RuntimeBuildId,
                admissions,
                hasHigherPrecisionSource: source?.Precision is
                    WeightQuantisation.F16 or WeightQuantisation.BF16
                    or WeightQuantisation.F32,
                requantisationPolicy: requantisation,
                conversionSource: source,
                admittedQuantiser: quantizer,
                turboQuantImplementation: admissions.Any(admission =>
                    IsTurboQuant(admission.Weights, admission.KvCache))
                    ? GgufTurboQuantImplementationIdentity.Create(
                        manifest.RuntimeBuildId,
                        manifest.RuntimeSourceCommit,
                        publishedBackend,
                        publishedDevice)
                    : null,
                runtimeAuthority: runtimeAuthority);
            string capabilityDigest = CapabilityDigest(
                manifestBytes,
                admissions,
                verifiedQuantizer);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForGguf(
                    $"gguf-{publishedBackend.ToString().ToLowerInvariant()}-"
                        + capabilityDigest[..12],
                    capabilityDigest,
                    payload);
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "local-chat",
                minimumContextTokens: 512,
                minimumQuality: OptimizationAssessment.Acceptable,
                CandidateContexts(maximumContext));
            var composer = new GgufExecutionPayloadComposer(
                runtimeAuthority,
                prepared.Model.LayerCount ?? 0,
                source,
                quantizer,
                requantisation);
            GgufAdmittedConfiguration[] currentAdmissions =
            [
                .. admissions.Where(admission =>
                    admission.Weights == GgufWeightFormat.Imported
                    && admission.KvCache == GgufKvCacheFormat.F16
                    && admission.Backend == CompatibilityBackend.Cpu
                    && admission.Device == DeviceRouteId.Cpu
                    && admission.Offload == GpuOffloadLevel.None
                    && admission.MinimumContextTokens <= evaluatedContext
                    && admission.MaximumContextTokens >= evaluatedContext
                    && runtimeAuthority.Profiles.ContainsKey(admission.EvidenceId))
            ];
            if (currentAdmissions.Length != 1)
            {
                return false;
            }
            OptimizationExecutionPayload currentPayload = composer.ComposeCurrent(
                evaluatedContext,
                currentAdmissions[0].EvidenceId);
            OptimizationEvidenceCatalog qualityEvidence = new(
            [
                .. PublishedGgufOptimizationEvidence.Records(),
                .. verifiedRecords,
            ]);

            authority = new GgufOptimizationProductionAuthority(
                prepared,
                hardware,
                snapshot,
                workload,
                binding,
                composer,
                currentPayload,
                source?.Precision ?? WeightQuantisation.Unknown,
                qualityEvidence)
            {
                RuntimePackageRoot = runtimeRoot,
                TrustedRuntimeManifest = manifestBytes,
                QuantizerPackageRoot = verifiedQuantizer?.Root,
            };
            return true;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or InvalidDataException
            or CryptographicException)
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
        lock (_gate)
        {
            _issuanceAuthority = null;
            _latestEvaluation = null;
        }
        fresh = CompatibilityFreshResourceNormalizer.ConstrainTo(
            _hardware,
            fresh);
        HashSet<string> admittedEvidence = new(
            optedInEvidence,
            StringComparer.Ordinal);
        foreach (GgufAdmittedConfiguration admission in _snapshot.Gguf!.Admitted)
        {
            if (admission.RequiresEvidence
                && admission.Level == SupportLevel.Experimental)
            {
                admittedEvidence.Add(admission.EvidenceId);
            }
        }
        CompatibilityOptimizationProductionInput optimization =
            CompatibilityOptimizationProductionInput.Create(
                _snapshot,
                _workload,
                _binding,
                admittedEvidence,
                _qualityEvidence,
                executionConsentedEvidenceIds: optedInEvidence);
        CompatibilityJourneyAuthorityInput journey =
            CompatibilityJourneyAuthorityInput.Create(
                _prepared.ModelInspectionHandoffId,
                _prepared.ModelSha256,
                CompatibilityFactDigest.ComputeModel(_prepared.CurrentModel),
                _prepared.HardwareSnapshotSha256,
                CompatibilityFactDigest.ComputeHardware(_hardware));
        CompatibilityProductionInput input = CompatibilityProductionInput.Create(
            _prepared.ModelInspectionRunId,
            _prepared.ProductHardwareRunId,
            _prepared.CurrentModel,
            journey,
            _hardware,
            fresh,
            optimization);
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            input,
            new FixedTimeProvider(evaluatedAtUtc),
            cancellationToken);
        OptimizationIssuanceAuthority? issuance = null;
        try
        {
            issuance = CompatibilityEngine.CreateOptimizationIssuanceAuthority(
                input,
                evaluatedAtUtc);
        }
        catch (ArgumentException)
        {
            // an exhausted safe budget produces no actionable plan
        }
        lock (_gate)
        {
            _issuanceAuthority = issuance;
            _latestEvaluation = evaluation;
        }
        return evaluation;
    }

    internal CurrentModelLaunchHandoff? ResolveCurrentModel(
        CompatibilityEvaluation evaluation,
        CurrentModelChatLaunchRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(registry);
        lock (_gate)
        {
            if (!ReferenceEquals(evaluation, _latestEvaluation)
                || !MatchesCurrentLaunchSetup(evaluation.Screen))
            {
                return null;
            }
            CurrentCompatibleConfiguration current =
                CurrentModelLaunchHandoff.CreateGgufConfiguration(
                    _prepared.ModelInspectionHandoffId,
                    _prepared.ProductHardwareRunId,
                    _currentPayload);
            CurrentModelLaunchHandoff handoff = CurrentModelLaunchHandoff.Create(
                OptimizationRoute.Gguf,
                _prepared.ModelInspectionRunId,
                _prepared.ModelInspectionHandoffId,
                _prepared.ModelSha256,
                checked((long)_prepared.Model.FileLengthBytes),
                _prepared.ProductHardwareRunId,
                _prepared.HardwareSnapshotSha256,
                current);
            ModelSourceCustodyKey sourceKey = new(
                _prepared.ModelInspectionHandoffId,
                _prepared.ModelSha256,
                checked((long)_prepared.Model.FileLengthBytes),
                OptimizationRoute.Gguf);
            CurrentModelLaunchContext context = CurrentModelLaunchContext.Create(
                handoff,
                _currentPayload,
                sourceKey);
            return registry.RegisterContext(context) ? handoff : null;
        }
    }

    private bool MatchesCurrentLaunchSetup(CompatibilityScreenModel screen)
    {
        if (screen.State != CompatibilityScreenState.EstimatedCompatible
            || !screen.UseCurrentModelAvailable
            || screen.CurrentSetup is not { } setup
            || _currentPayload.Gguf is not { } payload)
        {
            return false;
        }

        return _sourceWeights != WeightQuantisation.Unknown
            && setup.Route == RuntimeRouteId.LlamaCpp
            && setup.Backend == CompatibilityBackend.Cpu
            && setup.Device == DeviceRouteId.Cpu
            && setup.GgufKvCache == GgufKvCacheFormat.F16
            && setup.OpenVinoKvCache is null
            && setup.Weights == _sourceWeights
            && setup.ContextTokens == payload.ContextSize
            && setup.Fit is CompatibilityFitState.Safe or CompatibilityFitState.Narrow
            && !setup.IsExperimental
            && !setup.RequiresConversion
            && payload.Backend == GgufRuntimeBackend.Cpu
            && string.Equals(payload.DeviceId, PackagedCpuDeviceId, StringComparison.Ordinal)
            && payload.KeyCacheType == GgufCacheType.F16
            && payload.ValueCacheType == GgufCacheType.F16
            && payload.GpuLayerCount == 0
            && payload.PersistentTargetWeightFormat == GgufWeightFormat.Imported
            && !payload.RequiresPersistentConversion;
    }

    public bool TryGetOptimizationAuthority(
        OptimizationRoute route,
        out IOptimizationExecutionPayloadComposer? composer,
        out OptimizationIssuanceAuthority? issuanceAuthority)
    {
        lock (_gate)
        {
            composer = route == OptimizationRoute.Gguf ? _composer : null;
            issuanceAuthority = route == OptimizationRoute.Gguf
                ? _issuanceAuthority
                : null;
            return composer is not null && issuanceAuthority is not null;
        }
    }

    public bool IsCurrentModelChatAvailable(OptimizationRoute route) =>
        route == OptimizationRoute.Gguf;

    internal bool MatchesCapability(OptimizationExecutionPlan plan) =>
        plan.Route == OptimizationRoute.Gguf
        && plan.MatchesCapability(_snapshot)
        && plan.Binding == _binding;

    private static CompatibilityHardwareInput WithPackagedCpuRuntime(
        CompatibilityHardwareInput hardware) => CompatibilityHardwareInput.Create(
        hardware.TotalPhysicalMemory,
        hardware.InstalledDedicatedDeviceMemoryBytes,
        hardware.FreeStorageBytes,
        hardware.PresentDevices.Append(DeviceRouteId.Cpu),
        hardware.VerifiedBackends.Append(CompatibilityBackend.Cpu));

    private static GgufAdmittedConfiguration Admission(
        string evidenceId,
        GgufWeightFormat weights,
        int maximumContext) => Admission(
            evidenceId,
            weights,
            minimumContext: 512,
            maximumContext);

    private static GgufAdmittedConfiguration Admission(
        string evidenceId,
        GgufWeightFormat weights,
        int minimumContext,
        int maximumContext) => GgufAdmittedConfiguration.Create(
            evidenceId,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            weights,
            GgufKvCacheFormat.F16,
            GpuOffloadLevel.None,
            minimumContext,
            maximumContext,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

    internal static List<GgufAdmittedConfiguration> ComposeCurrentCpuAdmissions(
        int maximumContext,
        IReadOnlyList<GgufAdmittedConfiguration> publishedAdmissions)
    {
        ArgumentNullException.ThrowIfNull(publishedAdmissions);
        if (maximumContext < 512)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContext));
        }

        (int Minimum, int Maximum)[] covered =
        [
            .. publishedAdmissions
                .Where(admission =>
                    admission.Weights == GgufWeightFormat.Imported
                    && admission.KvCache == GgufKvCacheFormat.F16
                    && admission.Backend == CompatibilityBackend.Cpu
                    && admission.Device == DeviceRouteId.Cpu
                    && admission.Offload == GpuOffloadLevel.None
                    && admission.MaximumContextTokens >= 512
                    && admission.MinimumContextTokens <= maximumContext)
                .Select(admission => (
                    Math.Max(512, admission.MinimumContextTokens),
                    Math.Min(maximumContext, admission.MaximumContextTokens)))
                .OrderBy(interval => interval.Item1)
                .ThenBy(interval => interval.Item2)
        ];
        var merged = new List<(int Minimum, int Maximum)>();
        foreach ((int minimum, int maximum) in covered)
        {
            if (merged.Count == 0
                || minimum > (long)merged[^1].Maximum + 1L)
            {
                merged.Add((minimum, maximum));
                continue;
            }

            (int priorMinimum, int priorMaximum) = merged[^1];
            merged[^1] = (priorMinimum, Math.Max(priorMaximum, maximum));
        }

        var gaps = new List<(int Minimum, int Maximum)>();
        int nextMinimum = 512;
        foreach ((int minimum, int maximum) in merged)
        {
            if (nextMinimum < minimum)
            {
                gaps.Add((nextMinimum, minimum - 1));
            }
            if (maximum == int.MaxValue)
            {
                nextMinimum = int.MaxValue;
                break;
            }
            nextMinimum = Math.Max(nextMinimum, maximum + 1);
        }
        if (nextMinimum <= maximumContext)
        {
            gaps.Add((nextMinimum, maximumContext));
        }

        int primaryGap = gaps.FindIndex(gap =>
            gap.Minimum <= maximumContext && gap.Maximum >= maximumContext);
        if (primaryGap < 0 && gaps.Count > 0)
        {
            primaryGap = gaps.Count - 1;
        }

        var result = new List<GgufAdmittedConfiguration>(gaps.Count);
        int suffix = 1;
        for (int index = 0; index < gaps.Count; index++)
        {
            (int minimum, int maximum) = gaps[index];
            string evidenceId = index == primaryGap
                ? "gguf-current-cpu"
                : $"gguf-current-cpu-split-{suffix++}";
            result.Add(Admission(
                evidenceId,
                GgufWeightFormat.Imported,
                minimum,
                maximum));
        }
        return result;
    }

    private static List<GgufAdmittedConfiguration> PublishedAdmissions(
        string modelSha256,
        int maximumContext,
        int modelLayerCount,
        CompatibilityHardwareInput hardware,
        out CompatibilityBackend backend,
        out DeviceRouteId device)
    {
        bool preferVulkan = modelLayerCount > 0
            && hardware.VerifiedBackends.Contains(CompatibilityBackend.IntelVulkan)
            && hardware.PresentDevices.Contains(DeviceRouteId.IntelIntegratedGpu);
        backend = preferVulkan
            ? CompatibilityBackend.IntelVulkan
            : CompatibilityBackend.Cpu;
        device = preferVulkan
            ? DeviceRouteId.IntelIntegratedGpu
            : DeviceRouteId.Cpu;
        CompatibilityBackend selectedBackend = backend;
        DeviceRouteId selectedDevice = device;
        if (maximumContext < 4096)
        {
            return [];
        }

        return
        [
            .. PublishedGgufOptimizationEvidence.Records()
                .Where(record =>
                    string.Equals(
                        record.Key.ModelIdentitySha256,
                        modelSha256,
                        StringComparison.Ordinal)
                    && record.Key.Backend == (preferVulkan
                        ? OptimizationEvidenceBackend.Vulkan
                        : OptimizationEvidenceBackend.Cpu)
                    && record.Key.DeviceClass == (preferVulkan
                        ? OptimizationEvidenceDeviceClass.IntelIntegratedGpu
                        : OptimizationEvidenceDeviceClass.Cpu)
                    && record.Key.ContextTokens == 4096
                    && (preferVulkan
                        ? record.Key.ExecutionProfile is
                            "vulkan-partial" or "vulkan-full"
                        : string.Equals(
                            record.Key.ExecutionProfile,
                            "cpu",
                            StringComparison.Ordinal)))
                .OrderByDescending(record => record.Quality.Value)
                .Select(record =>
                {
                    GgufKvCacheFormat cache =
                        PublishedCache(record.Key.CacheConfiguration);
                    bool turbo = IsTurboQuant(GgufWeightFormat.Imported, cache);
                    return GgufAdmittedConfiguration.Create(
                        record.EvidenceId,
                        selectedBackend,
                        selectedDevice,
                        GgufWeightFormat.Imported,
                        cache,
                        PublishedOffload(record.Key.ExecutionProfile),
                        4096,
                        4096,
                        turbo ? SupportLevel.Experimental : SupportLevel.DeclaredSupported,
                        requiresEvidence: turbo);
                })
        ];
    }

    private static GgufKvCacheFormat PublishedCache(string cache) => cache switch
    {
        "f16" => GgufKvCacheFormat.F16,
        "q8_0" => GgufKvCacheFormat.Q8_0,
        "turbo4" => GgufKvCacheFormat.TurboQuant4Bit,
        "turbo3" => GgufKvCacheFormat.TurboQuant3Bit,
        "turbo2" => GgufKvCacheFormat.TurboQuant2Bit,
        _ => throw new InvalidDataException(
            "Published GGUF evidence names an unsupported cache format."),
    };

    private static GpuOffloadLevel PublishedOffload(string profile) => profile switch
    {
        "cpu" => GpuOffloadLevel.None,
        "vulkan-partial" => GpuOffloadLevel.Partial,
        "vulkan-full" => GpuOffloadLevel.Full,
        _ => throw new InvalidDataException(
            "Published GGUF evidence names an unsupported execution profile."),
    };

    private static bool IsTurboQuant(
        GgufWeightFormat weights,
        GgufKvCacheFormat cache) =>
        weights is GgufWeightFormat.TQ4_1S or GgufWeightFormat.TQ3_1S
        || cache is GgufKvCacheFormat.TurboQuant4Bit
            or GgufKvCacheFormat.TurboQuant3Bit
            or GgufKvCacheFormat.TurboQuant2Bit;

    private static IReadOnlyList<ContextTokenCount> CandidateContexts(int maximum)
    {
        int[] requested = [512, 2048, 4096, maximum];
        return [.. requested.Where(value => value <= maximum)
            .Distinct()
            .Order()
            .Select(ContextTokenCount.FromTokens)];
    }

    private static GgufWeightFormat[] DownwardTargets(
        WeightQuantisation source) => source switch
        {
            WeightQuantisation.F32 or WeightQuantisation.BF16
                or WeightQuantisation.F16 =>
                [GgufWeightFormat.Q8_0, GgufWeightFormat.Q6K,
                 GgufWeightFormat.Q5KM, GgufWeightFormat.Q4KM,
                 GgufWeightFormat.Q3KM, GgufWeightFormat.Q2K],
            WeightQuantisation.Q8_0 => [GgufWeightFormat.Q2K],
            WeightQuantisation.Q6_K => [GgufWeightFormat.Q2K],
            WeightQuantisation.Q5_K_M => [GgufWeightFormat.Q2K],
            WeightQuantisation.Q4_K_M => [GgufWeightFormat.Q2K],
            WeightQuantisation.Q3_K_M => [GgufWeightFormat.Q2K],
            _ => [],
        };

    private static bool IsAlreadyQuantized(WeightQuantisation source) => source is
        WeightQuantisation.Q8_0 or WeightQuantisation.Q6_K
        or WeightQuantisation.Q5_K_M or WeightQuantisation.Q4_K_M
        or WeightQuantisation.Q3_K_M or WeightQuantisation.Q2_K;

    private static byte[] ReadRuntimeManifest()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(RuntimeManifestResource)
            ?? throw new InvalidDataException(
                "The trusted GGUF runtime manifest is not embedded.");
        if (stream.Length is <= 0 or > GgufRuntimeManifestJson.MaximumManifestBytes)
        {
            throw new InvalidDataException("The trusted runtime manifest is invalid.");
        }
        using var memory = new MemoryStream(checked((int)stream.Length));
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static VerifiedQuantizer? TryVerifyQuantizer()
    {
        string packaged = Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "GgufQuantizer");
        if (Directory.Exists(packaged))
        {
            try
            {
                return new VerifiedQuantizer(
                    packaged,
                    GgufQuantizerPackageVerifier.Verify(
                        packaged,
                        ExpectedQuantizerManifestSha256));
            }
            catch (Exception failure) when (
                failure is InvalidDataException
                    or IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or CryptographicException)
            {
                return null;
            }
        }
        return null;
    }

    private static string CapabilityDigest(
        byte[] manifest,
        IEnumerable<GgufAdmittedConfiguration> admissions,
        VerifiedQuantizer? quantizer)
    {
        var canonical = new StringBuilder("gguf-capability-v1|");
        canonical.Append(Convert.ToHexString(SHA256.HashData(manifest))
            .ToLowerInvariant());
        canonical.Append('|').Append(quantizer?.Package.ManifestSha256 ?? "none");
        canonical.Append('|').Append(quantizer?.Package.ExecutableSha256 ?? "none");
        foreach (GgufAdmittedConfiguration admission in admissions)
        {
            canonical.Append('|').Append(admission.EvidenceId)
                .Append(':').Append((int)admission.Backend)
                .Append(':').Append((int)admission.Device)
                .Append(':').Append((int)admission.Weights)
                .Append(':').Append((int)admission.KvCache)
                .Append(':').Append((int)admission.Offload)
                .Append(':').Append(admission.MinimumContextTokens)
                .Append(':').Append(admission.MaximumContextTokens)
                .Append(':').Append((int)admission.Level)
                .Append(':').Append(admission.RequiresEvidence ? 1 : 0);
        }
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record VerifiedQuantizer(
        string Root,
        VerifiedGgufQuantizerPackage Package);

    private sealed class GgufExecutionPayloadComposer(
        GgufRuntimeAuthority runtime,
        int layerCount,
        GgufConversionSourceBinding? source,
        GgufQuantiserIdentity? quantizer,
        GgufRequantisationPolicy? requantisation)
        : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;

        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            if (candidate.Route != Route
                || candidate.Configuration is not GgufRouteConfiguration configuration
                || !runtime.Profiles.TryGetValue(
                    candidate.EvidenceId,
                    out GgufExecutionProfileAuthority? profile))
            {
                throw new ArgumentException(
                    "The GGUF candidate has no exact runtime profile.",
                    nameof(candidate));
            }
            bool persistent = candidate.Metrics.RequiresPersistentChange;
            return Compose(
                configuration,
                candidate.Metrics.ContextTokens,
                profile,
                persistent ? configuration.Weights : GgufWeightFormat.Imported,
                persistent);
        }

        internal OptimizationExecutionPayload ComposeCurrent(
            int contextTokens,
            string evidenceId)
        {
            GgufExecutionProfileAuthority profile = runtime.Profiles[evidenceId];
            return Compose(
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                contextTokens,
                profile,
                GgufWeightFormat.Imported,
                persistent: false);
        }

        private OptimizationExecutionPayload Compose(
            GgufRouteConfiguration configuration,
            int contextTokens,
            GgufExecutionProfileAuthority profile,
            GgufWeightFormat target,
            bool persistent)
        {
            int gpuLayers = GgufOffloadPolicy.ExactLayerCount(
                configuration.Offload,
                layerCount);
            GgufExecutionPayload payload = GgufExecutionPayload.Create(
                runtime.RuntimeBuildId,
                runtime.RuntimeSourceCommit,
                RuntimeBackend(configuration.Backend),
                RuntimeDevice(configuration.Device),
                contextTokens,
                RuntimeCache(configuration.KvCache),
                RuntimeCache(configuration.KvCache),
                gpuLayers,
                profile.FlashAttention,
                profile.ThreadCount,
                profile.BatchSize,
                profile.Evidence.ToString(),
                profile.ProfileId,
                profile.MaximumGeneratedTokens,
                target,
                persistent ? quantizer : null,
                persistent ? source : null,
                persistent ? requantisation : null);
            return OptimizationExecutionPayload.ForGguf(payload);
        }

        private static GgufRuntimeBackend RuntimeBackend(
            CompatibilityBackend backend) => backend switch
        {
            CompatibilityBackend.Cpu => GgufRuntimeBackend.Cpu,
            CompatibilityBackend.IntelVulkan => GgufRuntimeBackend.Vulkan,
            CompatibilityBackend.IntelSycl => GgufRuntimeBackend.Sycl,
            _ => throw new ArgumentOutOfRangeException(
                nameof(backend), backend, "The admitted backend is not executable."),
        };

        private static string RuntimeDevice(DeviceRouteId device) => device switch
        {
            DeviceRouteId.Cpu => PackagedCpuDeviceId,
            DeviceRouteId.IntelIntegratedGpu => "IntelIntegratedGpu",
            DeviceRouteId.IntelDiscreteGpu => "IntelDiscreteGpu",
            _ => throw new ArgumentOutOfRangeException(
                nameof(device), device, "The admitted device is not executable."),
        };

        private static GgufCacheType RuntimeCache(GgufKvCacheFormat cache) => cache switch
        {
            GgufKvCacheFormat.F16 => GgufCacheType.F16,
            GgufKvCacheFormat.Q8_0 => GgufCacheType.Q8Zero,
            GgufKvCacheFormat.TurboQuant4Bit => GgufCacheType.Turbo4,
            GgufKvCacheFormat.TurboQuant3Bit => GgufCacheType.Turbo3,
            GgufKvCacheFormat.TurboQuant2Bit => GgufCacheType.Turbo2,
            _ => throw new ArgumentOutOfRangeException(
                nameof(cache), cache, "The admitted cache format is not executable."),
        };
    }
}
