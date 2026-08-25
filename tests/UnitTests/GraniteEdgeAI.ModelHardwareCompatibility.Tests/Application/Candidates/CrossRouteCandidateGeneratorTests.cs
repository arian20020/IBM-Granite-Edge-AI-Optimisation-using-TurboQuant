using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
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

    private static class CrossRouteTestData
    {
        internal static InspectedModelFacts Facts() =>
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, 15, 2);

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
                "ov-cap", Digest, OpenVinoCapabilityPayload.Create("2026.1.0", admitted));

        internal static OptimizationCapabilitySnapshot GgufSnapshot(
            params GgufAdmittedConfiguration[] admitted) =>
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", Digest, GgufCapabilityPayload.Create("b4321", admitted));
    }

    private static CrossRouteGenerationResult Generate(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload? workload = null,
        ulong budgetGibibytes = 32,
        ulong diskGibibytes = 500,
        params string[] optedIn) =>
        CrossRouteCandidateGenerator.Generate(
            snapshot,
            CrossRouteTestData.Facts(),
            workload ?? CrossRouteTestData.Workload(),
            ByteCount.FromBytes(budgetGibibytes * Gibibyte),
            ByteCount.FromBytes(diskGibibytes * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(optedIn));

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
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>());

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
