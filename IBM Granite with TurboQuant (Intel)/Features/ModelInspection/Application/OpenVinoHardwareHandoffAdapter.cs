using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

internal static class OpenVinoHardwareHandoffAdapter
{
    internal static bool TryProject(
        ModelInspectionHandoffV2? source,
        out ModelInspectionHandoff? handoff)
    {
        handoff = null;
        if (source is null)
        {
            return false;
        }

        try
        {
            source.Validate();
            handoff = new ModelInspectionHandoff(
                source.SchemaVersion,
                source.ModelInspectionHandoffId,
                source.ModelInspectionRunId,
                source.Outcome == GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome.Ready
                    ? Contracts.ModelInspectionOutcome.Ready
                    : Contracts.ModelInspectionOutcome.ReadyWithWarnings,
                source.ModelSha256,
                source.ModelLengthBytes,
                ModelInspectionRouteKind.OpenVino);
            return true;
        }
        catch (Exception)
        {
            handoff = null;
            return false;
        }
    }
}
