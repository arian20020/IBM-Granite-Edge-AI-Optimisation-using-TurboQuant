using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// One candidate with everything already computed about it.
///
/// Evaluation happens once per candidate per run, so all four modes rank the
/// same numbers against the same single fresh-memory observation. Recomputing
/// per mode would let two modes disagree about the same configuration.
/// </summary>
internal sealed record EvaluatedCandidate
{
    private EvaluatedCandidate(
        CompatibilityCandidate candidate,
        ResourceEstimate estimate,
        ResourcePeakProfile peaks,
        FitAssessment.FitAssessment fit,
        WeightQuantisation effectiveQuantisation,
        EvidenceGrade evidence,
        PerformanceIndicator performance,
        ByteCount addedStorageBytes)
    {
        Candidate = candidate;
        Estimate = estimate;
        Peaks = peaks;
        Fit = fit;
        EffectiveQuantisation = effectiveQuantisation;
        Evidence = evidence;
        Performance = performance;
        AddedStorageBytes = addedStorageBytes;
    }

    internal CompatibilityCandidate Candidate { get; }

    internal ResourceEstimate Estimate { get; }

    internal ResourcePeakProfile Peaks { get; }

    internal FitAssessment.FitAssessment Fit { get; }

    /// <summary>
    /// The encoding this configuration actually runs: its own when it converts,
    /// the imported file's when it does not.
    /// </summary>
    internal WeightQuantisation EffectiveQuantisation { get; }

    internal EvidenceGrade Evidence { get; }

    internal PerformanceIndicator Performance { get; }

    /// <summary>Disk a conversion would newly occupy. Zero when nothing is written.</summary>
    internal ByteCount AddedStorageBytes { get; }

    internal ContextTokenCount Context => Candidate.Context;

    internal CandidateFingerprint Fingerprint => Candidate.Fingerprint;

    internal bool IsExperimental => Candidate.IsExperimental;

    internal CandidatePreparation Preparation => Candidate.Preparation;

    internal static EvaluatedCandidate Create(
        CompatibilityCandidate candidate,
        ResourceEstimate estimate,
        ResourcePeakProfile peaks,
        FitAssessment.FitAssessment fit,
        WeightQuantisation effectiveQuantisation,
        EvidenceGrade evidence,
        PerformanceIndicator performance,
        ByteCount addedStorageBytes)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(peaks);
        ArgumentNullException.ThrowIfNull(fit);
        ArgumentNullException.ThrowIfNull(performance);

        if (evidence == EvidenceGrade.Unknown)
        {
            throw new ArgumentException(
                "An evaluated candidate must carry a grade; Unknown would let an "
                + "ungraded result compete against a measured one.",
                nameof(evidence));
        }

        return new EvaluatedCandidate(
            candidate,
            estimate,
            peaks,
            fit,
            effectiveQuantisation,
            evidence,
            performance,
            addedStorageBytes);
    }
}
