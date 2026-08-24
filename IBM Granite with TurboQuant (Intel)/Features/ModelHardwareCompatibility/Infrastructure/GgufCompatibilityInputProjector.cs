using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal static class GgufCompatibilityInputProjector
{
    private static readonly TimeSpan MaximumMemoryAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromSeconds(5);

    internal static bool TryProject(
        ModelInspectionHandoff modelHandoff,
        ModelInspectionExecutionResult terminalModelResult,
        Guid productHardwareRunId,
        HardwareInspectionHandoff hardwareHandoff,
        AvailableMemorySnapshot freshMemory,
        out CompatibilityProductionInput? input)
    {
        ArgumentNullException.ThrowIfNull(modelHandoff);
        ArgumentNullException.ThrowIfNull(terminalModelResult);
        ArgumentNullException.ThrowIfNull(hardwareHandoff);
        ArgumentNullException.ThrowIfNull(freshMemory);
        input = null;

        if (terminalModelResult.Status != ModelInspectionExecutionStatus.Completed
            || terminalModelResult.Result is not { } result
            || !result.CanContinueToHardwareFit
            || result.Outcome != modelHandoff.Outcome
            || !result.Evidence.File.IntegrityPreserved
            || result.Evidence.File.LengthBytes != modelHandoff.ModelLengthBytes
            || !string.Equals(
                result.Evidence.File.ModelSha256,
                modelHandoff.ModelSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                result.Evidence.Configuration.Format,
                "GGUF",
                StringComparison.Ordinal)
            || productHardwareRunId == Guid.Empty
            || hardwareHandoff.InspectionId != productHardwareRunId
            || hardwareHandoff.Snapshot.Usability != HardwareSnapshotUsability.Usable)
        {
            return false;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan age = now - freshMemory.CapturedAtUtc;
        if (age > MaximumMemoryAge || age < -MaximumFutureSkew)
        {
            return false;
        }

        try
        {
            ModelInspectionConfigurationEvidence configuration =
                result.Evidence.Configuration;
            HardwareSnapshot snapshot = hardwareHandoff.Snapshot;
            ulong dedicatedMemory = 0;
            HashSet<DeviceRouteId> devices = [DeviceRouteId.Cpu];

            foreach (GraphicsAdapterFacts adapter in snapshot.GraphicsAdapters)
            {
                if (adapter.DedicatedVideoMemoryBytes is { } dedicated)
                {
                    dedicatedMemory = checked(dedicatedMemory + dedicated);
                    if (dedicated > 0)
                    {
                        devices.Add(DeviceRouteId.IntelDiscreteGpu);
                    }
                }

                if (adapter.SharedSystemMemoryBytes is > 0
                    || adapter.DedicatedSystemMemoryBytes is > 0)
                {
                    devices.Add(DeviceRouteId.IntelIntegratedGpu);
                }
            }

            if (snapshot.NeuralProcessor.State == NpuDetectionState.Present)
            {
                devices.Add(DeviceRouteId.IntelNpu);
            }

            HashSet<CompatibilityBackend> backends =
            [
                .. snapshot.LocalRuntime.SupportedBackends.Select(MapBackend),
            ];

            GgufCompatibilityModelInput model = GgufCompatibilityModelInput.Create(
                checked((ulong)modelHandoff.ModelLengthBytes),
                configuration.LayerCount,
                ToOptionalInt(configuration.EmbeddingSize),
                configuration.AttentionHeadCount,
                configuration.KvHeadCount,
                ToOptionalInt(configuration.DeclaredContextLength),
                configuration.FileType,
                configuration.QuantisationVersion);

            input = CompatibilityProductionInput.Create(
                modelHandoff.ModelInspectionRunId,
                productHardwareRunId,
                model,
                CompatibilityHardwareInput.Create(
                    snapshot.Memory.PhysicallyInstalledBytes,
                    dedicatedMemory,
                    snapshot.Storage.SystemVolumeAvailableBytes,
                    devices,
                    backends),
                CompatibilityFreshResourcesInput.Create(
                    freshMemory.AvailablePhysicalBytes,
                    availableDedicatedDeviceMemoryBytes: 0,
                    snapshot.Storage.SystemVolumeAvailableBytes,
                    freshMemory.CapturedAtUtc));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            input = null;
            return false;
        }
    }

    private static int? ToOptionalInt(ulong? value) =>
        value.HasValue ? checked((int)value.Value) : null;

    private static CompatibilityBackend MapBackend(LocalRuntimeBackend backend) => backend switch
    {
        LocalRuntimeBackend.Cpu => CompatibilityBackend.Cpu,
        LocalRuntimeBackend.Sycl => CompatibilityBackend.IntelSycl,
        LocalRuntimeBackend.Vulkan => CompatibilityBackend.IntelVulkan,
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };
}
