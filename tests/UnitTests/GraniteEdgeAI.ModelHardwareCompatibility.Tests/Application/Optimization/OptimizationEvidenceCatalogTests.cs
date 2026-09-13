using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

[TestClass]
public sealed class OptimizationEvidenceCatalogTests
{
    [TestMethod]
    public void ReleasedDefaultCacheCannotBorrowExplicitF16ActivationEvidence()
    {
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            OpenVinoWeightFormat.Original, OpenVinoKvCacheFormat.RouteDefault, DeviceRouteId.Cpu,
            OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Disabled, 1);
        OptimizationEvidenceRecord explicitF16 = AdmittedEvidence(ExactKey() with
        {
            Route = OptimizationRoute.OpenVino, SourceWeightRepresentation = "fp16",
            TargetWeightRepresentation = "fp16", CacheConfiguration = "f16",
            Backend = OptimizationEvidenceBackend.OpenVinoCpu,
            DeviceClass = OptimizationEvidenceDeviceClass.Cpu, ExecutionProfile = "openvino-cpu"
        });
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationCandidate.Create(
            configuration, SafeMetrics(), explicitF16, false));
        OptimizationEvidenceRecord releasedDefault = explicitF16 with
        {
            Key = explicitF16.Key with { CacheConfiguration = "released-default" }
        };
        Assert.AreSame(releasedDefault, OptimizationCandidate.Create(
            configuration, SafeMetrics(), releasedDefault, false).Evidence);
    }
    [TestMethod]
    public void ResolveRequiresEveryConfigurationAndMethodologyDimensionToMatch()
    {
        OptimizationEvidenceKey key = ExactKey();
        OptimizationEvidenceRecord evidence = AdmittedEvidence(key);
        OptimizationEvidenceCatalog catalog = new([evidence]);

        Assert.IsTrue(catalog.TryResolve(key, out OptimizationEvidenceRecord? resolved));
        Assert.AreSame(evidence, resolved);
        Assert.IsFalse(catalog.TryResolve(
            key with { DeviceClass = OptimizationEvidenceDeviceClass.Cpu }, out _));
        Assert.IsFalse(catalog.TryResolve(key with { MethodologyIdentity = "different-method" }, out _));
        Assert.IsFalse(catalog.TryResolveArtifactConfiguration(
            key.ModelFamily,
            key.ModelIdentitySha256,
            key.ParameterCount + 1,
            key.Route,
            key.RuntimePackageIdentity,
            key.SourceWeightRepresentation,
            key.TargetWeightRepresentation,
            key.CacheConfiguration,
            key.Backend,
            key.DeviceClass,
            key.ContextTokens,
            key.Workload,
            key.MethodologyIdentity,
            key.MemoryPerformanceProtocol,
            key.ExecutionProfile,
            out _));
    }

    [TestMethod]
    public void ResolveDistinguishesPartialFromFullVulkanOffload()
    {
        OptimizationEvidenceKey partial = ExactKey() with
        {
            ExecutionProfile = "vulkan-partial"
        };
        OptimizationEvidenceKey full = partial with
        {
            ExecutionProfile = "vulkan-full"
        };
        OptimizationEvidenceCatalog catalog = new([AdmittedEvidence(partial)]);

        Assert.IsTrue(catalog.TryResolve(partial, out _));
        Assert.IsFalse(catalog.TryResolve(full, out _));
    }

    [TestMethod]
    public void CatalogRequiresAnOpaqueModelIdentityDigest()
    {
        OptimizationEvidenceRecord unsafeEvidence = AdmittedEvidence(
            ExactKey() with { ModelIdentitySha256 = "ibm-granite-4.1-3b" });

        Assert.ThrowsExactly<ArgumentException>(
            () => new OptimizationEvidenceCatalog([unsafeEvidence]));
    }

    [TestMethod]
    public void CatalogRejectsPathShapedEvidenceDimensions()
    {
        OptimizationEvidenceRecord unsafeEvidence = AdmittedEvidence(
            ExactKey() with { RuntimePackageIdentity = @"C:\workers\runtime" });

        Assert.ThrowsExactly<ArgumentException>(
            () => new OptimizationEvidenceCatalog([unsafeEvidence]));
    }

    [TestMethod]
    public void QualityBelowFourIsNotAdmitted()
    {
        OptimizationEvidenceRecord evidence = AdmittedEvidence(ExactKey()) with
        {
            Quality = new OptimizationQualityScore(3.99m)
        };

        Assert.IsFalse(evidence.IsAdmitted);
    }

    [TestMethod]
    public void AnyFailedHardGateRejectsOtherwiseHighQualityEvidence()
    {
        OptimizationEvidenceRecord healthy = AdmittedEvidence(ExactKey());

        Assert.IsFalse((healthy with { OutputHealthPassed = false }).IsAdmitted);
        Assert.IsFalse((healthy with { StabilityPassed = false }).IsAdmitted);
        Assert.IsFalse((healthy with { ActivationPassed = false }).IsAdmitted);
        Assert.IsFalse((healthy with { IntegrityPassed = false }).IsAdmitted);
    }

    [TestMethod]
    [DataRow("-0.01")]
    [DataRow("10.01")]
    public void QualityScoreRejectsValuesOutsideTheClosedZeroToTenRange(string value)
    {
        decimal parsed = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new OptimizationQualityScore(parsed));
    }

    [TestMethod]
    [DataRow("4.00", OptimizationQualityLevel.Acceptable)]
    [DataRow("5.00", OptimizationQualityLevel.Fair)]
    [DataRow("6.00", OptimizationQualityLevel.Good)]
    [DataRow("7.00", OptimizationQualityLevel.VeryGood)]
    [DataRow("8.00", OptimizationQualityLevel.Excellent)]
    [DataRow("10.00", OptimizationQualityLevel.Excellent)]
    public void AdmittedScoreUsesTheFivePublishedQualityBands(
        string value,
        OptimizationQualityLevel expected)
    {
        decimal parsed = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, new OptimizationQualityScore(parsed).Level);
    }

    [TestMethod]
    public void CandidateCarriesTheExactAdmittedEvidenceRecord()
    {
        OptimizationEvidenceRecord evidence = CpuEvidence();

        OptimizationCandidate candidate = OptimizationCandidate.Create(
            CpuConfiguration(), SafeMetrics(), evidence, isExperimental: false);

        Assert.AreSame(evidence, candidate.Evidence);
        Assert.AreEqual(evidence.EvidenceId, candidate.EvidenceId);
        Assert.AreEqual(7.25m, candidate.QualityScore);
        Assert.AreEqual(OptimizationQualityLevel.VeryGood, candidate.QualityLevel);
    }

    [TestMethod]
    public void CandidateRejectsAdmittedEvidenceForAnyMateriallyDifferentConfiguration()
    {
        OptimizationEvidenceRecord matching = CpuEvidence();
        OptimizationEvidenceRecord[] mismatches =
        [
            matching with { Key = matching.Key with { Route = OptimizationRoute.OpenVino } },
            matching with { Key = matching.Key with { SourceWeightRepresentation = "q8_0" } },
            matching with { Key = matching.Key with { TargetWeightRepresentation = "q8_0" } },
            matching with { Key = matching.Key with
                { SourceWeightRepresentation = "q8_0", TargetWeightRepresentation = "q8_0" } },
            matching with { Key = matching.Key with { CacheConfiguration = "q8_0" } },
            matching with { Key = matching.Key with { Backend = OptimizationEvidenceBackend.Vulkan } },
            matching with { Key = matching.Key with
                { DeviceClass = OptimizationEvidenceDeviceClass.IntelIntegratedGpu } },
            matching with { Key = matching.Key with { ContextTokens = 8192 } }
        ];

        foreach (OptimizationEvidenceRecord mismatch in mismatches)
        {
            Assert.ThrowsExactly<ArgumentException>(() => OptimizationCandidate.Create(
                CpuConfiguration(), SafeMetrics(), mismatch, isExperimental: false));
        }
    }

    [TestMethod]
    public void CandidateRejectsEvidenceThatFailedAHealthGate()
    {
        OptimizationEvidenceRecord rejected = CpuEvidence() with
        {
            ActivationPassed = false
        };

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationCandidate.Create(
            CpuConfiguration(), SafeMetrics(), rejected, isExperimental: false));
    }

    [TestMethod]
    public void CandidateRejectsGgufPartialOffloadEvidenceForFullOffload()
    {
        OptimizationEvidenceRecord partial = AdmittedEvidence(ExactKey() with
        {
            Route = OptimizationRoute.Gguf,
            SourceWeightRepresentation = "q4_k_m",
            TargetWeightRepresentation = "q4_k_m",
            CacheConfiguration = "f16",
            Backend = OptimizationEvidenceBackend.Vulkan,
            DeviceClass = OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
            ExecutionProfile = "vulkan-partial"
        });

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full),
            SafeMetrics(), partial, isExperimental: false));
    }

    [TestMethod]
    public void CandidateRejectsOpenVinoEvidenceWithTheWrongExecutionProfile()
    {
        OptimizationEvidenceRecord evidence = AdmittedEvidence(CpuEvidence().Key with
        {
            Route = OptimizationRoute.OpenVino,
            SourceWeightRepresentation = "int4",
            TargetWeightRepresentation = "int4",
            CacheConfiguration = "u8",
            Backend = OptimizationEvidenceBackend.OpenVinoCpu,
            ExecutionProfile = "openvino-gpu"
        });

        Assert.ThrowsExactly<ArgumentException>(() => OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, 1),
            SafeMetrics(), evidence, isExperimental: false));
    }

    [TestMethod]
    public void SafeFrontierOrdersFiveEvidenceBandsByExactScoreNotCoarseEnum()
    {
        decimal[] scores = [4.2m, 5.2m, 6.2m, 7.2m, 8.2m];
        OptimizationCandidate[] candidates =
        [
            .. scores.Select((score, index) =>
            {
                int context = (index + 1) * 1024;
                OptimizationEvidenceRecord evidence = CpuEvidence(context) with
                {
                    EvidenceId = $"quality-band-{index}",
                    Quality = new OptimizationQualityScore(score)
                };
                return OptimizationCandidate.Create(
                    CpuConfiguration(),
                    SafeMetrics(context, (ulong)(index + 1)),
                    evidence,
                    isExperimental: false);
            })
        ];

        IReadOnlyList<OptimizationCandidate> frontier =
            SafeCandidateFrontier.Create(candidates);

        CollectionAssert.AreEqual(scores, frontier.Select(item => item.QualityScore).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                OptimizationQualityLevel.Acceptable,
                OptimizationQualityLevel.Fair,
                OptimizationQualityLevel.Good,
                OptimizationQualityLevel.VeryGood,
                OptimizationQualityLevel.Excellent
            },
            frontier.Select(item => item.QualityLevel).ToArray());
    }

    [TestMethod]
    public void PublishedOpenVinoCatalogContainsOnlyTheFifteenExecutedExactTuples()
    {
        IReadOnlyList<OptimizationEvidenceRecord> records =
            PublishedOpenVinoOptimizationEvidence.Records();

        Assert.HasCount(15, records);
        Assert.IsTrue(records.All(static record => record.IsAdmitted));
        Assert.IsTrue(records.All(static record =>
            record.Key.ContextTokens == 4096 &&
            record.Key.Backend == OptimizationEvidenceBackend.OpenVinoCpu));
        Assert.IsFalse(records.Any(static record =>
            record.Key.SourceWeightRepresentation == "fp16"),
            "The failed/blocked FP16 rows must not become positive evidence.");
    }

    [TestMethod]
    public void PublishedOpenVinoCatalogResolvesTheExactTbq3ArtifactAndNothingNearby()
    {
        OptimizationEvidenceRecord expected =
            PublishedOpenVinoOptimizationEvidence.Records().Single(record =>
                record.Key.ModelIdentitySha256.StartsWith("6da1029a",
                    StringComparison.Ordinal) &&
                record.Key.CacheConfiguration == "tbq3");
        OptimizationEvidenceCatalog catalog =
            PublishedOpenVinoOptimizationEvidence.CreateCatalog();

        Assert.IsTrue(catalog.TryResolve(expected.Key, out var resolved));
        Assert.AreEqual(5.42m, resolved!.Quality.Value);
        Assert.AreEqual(OptimizationQualityLevel.Fair, resolved.Quality.Level);
        Assert.IsFalse(catalog.TryResolve(
            expected.Key with { ContextTokens = 8192 }, out _));
    }

    [TestMethod]
    public void PublishedGgufCatalogQuarantinesEveryCriticalPromptFailureDespiteAboveFloorMeans()
    {
        IReadOnlyList<OptimizationEvidenceRecord> records =
            PublishedGgufOptimizationEvidence.Records();

        Assert.HasCount(12, records);
        Assert.AreEqual(0, records.Count(static record => record.IsAdmitted));
        CollectionAssert.AreEquivalent(new[] { "AB-KV3-F16-4K", "AB-04", "AB-05", "AB-06", "AB-08Q", "AB-09", "AB-10", "AB-11", "AB-12", "AB-13", "AB-14", "AB-15" },
            records.Where(record => !record.IsAdmitted).Select(record => record.EvidenceId).ToArray());
        Assert.IsTrue(records.All(static record =>
            record.Key.RuntimePackageIdentity ==
                PublishedGgufOptimizationEvidence.RuntimePackageIdentity));
        Assert.IsFalse(records.Any(static record =>
            record.Key.CacheConfiguration == "turbo2"),
            "AB-07 scored 0.70/10 and must remain excluded.");
        Assert.IsFalse(records.Any(static record =>
            record.EvidenceId == "AB-15M"),
            "The unexecuted full-offload 8B row must not become positive evidence.");
    }

    [TestMethod]
    public void PublishedGgufCatalogKeepsPartialAndFullVulkanEvidenceSeparate()
    {
        IReadOnlyList<OptimizationEvidenceRecord> granite3bTurbo3 =
        [
            .. PublishedGgufOptimizationEvidence.Records().Where(static record =>
                record.Key.ModelIdentitySha256.StartsWith("662b0626", StringComparison.Ordinal)
                && record.Key.CacheConfiguration == "turbo3"
                && record.Key.Backend == OptimizationEvidenceBackend.Vulkan)
        ];

        Assert.HasCount(2, granite3bTurbo3);
        CollectionAssert.AreEquivalent(
            new[] { "vulkan-partial", "vulkan-full" },
            granite3bTurbo3.Select(static record => record.Key.ExecutionProfile).ToArray());
        Assert.AreNotEqual(
            granite3bTurbo3[0].Quality.Value,
            granite3bTurbo3[1].Quality.Value);
    }

    [TestMethod]
    public void PublishedGgufCatalogBindsTheFullPinnedSourceCommit()
    {
        string pinnedCommit = string.Concat(
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit);
        Assert.IsTrue(string.Equals(
            $"atomicbot-turboquant-{pinnedCommit}-win-x64",
            PublishedGgufOptimizationEvidence.RuntimePackageIdentity,
            StringComparison.Ordinal));
        Assert.IsTrue(string.Equals(
            $"atomicbot-{pinnedCommit}-cpu-vulkan",
            PublishedGgufOptimizationEvidence.RuntimeBuildId,
            StringComparison.Ordinal));

        OptimizationEvidenceCatalog catalog =
            PublishedGgufOptimizationEvidence.CreateCatalog();
        OptimizationEvidenceRecord evidence =
            PublishedGgufOptimizationEvidence.Records().First();

        Assert.IsFalse(catalog.TryResolve(evidence.Key with
        {
            RuntimePackageIdentity = "atomicbot-turboquant-519f0c5-win-x64"
        }, out _));
    }

    private static OptimizationEvidenceKey ExactKey() => new(
        ModelFamily: OptimizationEvidenceModelFamily.Granite,
        ModelIdentitySha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        ParameterCount: 3_000_000_000,
        Route: OptimizationRoute.Gguf,
        RuntimePackageIdentity: "atomicbot@519f0c",
        SourceWeightRepresentation: "f16",
        TargetWeightRepresentation: "tq4_1s",
        CacheConfiguration: "turbo4",
        Backend: OptimizationEvidenceBackend.Vulkan,
        DeviceClass: OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
        ContextTokens: 4096,
        Workload: "local-chat-v1",
        MethodologyIdentity: "granite-quality-v1",
        MemoryPerformanceProtocol: "windows-memory-v1",
        ExecutionProfile: "vulkan-partial");

    private static OptimizationEvidenceRecord AdmittedEvidence(
        OptimizationEvidenceKey key) => new(
            EvidenceId: "granite-3b-atomicbot-vulkan-tq4-turbo4-v1",
            Key: key,
            Quality: new OptimizationQualityScore(7.25m),
            OutputHealthPassed: true,
            StabilityPassed: true,
            ActivationPassed: true,
            IntegrityPassed: true);

    private static OptimizationEvidenceRecord CpuEvidence(
        int contextTokens = 4096) => AdmittedEvidence(ExactKey() with
    {
        Route = OptimizationRoute.Gguf,
        SourceWeightRepresentation = "q4_k_m",
        TargetWeightRepresentation = "q4_k_m",
        CacheConfiguration = "f16",
        Backend = OptimizationEvidenceBackend.Cpu,
        DeviceClass = OptimizationEvidenceDeviceClass.Cpu,
        ContextTokens = contextTokens,
        ExecutionProfile = "cpu"
    });

    private static GgufRouteConfiguration CpuConfiguration() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static OptimizationCandidateMetrics SafeMetrics(
        int contextTokens = 4096,
        ulong predictedPeakBytes = 4) =>
        OptimizationCandidateMetrics.Create(
            EvidenceGrade.Measured,
            OptimizationAssessment.Good,
            OptimizationAssessment.Good,
            OptimizationAssessment.Good,
            contextTokens: contextTokens,
            predictedPeakBytes: predictedPeakBytes,
            safeBudgetBytes: 8,
            headroomBytes: 8 - predictedPeakBytes,
            workingDiskBytes: 0,
            outputDiskBytes: 0,
            requiresPersistentChange: false,
            availableDiskBytes: 8);
}
