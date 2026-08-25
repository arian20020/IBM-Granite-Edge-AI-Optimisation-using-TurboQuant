using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class GraphicsEvidenceResolver
{
    internal static ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> Resolve(
        DxgiGraphicsEvidence dxgi,
        LlmFitHardwareEvidence llmFit,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(dxgi);
        ArgumentNullException.ThrowIfNull(llmFit);

        EvidenceFreshness dxgiFreshness = HardwareEvidenceNormalizer.GetFreshness(
            dxgi.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        EvidenceFreshness llmFitFreshness = HardwareEvidenceNormalizer.GetFreshness(
            llmFit.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        bool dxgiUsable = dxgi.State == DxgiGraphicsEvidenceState.Available &&
            dxgiFreshness == EvidenceFreshness.Accepted;
        bool llmFitUsable = llmFit.State == LlmFitEvidenceState.Available &&
            llmFitFreshness == EvidenceFreshness.Accepted;

        HardwareResolutionDiagnosticCode dxgiUnavailable = UnavailableDiagnostic(
            dxgi.State == DxgiGraphicsEvidenceState.Available,
            dxgiFreshness,
            HardwareResolutionDiagnosticCode.DxgiUnavailable);
        HardwareResolutionDiagnosticCode llmFitUnavailable = UnavailableDiagnostic(
            llmFit.State == LlmFitEvidenceState.Available,
            llmFitFreshness,
            HardwareResolutionDiagnosticCode.LlmFitUnavailable);
        List<HardwareResolutionDiagnosticCode> diagnostics = [];

        if (dxgiUsable)
        {
            if (!llmFitUsable)
            {
                diagnostics.Add(llmFitUnavailable);
            }

            ReadOnlyCollection<GraphicsAdapterFacts> canonical = Array.AsReadOnly(
                dxgi.Adapters
                    .OrderBy(static adapter => adapter.Ordinal)
                    .Select(static adapter => new GraphicsAdapterFacts(
                        adapter.Name,
                        adapter.DedicatedVideoMemoryBytes,
                        adapter.DedicatedSystemMemoryBytes,
                        adapter.SharedSystemMemoryBytes))
                    .ToArray());
            HardwareEvidenceEntry[] entries =
            [
                Entry("graphics.adapters", EvidenceSourceKind.Dxgi,
                    EvidenceResolutionState.ResolvedPrimary, dxgi.CapturedAtUtc),
                Entry("graphics.memory", EvidenceSourceKind.Dxgi,
                    EvidenceResolutionState.ResolvedPrimary, dxgi.CapturedAtUtc),
            ];
            return new(canonical, entries, diagnostics, hasCriticalFailure: false);
        }

        diagnostics.Add(dxgiUnavailable);
        bool fallbackAvailable = llmFitUsable &&
            llmFit.GpuState == LlmFitGpuDetectionState.Reported;
        if (fallbackAvailable)
        {
            List<GraphicsAdapterFacts> fallback = new(capacity: llmFit.ReportedGpuCount);
            foreach (LlmFitReportedGpu reportedGpu in llmFit.Gpus)
            {
                for (int occurrence = 0; occurrence < reportedGpu.Count; occurrence++)
                {
                    fallback.Add(new GraphicsAdapterFacts(
                        reportedGpu.Name,
                        dedicatedVideoMemoryBytes: null,
                        dedicatedSystemMemoryBytes: null,
                        sharedSystemMemoryBytes: null));
                }
            }

            HardwareEvidenceEntry[] entries =
            [
                Entry("graphics.adapters", EvidenceSourceKind.LlmFit,
                    EvidenceResolutionState.ResolvedFallback, llmFit.CapturedAtUtc),
                Entry("graphics.memory", EvidenceSourceKind.Dxgi,
                    EvidenceResolutionState.Unavailable, dxgi.CapturedAtUtc, dxgiUnavailable),
            ];
            return new(
                Array.AsReadOnly(fallback.ToArray()),
                entries,
                diagnostics,
                hasCriticalFailure: false);
        }

        if (!llmFitUsable)
        {
            diagnostics.Add(llmFitUnavailable);
        }

        diagnostics.Add(HardwareResolutionDiagnosticCode.GraphicsUnresolved);
        HardwareEvidenceEntry[] unavailableEntries =
        [
            Entry("graphics.adapters", EvidenceSourceKind.Dxgi,
                EvidenceResolutionState.Unavailable, dxgi.CapturedAtUtc,
                HardwareResolutionDiagnosticCode.GraphicsUnresolved),
            Entry("graphics.memory", EvidenceSourceKind.Dxgi,
                EvidenceResolutionState.Unavailable, dxgi.CapturedAtUtc, dxgiUnavailable),
        ];
        IReadOnlyList<GraphicsAdapterFacts> empty =
            Array.AsReadOnly(Array.Empty<GraphicsAdapterFacts>());
        return new(empty, unavailableEntries, diagnostics, hasCriticalFailure: false);
    }

    private static HardwareEvidenceEntry Entry(
        string field,
        EvidenceSourceKind source,
        EvidenceResolutionState resolution,
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode? diagnostic = null) =>
        new(
            field,
            source,
            resolution,
            capturedAtUtc,
            resolution == EvidenceResolutionState.ResolvedPrimary
                ? EvidenceConfidence.High
                : resolution == EvidenceResolutionState.ResolvedFallback
                    ? EvidenceConfidence.Medium
                    : EvidenceConfidence.Low,
            diagnostic.HasValue
                ? HardwareResolutionDiagnosticTokens.Get(diagnostic.Value)
                : null);

    private static HardwareResolutionDiagnosticCode UnavailableDiagnostic(
        bool stateAvailable,
        EvidenceFreshness freshness,
        HardwareResolutionDiagnosticCode unavailable) =>
        !stateAvailable
            ? unavailable
            : freshness switch
            {
                EvidenceFreshness.Stale => HardwareResolutionDiagnosticCode.EvidenceStale,
                EvidenceFreshness.Future => HardwareResolutionDiagnosticCode.ClockFuture,
                EvidenceFreshness.Accepted => unavailable,
                _ => throw new ArgumentOutOfRangeException(nameof(freshness)),
            };
}
