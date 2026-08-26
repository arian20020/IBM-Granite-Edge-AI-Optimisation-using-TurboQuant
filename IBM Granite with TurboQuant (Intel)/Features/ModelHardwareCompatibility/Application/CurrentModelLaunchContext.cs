using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;

internal sealed record CurrentModelLaunchContext
{
    private CurrentModelLaunchContext(
        CurrentModelLaunchHandoff handoff,
        OptimizationExecutionPayload exactExecutionPayload,
        ModelSourceCustodyKey sourceKey)
    {
        Handoff = handoff;
        ExactExecutionPayload = exactExecutionPayload;
        SourceKey = sourceKey;
    }

    internal CurrentModelLaunchHandoff Handoff { get; }
    internal OptimizationExecutionPayload ExactExecutionPayload { get; }
    internal ModelSourceCustodyKey SourceKey { get; }

    internal static CurrentModelLaunchContext Create(
        CurrentModelLaunchHandoff handoff,
        OptimizationExecutionPayload exactExecutionPayload,
        ModelSourceCustodyKey sourceKey)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(exactExecutionPayload);
        ArgumentNullException.ThrowIfNull(sourceKey);
        if (handoff.Route != exactExecutionPayload.Route
            || handoff.Route != sourceKey.Route
            || handoff.ModelInspectionHandoffId
                != sourceKey.ModelInspectionHandoffId
            || handoff.ModelLengthBytes != sourceKey.ModelLengthBytes
            || !string.Equals(
                handoff.ModelSha256,
                sourceKey.ModelSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                handoff.RuntimeConfigurationSha256,
                exactExecutionPayload.ComputeRuntimeConfigurationSha256(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A current-model launch context must bind the exact source and runtime configuration.");
        }

        return new CurrentModelLaunchContext(
            handoff,
            exactExecutionPayload,
            sourceKey);
    }
}
