using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// The only way a plan comes into existence.
///
/// Every plan gets a fresh id. There is no amendment: a changed input produces a
/// new plan the user re-confirms, because the alternative is an executor running
/// something subtly different from what was agreed to while carrying the id of
/// what was.
///
/// Issuance is where the candidate and the execution payload are proved to
/// agree. They are built by different code from different inputs, and if they
/// were allowed to disagree the plan would carry two answers to the same
/// question - which is worse than carrying none, because both look authoritative.
///
/// The issuer is pure. It takes the time as an argument rather than reading a
/// clock, so the same inputs produce the same plan in a test and in production.
/// </summary>
public static class OptimizationPlanIssuer
{
    /// <summary>
    /// Issues a plan for a candidate that was actually selected, together with
    /// the exact settings its executor will use.
    ///
    /// <paramref name="modelLayerCount"/> is required because an exact GPU layer
    /// count can only be checked against the coarse offload category if the
    /// model's layer count is known. It comes from the inspected facts, so it is
    /// a measured value rather than an assumption.
    /// </summary>
    public static OptimizationExecutionPlan Issue(
        OptimizationSelection selection,
        OptimizationExecutionPayload executionPayload,
        OptimizationCapabilitySnapshot capabilitySnapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        int modelLayerCount,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(executionPayload);
        ArgumentNullException.ThrowIfNull(capabilitySnapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);

        OptimizationCandidate candidate = selection.Candidate;

        if (candidate.Route != capabilitySnapshot.Route)
        {
            throw new ArgumentException(
                $"The candidate is a {candidate.Route} configuration but the "
                + $"capability snapshot describes {capabilitySnapshot.Route}. A plan "
                + "bound to evidence about a different executor could not be verified "
                + "by either.",
                nameof(capabilitySnapshot));
        }

        if (candidate.Route != executionPayload.Route)
        {
            throw new ArgumentException(
                $"The candidate is a {candidate.Route} configuration but the "
                + $"execution payload is for {executionPayload.Route}. One of the two "
                + "would have to be reinterpreted, and reinterpretation is the "
                + "substitution this contract exists to prevent.",
                nameof(executionPayload));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Plan timestamps are UTC. A local offset would make two plans issued "
                + "at the same instant compare as different.",
                nameof(createdAtUtc));
        }

        RequireAgreement(candidate, executionPayload, modelLayerCount);

