using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

internal sealed record GgufWeightNormalizationProof
{
    private GgufWeightNormalizationProof(
        int fileType,
        int quantisationVersion,
        WeightQuantisation source,
        GgufWeightFormat admittedWeight)
    {
        FileType = fileType;
        QuantisationVersion = quantisationVersion;
        Source = source;
        AdmittedWeight = admittedWeight;
    }

    internal int FileType { get; }

    internal int QuantisationVersion { get; }

    internal WeightQuantisation Source { get; }

    internal GgufWeightFormat AdmittedWeight { get; }

    internal static GgufWeightNormalizationProof FromInspection(
        int? fileType,
        int? quantisationVersion,
        GgufWeightFormat admittedWeight)
    {
        WeightQuantisation source = WeightQuantisationMap.FromGgufFileType(
            fileType, quantisationVersion);
        if (fileType is not { } exactFileType
            || quantisationVersion is not { } exactVersion
            || admittedWeight == GgufWeightFormat.Imported
            || source != GgufWeightFormatMap.ToCanonical(admittedWeight))
        {
            throw new ArgumentException(
                "A normalization proof requires exact inspected GGUF metadata "
                + "matching the admitted named weight format.",
                nameof(admittedWeight));
        }

        return new GgufWeightNormalizationProof(
            exactFileType, exactVersion, source, admittedWeight);
    }
}

/// <summary>
/// How good a candidate is on one axis, in terms both routes can be scored on.
///
/// Coarse on purpose. A finer scale would imply a precision the evidence does
/// not support and would make two candidates look separable when nothing
/// distinguishes them, which is how a slider band ends up inventing a
/// difference to justify itself.
/// </summary>
public enum OptimizationAssessment
{
    /// <summary>Never planned against. Unknown fails closed.</summary>
    Unknown = 0,
    Poor = 1,
    Acceptable = 2,
    Good = 3,
    Excellent = 4
}

/// <summary>
/// Why a candidate could not be offered.
///
/// A typed exclusion rather than a silent absence: a configuration that
/// vanished without explanation looks like one that was never possible, and the
/// user cannot tell which of those they are looking at.
/// </summary>
public enum OptimizationExclusionReason
{
    None = 0,
    EstimateNotEstablished,
    ExceedsSafeMemoryBudget,
    InsufficientDiskSpace,
    ContextBelowWorkloadMinimum,
    EvidenceBelowAdmissionLevel,
    ExperimentalNotAdmitted,
    QualityBelowFloor,
    Dominated,
    RequantisationNotAuthorized
}

/// <summary>A closed presentation notice derived from admitted candidate facts.</summary>
public enum OptimizationCandidateNotice
{
    None = 0,
    LowQuality,
    Requantisation,
    LowQualityRequantisation
}

public enum OptimizationConversionProvenance
{
    None = 0,
    HigherPrecisionSource,
    ControlledRequantisation
}

/// <summary>
/// The comparable part of a candidate: what it costs and how good it is.
///
/// This is what the shared layer is allowed to reason about. Everything
/// route-specific stays in the sealed configuration, so the planner can rank a
/// GGUF setup against an OpenVINO one without either route's vocabulary leaking
/// into the comparison.
///
/// Every field is required. There is no partially-populated metrics record,
/// because a zero in a memory field and an unmeasured memory field would then
/// be indistinguishable, and one of those is safe to compare while the other
/// is not.
/// </summary>
public sealed record OptimizationCandidateMetrics
{
    private OptimizationCandidateMetrics(
        EvidenceGrade evidence,
        OptimizationAssessment quality,
        OptimizationAssessment performance,
        OptimizationAssessment stability,
        int contextTokens,
        ulong predictedPeakBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        ulong workingDiskBytes,
        ulong outputDiskBytes,
        bool requiresPersistentChange,
        ulong? availableDiskBytes)
    {
        Evidence = evidence;
        Quality = quality;
        Performance = performance;
        Stability = stability;
        ContextTokens = contextTokens;
        PredictedPeakBytes = predictedPeakBytes;
        SafeBudgetBytes = safeBudgetBytes;
        HeadroomBytes = headroomBytes;
        WorkingDiskBytes = workingDiskBytes;
        OutputDiskBytes = outputDiskBytes;
        RequiresPersistentChange = requiresPersistentChange;
        AvailableDiskBytes = availableDiskBytes;
    }

    public EvidenceGrade Evidence { get; }

    public OptimizationAssessment Quality { get; }

    public OptimizationAssessment Performance { get; }

    public OptimizationAssessment Stability { get; }

    public int ContextTokens { get; }

    /// <summary>Peak memory pressure, excluding the reserve held back from it.</summary>
    public ulong PredictedPeakBytes { get; }

    public ulong SafeBudgetBytes { get; }

    /// <summary>Budget left over. Zero when the candidate does not fit.</summary>
    public ulong HeadroomBytes { get; }

    /// <summary>
    /// Composite peak storage obligation during any working lifecycle phase.
    /// It may already include the final output, so it is never added to
    /// <see cref="OutputDiskBytes"/> when determining the disk obligation.
    /// </summary>
    public ulong WorkingDiskBytes { get; }

