using System;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class StorageEvidenceResolver
{
    internal static ComponentResolution<StorageFacts> Resolve(
        WindowsStorageEvidence evidence,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        EvidenceFreshness freshness = HardwareEvidenceNormalizer.GetFreshness(
            evidence.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        bool usable = evidence.State == WindowsStorageEvidenceState.Available &&
            freshness == EvidenceFreshness.Accepted;

        if (!usable)
        {
            HardwareResolutionDiagnosticCode diagnostic = evidence.State !=
                WindowsStorageEvidenceState.Available
                    ? HardwareResolutionDiagnosticCode.StorageUnavailable
                    : freshness == EvidenceFreshness.Stale
                        ? HardwareResolutionDiagnosticCode.EvidenceStale
                        : HardwareResolutionDiagnosticCode.ClockFuture;
            return new(
                value: null,
                UnavailableEntries(evidence.CapturedAtUtc, diagnostic),
                [diagnostic],
                hasCriticalFailure: true);
        }

        return new(
            new StorageFacts(
                evidence.CapacityBytes!.Value,
                evidence.AvailableToCallerBytes!.Value),
            ResolvedEntries(evidence.CapturedAtUtc),
            [],
            hasCriticalFailure: false);
    }

    private static HardwareEvidenceEntry[] ResolvedEntries(DateTimeOffset capturedAtUtc) =>
    [
        Entry("storage.systemVolumeCapacityBytes", EvidenceResolutionState.ResolvedPrimary,
            capturedAtUtc),
        Entry("storage.systemVolumeAvailableBytes", EvidenceResolutionState.ResolvedPrimary,
            capturedAtUtc),
    ];

    private static HardwareEvidenceEntry[] UnavailableEntries(
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode diagnostic) =>
    [
        Entry("storage.systemVolumeCapacityBytes", EvidenceResolutionState.Unavailable,
            capturedAtUtc, diagnostic),
        Entry("storage.systemVolumeAvailableBytes", EvidenceResolutionState.Unavailable,
            capturedAtUtc, diagnostic),
    ];

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
            resolution == EvidenceResolutionState.ResolvedPrimary
                ? EvidenceConfidence.High
                : EvidenceConfidence.Low,
            diagnostic.HasValue
                ? HardwareResolutionDiagnosticTokens.Get(diagnostic.Value)
                : null);
}
