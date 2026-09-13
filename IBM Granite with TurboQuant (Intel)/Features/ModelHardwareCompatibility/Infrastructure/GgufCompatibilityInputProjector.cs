using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal static class GgufCompatibilityInputProjector
{
    private const int PinnedGraniteHMicroKvHeadCount = 8;
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
        ArgumentNullException.ThrowIfNull(freshMemory);
        input = null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan age = now - freshMemory.CapturedAtUtc;
        if (age > MaximumMemoryAge || age < -MaximumFutureSkew
            || !TryPrepare(
                modelHandoff, terminalModelResult, productHardwareRunId,
                hardwareHandoff, out PreparedGgufCompatibilityInput? prepared))
        {
            return false;
        }

        return prepared!.TryBindFresh(
            CompatibilityFreshResourcesInput.Create(
                CurrentlyAvailableMemory.FromBytes(freshMemory.AvailablePhysicalBytes),
                availableDedicatedDeviceMemoryBytes: 0,
                hardwareHandoff.Snapshot.Storage.SystemVolumeAvailableBytes,
                freshMemory.CapturedAtUtc),
            out input);
    }

    internal static bool TryPrepare(
        ModelInspectionHandoff modelHandoff,
        ModelInspectionExecutionResult terminalModelResult,
        Guid productHardwareRunId,
        HardwareInspectionHandoff hardwareHandoff,
        out PreparedGgufCompatibilityInput? prepared)
    {
        ArgumentNullException.ThrowIfNull(modelHandoff);
        ArgumentNullException.ThrowIfNull(terminalModelResult);
        ArgumentNullException.ThrowIfNull(hardwareHandoff);
        prepared = null;

        if (terminalModelResult.Status != ModelInspectionExecutionStatus.Completed
            || terminalModelResult.Result is not { } result
            || !result.CanContinueToHardwareFit
            || result.Outcome != modelHandoff.Outcome
            || !result.Evidence.File.IntegrityPreserved
            || result.Evidence.File.LengthBytes != modelHandoff.ModelLengthBytes
            || !string.Equals(result.Evidence.File.ModelSha256,
                modelHandoff.ModelSha256, StringComparison.Ordinal)
            || !string.Equals(result.Evidence.Configuration.Format,
                "GGUF", StringComparison.Ordinal)
            || productHardwareRunId == Guid.Empty
            || hardwareHandoff.InspectionId != productHardwareRunId
            || hardwareHandoff.Snapshot.Usability != HardwareSnapshotUsability.Usable)
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

            int? keyValueHeadCount = ResolveKvHeadCount(
                modelHandoff,
                configuration);

            GgufCompatibilityModelInput model = GgufCompatibilityModelInput.Create(
                checked((ulong)modelHandoff.ModelLengthBytes),
                configuration.LayerCount,
                ToOptionalInt(configuration.EmbeddingSize),
                configuration.AttentionHeadCount,
                keyValueHeadCount,
                ToOptionalInt(configuration.DeclaredContextLength),
                configuration.FileType,
                configuration.QuantisationVersion,
                ResolveParameterCount(
                    modelHandoff,
                    configuration,
                    keyValueHeadCount));

            prepared = new PreparedGgufCompatibilityInput(
                modelHandoff.ModelInspectionRunId,
                modelHandoff.ModelInspectionHandoffId,
                modelHandoff.ModelSha256,
                productHardwareRunId,
                snapshot.Identity.Sha256,
                model,
                CompatibilityHardwareInput.Create(
                    TotalPhysicalMemory.FromBytes(snapshot.Memory.PhysicallyInstalledBytes),
                    dedicatedMemory,
                    snapshot.Storage.SystemVolumeAvailableBytes,
                    devices,
                    backends));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            prepared = null;
            return false;
        }
    }

    private static int? ToOptionalInt(ulong? value) =>
        value.HasValue ? checked((int)value.Value) : null;

    private static int? ResolveKvHeadCount(
        ModelInspectionHandoff modelHandoff,
        ModelInspectionConfigurationEvidence configuration)
    {
        if (configuration.KvHeadCount is { } inspected)
        {
            return inspected;
        }

        int exactMatches = PinnedGraniteModelCatalog.Entries.Count(entry =>
            entry.ExpectedByteLength == modelHandoff.ModelLengthBytes
            && string.Equals(
                entry.ExpectedSha256,
                modelHandoff.ModelSha256,
                StringComparison.Ordinal));

        return exactMatches == 1
            ? PinnedGraniteHMicroKvHeadCount
            : null;
    }

    private static ulong? ResolveParameterCount(
        ModelInspectionHandoff modelHandoff,
        ModelInspectionConfigurationEvidence configuration,
        int? keyValueHeadCount)
    {
        if (configuration.ParameterCount is { } inspected)
        {
            return inspected;
        }

        return VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            modelHandoff.ModelSha256,
            checked((ulong)modelHandoff.ModelLengthBytes),
            configuration.LayerCount,
            ToOptionalInt(configuration.EmbeddingSize),
            configuration.AttentionHeadCount,
            keyValueHeadCount,
            ToOptionalInt(configuration.DeclaredContextLength),
            configuration.FileType,
            configuration.QuantisationVersion,
            out ulong derived)
                ? derived
                : null;
    }

    private static CompatibilityBackend MapBackend(LocalRuntimeBackend backend) => backend switch
    {
        LocalRuntimeBackend.Cpu => CompatibilityBackend.Cpu,
        LocalRuntimeBackend.Sycl => CompatibilityBackend.IntelSycl,
        LocalRuntimeBackend.Vulkan => CompatibilityBackend.IntelVulkan,
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };
}

