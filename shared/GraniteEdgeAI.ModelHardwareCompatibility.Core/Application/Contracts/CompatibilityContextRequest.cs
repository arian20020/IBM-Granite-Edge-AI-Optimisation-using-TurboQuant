using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// The user's context intent. The two modes are mutually exclusive: a default
/// carries no number, an explicit request always carries one. Named factories
/// make the invalid combinations unrepresentable.
/// </summary>
internal sealed record CompatibilityContextRequest
{
    private CompatibilityContextRequest(
        CompatibilityContextMode mode,
        ContextTokenCount? requestedTokens)
    {
        Mode = mode;
        RequestedTokens = requestedTokens;
    }

    internal CompatibilityContextMode Mode { get; }

    internal ContextTokenCount? RequestedTokens { get; }

    internal static CompatibilityContextRequest ApplicationDefault() =>
        new(CompatibilityContextMode.ApplicationDefault, requestedTokens: null);

    internal static CompatibilityContextRequest UserRequested(
        ContextTokenCount requestedTokens) =>
        new(CompatibilityContextMode.UserRequested, requestedTokens);
}
