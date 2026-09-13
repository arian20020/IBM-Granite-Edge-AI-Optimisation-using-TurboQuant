using System;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

/// <summary>
/// Maps a typed GGUF startup result without selecting a different backend.
/// A CPU retry must be issued by planning as a new CPU configuration; this
/// mapper only retires the Vulkan plan that the worker could not execute.
/// </summary>
internal static class GgufRuntimeFailureResultMapper
{
    internal static OptimizationExecutionResult Map(
        OptimizationExecutionPlan plan,
        OptimizationAttemptContext context,
        GgufRuntimeBackend requestedBackend,
        GgufRuntimeFailure failure,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(failure);

        DateTimeOffset completedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (requestedBackend == GgufRuntimeBackend.Vulkan
            && IsVulkanRuntimeUnavailable(failure))
        {
            return OptimizationExecutionResult.ReplanRequired(
                plan,
                OptimizationSupportCode.ToolNotAdmitted,
                context.Source.RehashMatches(),
                completedAtUtc);
        }

        return OptimizationExecutionResult.Failed(
            plan,
            OptimizationSupportCode.ToolNotAdmitted,
            context.Source.RehashMatches(),
            completedAtUtc);
    }

    internal static bool IsVulkanRuntimeUnavailable(GgufRuntimeFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Category == GgufRuntimeFailureCategory.RuntimeUnavailable
            && string.Equals(
                failure.Code,
                "vulkan-runtime-unavailable",
                StringComparison.Ordinal);
    }
}
