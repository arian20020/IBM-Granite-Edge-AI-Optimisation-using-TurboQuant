using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// The four lexicographic orderings from section 11.
///
/// Each is a list of comparison steps applied in order: the first step that
/// separates two candidates decides, and no later step can outvote an earlier
/// one. That is deliberately not a weighted score — a score lets a large win on
/// a minor axis overturn a small loss on the axis the user actually chose.
///
/// Every ordering ends with a fingerprint tiebreak, which makes it a total order
/// and therefore independent of the order candidates arrive in.
///
/// Convention: less is better. Compare(a, b) &lt; 0 means a wins.
/// </summary>
internal static class ModeComparers
{
    internal static IReadOnlyList<SelectionFactor> FactorsFor(CompatibilityMode mode) =>
        mode switch
        {
            CompatibilityMode.Quality =>
            [
                SelectionFactor.PreservesRequestedContext,
                SelectionFactor.QualityTier,
                SelectionFactor.EvidenceGrade,
                SelectionFactor.NonExperimental,
                SelectionFactor.LeastDestructivePreparation,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.Headroom,
                SelectionFactor.Fingerprint
            ],
            CompatibilityMode.Efficiency =>
            [
                SelectionFactor.WorstPoolPressureRatio,
                SelectionFactor.AddedStorage,
                SelectionFactor.MaximiseContext,
                SelectionFactor.QualityTier,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.NonExperimental,
                SelectionFactor.Fingerprint
            ],
            CompatibilityMode.Balanced =>
            [
                SelectionFactor.PreservesRequestedContext,
                SelectionFactor.QualityTier,
                SelectionFactor.Headroom,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.LeastDestructivePreparation,
                SelectionFactor.Fingerprint
            ],
            CompatibilityMode.Automatic =>
            [
                SelectionFactor.PreservesRequestedContext,
                SelectionFactor.NonExperimental,
                SelectionFactor.LeastDestructivePreparation,
                SelectionFactor.DistanceFromImportedConfiguration,
                SelectionFactor.QualityTier,
                SelectionFactor.EvidenceGrade,
                SelectionFactor.Headroom,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.Fingerprint
            ],
            _ => throw new ArgumentException(
                "A comparison must name its mode.", nameof(mode))
        };

    internal static IComparer<EvaluatedCandidate> For(
        CompatibilityMode mode,
        ContextTokenCount preservationTarget,
        GgufRouteConfiguration baselineConfiguration)
    {
        ArgumentNullException.ThrowIfNull(baselineConfiguration);

        // Validates the mode and gives the comparer its ordering.
        IReadOnlyList<SelectionFactor> factors = FactorsFor(mode);

        return new FactorComparer(factors, preservationTarget, baselineConfiguration);
    }

    private sealed class FactorComparer(
        IReadOnlyList<SelectionFactor> factors,
        ContextTokenCount preservationTarget,
        GgufRouteConfiguration baselineConfiguration)
        : IComparer<EvaluatedCandidate>
    {
        public int Compare(EvaluatedCandidate? left, EvaluatedCandidate? right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            foreach (SelectionFactor factor in factors)
            {
                int verdict = Apply(factor, left, right);

                if (verdict != 0)
                {
                    return verdict;
                }
            }

            return 0;
        }

        private int Apply(SelectionFactor factor, EvaluatedCandidate a, EvaluatedCandidate b) =>
            factor switch
            {
                // Booleans: true is better, so the true case sorts first.
                SelectionFactor.PreservesRequestedContext =>
                    Flag(a.Context == preservationTarget, b.Context == preservationTarget),

                SelectionFactor.NonExperimental =>
                    Flag(!a.IsExperimental, !b.IsExperimental),

                // Higher is better.
                SelectionFactor.QualityTier =>
                    Bits(b).CompareTo(Bits(a)),

                SelectionFactor.EvidenceGrade =>
                    b.Evidence.CompareTo(a.Evidence),

                SelectionFactor.Headroom =>
                    b.Fit.Headroom.CompareTo(a.Fit.Headroom),

                SelectionFactor.MaximiseContext =>
                    b.Context.Tokens.CompareTo(a.Context.Tokens),

                // Lower is better.
                SelectionFactor.LeastDestructivePreparation =>
                    Destructiveness(a).CompareTo(Destructiveness(b)),

                SelectionFactor.WorstPoolPressureRatio =>
                    WorstPoolRatio(a).CompareTo(WorstPoolRatio(b)),

                SelectionFactor.AddedStorage =>
                    a.AddedStorageBytes.CompareTo(b.AddedStorageBytes),

                SelectionFactor.DistanceFromImportedConfiguration =>
                    Distance(a).CompareTo(Distance(b)),

                // Nothing measures throughput yet, so this separates nothing.
                SelectionFactor.PerformanceNotEstablished => 0,

                SelectionFactor.Fingerprint => string.CompareOrdinal(
                    a.Fingerprint.Value, b.Fingerprint.Value),

                _ => 0
            };

        private static int Flag(bool a, bool b) => a == b ? 0 : a ? -1 : 1;

        private static decimal Bits(EvaluatedCandidate candidate) =>
            candidate.EffectiveQuantisation == WeightQuantisation.Unknown
                ? 0m
                : WeightQuantisationMap.BitsPerWeight(candidate.EffectiveQuantisation);

        private static int Destructiveness(EvaluatedCandidate candidate) =>
            candidate.Preparation switch
            {
                CandidatePreparation.None => 0,
                CandidatePreparation.RuntimeProfileOnly => 1,
                CandidatePreparation.WeightConversionRequired => 2,
                _ => 3
            };

        /// <summary>
        /// The worst pool controls. FitPolicy gates system memory alone today, so
        /// that is the only ratio; written over a collection so the device and
        /// storage gates slot in without reshaping the ordering.
        /// </summary>
        private static decimal WorstPoolRatio(EvaluatedCandidate candidate)
        {
            decimal[] ratios = [candidate.Fit.PressureRatio];

            return ratios.Max();
        }

        private int Distance(EvaluatedCandidate candidate)
        {
            if (candidate.Candidate.Configuration is not GgufRouteConfiguration configuration)
            {
                return int.MaxValue;
            }

            int distance = 0;

            if (configuration.Weights != baselineConfiguration.Weights) { distance++; }
            if (configuration.KvCache != baselineConfiguration.KvCache) { distance++; }
            if (configuration.Backend != baselineConfiguration.Backend) { distance++; }
            if (configuration.Device != baselineConfiguration.Device) { distance++; }
            if (configuration.Offload != baselineConfiguration.Offload) { distance++; }

            return distance;
        }
    }
}
