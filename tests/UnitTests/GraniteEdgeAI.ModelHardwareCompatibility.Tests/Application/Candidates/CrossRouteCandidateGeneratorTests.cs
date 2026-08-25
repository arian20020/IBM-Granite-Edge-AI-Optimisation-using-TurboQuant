using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Candidates;

/// <summary>
/// One generator, two routes, no leakage.
///
/// The property that matters is not that both routes produce candidates - it is
/// that the shared layer never has to know which route it is looking at in
/// order to compare them, and never quietly admits a combination nothing
/// evidenced.
/// </summary>
[TestClass]
public sealed class CrossRouteCandidateGeneratorTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const string Digest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public void GenerationResultOwnsTrulyImmutableCollections()
    {
        OptimizationCandidate candidate = Generate(CrossRouteTestData.OpenVinoSnapshot(
            CrossRouteTestData.OpenVino("ov-u8", OpenVinoWeightFormat.Fp16))).Candidates.Single();
        List<OptimizationCandidate> candidates = [candidate];
        List<OptimizationExclusion> exclusions =
        [
            new("excluded", "descriptor", OptimizationExclusionReason.ExperimentalNotAdmitted)
        ];
        CrossRouteGenerationResult result = new(candidates, exclusions);

        candidates.Clear();
        exclusions.Clear();

        Assert.AreEqual(1, result.Candidates.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.ThrowsExactly<NotSupportedException>(
            () => ((IList<OptimizationCandidate>)result.Candidates).Clear());
        Assert.ThrowsExactly<NotSupportedException>(
            () => ((IList<OptimizationExclusion>)result.Exclusions).Clear());
    }

    [TestMethod]
    public void UndefinedSupportCannotGenerateThroughAMutatedOpenVinoAdmission()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-invalid-support", OpenVinoWeightFormat.Int8);
        SetSupportLevel(admitted, (SupportLevel)(-1));

        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(admitted));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void UndefinedSupportCannotSealAMutatedGgufAdmission()
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf(
            "gguf-invalid-support", GgufWeightFormat.Q4KM);
        SetSupportLevel(admitted, (SupportLevel)3);

        Assert.ThrowsExactly<ArgumentException>(() =>
            CrossRouteTestData.GgufSnapshot(admitted));
    }

    private static void SetSupportLevel(object admitted, SupportLevel level) =>
        admitted.GetType().GetField(
            "<Level>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(admitted, level);

    private static class CrossRouteTestData
    {
        internal static InspectedModelFacts Facts(int fileType = 15) =>
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, fileType, 2);

        internal static OptimizationWorkload Workload(
            OptimizationAssessment floor = OptimizationAssessment.Poor,
            int minimumContext = 512) =>
            OptimizationWorkload.Create(
                "chat",
                minimumContext,
                floor,
                [ContextTokenCount.FromTokens(4096)]);

        internal static OpenVinoAdmittedConfiguration OpenVino(
            string id,
            OpenVinoWeightFormat weights,
            SupportLevel level = SupportLevel.DeclaredSupported,
            DeviceRouteId device = DeviceRouteId.Cpu,
            OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8) =>
            OpenVinoAdmittedConfiguration.Create(
                id, device, weights, cache,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Disabled,
                1, 512, 32768, level, level == SupportLevel.Experimental);

        internal static GgufAdmittedConfiguration Gguf(
            string id, GgufWeightFormat weights,
            SupportLevel level = SupportLevel.DeclaredSupported) =>
            GgufAdmittedConfiguration.Create(
                id, CompatibilityBackend.Cpu, DeviceRouteId.Cpu, weights,
                GgufKvCacheFormat.F16, GpuOffloadLevel.None, 512, 32768, level,
                level == SupportLevel.Experimental);

        internal static OptimizationCapabilitySnapshot OpenVinoSnapshot(
            params OpenVinoAdmittedConfiguration[] admitted) =>
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", Digest, OpenVinoCapabilityPayload.Create(
                    "2026.1.0", admitted,
                    [.. admitted.Select(entry => OpenVinoExecutionAuthority.Create(
                        entry.EvidenceId,
                        entry.EvidenceId,
                        OpenVinoWeightPrecision.Fp16,
                        OpenVinoBuildIdentity.Create(
                            "2026.1.0", "2026.1.0", "2026.1.0", Digest),
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["openvino"] = "2026.1.0"
                        },
                        compiledCacheIsDisposable: true,
                        turboQuantBuild: entry.KvCache is OpenVinoKvCacheFormat.TurboQuantTbq4
                            or OpenVinoKvCacheFormat.TurboQuantTbq3
                            ? TurboQuantBuildIdentity.Create(
                                Commit, Commit, Digest, Digest)
                            : null))]));

        internal static OptimizationCapabilitySnapshot GgufSnapshot(
            params GgufAdmittedConfiguration[] admitted) =>
            GgufSnapshotWithPolicy(null, false, admitted);

        internal static OptimizationCapabilitySnapshot GgufSnapshotWithPolicy(
            GgufRequantisationPolicy? requantisationPolicy = null,
            bool hasHigherPrecisionSource = false,
            params GgufAdmittedConfiguration[] admitted)
        {
            GgufConversionSourceBinding? source = hasHigherPrecisionSource
                ? Source(WeightQuantisation.F16)
                : requantisationPolicy?.Source;

            return OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", Digest, GgufCapabilityPayload.Create(
                    "b4321", admitted, hasHigherPrecisionSource, requantisationPolicy,
                    source, source is null ? null : Quantiser(),
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        "b4321", Commit,
                        [.. admitted.Select(entry =>
                            GgufExecutionProfileAuthority.Create(
                                entry.EvidenceId, EvidenceGrade.Estimated, "profile",
                                false, 4, 128, 256))])));
        }

        internal static OptimizationJourneyBinding Binding() =>
            OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", Digest, 3 * Gibibyte,
                "hw-run-1", Digest);

        internal static GgufQuantiserIdentity Quantiser() =>
            GgufQuantiserIdentity.Create("llama-quantize", "b4321", Digest);

        internal static GgufRuntimeAuthority RuntimeAuthority(
            string runtime,
            string sourceCommit,
            params GgufAdmittedConfiguration[] admitted) =>
            GgufRuntimeAuthority.Create(
                runtime, sourceCommit,
                [.. admitted.Select(entry => GgufExecutionProfileAuthority.Create(
                    entry.EvidenceId, EvidenceGrade.Estimated, "profile",
                    false, 4, 128, 256))]);

        internal static GgufConversionSourceBinding Source(
            WeightQuantisation precision = WeightQuantisation.Q4_K_M) =>
            GgufConversionSourceBinding.Create(precision, Binding());

        internal static GgufRequantisationPolicy Requantisation(
            string evidenceId = "gguf-q2",
            bool acknowledged = true,
            bool preserveOriginal = true,
            bool requireNewOutput = true) =>
            GgufRequantisationPolicy.Create(
                acknowledged,
                preserveOriginal,
                requireNewOutput,
                Gguf(evidenceId, GgufWeightFormat.Q2K),
                Quantiser(),
                Source());
    }

    private static CrossRouteGenerationResult Generate(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload? workload = null,
        InspectedModelFacts? facts = null,
        ulong budgetGibibytes = 32,
        ulong diskGibibytes = 500,
        params string[] optedIn) =>
        CrossRouteCandidateGenerator.Generate(
            snapshot,
            facts ?? CrossRouteTestData.Facts(),
            workload ?? CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(),
            ByteCount.FromBytes(budgetGibibytes * Gibibyte),
            ByteCount.FromBytes(diskGibibytes * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(optedIn),
            HardwareAuthority());

    private static OptimizationHardwareAuthority HardwareAuthority() =>
        OptimizationHardwareAuthority.Create(
            Digest,
            [DeviceRouteId.Cpu, DeviceRouteId.IntelIntegratedGpu,
             DeviceRouteId.IntelDiscreteGpu, DeviceRouteId.IntelNpu],
            [CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl,
             CompatibilityBackend.IntelVulkan, CompatibilityBackend.OpenVinoCpu,
             CompatibilityBackend.OpenVinoGpu, CompatibilityBackend.OpenVinoNpu],
            ByteCount.FromBytes(64 * Gibibyte),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "test-freshness-v1");

    private static OptimizationHardwareAuthority HardwareAuthority(
        DeviceRouteId device,
        CompatibilityBackend backend,
        ulong? dedicatedBytes = 64 * Gibibyte) =>
        OptimizationHardwareAuthority.Create(
            Digest, [device], [backend],
            dedicatedBytes.HasValue
                ? ByteCount.FromBytes(dedicatedBytes.Value)
                : null,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            "test-freshness-v1");

    [TestMethod]
    public void BothRoutesGenerateCompleteCandidates()
    {
        CrossRouteGenerationResult openVino = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8)));

        CrossRouteGenerationResult gguf = Generate(
            CrossRouteTestData.GgufSnapshot(
                CrossRouteTestData.Gguf("gguf-q4", GgufWeightFormat.Q4KM)));

        Assert.AreEqual(1, openVino.Candidates.Count);
        Assert.AreEqual(1, gguf.Candidates.Count);
        Assert.AreEqual(OptimizationRoute.OpenVino, openVino.Candidates[0].Route);
        Assert.AreEqual(OptimizationRoute.Gguf, gguf.Candidates[0].Route);
    }

    [TestMethod]
    public void AlternativeClaimingAbsentOpenVinoNpuIsExcluded()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-npu", OpenVinoWeightFormat.Int8,
            device: DeviceRouteId.IntelNpu);
        CrossRouteGenerationResult result = CrossRouteCandidateGenerator.Generate(
            CrossRouteTestData.OpenVinoSnapshot(admitted),
            CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.HardwareCapabilityUnavailable,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void AlternativeClaimingAbsentGgufGpuBackendIsExcluded()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-sycl", CompatibilityBackend.IntelSycl,
            DeviceRouteId.IntelIntegratedGpu, GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16, GpuOffloadLevel.Full, 512, 8192,
            SupportLevel.DeclaredSupported, false);
        CrossRouteGenerationResult result = CrossRouteCandidateGenerator.Generate(
            CrossRouteTestData.GgufSnapshot(admitted),
            CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.Cpu, CompatibilityBackend.Cpu));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.HardwareCapabilityUnavailable,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void DedicatedMemoryDemandRequiresEstablishedSufficientDedicatedBudget()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-gpu", OpenVinoWeightFormat.Fp16,
            device: DeviceRouteId.IntelDiscreteGpu);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.OpenVinoSnapshot(admitted);

        CrossRouteGenerationResult unknown = CrossRouteCandidateGenerator.Generate(
            snapshot, CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, null));
        CrossRouteGenerationResult insufficient = CrossRouteCandidateGenerator.Generate(
            snapshot, CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, 1));

        Assert.AreEqual(
            OptimizationExclusionReason.DedicatedMemoryNotEstablished,
            unknown.Exclusions.Single().Reason);
        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsDedicatedDeviceMemory,
            insufficient.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void AlternativeDedicatedBudget_ExactBoundaryFits_OneByteLessFails()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-gpu-boundary", OpenVinoWeightFormat.Fp16,
            device: DeviceRouteId.IntelDiscreteGpu);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.OpenVinoSnapshot(admitted);
        InspectedModelFacts facts = CrossRouteTestData.Facts();
        OptimizationWorkload workload = CrossRouteTestData.Workload();
        ContextTokenCount context = workload.CandidateContexts.Single();
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            admitted.Weights, admitted.KvCache, admitted.Device,
            admitted.PerformanceHint, admitted.CompiledCache, admitted.Streams);
        ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
            facts, configuration, context, EstimatorPolicy.ProvisionalV1());
        ulong dedicatedPeak = ResourcePhaseComposer.Compose(estimate.Components)
            .PeakFor(ResourceTarget.DedicatedDeviceMemory).Bytes;

        CrossRouteGenerationResult exact = CrossRouteCandidateGenerator.Generate(
            snapshot, facts, workload, CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, dedicatedPeak));
        CrossRouteGenerationResult shortByOne = CrossRouteCandidateGenerator.Generate(
            snapshot, facts, workload, CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, dedicatedPeak - 1));

        Assert.AreEqual(1, exact.Candidates.Count);
        Assert.AreEqual(
            dedicatedPeak,
            exact.Candidates.Single().Metrics.DedicatedRequiredBytes);
        Assert.AreEqual(
            dedicatedPeak,
            exact.Candidates.Single().Metrics.DedicatedSafeBudgetBytes);
        Assert.AreEqual(
            0UL,
            exact.Candidates.Single().Metrics.DedicatedHeadroomBytes);
        OptimizationAdmissionProof proof =
            exact.Candidates.Single().AdmissionProof!;
        Assert.IsNotNull(proof);
        Assert.AreEqual(
            dedicatedPeak,
            proof.DedicatedRequiredBytes);
        Assert.AreEqual(
            dedicatedPeak,
            proof.DedicatedSafeBudgetBytes);
        Assert.AreEqual(
            0UL,
            proof.DedicatedHeadroomBytes);
        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsDedicatedDeviceMemory,
            shortByOne.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void CpuCandidateCarriesNoFabricatedDedicatedMemoryAxis()
    {
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-cpu", OpenVinoWeightFormat.Original))).Candidates.Single();

        Assert.IsNull(candidate.Metrics.DedicatedRequiredBytes);
        Assert.IsNull(candidate.Metrics.DedicatedSafeBudgetBytes);
        Assert.IsNull(candidate.Metrics.DedicatedHeadroomBytes);
    }

    [TestMethod]
    public void GgufCandidateWithoutExactRuntimeProfileAuthorityIsTypedExcluded()
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf(
            "gguf-no-runtime-authority", GgufWeightFormat.Imported);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create("b4321", [admitted]));

        CrossRouteGenerationResult result = Generate(snapshot);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExecutionAuthorityNotEstablished,
            result.Exclusions[0].Reason);
    }

    [TestMethod]
    public void OpenVinoCandidateWithoutExactExecutionAuthorityIsTypedExcluded()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-no-execution-authority", OpenVinoWeightFormat.Original);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-cap", Digest,
            OpenVinoCapabilityPayload.Create("2026.1.0", [admitted]));

        CrossRouteGenerationResult result = Generate(snapshot);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExecutionAuthorityNotEstablished,
            result.Exclusions[0].Reason);
    }

    [TestMethod]
    public void ReleasedEvidenceRequirementIsFailClosedWithoutItsExactOptIn()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-evidence-bound", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: true);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.GgufSnapshot(admitted);

        CrossRouteGenerationResult absent = Generate(snapshot);
        CrossRouteGenerationResult exact = Generate(snapshot, optedIn: admitted.EvidenceId);

        Assert.AreEqual(0, absent.Candidates.Count);
        Assert.AreEqual(1, absent.Exclusions.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            absent.Exclusions[0].Reason);
        Assert.AreEqual(1, exact.Candidates.Count);
    }

    [TestMethod]
    public void GgufTurboQuantCapabilitiesRemainBackendDeviceAndSourceSpecific()
    {
        var implementations = new[]
        {
            new
            {
                Evidence = "turbo3",
                Runtime = "turbo3",
                Source = "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                Backend = CompatibilityBackend.IntelVulkan,
                Device = DeviceRouteId.IntelIntegratedGpu
            },
            new
            {
                Evidence = "tq3_0",
                Runtime = "tq3_0",
                Source = "5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc",
                Backend = CompatibilityBackend.IntelSycl,
                Device = DeviceRouteId.IntelDiscreteGpu
            }
        };

        foreach (var implementation in implementations)
        {
            GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
                implementation.Evidence,
                implementation.Backend,
                implementation.Device,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.TurboQuant3Bit,
                GpuOffloadLevel.Full,
                512,
                32768,
                SupportLevel.Experimental,
                requiresEvidence: true);
            OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap",
                Digest,
                GgufCapabilityPayload.Create(
                    implementation.Runtime,
                    [admitted],
                    turboQuantImplementation:
                        GgufTurboQuantImplementationIdentity.Create(
                            implementation.Runtime,
                            implementation.Source,
                            implementation.Backend,
                            implementation.Device),
                    runtimeAuthority: CrossRouteTestData.RuntimeAuthority(
                        implementation.Runtime, implementation.Source, admitted)));

            CrossRouteGenerationResult absent = Generate(snapshot);
            Assert.AreEqual(0, absent.Candidates.Count);
            Assert.AreEqual(
                OptimizationExclusionReason.ExperimentalNotAdmitted,
                absent.Exclusions.Single().Reason);

            OptimizationCandidate candidate = Generate(
                snapshot,
                optedIn: implementation.Evidence).Candidates.Single();
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;

            Assert.AreEqual(implementation.Runtime, snapshot.Gguf!.RuntimeVersion);
            Assert.AreEqual(
                implementation.Source,
                snapshot.Gguf.TurboQuantImplementation!.SourceCommit);
            Assert.AreEqual(implementation.Evidence, candidate.EvidenceId);
            Assert.AreEqual(implementation.Backend, configuration.Backend);
            Assert.AreEqual(implementation.Device, configuration.Device);
            Assert.AreEqual(GgufKvCacheFormat.TurboQuant3Bit, configuration.KvCache);
        }
    }

    [TestMethod]
    public void CandidateRouteAlwaysAgreesWithItsConfiguration()
    {
        // The discriminator is derived, not supplied, so nothing can label a
        // GGUF configuration as OpenVINO and send it to the wrong executor.
        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8))).Candidates)
        {
            Assert.IsInstanceOfType<OpenVinoRouteConfiguration>(candidate.Configuration);
        }

        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.GgufSnapshot(
                CrossRouteTestData.Gguf("gguf-q4", GgufWeightFormat.Q4KM))).Candidates)
        {
            Assert.IsInstanceOfType<GgufRouteConfiguration>(candidate.Configuration);
        }
    }

    [TestMethod]
    public void SharedMetricsCarryNoRouteVocabulary()
    {
        // Route-field leakage check. Everything the shared layer ranks on must
        // be expressible for both routes, so the metrics record may not name a
        // representation from either.
        string[] properties = typeof(OptimizationCandidateMetrics)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (string name in properties)
        {
            foreach (string foreign in new[] { "Gguf", "OpenVino", "Quant", "Kv" })
            {
                Assert.IsFalse(
                    name.Contains(foreign, StringComparison.OrdinalIgnoreCase),
                    $"Shared metrics expose the route-specific member {name}.");
            }
        }
    }

    [TestMethod]
    public void ExperimentalEntryIsAbsentWithoutAnExactOptIn()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-tbq4", OpenVinoWeightFormat.Int4, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq4)));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExperimentalNotAdmitted,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void OptingInToOneExperimentalEntryDoesNotAdmitAnother()
    {
        // Opting in is per evidence record. A user who accepted one
        // experimental route has not accepted every experimental route.
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-tbq4", OpenVinoWeightFormat.Int4, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq4),
                CrossRouteTestData.OpenVino(
                    "ov-tbq3", OpenVinoWeightFormat.Int4, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3)),
            optedIn: "ov-tbq4");

        Assert.AreEqual(1, result.Candidates.Count);
        Assert.AreEqual("ov-tbq4", result.Candidates[0].EvidenceId);
        Assert.IsTrue(result.Candidates[0].IsExperimental);
    }

    [TestMethod]
    public void TurboQuantCacheQualityIsCombinedConservativelyWithWeightQuality()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-tbq4",
                    OpenVinoWeightFormat.Fp16,
                    SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq4)),
            optedIn: "ov-tbq4");

        Assert.AreEqual(1, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationAssessment.Acceptable,
            result.Candidates[0].Metrics.Quality,
            "TBQ4 cache compression was omitted from the combined quality grade.");
    }

    [TestMethod]
    public void CacheCompressionPreservesWeightIdentityAndAddsItsOwnQualityEffect()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "f16-cache", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "u8-cache", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U8)));

        OptimizationCandidate f16 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "f16-cache");
        OptimizationCandidate u8 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "u8-cache");

        Assert.AreEqual(
            OpenVinoWeightFormat.Fp16,
            ((OpenVinoRouteConfiguration)f16.Configuration).Weights);
        Assert.AreEqual(
            OpenVinoWeightFormat.Fp16,
            ((OpenVinoRouteConfiguration)u8.Configuration).Weights);
        Assert.AreEqual(OptimizationAssessment.Excellent, f16.Metrics.Quality);
        Assert.AreEqual(OptimizationAssessment.Good, u8.Metrics.Quality);
    }

    [TestMethod]
    public void WeightCompressionPreservesCacheIdentityAndAddsItsOwnQualityEffect()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "fp16-weights", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "int8-weights", OpenVinoWeightFormat.Int8,
                    cache: OpenVinoKvCacheFormat.F16)));

        OptimizationCandidate fp16 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "fp16-weights");
        OptimizationCandidate int8 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "int8-weights");

        Assert.AreEqual(
            OpenVinoKvCacheFormat.F16,
            ((OpenVinoRouteConfiguration)fp16.Configuration).KvCache);
        Assert.AreEqual(
            OpenVinoKvCacheFormat.F16,
            ((OpenVinoRouteConfiguration)int8.Configuration).KvCache);
        Assert.AreEqual(OptimizationAssessment.Excellent, fp16.Metrics.Quality);
        Assert.AreEqual(OptimizationAssessment.Good, int8.Metrics.Quality);
    }

    [TestMethod]
    public void TurboQuantTbq3IsExcludedByAnAcceptableQualityFloor()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-tbq3",
                    OpenVinoWeightFormat.Fp16,
                    SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3)),
            workload: CrossRouteTestData.Workload(
                floor: OptimizationAssessment.Acceptable),
            optedIn: "ov-tbq3");

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void Q2KIsExcludedAboveThePoorQualityFloor()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                CrossRouteTestData.Requantisation(),
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)),
            workload: CrossRouteTestData.Workload(OptimizationAssessment.Acceptable));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(GgufWeightFormat.Imported)]
    [DataRow(GgufWeightFormat.Q2K)]
    public void AlreadyQ2KSourceRetainsPoorQualityAndWarningAfterImportNormalization(
        GgufWeightFormat admittedWeights)
    {
        OptimizationCapabilitySnapshot snapshot = CrossRouteTestData.GgufSnapshot(
            CrossRouteTestData.Gguf("gguf-existing-q2", admittedWeights));

        CrossRouteGenerationResult excluded = Generate(
            snapshot,
            workload: CrossRouteTestData.Workload(OptimizationAssessment.Acceptable),
            facts: CrossRouteTestData.Facts(fileType: 10));

        Assert.AreEqual(0, excluded.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            excluded.Exclusions.Single().Reason);

        OptimizationCandidate admitted = Generate(
            snapshot,
            facts: CrossRouteTestData.Facts(fileType: 10)).Candidates.Single();

        Assert.AreEqual(OptimizationAssessment.Poor, admitted.Metrics.Quality);
        Assert.AreEqual(OptimizationCandidateNotice.LowQuality, admitted.Notice);
        Assert.AreEqual(
            GgufWeightFormat.Imported,
            ((GgufRouteConfiguration)admitted.Configuration).Weights);
        Assert.IsFalse(admitted.Metrics.RequiresPersistentChange);
    }

    [TestMethod]
    public void QuantisedSourceIsNotRequantisedByDefault()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.RequantisationNotAuthorized,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(WeightQuantisation.F32)]
    [DataRow(WeightQuantisation.BF16)]
    [DataRow(WeightQuantisation.F16)]
    public void GenuineHigherPrecisionSourcePermitsOrdinaryDownwardConversion(
        WeightQuantisation sourcePrecision)
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        GgufConversionSourceBinding source =
            CrossRouteTestData.Source(sourcePrecision);
        OptimizationCandidate candidate = Generate(
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", Digest,
                GgufCapabilityPayload.Create(
                    "b4321", [admitted], hasHigherPrecisionSource: true,
                    conversionSource: source,
                    admittedQuantiser: CrossRouteTestData.Quantiser(),
                    runtimeAuthority: CrossRouteTestData.RuntimeAuthority(
                        "b4321", Commit, admitted))))
            .Candidates.Single();

        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        Assert.AreEqual(OptimizationCandidateNotice.LowQuality, candidate.Notice);
    }

    [TestMethod]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    public void ControlledRequantisationRequiresEveryExplicitSafetyFact(
        bool acknowledged, bool preserveOriginal, bool requireNewOutput)
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                CrossRouteTestData.Requantisation(
                    acknowledged: acknowledged,
                    preserveOriginal: preserveOriginal,
                    requireNewOutput: requireNewOutput),
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.RequantisationNotAuthorized,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void ControlledRequantisationIsBoundToExactEvidenceAndCarriesWarning()
    {
        Assert.AreEqual(
            0,
            Generate(
                CrossRouteTestData.GgufSnapshotWithPolicy(
                    CrossRouteTestData.Requantisation("different-evidence"),
                    admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)))
                .Candidates.Count);

        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                CrossRouteTestData.Requantisation(),
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)))
            .Candidates.Single();

        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        Assert.IsTrue(candidate.Metrics.OutputDiskBytes > 0);
        Assert.AreEqual(
            OptimizationCandidateNotice.LowQualityRequantisation,
            candidate.Notice);
    }

    [TestMethod]
    [DataRow(7, WeightQuantisation.Q8_0, GgufWeightFormat.Q4KM,
        OptimizationCandidateNotice.Requantisation)]
    [DataRow(15, WeightQuantisation.Q4_K_M, GgufWeightFormat.Q3KM,
        OptimizationCandidateNotice.Requantisation)]
    [DataRow(15, WeightQuantisation.Q4_K_M, GgufWeightFormat.Q2K,
        OptimizationCandidateNotice.LowQualityRequantisation)]
    public void ControlledRequantisationDerivesWarningFromSourceAndTarget(
        int fileType,
        WeightQuantisation sourcePrecision,
        GgufWeightFormat target,
        OptimizationCandidateNotice expectedNotice)
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf("same-id", target);
        GgufConversionSourceBinding source = CrossRouteTestData.Source(sourcePrecision);
        GgufQuantiserIdentity quantiser = CrossRouteTestData.Quantiser();
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, admitted, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], false, policy, source, quantiser,
                runtimeAuthority: CrossRouteTestData.RuntimeAuthority(
                    "b4321", Commit, admitted)));

        OptimizationCandidate candidate = Generate(
            snapshot, facts: CrossRouteTestData.Facts(fileType)).Candidates.Single();

        Assert.AreEqual(expectedNotice, candidate.Notice);
        Assert.AreEqual(
            OptimizationConversionProvenance.ControlledRequantisation,
            candidate.ConversionProvenance);
    }

    [TestMethod]
    public void BooleanAloneNeverAuthorizesOrdinaryConversion()
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], hasHigherPrecisionSource: true));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void BoundSourceWithoutAnAdmittedQuantiserNeverAuthorizesConversion()
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], hasHigherPrecisionSource: true,
                conversionSource: CrossRouteTestData.Source(WeightQuantisation.F16)));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void BoundSourceMustBeStrictlyHigherPrecisionThanTheTarget()
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        GgufConversionSourceBinding source =
            CrossRouteTestData.Source(WeightQuantisation.Q2_K);
        GgufQuantiserIdentity quantiser = CrossRouteTestData.Quantiser();
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, admitted, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], false, policy, source, quantiser));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void PolicyForSameEvidenceButDifferentTargetDoesNotAuthorize()
    {
        GgufAdmittedConfiguration authorized =
            CrossRouteTestData.Gguf("same-id", GgufWeightFormat.Q3KM);
        GgufAdmittedConfiguration requested =
            CrossRouteTestData.Gguf("same-id", GgufWeightFormat.Q2K);
        GgufConversionSourceBinding source = CrossRouteTestData.Source();
        GgufQuantiserIdentity quantiser = CrossRouteTestData.Quantiser();
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, authorized, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [requested], false, policy, source, quantiser));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void RequantisationPolicyCarriesIdentitiesButNoPath()
    {
        GgufRequantisationPolicy policy = CrossRouteTestData.Requantisation();

        Assert.AreEqual(CrossRouteTestData.Quantiser(), policy.Quantiser);
        Assert.AreEqual(CrossRouteTestData.Binding(), policy.Source.Journey);
        Assert.IsFalse(typeof(GgufRequantisationPolicy).GetProperties().Any(property =>
            property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("File", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void AutomaticAvoidsTbq3WhileAFittingAcceptableOrBetterCacheExists()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-u8", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U8),
                CrossRouteTestData.OpenVino(
                    "ov-tbq3",
                    OpenVinoWeightFormat.Fp16,
                    SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3)),
            optedIn: "ov-tbq3");

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            result.Candidates,
            OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("No candidate was selected.");

        Assert.AreEqual("ov-u8", selection.Candidate.EvidenceId);
        Assert.IsTrue(
            selection.Candidate.Metrics.Quality >= OptimizationAssessment.Acceptable);
    }

    [TestMethod]
    public void OpenVinoFallbackSetContainsOnlyExactlyAdmittedEvidence()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "tbq3", OpenVinoWeightFormat.Fp16, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3),
                CrossRouteTestData.OpenVino(
                    "tbq4", OpenVinoWeightFormat.Fp16, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq4),
                CrossRouteTestData.OpenVino(
                    "u4", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U4),
                CrossRouteTestData.OpenVino(
                    "u8", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U8),
                CrossRouteTestData.OpenVino(
                    "f16", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "bf16", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.Bf16)),
            optedIn: "tbq4");

        CollectionAssert.AreEquivalent(
            new[] { "tbq4", "u4", "u8", "f16", "bf16" },
            result.Candidates.Select(candidate => candidate.EvidenceId).ToArray());
        Assert.AreEqual(
            OptimizationExclusionReason.ExperimentalNotAdmitted,
            result.Exclusions.Single(exclusion => exclusion.EvidenceId == "tbq3").Reason);
    }

    [TestMethod]
    public void AutomaticMayChooseConversionWhenCurrentRepresentationDoesNotFit()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "current", OpenVinoWeightFormat.Original,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "converted", OpenVinoWeightFormat.Int4,
                    cache: OpenVinoKvCacheFormat.U4)),
            budgetGibibytes: 2);

        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsSafeMemoryBudget,
            result.Exclusions.Single(exclusion => exclusion.EvidenceId == "current").Reason);

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            result.Candidates,
            OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("No fitting conversion was selected.");

        Assert.AreEqual("converted", selection.Candidate.EvidenceId);
        Assert.IsTrue(selection.Candidate.Metrics.RequiresPersistentChange);
    }

    [TestMethod]
    public void CandidateExceedingTheSafeBudgetIsExcludedWithAReason()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-fp16", OpenVinoWeightFormat.Fp16)),
            budgetGibibytes: 1);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsSafeMemoryBudget,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void CandidateWithoutDiskSpaceIsExcludedWithAReason()
    {
        // A conversion that cannot be written is not a cheaper conversion.
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8)),
            diskGibibytes: 0);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.InsufficientDiskSpace,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void RuntimeOnlyCandidateAlsoFailsClosedWhenDiskObservationIsZero()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-original", OpenVinoWeightFormat.Original)),
            diskGibibytes: 0);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.InsufficientDiskSpace,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void GeneratedCandidateCarriesTheExactDiskObservationUsedForAdmission()
    {
        const ulong availableGibibytes = 123;
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int8", OpenVinoWeightFormat.Int8)),
            diskGibibytes: availableGibibytes).Candidates.Single();

        Assert.AreEqual(
            availableGibibytes * Gibibyte,
            candidate.Metrics.AvailableDiskBytes);
        Assert.IsTrue(candidate.Metrics.FitsDiskSafely);
    }

    [TestMethod]
    public void UnestimableCandidateBecomesATypedExclusionNotAZero()
    {
        // The whole point. A zero peak would be the cheapest option on the
        // board and would win every efficiency band, so an estimate that could
        // not be established must leave nothing behind to rank.
        CrossRouteGenerationResult result = CrossRouteCandidateGenerator.Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8)),
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), null, null, null, null, 8192, 15, 2),
            CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority());

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EstimateNotEstablished,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void CandidateBelowTheQualityFloorIsExcluded()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int4", OpenVinoWeightFormat.Int4)),
            CrossRouteTestData.Workload(floor: OptimizationAssessment.Excellent));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void EntryOutsideItsOwnContextBoundsGeneratesNothing()
    {
        OpenVinoAdmittedConfiguration narrow = OpenVinoAdmittedConfiguration.Create(
            "ov-narrow", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled, 1,
            minimumContextTokens: 8192, maximumContextTokens: 16384,
            SupportLevel.DeclaredSupported, false);

        Assert.AreEqual(
            0,
            Generate(CrossRouteTestData.OpenVinoSnapshot(narrow)).Candidates.Count);
    }

    [TestMethod]
    public void IdenticalConfigurationsFromTwoEntriesAreOfferedOnce()
    {
        // The same setup reached through two admitted records is one choice,
        // not two competing for the same band.
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-a", OpenVinoWeightFormat.Int8),
                CrossRouteTestData.OpenVino("ov-b", OpenVinoWeightFormat.Int8)));

        Assert.AreEqual(1, result.Candidates.Count);
    }

    [TestMethod]
    public void IdenticalConfigurationsChooseReleasedEvidenceRegardlessOfEntryOrder()
    {
        OpenVinoAdmittedConfiguration released = CrossRouteTestData.OpenVino(
            "released", OpenVinoWeightFormat.Int8);
        OpenVinoAdmittedConfiguration experimental = CrossRouteTestData.OpenVino(
            "experimental", OpenVinoWeightFormat.Int8, SupportLevel.Experimental);

        OptimizationCandidate Forward() => Generate(
            CrossRouteTestData.OpenVinoSnapshot(experimental, released),
            optedIn: "experimental").Candidates.Single();

        OptimizationCandidate Reversed() => Generate(
            CrossRouteTestData.OpenVinoSnapshot(released, experimental),
            optedIn: "experimental").Candidates.Single();

        Assert.AreEqual("released", Forward().EvidenceId);
        Assert.AreEqual(Forward().EvidenceId, Reversed().EvidenceId);
        Assert.IsFalse(Forward().IsExperimental);
    }

    [TestMethod]
    public void EveryAdmittedCandidateCarriesCompleteMetrics()
    {
        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8),
                CrossRouteTestData.OpenVino("ov-int4", OpenVinoWeightFormat.Int4))).Candidates)
        {
            OptimizationCandidateMetrics metrics = candidate.Metrics;

            Assert.AreNotEqual(EvidenceGrade.Unknown, metrics.Evidence);
            Assert.AreNotEqual(OptimizationAssessment.Unknown, metrics.Quality);
            Assert.AreNotEqual(OptimizationAssessment.Unknown, metrics.Performance);
            Assert.AreNotEqual(OptimizationAssessment.Unknown, metrics.Stability);
            Assert.IsTrue(metrics.PredictedPeakBytes > 0);
            Assert.IsTrue(metrics.FitsSafely);
        }
    }

    [TestMethod]
    public void NothingIsGradedMeasuredBeforeAnythingHasRun()
    {
        // Every figure comes from documented defaults. Grading one Measured
        // would let it outrank a real measurement later.
        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8))).Candidates)
        {
            Assert.AreEqual(EvidenceGrade.Estimated, candidate.Metrics.Evidence);
        }
    }

    [TestMethod]
    public void RuntimeOnlyCandidateClaimsNoPersistentChange()
    {
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-original", OpenVinoWeightFormat.Original))).Candidates.Single();

        Assert.IsFalse(candidate.Metrics.RequiresPersistentChange);
        Assert.AreEqual(0UL, candidate.Metrics.OutputDiskBytes);
    }

    [TestMethod]
    public void ConvertingCandidateDeclaresItsPersistentChange()
    {
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int4", OpenVinoWeightFormat.Int4))).Candidates.Single();

        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        Assert.IsTrue(candidate.Metrics.OutputDiskBytes > 0);
    }

    [TestMethod]
    public void GenerationIsDeterministic()
    {
        // Two identical runs must order identically, or a plan issued twice
        // from the same inputs would bind two different configurations.
        string[] First() =>
            [.. Generate(CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8),
                CrossRouteTestData.OpenVino("ov-int4", OpenVinoWeightFormat.Int4),
                CrossRouteTestData.OpenVino("ov-fp16", OpenVinoWeightFormat.Fp16)))
                .Candidates.Select(candidate => candidate.CanonicalDescriptor)];

        CollectionAssert.AreEqual(First(), First());
    }
}
