using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

internal static partial class ModelInspectionProjectionFactory
{
    internal static ModelInspectionProjectionV2 CreateOpenVino(
        ModelInspectionHandoffV2 handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        handoff.Validate();
        return ModelInspectionProjectionV2.Create(
            ModelInspectionRoute.OpenVino,
            "openvino-ir",
            handoff.ModelInspectionRunId,
            handoff.ModelInspectionHandoffId,
            handoff.Outcome,
            handoff.ModelSha256,
            handoff.ModelLengthBytes);
    }
}