    /// <summary>Space the finished output occupies. Zero for runtime-only work.</summary>
    public ulong OutputDiskBytes { get; }

    /// <summary>
    /// Exact free-disk observation used to admit this candidate. Null exists
    /// only for the frozen version-two vocabulary; new resolution fails closed
    /// when no disk admission proof is present.
    /// </summary>
    public ulong? AvailableDiskBytes { get; }

    /// <summary>The peak disk obligation without double-counting overlapping phases.</summary>
    public ulong DiskObligationBytes => Math.Max(WorkingDiskBytes, OutputDiskBytes);

    /// <summary>
    /// Whether choosing this writes a new model or package.
    ///
    /// The one fact the confirmation surface must get right: it is the
    /// difference between changing a setting and creating a file, and the user
    /// is agreeing to different things in each case.
    /// </summary>
    public bool RequiresPersistentChange { get; }

    public static OptimizationCandidateMetrics Create(
        EvidenceGrade evidence,
        OptimizationAssessment quality,
        OptimizationAssessment performance,
        OptimizationAssessment stability,
        int contextTokens,
        ulong predictedPeakBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        ulong workingDiskBytes,
        ulong outputDiskBytes,
        bool requiresPersistentChange,
        ulong? availableDiskBytes = null)
    {
        if (evidence == EvidenceGrade.Unknown)
        {
            throw new ArgumentException(
                "Evidence Unknown fails closed: an ungraded candidate must not "
                + "compete against a graded one.",
                nameof(evidence));
        }

        RequireAssessed(quality, nameof(quality));
        RequireAssessed(performance, nameof(performance));
        RequireAssessed(stability, nameof(stability));

        if (contextTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextTokens),
                contextTokens,
                "A candidate that can read nothing is not a candidate.");
        }

        if (predictedPeakBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(predictedPeakBytes),
                predictedPeakBytes,
                "A peak of zero reads as a configuration that costs nothing, which "
                + "is the shape an unestimated candidate would take.");
        }

        return new OptimizationCandidateMetrics(
            evidence,
            quality,
            performance,
            stability,
            contextTokens,
            predictedPeakBytes,
            safeBudgetBytes,
            headroomBytes,
            workingDiskBytes,
            outputDiskBytes,
            requiresPersistentChange,
            availableDiskBytes);
    }

    /// <summary>Whether the peak fits inside what this candidate was allowed.</summary>
    public bool FitsSafely => PredictedPeakBytes <= SafeBudgetBytes;

    /// <summary>
    /// Whether a positive, explicit disk observation covers the peak disk
    /// obligation. Working and output figures are alternative phase peaks, not
    /// additive reservations.
    /// </summary>
    public bool FitsDiskSafely => AvailableDiskBytes is > 0
        && DiskObligationBytes <= AvailableDiskBytes.Value;

    private static void RequireAssessed(OptimizationAssessment value, string parameter)
    {
        if (value == OptimizationAssessment.Unknown)
        {
            throw new ArgumentException(
                "An unassessed axis fails closed. Ranking against it would treat "
                + "'not established' as a score.",
                parameter);
        }
    }
}

/// <summary>
/// One complete thing the user could be given.
///
/// Not a weight precision. It carries the whole configuration - route, device,
/// representation, cache, context and route controls - because a preference
/// selects a setup rather than a number, and the page has to be able to show
/// what was actually chosen.
///
/// The configuration is the sealed route record. Shared code compares
/// <see cref="Metrics"/>; an executor pattern-matches the configuration to its
/// own type and refuses anything else.
/// </summary>
public sealed record OptimizationCandidate
{
    private OptimizationCandidate(
        OptimizationRoute route,
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental,
        OptimizationConversionProvenance conversionProvenance,
        OptimizationCandidateNotice notice,
        GgufWeightNormalizationProof? weightNormalizationProof,
        OptimizationAdmissionProof? admissionProof)
    {
        Route = route;
        Configuration = configuration;
        Metrics = metrics;
        EvidenceId = evidenceId;
        IsExperimental = isExperimental;
        ConversionProvenance = conversionProvenance;
        Notice = notice;
        WeightNormalizationProof = weightNormalizationProof;
        AdmissionProof = admissionProof;
    }

    public OptimizationRoute Route { get; }

    /// <summary>
    /// The sealed route configuration. Exactly one concrete type per route, and
    /// the route discriminator always agrees with it.
    /// </summary>
    public RouteConfiguration Configuration { get; }

    public OptimizationCandidateMetrics Metrics { get; }

    /// <summary>The capability record that admitted this combination.</summary>
    public string EvidenceId { get; }

    public bool IsExperimental { get; }

    public OptimizationConversionProvenance ConversionProvenance { get; }

    public OptimizationCandidateNotice Notice { get; }

    internal GgufWeightNormalizationProof? WeightNormalizationProof { get; }

    internal OptimizationAdmissionProof? AdmissionProof { get; }

