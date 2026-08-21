using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.ModeSelection;

[TestClass]
public sealed class ModeComparerTests
{
    private static GgufRouteConfiguration Configuration(
        GgufWeightFormat weights = GgufWeightFormat.Imported,
        GgufKvCacheFormat kv = GgufKvCacheFormat.F16,
        DeviceRouteId device = DeviceRouteId.Cpu,
        CompatibilityBackend backend = CompatibilityBackend.Cpu,
        GpuOffloadLevel offload = GpuOffloadLevel.None) =>
        GgufRouteConfiguration.Create(weights, kv, backend, device, offload);

    private static EvaluatedCandidate Candidate(
        int context = 4096,
        WeightQuantisation quantisation = WeightQuantisation.Q4_K_M,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        bool experimental = false,
        CandidatePreparation preparation = CandidatePreparation.None,
        decimal pressureRatio = 0.5m,
        ulong headroom = 1_000_000,
        ulong addedStorage = 0,
        GgufRouteConfiguration? configuration = null,
        string entryId = "entry-1")
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration ?? Configuration(),
            ContextTokenCount.FromTokens(context),
            preparation,
            entryId,
            experimental,
            isBaseline: false);

        ResourceEstimate estimate = ResourceEstimate.Established(
            [
                ResourceComponent.Create(
                    ResourceComponentKind.Weights,
                    ResourceTarget.SystemMemory,
                    ByteCount.FromBytes(1024),
                    new HashSet<LifecyclePhase> { LifecyclePhase.Load })
            ],
            new HashSet<EstimationLimitation>());

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                CompatibilityFitState.Safe,
                FitLimitingReason.None,
                ByteCount.FromBytes(8192),
                ByteCount.FromBytes(1024),
                ByteCount.FromBytes(headroom),
                pressureRatio),
            quantisation,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.FromBytes(addedStorage));
    }

    private static IComparer<EvaluatedCandidate> Comparer(
        CompatibilityMode mode, int preservation = 4096) =>
        ModeComparers.For(
            mode, ContextTokenCount.FromTokens(preservation), Configuration());

    private static void AssertPrefers(
        IComparer<EvaluatedCandidate> comparer,
        EvaluatedCandidate better,
        EvaluatedCandidate worse)
    {
        Assert.IsTrue(comparer.Compare(better, worse) < 0, "Expected the first to win.");
        Assert.IsTrue(comparer.Compare(worse, better) > 0, "Comparison must be antisymmetric.");
    }

    [TestMethod]
    public void Quality_PrefersPreservingTheRequestedContext()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(context: 4096),
            Candidate(context: 2048));
    }

    [TestMethod]
    public void Quality_PrefersAHigherQualityTierOnceContextTies()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(quantisation: WeightQuantisation.Q8_0),
            Candidate(quantisation: WeightQuantisation.Q4_K_M));
    }

    [TestMethod]
    public void Quality_PutsContextAheadOfQuality()
    {
        // Lexicographic, not weighted: a better quality tier cannot buy back a
        // context the user asked to keep.
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(context: 4096, quantisation: WeightQuantisation.Q3_K_M),
            Candidate(context: 2048, quantisation: WeightQuantisation.Q8_0));
    }

    [TestMethod]
    public void Quality_PrefersNonExperimentalOnceQualityAndEvidenceTie()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(experimental: false),
            Candidate(experimental: true));
    }

    [TestMethod]
    public void Quality_PrefersTheLeastDestructivePreparation()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(preparation: CandidatePreparation.None),
            Candidate(preparation: CandidatePreparation.WeightConversionRequired));
    }

    [TestMethod]
    public void Efficiency_PrefersTheLowerWorstPoolPressureRatio()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(pressureRatio: 0.25m),
            Candidate(pressureRatio: 0.75m));
    }

    [TestMethod]
    public void Efficiency_PutsPressureAheadOfContext()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(pressureRatio: 0.25m, context: 1024),
            Candidate(pressureRatio: 0.75m, context: 8192));
    }

    [TestMethod]
    public void Efficiency_PrefersLessAddedStorageOncePressureTies()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(addedStorage: 0),
            Candidate(addedStorage: 5_000_000_000));
    }

    [TestMethod]
    public void Efficiency_PrefersMoreContextOncePressureAndStorageTie()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(context: 8192),
            Candidate(context: 2048));
    }

    [TestMethod]
    public void Automatic_PrefersAvoidingAWeightConversion()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Automatic),
            Candidate(preparation: CandidatePreparation.RuntimeProfileOnly),
            Candidate(preparation: CandidatePreparation.WeightConversionRequired));
    }

    [TestMethod]
    public void Automatic_PrefersTheConfigurationClosestToTheImportedOne()
    {
        // Two changes from the baseline lose to one, all else equal.
        AssertPrefers(
            Comparer(CompatibilityMode.Automatic),
            Candidate(configuration: Configuration(kv: GgufKvCacheFormat.Q8_0)),
            Candidate(configuration: Configuration(
                kv: GgufKvCacheFormat.Q8_0,
                device: DeviceRouteId.IntelDiscreteGpu,
                backend: CompatibilityBackend.IntelSycl,
                offload: GpuOffloadLevel.Full)));
    }

    [TestMethod]
    public void Automatic_PutsNonExperimentalAheadOfAvoidingConversion()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Automatic),
            Candidate(experimental: false, preparation: CandidatePreparation.WeightConversionRequired),
            Candidate(experimental: true, preparation: CandidatePreparation.None));
    }

    [TestMethod]
    public void Balanced_PrefersMoreHeadroomOnceContextAndQualityTie()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Balanced),
            Candidate(headroom: 8_000_000),
            Candidate(headroom: 1_000_000));
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_BreaksTiesByFingerprintSoTheOrderIsTotal(string mode)
    {
        // Two candidates identical in every ranked respect but from different
        // entries. Without a tiebreak the winner would depend on input order.
        IComparer<EvaluatedCandidate> comparer =
            Comparer(Enum.Parse<CompatibilityMode>(mode));

        EvaluatedCandidate first = Candidate(configuration: Configuration());
        EvaluatedCandidate second = Candidate(
            configuration: Configuration(kv: GgufKvCacheFormat.Q8_0));

        Assert.AreNotEqual(0, comparer.Compare(first, second));
        Assert.AreEqual(
            -Math.Sign(comparer.Compare(first, second)),
            Math.Sign(comparer.Compare(second, first)));
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_ComparesACandidateToItselfAsEqual(string mode)
    {
        EvaluatedCandidate candidate = Candidate();

        Assert.AreEqual(
            0,
            Comparer(Enum.Parse<CompatibilityMode>(mode)).Compare(candidate, candidate));
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_RecordsPerformanceAsNotEstablished(string mode)
    {
        // Performance is in three of the four orderings but nothing measures it.
        // The factor list must say so rather than implying it separated anything.
        IReadOnlyList<SelectionFactor> factors =
            ModeComparers.FactorsFor(Enum.Parse<CompatibilityMode>(mode));

        Assert.IsFalse(
            factors.Contains(SelectionFactor.Performance),
            "Performance cannot be a live factor while nothing measures it.");
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_EndsItsFactorListWithTheFingerprintTiebreak(string mode)
    {
        IReadOnlyList<SelectionFactor> factors =
            ModeComparers.FactorsFor(Enum.Parse<CompatibilityMode>(mode));

        Assert.AreEqual(SelectionFactor.Fingerprint, factors[^1]);
    }

    [TestMethod]
    public void For_RejectsAnUnspecifiedMode()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ModeComparers.For(
                CompatibilityMode.Unspecified,
                ContextTokenCount.FromTokens(4096),
                Configuration()));
    }
}
