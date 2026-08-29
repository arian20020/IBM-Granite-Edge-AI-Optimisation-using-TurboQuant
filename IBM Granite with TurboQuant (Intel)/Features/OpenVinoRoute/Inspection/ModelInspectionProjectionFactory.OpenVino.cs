using GraniteEdgeAI.ModelInspection.Contracts;
using OpenVinoHandoff = GraniteEdgeAI.OpenVino.Contracts.ModelInspectionHandoffV2;
using OpenVinoOutcome = GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome;
using SharedOutcome = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

internal static partial class ModelInspectionProjectionFactory
{
    internal static ModelInspectionProjectionV2 CreateOpenVino(
        OpenVinoHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        handoff.Validate();
        return ModelInspectionProjectionV2.Create(
            ModelInspectionRoute.OpenVino,
            "openvino-ir",
            handoff.ModelInspectionRunId,
            handoff.ModelInspectionHandoffId,
            Map(handoff.Outcome),
            handoff.ModelSha256,
            handoff.ModelLengthBytes);
    }

    private static SharedOutcome Map(OpenVinoOutcome outcome) => outcome switch
    {
        OpenVinoOutcome.Ready => SharedOutcome.Ready,
        OpenVinoOutcome.ReadyWithWarnings => SharedOutcome.ReadyWithWarnings,
        _ => throw new InvalidOperationException(
            "Only eligible OpenVINO outcomes can enter schema-v2 projection.")
    };
}
