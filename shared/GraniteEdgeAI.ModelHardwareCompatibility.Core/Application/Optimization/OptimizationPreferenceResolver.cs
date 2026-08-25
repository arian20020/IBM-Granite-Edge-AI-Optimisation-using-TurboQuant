using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// What a preference resolved to, and the honest reason.
/// </summary>
public sealed record OptimizationSelection
{
    internal OptimizationSelection(
        OptimizationCandidate candidate,
        OptimizationPreferenceSelection preference,
        bool sharedWithAdjacentBand)
    {
        Candidate = candidate;
        Preference = preference;
        SharedWithAdjacentBand = sharedWithAdjacentBand;
    }

    public OptimizationCandidate Candidate { get; }

    public OptimizationPreferenceSelection Preference { get; }

    /// <summary>
    /// Whether a neighbouring band resolves to this same candidate.
    ///
    /// Surfaced rather than hidden. The page has to be able to say that no
    /// meaningful safer distinction exists at this position, instead of
    /// implying the slider changed something invisible.
    /// </summary>
    public bool SharedWithAdjacentBand { get; }
}

/// <summary>
/// Turns a preference into one candidate from the safe frontier.
///
/// The rule the whole design rests on: nothing here fabricates a worse or
/// unsupported configuration to make five bands produce five different answers.
/// If a machine only supports two meaningfully different safe setups, several
/// bands resolve to the same one and the result says so.
///
/// No band is a precision. Every resolution is a position on the frontier that
/// was actually generated for this model, machine, workload and route.
/// </summary>
public static class OptimizationPreferenceResolver
{
    /// <summary>
    /// Resolves against the frontier built from the admitted candidates.
    /// Returns null when nothing was admitted: no preference can select from an
    /// empty set, and inventing something to return would be the fabrication
    /// this contract forbids.
    /// </summary>
    public static OptimizationSelection? Resolve(
        IReadOnlyList<OptimizationCandidate> admitted,
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        ArgumentNullException.ThrowIfNull(preference);

        // The generator is the admission authority, but the resolver is public
        // and must still fail closed if a caller accidentally passes through a
        // candidate whose own budget-bound metrics mark it unsafe. Filtering
        // here prevents a previously excluded over-budget setup from becoming
        // selectable merely because it was reintroduced into the input list.
        IReadOnlyList<OptimizationCandidate> frontier = SafeCandidateFrontier.Create(
            [.. admitted.Where(candidate =>
                candidate.AdmissionProof is { } proof
                && proof.MatchesCandidate(candidate)
                && candidate.Metrics.FitsSafely
                && candidate.Metrics.FitsDiskSafely)]);

        if (frontier.Count == 0)
        {
            return null;
        }

        OptimizationCandidate chosen = Choose(frontier, preference);

        return new OptimizationSelection(
            chosen, preference, SharesWithNeighbour(frontier, preference, chosen));
    }

    /// <summary>
    /// A band is a position on the frontier, not a rule of its own.
    ///
    /// This is what makes the two monotonic properties structural instead of
    /// hoped-for. The frontier is ordered from least memory to most, and
    /// because dominance is taken over quality and memory, with only an
    /// equal-quality exception for avoiding persistent conversion, quality
    /// rises along it in lockstep. Mapping the five bands onto that ordering in
    /// order therefore cannot produce a band that is worse than the one before
    /// it, whatever shape the frontier happens to have.
    ///
    /// It also gives the sharing behaviour for free. A frontier with two points
    /// maps five bands onto two answers, which is the truthful outcome on a
    /// machine that supports two meaningfully different setups - rather than
    /// three invented ones to fill the slider.
    /// </summary>
    private static OptimizationCandidate Choose(
        IReadOnlyList<OptimizationCandidate> frontier,
        OptimizationPreferenceSelection preference)
    {
        if (preference.Kind == OptimizationPreferenceKind.Automatic)
        {
            return Automatic(frontier);
        }

        if (preference.Band is not { } band)
        {
            throw new ArgumentException(
                "A manual preference with no band cannot be positioned on the "
                + "frontier.",
                nameof(preference));
        }

        return frontier[PositionOf(band, frontier.Count)];
    }

