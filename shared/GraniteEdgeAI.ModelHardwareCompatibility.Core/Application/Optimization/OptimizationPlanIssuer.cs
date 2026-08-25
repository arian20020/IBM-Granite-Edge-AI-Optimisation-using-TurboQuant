using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
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

        RequireAdmissionAuthority(
            candidate, executionPayload, capabilitySnapshot, workload, binding);
        RequireAgreement(candidate, executionPayload, modelLayerCount);
        RequireOpenVinoRuntimeAuthority(candidate, executionPayload, capabilitySnapshot);
        RequireGgufRuntimeAuthority(candidate, executionPayload, capabilitySnapshot);
        RequireGgufConversionAuthority(
            candidate, executionPayload, capabilitySnapshot, binding);

        return new OptimizationExecutionPlan(
            OptimizationExecutionPlan.CurrentContractVersion,
            Guid.NewGuid(),
            binding,
            capabilitySnapshot,
            workload,
            candidate,
            executionPayload,
            selection.Preference,
            selection.SharedWithAdjacentBand,
            OptimizationCanonicalizer.ConfigurationSha256(
                candidate,
                executionPayload,
                OptimizationExecutionPlan.CurrentContractVersion),
            createdAtUtc);
    }

    private static void RequireAdmissionAuthority(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding)
    {
        OptimizationAdmissionProof? proof = candidate.AdmissionProof;
        Require(
            proof is not null
                && proof.MatchesCandidate(candidate)
                && proof.MatchesAuthority(snapshot, workload, binding)
                && proof.RequiresPersistentChange == payload.RequiresPersistentConversion,
            "frontier admission authority",
            "candidate quantities, snapshot, workload, journey, or persistence differs");
    }

    private static void RequireGgufRuntimeAuthority(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        OptimizationCapabilitySnapshot snapshot)
    {
        if (candidate.Configuration is not GgufRouteConfiguration configuration)
        {
            return;
        }

        GgufCapabilityPayload capability = snapshot.Gguf!;
        GgufAdmittedConfiguration? admitted = capability.Admitted.SingleOrDefault(entry =>
            string.Equals(entry.EvidenceId, candidate.EvidenceId, StringComparison.Ordinal));
        Require(admitted is not null, "admitted GGUF evidence", candidate.EvidenceId);

        GgufRouteConfiguration admittedConfiguration = GgufRouteConfiguration.Create(
            admitted!.Weights, admitted.KvCache, admitted.Backend, admitted.Device,
            admitted.Offload);
        Require(
            AdmittedGgufConfigurationMatches(
                admittedConfiguration, configuration,
                candidate.Metrics.RequiresPersistentChange,
                candidate.WeightNormalizationProof)
                && candidate.Metrics.ContextTokens >= admitted.MinimumContextTokens
                && candidate.Metrics.ContextTokens <= admitted.MaximumContextTokens,
            "admitted GGUF configuration",
            "the evidence identifier names a different target or context range");

        OptimizationAdmissionProof proof = candidate.AdmissionProof!;
        Require(
            proof.SupportLevel == admitted.Level
                && proof.RequiresEvidence == admitted.RequiresEvidence
                && candidate.IsExperimental
                    == (admitted.Level == SupportLevel.Experimental)
                && (!(admitted.RequiresEvidence
                        || admitted.Level == SupportLevel.Experimental)
                    || string.Equals(
                        proof.OptedInEvidenceId,
                        admitted.EvidenceId,
                        StringComparison.Ordinal)),
            "GGUF support admission",
            "support level, experimental state, evidence requirement, or opt-in differs");

        GgufRuntimeAuthority? runtimeAuthority = capability.RuntimeAuthority;
        GgufExecutionProfileAuthority? profile = null;
        if (runtimeAuthority is not null)
        {
            runtimeAuthority.Profiles.TryGetValue(candidate.EvidenceId, out profile);
        }
        GgufExecutionPayload gguf = payload.Gguf!;
        Require(
            runtimeAuthority is not null
                && profile is not null
                && string.Equals(
                    runtimeAuthority.RuntimeBuildId,
                    gguf.RuntimeBuildId,
                    StringComparison.Ordinal)
                && string.Equals(
                    runtimeAuthority.RuntimeSourceCommit,
                    gguf.RuntimeSourceCommit,
                    StringComparison.Ordinal)
                && profile.Evidence == candidate.Metrics.Evidence
                && string.Equals(
                    GgufEvidenceGradeMap.ToExecutionValue(profile.Evidence),
                    gguf.EvidenceGrade,
                    StringComparison.Ordinal)
                && string.Equals(
                    profile.ProfileId,
                    gguf.ProfileId,
                    StringComparison.Ordinal),
            "GGUF runtime execution authority",
            "runtime build, source commit, evidence grade, or profile differs");

        GgufTurboQuantImplementationIdentity? identity =
            capability.TurboQuantImplementation;
        if (identity is not null)
        {
            Require(
                string.Equals(
                    identity.RuntimeName, gguf.RuntimeBuildId, StringComparison.Ordinal)
                    && string.Equals(
                        identity.SourceCommit,
                        gguf.RuntimeSourceCommit,
                        StringComparison.Ordinal),
                "GGUF TurboQuant runtime identity",
                "the capability snapshot and execution payload name different runtimes");
        }

        if (configuration.KvCache != GgufKvCacheFormat.TurboQuant3Bit)
        {
            return;
        }

        Require(
            identity is not null
                && candidate.IsExperimental
                && admitted.Level == SupportLevel.Experimental
                && admitted.RequiresEvidence
                && identity.Backend == configuration.Backend
                && identity.Device == configuration.Device
                && string.Equals(
                    identity.RuntimeName, gguf.RuntimeBuildId, StringComparison.Ordinal)
                && string.Equals(
                    identity.SourceCommit,
                    gguf.RuntimeSourceCommit,
                    StringComparison.Ordinal),
            "GGUF TurboQuant implementation identity",
            "runtime name, source commit, backend, device, or evidence level differs");
    }

    private static void RequireOpenVinoRuntimeAuthority(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        OptimizationCapabilitySnapshot snapshot)
    {
        if (candidate.Configuration is not OpenVinoRouteConfiguration configuration)
        {
            return;
        }

        OpenVinoCapabilityPayload capability = snapshot.OpenVino!;
        OpenVinoAdmittedConfiguration? admitted = capability.Admitted.SingleOrDefault(entry =>
            string.Equals(entry.EvidenceId, candidate.EvidenceId, StringComparison.Ordinal));
        Require(admitted is not null, "admitted OpenVINO evidence", candidate.EvidenceId);

        OpenVinoRouteConfiguration admittedConfiguration =
            OpenVinoRouteConfiguration.Create(
                admitted!.Weights, admitted.KvCache, admitted.Device,
                admitted.PerformanceHint, admitted.CompiledCache, admitted.Streams);
        OptimizationAdmissionProof proof = candidate.AdmissionProof!;
        OpenVinoExecutionPayload openVino = payload.OpenVino!;
        capability.ExecutionAuthorities.TryGetValue(
            admitted.EvidenceId, out OpenVinoExecutionAuthority? executionAuthority);
        string expectedMaturity = admitted.Level == SupportLevel.Experimental
            ? "Experimental candidate"
            : "Standard candidate";

        Require(
            admittedConfiguration == configuration
                && candidate.Metrics.ContextTokens >= admitted.MinimumContextTokens
                && candidate.Metrics.ContextTokens <= admitted.MaximumContextTokens,
            "admitted OpenVINO configuration",
            "the evidence identifier names a different complete target or context range");
        Require(
            proof.SupportLevel == admitted.Level
                && proof.RequiresEvidence == admitted.RequiresEvidence
                && candidate.IsExperimental
                    == (admitted.Level == SupportLevel.Experimental)
                && (!(admitted.RequiresEvidence
                        || admitted.Level == SupportLevel.Experimental)
                    || string.Equals(
                        proof.OptedInEvidenceId,
                        admitted.EvidenceId,
                        StringComparison.Ordinal)),
            "OpenVINO support admission",
            "support level, experimental state, evidence requirement, or opt-in differs");
        Require(
            string.Equals(
                capability.RuntimeVersion,
                openVino.BuildIdentity.RuntimeBuild,
                StringComparison.Ordinal),
            "OpenVINO runtime build",
            "capability and execution payload runtime identities differ");
        Require(
            executionAuthority is not null
                && string.Equals(
                    executionAuthority.ConfigurationId,
                    openVino.ConfigurationId,
                    StringComparison.Ordinal)
                && executionAuthority.SourceWeightPrecision
                    == openVino.SourceWeightPrecision
                && executionAuthority.BuildIdentity == openVino.BuildIdentity
                && ExecutionAuthorityMapsAgree(
                    executionAuthority.OptimizerVersions,
                    openVino.OptimizerVersions)
                && executionAuthority.TurboQuantBuild == openVino.TurboQuantBuild,
            "OpenVINO execution authority",
            "configuration, source precision, build, optimizer map, or TurboQuant build differs");
        Require(
            string.Equals(openVino.EvidenceId, admitted.EvidenceId, StringComparison.Ordinal)
                && string.Equals(openVino.Maturity, expectedMaturity, StringComparison.Ordinal)
                && openVino.CreatesCompletePackage
                    == candidate.Metrics.RequiresPersistentChange,
            "OpenVINO execution admission",
            "evidence, maturity, package creation, or persistence differs");
    }

    private static bool ExecutionAuthorityMapsAgree(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual) =>
        expected.Count == actual.Count
        && expected.All(entry =>
            actual.TryGetValue(entry.Key, out string? value)
            && string.Equals(entry.Value, value, StringComparison.Ordinal));

    private static bool AdmittedGgufConfigurationMatches(
        GgufRouteConfiguration admitted,
        GgufRouteConfiguration candidate,
        bool requiresPersistentChange,
        GgufWeightNormalizationProof? normalizationProof)
    {
        if (admitted == candidate)
        {
            return normalizationProof is null;
        }

        // The generator authenticates an already-at-target named weight against
        // its admitted record, then represents the runtime-only result as
        // Imported so no conversion can be implied by the execution contract.
        return !requiresPersistentChange
            && admitted.Weights != GgufWeightFormat.Imported
            && candidate.Weights == GgufWeightFormat.Imported
            && normalizationProof is not null
            && normalizationProof.AdmittedWeight == admitted.Weights
            && admitted.KvCache == candidate.KvCache
            && admitted.Backend == candidate.Backend
            && admitted.Device == candidate.Device
            && admitted.Offload == candidate.Offload;
    }

    private static void RequireGgufConversionAuthority(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationJourneyBinding binding)
    {
        if (candidate.Configuration is not GgufRouteConfiguration configuration)
        {
            return;
        }

        if (configuration.Weights == GgufWeightFormat.Q2K)
        {
            Require(
                candidate.Metrics.Quality == OptimizationAssessment.Poor,
                "Q2_K quality",
                "the product floor must remain Poor regardless of caller metrics");
        }

        OptimizationCandidateNotice expectedNotice =
            OptimizationCandidate.ExpectedNotice(
                configuration, candidate.Metrics, candidate.ConversionProvenance);
        Require(
            candidate.Notice == expectedNotice,
            "candidate warning",
            $"expected {expectedNotice} but received {candidate.Notice}");

        GgufExecutionPayload ggufPayload = payload.Gguf!;
        if (!candidate.Metrics.RequiresPersistentChange)
        {
            Require(
                ggufPayload.ConversionSource is null
                    && ggufPayload.RequantisationPolicy is null,
                "runtime-only conversion authority",
                "a runtime-only plan must authorize no conversion source or policy");
            return;
        }

        GgufCapabilityPayload capability = snapshot.Gguf!;
        GgufAdmittedConfiguration? admitted = capability.Admitted.SingleOrDefault(entry =>
            string.Equals(entry.EvidenceId, candidate.EvidenceId, StringComparison.Ordinal));
        Require(admitted is not null, "admitted GGUF evidence", candidate.EvidenceId);

        GgufRouteConfiguration admittedConfiguration = GgufRouteConfiguration.Create(
            admitted!.Weights, admitted.KvCache, admitted.Backend, admitted.Device,
            admitted.Offload);
        Require(
            admittedConfiguration == configuration
                && candidate.Metrics.ContextTokens >= admitted.MinimumContextTokens
                && candidate.Metrics.ContextTokens <= admitted.MaximumContextTokens,
            "admitted GGUF configuration",
            "the evidence identifier names a different target or context range");

        GgufConversionSourceBinding? source = capability.ConversionSource;
        Require(
            source is not null && source == ggufPayload.ConversionSource,
            "GGUF conversion source",
            "capability and execution payload must bind the exact same source identity");
        Require(
            string.Equals(
                source!.Journey.ProductHardwareRunId,
                binding.ProductHardwareRunId,
                StringComparison.Ordinal)
                && string.Equals(
                    source.Journey.HardwareSnapshotSha256,
                    binding.HardwareSnapshotSha256,
                    StringComparison.Ordinal),
            "GGUF conversion source journey",
            "the selected source was established against a different hardware journey");
        Require(
            capability.AdmittedQuantiser is not null
                && capability.AdmittedQuantiser == ggufPayload.Quantiser,
            "GGUF conversion quantiser",
            "every persistent conversion must use the exact admitted quantiser identity");

        WeightQuantisation target = GgufWeightFormatMap.ToCanonical(configuration.Weights);
        Require(
            source!.CanProduce(target),
            "GGUF conversion precision",
            $"source {source.Precision} cannot produce downward target {target}");

        OptimizationConversionProvenance expectedProvenance = source.IsAlreadyQuantised
            ? OptimizationConversionProvenance.ControlledRequantisation
            : OptimizationConversionProvenance.HigherPrecisionSource;
        Require(
            candidate.ConversionProvenance == expectedProvenance,
            "GGUF conversion provenance",
            $"expected {expectedProvenance} but received {candidate.ConversionProvenance}");

        if (source.IsAlreadyQuantised)
        {
            Require(
                source.Journey == binding,
                "controlled requantisation imported source",
                "the acknowledged quantised source is not the imported model bound to the plan");
            GgufRequantisationPolicy? policy = capability.RequantisationPolicy;
            Require(
                policy is not null
                    && policy == ggufPayload.RequantisationPolicy
                    && policy.Authorizes(admitted)
                    && policy.Source == source
                    && policy.Quantiser == capability.AdmittedQuantiser,
                "controlled requantisation policy",
                "acknowledgement, admitted target, source, or quantiser differs");
        }
        else
        {
            Require(
                ggufPayload.RequantisationPolicy is null,
                "ordinary conversion policy",
                "a higher-precision conversion must not masquerade as requantisation");
        }
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

        OpenVinoKvCacheAlgorithm cacheAlgorithm =
            ExecutionVocabularyMap.ToKvCacheAlgorithm(configuration.KvCache);
        OpenVinoKvCachePrecision cachePrecision =
            ExecutionVocabularyMap.ToKvCachePrecision(configuration.KvCache);

        Require(
            cacheAlgorithm == payload.KvCacheAlgorithm
                && cachePrecision == payload.KvCachePrecision,
            "KV cache precision",
            $"{configuration.KvCache} against algorithm {payload.KvCacheAlgorithm} "
                + $"and precision {payload.KvCachePrecision}");

        bool turboQuant = cacheAlgorithm == OpenVinoKvCacheAlgorithm.TurboQuant;

        Require(
            !turboQuant || candidate.IsExperimental,
            "TurboQuant evidence level",
            $"cache {configuration.KvCache} against experimental={candidate.IsExperimental}");

        Require(
            (payload.TurboQuantBuild is not null) == turboQuant,
            "TurboQuant build identity",
            $"cache {configuration.KvCache} against build-present="
                + $"{payload.TurboQuantBuild is not null}");

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
