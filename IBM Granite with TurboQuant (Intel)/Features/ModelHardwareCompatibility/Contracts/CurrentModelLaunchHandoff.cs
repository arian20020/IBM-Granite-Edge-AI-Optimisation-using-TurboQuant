using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

internal sealed record CurrentModelLaunchHandoff
{
    private CurrentModelLaunchHandoff(
        OptimizationRoute route,
        Guid modelInspectionRunId,
        Guid modelInspectionHandoffId,
        string modelSha256,
        long modelLengthBytes,
        Guid productHardwareRunId,
        string hardwareSnapshotSha256,
        string runtimeConfigurationSha256,
        string compatibilityDecisionId)
    {
        Route = route;
        ModelInspectionRunId = modelInspectionRunId;
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelSha256 = modelSha256;
        ModelLengthBytes = modelLengthBytes;
        ProductHardwareRunId = productHardwareRunId;
        HardwareSnapshotSha256 = hardwareSnapshotSha256;
        RuntimeConfigurationSha256 = runtimeConfigurationSha256;
        CompatibilityDecisionId = compatibilityDecisionId;
    }

    public OptimizationRoute Route { get; }
    public Guid ModelInspectionRunId { get; }
    public Guid ModelInspectionHandoffId { get; }
    public string ModelSha256 { get; }
    public long ModelLengthBytes { get; }
    public Guid ProductHardwareRunId { get; }
    public string HardwareSnapshotSha256 { get; }
    public string RuntimeConfigurationSha256 { get; }
    public string CompatibilityDecisionId { get; }

    internal static CurrentCompatibleConfiguration CreateGgufConfiguration(
        Guid inspectionHandoffId,
        Guid hardwareRunId,
        OptimizationExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!IsUuidV4(inspectionHandoffId) || !IsUuidV4(hardwareRunId)
            || inspectionHandoffId == hardwareRunId)
        {
            throw new ArgumentException("The current configuration requires distinct inspection and hardware runs.");
        }
        string digest = payload.ComputeRuntimeConfigurationSha256();
        // Runtime settings can be identical across models and re-inspections.
        // Keep each validated journey separate in the launch registry.
        string decisionDigest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{inspectionHandoffId:N}:{hardwareRunId:N}:{digest}")))
            .ToLowerInvariant();
        return new CurrentCompatibleConfiguration(
            OptimizationRoute.Gguf, payload, digest, $"compat-{decisionDigest}");
    }

    internal static CurrentModelLaunchHandoff Create(
        OptimizationRoute route,
        Guid modelInspectionRunId,
        Guid modelInspectionHandoffId,
        string modelSha256,
        long modelLengthBytes,
        Guid productHardwareRunId,
        string hardwareSnapshotSha256,
        CurrentCompatibleConfiguration currentConfiguration)
    {
        ArgumentNullException.ThrowIfNull(currentConfiguration);
        if (!Enum.IsDefined(route)
            || currentConfiguration.Route != route
            || !IsUuidV4(modelInspectionRunId)
            || !IsUuidV4(modelInspectionHandoffId)
            || !IsUuidV4(productHardwareRunId)
            || modelInspectionRunId == modelInspectionHandoffId
            || modelInspectionRunId == productHardwareRunId
            || modelInspectionHandoffId == productHardwareRunId
            || modelLengthBytes < 1
            || !IsCanonicalSha256(modelSha256)
            || !IsCanonicalSha256(hardwareSnapshotSha256)
            || !string.Equals(
                currentConfiguration.RuntimeConfigurationSha256,
                currentConfiguration.ExactExecutionPayload
                    .ComputeRuntimeConfigurationSha256(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Current-model launch authority must bind one exact model, Hardware run and runtime configuration.");
        }

        return new CurrentModelLaunchHandoff(
            route,
            modelInspectionRunId,
            modelInspectionHandoffId,
            modelSha256,
            modelLengthBytes,
            productHardwareRunId,
            hardwareSnapshotSha256,
            currentConfiguration.RuntimeConfigurationSha256,
            currentConfiguration.CompatibilityDecisionId);
    }

    private static bool IsUuidV4(Guid value)
    {
        string canonical = value.ToString("D");
        return value != Guid.Empty
            && canonical[14] == '4'
            && canonical[19] is '8' or '9' or 'a' or 'b';
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
