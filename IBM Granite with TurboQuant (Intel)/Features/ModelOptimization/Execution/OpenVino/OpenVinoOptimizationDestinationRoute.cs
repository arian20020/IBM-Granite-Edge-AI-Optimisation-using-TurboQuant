using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.OpenVino;

internal sealed class OpenVinoDestinationChatTarget(
    OptimizationExecutionResult result,
    OpenVinoOptimizationChatTarget target) : OptimizationChatTarget(result)
{
    internal OpenVinoOptimizationChatTarget Target { get; } = target;

    protected override void DisposeCore() => Target.Dispose();
}

internal sealed class OpenVinoOptimizationDestinationRoute(
    OpenVinoOptimizationExecutor executor) : IOptimizationDestinationRoute
{
    public OptimizationRoute Route => OptimizationRoute.OpenVino;

    public async Task<OptimizationChatTarget?> CreateChatTargetAsync(
        OptimizationExecutionResult result,
        CancellationToken cancellationToken)
    {
        OpenVinoOptimizationChatTarget? target =
            await executor.CreateChatTargetAsync(result, cancellationToken)
                .ConfigureAwait(false);
        return target is null
            ? null
            : new OpenVinoDestinationChatTarget(result, target);
    }

    public async Task<OptimizationDestinationExportResult> ExportPersistentAsync(
        OptimizationExecutionResult result,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        OpenVinoExportResult export = await executor.ExportPersistentAsync(
            result, destination, maximumBytes, cancellationToken)
            .ConfigureAwait(false);
        if (export.OptimizationPlanId != result.OptimizationPlanId
            || export.ExecutionId != result.ExecutionId)
        {
            return OptimizationDestinationExportResult.For(
                result, OptimizationDestinationExportDisposition.ResultRejected);
        }
        return OptimizationDestinationExportResult.For(result,
            export.Disposition switch
            {
                OpenVinoExportDisposition.Succeeded =>
                    OptimizationDestinationExportDisposition.Succeeded,
                OpenVinoExportDisposition.RuntimeOnly =>
                    OptimizationDestinationExportDisposition.RuntimeOnly,
                OpenVinoExportDisposition.ResultRejected or
                OpenVinoExportDisposition.IdentityMismatch =>
                    OptimizationDestinationExportDisposition.ResultRejected,
                OpenVinoExportDisposition.DestinationExists =>
                    OptimizationDestinationExportDisposition.DestinationExists,
                OpenVinoExportDisposition.DestinationRejected =>
                    OptimizationDestinationExportDisposition.DestinationRejected,
                OpenVinoExportDisposition.CleanupFailed =>
                    OptimizationDestinationExportDisposition.CleanupFailed,
                _ => OptimizationDestinationExportDisposition.Failed,
            });
    }
}
