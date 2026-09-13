using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// The candidates worth offering, ordered from efficiency to capability.
///
/// A candidate is dominated when another is at least as good on every axis and
/// better on one. Offering a dominated candidate means offering a setup that
/// costs more and delivers less than something already on the list, which no
/// preference could honestly resolve to.
///
/// The ordering is total and deterministic. A frontier that ordered differently
/// between two identical runs would let the same inputs produce two different
/// plans, and the plan is the thing a user confirms.
/// </summary>
internal static class SafeCandidateFrontier
{
    /// <summary>
    /// Builds the frontier. Input candidates are assumed already admitted: hard
    /// constraints are the generator's job, and re-checking them here would put
    /// the safety rule in two places that can disagree.
    /// </summary>
    internal static IReadOnlyList<OptimizationCandidate> Create(
        IReadOnlyList<OptimizationCandidate> admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        List<OptimizationCandidate> unique = [];
        foreach (OptimizationCandidate candidate in admitted)
        {
            if (!unique.Any(existing =>
                    OptimizationPreferenceResolver.Compare(existing, candidate) == 0))
            {
                unique.Add(candidate);
            }
        }

        List<OptimizationCandidate> frontier = [];

        foreach (OptimizationCandidate candidate in unique)
        {
            if (!unique.Any(other => Dominates(other, candidate)))
            {
                frontier.Add(candidate);
            }
        }

        // Efficiency first, capability last: the same direction the preference
        // bands run, because the monotonic properties compare the two orderings
        // against each other.
        return
        [
            .. frontier
                .OrderBy(candidate => candidate.Metrics.PredictedPeakBytes)
                .ThenBy(candidate => candidate.QualityScore)
                .ThenBy(candidate => candidate.CanonicalDescriptor, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// Whether <paramref name="challenger"/> beats <paramref name="candidate"/>
    /// on the two axes the slider actually trades.
    ///
    /// Quality against memory, plus the one non-runtime cost that Automatic is
    /// required to avoid when it buys no quality: persistent conversion. A
    /// converting challenger cannot dominate a fitting as-is candidate of the
    /// same quality solely by using less memory. Both remain a deterministic,
    /// explicitly non-dominated trade-off. Because this exception applies only
    /// at equal quality, quality still cannot fall as memory rises along the
    /// frontier.
    ///
    /// The richer axes are not discarded. They break ties between candidates
    /// that are equal on both of these, which is where the design puts them.
    /// </summary>
    private static bool Dominates(
        OptimizationCandidate challenger, OptimizationCandidate candidate)
    {
        if (ReferenceEquals(challenger, candidate))
        {
            return false;
        }

        OptimizationCandidateMetrics a = challenger.Metrics;
        OptimizationCandidateMetrics b = candidate.Metrics;

        if (challenger.QualityScore == candidate.QualityScore
            && a.RequiresPersistentChange
            && !b.RequiresPersistentChange)
        {
            return false;
        }

        bool atLeastAsGood =
            a.PredictedPeakBytes <= b.PredictedPeakBytes
            && challenger.QualityScore >= candidate.QualityScore;

        bool betterSomewhere =
            a.PredictedPeakBytes < b.PredictedPeakBytes
            || challenger.QualityScore > candidate.QualityScore;

        if (atLeastAsGood && betterSomewhere)
        {
            return true;
        }

        // Equal measured trade-offs do not make two different OpenVINO weight
        // encodings interchangeable. INT4 and MXFP4 select different sealed
        // converter/runtime paths and stage 4 explicitly exposes that choice.
        // Keep both without inventing a memory or quality delta; the stable
        // ordering below still makes every slider position deterministic.
        if (a.PredictedPeakBytes == b.PredictedPeakBytes
            && challenger.QualityScore == candidate.QualityScore
            && challenger.Configuration is OpenVinoRouteConfiguration left
            && candidate.Configuration is OpenVinoRouteConfiguration right
            && left.Weights != right.Weights)
        {
            return false;
        }

        // Equal on both axes the slider trades. One of them still has to go, or
        // the frontier would offer the user the same tradeoff twice; the
        // deterministic tie-break decides which, so the outcome does not depend
        // on the order candidates arrived in.
        return a.PredictedPeakBytes == b.PredictedPeakBytes
            && challenger.QualityScore == candidate.QualityScore
            && OptimizationPreferenceResolver.PrefersFirst(challenger, candidate);
    }
}
