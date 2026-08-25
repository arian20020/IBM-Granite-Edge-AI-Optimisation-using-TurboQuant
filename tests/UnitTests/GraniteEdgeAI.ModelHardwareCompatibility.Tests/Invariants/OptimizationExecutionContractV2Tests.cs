using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// The V2 execution contract: nothing left for an executor to invent.
///
/// V1 bound a coarse route configuration, so a thread count, a cache type or a
/// GPU layer count could change without the plan's identity changing. A setting
/// outside the digest is a setting nobody confirmed, so the central property
/// here is that every execution field moves the digest.
/// </summary>
[TestClass]
public sealed class OptimizationExecutionContractV2Tests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const int ModelLayers = 32;

    private const string Digest64 =
        "1111111111111111111111111111111111111111111111111111111111111111";

    private const string OtherDigest64 =
        "2222222222222222222222222222222222222222222222222222222222222222";

    private const string Commit40 = "0123456789abcdef0123456789abcdef01234567";

    private static class V2TestData
    {
        internal static OptimizationJourneyBinding Binding() =>
            OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", Digest64, 4 * Gibibyte,
                "hw-run-1", OtherDigest64);

        internal static OptimizationWorkload Workload() =>
            OptimizationWorkload.Create(
                "chat", 512, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4096)]);

        // ---- GGUF ----

        internal static OptimizationCandidate GgufCandidate(
            GgufWeightFormat weights = GgufWeightFormat.Imported,
            GgufKvCacheFormat cache = GgufKvCacheFormat.F16,
            CompatibilityBackend backend = CompatibilityBackend.Cpu,
            DeviceRouteId device = DeviceRouteId.Cpu,
            GpuOffloadLevel offload = GpuOffloadLevel.None,
            int context = 4096,
            OptimizationConversionProvenance provenance =
                OptimizationConversionProvenance.None,
            OptimizationAssessment quality = OptimizationAssessment.Good)
        {
            bool persistent = weights != GgufWeightFormat.Imported;
            OptimizationConversionProvenance effectiveProvenance =
                persistent && provenance == OptimizationConversionProvenance.None
                    ? OptimizationConversionProvenance.HigherPrecisionSource
                    : provenance;
            OptimizationAssessment effectiveQuality = weights == GgufWeightFormat.Q2K
                ? OptimizationAssessment.Poor
                : quality;

            return OptimizationCandidate.Create(
                GgufRouteConfiguration.Create(weights, cache, backend, device, offload),
                Metrics(context, persistent, effectiveQuality),
                "gguf-evidence",
                isExperimental: false,
                effectiveProvenance);
        }

        internal static GgufQuantiserIdentity Quantiser(
            string packageId = "llama-quantize-pkg",
            string toolVersion = "b4321",
            string executableSha256 = Digest64) =>
            GgufQuantiserIdentity.Create(packageId, toolVersion, executableSha256);

        internal static OptimizationCandidate LegacyGgufQ3Candidate() =>
            OptimizationCandidate.CreateLegacyVersionTwo(
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Q3KM, GgufKvCacheFormat.F16,
                    CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                    GpuOffloadLevel.None),
                Metrics(4096, persistent: true, OptimizationAssessment.Poor),
                "gguf-evidence",
                isExperimental: false);

        internal static GgufExecutionPayload GgufPayload(
            string runtimeBuildId = "gguf-build-1",
            string runtimeSourceCommit = Commit40,
            GgufRuntimeBackend backend = GgufRuntimeBackend.Cpu,
            string deviceId = "CPU",
            int contextSize = 4096,
            GgufCacheType keyCacheType = GgufCacheType.F16,
            GgufCacheType valueCacheType = GgufCacheType.F16,
            int gpuLayerCount = 0,
            bool flashAttention = false,
            int threadCount = 8,
            int batchSize = 512,
            string evidenceGrade = "Estimated",
            string profileId = "profile-1",
            int maximumGeneratedTokens = 512,
            GgufWeightFormat persistentTarget = GgufWeightFormat.Imported,
            GgufQuantiserIdentity? quantiser = null) =>
            GgufExecutionPayload.Create(
                runtimeBuildId, runtimeSourceCommit, backend, deviceId, contextSize,
                keyCacheType, valueCacheType, gpuLayerCount, flashAttention,
                threadCount, batchSize, evidenceGrade, profileId,
                maximumGeneratedTokens, persistentTarget, quantiser);

        internal static OptimizationCapabilitySnapshot GgufSnapshot(
            OptimizationCandidate candidate,
            GgufExecutionPayload executionPayload)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;
            GgufConversionSourceBinding? source = candidate.Metrics.RequiresPersistentChange
                ? GgufConversionSourceBinding.Create(
                    WeightQuantisation.F16, Binding())
                : null;

            return
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", Digest64,
                GgufCapabilityPayload.Create(
                    executionPayload.RuntimeBuildId,
                    [
                        GgufAdmittedConfiguration.Create(
                            "gguf-evidence", configuration.Backend, configuration.Device,
                            configuration.Weights, configuration.KvCache,
                            configuration.Offload, 512, 32768,
                            SupportLevel.DeclaredSupported, false)
                    ],
                    hasHigherPrecisionSource: true,
                    conversionSource: source,
                    admittedQuantiser: executionPayload.Quantiser,
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        executionPayload.RuntimeBuildId,
                        executionPayload.RuntimeSourceCommit,
                        [
                            GgufExecutionProfileAuthority.Create(
                                "gguf-evidence",
                                candidate.Metrics.Evidence,
                                executionPayload.ProfileId,
                                executionPayload.FlashAttention,
                                executionPayload.ThreadCount,
                                executionPayload.BatchSize,
                                executionPayload.MaximumGeneratedTokens)
                        ])));
        }

        internal static OptimizationCapabilitySnapshot GgufSnapshot() =>
            GgufSnapshot(GgufCandidate(), GgufPayload());

        // ---- OpenVINO ----

        internal static OptimizationCandidate OpenVinoCandidate(
            OpenVinoWeightFormat weights = OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8,
            DeviceRouteId device = DeviceRouteId.Cpu,
            Core.Routes.OpenVino.OpenVinoCompiledCachePolicy compiledCache =
                Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Enabled,
            int context = 4096) =>
            OptimizationCandidate.Create(
                OpenVinoRouteConfiguration.Create(
                    weights, cache, device, OpenVinoPerformanceHint.Latency,
                    compiledCache, 1),
                Metrics(context, persistent: true),
                "ov-evidence",
                isExperimental: false);

        /// <summary>
        /// A candidate that runs the package as it is. The published OpenVINO
        /// route has no "Original" precision, so runtime-only is expressed as a
        /// target equal to the source.
        /// </summary>
        internal static OptimizationCandidate OpenVinoCandidateRuntimeOnly() =>
            OptimizationCandidate.Create(
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original,
                    OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Enabled,
                    1),
                Metrics(4096, persistent: false),
                "ov-evidence",
                isExperimental: false);

        internal static OpenVinoBuildIdentity Build(
            string runtimeBuild = "2026.3.0",
            string genAiBuild = "2026.3.0.0",
            string tokenizersBuild = "2026.3.0",
            string workerManifestDigest = Digest64) =>
            OpenVinoBuildIdentity.Create(
                runtimeBuild, genAiBuild, tokenizersBuild, workerManifestDigest);

        internal static Dictionary<string, string> Versions() => new(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        };

        internal static OpenVinoExecutionPayload OpenVinoPayload(
            string configurationId = "openvino.standard.cpu.int8.default.v1",
            string device = "CPU",
            string maturity = "Standard candidate",
            string evidenceId = "ov-evidence",
            OpenVinoWeightPrecision source = OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision target = OpenVinoWeightPrecision.EightBit,
            OpenVinoKvCachePrecision kvCache = OpenVinoKvCachePrecision.U8,
            bool compiledCacheEnabled = true,
            bool compiledCacheIsDisposable = true,
            bool createsCompletePackage = true,
            OpenVinoBuildIdentity? build = null,
            IReadOnlyDictionary<string, string>? versions = null,
            TurboQuantBuildIdentity? turboQuant = null) =>
            OpenVinoExecutionPayload.CreateV2(
                configurationId, device, maturity, evidenceId, source, target, kvCache,
                compiledCacheEnabled, compiledCacheIsDisposable,
                compiledCacheIsModelArtifact: false,
                createsCompletePackage, build ?? Build(), versions ?? Versions(),
                turboQuant);

        internal static OptimizationCapabilitySnapshot OpenVinoSnapshot(
            OptimizationCandidate? candidate = null,
            OpenVinoExecutionPayload? payload = null)
        {
            OpenVinoRouteConfiguration configuration = candidate?.Configuration
                as OpenVinoRouteConfiguration
                ?? (OpenVinoRouteConfiguration)OpenVinoCandidate().Configuration;
            return OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", Digest64,
                OpenVinoCapabilityPayload.Create(
                    payload?.BuildIdentity.RuntimeBuild ?? "2026.3.0",
                    [
                        OpenVinoAdmittedConfiguration.Create(
                            candidate?.EvidenceId ?? "ov-evidence",
                            configuration.Device, configuration.Weights,
                            configuration.KvCache, configuration.PerformanceHint,
                            configuration.CompiledCache, configuration.Streams,
                            512, 32768, SupportLevel.DeclaredSupported, false)
                    ],
                    [
                        OpenVinoExecutionAuthority.Create(
                            candidate?.EvidenceId ?? "ov-evidence",
                            payload?.ConfigurationId
                                ?? "openvino.standard.cpu.int8.default.v1",
                            payload?.SourceWeightPrecision
                                ?? OpenVinoWeightPrecision.Fp16,
                            payload?.BuildIdentity ?? Build(),
                            payload?.OptimizerVersions ?? Versions(),
                            payload?.CompiledCacheIsDisposable ?? true,
                            payload?.TurboQuantBuild)
                    ]));
        }

        private static OptimizationCandidateMetrics Metrics(
            int context, bool persistent,
            OptimizationAssessment quality = OptimizationAssessment.Good) =>
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                quality,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                context,
                8 * Gibibyte,
                32 * Gibibyte,
                24 * Gibibyte,
                0,
                persistent ? 4 * Gibibyte : 0,
                persistent);
    }

    private static OptimizationExecutionPlan IssueGguf(
        OptimizationCandidate? candidate = null,
        GgufExecutionPayload? payload = null,
        int modelLayers = ModelLayers,
        OptimizationJourneyBinding? binding = null)
    {
        OptimizationJourneyBinding chosenBinding = binding ?? V2TestData.Binding();
        OptimizationCandidate chosen = WithDiskAdmission(
            candidate ?? V2TestData.GgufCandidate());
        GgufExecutionPayload chosenPayload = payload ?? V2TestData.GgufPayload();
        if (chosen.Metrics.RequiresPersistentChange
            && chosenPayload.ConversionSource is null)
        {
            chosenPayload = GgufExecutionPayload.Create(
                chosenPayload.RuntimeBuildId, chosenPayload.RuntimeSourceCommit,
                chosenPayload.Backend, chosenPayload.DeviceId, chosenPayload.ContextSize,
                chosenPayload.KeyCacheType, chosenPayload.ValueCacheType,
                chosenPayload.GpuLayerCount, chosenPayload.FlashAttention,
                chosenPayload.ThreadCount, chosenPayload.BatchSize,
                chosenPayload.EvidenceGrade, chosenPayload.ProfileId,
                chosenPayload.MaximumGeneratedTokens,
                chosenPayload.PersistentTargetWeightFormat, chosenPayload.Quantiser,
                GgufConversionSourceBinding.Create(
                    WeightQuantisation.F16, V2TestData.Binding()));
        }

        OptimizationCapabilitySnapshot snapshot =
            V2TestData.GgufSnapshot(chosen, chosenPayload);
        chosen = OptimizationAdmissionTestFactory.Admit(
            chosen, snapshot, V2TestData.Workload(), chosenBinding,
            SupportLevel.DeclaredSupported);
        return OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [chosen],
                OptimizationPreferenceSelection.Manual(50))!,
            OptimizationExecutionPayload.ForGguf(chosenPayload),
            snapshot,
            V2TestData.Workload(),
            chosenBinding,
            modelLayers,
            DateTimeOffset.UnixEpoch);
    }

    private static OptimizationExecutionPlan IssueOpenVino(
        OptimizationCandidate? candidate = null,
        OpenVinoExecutionPayload? payload = null)
    {
        OptimizationCandidate chosen = WithDiskAdmission(
            candidate ?? V2TestData.OpenVinoCandidate());
        OpenVinoExecutionPayload chosenPayload = payload ?? V2TestData.OpenVinoPayload();
        OptimizationCapabilitySnapshot snapshot =
            V2TestData.OpenVinoSnapshot(chosen, chosenPayload);
        chosen = OptimizationAdmissionTestFactory.Admit(
            chosen, snapshot, V2TestData.Workload(), V2TestData.Binding(),
            SupportLevel.DeclaredSupported);

        return OptimizationPlanIssuer.Issue(
            OptimizationPreferenceResolver.Resolve(
                [chosen],
                OptimizationPreferenceSelection.Manual(50))!,
            OptimizationExecutionPayload.ForOpenVino(
                chosenPayload),
            snapshot,
            V2TestData.Workload(),
            V2TestData.Binding(),
            ModelLayers,
            DateTimeOffset.UnixEpoch);
    }

    private static OptimizationCandidate WithDiskAdmission(
        OptimizationCandidate candidate)
    {
        OptimizationCandidateMetrics metrics = candidate.Metrics;
        OptimizationCandidateMetrics admitted = OptimizationCandidateMetrics.Create(
            metrics.Evidence,
            metrics.Quality,
            metrics.Performance,
            metrics.Stability,
            metrics.ContextTokens,
            metrics.PredictedPeakBytes,
            metrics.SafeBudgetBytes,
            metrics.HeadroomBytes,
            Math.Max(metrics.WorkingDiskBytes, metrics.OutputDiskBytes),
            metrics.OutputDiskBytes,
            metrics.RequiresPersistentChange,
            availableDiskBytes: 500 * Gibibyte);

        if (candidate.Configuration is GgufRouteConfiguration
                { Weights: GgufWeightFormat.Q3KM }
            && candidate.Notice == OptimizationCandidateNotice.None)
        {
            return OptimizationCandidate.CreateLegacyVersionTwo(
                candidate.Configuration,
                admitted,
                candidate.EvidenceId,
                candidate.IsExperimental);
        }

        return OptimizationCandidate.Create(
            candidate.Configuration,
            admitted,
            candidate.EvidenceId,
            candidate.IsExperimental,
            candidate.ConversionProvenance);
    }

    // ---------- version ----------

    [TestMethod]
    public void OpenVinoV2CanonicalVectorsRemainFrozen()
    {
        OptimizationCandidate candidate = V2TestData.OpenVinoCandidate();
        OptimizationExecutionPayload released = OptimizationExecutionPayload.ForOpenVino(
            V2TestData.OpenVinoPayload());
        OptimizationExecutionPayload legacyTurboIdentity = OptimizationExecutionPayload.ForOpenVino(
            V2TestData.OpenVinoPayload(
                turboQuant: TurboQuantBuildIdentity.Create(
                    Commit40, Commit40, Digest64, OtherDigest64)));

        Assert.AreEqual(
            "v=1:2|route=1:2|config=66:openvino|w=Int8|kv=U8|dev=Cpu|hint=Latency|cache=Enabled|streams=1|ctx=4:4096|persistent=1:1|evidence=11:ov-evidence|experimental=1:0|ov.configurationId=37:openvino.standard.cpu.int8.default.v1|ov.device=3:CPU|ov.maturity=18:Standard candidate|ov.evidenceId=11:ov-evidence|ov.sourceWeightPrecision=1:0|ov.targetWeightPrecision=1:1|ov.kvCachePrecision=1:1|ov.compiledCacheEnabled=1:1|ov.compiledCacheIsDisposable=1:1|ov.compiledCacheIsModelArtifact=1:0|ov.createsCompletePackage=1:1|ov.build.runtimeBuild=8:2026.3.0|ov.build.genAiBuild=10:2026.3.0.0|ov.build.tokenizersBuild=8:2026.3.0|ov.build.workerManifestDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.optimizer.nncf=5:3.3.0|ov.optimizer.openvino=8:2026.3.0|ov.optimizer.openvino-genai=10:2026.3.0.0|ov.optimizer.optimum=5:2.3.0|ov.optimizer.optimum-intel=5:2.1.0|ov.optimizer.transformers=5:5.5.4|ov.turboQuant=4:none",
            OptimizationCanonicalizer.CanonicalizeV2(candidate, released));
        Assert.AreEqual(
            "v=1:2|route=1:2|config=66:openvino|w=Int8|kv=U8|dev=Cpu|hint=Latency|cache=Enabled|streams=1|ctx=4:4096|persistent=1:1|evidence=11:ov-evidence|experimental=1:0|ov.configurationId=37:openvino.standard.cpu.int8.default.v1|ov.device=3:CPU|ov.maturity=18:Standard candidate|ov.evidenceId=11:ov-evidence|ov.sourceWeightPrecision=1:0|ov.targetWeightPrecision=1:1|ov.kvCachePrecision=1:1|ov.compiledCacheEnabled=1:1|ov.compiledCacheIsDisposable=1:1|ov.compiledCacheIsModelArtifact=1:0|ov.createsCompletePackage=1:1|ov.build.runtimeBuild=8:2026.3.0|ov.build.genAiBuild=10:2026.3.0.0|ov.build.tokenizersBuild=8:2026.3.0|ov.build.workerManifestDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.optimizer.nncf=5:3.3.0|ov.optimizer.openvino=8:2026.3.0|ov.optimizer.openvino-genai=10:2026.3.0.0|ov.optimizer.optimum=5:2.3.0|ov.optimizer.optimum-intel=5:2.1.0|ov.optimizer.transformers=5:5.5.4|ov.turboQuant.sourceCommit=40:0123456789abcdef0123456789abcdef01234567|ov.turboQuant.implementationCommit=40:0123456789abcdef0123456789abcdef01234567|ov.turboQuant.patchSeriesDigest=64:1111111111111111111111111111111111111111111111111111111111111111|ov.turboQuant.runtimeManifestDigest=64:2222222222222222222222222222222222222222222222222222222222222222",
            OptimizationCanonicalizer.CanonicalizeV2(candidate, legacyTurboIdentity));

        Assert.AreEqual(
            "6d5902369602fb3b9b12c63f003890794488864c79323476c463610e9606270e",
            OptimizationCanonicalizer.ConfigurationSha256V2(candidate, released));
        Assert.AreEqual(
            "d037be18e90cba1d7d7d9dec28140cd43a91dc41d7c69686e7f0e68ee4f5dbcf",
            OptimizationCanonicalizer.ConfigurationSha256V2(candidate, legacyTurboIdentity));
    }

    [TestMethod]
    public void GgufQ3V2CandidateRemainsNoticeFreeAndCanonicalizable()
    {
        OptimizationCandidate candidate = V2TestData.LegacyGgufQ3Candidate();
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q3KM,
                quantiser: V2TestData.Quantiser()));

        Assert.AreEqual(OptimizationCandidateNotice.None, candidate.Notice);
        Assert.AreEqual(
            "v=1:2|route=1:1|config=42:gguf|w=Q3KM|kv=F16|be=Cpu|dev=Cpu|off=None|ctx=4:4096|persistent=1:1|evidence=13:gguf-evidence|experimental=1:0|gguf.runtimeBuildId=12:gguf-build-1|gguf.runtimeSourceCommit=40:0123456789abcdef0123456789abcdef01234567|gguf.backend=1:0|gguf.deviceId=3:CPU|gguf.contextSize=4:4096|gguf.keyCacheType=1:0|gguf.valueCacheType=1:0|gguf.gpuLayerCount=1:0|gguf.flashAttention=1:0|gguf.threadCount=1:8|gguf.batchSize=3:512|gguf.evidenceGrade=9:Estimated|gguf.profileId=9:profile-1|gguf.maximumGeneratedTokens=3:512|gguf.persistentTargetWeightFormat=1:8|gguf.quantiser.packageId=18:llama-quantize-pkg|gguf.quantiser.toolVersion=5:b4321|gguf.quantiser.executableSha256=64:1111111111111111111111111111111111111111111111111111111111111111",
            OptimizationCanonicalizer.CanonicalizeV2(candidate, payload));
        Assert.AreEqual(
            "fd3699ffb644b9a8a15ec6aa4080f79779c78fb2a42461cdc7356180e007eb10",
            OptimizationCanonicalizer.ConfigurationSha256V2(candidate, payload));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCachePrecision.F16)]
    [DataRow(OpenVinoKvCachePrecision.Bf16)]
    [DataRow(OpenVinoKvCachePrecision.U4)]
    [DataRow(OpenVinoKvCachePrecision.Tbq4)]
    [DataRow(OpenVinoKvCachePrecision.Tbq3)]
    public void VersionTwoPayloadRejectsPostVersionTwoCachePrecision(
        OpenVinoKvCachePrecision precision)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => V2TestData.OpenVinoPayload(kvCache: precision));
    }

    [TestMethod]
    public void VersionTwoCanonicalizerRejectsVersionThreeTurboQuantPayload()
    {
        OptimizationCandidate candidate = V2TestData.OpenVinoCandidate();
        OptimizationExecutionPayload payload = VersionThreeTurboQuantPayload();

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCanonicalizer.CanonicalizeV2(candidate, payload));
    }

    [TestMethod]
    public void VersionTwoHashRejectsVersionThreeTurboQuantPayload()
    {
        OptimizationCandidate candidate = V2TestData.OpenVinoCandidate();
        OptimizationExecutionPayload payload = VersionThreeTurboQuantPayload();

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCanonicalizer.ConfigurationSha256V2(candidate, payload));
    }

    [TestMethod]
    public void VersionTwoRejectsVersionThreeDiskAdmissionProof()
    {
        OptimizationCandidate legacy = V2TestData.OpenVinoCandidate();
        OptimizationCandidateMetrics metrics = legacy.Metrics;
        OptimizationCandidate versionThree = OptimizationCandidate.Create(
            legacy.Configuration,
            OptimizationCandidateMetrics.Create(
                metrics.Evidence,
                metrics.Quality,
                metrics.Performance,
                metrics.Stability,
                metrics.ContextTokens,
                metrics.PredictedPeakBytes,
                metrics.SafeBudgetBytes,
                metrics.HeadroomBytes,
                metrics.WorkingDiskBytes,
                metrics.OutputDiskBytes,
                metrics.RequiresPersistentChange,
                availableDiskBytes: 500 * Gibibyte),
            legacy.EvidenceId,
            legacy.IsExperimental);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForOpenVino(
            V2TestData.OpenVinoPayload());

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCanonicalizer.CanonicalizeV2(versionThree, payload));
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCanonicalizer.ConfigurationSha256V2(versionThree, payload));
    }

    [TestMethod]
    public void VersionTwoCanonicalAndHashRejectQ2KWeightVocabulary()
    {
        OptimizationCandidate candidate = V2TestData.GgufCandidate(
            weights: GgufWeightFormat.Q2K,
            provenance: OptimizationConversionProvenance.HigherPrecisionSource);
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q2K,
                quantiser: V2TestData.Quantiser()));

        ArgumentException canonical = Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCanonicalizer.CanonicalizeV2(candidate, payload));
        ArgumentException hash = Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCanonicalizer.ConfigurationSha256V2(candidate, payload));

        StringAssert.Contains(canonical.Message, "Q2_K");
        StringAssert.Contains(hash.Message, "Q2_K");

        OptimizationCandidate legacyCandidate = V2TestData.GgufCandidate(
            weights: GgufWeightFormat.Q4KM);

        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationCanonicalizer.CanonicalizeV2(legacyCandidate, payload));
        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationCanonicalizer.ConfigurationSha256V2(legacyCandidate, payload));
    }

    [TestMethod]
    public void VersionTwoCanonicalAndHashRejectEveryPostVersionTwoCandidateNotice()
    {
        OptimizationExecutionPayload payload = OptimizationExecutionPayload.ForGguf(
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser()));

        OptimizationCandidate[] candidates =
        [
            V2TestData.GgufCandidate(
                weights: GgufWeightFormat.Q2K,
                provenance: OptimizationConversionProvenance.HigherPrecisionSource,
                quality: OptimizationAssessment.Poor),
            V2TestData.GgufCandidate(
                weights: GgufWeightFormat.Q4KM,
                provenance: OptimizationConversionProvenance.ControlledRequantisation),
            V2TestData.GgufCandidate(
                weights: GgufWeightFormat.Q2K,
                provenance: OptimizationConversionProvenance.ControlledRequantisation)
        ];

        CollectionAssert.AreEquivalent(
            Enum.GetValues<OptimizationCandidateNotice>()
                .Where(value => value != OptimizationCandidateNotice.None).ToArray(),
            candidates.Select(candidate => candidate.Notice).ToArray());

        foreach (OptimizationCandidate candidate in candidates)
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => OptimizationCanonicalizer.CanonicalizeV2(candidate, payload),
                $"V2 accepted candidate notice {candidate.Notice}.");
            Assert.ThrowsExactly<ArgumentException>(
                () => OptimizationCanonicalizer.ConfigurationSha256V2(candidate, payload),
                $"V2 hashed candidate notice {candidate.Notice}.");
        }
    }

    [TestMethod]
    public void NewlyIssuedPlanReportsContractVersionThree()
    {
        OptimizationExecutionPlan plan = IssueGguf();

        Assert.AreEqual(3, plan.ContractVersion);

        // Read through the instance so the check is a real comparison rather
        // than two literals the compiler folds together.
        Assert.AreEqual(
            OptimizationExecutionPlan.CurrentContractVersion, plan.ContractVersion);
    }

    [TestMethod]
    public void VersionTwoCannotInterpretANewVersionThreePlan()
    {
        // A V1 plan carries no execution payload at all. An executor built for
        // V2 that accepted one would have to supply every runtime setting from
        // somewhere, which is the defect this version closes.
        OptimizationExecutionPlan plan = IssueGguf();

        Assert.IsFalse(plan.IsExecutableBy(1));
        Assert.IsFalse(plan.IsExecutableBy(2));
        Assert.IsTrue(plan.IsExecutableBy(3));

        // The minimum is asserted through its consequence rather than as a
        // literal: what matters is that no executor below it is admitted.
        Assert.IsFalse(
            plan.IsExecutableBy(OptimizationExecutionPlan.MinimumExecutableContractVersion - 1),
            "An executor below the minimum version was admitted.");
    }

    private static OptimizationExecutionPayload VersionThreeTurboQuantPayload() =>
        OptimizationExecutionPayload.ForOpenVino(OpenVinoExecutionPayload.Create(
            "openvino.experimental.cpu.int4.turbo.v3",
            "CPU",
            "Experimental candidate",
            "ov-turbo-evidence",
            OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision.FourBit,
            OpenVinoKvCachePrecision.Tbq4,
            compiledCacheEnabled: false,
            compiledCacheIsDisposable: true,
            compiledCacheIsModelArtifact: false,
            createsCompletePackage: true,
            V2TestData.Build(),
            V2TestData.Versions(),
            TurboQuantBuildIdentity.Create(
                Commit40, Commit40, Digest64, OtherDigest64),
            OpenVinoKvCacheAlgorithm.TurboQuant));

    [TestMethod]
    public void PlanCannotBeConstructedWithoutAnExecutionPayload()
    {
        // The only constructor is internal and requires one, and the only public
        // route to a plan is the issuer, which also requires one. There is no
        // way to produce a payload-less plan from this assembly.
        Assert.IsTrue(
            typeof(OptimizationPlanIssuer)
                .GetMethod(nameof(OptimizationPlanIssuer.Issue))!
                .GetParameters()
                .Any(parameter =>
                    parameter.ParameterType == typeof(OptimizationExecutionPayload)),
            "The issuer no longer requires an execution payload.");

        Assert.IsFalse(
            typeof(OptimizationExecutionPlan)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Any(),
            "A public plan constructor would bypass the payload requirement.");
    }

    // ---------- exactly one payload ----------

    [TestMethod]
    public void ExactlyOneRoutePayloadIsCarried()
    {
        OptimizationExecutionPayload gguf =
            OptimizationExecutionPayload.ForGguf(V2TestData.GgufPayload());

        Assert.AreEqual(OptimizationRoute.Gguf, gguf.Route);
        Assert.IsNotNull(gguf.Gguf);
        Assert.IsNull(gguf.OpenVino);

        OptimizationExecutionPayload openVino =
            OptimizationExecutionPayload.ForOpenVino(V2TestData.OpenVinoPayload());

        Assert.AreEqual(OptimizationRoute.OpenVino, openVino.Route);
        Assert.IsNotNull(openVino.OpenVino);
        Assert.IsNull(openVino.Gguf);
    }

    [TestMethod]
    public void NullPayloadIsRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => OptimizationExecutionPayload.ForGguf(null!));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => OptimizationExecutionPayload.ForOpenVino(null!));
    }

    [TestMethod]
    public void PayloadRouteMustMatchTheCandidate()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            new OptimizationSelection(
                WithDiskAdmission(V2TestData.GgufCandidate()),
                OptimizationPreferenceSelection.Automatic(), false),
            OptimizationExecutionPayload.ForOpenVino(V2TestData.OpenVinoPayload()),
            V2TestData.GgufSnapshot(),
            V2TestData.Workload(),
            V2TestData.Binding(),
            ModelLayers,
            DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void PayloadRouteMustMatchTheCapabilitySnapshot()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationPlanIssuer.Issue(
            new OptimizationSelection(
                WithDiskAdmission(V2TestData.GgufCandidate()),
                OptimizationPreferenceSelection.Automatic(), false),
            OptimizationExecutionPayload.ForGguf(V2TestData.GgufPayload()),
            V2TestData.OpenVinoSnapshot(),
            V2TestData.Workload(),
            V2TestData.Binding(),
            ModelLayers,
            DateTimeOffset.UnixEpoch));
    }

    // ---------- every GGUF field is inside the digest ----------

    [TestMethod]
    public void EveryGgufExecutionFieldChangesTheConfigurationDigest()
    {
        string baseline = IssueGguf().ConfigurationSha256;

        (string Field, GgufExecutionPayload Payload)[] variants =
        [
            ("runtimeBuildId", V2TestData.GgufPayload(runtimeBuildId: "gguf-build-2")),
            ("runtimeSourceCommit", V2TestData.GgufPayload(
                runtimeSourceCommit: "89abcdef0123456789abcdef0123456789abcdef")),
            ("flashAttention", V2TestData.GgufPayload(flashAttention: true)),
            ("threadCount", V2TestData.GgufPayload(threadCount: 16)),
            ("batchSize", V2TestData.GgufPayload(batchSize: 1024)),
            ("maximumGeneratedTokens", V2TestData.GgufPayload(maximumGeneratedTokens: 1024))
        ];

        foreach ((string field, GgufExecutionPayload payload) in variants)
        {
            Assert.AreNotEqual(
                baseline,
                IssueGguf(payload: payload).ConfigurationSha256,
                $"Changing {field} did not change the configuration digest, so an "
                + "executor could change it without the plan noticing.");
        }
    }

    [TestMethod]
    public void GgufContextAndOffloadChangeTheDigest()
    {
        // These two are checked against the candidate, so both sides move
        // together - which is why they need their own case.
        string baseline = IssueGguf().ConfigurationSha256;

        Assert.AreNotEqual(
            baseline,
            IssueGguf(
                V2TestData.GgufCandidate(context: 8192),
                V2TestData.GgufPayload(contextSize: 8192)).ConfigurationSha256,
            "Context size does not reach the digest.");

        // Device overlaps with the candidate, so it moves on both sides at once -
        // varying one alone is what the agreement check exists to reject.
        Assert.AreNotEqual(
            baseline,
            IssueGguf(
                V2TestData.GgufCandidate(
                    backend: CompatibilityBackend.IntelSycl,
                    device: DeviceRouteId.IntelIntegratedGpu),
                V2TestData.GgufPayload(
                    backend: GgufRuntimeBackend.Sycl, deviceId: "GPU.0"))
                .ConfigurationSha256,
            "Device does not reach the digest.");

        Assert.AreNotEqual(
            baseline,
            IssueGguf(
                V2TestData.GgufCandidate(
                    backend: CompatibilityBackend.IntelSycl,
                    device: DeviceRouteId.IntelIntegratedGpu,
                    offload: GpuOffloadLevel.Full),
                V2TestData.GgufPayload(
                    backend: GgufRuntimeBackend.Sycl,
                    deviceId: "GPU.0",
                    gpuLayerCount: ModelLayers)).ConfigurationSha256,
            "GPU layer count does not reach the digest.");
    }

    [TestMethod]
    public void GgufCacheTypesReachTheDigestAndMustMatchTheAdmittedFormat()
    {
        // Both halves reach the digest, moved together with the candidate.
        //
        // They must move together, and that is a real constraint rather than a
        // shortcut: the support matrix admits one cache format per entry, while
        // the runtime configures key and value separately. So an admitted entry
        // can only authorise the same format for both. A payload naming
        // different halves would be running a configuration no evidence
        // covered - so it is refused, and admitting asymmetric caches would
        // need the matrix to admit a pair.
        string baseline = IssueGguf().ConfigurationSha256;

        Assert.AreNotEqual(
            baseline,
            IssueGguf(
                V2TestData.GgufCandidate(cache: GgufKvCacheFormat.Q8_0),
                V2TestData.GgufPayload(
                    keyCacheType: GgufCacheType.Q8Zero,
                    valueCacheType: GgufCacheType.Q8Zero)).ConfigurationSha256,
            "Cache type does not reach the digest.");

        Assert.ThrowsExactly<ArgumentException>(
            () => IssueGguf(
                V2TestData.GgufCandidate(cache: GgufKvCacheFormat.Q8_0),
                V2TestData.GgufPayload(
                    keyCacheType: GgufCacheType.Q8Zero,
                    valueCacheType: GgufCacheType.F16)),
            "An asymmetric cache was admitted against an entry authorising one "
            + "format.");
    }

    [TestMethod]
    public void GgufBackendAndMatchingDeviceChangeChangesTheDigest()
    {
        string baseline = IssueGguf().ConfigurationSha256;

        Assert.AreNotEqual(
            baseline,
            IssueGguf(
                V2TestData.GgufCandidate(
                    backend: CompatibilityBackend.IntelSycl,
                    device: DeviceRouteId.IntelIntegratedGpu),
                V2TestData.GgufPayload(
                    backend: GgufRuntimeBackend.Sycl,
                    deviceId: "GPU.0"))
                .ConfigurationSha256);
    }

    [TestMethod]
    public void GgufQuantiserIdentityChangesTheDigest()
    {
        OptimizationCandidate converting =
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Q4KM);

        string baseline = IssueGguf(
            converting,
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser())).ConfigurationSha256;

        (string Field, GgufQuantiserIdentity Quantiser)[] variants =
        [
            ("packageId", V2TestData.Quantiser(packageId: "other-pkg")),
            ("toolVersion", V2TestData.Quantiser(toolVersion: "b9999")),
            ("executableSha256", V2TestData.Quantiser(executableSha256: OtherDigest64))
        ];

        foreach ((string field, GgufQuantiserIdentity quantiser) in variants)
        {
            Assert.AreNotEqual(
                baseline,
                IssueGguf(
                    converting,
                    V2TestData.GgufPayload(
                        persistentTarget: GgufWeightFormat.Q4KM,
                        quantiser: quantiser)).ConfigurationSha256,
                $"Changing the quantiser {field} did not change the digest, so the "
                + "tool could be swapped without invalidating the plan.");
        }
    }

    [TestMethod]
    public void RuntimeOnlyAndConvertingPlansDoNotCollide()
    {
        // Absence of a quantiser is canonicalised explicitly. If it emitted
        // nothing, a runtime-only plan could hash the same as a converting one.
        string runtimeOnly = IssueGguf().ConfigurationSha256;

        string converting = IssueGguf(
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Q4KM),
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser())).ConfigurationSha256;

        Assert.AreNotEqual(runtimeOnly, converting);
    }

    // ---------- every OpenVINO field is inside the digest ----------

    [TestMethod]
    public void EveryOpenVinoExecutionFieldChangesTheConfigurationDigest()
    {
        OptimizationExecutionPlan baselinePlan = IssueOpenVino();
        string baseline = baselinePlan.ConfigurationSha256;

        Dictionary<string, string> otherVersions = V2TestData.Versions();
        otherVersions["nncf"] = "3.4.0";

        (string Field, OpenVinoExecutionPayload Payload)[] variants =
        [
            ("configurationId", V2TestData.OpenVinoPayload(
                configurationId: "openvino.standard.cpu.int8.u8.v1")),
            ("maturity", V2TestData.OpenVinoPayload(maturity: "Experimental candidate")),
            ("compiledCacheIsDisposable", V2TestData.OpenVinoPayload(
                compiledCacheIsDisposable: false)),
            ("createsCompletePackage", V2TestData.OpenVinoPayload(
                createsCompletePackage: false)),
            ("runtimeBuild", V2TestData.OpenVinoPayload(
                build: V2TestData.Build(runtimeBuild: "2026.4.0"))),
            ("genAiBuild", V2TestData.OpenVinoPayload(
                build: V2TestData.Build(genAiBuild: "2026.4.0.0"))),
            ("tokenizersBuild", V2TestData.OpenVinoPayload(
                build: V2TestData.Build(tokenizersBuild: "2026.4.0"))),
            ("workerManifestDigest", V2TestData.OpenVinoPayload(
                build: V2TestData.Build(workerManifestDigest: OtherDigest64))),
            ("optimizerVersions", V2TestData.OpenVinoPayload(versions: otherVersions))
        ];

        foreach ((string field, OpenVinoExecutionPayload payload) in variants)
        {
            Assert.AreNotEqual(
                baseline,
                OptimizationCanonicalizer.ConfigurationSha256(
                    baselinePlan.Candidate,
                    OptimizationExecutionPayload.ForOpenVino(payload),
                    contractVersion: 3),
                $"Changing {field} did not change the configuration digest.");
        }
    }

    [TestMethod]
    public void OpenVinoSelectedSettingsChangeTheDigestOnBothSides()
    {
        string baseline = IssueOpenVino().ConfigurationSha256;

        Assert.AreNotEqual(
            baseline,
            IssueOpenVino(
                V2TestData.OpenVinoCandidate(weights: OpenVinoWeightFormat.Int4),
                V2TestData.OpenVinoPayload(target: OpenVinoWeightPrecision.FourBit))
                .ConfigurationSha256,
            "Target weight precision does not reach the digest.");

        Assert.AreNotEqual(
            baseline,
            IssueOpenVino(
                V2TestData.OpenVinoCandidate(cache: OpenVinoKvCacheFormat.RouteDefault),
                V2TestData.OpenVinoPayload(
                    kvCache: OpenVinoKvCachePrecision.ReleasedDefault))
                .ConfigurationSha256,
            "KV cache precision does not reach the digest.");

        Assert.AreNotEqual(
            baseline,
            IssueOpenVino(
                V2TestData.OpenVinoCandidate(
                    compiledCache: Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Disabled),
                V2TestData.OpenVinoPayload(compiledCacheEnabled: false))
                .ConfigurationSha256,
            "Compiled cache policy does not reach the digest.");
    }

    [TestMethod]
    public void OpenVinoSourcePrecisionReachesTheDigest()
    {
        // Source precision cannot be varied alone on a converting plan: with a
        // fixed target, only one source both converts and avoids an upward
        // conversion. So it is varied across two runtime-only pairs, which are
        // the plans an Original candidate agrees with.
        OptimizationCandidate runtimeOnly =
            V2TestData.OpenVinoCandidateRuntimeOnly();

        string eightBit = IssueOpenVino(
            runtimeOnly,
            V2TestData.OpenVinoPayload(
                source: OpenVinoWeightPrecision.EightBit,
                target: OpenVinoWeightPrecision.EightBit,
                createsCompletePackage: false)).ConfigurationSha256;

        string fourBit = IssueOpenVino(
            runtimeOnly,
            V2TestData.OpenVinoPayload(
                source: OpenVinoWeightPrecision.FourBit,
                target: OpenVinoWeightPrecision.FourBit,
                createsCompletePackage: false)).ConfigurationSha256;

        Assert.AreNotEqual(
            eightBit, fourBit, "Source weight precision does not reach the digest.");
    }

    [TestMethod]
    public void TurboQuantEvidenceChangesTheDigest()
    {
        OptimizationCandidate candidate = V2TestData.OpenVinoCandidate();
        string without = OptimizationCanonicalizer.ConfigurationSha256V2(
            candidate,
            OptimizationExecutionPayload.ForOpenVino(V2TestData.OpenVinoPayload()));

        string with = OptimizationCanonicalizer.ConfigurationSha256V2(
            candidate,
            OptimizationExecutionPayload.ForOpenVino(V2TestData.OpenVinoPayload(
                turboQuant: TurboQuantBuildIdentity.Create(
                    Commit40, Commit40, Digest64, OtherDigest64))));

        Assert.AreNotEqual(without, with);
    }

    // ---------- canonicalisation properties ----------

    [TestMethod]
    public void DigestIsStableAcrossRuns()
    {
        Assert.AreEqual(IssueGguf().ConfigurationSha256, IssueGguf().ConfigurationSha256);
        Assert.AreEqual(
            IssueOpenVino().ConfigurationSha256, IssueOpenVino().ConfigurationSha256);
    }

    [TestMethod]
    public void DigestDoesNotDependOnOptimizerVersionEnumerationOrder()
    {
        // A dictionary enumerated in a different order on another machine must
        // still hash the same, or the executor would report drift that is not
        // there.
        Dictionary<string, string> forward = new(StringComparer.Ordinal);
        Dictionary<string, string> reversed = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> entry in V2TestData.Versions())
        {
            forward[entry.Key] = entry.Value;
        }

        foreach (KeyValuePair<string, string> entry in V2TestData.Versions().Reverse())
        {
            reversed[entry.Key] = entry.Value;
        }

        Assert.AreEqual(
            IssueOpenVino(payload: V2TestData.OpenVinoPayload(versions: forward))
                .ConfigurationSha256,
            IssueOpenVino(payload: V2TestData.OpenVinoPayload(versions: reversed))
                .ConfigurationSha256);
    }

    [TestMethod]
    public void DigestIsLowercaseHex()
    {
        StringAssert.Matches(IssueGguf().ConfigurationSha256, new("^[0-9a-f]{64}$"));
        StringAssert.Matches(IssueOpenVino().ConfigurationSha256, new("^[0-9a-f]{64}$"));
    }

    [TestMethod]
    public void FieldValuesCannotBeShiftedBetweenFields()
    {
        // Length-prefixed encoding. Without it, two different field layouts
        // could concatenate to the same bytes and two distinct configurations
        // would share a digest.
        // Both pairs concatenate to the same characters. Without a length
        // prefix per field they would produce identical bytes and two distinct
        // configurations would share a digest.
        string a = IssueGguf(payload: V2TestData.GgufPayload(
            runtimeBuildId: "ab", profileId: "c")).ConfigurationSha256;

        string b = IssueGguf(payload: V2TestData.GgufPayload(
            runtimeBuildId: "a", profileId: "bc")).ConfigurationSha256;

        Assert.AreNotEqual(a, b);
    }

    // ---------- candidate / payload agreement ----------

    [TestMethod]
    public void GgufBackendDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(backend: CompatibilityBackend.Cpu),
            V2TestData.GgufPayload(backend: GgufRuntimeBackend.Sycl)));
    }

    [TestMethod]
    public void GgufDeviceDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(device: DeviceRouteId.Cpu),
            V2TestData.GgufPayload(deviceId: "GPU.0")));
    }

    [TestMethod]
    public void GgufCacheDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(cache: GgufKvCacheFormat.F16),
            V2TestData.GgufPayload(keyCacheType: GgufCacheType.Q8Zero)));

        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(cache: GgufKvCacheFormat.F16),
            V2TestData.GgufPayload(valueCacheType: GgufCacheType.Q8Zero)));
    }

    [TestMethod]
    public void GgufContextDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(context: 4096),
            V2TestData.GgufPayload(contextSize: 8192)));
    }

    [TestMethod]
    public void GgufWeightFormatDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Q4KM),
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q8_0,
                quantiser: V2TestData.Quantiser())));
    }

    [TestMethod]
    public void GgufOffloadDisagreementIsRejected()
    {
        // The exact count must be the one the category implies.
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(offload: GpuOffloadLevel.None),
            V2TestData.GgufPayload(gpuLayerCount: 16)));

        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(
                backend: CompatibilityBackend.IntelSycl,
                device: DeviceRouteId.IntelIntegratedGpu,
                offload: GpuOffloadLevel.Full),
            V2TestData.GgufPayload(
                backend: GgufRuntimeBackend.Sycl, deviceId: "GPU.0", gpuLayerCount: 31)));
    }

    [TestMethod]
    public void OffloadCategoryMapsDeterministicallyToAnExactCount()
    {
        Assert.AreEqual(0, GgufOffloadPolicy.ExactLayerCount(GpuOffloadLevel.None, 32));
        Assert.AreEqual(16, GgufOffloadPolicy.ExactLayerCount(GpuOffloadLevel.Partial, 32));
        Assert.AreEqual(32, GgufOffloadPolicy.ExactLayerCount(GpuOffloadLevel.Full, 32));

        // Same inputs, same answer, every time.
        Assert.AreEqual(
            GgufOffloadPolicy.ExactLayerCount(GpuOffloadLevel.Partial, 33),
            GgufOffloadPolicy.ExactLayerCount(GpuOffloadLevel.Partial, 33));
    }

    [TestMethod]
    public void UnspecifiedOffloadHasNoExactCount()
    {
        // Zero is a real placement - everything on the CPU - so it must not
        // double as "we do not know".
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => GgufOffloadPolicy.ExactLayerCount(GpuOffloadLevel.Unspecified, 32));
    }

    [TestMethod]
    public void PartialOffloadIsAcceptedWhenItMatches()
    {
        // A GPU device, because the route configuration already refuses to
        // offload layers from a CPU-only placement.
        OptimizationExecutionPlan plan = IssueGguf(
            V2TestData.GgufCandidate(
                backend: CompatibilityBackend.IntelSycl,
                device: DeviceRouteId.IntelIntegratedGpu,
                offload: GpuOffloadLevel.Partial),
            V2TestData.GgufPayload(
                backend: GgufRuntimeBackend.Sycl,
                deviceId: "GPU.0",
                gpuLayerCount: 16));

        Assert.AreEqual(16, plan.ExecutionPayload.Gguf!.GpuLayerCount);
    }

    [TestMethod]
    public void OpenVinoWeightPrecisionDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueOpenVino(
            V2TestData.OpenVinoCandidate(weights: OpenVinoWeightFormat.Int8),
            V2TestData.OpenVinoPayload(target: OpenVinoWeightPrecision.FourBit)));
    }

    [TestMethod]
    public void OpenVinoCacheDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueOpenVino(
            V2TestData.OpenVinoCandidate(cache: OpenVinoKvCacheFormat.U8),
            V2TestData.OpenVinoPayload(
                kvCache: OpenVinoKvCachePrecision.ReleasedDefault)));
    }

    [TestMethod]
    public void OpenVinoCompiledCacheDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueOpenVino(
            V2TestData.OpenVinoCandidate(
                compiledCache: Core.Routes.OpenVino.OpenVinoCompiledCachePolicy.Enabled),
            V2TestData.OpenVinoPayload(compiledCacheEnabled: false)));
    }

    [TestMethod]
    public void OpenVinoEvidenceDisagreementIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IssueOpenVino(
            payload: V2TestData.OpenVinoPayload(evidenceId: "other-evidence")));
    }

    [TestMethod]
    public void PersistenceDisagreementIsRejected()
    {
        // The candidate says runtime-only, the payload converts. The user would
        // be shown one promise and given the other.
        Assert.ThrowsExactly<ArgumentException>(() => IssueGguf(
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Imported),
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser())));
    }

    // ---------- quantiser identity completeness ----------

    [TestMethod]
    public void PersistentGgufConversionRequiresACompleteQuantiserIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM, quantiser: null));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-digest")]
    [DataRow("AAAA111111111111111111111111111111111111111111111111111111111111")]
    public void QuantiserWithoutAValidExecutableDigestIsRefused(string digest)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => GgufQuantiserIdentity.Create("pkg", "v1", digest));
    }

    [TestMethod]
    public void RuntimeOnlyWorkCannotClaimAQuantisedArtifact()
    {
        // A quantiser named on a runtime-only payload advertises a conversion
        // that never happens.
        Assert.ThrowsExactly<ArgumentException>(
            () => V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Imported,
                quantiser: V2TestData.Quantiser()));
    }

    [TestMethod]
    public void CompiledCacheCannotBeDeclaredAModelArtifact()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoExecutionPayload.Create(
                "openvino.standard.cpu.int8.default.v1", "CPU", "Standard candidate",
                "ov-evidence", OpenVinoWeightPrecision.Fp16,
                OpenVinoWeightPrecision.EightBit, OpenVinoKvCachePrecision.U8,
                true, true, compiledCacheIsModelArtifact: true, true,
                V2TestData.Build(), V2TestData.Versions()));
    }

    [TestMethod]
    public void OpenVinoUpwardConversionIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => V2TestData.OpenVinoPayload(
                source: OpenVinoWeightPrecision.FourBit,
                target: OpenVinoWeightPrecision.Fp16));
    }

    // ---------- runtime field validation ----------

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void NonPositiveRuntimeQuantitiesAreRefused(int value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => V2TestData.GgufPayload(contextSize: value));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => V2TestData.GgufPayload(threadCount: value));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => V2TestData.GgufPayload(batchSize: value));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => V2TestData.GgufPayload(maximumGeneratedTokens: value));
    }

    [TestMethod]
    public void NegativeGpuLayerCountIsRefused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => V2TestData.GgufPayload(gpuLayerCount: -1));
    }

    [TestMethod]
    [DataRow("short")]
    [DataRow("ABCDEF0123456789ABCDEF0123456789ABCDEF01")]
    [DataRow("0123456789abcdef0123456789abcdef0123456")]
    public void MalformedRuntimeSourceCommitIsRefused(string commit)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => V2TestData.GgufPayload(runtimeSourceCommit: commit));
    }

    [TestMethod]
    public void EmptyOptimizerVersionsAreRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => V2TestData.OpenVinoPayload(
                versions: new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    // ---------- privacy ----------

    [TestMethod]
    public void NoPayloadMemberCarriesAPath()
    {
        // Paths reach manifests, logs and support records. The plan is the wrong
        // place for one, so every adapter-supplied identifier goes through the
        // enforced shape check.
        foreach (string pathLike in new[]
        {
            @"C:\models\granite.gguf", "/home/user/model", @"..\..\secrets", "share|name"
        })
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => V2TestData.GgufPayload(deviceId: pathLike),
                $"deviceId accepted {pathLike}");

            Assert.ThrowsExactly<ArgumentException>(
                () => V2TestData.GgufPayload(profileId: pathLike),
                $"profileId accepted {pathLike}");

            Assert.ThrowsExactly<ArgumentException>(
                () => GgufQuantiserIdentity.Create(pathLike, "v1", Digest64),
                $"quantiser packageId accepted {pathLike}");

            Assert.ThrowsExactly<ArgumentException>(
                () => V2TestData.OpenVinoPayload(configurationId: pathLike),
                $"configurationId accepted {pathLike}");
        }
    }

    [TestMethod]
    public void NoPlanOrPayloadMemberIsTypedAsAPath()
    {
        // A member named or typed as a path would be one a serialiser writes
        // out. The trusted contexts hold paths instead, and they are not part
        // of the plan.
        Type[] planTypes =
        [
            typeof(OptimizationExecutionPlan),
            typeof(OptimizationExecutionPayload),
            typeof(GgufExecutionPayload),
            typeof(OpenVinoExecutionPayload),
            typeof(GgufQuantiserIdentity),
            typeof(OpenVinoBuildIdentity),
            typeof(TurboQuantBuildIdentity),
            typeof(OptimizationExecutionResult)
        ];

        foreach (Type type in planTypes)
        {
            foreach (PropertyInfo property in type.GetProperties())
            {
                foreach (string forbidden in new[] { "Path", "Directory", "FileName", "Folder" })
                {
                    Assert.IsFalse(
                        property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                        $"{type.Name}.{property.Name} names a filesystem location.");
                }
            }
        }
    }

    [TestMethod]
    public void TrustedContextsDoNotExposePathsThroughToString()
    {
        // The single most likely leak: a logger that formats its arguments, or
        // an interpolated string in a diagnostic.
        OptimizationExecutionPlan plan = IssueGguf(
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Q4KM),
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser()));

        string secret = @"C:\Users\someone\private\granite.gguf";

        TrustedSourceContext source = TrustedSourceContext.ForPlan(plan, secret);
        TrustedToolContext tool = TrustedToolContext.ForGgufQuantiser(plan, secret);

        Assert.IsFalse(source.ToString().Contains("private", StringComparison.Ordinal));
        Assert.IsFalse(source.ToString().Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(tool.ToString().Contains("private", StringComparison.Ordinal));
        Assert.IsFalse(tool.ToString().Contains(secret, StringComparison.Ordinal));

        // Nor through any readable member.
        foreach (PropertyInfo property in source.GetType().GetProperties())
        {
            Assert.IsFalse(
                property.GetValue(source) is string value
                    && value.Contains(secret, StringComparison.Ordinal),
                $"TrustedSourceContext.{property.Name} exposes the path.");
        }
    }

    [TestMethod]
    public void SourcePathDoesNotReachTheConfigurationDigest()
    {
        OptimizationExecutionPlan plan = IssueGguf();

        _ = TrustedSourceContext.ForPlan(plan, @"C:\models\granite.gguf");

        Assert.AreEqual(IssueGguf().ConfigurationSha256, plan.ConfigurationSha256);
    }

    [TestMethod]
    public void ResolutionOutcomeCarriesNoPath()
    {
        TrustedResolution resolution =
            TrustedResolution.Of(TrustedResolutionOutcome.DigestMismatch)
                is { } value ? value : throw new AssertFailedException("unreachable");

        Assert.AreEqual("DigestMismatch", resolution.ToString());
    }

    // ---------- source and tool verification ----------

    [TestMethod]
    public void SourceLengthMismatchFailsBeforeAnyToolCouldLaunch()
    {
        OptimizationExecutionPlan plan = IssueGguf();

        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(file, "wrong length");

        try
        {
            TrustedResolution resolution =
                TrustedSourceContext.ForPlan(plan, file).Verify(plan);

            Assert.AreEqual(TrustedResolutionOutcome.LengthMismatch, resolution.Outcome);
            Assert.IsFalse(resolution.IsVerified);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void SourceDigestMismatchFailsWhenTheLengthHappensToMatch()
    {
        // The case a length check alone would wave through: right size, wrong
        // bytes.
        byte[] content = new byte[64];
        string digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

        OptimizationExecutionPlan plan = IssueGguf(binding:
            OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", digest, 64, "hw-run-1", OtherDigest64),
            modelLayers: ModelLayers);

        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        byte[] different = new byte[64];
        different[0] = 1;
        File.WriteAllBytes(file, different);

        try
        {
            Assert.AreEqual(
                TrustedResolutionOutcome.DigestMismatch,
                TrustedSourceContext.ForPlan(plan, file).Verify(plan).Outcome);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void MatchingSourceVerifies()
    {
        byte[] content = new byte[64];
        string digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

        OptimizationExecutionPlan plan = IssueGguf(binding:
            OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", digest, 64, "hw-run-1", OtherDigest64),
            modelLayers: ModelLayers);

        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllBytes(file, content);

        try
        {
            TrustedSourceContext context = TrustedSourceContext.ForPlan(plan, file);

            Assert.IsTrue(context.Verify(plan).IsVerified);
            Assert.AreEqual(Path.GetFullPath(file), context.RevealVerifiedPath(plan));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void MissingSourceIsNotARegularFile()
    {
        OptimizationExecutionPlan plan = IssueGguf();

        Assert.AreEqual(
            TrustedResolutionOutcome.NotARegularFile,
            TrustedSourceContext
                .ForPlan(plan, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))
                .Verify(plan)
                .Outcome);
    }

    [TestMethod]
    public void DirectoryIsNotARegularFile()
    {
        OptimizationExecutionPlan plan = IssueGguf();

        Assert.AreEqual(
            TrustedResolutionOutcome.NotARegularFile,
            TrustedSourceContext.ForPlan(plan, Path.GetTempPath()).Verify(plan).Outcome);
    }

    [TestMethod]
    public void UnverifiedSourcePathIsNotReleased()
    {
        OptimizationExecutionPlan plan = IssueGguf();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedSourceContext
                .ForPlan(plan, Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))
                .RevealVerifiedPath(plan));
    }

    [TestMethod]
    public void SourceContextFromAnotherPlanIsRejected()
    {
        OptimizationExecutionPlan first = IssueGguf();
        OptimizationExecutionPlan second = IssueGguf();

        Assert.AreEqual(
            TrustedResolutionOutcome.PlanMismatch,
            TrustedSourceContext.ForPlan(first, "whatever").Verify(second).Outcome);
    }

    [TestMethod]
    public void ToolDigestMismatchFailsBeforeLaunch()
    {
        OptimizationExecutionPlan plan = IssueGguf(
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Q4KM),
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser()));

        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(file, "not the admitted executable");

        try
        {
            TrustedToolContext tool = TrustedToolContext.ForGgufQuantiser(plan, file);

            Assert.AreEqual(
                TrustedResolutionOutcome.DigestMismatch, tool.Verify(plan).Outcome);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => tool.RevealVerifiedPath(plan));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void MatchingToolVerifies()
    {
        byte[] executable = [1, 2, 3, 4];
        string digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(executable)).ToLowerInvariant();

        OptimizationExecutionPlan plan = IssueGguf(
            V2TestData.GgufCandidate(weights: GgufWeightFormat.Q4KM),
            V2TestData.GgufPayload(
                persistentTarget: GgufWeightFormat.Q4KM,
                quantiser: V2TestData.Quantiser(executableSha256: digest)));

        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllBytes(file, executable);

        try
        {
            Assert.IsTrue(
                TrustedToolContext.ForGgufQuantiser(plan, file).Verify(plan).IsVerified);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void RuntimeOnlyPlanAuthorisesNoTool()
    {
        // A tool context on a plan that converts nothing would let a conversion
        // run under a plan that never proposed one.
        Assert.ThrowsExactly<InvalidOperationException>(
            () => TrustedToolContext.ForGgufQuantiser(IssueGguf(), "anything"));
    }

    // ---------- unchanged behaviour ----------

    [TestMethod]
    public void SeamNamesAreUnchanged()
    {
        Type binding = typeof(OptimizationJourneyBinding);

        foreach (string member in new[]
        {
            "ModelInspectionRunId", "ModelInspectionHandoffId", "ModelSha256",
            "ModelLengthBytes", "ProductHardwareRunId", "HardwareSnapshotSha256"
        })
        {
            Assert.IsNotNull(binding.GetProperty(member), $"The binding lost {member}.");
        }
    }

    [TestMethod]
    public void PreferenceLabelsAndRangesAreUnchanged()
    {
        (int Value, string Label)[] expected =
        [
            (0, "Maximum efficiency"), (19, "Maximum efficiency"),
            (20, "Efficient"), (39, "Efficient"),
            (40, "Balanced"), (59, "Balanced"),
            (60, "High capability"), (79, "High capability"),
            (80, "Maximum capability"), (100, "Maximum capability")
        ];

        foreach ((int value, string label) in expected)
        {
            Assert.AreEqual(
                label,
                OptimizationPreferenceLabelPolicy.GetLabel(
                    OptimizationPreferenceSelection.Manual(value)));
        }
    }

    [TestMethod]
    public void TrustedContextsAreNotReachableFromAPlan()
    {
        // The claim the privacy allowlist makes for these two types. They hold
        // a path, so nothing a serialiser walks may reach them: if the plan
        // exposed one, the path would travel wherever the plan travels and
        // every other control here would be decoration.
        Type[] serialisable =
        [
            typeof(OptimizationExecutionPlan),
            typeof(OptimizationExecutionPayload),
            typeof(GgufExecutionPayload),
            typeof(OpenVinoExecutionPayload),
            typeof(GgufQuantiserIdentity),
            typeof(OpenVinoBuildIdentity),
            typeof(TurboQuantBuildIdentity),
            typeof(OptimizationJourneyBinding),
            typeof(OptimizationCandidate),
            typeof(OptimizationCandidateMetrics),
            typeof(OptimizationCapabilitySnapshot),
            typeof(OptimizationExecutionResult)
        ];

        Type[] forbidden =
        [
            typeof(TrustedSourceContext),
            typeof(TrustedToolContext)
        ];

        HashSet<Type> seen = [];
        List<string> reachable = [];

        foreach (Type root in serialisable)
        {
            Walk(root, root.Name);
        }

        Assert.AreEqual(
            0,
            reachable.Count,
            "A path-carrying context is reachable from something a serialiser "
                + "walks: " + string.Join(", ", reachable));

        void Walk(Type type, string path)
        {
            if (!seen.Add(type))
            {
                return;
            }

            foreach (PropertyInfo property in type.GetProperties())
            {
                Type carried = property.PropertyType;
                string here = path + "." + property.Name;

                if (forbidden.Contains(carried))
                {
                    reachable.Add(here);
                    continue;
                }

                if (carried.Assembly == typeof(OptimizationExecutionPlan).Assembly)
                {
                    Walk(carried, here);
                }
            }
        }
    }

    [TestMethod]
    public void TrustedContextsAreNotRecords()
    {
        // A record would synthesize a member-printing ToString and a
        // member-comparing Equals, which is exactly what these types must not
        // have. Plain sealed classes on purpose.
        foreach (Type type in new[]
        {
            typeof(TrustedSourceContext), typeof(TrustedToolContext)
        })
        {
            Assert.IsNull(
                type.GetMethod(
                    "PrintMembers",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                type.Name + " is a record, so its members can be printed.");

            Assert.IsTrue(type.IsSealed, type.Name + " is not sealed.");
        }
    }

    [TestMethod]
    public void NoTrustedContextMemberReturnsThePathUngated()
    {
        // Only RevealVerifiedPath returns it, and only after verification. A
        // plain property would be read by something that had not checked.
        OptimizationExecutionPlan plan = IssueGguf();
        string secret = Path.Combine("Z:", "someone", "private", "granite.gguf");

        TrustedSourceContext context = TrustedSourceContext.ForPlan(plan, secret);

        foreach (PropertyInfo property in context.GetType().GetProperties())
        {
            object? value = property.GetValue(context);

            Assert.IsFalse(
                value is string text && text.Contains("private", StringComparison.Ordinal),
                "TrustedSourceContext." + property.Name + " returns the path ungated.");
        }

        // And the one gated accessor refuses while unverified.
        Assert.ThrowsExactly<InvalidOperationException>(
            () => context.RevealVerifiedPath(plan));
    }
}
