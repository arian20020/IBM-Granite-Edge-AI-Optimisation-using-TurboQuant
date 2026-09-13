using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal static class OpenVinoCompatibilityInputProjector
{
    internal static bool TryPrepare(
        ModelInspectionHandoff modelHandoff,
        OpenVinoStaticPackageEvidence evidence,
        Guid productHardwareRunId,
        HardwareInspectionHandoff hardwareHandoff,
        out PreparedOpenVinoCompatibilityInput? prepared)
    {
        ArgumentNullException.ThrowIfNull(modelHandoff);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(hardwareHandoff);
        prepared = null;
        if (productHardwareRunId == Guid.Empty ||
            hardwareHandoff.InspectionId != productHardwareRunId ||
            hardwareHandoff.Snapshot.Usability != HardwareSnapshotUsability.Usable ||
            evidence.ModelLengthBytes != modelHandoff.ModelLengthBytes ||
            !string.Equals(evidence.ModelSha256, modelHandoff.ModelSha256,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            HardwareSnapshot snapshot = hardwareHandoff.Snapshot;
            ulong dedicatedMemory = 0;
            HashSet<DeviceRouteId> devices = [DeviceRouteId.Cpu];
            foreach (GraphicsAdapterFacts adapter in snapshot.GraphicsAdapters)
            {
                if (adapter.DedicatedVideoMemoryBytes is { } dedicated)
                {
                    dedicatedMemory = checked(dedicatedMemory + dedicated);
                    if (dedicated > 0) devices.Add(DeviceRouteId.IntelDiscreteGpu);
                }
                if (adapter.SharedSystemMemoryBytes is > 0 ||
                    adapter.DedicatedSystemMemoryBytes is > 0)
                {
                    devices.Add(DeviceRouteId.IntelIntegratedGpu);
                }
            }
            if (snapshot.NeuralProcessor.State == NpuDetectionState.Present)
            {
                devices.Add(DeviceRouteId.IntelNpu);
            }

            CompatibilityHardwareInput hardware = CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(snapshot.Memory.PhysicallyInstalledBytes),
                dedicatedMemory,
                snapshot.Storage.SystemVolumeAvailableBytes,
                devices,
                [CompatibilityBackend.OpenVinoCpu]);
            ulong? parameterCount =
                VerifiedOpenVinoOptimizationEvidence.TryResolveParameterCount(
                    evidence.ModelSha256,
                    out ulong verifiedParameterCount)
                    ? verifiedParameterCount
                    : null;
            OpenVinoCompatibilityModelInput model =
                OpenVinoCompatibilityModelInput.Create(
                    checked((ulong)evidence.ModelLengthBytes),
                    evidence.LayerCount,
                    evidence.EmbeddingSize,
                    evidence.AttentionHeadCount,
                    evidence.KeyValueHeadCount,
                    declaredContextLimit: checked((int)evidence.ContextLength),
                    parameterCount);
            string sourcePrecision = evidence.WeightPrecision ?? evidence.Precision;
            if (!TryMapPrecision(
                    sourcePrecision,
                    out OpenVinoWeightFormat weightFormat,
                    out OpenVinoWeightPrecision weightPrecision))
            {
                return false;
            }
            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    weightFormat,
                    OpenVinoKvCacheFormat.RouteDefault,
                    DeviceRouteId.Cpu,
                    OpenVinoPerformanceHint.Latency,
                    OpenVinoCompiledCachePolicy.Disabled,
                    streams: 1);
            prepared = new PreparedOpenVinoCompatibilityInput(
                modelHandoff.ModelInspectionRunId,
                modelHandoff.ModelInspectionHandoffId,
                modelHandoff.ModelSha256,
                productHardwareRunId,
                snapshot.Identity.Sha256,
                model,
                configuration,
                hardware,
                sourcePrecision,
                weightPrecision,
                evidence.PackageManifestDigest);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryMapPrecision(
        string value,
        out OpenVinoWeightFormat format,
        out OpenVinoWeightPrecision precision)
    {
        string normalized = value.Trim().ToLowerInvariant();
        (format, precision) = normalized switch
        {
            "float16" or "fp16" or "f16" =>
                (OpenVinoWeightFormat.Original, OpenVinoWeightPrecision.Fp16),
            "int8" or "uint8" or "i8" or "u8" =>
                (OpenVinoWeightFormat.Int8, OpenVinoWeightPrecision.EightBit),
            "int4" or "uint4" or "i4" or "u4" =>
                (OpenVinoWeightFormat.Int4, OpenVinoWeightPrecision.FourBit),
            "mxfp4" =>
                (OpenVinoWeightFormat.MxFp4, OpenVinoWeightPrecision.MxFp4),
            _ => (OpenVinoWeightFormat.Unspecified, default),
        };
        return format != OpenVinoWeightFormat.Unspecified;
    }
}

internal sealed class PreparedOpenVinoCompatibilityInput
{
    internal PreparedOpenVinoCompatibilityInput(
        Guid modelInspectionRunId,
        Guid modelInspectionHandoffId,
        string modelSha256,
        Guid productHardwareRunId,
        string hardwareSnapshotSha256,
        OpenVinoCompatibilityModelInput model,
        OpenVinoRouteConfiguration configuration,
        CompatibilityHardwareInput hardware,
        string sourcePrecision,
        OpenVinoWeightPrecision sourceWeightPrecision,
        string? packageManifestSha256 = null)
    {
        ModelInspectionRunId = modelInspectionRunId;
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelSha256 = modelSha256;
        ProductHardwareRunId = productHardwareRunId;
        HardwareSnapshotSha256 = hardwareSnapshotSha256;
        Model = model;
        Configuration = configuration;
        Hardware = hardware;
        SourcePrecision = sourcePrecision;
        SourceWeightPrecision = sourceWeightPrecision;
        PackageManifestSha256 = packageManifestSha256;
    }

    internal Guid ModelInspectionRunId { get; }
    internal Guid ModelInspectionHandoffId { get; }
    internal string ModelSha256 { get; }
    internal string? PackageManifestSha256 { get; }
    internal Guid ProductHardwareRunId { get; }
    internal OpenVinoCompatibilityModelInput Model { get; }
    internal OpenVinoRouteConfiguration Configuration { get; }
    internal CompatibilityHardwareInput Hardware { get; }
    internal string SourcePrecision { get; }
    internal OpenVinoWeightPrecision SourceWeightPrecision { get; }
    internal CompatibilityCurrentModelInput CurrentModel =>
        CompatibilityCurrentModelInput.ForOpenVino(
            Model,
            Configuration,
            SourceWeightPrecision);

    internal string HardwareSnapshotSha256 { get; }

    internal bool TryBindFresh(
        CompatibilityFreshResourcesInput fresh,
        out CompatibilityProductionInput? input)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        try
        {
            CompatibilityFreshResourcesInput bounded =
                CompatibilityFreshResourceNormalizer.ConstrainTo(Hardware, fresh);
            input = CompatibilityProductionInput.Create(
                ModelInspectionRunId,
                ProductHardwareRunId,
                Model,
                Configuration,
                SourceWeightPrecision,
                Hardware,
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

internal static class CompatibilityFreshResourceNormalizer
{
    internal static CompatibilityFreshResourcesInput ConstrainTo(
        CompatibilityHardwareInput hardware,
        CompatibilityFreshResourcesInput fresh)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(fresh);
        ulong? dedicated = fresh.DedicatedDeviceMemoryEstablished
            ? Math.Min(
                fresh.AvailableDedicatedDeviceMemoryBytes,
                hardware.InstalledDedicatedDeviceMemoryBytes)
            : null;
        return CompatibilityFreshResourcesInput.Create(
            CurrentlyAvailableMemory.FromBytes(Math.Min(
                fresh.AvailableSystemMemoryBytes,
                hardware.InstalledSystemMemoryBytes)),
            dedicated,
            Math.Min(fresh.AvailableStorageBytes, hardware.FreeStorageBytes),
            fresh.ObservedAtUtc);
    }
}
