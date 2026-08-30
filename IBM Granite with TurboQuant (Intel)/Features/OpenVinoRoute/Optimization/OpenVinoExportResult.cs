using System;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal enum OpenVinoExportDisposition
{
    Succeeded,
    ResultRejected,
    RuntimeOnly,
    DestinationRejected,
    DestinationExists,
    IdentityMismatch,
    CleanupFailed
}

internal sealed record OpenVinoExportResult(
    OpenVinoExportDisposition Disposition,
    Guid OptimizationPlanId,
    Guid ExecutionId)
{
    internal bool IsSuccessful => Disposition == OpenVinoExportDisposition.Succeeded;

    internal static OpenVinoExportResult For(
        OptimizationExecutionResult result,
        OpenVinoExportDisposition disposition) => new(
            disposition,
            result.OptimizationPlanId,
            result.ExecutionId);
}

internal sealed class OpenVinoExportCleanupException : InvalidOperationException
{
    internal OpenVinoExportCleanupException(bool operationWasCancelled)
        : base("The temporary export could not be removed safely.")
    {
        OperationWasCancelled = operationWasCancelled;
    }

    internal bool OperationWasCancelled { get; }
}
