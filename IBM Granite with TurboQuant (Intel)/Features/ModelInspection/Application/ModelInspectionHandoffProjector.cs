using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

/// <summary>
/// Projects only a current eligible terminal Model result into the closed
/// cross-feature handoff.
/// </summary>
internal static class ModelInspectionHandoffProjector
{
    internal static bool TryProject(
        Guid currentModelInspectionRunId,
        Guid terminalModelInspectionRunId,
        ModelInspectionExecutionResult terminal,
        out ModelInspectionHandoff? handoff)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        handoff = null;

        if (!ModelInspectionHandoff.IsUuidV4(currentModelInspectionRunId) ||
            terminalModelInspectionRunId != currentModelInspectionRunId ||
            terminal.Status != ModelInspectionExecutionStatus.Completed ||
            terminal.Result is not { } result ||
            !result.CanContinueToHardwareFit ||
            result.Outcome is not ModelInspectionOutcome.Ready and
                not ModelInspectionOutcome.ReadyWithWarnings ||
            !result.Evidence.File.IntegrityPreserved)
        {
            return false;
        }

        try
        {
            handoff = new ModelInspectionHandoff(
                ModelInspectionHandoff.CurrentSchemaVersion,
                Guid.NewGuid(),
                terminalModelInspectionRunId,
                result.Outcome,
                result.Evidence.File.ModelSha256,
                result.Evidence.File.LengthBytes);
            return true;
        }
        catch (ArgumentException)
        {
            handoff = null;
            return false;
        }
    }
}
