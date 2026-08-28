using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.OpenVino.Contracts;
using AppOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;
using OpenVinoOutcome = GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome;
using SharedOutcome = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

/// <summary>
/// Converts route-validated terminal evidence into the one shared,
/// path-private schema-v2 projection without re-parsing route metadata.
/// </summary>
internal static class ModelInspectionProjectionFactory
{
    internal static bool TryCreateGguf(
        Guid currentModelInspectionRunId,
        Guid terminalModelInspectionRunId,
        ModelInspectionExecutionResult terminal,
        out ModelInspectionProjectionV2? projection)
    {
        projection = null;
        if (!ModelInspectionHandoffProjector.TryProject(
                currentModelInspectionRunId,
                terminalModelInspectionRunId,
                terminal,
                out ModelInspectionHandoff? routeHandoff) ||
            routeHandoff is null)
        {
            return false;
        }

        projection = ModelInspectionProjectionV2.Create(
            ModelInspectionRoute.Gguf,
            "gguf",
            routeHandoff.ModelInspectionRunId,
            routeHandoff.ModelInspectionHandoffId,
            Map(routeHandoff.Outcome),
            routeHandoff.ModelSha256,
            routeHandoff.ModelLengthBytes);
        return true;
    }

    internal static ModelInspectionProjectionV2 CreateOpenVino(
        OpenVinoStaticPackageInspectionResult staticInspection,
        OpenVinoNativeValidationEvidence nativeValidation,
        Guid modelInspectionRunId)
    {
        GraniteEdgeAI.OpenVino.Contracts.ModelInspectionHandoffV2 routeHandoff =
            new OpenVinoInspectionHandoffFactory().Create(
                staticInspection,
                nativeValidation,
                modelInspectionRunId);
        return ModelInspectionProjectionV2.Create(
            ModelInspectionRoute.OpenVino,
            "openvino-ir",
            routeHandoff.ModelInspectionRunId,
            routeHandoff.ModelInspectionHandoffId,
            Map(routeHandoff.Outcome),
            routeHandoff.ModelSha256,
            routeHandoff.ModelLengthBytes);
    }

    private static SharedOutcome Map(AppOutcome outcome) => outcome switch
    {
        AppOutcome.Ready => SharedOutcome.Ready,
        AppOutcome.ReadyWithWarnings => SharedOutcome.ReadyWithWarnings,
        _ => throw new InvalidOperationException(
            "Only eligible GGUF outcomes can enter schema-v2 projection.")
    };

    private static SharedOutcome Map(OpenVinoOutcome outcome) => outcome switch
    {
        OpenVinoOutcome.Ready => SharedOutcome.Ready,
        OpenVinoOutcome.ReadyWithWarnings => SharedOutcome.ReadyWithWarnings,
        _ => throw new InvalidOperationException(
            "Only eligible OpenVINO outcomes can enter schema-v2 projection.")
    };
}
