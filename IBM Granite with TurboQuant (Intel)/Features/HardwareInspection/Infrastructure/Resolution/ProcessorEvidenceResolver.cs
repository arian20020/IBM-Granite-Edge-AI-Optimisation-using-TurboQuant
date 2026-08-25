using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class ProcessorEvidenceResolver
{
    internal static ComponentResolution<ProcessorFacts> Resolve(
        LlmFitHardwareEvidence llmFit,
        WindowsProcessorEvidence windows,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(llmFit);
        ArgumentNullException.ThrowIfNull(windows);

        EvidenceFreshness llmFitFreshness = HardwareEvidenceNormalizer.GetFreshness(
            llmFit.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        EvidenceFreshness windowsFreshness = HardwareEvidenceNormalizer.GetFreshness(
            windows.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);

        bool llmFitUsable = llmFit.State == LlmFitEvidenceState.Available &&
            llmFitFreshness == EvidenceFreshness.Accepted;
        bool windowsUsable = windows.State == WindowsProcessorEvidenceState.Available &&
            windowsFreshness == EvidenceFreshness.Accepted;

        HardwareResolutionDiagnosticCode llmFitUnavailable = GetUnavailableDiagnostic(
            llmFit.State == LlmFitEvidenceState.Available,
            llmFitFreshness,
            HardwareResolutionDiagnosticCode.LlmFitUnavailable);
        HardwareResolutionDiagnosticCode windowsUnavailable = GetUnavailableDiagnostic(
            windows.State == WindowsProcessorEvidenceState.Available,
            windowsFreshness,
            HardwareResolutionDiagnosticCode.WindowsProcessorUnavailable);

        List<HardwareResolutionDiagnosticCode> diagnostics = [];
        if (!llmFitUsable)
        {
            diagnostics.Add(llmFitUnavailable);
        }

        if (!windowsUsable)
        {
            diagnostics.Add(windowsUnavailable);
        }

        bool nameConflict = llmFitUsable && windowsUsable &&
            !string.Equals(llmFit.CpuName, windows.Name, StringComparison.OrdinalIgnoreCase);
        bool topologyConflict = llmFitUsable && windowsUsable &&
            windows.PhysicalCoreCount!.Value > llmFit.CpuLogicalProcessorCount!.Value;
        bool logicalConflict = !topologyConflict && llmFitUsable && windowsUsable &&
            llmFit.CpuLogicalProcessorCount != windows.LogicalProcessorCount;

        if (nameConflict)
        {
            diagnostics.Add(HardwareResolutionDiagnosticCode.ProcessorNameConflict);
        }

        if (topologyConflict)
        {
            diagnostics.Add(HardwareResolutionDiagnosticCode.ProcessorTopologyConflict);
        }
        else if (logicalConflict)
        {
            diagnostics.Add(HardwareResolutionDiagnosticCode.LogicalProcessorConflict);
        }

        diagnostics.Add(HardwareResolutionDiagnosticCode.InstructionSetsUnavailable);

        HardwareEvidenceEntry nameEntry = ResolveNameEntry(
            llmFit,
            windows,
            llmFitUsable,
            windowsUsable,
            nameConflict,
            llmFitUnavailable);
        HardwareEvidenceEntry architectureEntry = windowsUsable
            ? Entry(
                "processor.architecture",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.ResolvedPrimary,
                windows.CapturedAtUtc,
                EvidenceConfidence.High)
            : Entry(
                "processor.architecture",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.Unavailable,
                windows.CapturedAtUtc,
                EvidenceConfidence.Low,
                windowsUnavailable);
        HardwareEvidenceEntry physicalCoreEntry = topologyConflict
            ? Entry(
                "processor.physicalCores",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.Conflict,
                windows.CapturedAtUtc,
                EvidenceConfidence.Low,
                HardwareResolutionDiagnosticCode.ProcessorTopologyConflict)
            : windowsUsable
                ? Entry(
                    "processor.physicalCores",
                    EvidenceSourceKind.Windows,
                    EvidenceResolutionState.ResolvedPrimary,
                    windows.CapturedAtUtc,
                    EvidenceConfidence.High)
                : Entry(
                    "processor.physicalCores",
                    EvidenceSourceKind.Windows,
                    EvidenceResolutionState.Unavailable,
                    windows.CapturedAtUtc,
                    EvidenceConfidence.Low,
                    windowsUnavailable);
        HardwareEvidenceEntry logicalProcessorEntry = ResolveLogicalProcessorEntry(
            llmFit,
            windows,
            llmFitUsable,
            windowsUsable,
            topologyConflict,
            logicalConflict,
            llmFitUnavailable);
        HardwareEvidenceEntry instructionSetEntry = Entry(
            "processor.instructionSets",
            EvidenceSourceKind.Windows,
            EvidenceResolutionState.Unavailable,
            resolvedAtUtc,
            EvidenceConfidence.Low,
            HardwareResolutionDiagnosticCode.InstructionSetsUnavailable);

        HardwareEvidenceEntry[] entries =
        [
            nameEntry,
            architectureEntry,
            physicalCoreEntry,
            logicalProcessorEntry,
            instructionSetEntry,
        ];

        bool criticalFailure = nameConflict || topologyConflict || logicalConflict ||
            !windowsUsable || (!llmFitUsable && !windowsUsable);
        ProcessorFacts? value = criticalFailure
            ? null
            : new ProcessorFacts(
                llmFitUsable ? llmFit.CpuName! : windows.Name!,
                MapArchitecture(windows.Architecture!.Value),
                windows.PhysicalCoreCount!.Value,
                llmFitUsable
                    ? llmFit.CpuLogicalProcessorCount!.Value
                    : windows.LogicalProcessorCount!.Value,
                []);

        return new ComponentResolution<ProcessorFacts>(
            value,
            entries,
            diagnostics,
            criticalFailure);
    }

    private static HardwareEvidenceEntry ResolveNameEntry(
        LlmFitHardwareEvidence llmFit,
        WindowsProcessorEvidence windows,
        bool llmFitUsable,
        bool windowsUsable,
        bool conflict,
        HardwareResolutionDiagnosticCode llmFitUnavailable)
    {
        if (conflict)
        {
            return Entry(
                "processor.name",
                EvidenceSourceKind.LlmFit,
                EvidenceResolutionState.Conflict,
                llmFit.CapturedAtUtc,
                EvidenceConfidence.Low,
                HardwareResolutionDiagnosticCode.ProcessorNameConflict);
        }

        if (llmFitUsable)
        {
            return Entry(
                "processor.name",
                EvidenceSourceKind.LlmFit,
                windowsUsable
                    ? EvidenceResolutionState.ResolvedCorroborated
                    : EvidenceResolutionState.ResolvedPrimary,
                llmFit.CapturedAtUtc,
                windowsUsable ? EvidenceConfidence.High : EvidenceConfidence.Medium);
        }

        if (windowsUsable)
        {
            return Entry(
                "processor.name",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.ResolvedFallback,
                windows.CapturedAtUtc,
                EvidenceConfidence.Medium);
        }

        return Entry(
            "processor.name",
            EvidenceSourceKind.LlmFit,
            EvidenceResolutionState.Unavailable,
            llmFit.CapturedAtUtc,
            EvidenceConfidence.Low,
            llmFitUnavailable);
    }

    private static HardwareEvidenceEntry ResolveLogicalProcessorEntry(
        LlmFitHardwareEvidence llmFit,
        WindowsProcessorEvidence windows,
        bool llmFitUsable,
        bool windowsUsable,
        bool topologyConflict,
        bool logicalConflict,
        HardwareResolutionDiagnosticCode llmFitUnavailable)
    {
        if (topologyConflict)
        {
            return Entry(
                "processor.logicalProcessors",
                EvidenceSourceKind.LlmFit,
                EvidenceResolutionState.Conflict,
                llmFit.CapturedAtUtc,
                EvidenceConfidence.Low,
                HardwareResolutionDiagnosticCode.ProcessorTopologyConflict);
        }

        if (logicalConflict)
        {
            return Entry(
                "processor.logicalProcessors",
                EvidenceSourceKind.LlmFit,
                EvidenceResolutionState.Conflict,
                llmFit.CapturedAtUtc,
                EvidenceConfidence.Low,
                HardwareResolutionDiagnosticCode.LogicalProcessorConflict);
        }

        if (llmFitUsable)
        {
            return Entry(
                "processor.logicalProcessors",
                EvidenceSourceKind.LlmFit,
                windowsUsable
                    ? EvidenceResolutionState.ResolvedCorroborated
                    : EvidenceResolutionState.ResolvedPrimary,
                llmFit.CapturedAtUtc,
                windowsUsable ? EvidenceConfidence.High : EvidenceConfidence.Medium);
        }

        if (windowsUsable)
        {
            return Entry(
                "processor.logicalProcessors",
                EvidenceSourceKind.Windows,
                EvidenceResolutionState.ResolvedFallback,
                windows.CapturedAtUtc,
                EvidenceConfidence.Medium);
        }

        return Entry(
            "processor.logicalProcessors",
            EvidenceSourceKind.LlmFit,
            EvidenceResolutionState.Unavailable,
            llmFit.CapturedAtUtc,
            EvidenceConfidence.Low,
            llmFitUnavailable);
    }

    private static HardwareEvidenceEntry Entry(
        string field,
        EvidenceSourceKind source,
        EvidenceResolutionState resolution,
        DateTimeOffset capturedAtUtc,
        EvidenceConfidence confidence,
        HardwareResolutionDiagnosticCode? diagnostic = null) =>
        new(
            field,
            source,
            resolution,
            capturedAtUtc,
            confidence,
            diagnostic.HasValue
                ? HardwareResolutionDiagnosticTokens.Get(diagnostic.Value)
                : null);

    private static HardwareResolutionDiagnosticCode GetUnavailableDiagnostic(
        bool stateAvailable,
        EvidenceFreshness freshness,
        HardwareResolutionDiagnosticCode unavailableDiagnostic)
    {
        if (!stateAvailable)
        {
            return unavailableDiagnostic;
        }

        return freshness switch
        {
            EvidenceFreshness.Stale => HardwareResolutionDiagnosticCode.EvidenceStale,
            EvidenceFreshness.Future => HardwareResolutionDiagnosticCode.ClockFuture,
            EvidenceFreshness.Accepted => unavailableDiagnostic,
            _ => throw new ArgumentOutOfRangeException(nameof(freshness)),
        };
    }

    private static string MapArchitecture(WindowsProcessorArchitecture architecture) =>
        architecture switch
        {
            WindowsProcessorArchitecture.X86 => "x86",
            WindowsProcessorArchitecture.X64 => "x64",
            WindowsProcessorArchitecture.Arm64 => "arm64",
            _ => throw new ArgumentOutOfRangeException(nameof(architecture)),
        };
}
