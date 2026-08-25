using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// The properties the preference slider has to satisfy however the frontier
/// happens to be shaped.
///
/// These are the ones a user would notice being wrong: moving the slider toward
/// capability and getting something worse, moving it toward efficiency and
/// getting something larger, or five bands producing five answers on a machine
/// that only supports two.
/// </summary>
[TestClass]
public sealed class OptimizationPreferenceInvariantTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static OptimizationCandidate Candidate(
        string id,
        OptimizationAssessment quality,
        ulong peakBytes,
        bool persistent = false,
        int context = 4096,
        OptimizationAssessment stability = OptimizationAssessment.Good,
        int streams = 1)
    {
        OpenVinoWeightFormat weights = quality switch
        {
            OptimizationAssessment.Excellent => OpenVinoWeightFormat.Fp16,
            OptimizationAssessment.Good => OpenVinoWeightFormat.Int8,
            OptimizationAssessment.Acceptable => OpenVinoWeightFormat.Int4,
            _ => OpenVinoWeightFormat.Int4
        };

        return OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                weights,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                quality,
                OptimizationAssessment.Good,
                stability,
                context,
                peakBytes,
                32 * Gibibyte,
                32 * Gibibyte - peakBytes,
                0,
                persistent ? peakBytes : 0,
                persistent),
            id,
            isExperimental: false);
    }

    /// <summary>A frontier with a real spread of safe options.</summary>
    private static IReadOnlyList<OptimizationCandidate> Spread() =>
    [
        Candidate("tiny", OptimizationAssessment.Poor, 2 * Gibibyte, persistent: true),
        Candidate("small", OptimizationAssessment.Acceptable, 4 * Gibibyte, persistent: true),
        Candidate("mid", OptimizationAssessment.Good, 8 * Gibibyte, persistent: true),
        Candidate("large", OptimizationAssessment.Excellent, 16 * Gibibyte)
    ];

    private static OptimizationCandidate Resolve(
        IReadOnlyList<OptimizationCandidate> candidates, OptimizationPreferenceBand band) =>
        OptimizationPreferenceResolver.Resolve(
            candidates,
            OptimizationPreferenceSelection.Manual(
                OptimizationPreferenceResolver.RepresentativeValue(band)))!.Candidate;

    private static readonly OptimizationPreferenceBand[] EfficiencyToCapability =
    [
        OptimizationPreferenceBand.MaximumEfficiency,
        OptimizationPreferenceBand.Efficient,
        OptimizationPreferenceBand.Balanced,
        OptimizationPreferenceBand.HighCapability,
        OptimizationPreferenceBand.MaximumCapability
    ];

    [TestMethod]
    public void QualityNeverDecreasesTowardMaximumCapability()
    {
        // Moving the slider right and getting a worse model is the single most
        // visible way this feature could lie.
        OptimizationAssessment previous = OptimizationAssessment.Unknown;

        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            OptimizationAssessment quality = Resolve(Spread(), band).Metrics.Quality;

            Assert.IsTrue(
                quality >= previous,
                $"{band} resolved to lower quality than the band before it.");

            previous = quality;
        }
    }

    [TestMethod]
    public void MemoryNeverIncreasesTowardMaximumEfficiency()
    {
        // The mirror property. Moving left must never cost more memory.
        ulong previous = ulong.MaxValue;

        foreach (OptimizationPreferenceBand band in EfficiencyToCapability.Reverse())
        {
            ulong peak = Resolve(Spread(), band).Metrics.PredictedPeakBytes;

            Assert.IsTrue(
                peak <= previous,
                $"{band} resolved to more memory than the band toward capability.");

            previous = peak;
        }
    }

    [TestMethod]
    public void AdjacentBandsMaySelectTheSameCandidateAndSaySo()
    {
        // A machine supporting one safe setup gets one answer at every
        // position. Fabricating four more to fill the slider is the thing the
        // design forbids outright.
        IReadOnlyList<OptimizationCandidate> only =
            [Candidate("only", OptimizationAssessment.Good, 6 * Gibibyte)];

        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
                only,
                OptimizationPreferenceSelection.Manual(
                    OptimizationPreferenceResolver.RepresentativeValue(band)))!;

            Assert.AreEqual("only", selection.Candidate.EvidenceId);
            Assert.IsTrue(
                selection.SharedWithAdjacentBand,
                $"{band} shares its answer with a neighbour but did not report it.");
        }
    }

    [TestMethod]
    public void NoBandIsHardcodedToAPrecision()
    {
        // The same band on two different machines must be able to resolve to
        // two different representations. If a band were pinned to a precision,
        // both would come back identical.
        OpenVinoWeightFormat Rich() =>
            ((OpenVinoRouteConfiguration)Resolve(
                Spread(), OptimizationPreferenceBand.MaximumCapability).Configuration).Weights;

        OpenVinoWeightFormat Constrained() =>
            ((OpenVinoRouteConfiguration)Resolve(
                [
                    Candidate("a", OptimizationAssessment.Poor, 1 * Gibibyte),
                    Candidate("b", OptimizationAssessment.Acceptable, 2 * Gibibyte)
                ],
                OptimizationPreferenceBand.MaximumCapability).Configuration).Weights;

        Assert.AreNotEqual(
            Rich(),
            Constrained(),
            "Maximum capability produced the same representation on two different "
            + "frontiers, which means it is pinned to a precision rather than "
            + "selected from what the machine supports.");
    }

    [TestMethod]
    public void MaximumEfficiencyTakesTheSmallestSafeCandidate()
    {
        Assert.AreEqual(
            "tiny",
            Resolve(Spread(), OptimizationPreferenceBand.MaximumEfficiency).EvidenceId);
    }

    [TestMethod]
    public void MaximumCapabilityTakesTheHighestQualityCandidate()
    {
        Assert.AreEqual(
            "large",
            Resolve(Spread(), OptimizationPreferenceBand.MaximumCapability).EvidenceId);
    }

    [TestMethod]
    public void AutomaticPrefersAvoidingAnUnnecessaryConversion()
    {
        // Two setups of equal quality, one of which writes a file. A user who
        // expressed no preference must not be quietly committed to disk writes.
        IReadOnlyList<OptimizationCandidate> pair =
        [
            Candidate("converts", OptimizationAssessment.Good, 8 * Gibibyte, persistent: true),
            Candidate("as-is", OptimizationAssessment.Good, 8 * Gibibyte)
        ];

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            pair, OptimizationPreferenceSelection.Automatic())!;

        Assert.AreEqual("as-is", selection.Candidate.EvidenceId);
        Assert.IsFalse(selection.Candidate.Metrics.RequiresPersistentChange);
    }

    [TestMethod]
    public void AutomaticNeverSelectsWarnedPoorQ2KWhenAcceptableFits()
    {
        OptimizationCandidate q2 = GgufCandidate(
            "q2", GgufWeightFormat.Q2K, OptimizationAssessment.Poor,
            2 * Gibibyte, OptimizationCandidateNotice.LowQualityRequantisation);
        OptimizationCandidate acceptable = GgufCandidate(
            "q4", GgufWeightFormat.Q4KM, OptimizationAssessment.Acceptable,
            4 * Gibibyte, OptimizationCandidateNotice.None);

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [q2, acceptable], OptimizationPreferenceSelection.Automatic())!;

        Assert.AreEqual("q4", selection.Candidate.EvidenceId);
    }

    [TestMethod]
    public void MaximumEfficiencySelectsQ2KOnlyAsWarnedLowestMemoryCandidate()
    {
        OptimizationCandidate q2 = GgufCandidate(
            "q2", GgufWeightFormat.Q2K, OptimizationAssessment.Poor,
            2 * Gibibyte, OptimizationCandidateNotice.LowQualityRequantisation);
        OptimizationCandidate acceptable = GgufCandidate(
            "q4", GgufWeightFormat.Q4KM, OptimizationAssessment.Acceptable,
            4 * Gibibyte, OptimizationCandidateNotice.None);

        OptimizationCandidate selected = Resolve(
            [acceptable, q2], OptimizationPreferenceBand.MaximumEfficiency);

        Assert.AreEqual("q2", selected.EvidenceId);
        Assert.AreEqual(
            OptimizationCandidateNotice.LowQualityRequantisation,
            selected.Notice);
        Assert.IsTrue(selected.Metrics.PredictedPeakBytes
            < acceptable.Metrics.PredictedPeakBytes);
    }

    [TestMethod]
    public void Q2KCannotEnterTheFrontierWithoutATypedQualityWarning()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufCandidate(
            "unwarned-q2", GgufWeightFormat.Q2K, OptimizationAssessment.Poor,
            2 * Gibibyte, OptimizationCandidateNotice.None));
    }

    private static OptimizationCandidate GgufCandidate(
        string id,
        GgufWeightFormat weights,
        OptimizationAssessment quality,
        ulong peakBytes,
        OptimizationCandidateNotice notice) =>
        OptimizationCandidate.Create(
            GgufRouteConfiguration.Create(
                weights, GgufKvCacheFormat.F16, CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu, GpuOffloadLevel.None),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated, quality, OptimizationAssessment.Good,
                OptimizationAssessment.Good, 4096, peakBytes, 32 * Gibibyte,
                32 * Gibibyte - peakBytes, peakBytes, peakBytes,
                requiresPersistentChange: true),
            id,
            isExperimental: false,
            notice);

    [TestMethod]
    public void AutomaticStillTakesAConversionThatIsClearlyBetter()
    {
        // The penalty is a bias, not a veto. Refusing every conversion would
        // make Automatic useless on a machine where the original does not fit
        // well.
        IReadOnlyList<OptimizationCandidate> pair =
        [
            Candidate("as-is", OptimizationAssessment.Poor, 8 * Gibibyte),
            Candidate("converts", OptimizationAssessment.Excellent, 8 * Gibibyte, persistent: true)
        ];

        Assert.AreEqual(
            "converts",
            OptimizationPreferenceResolver.Resolve(
                pair, OptimizationPreferenceSelection.Automatic())!.Candidate.EvidenceId);
    }

    [TestMethod]
    public void AutomaticSelectsFromTheSameFrontier()
    {
        // Not a separate search. If Automatic could reach something the bands
        // could not, it would be a sixth answer with no explanation available.
        IReadOnlyList<OptimizationCandidate> spread = Spread();

        string automatic = OptimizationPreferenceResolver.Resolve(
            spread, OptimizationPreferenceSelection.Automatic())!.Candidate.CanonicalDescriptor;

        Assert.IsTrue(
            SafeCandidateFrontier.Create(spread)
                .Any(candidate => candidate.CanonicalDescriptor == automatic));
    }

    [TestMethod]
    public void ResolutionIsDeterministic()
    {
        // The same inputs must issue the same plan twice, or a user could
        // confirm one configuration and receive another.
        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            Assert.AreEqual(
                Resolve(Spread(), band).CanonicalDescriptor,
                Resolve(Spread(), band).CanonicalDescriptor);
        }
    }

    [TestMethod]
    public void ResolutionDoesNotDependOnInputOrder()
    {
        // Candidate order is an accident of how the capability snapshot was
        // written. Letting it decide the answer would make the plan depend on
        // something nobody chose.
        IReadOnlyList<OptimizationCandidate> forward = Spread();
        IReadOnlyList<OptimizationCandidate> reversed = [.. Spread().Reverse()];

        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            Assert.AreEqual(
                Resolve(forward, band).CanonicalDescriptor,
                Resolve(reversed, band).CanonicalDescriptor,
                $"{band} depends on the order candidates arrived in.");
        }
    }

    [TestMethod]
    public void EveryBandResolvesWhenAnythingIsAdmitted()
    {
        // A band that returned nothing would leave the page with a slider
        // position that shows no configuration at all.
        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            Assert.IsNotNull(
                OptimizationPreferenceResolver.Resolve(
                    Spread(),
                    OptimizationPreferenceSelection.Manual(
                        OptimizationPreferenceResolver.RepresentativeValue(band))),
                $"{band} resolved to nothing.");
        }
    }

    [TestMethod]
    public void NothingAdmittedResolvesToNothing()
    {
        // Not to a fabricated candidate. "No safe setup exists" is a real
        // answer, and inventing one to fill the slider would be the exact
        // false-safe this design exists to prevent.
        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [], OptimizationPreferenceSelection.Automatic()));

        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [], OptimizationPreferenceSelection.Manual(50)));
    }

    [TestMethod]
    public void EverySelectedCandidateFitsSafely()
    {
        // Whatever the preference, the answer is inside the budget. There is no
        // band that trades safety for capability.
        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            Assert.IsTrue(Resolve(Spread(), band).Metrics.FitsSafely);
        }

        Assert.IsTrue(OptimizationPreferenceResolver.Resolve(
            Spread(), OptimizationPreferenceSelection.Automatic())!.Candidate.Metrics.FitsSafely);
    }

    [TestMethod]
    public void DominatedCandidateIsRemoved()
    {
        OptimizationCandidate strong =
            Candidate("strong", OptimizationAssessment.Excellent, 8 * Gibibyte);
        OptimizationCandidate weak =
            Candidate("weak", OptimizationAssessment.Good, 9 * Gibibyte);

        CollectionAssert.DoesNotContain(
            SafeCandidateFrontier.Create([strong, weak]).ToArray(), weak);
    }

    [TestMethod]
    public void EquallyGoodCandidatesBothSurvive()
    {
        // Neither dominates the other, so eliminating one would depend on which
        // was seen first.
        OptimizationCandidate cheap =
            Candidate("cheap", OptimizationAssessment.Acceptable, 4 * Gibibyte);
        OptimizationCandidate better =
            Candidate("better", OptimizationAssessment.Excellent, 12 * Gibibyte);

        Assert.AreEqual(2, SafeCandidateFrontier.Create([cheap, better]).Count);
    }

    [TestMethod]
    public void FrontierRunsFromEfficiencyToCapability()
    {
        IReadOnlyList<OptimizationCandidate> frontier = SafeCandidateFrontier.Create(Spread());

        for (int index = 1; index < frontier.Count; index++)
        {
            Assert.IsTrue(
                frontier[index].Metrics.PredictedPeakBytes
                    >= frontier[index - 1].Metrics.PredictedPeakBytes,
                "The frontier is not ordered from least memory upward, so it no "
                + "longer runs the same direction as the preference bands.");
        }
    }

    [TestMethod]
    public void ConversionFreeCandidateIsNotDominatedByAnIdenticalConvertingOne()
    {
        // Writing a file is a cost even when the resulting configuration is the
        // same, so the one that avoids it must survive.
        OptimizationCandidate converts =
            Candidate("converts", OptimizationAssessment.Good, 8 * Gibibyte, persistent: true);
        OptimizationCandidate asIs =
            Candidate("as-is", OptimizationAssessment.Good, 8 * Gibibyte);

        CollectionAssert.Contains(
            SafeCandidateFrontier.Create([converts, asIs]).ToArray(), asIs);
    }
}
