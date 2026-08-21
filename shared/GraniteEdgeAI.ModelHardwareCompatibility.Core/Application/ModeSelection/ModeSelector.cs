using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Everything mode selection needs, gathered explicitly so the selector performs
/// no lookup, no I/O and no clock read.
///
/// Constructed only through <see cref="Create"/>, like every other type in this
/// library: a positional record's implicit constructor would let a null
/// <see cref="Candidates"/>, <see cref="EvidenceRequiringEntryIds"/> or
/// <see cref="BaselineConfiguration"/> reach the selector as an unhandled
/// null-reference exception rather than a typed refusal, and would let a caller
/// mutate <see cref="Candidates"/> after construction by holding onto the list
/// it passed in.
/// </summary>
internal sealed record ModeSelectionRequest
{
    private ModeSelectionRequest(
        IReadOnlyList<EvaluatedCandidate> candidates,
        IReadOnlySet<string> evidenceRequiringEntryIds,
        ContextTokenCount preservationTarget,
        GgufRouteConfiguration baselineConfiguration)
    {
        Candidates = candidates;
        EvidenceRequiringEntryIds = evidenceRequiringEntryIds;
        PreservationTarget = preservationTarget;
        BaselineConfiguration = baselineConfiguration;
    }

    internal IReadOnlyList<EvaluatedCandidate> Candidates { get; }

    internal IReadOnlySet<string> EvidenceRequiringEntryIds { get; }

    internal ContextTokenCount PreservationTarget { get; }

    internal GgufRouteConfiguration BaselineConfiguration { get; }

    internal static ModeSelectionRequest Create(
        IReadOnlyList<EvaluatedCandidate> candidates,
        IReadOnlySet<string> evidenceRequiringEntryIds,
        ContextTokenCount preservationTarget,
        GgufRouteConfiguration baselineConfiguration)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(evidenceRequiringEntryIds);
        ArgumentNullException.ThrowIfNull(baselineConfiguration);

        return new ModeSelectionRequest(
            [.. candidates],
            evidenceRequiringEntryIds,
            preservationTarget,
            baselineConfiguration);
    }
}

/// <summary>
/// All four modes plus the separate "use current model" action.
/// </summary>
internal sealed record ModeSelectionOutcome(
    IReadOnlyList<CompatibilityModeSelection> Selections,
    bool UseCurrentModelAvailable);

/// <summary>
/// Resolves the four modes over one evaluated set.
///
/// All four rank the same evaluated candidates, so two modes can never disagree
/// about the same configuration's memory or fit. A mode with nothing to pick is
/// reported unavailable with the reason its candidates failed on, never omitted:
/// a missing option looks like an option that never existed.
/// </summary>
internal static class ModeSelector
{
    private static readonly CompatibilityMode[] Modes =
    [
        CompatibilityMode.Automatic,
        CompatibilityMode.Quality,
        CompatibilityMode.Balanced,
        CompatibilityMode.Efficiency
    ];

    /// <summary>
    /// Most fundamental first. A user with insufficient memory must always be
    /// told about memory, never about a downstream evidence or backend detail
    /// that only applied to some other candidate in the set - so the reason
    /// reported for a mode with nothing admitted is the highest-precedence
    /// refusal present, not whichever one the candidate list happened to name
    /// first.
    /// </summary>
    private static readonly ModeAdmissionReason[] RefusalPrecedence =
    [
        ModeAdmissionReason.EstimateNotEstablished,
        ModeAdmissionReason.FitStateNotSafeOrNarrow,
        ModeAdmissionReason.EvidenceBelowAdmissionLevel,
        ModeAdmissionReason.BackendMismatch
    ];

    /// <summary>
    /// Test-only window onto <see cref="RefusalPrecedence"/>, so an
    /// exhaustiveness sweep can catch a future <see cref="ModeAdmissionReason"/>
    /// member added without a precedence entry, without widening the array's
    /// own visibility.
    /// </summary>
    internal static IReadOnlyList<ModeAdmissionReason> RefusalPrecedenceForTests => RefusalPrecedence;

    internal static ModeSelectionOutcome SelectAll(ModeSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<EvaluatedCandidate> admitted = [];
        HashSet<ModeAdmissionReason> refusals = [];

        foreach (EvaluatedCandidate candidate in request.Candidates)
        {
            bool requiresEvidence = request.EvidenceRequiringEntryIds.Contains(
                candidate.Candidate.SupportEntryId);

            if (ModeAdmission.IsAdmitted(candidate, requiresEvidence, out ModeAdmissionReason reason))
            {
                admitted.Add(candidate);
                continue;
            }

            refusals.Add(reason);
        }

        // A deterministic, most-fundamental-first reason, so the explanation
        // shown to a user does not depend on candidate order.
        ModeAdmissionReason stableRefusal = RefusalPrecedence.FirstOrDefault(
            refusals.Contains, ModeAdmissionReason.None);

        // Every refusal actually produced today is one of the four entries in
        // RefusalPrecedence (EnumExhaustivenessTests pins this). If a refusal
        // reached here that the array does not name, falling through silently
        // would hand ModeAdmissionReason.None to Unavailable below, which
        // throws an ArgumentException that talks about naming a reason rather
        // than about the precedence array missing an entry. Failing loudly
        // here instead - with a message that points at the actual gap - never
        // fires for any input reachable today, since refusals is always a
        // subset of RefusalPrecedence's members.
        if (refusals.Count > 0 && stableRefusal == ModeAdmissionReason.None)
        {
            throw new InvalidOperationException(
                $"A refusal reason ({string.Join(", ", refusals)}) was produced that "
                + $"{nameof(RefusalPrecedence)} does not name. Add it to the precedence "
                + "order in ModeSelector so a stable reason can still be reported.");
        }

        WeightQuantisation? bestAdmittedTier = admitted.Count == 0
            ? null
            : admitted
                .Select(candidate => candidate.EffectiveQuantisation)
                .Where(quantisation => quantisation != WeightQuantisation.Unknown)
                .Cast<WeightQuantisation?>()
                .DefaultIfEmpty(null)
                .Min();

        List<CompatibilityModeSelection> selections = [];

        foreach (CompatibilityMode mode in Modes)
        {
            if (admitted.Count == 0)
            {
                // Nothing assessed at all is a different answer from everything
                // assessed and rejected, so it carries no reason.
                selections.Add(request.Candidates.Count == 0
                    ? CompatibilityModeSelection.NotEstablished(mode)
                    : CompatibilityModeSelection.Unavailable(mode, stableRefusal));

                continue;
            }

            IComparer<EvaluatedCandidate> comparer = ModeComparers.For(
                mode, request.PreservationTarget, request.BaselineConfiguration, bestAdmittedTier);

            EvaluatedCandidate winner = admitted[0];

            foreach (EvaluatedCandidate contender in admitted.Skip(1))
            {
                if (comparer.Compare(contender, winner) < 0)
                {
                    winner = contender;
                }
            }

            selections.Add(CompatibilityModeSelection.Available(
                mode, winner.Fingerprint, ModeComparers.FactorsFor(mode)));
        }

        // Separate from the four modes: it creates a runtime profile, not an
        // artifact, and it stands whenever what the user already has is safe.
        bool useCurrentModel = request.Candidates.Any(candidate =>
            candidate.Candidate.IsBaseline
            && candidate.Fit.State is CompatibilityFitState.Safe
                or CompatibilityFitState.Narrow);

        return new ModeSelectionOutcome(selections, useCurrentModel);
    }
}
