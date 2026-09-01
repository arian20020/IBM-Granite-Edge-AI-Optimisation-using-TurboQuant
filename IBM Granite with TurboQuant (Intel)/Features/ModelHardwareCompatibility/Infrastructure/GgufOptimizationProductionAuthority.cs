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
        "903c6e4620a45743cd920d577c32881422d8321d417c63fdf6ca69f67685f58b";
    private const string QuantizerPackageId =
        "granite-edge-ai-llama-quantize-x64";
    private const string QuantizerVersion = "llama-quantize-3f7c29d";
    internal const string PackagedCpuDeviceId = "CPU";
    private readonly object _gate = new();
    private readonly PreparedGgufCompatibilityInput _prepared;
    private readonly CompatibilityHardwareInput _hardware;
    private readonly OptimizationCapabilitySnapshot _snapshot;
    private readonly OptimizationWorkload _workload;
    private readonly OptimizationJourneyBinding _binding;
    private readonly GgufExecutionPayloadComposer _composer;
    private readonly OptimizationExecutionPayload _currentPayload;
    private OptimizationIssuanceAuthority? _issuanceAuthority;

    private GgufOptimizationProductionAuthority(
        PreparedGgufCompatibilityInput prepared,
        CompatibilityHardwareInput hardware,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        GgufExecutionPayloadComposer composer,
        OptimizationExecutionPayload currentPayload)
    {
        _prepared = prepared;
        _hardware = hardware;
        _snapshot = snapshot;
        _workload = workload;
        _binding = binding;
        _composer = composer;
        _currentPayload = currentPayload;
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

            int maximumContext = Math.Clamp(
                prepared.Model.DeclaredContextLimit ?? 4096,
                512,
                32768);
            List<GgufAdmittedConfiguration> admissions =
            [
                Admission("gguf-current-cpu", GgufWeightFormat.Imported,
                    maximumContext)
            ];
            GgufRequantisationPolicy? requantisation = null;
            if (source is not null && quantizer is not null)
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
                    // requantisation target. Use the smallest supported target;
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
            GgufExecutionProfileAuthority[] profiles =
            [
                .. admissions.Select(admission =>
                    GgufExecutionProfileAuthority.Create(
                        admission.EvidenceId,
                        EvidenceGrade.Estimated,
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
                runtimeAuthority: runtimeAuthority);
            string capabilityDigest = CapabilityDigest(
                manifestBytes,
                admissions,
                verifiedQuantizer);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForGguf(
                    $"gguf-cpu-{capabilityDigest[..12]}",
                    capabilityDigest,
                    payload);
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "local-chat",
                minimumContextTokens: 512,
                minimumQuality: OptimizationAssessment.Poor,
                CandidateContexts(maximumContext));
            var composer = new GgufExecutionPayloadComposer(
                runtimeAuthority,
                prepared.Model.LayerCount ?? 0,
                source,
                quantizer,
                requantisation);
            OptimizationExecutionPayload currentPayload = composer.ComposeCurrent(
                maximumContext,
                admissions[0].EvidenceId);

            authority = new GgufOptimizationProductionAuthority(
                prepared,
                hardware,
                snapshot,
                workload,
                binding,
                composer,
                currentPayload)
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
        fresh = CompatibilityFreshResourceNormalizer.ConstrainTo(
            _hardware,
            fresh);
        CompatibilityOptimizationProductionInput optimization =
            CompatibilityOptimizationProductionInput.Create(
                _snapshot,
                _workload,
                _binding,
                optedInEvidence);
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
            // An exhausted safe budget produces no actionable plan.
        }
        lock (_gate)
        {
            _issuanceAuthority = issuance;
        }
        return evaluation;
    }

    internal CurrentModelLaunchHandoff? ResolveCurrentModel(
        CompatibilityEvaluation evaluation,
        CurrentModelChatLaunchRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(registry);
        if (evaluation.Screen.State != CompatibilityScreenState.EstimatedCompatible)
        {
            return null;
        }
        string configurationSha =
            _currentPayload.ComputeRuntimeConfigurationSha256();
        var current = new CurrentCompatibleConfiguration(
            OptimizationRoute.Gguf,
            _currentPayload,
            configurationSha,
            $"compat-{configurationSha[..24]}");
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
        int maximumContext) => GgufAdmittedConfiguration.Create(
            evidenceId,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            weights,
            GgufKvCacheFormat.F16,
            GpuOffloadLevel.None,
            512,
            maximumContext,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

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
            return new VerifiedQuantizer(
                packaged,
                GgufQuantizerPackageVerifier.Verify(
                    packaged,
                    ExpectedQuantizerManifestSha256));
        }
#if DEBUG
        const string developmentStage =
            @"C:\GEAI-Tools\llama-quantize-3f7c29d-x64";
        if (Directory.Exists(developmentStage))
        {
            return new VerifiedQuantizer(
                developmentStage,
                GgufQuantizerPackageVerifier.Verify(
                    developmentStage,
                    ExpectedQuantizerManifestSha256));
        }
#endif
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
                .Append(':').Append((int)admission.Weights)
                .Append(':').Append(admission.MinimumContextTokens)
                .Append(':').Append(admission.MaximumContextTokens);
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
                GgufRuntimeBackend.Cpu,
                PackagedCpuDeviceId,
                contextTokens,
                GgufCacheType.F16,
                GgufCacheType.F16,
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
    }
}
