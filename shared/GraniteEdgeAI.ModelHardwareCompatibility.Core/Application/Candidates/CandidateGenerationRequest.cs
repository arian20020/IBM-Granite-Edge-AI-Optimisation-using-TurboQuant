using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Everything generation needs, gathered explicitly so the generator itself
/// performs no lookup, no I/O and no clock read.
/// </summary>
internal sealed record CandidateGenerationRequest(
    SupportMatrix Matrix,
    IReadOnlyDictionary<string, InstallationState> InstallationStates,
    InspectedModelFacts Facts,
    GgufRouteConfiguration BaselineConfiguration,
    ContextTokenCount BaselineContext,
    ContextTokenCount PreservationTarget,
    TrustedSourceAvailability TrustedSource);

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
    BaselineContextOutsideEntryBounds
}

/// <summary>
/// The generated candidate set, baseline first, plus the baseline's fate.
/// </summary>
internal sealed record CandidateGenerationResult(
    IReadOnlyList<CompatibilityCandidate> Candidates,
    bool BaselineIncluded,
    BaselineExclusionReason BaselineExclusionReason);
