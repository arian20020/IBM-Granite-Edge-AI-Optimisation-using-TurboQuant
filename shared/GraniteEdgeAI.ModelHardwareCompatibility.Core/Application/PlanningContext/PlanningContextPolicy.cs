using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;

/// <summary>
/// Resolves the context length every later estimate is calculated against.
/// Pure policy: no hardware, no I/O, no clock, so it is fully deterministic.
/// </summary>
internal static class PlanningContextPolicy
{
    /// <summary>
    /// The ceiling applied when the user has expressed no preference.
    /// Deliberately conservative so an untouched default never drives a large
    /// speculative KV cache that would fail an otherwise usable machine.
    /// </summary>
    internal const int ApplicationDefaultCeilingTokens = 4096;

    internal static PlanningContextResolution Resolve(
        CompatibilityContextRequest request,
        ulong? declaredModelContextLimit)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A model whose trained limit is unknown or zero gives no safe basis for
        // a default, and a limit beyond int range is not trustworthy input.
        if (declaredModelContextLimit is not > 0 ||
            declaredModelContextLimit > int.MaxValue)
        {
            return PlanningContextResolution.NotEstablished();
        }

        int modelLimit = (int)declaredModelContextLimit.Value;

        return request.Mode switch
        {
            CompatibilityContextMode.ApplicationDefault =>
                PlanningContextResolution.Resolved(
                    ContextTokenCount.FromTokens(
                        Math.Min(ApplicationDefaultCeilingTokens, modelLimit)),
                    withinModelLimit: true),

            // An explicit request is never silently clamped. It is preserved and
            // flagged so the mismatch stays visible instead of being hidden.
            CompatibilityContextMode.UserRequested =>
                PlanningContextResolution.Resolved(
                    request.RequestedTokens!.Value,
                    withinModelLimit: request.RequestedTokens!.Value.Tokens <= modelLimit),

            _ => PlanningContextResolution.NotEstablished()
        };
    }
}
