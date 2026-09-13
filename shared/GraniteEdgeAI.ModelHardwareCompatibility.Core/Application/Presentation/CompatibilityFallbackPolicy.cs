using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

internal static class CompatibilityFallbackPolicy
{
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