    /// <summary>
    /// Ordinal, stable, and covering the whole candidate rather than the
    /// configuration alone. Context is folded in here because two candidates
    /// differing only in context length are different candidates, and the route
    /// descriptor deliberately does not carry it.
    /// </summary>
    public string CanonicalDescriptor =>
        $"{Configuration.CanonicalDescriptor}|ctx={Metrics.ContextTokens}";

    public static OptimizationCandidate Create(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental,
        OptimizationConversionProvenance conversionProvenance =
            OptimizationConversionProvenance.None)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(metrics);

        OptimizationIdentifier.Require(
            evidenceId, nameof(evidenceId), "The evidence behind a candidate");

        if (!Enum.IsDefined(conversionProvenance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(conversionProvenance), conversionProvenance,
                "An undefined conversion provenance cannot be planned.");
        }

        if (configuration is GgufRouteConfiguration
                { Weights: GgufWeightFormat.Q2K }
            && metrics.Quality != OptimizationAssessment.Poor)
        {
            throw new ArgumentException(
                "A Q2_K candidate is always Poor on the coarse quality scale. "
                + "Caller-supplied metrics cannot upgrade the product floor.",
                nameof(metrics));
        }

        if (conversionProvenance != OptimizationConversionProvenance.None
            && !metrics.RequiresPersistentChange)
        {
            throw new ArgumentException(
                "Conversion provenance requires a persistent new artifact.",
                nameof(conversionProvenance));
        }

        OptimizationCandidateNotice notice = ExpectedNotice(
            configuration, metrics, conversionProvenance);

        // The discriminator is derived from the configuration rather than
        // supplied alongside it, so the two can never disagree and no caller
        // can label a GGUF configuration as an OpenVINO one.
        OptimizationRoute route = configuration switch
        {
            GgufRouteConfiguration => OptimizationRoute.Gguf,
            OpenVinoRouteConfiguration => OptimizationRoute.OpenVino,
            _ => throw new ArgumentException(
                $"{configuration.GetType().Name} belongs to no known route, so no "
                + "executor could claim it.",
                nameof(configuration))
        };

        return new OptimizationCandidate(
            route, configuration, metrics, evidenceId, isExperimental,
            conversionProvenance, notice, weightNormalizationProof: null,
            admissionProof: null);
    }

    internal static OptimizationCandidate CreateWithGgufWeightNormalization(
        GgufRouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental,
        GgufWeightNormalizationProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (configuration.Weights != GgufWeightFormat.Imported
            || metrics.RequiresPersistentChange)
        {
            throw new ArgumentException(
                "Only an imported, runtime-only GGUF candidate can carry an "
                + "already-at-target normalization proof.",
                nameof(configuration));
        }

        OptimizationCandidate validated = Create(
            configuration, metrics, evidenceId, isExperimental);
        return new OptimizationCandidate(
            validated.Route, validated.Configuration, validated.Metrics,
            validated.EvidenceId, validated.IsExperimental,
            validated.ConversionProvenance, validated.Notice, proof,
            admissionProof: null);
    }

    internal static OptimizationCandidate AttachAdmissionProof(
        OptimizationCandidate candidate,
        OptimizationAdmissionProof proof)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(proof);
        if (!proof.MatchesCandidate(candidate))
        {
            throw new ArgumentException(
                "An admission proof must describe the exact candidate it admits.",
                nameof(proof));
        }

        return new OptimizationCandidate(
            candidate.Route, candidate.Configuration, candidate.Metrics,
            candidate.EvidenceId, candidate.IsExperimental,
            candidate.ConversionProvenance, candidate.Notice,
            candidate.WeightNormalizationProof, proof);
    }

    internal static OptimizationCandidateNotice ExpectedNotice(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        OptimizationConversionProvenance provenance)
    {
        if (provenance == OptimizationConversionProvenance.ControlledRequantisation)
        {
            return configuration is GgufRouteConfiguration
                    { Weights: GgufWeightFormat.Q2K }
                ? OptimizationCandidateNotice.LowQualityRequantisation
                : OptimizationCandidateNotice.Requantisation;
        }

        return metrics.Quality == OptimizationAssessment.Poor
            ? OptimizationCandidateNotice.LowQuality
            : OptimizationCandidateNotice.None;
    }

    /// <summary>
    /// Reconstructs an authentic released-v2 candidate whose vocabulary had no
    /// notice or conversion provenance. Kept internal so new planning cannot
    /// silently discard a warning; the v2 canonical verifier is its sole use.
    /// </summary>
    internal static OptimizationCandidate CreateLegacyVersionTwo(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental)
    {
        if (configuration is GgufRouteConfiguration
            { Weights: GgufWeightFormat.Q2K })
        {
            throw new ArgumentException(
                "Q2_K did not exist in the released version-two vocabulary.",
                nameof(configuration));
        }

        OptimizationCandidate validated = Create(
            configuration, metrics, evidenceId, isExperimental,
            OptimizationConversionProvenance.None);

        return new OptimizationCandidate(
            validated.Route, validated.Configuration, validated.Metrics,
            validated.EvidenceId, validated.IsExperimental,
            OptimizationConversionProvenance.None,
            OptimizationCandidateNotice.None,
            weightNormalizationProof: null,
            admissionProof: null);
    }
}
