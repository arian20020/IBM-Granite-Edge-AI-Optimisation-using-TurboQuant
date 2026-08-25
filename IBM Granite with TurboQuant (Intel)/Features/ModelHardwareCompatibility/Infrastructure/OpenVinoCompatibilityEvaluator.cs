using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal sealed record OpenVinoCompatibilityEvaluation(
    CompatibilityPresentation Presentation,
    OptimizationCandidate? Candidate,
    OptimizationExecutionPlan? Plan,
    OptimizationCapabilitySnapshot? CapabilitySnapshot,
    OpenVinoOptimizationCapabilityEvidence? CapabilityEvidence,
    OptimizationWorkload? Workload,
    InspectedModelFacts? ModelFacts);

internal static class OpenVinoCompatibilityEvaluator
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private static readonly TimeSpan MaximumMemoryAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromSeconds(5);

    internal static OpenVinoCompatibilityEvaluation Evaluate(
        ModelInspectionHandoffV2 handoff,
        OpenVinoStaticPackageEvidence facts,
        OpenVinoBuildEvidence builds,
        HardwareInspectionHandoff hardware,
        AvailableMemorySnapshot freshMemory)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(builds);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(freshMemory);

        try
        {
            handoff.Validate();
            builds.Validate();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan age = now - freshMemory.CapturedAtUtc;
            if (hardware.InspectionId == Guid.Empty ||
                hardware.Snapshot.Usability != HardwareSnapshotUsability.Usable ||
                age > MaximumMemoryAge || age < -MaximumFutureSkew ||
                !string.Equals(handoff.ModelSha256, facts.ModelSha256,
                    StringComparison.Ordinal) ||
                handoff.ModelLengthBytes != facts.ModelLengthBytes)
            {
                return NotEstablished();
            }

            InspectedModelFacts modelFacts = InspectedModelFacts.Create(
                ByteCount.FromBytes(checked((ulong)handoff.ModelLengthBytes)),
                facts.LayerCount,
                facts.EmbeddingSize,
                facts.AttentionHeadCount,
                facts.KeyValueHeadCount,
                checked((int)facts.ContextLength),
                fileType: null,
                quantisationVersion: null);
            OpenVinoOptimizationCapabilityEvidence capabilityEvidence =
                OpenVinoOfficialCapabilityEvidenceFactory.Create(builds);
            OpenVinoCapabilityPayload payload =
                OpenVinoOptimizationCapabilityProjector.Project(capabilityEvidence);
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    "openvino-official-cpu-v1",
                    CapabilityDigest(payload),
                    payload);
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "local-chat",
                minimumContextTokens: 512,
                OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(
                    Math.Min(4_096, checked((int)facts.ContextLength)))]);

            ulong baseSafeBudget = freshMemory.AvailablePhysicalBytes > 3 * Gibibyte
                ? freshMemory.AvailablePhysicalBytes - 3 * Gibibyte
                : 0;
            CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
                snapshot,
                modelFacts,
                workload,
                ByteCount.FromBytes(baseSafeBudget),
                ByteCount.FromBytes(hardware.Snapshot.Storage.SystemVolumeAvailableBytes),
                EstimatorPolicy.ProvisionalV1(),
                new HashSet<string>(StringComparer.Ordinal));

            List<OptimizationCandidate> safe = [];
            foreach (OptimizationCandidate candidate in generated.Candidates)
            {
                ulong margin = Math.Max(
                    Gibibyte / 2,
                    checked((ulong)Math.Ceiling(
                        candidate.Metrics.PredictedPeakBytes * 0.10m)));
                ulong required = checked(candidate.Metrics.PredictedPeakBytes + margin);
                if (required > baseSafeBudget)
                {
                    continue;
                }

                safe.Add(OptimizationCandidate.Create(
                    candidate.Configuration,
                    OptimizationCandidateMetrics.Create(
                        candidate.Metrics.Evidence,
                        candidate.Metrics.Quality,
                        candidate.Metrics.Performance,
                        candidate.Metrics.Stability,
                        candidate.Metrics.ContextTokens,
                        required,
                        baseSafeBudget,
                        baseSafeBudget - required,
                        candidate.Metrics.WorkingDiskBytes,
                        candidate.Metrics.OutputDiskBytes,
                        candidate.Metrics.RequiresPersistentChange),
                    candidate.EvidenceId,
                    candidate.IsExperimental));
            }

            OptimizationSelection? selection = OptimizationPreferenceResolver.Resolve(
                safe,
                OptimizationPreferenceSelection.Automatic());
            if (selection is null)
            {
                bool unestablished = generated.Exclusions.Any(exclusion =>
                    exclusion.Reason == OptimizationExclusionReason.EstimateNotEstablished);
                return new OpenVinoCompatibilityEvaluation(
                    unestablished ? MissingEvidence() : NoSafeConfiguration(),
                    null,
                    null,
                    snapshot,
                    capabilityEvidence,
                    workload,
                    modelFacts);
            }

            OptimizationExecutionPlan? plan = null;
            if (string.Equals(facts.Precision, "float16", StringComparison.Ordinal))
            {
                OptimizationExecutionPayload executionPayload =
                    OpenVinoExecutionPayloadFactory.Create(selection.Candidate, builds);
                plan = OptimizationPlanIssuer.Issue(
                    selection,
                    executionPayload,
                    snapshot,
                    workload,
                    OptimizationJourneyBinding.Create(
                        handoff.ModelInspectionRunId.ToString("D"),
                        handoff.ModelInspectionHandoffId.ToString("D"),
                        handoff.ModelSha256,
                        checked((ulong)handoff.ModelLengthBytes),
                        hardware.InspectionId.ToString("D"),
                        HardwareSnapshotIdentity.Sha256(hardware.Snapshot)),
                    facts.LayerCount,
                    now);
            }

            return new OpenVinoCompatibilityEvaluation(
                Ready(selection.Candidate, modelFacts, plan is not null),
                selection.Candidate,
                plan,
                snapshot,
                capabilityEvidence,
                workload,
                modelFacts);
        }
        catch (Exception exception) when (exception is ArgumentException or
            InvalidOperationException or OverflowException)
        {
            return NotEstablished();
        }
    }

    private static CompatibilityPresentation Ready(
        OptimizationCandidate candidate,
        InspectedModelFacts facts,
        bool canContinue)
    {
        var configuration = (OpenVinoRouteConfiguration)candidate.Configuration;
        ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
            facts,
            configuration,
            ContextTokenCount.FromTokens(candidate.Metrics.ContextTokens),
            EstimatorPolicy.ProvisionalV1());
        ulong weights = Bytes(estimate, ResourceComponentKind.Weights);
        ulong kv = Bytes(estimate, ResourceComponentKind.KvCache);
        ulong runtime = estimate.Components
            .Where(component => component.Target != ResourceTarget.Storage &&
                component.Kind is not ResourceComponentKind.Weights and
                    not ResourceComponentKind.KvCache)
            .Aggregate(0UL, (sum, component) => checked(sum + component.Bytes.Bytes));
        ulong named = checked(weights + kv + runtime);
        ulong margin = candidate.Metrics.PredictedPeakBytes > named
            ? candidate.Metrics.PredictedPeakBytes - named
            : 0;
        bool narrow = candidate.Metrics.PredictedPeakBytes * 10 >
            candidate.Metrics.SafeBudgetBytes * 9;
        string precision = configuration.Weights switch
        {
            OpenVinoWeightFormat.Original => "Original package precision",
            OpenVinoWeightFormat.Fp16 => "FP16",
            OpenVinoWeightFormat.Int8 => "INT8",
            OpenVinoWeightFormat.Int4 => "INT4",
            _ => "Unsupported"
        };
        string cache = configuration.KvCache == OpenVinoKvCacheFormat.U8
            ? "U8 KV cache"
            : "OpenVINO default KV cache";

        return CompatibilityPresentation.Empty with
        {
            PageLede = "Based on this OpenVINO package and memory available right now.",
            ModelDetail = "OpenVINO GenAI package",
            Tone = narrow ? CompatibilityOutcomeTone.Caution : CompatibilityOutcomeTone.Positive,
            OutcomeTitle = narrow
                ? "This OpenVINO setup should run, but memory is tight"
                : "Yes — this OpenVINO model should run",
            OutcomeDetail = "This is an estimate from verified package facts and fresh Windows memory, not a test run.",
            OutcomeBadge = "ESTIMATE",
            Facts =
            [
                new CompatibilityFact("Memory needed", CompatibilityBudget.Describe(candidate.Metrics.PredictedPeakBytes), "Estimated peak"),
                new CompatibilityFact("Memory allowed", CompatibilityBudget.Describe(candidate.Metrics.SafeBudgetBytes), CompatibilityBudget.Describe(candidate.Metrics.HeadroomBytes) + " spare"),
                new CompatibilityFact("Context", $"{candidate.Metrics.ContextTokens:N0} tokens", "Local chat workload"),
                new CompatibilityFact("Runs on", "Processor", "Official OpenVINO CPU route")
            ],
            Budget = CompatibilityBudget.Create(
                [
                    new CompatibilityBudgetSegment("Model weights", weights, false),
                    new CompatibilityBudgetSegment("KV cache", kv, false),
                    new CompatibilityBudgetSegment("Runtime and buffers", runtime, false),
                    new CompatibilityBudgetSegment("Margin for error", margin, false)
                ],
                candidate.Metrics.SafeBudgetBytes),
            EstimateSummary = new CompatibilityEstimateSummary(
                weights,
                kv,
                runtime,
                margin,
                candidate.Metrics.PredictedPeakBytes,
                candidate.Metrics.SafeBudgetBytes),
            RuntimeCardTitle = "What would run",
            RuntimeRows =
            [
                new CompatibilityRow("Engine", "OpenVINO GenAI", string.Empty, CompatibilityOutcomeTone.Neutral, false),
                new CompatibilityRow("Device", "CPU", "Supported", CompatibilityOutcomeTone.Positive, true),
                new CompatibilityRow("Weights", precision, candidate.Metrics.RequiresPersistentChange ? "Creates a converted package" : "Runtime only", candidate.Metrics.RequiresPersistentChange ? CompatibilityOutcomeTone.Caution : CompatibilityOutcomeTone.Neutral, true),
                new CompatibilityRow("Cache", cache, "Supported", CompatibilityOutcomeTone.Positive, true)
            ],
            ChecksCardTitle = "What we checked",
            CheckRows =
            [
                new CompatibilityRow("Package", "Verified OpenVINO inspection evidence", "Passed", CompatibilityOutcomeTone.Positive, true),
                new CompatibilityRow("Hardware", "Fresh available physical memory", "Checked", CompatibilityOutcomeTone.Positive, true),
                new CompatibilityRow("Capability", candidate.EvidenceId, "Official", CompatibilityOutcomeTone.Positive, true)
            ],
            DisclosureTitle = "How we worked this out",
            DisclosureDetail = "The estimate uses the OpenVINO model structure, the exact admitted OpenVINO CPU capability, and memory available at this check. TurboQuant is not claimed.",
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = canContinue,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };
    }

    private static ulong Bytes(ResourceEstimate estimate, ResourceComponentKind kind) =>
        estimate.Components
            .Where(component => component.Target != ResourceTarget.Storage &&
                component.Kind == kind)
            .Aggregate(0UL, (sum, component) => checked(sum + component.Bytes.Bytes));

    private static OpenVinoCompatibilityEvaluation NotEstablished() =>
        new(MissingEvidence(), null, null, null, null, null, null);

    private static CompatibilityPresentation MissingEvidence() =>
        CompatibilityPresentation.Empty with
        {
            Tone = CompatibilityOutcomeTone.Caution,
            OutcomeTitle = "OpenVINO compatibility could not be established",
            OutcomeDetail = "The package, hardware, memory, or exact OpenVINO capability evidence is missing or stale. Run both inspections again.",
            OutcomeBadge = "NO ANSWER YET",
            Recoveries = [new CompatibilityRecovery("Inspect again", "Return to model inspection, then rerun the shared hardware inspection.")],
            SecondaryActionText = "Back"
        };

    private static CompatibilityPresentation NoSafeConfiguration() =>
        CompatibilityPresentation.Empty with
        {
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "No official OpenVINO CPU setup fits safely",
            OutcomeDetail = "Every admitted OpenVINO configuration exceeds the safe memory or storage allowance right now.",
            OutcomeBadge = "ESTIMATE",
            Recoveries = [new CompatibilityRecovery("Free memory and check again", "Close unused applications, then rerun hardware inspection so available memory is measured again.")],
            SecondaryActionText = "Back"
        };

    internal static string CapabilityDigest(OpenVinoCapabilityPayload payload)
    {
        StringBuilder canonical = new(payload.RuntimeVersion);
        foreach (OpenVinoAdmittedConfiguration entry in payload.Admitted)
        {
            canonical.Append('|').Append(entry.EvidenceId)
                .Append('|').Append((int)entry.Weights)
                .Append('|').Append((int)entry.KvCache)
                .Append('|').Append((int)entry.Device)
                .Append('|').Append((int)entry.PerformanceHint)
                .Append('|').Append((int)entry.CompiledCache)
                .Append('|').Append(entry.Streams)
                .Append('|').Append(entry.MinimumContextTokens)
                .Append('|').Append(entry.MaximumContextTokens)
                .Append('|').Append((int)entry.Level);
        }
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}
