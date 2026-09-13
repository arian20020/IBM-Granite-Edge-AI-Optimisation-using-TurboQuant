using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

[TestClass]
public sealed class VerifiedOpenVinoOptimizationEvidenceTests
{
    [TestMethod]
    public void CurrentRawInt4U4CompatibilityPreservesHistoricalScore()
    {
        var historical = Authority("OV-STD-CPU-INT4-U4-01", OpenVinoKvCacheFormat.U4);
        OpenVinoExecutionAuthority Current(OpenVinoExecutionAuthority template) =>
            OpenVinoExecutionAuthority.Create(template.EvidenceId, template.ConfigurationId,
                template.SourceWeightPrecision,
                OpenVinoBuildIdentity.Create(template.BuildIdentity.RuntimeBuild,
                    template.BuildIdentity.GenAiBuild, template.BuildIdentity.TokenizersBuild,
                    VerifiedOpenVinoOptimizationEvidence.CurrentOfficialWorkerManifestSha256),
                template.OptimizerVersions, template.CompiledCacheIsDisposable, template.TurboQuantBuild);
        var original = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [historical]);
        var bridged = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [Current(historical)]);
        Assert.HasCount(1, original);
        Assert.HasCount(1, bridged);
        Assert.AreEqual(original[0].Quality.Value, bridged[0].Quality.Value);
        Assert.AreEqual(5.52m, bridged[0].Quality.Value);
        Assert.AreEqual(original[0].Key.ContextTokens, bridged[0].Key.ContextTokens);
        Assert.IsTrue(bridged[0].IsAdmitted);
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(new string('a', 64),
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [Current(historical)]));
    }

    [TestMethod]
    [DataRow("OV-STD-CPU-AUTO-01", "int8", "default", false, 4d)]
    [DataRow("OV-STD-CPU-INT8-U8-01", "int8", "u8", false, 4d)]
    [DataRow("OV-STD-CPU-INT8-U4-01", "int8", "u4", false, 4d)]
    [DataRow("OV-STD-CPU-INT4-U8-01", "int4", "u8", false, 4d)]
    [DataRow("OV-STD-CPU-INT4-U4-01", "int4", "u4", false, 5.52d)]
    [DataRow("OV-TBQ3-CPU-INT8-01", "int8", "tbq3", true, 4d)]
    [DataRow("OV-TBQ4-CPU-INT8-01", "int8", "tbq4", true, 4d)]
    [DataRow("OV-TBQ3-CPU-INT4-01", "int4", "tbq3", true, 5.19d)]
    [DataRow("OV-TBQ4-CPU-INT4-01", "int4", "tbq4", true, 5.48d)]
    [DataRow("OV-TBQ3-CPU-MXFP4-01", "mxfp4", "tbq3", true, 4d)]
    [DataRow("OV-TBQ4-CPU-MXFP4-01", "mxfp4", "tbq4", true, 4d)]
    public void CurrentRawCompatibilityRetainsExactHistoricalRows(
        string id, string weights, string cache, bool turbo, double expectedQuality)
    {
        var old = turbo ? TurboAuthority(id, cache, weights)
            : Authority(id, cache == "u8" ? OpenVinoKvCacheFormat.U8 :
                cache == "u4" ? OpenVinoKvCacheFormat.U4 : OpenVinoKvCacheFormat.RouteDefault, weights);
        var current = OpenVinoExecutionAuthority.Create(old.EvidenceId, old.ConfigurationId,
            old.SourceWeightPrecision, OpenVinoBuildIdentity.Create(old.BuildIdentity.RuntimeBuild,
                old.BuildIdentity.GenAiBuild, old.BuildIdentity.TokenizersBuild,
                turbo ? "39a4eccc05d4677b59f7f882cc50f875591ee3d3f90f1037e61e17fe3a34c35e"
                    : VerifiedOpenVinoOptimizationEvidence.CurrentOfficialWorkerManifestSha256),
            old.OptimizerVersions, true, old.TurboQuantBuild);
        var records = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [current]);
        Assert.HasCount(1, records);
        Assert.AreEqual((decimal)expectedQuality, records[0].Quality.Value);
        Assert.AreEqual(4096, records[0].Key.ContextTokens);
        Assert.IsTrue(records[0].IsAdmitted);
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(new string('a', 64),
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [current]));
        var wrongVersions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in current.OptimizerVersions) wrongVersions.Add(pair.Key, pair.Value);
        wrongVersions["nncf"] = "0.0.0";
        var changed = OpenVinoExecutionAuthority.Create(current.EvidenceId, current.ConfigurationId,
            current.SourceWeightPrecision, current.BuildIdentity, wrongVersions, true, current.TurboQuantBuild);
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [changed]));
    }

    [TestMethod]
    public void RetainedInt4ResolvesProvenParameterCountWithoutAdmittingOtherModels()
    {
        Assert.IsTrue(VerifiedOpenVinoOptimizationEvidence.TryResolveParameterCount(
            "fcfb6ec62a2b823d7d1aebedee193083eaa2f86d9e89722e46102c7a4b90bd27", out ulong count));
        Assert.AreEqual(3_402_836_480UL, count);
        Assert.IsFalse(VerifiedOpenVinoOptimizationEvidence.TryResolveParameterCount(new string('b', 64), out _));
        Assert.IsTrue(VerifiedOpenVinoOptimizationEvidence.TryResolveParameterCount(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256, out ulong historicalCount));
        Assert.AreEqual(3_000_000_000UL, historicalCount);
    }

    [TestMethod]
    public void CurrentRetainedU4RequiresExactPackageModelCountClosureAndConfiguration()
    {
        const string package = "d5f33732fddb37c150f8ebd4be163c40474a245cd3a8f7683b63996d5f60ed68";
        const string worker = "f0089dae967a0b4249238f9bf49db02ba44e111c78b83ebff36778f6d0ddcbe2";
        var template = Authority("OV-STD-CPU-INT4-U4-01", OpenVinoKvCacheFormat.U4);
        OpenVinoExecutionAuthority Create(string id, string configuration, string digest) =>
            OpenVinoExecutionAuthority.Create(id, configuration, OpenVinoWeightPrecision.FourBit,
                OpenVinoBuildIdentity.Create(template.BuildIdentity.RuntimeBuild, template.BuildIdentity.GenAiBuild,
                    template.BuildIdentity.TokenizersBuild, digest), template.OptimizerVersions, true);
        var exact = Create(template.EvidenceId, template.ConfigurationId, worker);
        var records = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.OptimizedModelSha256, 3_402_836_480, [exact], package);
        Assert.HasCount(1, records);
        Assert.AreEqual("u4", records[0].Key.CacheConfiguration);
        Assert.AreEqual(5.520833333333333m, records[0].Quality.Value);
        Assert.IsTrue(records[0].IsAdmitted);
        foreach (string? changedPackage in new[] { null, new string('a', 64), VerifiedOpenVinoOptimizationEvidence.OptimizedPackageManifestSha256 })
            Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
                VerifiedOpenVinoOptimizationEvidence.OptimizedModelSha256, 3_402_836_480, [exact], changedPackage));
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(new string('a', 64), 3_402_836_480, [exact], package));
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.OptimizedModelSha256, 3_000_000_000, [exact], package));
        foreach (var refused in new[] {
            Create(exact.EvidenceId, exact.ConfigurationId, new string('a', 64)),
            Create("OV-STD-CPU-INT4-DEFAULT-01", "openvino.standard.cpu.int4.default.v1", worker),
            Create("OV-STD-CPU-INT4-U8-01", "openvino.standard.cpu.int4.u8.v1", worker),
            template })
            Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
                VerifiedOpenVinoOptimizationEvidence.OptimizedModelSha256, 3_402_836_480, [refused], package));
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256, 3_402_836_480, [exact], package));
    }

    [TestMethod]
    [DataRow(false, "e6171b0e77b3b71356109e7529794b9a8a034d6b4f8576d3ad15c7ed4cd579bf", true)]
    [DataRow(true, "2c8b77eba5f26d693fe3cbde164c8fe88f5d6d2496a6f79ea666eae0dd4271d5", true)]
    [DataRow(false, "0c642015d9b6912533d8c6d5e8f136e97f1071aa71ff62b6dbf46f818771a6f9", true)]
    [DataRow(false, "d25b9bdc97bc7e07301c6439e0e3482376494027fd098d89db541186b31723d6", false)]
    [DataRow(true, "7455fc0fcd20a2dcb38ef9636b26323e988083819ee810fba110b6171c7e6e88", true)]
    [DataRow(false, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [DataRow(true, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void CurrentWorkerClosureAdmitsEvidenceAndRejectsStaleWorkers(bool turbo, string digest, bool admitted)
    {
        var template = turbo
            ? TurboAuthority("OV-TBQ4-CPU-INT4-01", "tbq4")
            : Authority("OV-STD-CPU-INT4-U8-01", OpenVinoKvCacheFormat.U8);
        var execution = OpenVinoExecutionAuthority.Create(template.EvidenceId,
            template.ConfigurationId, template.SourceWeightPrecision,
            OpenVinoBuildIdentity.Create(template.BuildIdentity.RuntimeBuild,
                template.BuildIdentity.GenAiBuild, template.BuildIdentity.TokenizersBuild, digest),
            template.OptimizerVersions, true, template.TurboQuantBuild);
        var records = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [execution]);
        Assert.AreEqual(admitted ? 1 : 0, records.Count);
    }

    [TestMethod]
    [DataRow("OV-TBQ3-CPU-INT4-01", "tbq3", OpenVinoKvCacheFormat.TurboQuantTbq3, false)]
    [DataRow("OV-TBQ4-CPU-INT4-01", "tbq4", OpenVinoKvCacheFormat.TurboQuantTbq4, false)]
    [DataRow("OV-TBQ3-CPU-INT4-01", "tbq3", OpenVinoKvCacheFormat.TurboQuantTbq3, true)]
    [DataRow("OV-TBQ4-CPU-INT4-01", "tbq4", OpenVinoKvCacheFormat.TurboQuantTbq4, true)]
    public void ReleasedTurboGenerationRequiresCurrentSectorEvidence(
        string evidenceId, string codec, OpenVinoKvCacheFormat cache, bool currentClosure)
    {
        const ulong gib = 1024UL * 1024 * 1024;
        var execution = TurboAuthority(evidenceId, codec);
        if (currentClosure)
            execution = OpenVinoExecutionAuthority.Create(execution.EvidenceId, execution.ConfigurationId,
                execution.SourceWeightPrecision, OpenVinoBuildIdentity.Create(execution.BuildIdentity.RuntimeBuild,
                    execution.BuildIdentity.GenAiBuild, execution.BuildIdentity.TokenizersBuild,
                    "39a4eccc05d4677b59f7f882cc50f875591ee3d3f90f1037e61e17fe3a34c35e"),
                execution.OptimizerVersions, true, execution.TurboQuantBuild);
        var admission = OpenVinoAdmittedConfiguration.Create(evidenceId, DeviceRouteId.Cpu,
            OpenVinoWeightFormat.Int4, cache, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled, 1, 4096, 4096,
            SupportLevel.DeclaredSupported, false);
        var snapshot = OptimizationCapabilitySnapshot.ForOpenVino("release-test", new string('a', 64),
            OpenVinoCapabilityPayload.Create(execution.BuildIdentity.RuntimeBuild, [admission], [execution]));
        var record = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [execution]).Single();
        CrossRouteGenerationResult Generate(OptimizationEvidenceRecord row) => CrossRouteCandidateGenerator.Generate(
            snapshot, InspectedModelFacts.Create(ByteCount.FromBytes(6 * gib), 32, 4096, 32, 8, 4096, null, null),
            OptimizationWorkload.Create("local-chat", 4096, OptimizationAssessment.Acceptable, [ContextTokenCount.FromTokens(4096)]),
            OptimizationJourneyBinding.Create("mi-run", "mi-handoff", VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
                6 * gib, "hw-run", new string('a', 64)),
            ByteCount.FromBytes(32 * gib), ByteCount.FromBytes(500 * gib), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(), OptimizationHardwareAuthorityTestData.AllEstablished(),
            new OptimizationEvidenceCatalog([row]), VerifiedOpenVinoOptimizationEvidence.ParameterCount);
        var generated = Generate(record);
        Assert.HasCount(1, generated.Candidates);
        Assert.IsFalse(generated.Candidates[0].IsExperimental);
        Assert.AreEqual(record, generated.Candidates[0].Evidence);
        Assert.IsEmpty(Generate(record with { Key = record.Key with
        {
            MethodologyIdentity = VerifiedOpenVinoOptimizationEvidence.TurboMethodologyIdentity,
            MemoryPerformanceProtocol = VerifiedOpenVinoOptimizationEvidence.TurboMemoryPerformanceProtocol
        } }).Candidates);
        Assert.IsEmpty(Generate(record with { OutputHealthPassed = false }).Candidates);
    }

    [TestMethod]
    public void CurrentInt4Tbq3UsesCompleteHealthySectorEvidence()
    {
        var execution = TurboAuthority("OV-TBQ3-CPU-INT4-01", "tbq3");
        var records = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [execution]);
        Assert.HasCount(1, records);
        Assert.AreEqual(5.19m, records[0].Quality.Value);
        Assert.AreEqual(VerifiedOpenVinoOptimizationEvidence.SectorQualityMethodologyIdentity,
            records[0].Key.MethodologyIdentity);
        Assert.AreEqual(VerifiedOpenVinoOptimizationEvidence.SectorQualityMemoryPerformanceProtocol,
            records[0].Key.MemoryPerformanceProtocol);
        Assert.IsTrue(records[0].IsAdmitted);
    }

    [TestMethod]
    public void ReleasedTurboQuantRejectsLegacyOrMismatchedEvidence()
    {
        var execution = TurboAuthority("OV-TBQ3-CPU-INT4-01", "tbq3");
        var record = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, [execution]).Single();
        Assert.IsTrue(VerifiedOpenVinoOptimizationEvidence.IsReleasedTurboQuantEvidence(record, execution));
        OptimizationEvidenceRecord[] rejected =
        [
            record with { EvidenceId = "OV-TBQ4-CPU-INT4-01" },
            record with { Quality = new OptimizationQualityScore(3m) },
            record with { OutputHealthPassed = false },
            record with { StabilityPassed = false },
            record with { ActivationPassed = false },
            record with { IntegrityPassed = false },
            record with { Key = record.Key with { ModelIdentitySha256 = new string('a', 64) } },
            record with { Key = record.Key with { ParameterCount = 8_000_000_000 } },
            record with { Key = record.Key with { Route = OptimizationRoute.Gguf } },
            record with { Key = record.Key with { RuntimePackageIdentity = "other" } },
            record with { Key = record.Key with { SourceWeightRepresentation = "int8" } },
            record with { Key = record.Key with { TargetWeightRepresentation = "int8" } },
            record with { Key = record.Key with { CacheConfiguration = "tbq4" } },
            record with { Key = record.Key with { Backend = OptimizationEvidenceBackend.OpenVinoGpu } },
            record with { Key = record.Key with { DeviceClass = OptimizationEvidenceDeviceClass.IntelIntegratedGpu } },
            record with { Key = record.Key with { ContextTokens = 512 } },
            record with { Key = record.Key with { Workload = "other" } },
            record with { Key = record.Key with { MethodologyIdentity = VerifiedOpenVinoOptimizationEvidence.TurboMethodologyIdentity } },
            record with { Key = record.Key with { MemoryPerformanceProtocol = VerifiedOpenVinoOptimizationEvidence.TurboMemoryPerformanceProtocol } },
            record with { Key = record.Key with { ExecutionProfile = "other" } }
        ];
        foreach (var changed in rejected)
            Assert.IsFalse(VerifiedOpenVinoOptimizationEvidence.IsReleasedTurboQuantEvidence(changed, execution));
        Assert.IsFalse(VerifiedOpenVinoOptimizationEvidence.IsReleasedTurboQuantEvidence(null, execution));
        var changedBuild = OpenVinoExecutionAuthority.Create(
            execution.EvidenceId, execution.ConfigurationId, execution.SourceWeightPrecision,
            OpenVinoBuildIdentity.Create(execution.BuildIdentity.RuntimeBuild,
                execution.BuildIdentity.GenAiBuild, execution.BuildIdentity.TokenizersBuild, new string('a', 64)),
            execution.OptimizerVersions, true, execution.TurboQuantBuild);
        Assert.IsFalse(VerifiedOpenVinoOptimizationEvidence.IsReleasedTurboQuantEvidence(record, changedBuild));
    }

    [TestMethod]
    public void CurrentInt4U4UsesCompleteSectorQualityRatherThanSanityFloor()
    {
        var records = VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            [Authority("OV-STD-CPU-INT4-U4-01", OpenVinoKvCacheFormat.U4)]);
        Assert.HasCount(1, records);
        Assert.AreEqual(5.52m, records[0].Quality.Value);
        Assert.AreEqual("openvino-sector-experience-quality-v3", records[0].Key.MethodologyIdentity);
        Assert.AreEqual("ov-app-sector-2026-09-06-v1", records[0].Key.MemoryPerformanceProtocol);
        Assert.IsTrue(records[0].IsAdmitted);
    }

    [TestMethod]
    public void FreshRawSourceInt8TurboEvidenceDoesNotBorrowHistoricalQualityScores()
    {
        OpenVinoExecutionAuthority[] execution =
        [
            TurboAuthority("OV-TBQ3-CPU-INT8-01", "tbq3", "int8"),
            TurboAuthority("OV-TBQ4-CPU-INT8-01", "tbq4", "int8")
        ];
        OptimizationEvidenceRecord[] records =
        [.. VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, execution)];

        Assert.HasCount(2, records);
        Assert.IsTrue(records.All(item => item.IsAdmitted));
        Assert.IsTrue(records.All(item => item.Quality.Level == OptimizationQualityLevel.Acceptable));
        Assert.IsTrue(records.All(item => item.Key.MemoryPerformanceProtocol ==
            VerifiedOpenVinoOptimizationEvidence.FreshInt8MemoryPerformanceProtocol));
    }

    [TestMethod]
    public void FreshRawSourceInt8StandardEvidenceRequiresTheExactExecutionClosure()
    {
        OpenVinoExecutionAuthority[] execution =
        [
            Authority("OV-STD-CPU-AUTO-01", OpenVinoKvCacheFormat.RouteDefault, "int8"),
            Authority("OV-STD-CPU-INT8-U8-01", OpenVinoKvCacheFormat.U8, "int8"),
            Authority("OV-STD-CPU-INT8-U4-01", OpenVinoKvCacheFormat.U4, "int8")
        ];
        OptimizationEvidenceRecord[] records =
        [.. VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount, execution)];

        CollectionAssert.AreEquivalent(execution.Select(item => item.EvidenceId).ToArray(),
            records.Select(item => item.EvidenceId).ToArray());
        Assert.IsTrue(records.All(item => item.IsAdmitted));
        Assert.IsTrue(records.All(item => item.Quality.Level == OptimizationQualityLevel.Acceptable),
            "Functional sanity evidence must not invent a higher quality grade for INT8.");
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            new string('a', 64), VerifiedOpenVinoOptimizationEvidence.ParameterCount, execution));
    }

    [TestMethod]
    public void ReleasedSnapshotAcceptsOnlyThePinnedSeparateTurboWorker()
    {
        OpenVinoExecutionAuthority exact = TurboAuthority("OV-TBQ4-CPU-INT4-01", "tbq4");
        OpenVinoAdmittedConfiguration admitted = OpenVinoAdmittedConfiguration.Create(
            exact.EvidenceId, DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4,
            OpenVinoKvCacheFormat.TurboQuantTbq4, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled, 1, 512, 4096,
            SupportLevel.Experimental, requiresEvidence: true);
        OpenVinoCapabilityPayload accepted = OpenVinoCapabilityPayload.Create(
            VerifiedOpenVinoOptimizationEvidence.RuntimeBuild, [admitted], [exact]);
        Assert.AreEqual(exact, accepted.ExecutionAuthorities[exact.EvidenceId]);

        OpenVinoExecutionAuthority changed = OpenVinoExecutionAuthority.Create(
            exact.EvidenceId, exact.ConfigurationId, exact.SourceWeightPrecision,
            OpenVinoBuildIdentity.Create(exact.BuildIdentity.RuntimeBuild,
                exact.BuildIdentity.GenAiBuild, exact.BuildIdentity.TokenizersBuild,
                new string('f', 64)), exact.OptimizerVersions, true, exact.TurboQuantBuild);
        Assert.Throws<ArgumentException>(() => OpenVinoCapabilityPayload.Create(
            VerifiedOpenVinoOptimizationEvidence.RuntimeBuild, [admitted], [changed]));
    }

    [TestMethod]
    public void ExactObservedSourceAndExecutionClosurePublishU8AndU4Rows()
    {
        OpenVinoExecutionAuthority[] execution =
        [
            Authority("OV-STD-CPU-INT4-U8-01", OpenVinoKvCacheFormat.U8),
            Authority("OV-STD-CPU-INT4-U4-01", OpenVinoKvCacheFormat.U4)
        ];

        OptimizationEvidenceRecord[] records =
        [.. VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            execution)];

        CollectionAssert.AreEquivalent(
            new[] { "OV-STD-CPU-INT4-U8-01", "OV-STD-CPU-INT4-U4-01" },
            records.Select(static item => item.EvidenceId).ToArray());
        Assert.IsTrue(records.All(static item => item.IsAdmitted));
        Assert.IsTrue(VerifiedOpenVinoOptimizationEvidence.TryResolveParameterCount(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            out ulong parameterCount));
        Assert.AreEqual(VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            parameterCount);
    }

    [TestMethod]
    public void DifferentSourceOrExecutionClosurePublishesNoRows()
    {
        OpenVinoExecutionAuthority exact = Authority(
            "OV-STD-CPU-INT4-U8-01",
            OpenVinoKvCacheFormat.U8);
        OpenVinoExecutionAuthority changed = OpenVinoExecutionAuthority.Create(
            exact.EvidenceId,
            exact.ConfigurationId,
            exact.SourceWeightPrecision,
            OpenVinoBuildIdentity.Create(
                exact.BuildIdentity.RuntimeBuild,
                exact.BuildIdentity.GenAiBuild,
                exact.BuildIdentity.TokenizersBuild,
                new string('f', 64)),
            exact.OptimizerVersions,
            compiledCacheIsDisposable: true);

        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            new string('a', 64),
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            [exact]));
        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            [changed]));
    }

    [TestMethod]
    public void ExactTurboQuantClosurePublishesTbq4AndTbq3ForVerifiedGraniteArtifacts()
    {
        OpenVinoExecutionAuthority[] execution =
        [
            TurboAuthority("OV-TBQ4-CPU-INT4-01", "tbq4"),
            TurboAuthority("OV-TBQ3-CPU-INT4-01", "tbq3"),
            TurboAuthority("OV-TBQ4-CPU-MXFP4-01", "tbq4", "mxfp4"),
            TurboAuthority("OV-TBQ3-CPU-MXFP4-01", "tbq3", "mxfp4")
        ];

        OptimizationEvidenceRecord[] records =
        [.. VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            execution)];

        CollectionAssert.AreEquivalent(
            new[]
            {
                "OV-TBQ4-CPU-INT4-01", "OV-TBQ3-CPU-INT4-01",
                "OV-TBQ4-CPU-MXFP4-01", "OV-TBQ3-CPU-MXFP4-01"
            },
            records.Select(static item => item.EvidenceId).ToArray());
        Assert.IsTrue(records.All(static item => item.IsAdmitted));
        Assert.AreEqual(OptimizationQualityLevel.Fair,
            records.Single(item => item.EvidenceId == "OV-TBQ4-CPU-INT4-01").Quality.Level);
        Assert.AreEqual(OptimizationQualityLevel.Fair,
            records.Single(item => item.EvidenceId == "OV-TBQ3-CPU-INT4-01").Quality.Level);
        Assert.IsTrue(records.Where(item => item.EvidenceId.Contains("MXFP4", StringComparison.Ordinal))
            .All(item => item.Quality.Level == OptimizationQualityLevel.Acceptable));
    }

    [TestMethod]
    public void TurboQuantEvidenceWithAnyDifferentPackagedDigestIsRejected()
    {
        OpenVinoExecutionAuthority exact = TurboAuthority(
            "OV-TBQ4-CPU-MXFP4-01", "tbq4", "mxfp4");
        OpenVinoExecutionAuthority changed = OpenVinoExecutionAuthority.Create(
            exact.EvidenceId,
            exact.ConfigurationId,
            exact.SourceWeightPrecision,
            OpenVinoBuildIdentity.Create(
                exact.BuildIdentity.RuntimeBuild,
                exact.BuildIdentity.GenAiBuild,
                exact.BuildIdentity.TokenizersBuild,
                new string('f', 64)),
            exact.OptimizerVersions,
            compiledCacheIsDisposable: true,
            exact.TurboQuantBuild);

        Assert.IsEmpty(VerifiedOpenVinoOptimizationEvidence.Records(
            VerifiedOpenVinoOptimizationEvidence.SourceModelSha256,
            VerifiedOpenVinoOptimizationEvidence.ParameterCount,
            [changed]));
    }

    private static OpenVinoExecutionAuthority Authority(
        string evidenceId,
        OpenVinoKvCacheFormat cache,
        string weights = "int4") => OpenVinoExecutionAuthority.Create(
        evidenceId,
        $"openvino.standard.cpu.{weights}.{(cache == OpenVinoKvCacheFormat.U8 ? "u8" : cache == OpenVinoKvCacheFormat.U4 ? "u4" : "default")}.v1",
        OpenVinoWeightPrecision.Fp16,
        OpenVinoBuildIdentity.Create(
            VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
            VerifiedOpenVinoOptimizationEvidence.GenAiBuild,
            VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.WorkerManifestSha256),
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        },
        compiledCacheIsDisposable: true);

    private static OpenVinoExecutionAuthority TurboAuthority(
        string evidenceId,
        string cache,
        string weights = "int4") => OpenVinoExecutionAuthority.Create(
            evidenceId,
            $"openvino.turboquant.cpu.{weights}.{cache}.v1",
            OpenVinoWeightPrecision.Fp16,
            OpenVinoBuildIdentity.Create(
                VerifiedOpenVinoOptimizationEvidence.TurboRuntimeBuild,
                VerifiedOpenVinoOptimizationEvidence.TurboGenAiBuild,
                VerifiedOpenVinoOptimizationEvidence.TurboTokenizersBuild,
                VerifiedOpenVinoOptimizationEvidence.TurboWorkerManifestSha256),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["nncf"] = "3.3.0",
                ["openvino"] = "2026.3.0",
                ["openvino-genai"] = "2026.3.0.0",
                ["optimum"] = "2.3.0",
                ["optimum-intel"] = "2.1.0",
                ["transformers"] = "5.5.4"
            },
            compiledCacheIsDisposable: true,
            TurboQuantBuildIdentity.Create(
                "f5f594dc0c9e5961785f0d17743486d52eac87e7",
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256,
                VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256));
}
