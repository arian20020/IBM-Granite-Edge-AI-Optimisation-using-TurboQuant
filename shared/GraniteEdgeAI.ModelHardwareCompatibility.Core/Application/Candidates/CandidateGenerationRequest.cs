using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Everything generation needs, gathered explicitly so the generator itself
/// performs no lookup, no I/O and no clock read.
///
/// Constructed only through <see cref="Create"/>, like every other type in this
/// library: a positional record's implicit constructor would let a null
/// <see cref="Matrix"/>, <see cref="Facts"/>, <see cref="BaselineConfiguration"/>,
/// <see cref="TrustedSource"/> or <see cref="InstallationStates"/> reach the
/// generator as an unhandled null-reference exception rather than a typed
/// refusal.
/// </summary>
internal sealed record CandidateGenerationRequest
{
    private CandidateGenerationRequest(
        SupportMatrix matrix,
        IReadOnlyDictionary<string, InstallationState> installationStates,
        InspectedModelFacts facts,
        GgufRouteConfiguration baselineConfiguration,
        ContextTokenCount baselineContext,
        ContextTokenCount preservationTarget,
        TrustedSourceAvailability trustedSource)
    {
        Matrix = matrix;
        InstallationStates = installationStates;
        Facts = facts;
        BaselineConfiguration = baselineConfiguration;
        BaselineContext = baselineContext;
        PreservationTarget = preservationTarget;
        TrustedSource = trustedSource;
    }

    internal SupportMatrix Matrix { get; }

    internal IReadOnlyDictionary<string, InstallationState> InstallationStates { get; }

    internal InspectedModelFacts Facts { get; }

    internal GgufRouteConfiguration BaselineConfiguration { get; }

    internal ContextTokenCount BaselineContext { get; }

    internal ContextTokenCount PreservationTarget { get; }

    internal TrustedSourceAvailability TrustedSource { get; }

    internal static CandidateGenerationRequest Create(
        SupportMatrix matrix,
        IReadOnlyDictionary<string, InstallationState> installationStates,
        InspectedModelFacts facts,
        GgufRouteConfiguration baselineConfiguration,
        ContextTokenCount baselineContext,
        ContextTokenCount preservationTarget,
        TrustedSourceAvailability trustedSource)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(installationStates);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(baselineConfiguration);
        ArgumentNullException.ThrowIfNull(trustedSource);

        return new CandidateGenerationRequest(
            matrix,
            installationStates,
            facts,
            baselineConfiguration,
            baselineContext,
            preservationTarget,
            trustedSource);
    }
}

/// <summary>
/// Why the as-imported configuration is not among the candidates. Spec section
/// 10 requires the exact reason be preserved rather than the baseline silently
/// going missing.
/// </summary>
internal enum BaselineExclusionReason
{
    None = 0,
    SupportMatrixUnavailable,
    NoAdmittedEntryMatchesTheBaseline,
    BaselineContextOutsideEntryBounds,

    /// <summary>
    /// An admitted entry declares the baseline's exact configuration shape, but
    /// its installation state is not one the matrix resolves as available (for
    /// example <see cref="InstallationState.NotInstalled"/> or
    /// <see cref="InstallationState.Unknown"/>). This is reported in preference
    /// to <see cref="NoAdmittedEntryMatchesTheBaseline"/> because it is
    /// actionable: the fix is installing the named backend, not accepting that
    /// the configuration is unsupported.
    /// </summary>
    BaselineEntryNotInstalled,

    /// <summary>
    /// The model's trained context limit could not be established, so no
    /// candidate can be safely bounded against it. Substituting an entry's
    /// declared maximum in place of the real limit would give a model with an
    /// unknown limit no model-side context constraint at all.
    /// </summary>
    ModelContextLimitNotEstablished
}

/// <summary>
/// The generated candidate set, baseline first, plus the baseline's fate.
/// </summary>
internal sealed record CandidateGenerationResult(
    IReadOnlyList<CompatibilityCandidate> Candidates,
    BaselineExclusionReason BaselineExclusionReason)
{
    /// <summary>
    /// Derived rather than stored: whether the baseline was included is fully
    /// determined by whether an exclusion reason was recorded, so carrying both
    /// independently would let them disagree.
    /// </summary>
    internal bool BaselineIncluded => BaselineExclusionReason == BaselineExclusionReason.None;
}
