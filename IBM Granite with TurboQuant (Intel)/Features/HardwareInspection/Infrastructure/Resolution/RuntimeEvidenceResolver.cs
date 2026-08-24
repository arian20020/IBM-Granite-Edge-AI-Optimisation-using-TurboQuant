using System;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class RuntimeEvidenceResolver
{
    internal static ComponentResolution<LocalRuntimeCapabilities> Resolve(
        LlamaCppCapabilityEvidence evidence,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        EvidenceFreshness freshness = HardwareEvidenceNormalizer.GetFreshness(
            evidence.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        bool usable = evidence.State == LlamaCppCapabilityEvidenceState.Available &&
            freshness == EvidenceFreshness.Accepted;

        if (!usable)
        {
            HardwareResolutionDiagnosticCode diagnostic = evidence.State !=
                LlamaCppCapabilityEvidenceState.Available
                    ? HardwareResolutionDiagnosticCode.LlamaCppUnavailable
                    : freshness == EvidenceFreshness.Stale
                        ? HardwareResolutionDiagnosticCode.EvidenceStale
                        : HardwareResolutionDiagnosticCode.ClockFuture;
            return new(
                value: null,
                UnavailableEntries(evidence.CapturedAtUtc, diagnostic),
                [diagnostic],
                hasCriticalFailure: true);
        }

        LlamaCppRuntimeIdentity identity = evidence.RuntimeIdentity!;
        string buildIdentity = string.Concat(
            identity.ManagedPackage,
            "/",
            identity.ManagedVersion,
            ";",
            identity.BackendPackage,
            "/",
            identity.BackendVersion,
            ";llama.cpp/",
            identity.MappedLlamaCppCommit,
            ";",
            identity.RuntimeIdentifier);
        LocalRuntimeCapabilities capabilities = new(
            buildIdentity,
            evidence.Backends.Select(MapBackend),
            evidence.VisibleDevices
                .OrderBy(static device => device.Ordinal)
                .Select(static device => device.BufferType));

        return new(
            capabilities,
            ResolvedEntries(evidence.CapturedAtUtc),
            [],
            hasCriticalFailure: false);
    }

    private static LocalRuntimeBackend MapBackend(LlamaCppBackend backend) => backend switch
    {
        LlamaCppBackend.Cpu => LocalRuntimeBackend.Cpu,
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };

    private static HardwareEvidenceEntry[] ResolvedEntries(DateTimeOffset capturedAtUtc) =>
    [
        Entry("runtime.buildIdentity", EvidenceResolutionState.ResolvedPrimary, capturedAtUtc),
        Entry("runtime.backends", EvidenceResolutionState.ResolvedPrimary, capturedAtUtc),
        Entry("runtime.visibleDevices", EvidenceResolutionState.ResolvedPrimary, capturedAtUtc),
    ];

    private static HardwareEvidenceEntry[] UnavailableEntries(
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode diagnostic) =>
    [
        Entry("runtime.buildIdentity", EvidenceResolutionState.Unavailable, capturedAtUtc, diagnostic),
        Entry("runtime.backends", EvidenceResolutionState.Unavailable, capturedAtUtc, diagnostic),
        Entry("runtime.visibleDevices", EvidenceResolutionState.Unavailable, capturedAtUtc, diagnostic),
    ];

    private static HardwareEvidenceEntry Entry(
        string field,
        EvidenceResolutionState resolution,
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode? diagnostic = null) =>
        new(
            field,
            EvidenceSourceKind.LlamaCpp,
            resolution,
            capturedAtUtc,
            resolution == EvidenceResolutionState.ResolvedPrimary
                ? EvidenceConfidence.High
                : EvidenceConfidence.Low,
            diagnostic.HasValue
                ? HardwareResolutionDiagnosticTokens.Get(diagnostic.Value)
                : null);
}
