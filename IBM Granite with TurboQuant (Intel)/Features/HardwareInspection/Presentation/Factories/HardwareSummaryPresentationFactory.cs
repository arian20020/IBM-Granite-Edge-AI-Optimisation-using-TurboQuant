using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;

public static class HardwareSummaryPresentationFactory
{
    public static HardwareSummaryPresentation Create(HardwareSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        List<HardwareFactPresentation> facts =
        [
            new("Processor", "Processor", snapshot.Processor.Name),
            new("Processor", "Architecture", snapshot.Processor.Architecture),
            new("Processor", "Physical cores", snapshot.Processor.PhysicalCoreCount.ToString(CultureInfo.InvariantCulture)),
            new("Processor", "Logical processors", snapshot.Processor.LogicalProcessorCount.ToString(CultureInfo.InvariantCulture)),
            new("Memory", "Installed memory", FormatBytes(snapshot.Memory.PhysicallyInstalledBytes)),
            new("Memory", "Windows-usable memory", FormatBytes(snapshot.Memory.OsUsablePhysicalBytes)),
            new("Memory", "Available now", FormatBytes(snapshot.Memory.AvailablePhysicalBytes), "Captured for this inspection run"),
        ];

        foreach (GraphicsAdapterFacts adapter in snapshot.GraphicsAdapters)
        {
            facts.Add(new("Graphics", "Graphics adapter", adapter.Name));
            AddOptionalBytes(facts, "Graphics", "Dedicated graphics memory", adapter.DedicatedVideoMemoryBytes);
            AddOptionalBytes(facts, "Graphics", "Dedicated system memory", adapter.DedicatedSystemMemoryBytes);
            AddOptionalBytes(facts, "Graphics", "Shared system memory", adapter.SharedSystemMemoryBytes);
        }

        facts.Add(new("Storage", "System storage capacity", FormatBytes(snapshot.Storage.SystemVolumeCapacityBytes)));
        facts.Add(new("Storage", "System storage available", FormatBytes(snapshot.Storage.SystemVolumeAvailableBytes)));
        facts.Add(new("Local AI tools", "Runtime build", snapshot.LocalRuntime.BuildIdentity));
        facts.Add(new(
            "Local AI tools",
            "Available routes",
            string.Join(", ", snapshot.LocalRuntime.SupportedBackends.Select(BackendLabel))));
        facts.Add(new(
            "Information sources",
            "Confirmed by",
            string.Join(", ", snapshot.Evidence.Entries.Select(entry => SourceLabel(entry.Source)).Distinct())));

        return new HardwareSummaryPresentation(facts);
    }

    private static void AddOptionalBytes(
        ICollection<HardwareFactPresentation> facts,
        string group,
        string label,
        ulong? value)
    {
        if (value.HasValue)
        {
            facts.Add(new HardwareFactPresentation(group, label, FormatBytes(value.Value)));
        }
    }

    private static string FormatBytes(ulong bytes, string? helper = null)
    {
        const decimal bytesPerGibibyte = 1_073_741_824m;
        decimal gibibytes = bytes / bytesPerGibibyte;
        string value = gibibytes == decimal.Truncate(gibibytes)
            ? gibibytes.ToString("0", CultureInfo.InvariantCulture)
            : gibibytes.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{value} GiB";
    }

    private static string BackendLabel(LocalRuntimeBackend backend) => backend switch
    {
        LocalRuntimeBackend.Cpu => "CPU",
        LocalRuntimeBackend.Sycl => "SYCL",
        LocalRuntimeBackend.Vulkan => "Vulkan",
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };

    private static string SourceLabel(EvidenceSourceKind source) => source switch
    {
        EvidenceSourceKind.LlmFit => "LLM Fit",
        EvidenceSourceKind.Windows => "Windows",
        EvidenceSourceKind.Dxgi => "DXGI",
        EvidenceSourceKind.NeuralProcessorProbe => "Neural processor probe",
        EvidenceSourceKind.LlamaCpp => "llama.cpp",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
