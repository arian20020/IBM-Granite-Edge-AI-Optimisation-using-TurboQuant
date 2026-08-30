using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.CrossFeature.Contracts;

public readonly record struct ChatTargetAuthority(
    Guid OptimizationPlanId,
    string ConfigurationSha256,
    string OutputIdentity);

public readonly record struct ExportDestinationAuthority(Guid Value);

public interface IExactOptimizationResultConsumer
{
    bool TryCreateChatTarget(
        OptimizationExecutionResult exactResult,
        long lifecycleGeneration,
        out ChatTargetAuthority target);

    Task<bool> ExportPersistentAsync(
        OptimizationExecutionResult exactResult,
        ExportDestinationAuthority destination,
        long lifecycleGeneration,
        CancellationToken cancellationToken);
}
