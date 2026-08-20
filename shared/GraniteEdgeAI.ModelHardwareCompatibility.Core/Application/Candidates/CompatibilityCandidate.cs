using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// One complete, immutable configuration. There is no partially filled
/// candidate: construction either produces a fully specified configuration or
/// it fails. That is what lets later mode selection be a pure lookup over
/// already-validated options rather than a second round of derivation.
/// </summary>
internal sealed record CompatibilityCandidate
{
    private CompatibilityCandidate(
        RouteConfiguration configuration,
        ContextTokenCount context,
        CandidatePreparation preparation,
        string supportEntryId,
        bool isExperimental,
        bool isBaseline,
        CandidateFingerprint fingerprint)
    {
        Configuration = configuration;
        Context = context;
        Preparation = preparation;
        SupportEntryId = supportEntryId;
        IsExperimental = isExperimental;
        IsBaseline = isBaseline;
        Fingerprint = fingerprint;
    }

    internal RouteConfiguration Configuration { get; }

    internal ContextTokenCount Context { get; }

    internal CandidatePreparation Preparation { get; }

    /// <summary>The admitted support-matrix entry this candidate came from.</summary>
    internal string SupportEntryId { get; }

    internal bool IsExperimental { get; }

    /// <summary>True for the as-imported configuration.</summary>
    internal bool IsBaseline { get; }

    internal CandidateFingerprint Fingerprint { get; }

    internal RuntimeRouteId RouteId => Configuration.RouteId;

    internal static CompatibilityCandidate Create(
        RouteConfiguration configuration,
        ContextTokenCount context,
        CandidatePreparation preparation,
        string supportEntryId,
        bool isExperimental,
        bool isBaseline)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (preparation == CandidatePreparation.Unspecified)
        {
            throw new ArgumentException(
                "A candidate must declare what preparation it requires, because "
                + "creating a new model file is not the same as changing a setting.",
                nameof(preparation));
        }

        if (string.IsNullOrWhiteSpace(supportEntryId))
        {
            throw new ArgumentException(
                "A candidate must name the admitted support entry it came from, "
                + "so an unadmitted configuration can never be generated.",
                nameof(supportEntryId));
        }

        return new CompatibilityCandidate(
            configuration,
            context,
            preparation,
            supportEntryId,
            isExperimental,
            isBaseline,
            CandidateFingerprint.Compute(configuration, context, preparation));
    }
}
