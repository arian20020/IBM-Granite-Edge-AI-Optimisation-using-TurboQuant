using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Everything mode selection needs, gathered explicitly so the selector performs
/// no lookup, no I/O and no clock read.
/// </summary>
internal sealed record ModeSelectionRequest(
    IReadOnlyList<EvaluatedCandidate> Candidates,
    IReadOnlySet<string> EvidenceRequiringEntryIds,
    ContextTokenCount PreservationTarget,
    GgufRouteConfiguration BaselineConfiguration);

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

    internal static ModeSelectionOutcome SelectAll(ModeSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<EvaluatedCandidate> admitted = [];
        ModeAdmissionReason firstRefusal = ModeAdmissionReason.None;

        foreach (EvaluatedCandidate candidate in request.Candidates)
        {
            bool requiresEvidence = request.EvidenceRequiringEntryIds.Contains(
                candidate.Candidate.SupportEntryId);

            if (ModeAdmission.IsAdmitted(candidate, requiresEvidence, out ModeAdmissionReason reason))
            {
                admitted.Add(candidate);
                continue;
            }

            // The first refusal explains a mode that ends up with nothing.
            if (firstRefusal == ModeAdmissionReason.None)
            {
                firstRefusal = reason;
            }
        }

        List<CompatibilityModeSelection> selections = [];

        foreach (CompatibilityMode mode in Modes)
        {
            if (admitted.Count == 0)
            {
                // Nothing assessed at all is a different answer from everything
                // assessed and rejected, so it carries no reason.
                selections.Add(request.Candidates.Count == 0
                    ? CompatibilityModeSelection.NotEstablished(mode)
                    : CompatibilityModeSelection.Unavailable(mode, firstRefusal));

                continue;
            }

            IComparer<EvaluatedCandidate> comparer = ModeComparers.For(
                mode, request.PreservationTarget, request.BaselineConfiguration);

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
