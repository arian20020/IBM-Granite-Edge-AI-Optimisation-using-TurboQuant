using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

internal static class CompatibilityFallbackPolicy
{
    internal static T Execute<T>(Func<T> operation, Func<T> fallback)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(fallback);

        try
        {
            return operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return fallback();
        }
    }

    internal static CompatibilityScreenModel NotEstablished() =>
        CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NotEstablished,
            [new CompatibilityFindingView(
                CompatibilityFindingCode.UnexpectedFailure,
                FindingSeverity.Blocking)],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false);
}
