using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;

/// <summary>
/// The outcome of resolving a context request against a model's trained limit.
/// </summary>
internal sealed record PlanningContextResolution
{
    private PlanningContextResolution(
        PlanningContextStatus status,
        ContextTokenCount? resolvedTokens,
        bool withinModelLimit)
    {
        Status = status;
        ResolvedTokens = resolvedTokens;
        WithinModelLimit = withinModelLimit;
    }

    internal PlanningContextStatus Status { get; }

    internal ContextTokenCount? ResolvedTokens { get; }

    /// <summary>
    /// False when an explicit request exceeds the model's trained limit. The
    /// request is still preserved exactly; it is the caller's decision what to
    /// do about it, so the user always sees their own number.
    /// </summary>
    internal bool WithinModelLimit { get; }

    internal static PlanningContextResolution Resolved(
        ContextTokenCount tokens,
        bool withinModelLimit) =>
        new(PlanningContextStatus.Resolved, tokens, withinModelLimit);

    internal static PlanningContextResolution NotEstablished() =>
        new(PlanningContextStatus.NotEstablished, resolvedTokens: null, withinModelLimit: false);
}