    /// <summary>
    /// Where a band lands on a frontier of the given length.
    ///
    /// Proportional and rounded, so the endpoints are always the true extremes:
    /// Maximum efficiency is the least memory available and Maximum capability
    /// the highest quality, never an approximation of either.
    /// </summary>
    internal static int PositionOf(OptimizationPreferenceBand band, int frontierLength)
    {
        if (frontierLength < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frontierLength),
                frontierLength,
                "An empty frontier has no positions; the caller must report that "
                + "nothing was admitted rather than select from nothing.");
        }

        int steps = (int)OptimizationPreferenceBand.MaximumCapability
            - (int)OptimizationPreferenceBand.MaximumEfficiency;

        int offset = (int)band - (int)OptimizationPreferenceBand.MaximumEfficiency;

        return (int)Math.Round(
            offset * (frontierLength - 1) / (double)steps,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The strongest overall result, penalising a persistent conversion that
    /// buys nothing.
    ///
    /// Automatic is what a user who expressed no preference is given, so it must
    /// not quietly commit them to writing a file unless the file is meaningfully
    /// better than not writing one. It selects from the same frontier as the
    /// bands: a sixth answer nothing else could reach would be one the page
    /// could not explain.
    /// </summary>
    private static OptimizationCandidate Automatic(
        IReadOnlyList<OptimizationCandidate> frontier)
    {
        IReadOnlyList<OptimizationCandidate> considered =
            frontier.Any(candidate =>
                candidate.Metrics.Quality >= OptimizationAssessment.Acceptable)
                ? [.. frontier.Where(candidate =>
                    candidate.Metrics.Quality >= OptimizationAssessment.Acceptable)]
                : frontier;

        OptimizationCandidate best = considered[0];

        foreach (OptimizationCandidate candidate in considered)
        {
            int score = AutomaticScore(candidate);
            int incumbent = AutomaticScore(best);

            if (score > incumbent || (score == incumbent && PrefersFirst(candidate, best)))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static int AutomaticScore(OptimizationCandidate candidate)
    {
        int score = ((int)candidate.Metrics.Quality * 3)
            + (int)candidate.Metrics.Performance
            + (int)candidate.Metrics.Stability;

        // One quality band's worth of penalty. A conversion has to be clearly
        // better than the alternative, not marginally.
        return candidate.Metrics.RequiresPersistentChange ? score - 3 : score;
    }

    /// <summary>
    /// The deterministic tie-break, in the design's order: evidence, headroom,
    /// no persistent conversion, stability, workload fit, released evidence,
    /// canonical descriptor, exact evidence identity.
    ///
    /// It ends on the canonical descriptor and then the exact evidence ID so
    /// the order is total even when two evidence records admit the same
    /// configuration. Two candidates that tie on everything else still resolve
    /// the same way on every run, which is what lets the same inputs issue the
    /// same evidence-bound plan twice.
    /// </summary>
    internal static bool PrefersFirst(OptimizationCandidate a, OptimizationCandidate b)
        => Compare(a, b) < 0;

    /// <summary>One strict order shared by reduction, frontier ties, and Automatic.</summary>
    internal static int Compare(OptimizationCandidate a, OptimizationCandidate b)
    {
        if (a.Metrics.Evidence != b.Metrics.Evidence)
        {
            return ((int)b.Metrics.Evidence).CompareTo((int)a.Metrics.Evidence);
        }

        if (a.Metrics.HeadroomBytes != b.Metrics.HeadroomBytes)
        {
            return b.Metrics.HeadroomBytes.CompareTo(a.Metrics.HeadroomBytes);
        }

        if (a.Metrics.RequiresPersistentChange != b.Metrics.RequiresPersistentChange)
        {
            return a.Metrics.RequiresPersistentChange ? 1 : -1;
        }

        if (a.Metrics.Stability != b.Metrics.Stability)
        {
            return ((int)b.Metrics.Stability).CompareTo((int)a.Metrics.Stability);
        }

        if (a.Metrics.ContextTokens != b.Metrics.ContextTokens)
        {
            return b.Metrics.ContextTokens.CompareTo(a.Metrics.ContextTokens);
        }

        if (a.IsExperimental != b.IsExperimental)
        {
            return a.IsExperimental ? 1 : -1;
        }

        if (a.Metrics.Performance != b.Metrics.Performance)
        {
            return ((int)b.Metrics.Performance).CompareTo((int)a.Metrics.Performance);
        }

        if (a.Metrics.Quality != b.Metrics.Quality)
        {
            return ((int)b.Metrics.Quality).CompareTo((int)a.Metrics.Quality);
        }

        if (a.Metrics.PredictedPeakBytes != b.Metrics.PredictedPeakBytes)
        {
            return a.Metrics.PredictedPeakBytes.CompareTo(b.Metrics.PredictedPeakBytes);
        }

        if (a.Metrics.SafeBudgetBytes != b.Metrics.SafeBudgetBytes)
        {
            return b.Metrics.SafeBudgetBytes.CompareTo(a.Metrics.SafeBudgetBytes);
        }

        if (a.Metrics.WorkingDiskBytes != b.Metrics.WorkingDiskBytes)
        {
            return a.Metrics.WorkingDiskBytes.CompareTo(b.Metrics.WorkingDiskBytes);
        }

        if (a.Metrics.OutputDiskBytes != b.Metrics.OutputDiskBytes)
        {
            return a.Metrics.OutputDiskBytes.CompareTo(b.Metrics.OutputDiskBytes);
        }

        if (a.Metrics.AvailableDiskBytes != b.Metrics.AvailableDiskBytes)
        {
            return Nullable.Compare(b.Metrics.AvailableDiskBytes, a.Metrics.AvailableDiskBytes);
        }

        int canonical = string.CompareOrdinal(
            a.CanonicalDescriptor, b.CanonicalDescriptor);

        if (canonical != 0)
        {
            return canonical;
        }

        int evidence = string.CompareOrdinal(a.EvidenceId, b.EvidenceId);
        if (evidence != 0)
        {
            return evidence;
        }

        int provenance = ((int)a.ConversionProvenance)
            .CompareTo((int)b.ConversionProvenance);
        if (provenance != 0)
        {
            return provenance;
        }

        int notice = ((int)a.Notice).CompareTo((int)b.Notice);
        if (notice != 0)
        {
            return notice;
        }

        int authority = CompareAdmissionAuthority(a.AdmissionProof, b.AdmissionProof);
        if (authority != 0)
        {
            return authority;
        }

        return CompareNormalizationProof(
            a.WeightNormalizationProof, b.WeightNormalizationProof);
    }

    private static int CompareAdmissionAuthority(
        OptimizationAdmissionProof? a,
        OptimizationAdmissionProof? b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a is null || b is null)
        {
            return a is null ? 1 : -1;
        }

        int result = string.CompareOrdinal(a.SnapshotId, b.SnapshotId);
        if (result == 0)
        {
            result = string.CompareOrdinal(
                a.CapabilitySnapshotSha256, b.CapabilitySnapshotSha256);
        }
        if (result == 0)
        {
            result = string.CompareOrdinal(a.WorkloadId, b.WorkloadId);
        }
        if (result == 0)
        {
            result = string.CompareOrdinal(a.WorkloadSha256, b.WorkloadSha256);
        }
        if (result == 0)
        {
            result = string.CompareOrdinal(a.JourneySha256, b.JourneySha256);
        }
        if (result == 0)
        {
            result = ((int)a.SupportLevel).CompareTo((int)b.SupportLevel);
        }
        if (result == 0)
        {
            result = a.RequiresEvidence.CompareTo(b.RequiresEvidence);
        }
        if (result == 0)
        {
            result = string.CompareOrdinal(
                a.OptedInEvidenceId ?? string.Empty,
                b.OptedInEvidenceId ?? string.Empty);
        }
        return result;
    }

    private static int CompareNormalizationProof(
        GgufWeightNormalizationProof? a,
        GgufWeightNormalizationProof? b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a is null || b is null)
        {
            return a is null ? 1 : -1;
        }

        int result = a.FileType.CompareTo(b.FileType);
        if (result == 0)
        {
            result = a.QuantisationVersion.CompareTo(b.QuantisationVersion);
        }
        if (result == 0)
        {
            result = ((int)a.Source).CompareTo((int)b.Source);
        }
        if (result == 0)
        {
            result = ((int)a.AdmittedWeight).CompareTo((int)b.AdmittedWeight);
        }
        return result;
    }

    private static bool SharesWithNeighbour(
        IReadOnlyList<OptimizationCandidate> frontier,
        OptimizationPreferenceSelection preference,
        OptimizationCandidate chosen)
    {
        if (preference.Band is not { } band)
        {
            return false;
        }

        foreach (int step in new[] { -1, 1 })
        {
            int neighbour = (int)band + step;

            if (neighbour is < (int)OptimizationPreferenceBand.MaximumEfficiency
                or > (int)OptimizationPreferenceBand.MaximumCapability)
            {
                continue;
            }

            OptimizationCandidate other = Choose(
                frontier,
                OptimizationPreferenceSelection.Manual(
                    RepresentativeValue((OptimizationPreferenceBand)neighbour)));

            if (other.CanonicalDescriptor == chosen.CanonicalDescriptor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A slider position inside each band. Any position in a band resolves
    /// identically, so the midpoint stands for the whole band.
    /// </summary>
    internal static int RepresentativeValue(OptimizationPreferenceBand band) => band switch
    {
        OptimizationPreferenceBand.MaximumEfficiency => 10,
        OptimizationPreferenceBand.Efficient => 30,
        OptimizationPreferenceBand.Balanced => 50,
        OptimizationPreferenceBand.HighCapability => 70,
        OptimizationPreferenceBand.MaximumCapability => 90,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unnamed band.")
    };
}
