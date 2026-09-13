using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Section 11's hard gates.
///
/// A candidate that fails one of these is not ranked worse — it is not ranked at
/// all. Letting a does-not-fit candidate compete and lose would mean it could win
/// whenever nothing better existed, which is exactly the false-safe result the
/// whole design is built to avoid.
/// </summary>
internal static class ModeAdmission
{
    internal static bool IsAdmitted(
        EvaluatedCandidate candidate,
        bool entryRequiresEvidence,
        out ModeAdmissionReason reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // Checked first because an unestablished estimate also produces a
        // NotEstablished fit state, and the estimate is the more specific answer.
        if (candidate.Estimate.Status != EstimationStatus.Established)
        {
            reason = ModeAdmissionReason.EstimateNotEstablished;
            return false;
        }

        if (candidate.Fit.State is not (CompatibilityFitState.Safe
            or CompatibilityFitState.Narrow))
        {
            reason = ModeAdmissionReason.FitStateNotSafeOrNarrow;
            return false;
        }

        // A declared threshold with nothing to test it against is not a pass.
        if (entryRequiresEvidence && candidate.Evidence < EvidenceGrade.Measured)
        {
            reason = ModeAdmissionReason.EvidenceBelowAdmissionLevel;
            return false;
        }

        reason = ModeAdmissionReason.None;
        return true;
    }
}
