using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

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
    LowQualityRequantisation
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
        bool requiresPersistentChange)
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

    /// <summary>Transient space a conversion needs while it runs.</summary>
    public ulong WorkingDiskBytes { get; }

    /// <summary>Space the finished output occupies. Zero for runtime-only work.</summary>
    public ulong OutputDiskBytes { get; }

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
        bool requiresPersistentChange)
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
            requiresPersistentChange);
    }

    /// <summary>Whether the peak fits inside what this candidate was allowed.</summary>
    public bool FitsSafely => PredictedPeakBytes <= SafeBudgetBytes;

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
        OptimizationCandidateNotice notice)
    {
        Route = route;
        Configuration = configuration;
        Metrics = metrics;
        EvidenceId = evidenceId;
        IsExperimental = isExperimental;
        Notice = notice;
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

    public OptimizationCandidateNotice Notice { get; }

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
        OptimizationCandidateNotice notice = OptimizationCandidateNotice.None)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(metrics);

        OptimizationIdentifier.Require(
            evidenceId, nameof(evidenceId), "The evidence behind a candidate");

        if (!Enum.IsDefined(notice))
        {
            throw new ArgumentOutOfRangeException(
                nameof(notice), notice, "An undefined notice cannot be presented.");
        }

        if (configuration is GgufRouteConfiguration
                { Weights: GgufWeightFormat.Q2K }
            && notice is not (OptimizationCandidateNotice.LowQuality
                or OptimizationCandidateNotice.LowQualityRequantisation))
        {
            throw new ArgumentException(
                "A Q2_K candidate must carry its typed low-quality warning; "
                + "without it a presentation surface could offer the product "
                + "floor as an ordinary precision.",
                nameof(notice));
        }

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
            route, configuration, metrics, evidenceId, isExperimental, notice);
    }
}