internal sealed class PreparedGgufCompatibilityInput
{
    private readonly Guid _modelInspectionRunId;
    private readonly Guid _modelInspectionHandoffId;
    private readonly string _modelSha256;
    private readonly Guid _productHardwareRunId;
    private readonly string _hardwareSnapshotSha256;
    private readonly GgufCompatibilityModelInput _model;
    private readonly CompatibilityHardwareInput _hardware;

    internal PreparedGgufCompatibilityInput(
        Guid modelInspectionRunId,
        Guid modelInspectionHandoffId,
        string modelSha256,
        Guid productHardwareRunId,
        string hardwareSnapshotSha256,
        GgufCompatibilityModelInput model,
        CompatibilityHardwareInput hardware)
    {
        _modelInspectionRunId = modelInspectionRunId;
        _modelInspectionHandoffId = modelInspectionHandoffId;
        _modelSha256 = modelSha256;
        _productHardwareRunId = productHardwareRunId;
        _hardwareSnapshotSha256 = hardwareSnapshotSha256;
        _model = model;
        _hardware = hardware;
    }

    internal Guid ModelInspectionRunId => _modelInspectionRunId;
    internal Guid ModelInspectionHandoffId => _modelInspectionHandoffId;
    internal string ModelSha256 => _modelSha256;
    internal Guid ProductHardwareRunId => _productHardwareRunId;
    internal GgufCompatibilityModelInput Model => _model;
    internal CompatibilityHardwareInput Hardware => _hardware;

    internal CompatibilityCurrentModelInput CurrentModel =>
        CompatibilityCurrentModelInput.ForGguf(
            _model,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None));

    internal string HardwareSnapshotSha256 => _hardwareSnapshotSha256;

    internal bool TryBindFresh(
        CompatibilityFreshResourcesInput freshResources,
        out CompatibilityProductionInput? input)
    {
        ArgumentNullException.ThrowIfNull(freshResources);
        try
        {
            CompatibilityFreshResourcesInput bounded =
                CompatibilityFreshResourceNormalizer.ConstrainTo(
                    _hardware,
                    freshResources);
            input = CompatibilityProductionInput.Create(
                _modelInspectionRunId,
                _productHardwareRunId,
                _model,
                _hardware,
                bounded);
            return true;
        }
        catch (ArgumentException)
        {
            input = null;
            return false;
        }
    }
}
