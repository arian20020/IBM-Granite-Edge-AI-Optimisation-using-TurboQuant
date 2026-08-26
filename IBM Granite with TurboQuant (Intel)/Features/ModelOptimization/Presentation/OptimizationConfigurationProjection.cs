using System;
using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.Features.ModelOptimization.Presentation;

/// <summary>
/// Projects only immutable version-three plan facts. It never reconstructs an
/// executor configuration from labels rendered by the page.
/// </summary>
internal static class OptimizationConfigurationProjection
{
    internal static OptimizationConfigurationPresentation From(
        OptimizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.ContractVersion != OptimizationExecutionPlan.CurrentContractVersion
            || !plan.IsExecutableBy(OptimizationExecutionPlan.CurrentContractVersion))
        {
            throw new ArgumentException(
                "The optimisation page requires one executable version-three plan.",
                nameof(plan));
        }

        OptimizationCandidate candidate = plan.Candidate;
        OptimizationCandidateMetrics metrics = candidate.Metrics;
        (string weights, string cache, string backend, string device,
            string offload, string flashAttention) = candidate.Configuration switch
        {
            GgufRouteConfiguration gguf =>
            (
                gguf.Weights.ToString(),
                gguf.KvCache.ToString(),
                gguf.Backend.ToString(),
                gguf.Device.ToString(),
                gguf.Offload.ToString(),
                plan.ExecutionPayload.Gguf?.FlashAttention == true ? "On" : "Off"
            ),
            OpenVinoRouteConfiguration openVino =>
            (
                openVino.Weights.ToString(),
                openVino.KvCache.ToString(),
                "OpenVINO",
                openVino.Device.ToString(),
                "Managed by the selected OpenVINO device",
                "Not applicable"
            ),
            _ => throw new ArgumentException(
                "The plan configuration has no registered presentation route.",
                nameof(plan))
        };

        bool persistent = plan.ProducesPersistentArtifact;
        return new OptimizationConfigurationPresentation(
            weights,
            cache,
            backend,
            device,
            $"{metrics.ContextTokens.ToString("N0", CultureInfo.CurrentCulture)} tokens",
            offload,
            flashAttention,
            "Included in the complete peak estimate",
            "Included in the complete peak estimate",
            "Included in the complete peak estimate",
            Bytes(metrics.PredictedPeakBytes),
            Bytes(metrics.SafeBudgetBytes),
            Bytes(metrics.HeadroomBytes),
            metrics.Evidence.ToString(),
            Tradeoff(candidate),
            "Figures remain estimates until the bounded validation run finishes.",
            persistent,
            persistent
                ? "Validated optimised model package"
                : "Validated runtime configuration",
            Bytes(metrics.WorkingDiskBytes),
            persistent ? Bytes(metrics.OutputDiskBytes) : "No new model file",
            candidate.Route == OptimizationRoute.Gguf
                ? "Validate the GGUF output, smoke test it, and reinspect it"
                : "Validate the OpenVINO package or profile, smoke test it, and reinspect it");
    }

    private static string Tradeoff(OptimizationCandidate candidate) =>
        candidate.Notice switch
        {
            OptimizationCandidateNotice.LowQuality =>
                "Uses the smallest admitted format and may noticeably reduce response quality.",
            OptimizationCandidateNotice.LowQualityRequantisation =>
                "Creates a smaller copy with a potentially noticeable quality reduction.",
            OptimizationCandidateNotice.Requantisation =>
                "Creates a smaller copy and may reduce quality compared with the source.",
            _ => $"Expected quality is {candidate.Metrics.Quality.ToString().ToLowerInvariant()}."
        };

    private static string Bytes(ulong value)
    {
        const decimal gibibyte = 1024m * 1024m * 1024m;
        const decimal mebibyte = 1024m * 1024m;
        return value >= (ulong)gibibyte
            ? $"{value / gibibyte:0.0} GB"
            : $"{value / mebibyte:0} MB";
    }
}
