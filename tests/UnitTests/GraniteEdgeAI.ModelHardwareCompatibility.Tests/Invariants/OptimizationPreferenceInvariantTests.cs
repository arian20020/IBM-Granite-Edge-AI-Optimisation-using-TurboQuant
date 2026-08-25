using System.Reflection;
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
    private const string Digest64 =
        "1111111111111111111111111111111111111111111111111111111111111111";

    [TestMethod]
    [DataRow(-1)]
    [DataRow(5)]
    public void CandidateMetricsRejectUndefinedEvidenceAndAssessmentEnums(int raw)
    {
        OptimizationCandidateMetrics Create(
            EvidenceGrade evidence = EvidenceGrade.Estimated,
            OptimizationAssessment quality = OptimizationAssessment.Good,
            OptimizationAssessment performance = OptimizationAssessment.Good,
            OptimizationAssessment stability = OptimizationAssessment.Good) =>
            OptimizationCandidateMetrics.Create(
                evidence, quality, performance, stability, 4096,
                8 * Gibibyte, 32 * Gibibyte, 24 * Gibibyte,
                0, 0, false, availableDiskBytes: 32 * Gibibyte);

        Assert.ThrowsExactly<ArgumentException>(() =>
            Create(evidence: (EvidenceGrade)raw));
        Assert.ThrowsExactly<ArgumentException>(() =>
            Create(quality: (OptimizationAssessment)raw));
        Assert.ThrowsExactly<ArgumentException>(() =>
            Create(performance: (OptimizationAssessment)raw));
        Assert.ThrowsExactly<ArgumentException>(() =>
            Create(stability: (OptimizationAssessment)raw));
    }

    private static OptimizationCandidate Candidate(
        string id,
        OptimizationAssessment quality,
        ulong peakBytes,
        bool persistent = false,
        int context = 4096,
        OptimizationAssessment stability = OptimizationAssessment.Good,
        int streams = 1,
        OptimizationAssessment performance = OptimizationAssessment.Good,
        bool isExperimental = false,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        ulong safeBudgetBytes = 32 * Gibibyte,
        string snapshotId = "preference-cap",
        string capabilityDigest = Digest64,
        ulong? workingDiskBytes = null,
        ulong? outputDiskBytes = null,
        ulong? availableDiskBytes = null,
        OptimizationConversionProvenance provenance =
            OptimizationConversionProvenance.None)
    {
        OpenVinoWeightFormat weights = quality switch
        {
            OptimizationAssessment.Excellent => OpenVinoWeightFormat.Fp16,
            OptimizationAssessment.Good => OpenVinoWeightFormat.Int8,
            OptimizationAssessment.Acceptable => OpenVinoWeightFormat.Int4,
            _ => OpenVinoWeightFormat.Int4
        };

        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
                weights,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams);
        OptimizationCandidateMetrics metrics = OptimizationCandidateMetrics.Create(
                evidence,
                quality,
                performance,
                stability,
                context,
                peakBytes,
                safeBudgetBytes,
                safeBudgetBytes - peakBytes,
                workingDiskBytes ?? (persistent ? peakBytes : 0),
                outputDiskBytes ?? (persistent ? peakBytes : 0),
                persistent,
                availableDiskBytes: availableDiskBytes ?? 32 * Gibibyte);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration, metrics, id, isExperimental, provenance);
        SupportLevel level = isExperimental
            ? SupportLevel.Experimental
            : SupportLevel.DeclaredSupported;
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                snapshotId, capabilityDigest,
                OpenVinoCapabilityPayload.Create(
                    "runtime",
                    [OpenVinoAdmittedConfiguration.Create(
                        id, DeviceRouteId.Cpu, weights, OpenVinoKvCacheFormat.U8,
                        OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Disabled, streams, 1, 32768,
                        level, isExperimental)]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "preference-workload", 1, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(context)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run", "mi-handoff", Digest64, Gibibyte, "hw-run", Digest64);
        OptimizationAdmissionProof proof = OptimizationAdmissionProof.Create(
            snapshot, workload, binding, candidate, level,
            isExperimental, isExperimental ? new HashSet<string> { id } : new HashSet<string>());
        return OptimizationCandidate.AttachAdmissionProof(candidate, proof);
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
    public void AdmissionProofCannotBeReusedAfterChangingConversionProvenance()
    {
        OptimizationCandidate admitted = Candidate(
            "proof-bound", OptimizationAssessment.Poor, 2 * Gibibyte,
            persistent: true);
        OptimizationCandidate substituted = OptimizationCandidate.Create(
            admitted.Configuration,
            admitted.Metrics,
            admitted.EvidenceId,
            admitted.IsExperimental,
            OptimizationConversionProvenance.ControlledRequantisation);

        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationCandidate.AttachAdmissionProof(
                substituted, admitted.AdmissionProof!));
    }

    [TestMethod]
    public void AutomaticAvoidsALowerMemoryConversionWithNoQualityBenefit()
    {
        IReadOnlyList<OptimizationCandidate> pair =
        [
            Candidate(
                "converts", OptimizationAssessment.Good,
                4 * Gibibyte, persistent: true),
            Candidate("as-is", OptimizationAssessment.Good, 8 * Gibibyte)
        ];

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            pair, OptimizationPreferenceSelection.Automatic())!;

        Assert.AreEqual("as-is", selection.Candidate.EvidenceId);
        Assert.IsFalse(selection.Candidate.Metrics.RequiresPersistentChange);
    }

    [TestMethod]
    public void EveryManualSliderValueIsMonotonicInQualityAndMemory()
    {
        OptimizationAssessment previousQuality = OptimizationAssessment.Unknown;
        ulong previousMemory = 0;

        for (int value = 0; value <= 100; value++)
        {
            OptimizationCandidate candidate = OptimizationPreferenceResolver.Resolve(
                Spread(),
                OptimizationPreferenceSelection.Manual(value))!.Candidate;

            Assert.IsTrue(
                candidate.Metrics.Quality >= previousQuality,
                $"Slider value {value} reduced quality.");
            Assert.IsTrue(
                candidate.Metrics.PredictedPeakBytes >= previousMemory,
                $"Slider value {value} used less memory than a lower value, "
                + "breaking the deterministic frontier ordering.");

            previousQuality = candidate.Metrics.Quality;
            previousMemory = candidate.Metrics.PredictedPeakBytes;
        }
    }

    [TestMethod]
    public void SharedWithAdjacentBandMatchesTheActualFrontierMapping()
    {
        IReadOnlyList<OptimizationCandidate> frontier = SafeCandidateFrontier.Create(Spread());

        foreach (OptimizationPreferenceBand band in EfficiencyToCapability)
        {
            OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
                frontier,
                OptimizationPreferenceSelection.Manual(
                    OptimizationPreferenceResolver.RepresentativeValue(band)))!;
            bool expected = EfficiencyToCapability
                .Where(other => Math.Abs((int)other - (int)band) == 1)
                .Select(other => Resolve(frontier, other))
                .Any(other => other.CanonicalDescriptor
                    == selection.Candidate.CanonicalDescriptor);

            Assert.AreEqual(
                expected,
                selection.SharedWithAdjacentBand,
                $"{band} reported an inaccurate adjacent-band sharing state.");
        }
    }

    [TestMethod]
    public void AutomaticNeverSelectsWarnedPoorQ2KWhenAcceptableFits()
    {
        OptimizationCandidate q2 = GgufCandidate(
            "q2", GgufWeightFormat.Q2K, OptimizationAssessment.Poor,
            2 * Gibibyte, OptimizationConversionProvenance.ControlledRequantisation);
        OptimizationCandidate acceptable = GgufCandidate(
            "q4", GgufWeightFormat.Q4KM, OptimizationAssessment.Acceptable,
            4 * Gibibyte, OptimizationConversionProvenance.None);

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [q2, acceptable], OptimizationPreferenceSelection.Automatic())!;

        Assert.AreEqual("q4", selection.Candidate.EvidenceId);
    }

    [TestMethod]
    public void MaximumEfficiencySelectsQ2KOnlyAsWarnedLowestMemoryCandidate()
    {
        OptimizationCandidate q2 = GgufCandidate(
            "q2", GgufWeightFormat.Q2K, OptimizationAssessment.Poor,
            2 * Gibibyte, OptimizationConversionProvenance.ControlledRequantisation);
        OptimizationCandidate acceptable = GgufCandidate(
            "q4", GgufWeightFormat.Q4KM, OptimizationAssessment.Acceptable,
            4 * Gibibyte, OptimizationConversionProvenance.None);

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
    public void Q2KAlwaysDerivesATypedQualityWarning()
    {
        OptimizationCandidate candidate = GgufCandidate(
            "unwarned-q2", GgufWeightFormat.Q2K, OptimizationAssessment.Poor,
            2 * Gibibyte, OptimizationConversionProvenance.HigherPrecisionSource);

        Assert.AreEqual(OptimizationCandidateNotice.LowQuality, candidate.Notice);
    }

    [TestMethod]
    public void Q2KRejectsCallerSuppliedQualityAbovePoor()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufCandidate(
            "misgraded-q2", GgufWeightFormat.Q2K,
            OptimizationAssessment.Acceptable, 2 * Gibibyte,
            OptimizationConversionProvenance.HigherPrecisionSource));
    }

    private static OptimizationCandidate GgufCandidate(
        string id,
        GgufWeightFormat weights,
        OptimizationAssessment quality,
        ulong peakBytes,
        OptimizationConversionProvenance provenance)
    {
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                weights, GgufKvCacheFormat.F16, CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu, GpuOffloadLevel.None);
        OptimizationCandidateMetrics metrics = OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated, quality, OptimizationAssessment.Good,
                OptimizationAssessment.Good, 4096, peakBytes, 32 * Gibibyte,
                32 * Gibibyte - peakBytes, peakBytes, peakBytes,
                requiresPersistentChange: true,
                availableDiskBytes: 32 * Gibibyte);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration, metrics, id, false, provenance);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "preference-gguf-cap", Digest64,
            GgufCapabilityPayload.Create(
                "runtime",
                [GgufAdmittedConfiguration.Create(
                    id, CompatibilityBackend.Cpu, DeviceRouteId.Cpu, weights,
                    GgufKvCacheFormat.F16, GpuOffloadLevel.None, 1, 32768,
                    SupportLevel.DeclaredSupported, false)]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "preference-workload", 1, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run", "mi-handoff", Digest64, Gibibyte, "hw-run", Digest64);
        return OptimizationCandidate.AttachAdmissionProof(
            candidate,
            OptimizationAdmissionProof.Create(
                snapshot, workload, binding, candidate,
                SupportLevel.DeclaredSupported, false, new HashSet<string>()));
    }

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
    public void EquivalentCanonicalConfigurationsResolveToTheSameEvidenceRegardlessOfInputOrder()
    {
        OptimizationCandidate alpha = Candidate(
            "alpha-evidence", OptimizationAssessment.Good, 8 * Gibibyte);
        OptimizationCandidate omega = Candidate(
            "omega-evidence", OptimizationAssessment.Good, 8 * Gibibyte);

        string Forward() => OptimizationPreferenceResolver.Resolve(
            [omega, alpha],
            OptimizationPreferenceSelection.Automatic())!.Candidate.EvidenceId;

        string Reversed() => OptimizationPreferenceResolver.Resolve(
            [alpha, omega],
            OptimizationPreferenceSelection.Automatic())!.Candidate.EvidenceId;

        Assert.AreEqual(
            Forward(),
            Reversed(),
            "Equivalent configurations retained list-order-dependent evidence identity.");
    }

    [TestMethod]
    public void ReleasedEvidenceWinsBeforeLexicalEvidenceIdentityRegardlessOfInputOrder()
    {
        OptimizationCandidate released = Candidate(
            "z-released", OptimizationAssessment.Good, 8 * Gibibyte);
        OptimizationCandidate experimental = Candidate(
            "a-experimental", OptimizationAssessment.Good, 8 * Gibibyte,
            isExperimental: true);

        string Resolve(IReadOnlyList<OptimizationCandidate> candidates) =>
            OptimizationPreferenceResolver.Resolve(
                candidates,
                OptimizationPreferenceSelection.Automatic())!.Candidate.EvidenceId;

        Assert.AreEqual("z-released", Resolve([experimental, released]));
        Assert.AreEqual("z-released", Resolve([released, experimental]));
    }

    [TestMethod]
    public void HigherPerformanceWinsEqualFrontierTieRegardlessOfInputOrder()
    {
        OptimizationCandidate lower = Candidate(
            "a-lower", OptimizationAssessment.Good, 8 * Gibibyte,
            performance: OptimizationAssessment.Acceptable);
        OptimizationCandidate higher = Candidate(
            "z-higher", OptimizationAssessment.Good, 8 * Gibibyte,
            performance: OptimizationAssessment.Excellent);

        string Resolve(IReadOnlyList<OptimizationCandidate> candidates) =>
            OptimizationPreferenceResolver.Resolve(
                candidates, OptimizationPreferenceSelection.Automatic())!
                .Candidate.EvidenceId;

        Assert.AreEqual("z-higher", Resolve([lower, higher]));
        Assert.AreEqual("z-higher", Resolve([higher, lower]));
    }

    [TestMethod]
    public void DeterministicComparisonDistinguishesEveryMetricTradeoff()
    {
        OptimizationCandidate Create(
            OptimizationAssessment quality, ulong peak, ulong budget)
        {
            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8,
                    DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled, 1);
            OptimizationCandidate candidate = OptimizationCandidate.Create(
                configuration,
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Estimated, quality, OptimizationAssessment.Good,
                    OptimizationAssessment.Good, 4096, peak, budget,
                    budget - peak, 0, 0, false,
                    availableDiskBytes: 32 * Gibibyte),
                "strict-order", false);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    "strict-cap", Digest64,
                    OpenVinoCapabilityPayload.Create(
                        "runtime",
                        [OpenVinoAdmittedConfiguration.Create(
                            candidate.EvidenceId, DeviceRouteId.Cpu,
                            OpenVinoWeightFormat.Int8, OpenVinoKvCacheFormat.U8,
                            OpenVinoPerformanceHint.Latency,
                            OpenVinoCompiledCachePolicy.Disabled, 1, 1, 32768,
                            SupportLevel.DeclaredSupported, false)]));
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "strict-workload", 1, OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4096)]);
            OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
                "mi-run", "mi-handoff", Digest64, Gibibyte,
                "hw-run", Digest64);
            return OptimizationAdmissionTestFactory.Admit(
                candidate, snapshot, workload, binding,
                SupportLevel.DeclaredSupported);
        }

        OptimizationCandidate acceptable = Create(
            OptimizationAssessment.Acceptable, 8 * Gibibyte, 32 * Gibibyte);
        OptimizationCandidate good = Create(
            OptimizationAssessment.Good, 8 * Gibibyte, 32 * Gibibyte);
        OptimizationCandidate alternateBudget = Create(
            OptimizationAssessment.Good, 9 * Gibibyte, 33 * Gibibyte);

        Assert.AreNotEqual(0, OptimizationPreferenceResolver.Compare(acceptable, good));
        Assert.AreNotEqual(0, OptimizationPreferenceResolver.Compare(good, alternateBudget));
        Assert.AreEqual(
            -Math.Sign(OptimizationPreferenceResolver.Compare(acceptable, good)),
            Math.Sign(OptimizationPreferenceResolver.Compare(good, acceptable)));
    }

    [TestMethod]
    public void TotalOrderIsStrictAntisymmetricTransitiveAndPermutationInvariant()
    {
        OptimizationCandidate baseline = Candidate(
            "baseline", OptimizationAssessment.Good, 8 * Gibibyte);
        OptimizationCandidate[] variants =
        [
            Candidate("evidence-grade", OptimizationAssessment.Good, 8 * Gibibyte,
                evidence: EvidenceGrade.Verified),
            Candidate("experimental", OptimizationAssessment.Good, 8 * Gibibyte,
                isExperimental: true),
            Candidate("headroom", OptimizationAssessment.Good, 8 * Gibibyte,
                safeBudgetBytes: 33 * Gibibyte),
            Candidate("conversion", OptimizationAssessment.Good, 8 * Gibibyte,
                persistent: true),
            Candidate("stability", OptimizationAssessment.Good, 8 * Gibibyte,
                stability: OptimizationAssessment.Excellent),
            Candidate("performance", OptimizationAssessment.Good, 8 * Gibibyte,
                performance: OptimizationAssessment.Excellent),
            Candidate("context", OptimizationAssessment.Good, 8 * Gibibyte,
                context: 8192),
            Candidate("configuration", OptimizationAssessment.Good, 8 * Gibibyte,
                streams: 2),
            Candidate("z-evidence-id", OptimizationAssessment.Good, 8 * Gibibyte),
            Candidate("authority", OptimizationAssessment.Good, 8 * Gibibyte,
                snapshotId: "z-authority"),
            Candidate("composite-storage", OptimizationAssessment.Good, 8 * Gibibyte,
                persistent: true, workingDiskBytes: 9 * Gibibyte,
                outputDiskBytes: 8 * Gibibyte),
            Candidate("output-storage", OptimizationAssessment.Good, 8 * Gibibyte,
                persistent: true, workingDiskBytes: 9 * Gibibyte,
                outputDiskBytes: 7 * Gibibyte),
            Candidate("available-storage", OptimizationAssessment.Good, 8 * Gibibyte,
                availableDiskBytes: 31 * Gibibyte),
            Candidate("provenance", OptimizationAssessment.Good, 8 * Gibibyte,
                persistent: true,
                provenance: OptimizationConversionProvenance.ControlledRequantisation),
            Candidate("notice", OptimizationAssessment.Poor, 8 * Gibibyte,
                persistent: true,
                provenance: OptimizationConversionProvenance.ControlledRequantisation),
            Candidate("capability-digest", OptimizationAssessment.Good, 8 * Gibibyte,
                capabilityDigest:
                    "2222222222222222222222222222222222222222222222222222222222222222")
        ];

        foreach (OptimizationCandidate variant in variants)
        {
            int forward = OptimizationPreferenceResolver.Compare(baseline, variant);
            int reverse = OptimizationPreferenceResolver.Compare(variant, baseline);
            Assert.AreNotEqual(0, forward);
            Assert.AreEqual(-Math.Sign(forward), Math.Sign(reverse));

            string first = OptimizationPreferenceResolver.Resolve(
                [baseline, variant], OptimizationPreferenceSelection.Automatic())!
                .Candidate.CanonicalDescriptor + ":" +
                OptimizationPreferenceResolver.Resolve(
                    [baseline, variant], OptimizationPreferenceSelection.Automatic())!
                    .Candidate.EvidenceId;
            string reversed = OptimizationPreferenceResolver.Resolve(
                [variant, baseline], OptimizationPreferenceSelection.Automatic())!
                .Candidate.CanonicalDescriptor + ":" +
                OptimizationPreferenceResolver.Resolve(
                    [variant, baseline], OptimizationPreferenceSelection.Automatic())!
                    .Candidate.EvidenceId;
            Assert.AreEqual(first, reversed);
        }

        OptimizationCandidate[] ordered = [baseline, .. variants];
        Array.Sort(ordered, OptimizationPreferenceResolver.Compare);
        for (int i = 0; i < ordered.Length; i++)
        {
            for (int j = i + 1; j < ordered.Length; j++)
            {
                for (int k = j + 1; k < ordered.Length; k++)
                {
                    Assert.IsTrue(OptimizationPreferenceResolver.Compare(
                        ordered[i], ordered[k]) < 0);
                }
            }
        }

        OptimizationCandidate duplicate = Candidate(
            "baseline", OptimizationAssessment.Good, 8 * Gibibyte);
        Assert.AreEqual(0, OptimizationPreferenceResolver.Compare(baseline, duplicate));
        Assert.AreEqual(1, SafeCandidateFrontier.Create([baseline, duplicate]).Count);

        foreach ((string property, object changed) in new (string, object)[]
        {
            (nameof(OptimizationAdmissionProof.SupportLevel), SupportLevel.Experimental),
            (nameof(OptimizationAdmissionProof.RequiresEvidence), true),
            (nameof(OptimizationAdmissionProof.OptedInEvidenceId), "baseline"),
            (nameof(OptimizationAdmissionProof.RouteExecutionAuthoritySha256),
                "2222222222222222222222222222222222222222222222222222222222222222")
        })
        {
            FieldInfo field = typeof(OptimizationAdmissionProof).GetField(
                $"<{property}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            object? original = field.GetValue(duplicate.AdmissionProof!);
            try
            {
                field.SetValue(duplicate.AdmissionProof, changed);
                int forward = OptimizationPreferenceResolver.Compare(baseline, duplicate);
                Assert.AreNotEqual(0, forward, property);
                Assert.AreEqual(-Math.Sign(forward), Math.Sign(
                    OptimizationPreferenceResolver.Compare(duplicate, baseline)), property);
            }
            finally
            {
                field.SetValue(duplicate.AdmissionProof, original);
            }
        }
    }

    [TestMethod]
    public void TotalOrderIncludesGgufNormalizationProofIdentity()
    {
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None);
        OptimizationCandidateMetrics metrics = OptimizationCandidateMetrics.Create(
            EvidenceGrade.Estimated, OptimizationAssessment.Good,
            OptimizationAssessment.Good, OptimizationAssessment.Good,
            4096, 8 * Gibibyte, 32 * Gibibyte, 24 * Gibibyte,
            0, 0, false, availableDiskBytes: 32 * Gibibyte);
        OptimizationCandidate plain = OptimizationCandidate.Create(
            configuration, metrics, "normalization", false);
        OptimizationCandidate normalized =
            OptimizationCandidate.CreateWithGgufWeightNormalization(
                configuration, metrics, "normalization", false,
                GgufWeightNormalizationProof.FromInspection(
                    15, 2, GgufWeightFormat.Q4KM));
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "normalization-cap", Digest64,
            GgufCapabilityPayload.Create(
                "runtime",
                [GgufAdmittedConfiguration.Create(
                    "normalization", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                    GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                    GpuOffloadLevel.None, 1, 32768,
                    SupportLevel.DeclaredSupported, false)]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "normalization-workload", 1, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run", "mi-handoff", Digest64, Gibibyte, "hw-run", Digest64);
        OptimizationAdmissionProof proof = OptimizationAdmissionProof.Create(
            snapshot, workload, binding, plain, SupportLevel.DeclaredSupported,
            false, new HashSet<string>());
        plain = OptimizationCandidate.AttachAdmissionProof(plain, proof);
        normalized = OptimizationCandidate.AttachAdmissionProof(normalized, proof);

        int forward = OptimizationPreferenceResolver.Compare(plain, normalized);
        Assert.AreNotEqual(0, forward);
        Assert.AreEqual(-Math.Sign(forward), Math.Sign(
            OptimizationPreferenceResolver.Compare(normalized, plain)));
        Assert.AreEqual(
            OptimizationPreferenceResolver.Resolve(
                [plain, normalized], OptimizationPreferenceSelection.Automatic())!
                .Candidate.WeightNormalizationProof,
            OptimizationPreferenceResolver.Resolve(
                [normalized, plain], OptimizationPreferenceSelection.Automatic())!
                .Candidate.WeightNormalizationProof);
    }

    [TestMethod]
    public void ComparatorIsolatesStoragePresentationAndEveryAdmissionProofField()
    {
        OptimizationCandidate baseline = Candidate(
            "isolated", OptimizationAssessment.Good, 8 * Gibibyte);
        OptimizationCandidate changed = Candidate(
            "isolated", OptimizationAssessment.Good, 8 * Gibibyte);

        void AssertStrictAndPermutationInvariant(string dimension)
        {
            int forward = OptimizationPreferenceResolver.Compare(baseline, changed);
            int reverse = OptimizationPreferenceResolver.Compare(changed, baseline);
            Assert.AreNotEqual(0, forward, dimension);
            Assert.AreEqual(-Math.Sign(forward), Math.Sign(reverse), dimension);
            Assert.AreEqual(
                OptimizationPreferenceResolver.Resolve(
                    [baseline, changed], OptimizationPreferenceSelection.Automatic())!
                    .Candidate,
                OptimizationPreferenceResolver.Resolve(
                    [changed, baseline], OptimizationPreferenceSelection.Automatic())!
                    .Candidate,
                dimension);
        }

        foreach ((object target, Type owner, string property, object value) in new[]
        {
            ((object)changed.Metrics, typeof(OptimizationCandidateMetrics),
                nameof(OptimizationCandidateMetrics.WorkingDiskBytes), (object)1UL),
            (changed.Metrics, typeof(OptimizationCandidateMetrics),
                nameof(OptimizationCandidateMetrics.OutputDiskBytes), (object)1UL),
            (changed.Metrics, typeof(OptimizationCandidateMetrics),
                nameof(OptimizationCandidateMetrics.AvailableDiskBytes),
                (object)(ulong?)(31 * Gibibyte)),
            (changed, typeof(OptimizationCandidate),
                nameof(OptimizationCandidate.ConversionProvenance),
                (object)OptimizationConversionProvenance.HigherPrecisionSource),
            (changed, typeof(OptimizationCandidate),
                nameof(OptimizationCandidate.Notice),
                (object)OptimizationCandidateNotice.LowQuality)
        })
        {
            FieldInfo field = owner.GetField(
                $"<{property}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            object? original = field.GetValue(target);
            try
            {
                field.SetValue(target, value);
                AssertStrictAndPermutationInvariant(property);
            }
            finally
            {
                field.SetValue(target, original);
            }
        }

        HashSet<string> orderedAuthorityFields = new(StringComparer.Ordinal)
        {
            nameof(OptimizationAdmissionProof.SnapshotId),
            nameof(OptimizationAdmissionProof.CapabilitySnapshotSha256),
            nameof(OptimizationAdmissionProof.WorkloadId),
            nameof(OptimizationAdmissionProof.WorkloadSha256),
            nameof(OptimizationAdmissionProof.JourneySha256),
            nameof(OptimizationAdmissionProof.RouteExecutionAuthoritySha256),
            nameof(OptimizationAdmissionProof.SupportLevel),
            nameof(OptimizationAdmissionProof.RequiresEvidence),
            nameof(OptimizationAdmissionProof.OptedInEvidenceId)
        };
        FieldInfo[] proofFields = typeof(OptimizationAdmissionProof)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.Name.Contains("BackingField", StringComparison.Ordinal))
            .ToArray();
        foreach (FieldInfo field in proofFields)
        {
            object? original = field.GetValue(changed.AdmissionProof!);
            object replacement = field.FieldType == typeof(string)
                ? (string?)original + "-changed"
                : field.FieldType == typeof(bool)
                    ? !(bool)original!
                    : field.FieldType == typeof(int)
                        ? checked((int)original! + 1)
                        : field.FieldType == typeof(ulong)
                            ? checked((ulong)original! + 1)
                            : field.FieldType.IsEnum
                                ? Enum.GetValues(field.FieldType).Cast<object>()
                                    .First(value => !value.Equals(original))
                                : throw new AssertFailedException(
                                    $"No mutation for {field.FieldType.Name}.");
            string property = field.Name[1..field.Name.IndexOf('>')];
            try
            {
                field.SetValue(changed.AdmissionProof, replacement);
                if (orderedAuthorityFields.Contains(property))
                {
                    AssertStrictAndPermutationInvariant(property);
                }
                else
                {
                    Assert.IsFalse(
                        changed.AdmissionProof!.MatchesCandidate(changed),
                        $"{property} is neither ordered authority nor candidate-bound.");
                }
            }
            finally
            {
                field.SetValue(changed.AdmissionProof, original);
            }
        }

        Assert.AreEqual(27, proofFields.Length);
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
    public void UnsafeCandidateCannotReenterThroughPreferenceResolution()
    {
        OptimizationCandidate unsafeCandidate = OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U4,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams: 1),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Acceptable,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes: 8 * Gibibyte,
                safeBudgetBytes: 4 * Gibibyte,
                headroomBytes: 0,
                workingDiskBytes: 0,
                outputDiskBytes: 0,
                requiresPersistentChange: false,
                availableDiskBytes: 32 * Gibibyte),
            "excluded-over-budget",
            isExperimental: false);

        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [unsafeCandidate], OptimizationPreferenceSelection.Automatic()));
    }

    [TestMethod]
    public void PublicCallerCannotForgeAnAdmittedCandidateWithSafeLookingMetrics()
    {
        OptimizationCandidate forged = OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U4,
                DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled, streams: 1),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Measured,
                OptimizationAssessment.Excellent,
                OptimizationAssessment.Excellent,
                OptimizationAssessment.Excellent,
                contextTokens: 4096,
                predictedPeakBytes: 1,
                safeBudgetBytes: 32 * Gibibyte,
                headroomBytes: 32 * Gibibyte - 1,
                workingDiskBytes: 0,
                outputDiskBytes: 0,
                requiresPersistentChange: false,
                availableDiskBytes: 32 * Gibibyte),
            "forged-public-admission",
            isExperimental: false);

        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [forged], OptimizationPreferenceSelection.Automatic()));
    }

    [TestMethod]
    public void AdmissionProofRejectsInconsistentMemoryDiskAndPersistenceClaims()
    {
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U4,
            DeviceRouteId.Cpu, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled, 1);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "proof-validation-cap", Digest64,
                OpenVinoCapabilityPayload.Create(
                    "runtime",
                    [OpenVinoAdmittedConfiguration.Create(
                        "proof-validation", DeviceRouteId.Cpu,
                        OpenVinoWeightFormat.Int4, OpenVinoKvCacheFormat.U4,
                        OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Disabled, 1, 1, 32768,
                        SupportLevel.DeclaredSupported, false)]));
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "proof-validation-workload", 1, OptimizationAssessment.Poor,
            [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run", "mi-handoff", Digest64, Gibibyte, "hw-run", Digest64);

        void AssertRejected(
            ulong headroom, ulong working, ulong output,
            bool persistent, ulong available,
            ulong peak = 8 * Gibibyte,
            ulong budget = 32 * Gibibyte)
        {
            Assert.Throws<ArgumentException>(() =>
            {
                OptimizationCandidate candidate = OptimizationCandidate.Create(
                    configuration,
                    OptimizationCandidateMetrics.Create(
                        EvidenceGrade.Estimated,
                        OptimizationAssessment.Acceptable,
                        OptimizationAssessment.Good,
                        OptimizationAssessment.Good,
                        4096, peak, budget, headroom,
                        working, output, persistent, available),
                    "proof-validation", false);
                _ = OptimizationAdmissionProof.Create(
                        snapshot, workload, binding, candidate,
                        SupportLevel.DeclaredSupported, false,
                        new HashSet<string>());
            });
        }

        AssertRejected(23 * Gibibyte, 0, 0, false, 32 * Gibibyte);
        AssertRejected(24 * Gibibyte, 4 * Gibibyte, 8 * Gibibyte, true, 32 * Gibibyte);
        AssertRejected(24 * Gibibyte, 8 * Gibibyte, 8 * Gibibyte, true, 4 * Gibibyte);
        AssertRejected(24 * Gibibyte, 4 * Gibibyte, 4 * Gibibyte, false, 32 * Gibibyte);
        AssertRejected(24 * Gibibyte, 0, 0, true, 32 * Gibibyte);
        AssertRejected(32 * Gibibyte, 0, 0, false, 32 * Gibibyte, peak: 0);
        AssertRejected(0, 0, 0, false, 32 * Gibibyte,
            peak: 33 * Gibibyte, budget: 32 * Gibibyte);
        AssertRejected(0, 0, 0, false, 32 * Gibibyte, peak: 0, budget: 0);
        AssertRejected(24 * Gibibyte, 0, 0, false, available: 0);
    }

    [TestMethod]
    [DataRow(8UL, 4UL, 6UL)]
    [DataRow(4UL, 8UL, 6UL)]
    public void DiskUnsafeCandidateCannotReenterThroughPreferenceResolution(
        ulong workingGibibytes,
        ulong outputGibibytes,
        ulong availableGibibytes)
    {
        OptimizationCandidate diskUnsafe = OptimizationCandidate.Create(
            OpenVinoRouteConfiguration.Create(
                OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.U4,
                DeviceRouteId.Cpu,
                OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Disabled,
                streams: 1),
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Estimated,
                OptimizationAssessment.Acceptable,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes: 2 * Gibibyte,
                safeBudgetBytes: 4 * Gibibyte,
                headroomBytes: 2 * Gibibyte,
                workingDiskBytes: workingGibibytes * Gibibyte,
                outputDiskBytes: outputGibibytes * Gibibyte,
                requiresPersistentChange: true,
                availableDiskBytes: availableGibibytes * Gibibyte),
            "excluded-over-disk",
            isExperimental: false);

        Assert.IsNull(OptimizationPreferenceResolver.Resolve(
            [diskUnsafe], OptimizationPreferenceSelection.Automatic()));
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
