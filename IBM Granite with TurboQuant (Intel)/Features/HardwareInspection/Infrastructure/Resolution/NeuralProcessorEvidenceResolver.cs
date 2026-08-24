using System;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal static class NeuralProcessorEvidenceResolver
{
    internal static ComponentResolution<NeuralProcessorFacts> Resolve(
        NeuralProcessorEvidence evidence,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        EvidenceFreshness freshness = HardwareEvidenceNormalizer.GetFreshness(
            evidence.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        if (freshness != EvidenceFreshness.Accepted)
        {
            HardwareResolutionDiagnosticCode diagnostic = freshness == EvidenceFreshness.Stale
                ? HardwareResolutionDiagnosticCode.EvidenceStale
                : HardwareResolutionDiagnosticCode.ClockFuture;
            return Unavailable(evidence.CapturedAtUtc, diagnostic);
        }

        return evidence.State switch
        {
            NeuralProcessorEvidenceState.Present => Resolved(
                new NeuralProcessorFacts(NpuDetectionState.Present, evidence.Name),
                evidence.CapturedAtUtc),
            NeuralProcessorEvidenceState.NotPresent => Resolved(
                new NeuralProcessorFacts(NpuDetectionState.NotPresent, name: null),
                evidence.CapturedAtUtc),
            NeuralProcessorEvidenceState.DetectionUnavailable => Unavailable(
                evidence.CapturedAtUtc,
                HardwareResolutionDiagnosticCode.NeuralProcessorUnavailable),
            _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
        };
    }

    private static ComponentResolution<NeuralProcessorFacts> Resolved(
        NeuralProcessorFacts value,
        DateTimeOffset capturedAtUtc) =>
        new(
            value,
            [new HardwareEvidenceEntry(
                "npu.state",
                EvidenceSourceKind.NeuralProcessorProbe,
                EvidenceResolutionState.ResolvedPrimary,
                capturedAtUtc,
                EvidenceConfidence.High,
                safeDiagnosticCode: null)],
            [],
            hasCriticalFailure: false);

    private static ComponentResolution<NeuralProcessorFacts> Unavailable(
        DateTimeOffset capturedAtUtc,
        HardwareResolutionDiagnosticCode diagnostic) =>
        new(
            new NeuralProcessorFacts(NpuDetectionState.DetectionUnavailable, name: null),
            [new HardwareEvidenceEntry(
                "npu.state",
                EvidenceSourceKind.NeuralProcessorProbe,
                EvidenceResolutionState.Unavailable,
                capturedAtUtc,
                EvidenceConfidence.Low,
                HardwareResolutionDiagnosticTokens.Get(diagnostic))],
            [diagnostic],
            hasCriticalFailure: false);
}