        return new OptimizationExecutionPlan(
            Guid.NewGuid(),
            binding,
            capabilitySnapshot,
            workload,
            candidate,
            executionPayload,
            selection.Preference,
            selection.SharedWithAdjacentBand,
            OptimizationCanonicalizer.ConfigurationSha256(candidate, executionPayload),
            createdAtUtc);
    }

    /// <summary>
    /// Proves the payload implements the candidate rather than something near it.
    ///
    /// Every field checked here appears on both sides. Where a value exists in
    /// only one of them - a thread count, a batch size - there is nothing to
    /// disagree with and nothing to check; those are validated for shape by the
    /// payload itself.
    /// </summary>
    private static void RequireAgreement(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        int modelLayerCount)
    {
        // The one fact the confirmation surface must not get wrong. Checked for
        // both routes before anything route-specific, because it is the same
        // promise either way.
        if (candidate.Metrics.RequiresPersistentChange != payload.RequiresPersistentConversion)
        {
            throw new ArgumentException(
                candidate.Metrics.RequiresPersistentChange
                    ? "The candidate creates a new model but the payload converts "
                      + "nothing, so the plan would promise a file it never writes."
                    : "The payload converts the model but the candidate claims no "
                      + "persistent change, so the user would be shown a runtime-only "
                      + "choice and given a new file.",
                nameof(payload));
        }

        switch (candidate.Configuration)
        {
            case GgufRouteConfiguration gguf:
                RequireGgufAgreement(gguf, candidate, payload.Gguf!, modelLayerCount);
                break;

            case OpenVinoRouteConfiguration openVino:
                RequireOpenVinoAgreement(openVino, candidate, payload.OpenVino!);
                break;

            default:
                throw new ArgumentException(
                    $"{candidate.Configuration.GetType().Name} belongs to no known "
                    + "route, so no payload could be checked against it.",
                    nameof(candidate));
        }
    }

    private static void RequireGgufAgreement(
        GgufRouteConfiguration configuration,
        OptimizationCandidate candidate,
        GgufExecutionPayload payload,
        int modelLayerCount)
    {
        Require(
            ExecutionVocabularyMap.ToRuntimeBackend(configuration.Backend) == payload.Backend,
            "backend",
            $"{configuration.Backend} against {payload.Backend}");

        Require(
            ExecutionVocabularyMap.ToDeviceId(configuration.Device) == payload.DeviceId,
            "device",
            $"{configuration.Device} against {payload.DeviceId}");

        // Both halves of the cache. The candidate names one format because the
        // support matrix admits one; the runtime configures key and value
        // separately, and both must be the admitted one.
        GgufCacheType admitted = ExecutionVocabularyMap.ToCacheType(configuration.KvCache);

        Require(
            payload.KeyCacheType == admitted && payload.ValueCacheType == admitted,
            "cache",
            $"{configuration.KvCache} against key {payload.KeyCacheType} and value "
                + $"{payload.ValueCacheType}");

        Require(
            configuration.Weights == payload.PersistentTargetWeightFormat,
            "weight format",
            $"{configuration.Weights} against {payload.PersistentTargetWeightFormat}");

        Require(
            candidate.Metrics.ContextTokens == payload.ContextSize,
            "context",
            $"{candidate.Metrics.ContextTokens} against {payload.ContextSize}");

        // The exact count has to be the one the category implies, computed the
        // one deterministic way. Otherwise the plan shows a category and the
        // executor runs a different placement.
        Require(
            GgufOffloadPolicy.Agrees(
                configuration.Offload, modelLayerCount, payload.GpuLayerCount),
            "GPU offload",
            $"{configuration.Offload} over {modelLayerCount} layers implies "
                + $"{GgufOffloadPolicy.ExactLayerCount(configuration.Offload, modelLayerCount)} "
                + $"but the payload says {payload.GpuLayerCount}");
    }

    private static void RequireOpenVinoAgreement(
        OpenVinoRouteConfiguration configuration,
        OptimizationCandidate candidate,
        OpenVinoExecutionPayload payload)
    {
        // The published OpenVINO route defines Fp16, EightBit and FourBit only -
        // there is no "Original" precision in it. A runtime-only candidate is
        // therefore expressed the way that route expresses it: a target equal to
        // the source, converting nothing. Mapping Original onto some named
        // precision instead would invent a conversion.
        if (configuration.Weights == OpenVinoWeightFormat.Original)
        {
            Require(
                payload.TargetWeightPrecision == payload.SourceWeightPrecision,
                "weight precision",
                $"the candidate runs the package as-is but the payload converts "
                    + $"{payload.SourceWeightPrecision} to {payload.TargetWeightPrecision}");
        }
        else
        {
            Require(
                ExecutionVocabularyMap.ToWeightPrecision(configuration.Weights)
                    == payload.TargetWeightPrecision,
                "weight precision",
                $"{configuration.Weights} against {payload.TargetWeightPrecision}");
        }

        Require(
            ExecutionVocabularyMap.ToKvCachePrecision(configuration.KvCache)
                == payload.KvCachePrecision,
            "KV cache precision",
            $"{configuration.KvCache} against {payload.KvCachePrecision}");

        Require(
            ExecutionVocabularyMap.ToDeviceId(configuration.Device) == payload.Device,
            "device",
            $"{configuration.Device} against {payload.Device}");

        Require(
            (configuration.CompiledCache == OpenVinoCompiledCachePolicy.Enabled)
                == payload.CompiledCacheEnabled,
            "compiled cache",
            $"{configuration.CompiledCache} against enabled={payload.CompiledCacheEnabled}");

        Require(
            candidate.EvidenceId == payload.EvidenceId,
            "evidence",
            $"{candidate.EvidenceId} against {payload.EvidenceId}");
    }

    private static void Require(bool agrees, string what, string detail)
    {
        if (!agrees)
        {
            throw new ArgumentException(
                $"The candidate and the execution payload disagree about {what}: "
                + $"{detail}. A plan carrying two answers to the same question is "
                + "worse than one carrying none, because both look authoritative.",
                "executionPayload");
        }
    }
}
