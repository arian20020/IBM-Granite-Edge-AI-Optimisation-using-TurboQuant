using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class MemorySystemEvidenceResolver
{
    internal static MemorySystemResolution Resolve(
        LlmFitHardwareEvidence llmFit,
        WindowsSystemEvidenceObservation windows,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(llmFit);
        ArgumentNullException.ThrowIfNull(windows);

        EvidenceFreshness llmFitFreshness = HardwareEvidenceNormalizer.GetFreshness(
            llmFit.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        EvidenceFreshness windowsFreshness = HardwareEvidenceNormalizer.GetFreshness(
            windows.AttemptedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.DynamicMemoryMaximumAge);

        bool windowsUsable = windows.State == WindowsSystemObservationState.Available &&
            windowsFreshness == EvidenceFreshness.Accepted;
        HardwareResolutionDiagnosticCode windowsUnavailable = GetUnavailableDiagnostic(
            windows.State == WindowsSystemObservationState.Available,
            windowsFreshness,
            HardwareResolutionDiagnosticCode.WindowsSystemUnavailable);

        bool llmFitStateAndClockUsable = llmFit.State == LlmFitEvidenceState.Available &&
            llmFitFreshness == EvidenceFreshness.Accepted;
        ulong llmFitTotalBytes = 0;
        ulong llmFitAvailableBytes = 0;
        bool normalized = llmFitStateAndClockUsable &&
            HardwareEvidenceNormalizer.TryConvertGibToBytes(
                llmFit.TotalRamGiB!.Value,
                out llmFitTotalBytes) &&
            HardwareEvidenceNormalizer.TryConvertGibToBytes(
                llmFit.AvailableRamGiB!.Value,
                out llmFitAvailableBytes);
        bool llmFitUsable = llmFitStateAndClockUsable && normalized;
        HardwareResolutionDiagnosticCode llmFitUnavailable = normalized ||
            !llmFitStateAndClockUsable
                ? GetUnavailableDiagnostic(
                    llmFit.State == LlmFitEvidenceState.Available,
                    llmFitFreshness,
                    HardwareResolutionDiagnosticCode.LlmFitUnavailable)
                : HardwareResolutionDiagnosticCode.NormalizationInvalid;

        if (!windowsUsable)
        {
            List<HardwareResolutionDiagnosticCode> unavailableDiagnostics = [windowsUnavailable];
            if (!llmFitUsable)
            {
                unavailableDiagnostics.Add(llmFitUnavailable);
            }

            return FailureForUnavailableWindows(
                windows.AttemptedAtUtc,
                windowsUnavailable,
                unavailableDiagnostics);
        }

        WindowsSystemSnapshot snapshot = windows.Snapshot!;
        List<HardwareResolutionDiagnosticCode> diagnostics = [];
        bool totalConflict = false;
        bool availableConflict = false;

        if (llmFitUsable)
        {
            totalConflict = !HardwareEvidenceNormalizer.AreWithinTolerance(
                snapshot.OsUsablePhysicalBytes,
                llmFitTotalBytes,
                HardwareResolutionPolicy.InstalledMemoryToleranceBytes);
            availableConflict = !HardwareEvidenceNormalizer.AreWithinTolerance(
                snapshot.AvailablePhysicalBytes,
                llmFitAvailableBytes,
                HardwareEvidenceNormalizer.GetAvailableMemoryTolerance(
                    snapshot.OsUsablePhysicalBytes));

            if (totalConflict)
            {
                diagnostics.Add(HardwareResolutionDiagnosticCode.TotalMemoryConflict);
            }

            if (availableConflict)
            {
                diagnostics.Add(HardwareResolutionDiagnosticCode.AvailableMemoryConflict);
            }
        }
        else
        {
            diagnostics.Add(llmFitUnavailable);
        }

        EvidenceResolutionState corroboration = llmFitUsable
            ? EvidenceResolutionState.ResolvedCorroborated
            : EvidenceResolutionState.ResolvedPrimary;
        HardwareEvidenceEntry[] entries =
        [
            Entry("memory.installedBytes", EvidenceResolutionState.ResolvedPrimary,
                snapshot.CapturedAtUtc),
            totalConflict
                ? Entry("memory.osUsableBytes", EvidenceResolutionState.Conflict,
                    snapshot.CapturedAtUtc, HardwareResolutionDiagnosticCode.TotalMemoryConflict)
                : Entry("memory.osUsableBytes", corroboration, snapshot.CapturedAtUtc),
            availableConflict
                ? Entry("memory.availableBytes", EvidenceResolutionState.Conflict,
                    snapshot.CapturedAtUtc, HardwareResolutionDiagnosticCode.AvailableMemoryConflict)
                : Entry("memory.availableBytes", corroboration, snapshot.CapturedAtUtc),
            Entry("os.name", EvidenceResolutionState.ResolvedPrimary, snapshot.CapturedAtUtc),
            Entry("os.version", EvidenceResolutionState.ResolvedPrimary, snapshot.CapturedAtUtc),
            Entry("os.architecture", EvidenceResolutionState.ResolvedPrimary, snapshot.CapturedAtUtc),
        ];

        bool criticalFailure = totalConflict || availableConflict;
        MemoryFacts? memory = criticalFailure
            ? null
            : new MemoryFacts(
                snapshot.PhysicallyInstalledBytes,
                snapshot.OsUsablePhysicalBytes,
                snapshot.AvailablePhysicalBytes,
                snapshot.CapturedAtUtc);
        OperatingSystemFacts? operatingSystem = criticalFailure
            ? null
            : new OperatingSystemFacts(
                snapshot.OperatingSystemName,
                snapshot.OperatingSystemVersion,
                snapshot.OperatingSystemArchitecture);

        return new MemorySystemResolution(
            memory,
            operatingSystem,
            entries,
            diagnostics,
            criticalFailure);
    }

    private static MemorySystemResolution FailureForUnavailableWindows(
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode entryDiagnostic,
        IEnumerable<HardwareResolutionDiagnosticCode> diagnostics)
    {
        string[] fields =
        [
            "memory.installedBytes",
            "memory.osUsableBytes",
            "memory.availableBytes",
            "os.name",
            "os.version",
            "os.architecture",
        ];
        HardwareEvidenceEntry[] entries = fields
            .Select(field => Entry(
                field,
                EvidenceResolutionState.Unavailable,
                capturedAtUtc,
                entryDiagnostic))
            .ToArray();

        return new MemorySystemResolution(
            memory: null,
            operatingSystem: null,
            entries,
            diagnostics,
            hasCriticalFailure: true);
    }

    private static HardwareEvidenceEntry Entry(
        string field,
        EvidenceResolutionState resolution,
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode? diagnostic = null) =>
        new(
            field,
            EvidenceSourceKind.Windows,
            resolution,
            capturedAtUtc,
            resolution is EvidenceResolutionState.ResolvedPrimary or
                EvidenceResolutionState.ResolvedCorroborated
                ? EvidenceConfidence.High
                : EvidenceConfidence.Low,
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
}
