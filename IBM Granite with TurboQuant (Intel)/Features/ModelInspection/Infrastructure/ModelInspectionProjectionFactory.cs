using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Contracts;
using AppOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;
using SharedOutcome = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

internal sealed class ModelInspectionProjectionHandle
{
    internal ModelInspectionProjectionHandle(ModelInspectionProjectionV2 projection)
    {
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    internal ModelInspectionProjectionV2 Projection { get; }
}

/// <summary>
/// Converts route-validated terminal evidence into the one shared,
/// path-private schema-v2 projection without re-parsing route metadata.
/// </summary>
internal static partial class ModelInspectionProjectionFactory
{
    internal static ModelInspectionProjectionHandle CreateGgufHandle(
        ModelInspectionHandoff handoff) =>
        new(CreateGguf(handoff));

    internal static ModelInspectionProjectionV2 CreateGguf(
        ModelInspectionHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        return ModelInspectionProjectionV2.Create(
            ModelInspectionRoute.Gguf,
            "gguf",
            handoff.ModelInspectionRunId,
            handoff.ModelInspectionHandoffId,
            Map(handoff.Outcome),
            handoff.ModelSha256,
            handoff.ModelLengthBytes);
    }

    private static SharedOutcome Map(AppOutcome outcome) => outcome switch
    {
        AppOutcome.Ready => SharedOutcome.Ready,
        AppOutcome.ReadyWithWarnings => SharedOutcome.ReadyWithWarnings,
        _ => throw new InvalidOperationException(
            "Only eligible GGUF outcomes can enter schema-v2 projection.")
    };
}
