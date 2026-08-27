using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
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
                snapshot.Memory.PhysicallyInstalledBytes,
                dedicatedMemory,
                snapshot.Storage.SystemVolumeAvailableBytes,
                devices,
                [CompatibilityBackend.OpenVinoCpu]);
            OpenVinoCompatibilityModelInput model =
                OpenVinoCompatibilityModelInput.Create(
                    checked((ulong)evidence.ModelLengthBytes),
                    layerCount: null,
                    embeddingSize: null,
                    attentionHeadCount: null,
                    keyValueHeadCount: null,
                    declaredContextLimit: checked((int)evidence.ContextLength));
            OpenVinoRouteConfiguration configuration =
                OpenVinoRouteConfiguration.Create(
                    OpenVinoWeightFormat.Original,
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
                model,
                configuration,
                hardware,
                evidence.Precision);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return false;
        }
    }
}

internal sealed class PreparedOpenVinoCompatibilityInput
{
    internal PreparedOpenVinoCompatibilityInput(
        Guid modelInspectionRunId,
        Guid modelInspectionHandoffId,
        string modelSha256,
        Guid productHardwareRunId,
        OpenVinoCompatibilityModelInput model,
        OpenVinoRouteConfiguration configuration,
        CompatibilityHardwareInput hardware,
        string sourcePrecision)
    {
        ModelInspectionRunId = modelInspectionRunId;
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelSha256 = modelSha256;
        ProductHardwareRunId = productHardwareRunId;
        Model = model;
        Configuration = configuration;
        Hardware = hardware;
        SourcePrecision = sourcePrecision;
    }

    internal Guid ModelInspectionRunId { get; }
    internal Guid ModelInspectionHandoffId { get; }
    internal string ModelSha256 { get; }
    internal Guid ProductHardwareRunId { get; }
    internal OpenVinoCompatibilityModelInput Model { get; }
    internal OpenVinoRouteConfiguration Configuration { get; }
    internal CompatibilityHardwareInput Hardware { get; }
    internal string SourcePrecision { get; }
    internal CompatibilityCurrentModelInput CurrentModel =>
        CompatibilityCurrentModelInput.ForOpenVino(
            Model,
            Configuration,
            OpenVinoWeightPrecision.Fp16);

    internal string HardwareSnapshotSha256
    {
        get
        {
            string canonical = $"hardware-snapshot-v1|{ProductHardwareRunId:N}|"
                + CompatibilityFactDigest.ComputeHardware(Hardware);
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }
    }

    internal bool TryBindFresh(
        CompatibilityFreshResourcesInput fresh,
        out CompatibilityProductionInput? input)
    {
        try
        {
            input = CompatibilityProductionInput.Create(
                ModelInspectionRunId,
                ProductHardwareRunId,
                Model,
                Configuration,
                OpenVinoWeightPrecision.Fp16,
                Hardware,
                fresh);
            return true;
        }
        catch (ArgumentException)
        {
            input = null;
            return false;
        }
    }
}
