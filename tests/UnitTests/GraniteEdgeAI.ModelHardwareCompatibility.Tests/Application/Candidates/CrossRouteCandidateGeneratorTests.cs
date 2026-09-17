using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Candidates;

/// <summary>
/// one generator, two routes, no leakage.
///
/// the property that matters is not that both routes produce candidates - it is
/// that the shared layer never has to know which route it is looking at in
/// order to compare them, and never quietly admits a combination nothing
/// evidenced
/// </summary>
[TestClass]
public sealed class CrossRouteCandidateGeneratorTests
{
    [TestMethod]
    [DataRow(OpenVinoWeightPrecision.FourBit, false, false)]
    [DataRow(OpenVinoWeightPrecision.EightBit, false, false)]
    [DataRow(OpenVinoWeightPrecision.MxFp4, false, false)]
    [DataRow(OpenVinoWeightPrecision.FourBit, true, false)]
    [DataRow(OpenVinoWeightPrecision.EightBit, true, false)]
    [DataRow(OpenVinoWeightPrecision.FourBit, false, true)]
    public void InspectedUnchangedQuantisedPackageWithoutQualityRowsDoesNotBecomeAnOptimisationCandidate(
        OpenVinoWeightPrecision precision, bool conflictingEvidence, bool unknownRuntime)
    {
        var admission = CrossRouteTestData.OpenVino("current", OpenVinoWeightFormat.Original,
            cache: OpenVinoKvCacheFormat.RouteDefault);
        var execution = OpenVinoExecutionAuthority.Create("current", "current", precision,
            OpenVinoBuildIdentity.Create(VerifiedOpenVinoOptimizationEvidence.RuntimeBuild,
                VerifiedOpenVinoOptimizationEvidence.GenAiBuild,
                VerifiedOpenVinoOptimizationEvidence.TokenizersBuild,
                unknownRuntime ? Digest : VerifiedOpenVinoOptimizationEvidence.CurrentOfficialWorkerManifestSha256),
            new Dictionary<string, string>
            {
                ["openvino"] = "2026.3.0", ["openvino-genai"] = "2026.3.0.0",
                ["nncf"] = "3.3.0", ["optimum"] = "2.3.0",
                ["optimum-intel"] = "2.1.0", ["transformers"] = "5.5.4"
            }, true);
        var snapshot = OptimizationCapabilitySnapshot.ForOpenVino("current", Digest,
            OpenVinoCapabilityPayload.Create(execution.BuildIdentity.RuntimeBuild, [admission], [execution]));
        var generated = CrossRouteCandidateGenerator.Generate(snapshot, CrossRouteTestData.Facts(),
            CrossRouteTestData.Workload(), CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(), HardwareAuthority(),
            conflictingEvidence ? new OptimizationEvidenceCatalog(PublishedOpenVinoOptimizationEvidence.Records()
                .Select(record => record with { OutputHealthPassed = false }))
                : new OptimizationEvidenceCatalog([]), inspectedParameterCount: null);
        Assert.AreEqual(0, generated.Candidates.Count);
        Assert.AreEqual(conflictingEvidence || unknownRuntime
                ? OptimizationExclusionReason.EvidenceBelowAdmissionLevel
                : OptimizationExclusionReason.CurrentModelQualityEvidenceUnavailable,
            generated.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(OpenVinoWeightPrecision.FourBit, OpenVinoWeightFormat.Int4, false)]
    [DataRow(OpenVinoWeightPrecision.EightBit, OpenVinoWeightFormat.Int8, false)]
    [DataRow(OpenVinoWeightPrecision.Fp16, OpenVinoWeightFormat.Fp16, false)]
    [DataRow(OpenVinoWeightPrecision.Fp16, OpenVinoWeightFormat.Original, false)]
    [DataRow(OpenVinoWeightPrecision.Fp16, OpenVinoWeightFormat.Int4, true)]
    [DataRow(OpenVinoWeightPrecision.Fp16, OpenVinoWeightFormat.Int8, true)]
    public void OpenVinoPersistenceMatchesTrustedSourceAndTargetPrecision(
        OpenVinoWeightPrecision sourcePrecision, OpenVinoWeightFormat target, bool persistent)
    {
        var admission = CrossRouteTestData.OpenVino("persistence-shape", target);
        var template = CrossRouteTestData.OpenVinoSnapshot(admission);
        var original = template.OpenVino!.ExecutionAuthorities[admission.EvidenceId];
        var execution = OpenVinoExecutionAuthority.Create(original.EvidenceId, original.ConfigurationId,
            sourcePrecision, original.BuildIdentity, original.OptimizerVersions, original.CompiledCacheIsDisposable);
        var snapshot = OptimizationCapabilitySnapshot.ForOpenVino("persistence-shape", Digest,
            OpenVinoCapabilityPayload.Create(original.BuildIdentity.RuntimeBuild, [admission], [execution]));
        var generated = Generate(snapshot);
        Assert.HasCount(1, generated.Candidates);
        var candidate = generated.Candidates.Single();
        Assert.AreEqual(persistent, candidate.Metrics.RequiresPersistentChange);
        Assert.AreEqual(persistent, candidate.AdmissionProof!.RequiresPersistentChange);
        Assert.AreEqual(persistent, candidate.Metrics.OutputDiskBytes > 0);
        Assert.AreEqual(target, ((OpenVinoRouteConfiguration)candidate.Configuration).Weights);
        Assert.AreEqual(sourcePrecision, snapshot.OpenVino!.ExecutionAuthorities[admission.EvidenceId].SourceWeightPrecision);
    }

    private const ulong Gibibyte = 1024UL * 1024 * 1024;
    private const string Digest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Bf16Q3EvidenceId = "GGUF-V5-BF16-Q3-CPU-F16-01";
    private const string AtomicBotQuantizerPackageId =
        "granite-edge-ai-atomicbot-llama-quantize-x64";
    private const string AtomicBotQuantizerToolVersion =
        "atomicbot-llama-quantize-519f0c594a8e31467d2e2f2cf17054c9e7e11536";
    private const string AtomicBotQuantizerExecutableSha256 =
        "0a17247d4807b520532df54f96ed73f3cf6fa921f879d8d465b44541b41d36e3";
    private const string Bf16SourceModelSha256 =
        "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091";

    [TestMethod]
    public void VerifiedV4CpuRowsGenerateStandardWithoutConsentAndTurboOnlyWithConsent()
    {
        CrossRouteGenerationResult standard = GenerateVerifiedV4Gguf(
            new HashSet<string>());

        CollectionAssert.AreEquivalent(
            new[] { "GGUF-V4-CPU-F16-01", "GGUF-V4-CPU-Q8-01" },
            standard.Candidates.Select(static item => item.EvidenceId).ToArray());
        Assert.IsTrue(standard.Candidates.All(static item => !item.IsExperimental));

        CrossRouteGenerationResult consented = GenerateVerifiedV4Gguf(
            new HashSet<string>
            {
                "GGUF-V4-CPU-TURBO4-01",
                "GGUF-V4-CPU-TURBO3-01",
            });
        Assert.HasCount(4, consented.Candidates);
        Assert.IsTrue(consented.Candidates.Where(static item =>
                item.EvidenceId.Contains("TURBO", StringComparison.Ordinal))
            .All(static item => item.IsExperimental));
        Assert.HasCount(4, consented.Candidates.Select(static item =>
            item.Configuration.CanonicalDescriptor).Distinct());
    }

    [TestMethod]
    public void VerifiedV5CpuRowsGenerateOnlyStandardF16AndQ8WithoutConsent()
    {
        GgufAdmittedConfiguration[] admissions =
        [
            GgufAdmittedConfiguration.Create(
                "GGUF-V5-CPU-F16-01", CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu, GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16, GpuOffloadLevel.None,
                4096, 4096, SupportLevel.DeclaredSupported,
                requiresEvidence: false),
            GgufAdmittedConfiguration.Create(
                "GGUF-V5-CPU-Q8-01", CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu, GgufWeightFormat.Imported,
                GgufKvCacheFormat.Q8_0, GpuOffloadLevel.None,
                4096, 4096, SupportLevel.DeclaredSupported,
                requiresEvidence: false),
        ];
        GgufExecutionProfileAuthority[] profiles =
        [
            GgufExecutionProfileAuthority.Create(
                admissions[0].EvidenceId, EvidenceGrade.Measured, "cpu",
                true, 4, 512, 256),
            GgufExecutionProfileAuthority.Create(
                admissions[1].EvidenceId, EvidenceGrade.Measured, "cpu",
                true, 4, 512, 256),
        ];
        OptimizationEvidenceRecord[] evidence =
        [
            .. VerifiedGgufOptimizationEvidence.Records(
                "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
                VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit)
        ];

        CrossRouteGenerationResult result = GenerateVerifiedV4Gguf(
            new HashSet<string>(), admissions, profiles, evidence);

        Assert.HasCount(2, result.Candidates);
        CollectionAssert.AreEquivalent(
            admissions.Select(static item => item.EvidenceId).ToArray(),
            result.Candidates.Select(static item => item.EvidenceId).ToArray());
        Assert.IsTrue(result.Candidates.All(static item =>
            !item.IsExperimental
            && item.AdmissionProof is { RequiresEvidence: false }));
    }

    [TestMethod]
    public void ExactBf16Q3EvidenceGeneratesPersistentCandidateOnlyForAtomicBotQuantizer()
    {
        CrossRouteGenerationResult verified = GenerateVerifiedBf16Q3(
            GgufQuantiserIdentity.Create(
                AtomicBotQuantizerPackageId,
                AtomicBotQuantizerToolVersion,
                AtomicBotQuantizerExecutableSha256));

        Assert.HasCount(1, verified.Candidates,
            string.Join("; ", verified.Exclusions.Select(static item =>
                $"{item.EvidenceId}:{item.Reason}:{item.CanonicalDescriptor}")));
        OptimizationCandidate candidate = verified.Candidates[0];
        Assert.AreEqual(Bf16Q3EvidenceId,
            candidate.EvidenceId);
        Assert.AreEqual(GgufWeightFormat.Q3KM,
            ((GgufRouteConfiguration)candidate.Configuration).Weights);
        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        Assert.IsFalse(candidate.IsExperimental);

        GgufQuantiserIdentity?[] refusedQuantizers =
        [
            null,
            GgufQuantiserIdentity.Create(
                "different-package",
                AtomicBotQuantizerToolVersion,
                AtomicBotQuantizerExecutableSha256),
            GgufQuantiserIdentity.Create(
                AtomicBotQuantizerPackageId,
                "different-version",
                AtomicBotQuantizerExecutableSha256),
            GgufQuantiserIdentity.Create(
                AtomicBotQuantizerPackageId,
                AtomicBotQuantizerToolVersion,
                new string('a', 64)),
        ];
        foreach (GgufQuantiserIdentity? refused in refusedQuantizers)
        {
            Assert.IsEmpty(GenerateVerifiedBf16Q3(refused).Candidates);
        }
    }

    [TestMethod]
    [DataRow("evidence")]
    [DataRow("cache")]
    [DataRow("backend")]
    [DataRow("device")]
    [DataRow("offload")]
    [DataRow("context")]
    [DataRow("profile")]
    [DataRow("flash")]
    [DataRow("threads")]
    [DataRow("batch")]
    [DataRow("maximum")]
    [DataRow("converter")]
    public void ExactBf16Q3EvidenceRejectsEveryConfigurationMutation(string mutation)
    {
        GgufQuantiserIdentity quantizer = GgufQuantiserIdentity.Create(
            AtomicBotQuantizerPackageId,
            AtomicBotQuantizerToolVersion,
            AtomicBotQuantizerExecutableSha256);

        Assert.IsEmpty(GenerateVerifiedBf16Q3(quantizer, mutation).Candidates);
    }

    [TestMethod]
    [DataRow("grade")]
    [DataRow("profile")]
    [DataRow("flash")]
    [DataRow("threads")]
    [DataRow("batch")]
    [DataRow("maximum")]
    public void VerifiedV4EvidenceRejectsEveryExecutionProfileMutation(string mutation)
    {
        GgufAdmittedConfiguration admission =
            VerifiedGgufOptimizationEvidence.Admissions().Single(static item =>
                item.EvidenceId == "GGUF-V4-CPU-F16-01");
        GgufExecutionProfileAuthority profile = mutation switch
        {
            "grade" => GgufExecutionProfileAuthority.Create(
                admission.EvidenceId, EvidenceGrade.Estimated, "cpu", true, 4, 512, 256),
            "profile" => GgufExecutionProfileAuthority.Create(
                admission.EvidenceId, EvidenceGrade.Measured, "other", true, 4, 512, 256),
            "flash" => GgufExecutionProfileAuthority.Create(
                admission.EvidenceId, EvidenceGrade.Measured, "cpu", false, 4, 512, 256),
            "threads" => GgufExecutionProfileAuthority.Create(
                admission.EvidenceId, EvidenceGrade.Measured, "cpu", true, 3, 512, 256),
            "batch" => GgufExecutionProfileAuthority.Create(
                admission.EvidenceId, EvidenceGrade.Measured, "cpu", true, 4, 511, 256),
            _ => GgufExecutionProfileAuthority.Create(
                admission.EvidenceId, EvidenceGrade.Measured, "cpu", true, 4, 512, 255),
        };

        Assert.IsEmpty(GenerateVerifiedV4Gguf(
            new HashSet<string>(), [admission], [profile]).Candidates);
    }

    [TestMethod]
    public void VerifiedV4EvidenceWithAnyFailedQualityGateGeneratesNoCandidate()
    {
        OptimizationEvidenceRecord exact = VerifiedV4Records().Single(static item =>
            item.EvidenceId == "GGUF-V4-CPU-Q8-01");
        OptimizationEvidenceRecord[] failed =
        [
            exact with { OutputHealthPassed = false },
            exact with { StabilityPassed = false },
            exact with { ActivationPassed = false },
            exact with { IntegrityPassed = false },
            exact with { Quality = new OptimizationQualityScore(3.99m) },
        ];
        GgufAdmittedConfiguration admission =
            VerifiedGgufOptimizationEvidence.Admissions().Single(static item =>
                item.EvidenceId == "GGUF-V4-CPU-Q8-01");
        GgufExecutionProfileAuthority profile =
            VerifiedGgufOptimizationEvidence.ExecutionProfiles().Single(static item =>
                item.EvidenceId == "GGUF-V4-CPU-Q8-01");

        foreach (OptimizationEvidenceRecord row in failed)
        {
            Assert.IsEmpty(GenerateVerifiedV4Gguf(
                new HashSet<string>(), [admission], [profile], [row]).Candidates);
        }
    }

    [TestMethod]
    public void VerifiedV4PlanningSessionPinsExactCpuPayloadAndConsentBoundary()
    {
        HashSet<string> consent =
        [
            "GGUF-V4-CPU-TURBO4-01",
            "GGUF-V4-CPU-TURBO3-01",
        ];
        CrossRouteGenerationResult generated = GenerateVerifiedV4Gguf(consent);
        OptimizationCapabilitySnapshot snapshot = VerifiedV4Snapshot();
        OptimizationWorkload workload = VerifiedV4Workload();
        OptimizationJourneyBinding binding = VerifiedV4Binding();
        CompatibilityPlanningSession session = CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf,
            generated.Candidates,
            snapshot,
            workload,
            binding,
            modelLayerCount: 40,
            consent)!;
        Assert.IsNotNull(session);
        OptimizationCandidate turbo = generated.Candidates.Single(static item =>
            item.EvidenceId == "GGUF-V4-CPU-TURBO3-01");
        OptimizationPreferenceSelection turboPreference =
            OptimizationPreferenceSelection.Exact(
                OptimizationPreferenceResolver.CandidateIdentity(turbo));
        Assert.AreEqual(turbo.EvidenceId,
            session.RequiredExperimentalEvidenceId(turboPreference));

        OptimizationIssuanceAuthority issuance =
            OptimizationIssuanceAuthority.FromGeneration(
                OptimizationHardwareAuthorityTestData.AllEstablished(),
                ByteCount.FromBytes(32 * Gibibyte),
                ByteCount.FromBytes(500 * Gibibyte));
        Assert.AreEqual(turbo.AdmissionProof!.HardwareAuthoritySha256,
            issuance.AuthoritySha256);
        Assert.IsTrue(turbo.AdmissionProof.MatchesCandidate(turbo));
        Assert.IsTrue(turbo.AdmissionProof.MatchesAuthority(
            snapshot, workload, binding));
        Assert.IsTrue(issuance.IsFreshAt(DateTimeOffset.UnixEpoch));
        var composer = new VerifiedV4Composer(snapshot.Gguf!.RuntimeAuthority!);
        OptimizationExecutionPayload composed = composer.Compose(turbo);
        Assert.AreEqual(turbo.AdmissionProof.RequiresPersistentChange,
            composed.RequiresPersistentConversion);
        OptimizationExecutionPlan plan = session.Issue(
            turboPreference,
            composer,
            issuance,
            new EpochTimeProvider());

        Assert.AreEqual(turbo, plan.Candidate);
        Assert.IsTrue(session.MatchesIssuedPlan(plan, turboPreference));
        GgufExecutionPayload payload = plan.ExecutionPayload.Gguf!;
        Assert.AreEqual(GgufRuntimeBackend.Cpu, payload.Backend);
        Assert.AreEqual("CPU", payload.DeviceId);
        Assert.AreEqual(4096, payload.ContextSize);
        Assert.AreEqual(GgufCacheType.Turbo3, payload.KeyCacheType);
        Assert.AreEqual(GgufCacheType.Turbo3, payload.ValueCacheType);
        Assert.AreEqual(0, payload.GpuLayerCount);
        Assert.IsTrue(payload.FlashAttention);
        Assert.AreEqual(4, payload.ThreadCount);
        Assert.AreEqual(512, payload.BatchSize);
        Assert.AreEqual(256, payload.MaximumGeneratedTokens);
        Assert.AreEqual("Measured", payload.EvidenceGrade);
        Assert.AreEqual("cpu", payload.ProfileId);

        Assert.IsNull(CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf,
            [turbo],
            snapshot,
            workload,
            binding,
            40,
            new HashSet<string>()));

        CompatibilityPlanningSession previewAfterConsentRetirement =
            CompatibilityPlanningSession.Create(
                OptimizationRoute.Gguf,
                [turbo],
                snapshot,
                workload,
                binding,
                40,
                new HashSet<string>(),
                allowUnconsentedPreview: true)!;
        Assert.IsNotNull(previewAfterConsentRetirement);
        Assert.AreEqual(turbo.EvidenceId,
            previewAfterConsentRetirement.RequiredExperimentalEvidenceId(
                turboPreference));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            previewAfterConsentRetirement.Issue(
                turboPreference,
                composer,
                issuance,
                new EpochTimeProvider()));

        OptimizationCandidate standard = generated.Candidates.Single(static item =>
            item.EvidenceId == "GGUF-V4-CPU-Q8-01");
        Assert.IsNotNull(CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf,
            [standard],
            snapshot,
            workload,
            binding,
            40,
            new HashSet<string>()));
    }

    [TestMethod]
    public void ReleasedV5Turbo4Threads8IssuesExactRuntimePlanWithoutConsent()
    {
        HashSet<string> evidenceIds = ["GGUF-V5-CPU-TURBO4-T8-01"];
        IReadOnlyList<GgufAdmittedConfiguration> admissions =
            VerifiedGgufOptimizationEvidence.Admissions(evidenceIds);
        IReadOnlyList<GgufExecutionProfileAuthority> profiles =
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(evidenceIds);
        OptimizationEvidenceRecord[] evidence =
        [
            .. VerifiedGgufOptimizationEvidence.Records(
                "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
                VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit)
        ];
        CrossRouteGenerationResult generated = GenerateVerifiedV4Gguf(
            new HashSet<string>(), admissions, profiles, evidence);

        OptimizationCandidate candidate = generated.Candidates.Single();
        Assert.AreEqual("GGUF-V5-CPU-TURBO4-T8-01", candidate.EvidenceId);
        Assert.IsFalse(candidate.IsExperimental);
        Assert.IsFalse(candidate.AdmissionProof!.RequiresEvidence);
        Assert.AreEqual(EvidenceGrade.Measured, candidate.Metrics.Evidence);
        Assert.IsFalse(candidate.Metrics.RequiresPersistentChange);

        OptimizationCapabilitySnapshot snapshot =
            VerifiedV4Snapshot(admissions, profiles);
        OptimizationWorkload workload = VerifiedV4Workload();
        OptimizationJourneyBinding binding = VerifiedV4Binding();
        CompatibilityPlanningSession session = CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf,
            generated.Candidates,
            snapshot,
            workload,
            binding,
            modelLayerCount: 40,
            new HashSet<string>())!;
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Exact(
                OptimizationPreferenceResolver.CandidateIdentity(candidate));
        Assert.IsNull(session.RequiredExperimentalEvidenceId(preference));

        OptimizationIssuanceAuthority issuance =
            OptimizationIssuanceAuthority.FromGeneration(
                OptimizationHardwareAuthorityTestData.AllEstablished(),
                ByteCount.FromBytes(32 * Gibibyte),
                ByteCount.FromBytes(500 * Gibibyte));
        var composer = new VerifiedV4Composer(snapshot.Gguf!.RuntimeAuthority!);

        OptimizationExecutionPlan plan = session.Issue(
            preference, composer, issuance, new EpochTimeProvider());

        GgufExecutionPayload payload = plan.ExecutionPayload.Gguf!;
        Assert.AreEqual(GgufRuntimeBackend.Cpu, payload.Backend);
        Assert.AreEqual("CPU", payload.DeviceId);
        Assert.AreEqual(4096, payload.ContextSize);
        Assert.AreEqual(GgufCacheType.Turbo4, payload.KeyCacheType);
        Assert.AreEqual(GgufCacheType.Turbo4, payload.ValueCacheType);
        Assert.AreEqual(0, payload.GpuLayerCount);
        Assert.IsTrue(payload.FlashAttention);
        Assert.AreEqual(8, payload.ThreadCount);
        Assert.AreEqual(512, payload.BatchSize);
        Assert.AreEqual(256, payload.MaximumGeneratedTokens);
        Assert.AreEqual("Measured", payload.EvidenceGrade);
        Assert.AreEqual("cpu", payload.ProfileId);
        Assert.AreEqual(GgufWeightFormat.Imported,
            payload.PersistentTargetWeightFormat);
        Assert.IsNull(payload.Quantiser);

        OptimizationEvidenceRecord[] wrongIdentityEvidence =
        [
            .. evidence.Select(static row => row.EvidenceId ==
                    "GGUF-V5-CPU-TURBO4-T8-01"
                ? row with
                {
                    EvidenceId = "GGUF-V5-CPU-TURBO4-T8-MUTATED",
                }
                : row)
        ];
        Assert.IsEmpty(GenerateVerifiedV4Gguf(
            new HashSet<string>(), admissions, profiles,
            wrongIdentityEvidence).Candidates);
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("commit")]
    [DataRow("backend")]
    [DataRow("device")]
    [DataRow("cache")]
    [DataRow("context")]
    [DataRow("flash")]
    [DataRow("threads")]
    [DataRow("batch")]
    [DataRow("maximum")]
    [DataRow("profile")]
    [DataRow("grade")]
    public void ReleasedV5Turbo4Threads8IssuerRejectsEveryPayloadNearMiss(
        string mutation)
    {
        HashSet<string> evidenceIds = ["GGUF-V5-CPU-TURBO4-T8-01"];
        IReadOnlyList<GgufAdmittedConfiguration> admissions =
            VerifiedGgufOptimizationEvidence.Admissions(evidenceIds);
        IReadOnlyList<GgufExecutionProfileAuthority> profiles =
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(evidenceIds);
        OptimizationEvidenceRecord[] evidence =
        [
            .. VerifiedGgufOptimizationEvidence.Records(
                "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
                VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit)
        ];
        CrossRouteGenerationResult generated = GenerateVerifiedV4Gguf(
            new HashSet<string>(), admissions, profiles, evidence);
        OptimizationCapabilitySnapshot snapshot =
            VerifiedV4Snapshot(admissions, profiles);
        OptimizationWorkload workload = VerifiedV4Workload();
        OptimizationJourneyBinding binding = VerifiedV4Binding();
        CompatibilityPlanningSession session = CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf, generated.Candidates, snapshot, workload,
            binding, 40, new HashSet<string>())!;
        OptimizationCandidate candidate = generated.Candidates.Single();
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Exact(
                OptimizationPreferenceResolver.CandidateIdentity(candidate));
        OptimizationIssuanceAuthority issuance =
            OptimizationIssuanceAuthority.FromGeneration(
                OptimizationHardwareAuthorityTestData.AllEstablished(),
                ByteCount.FromBytes(32 * Gibibyte),
                ByteCount.FromBytes(500 * Gibibyte));

        Assert.ThrowsExactly<ArgumentException>(() => session.Issue(
            preference,
            new VerifiedV4Composer(snapshot.Gguf!.RuntimeAuthority!, mutation),
            issuance,
            new EpochTimeProvider()));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.RouteDefault)]
    [DataRow(OpenVinoKvCacheFormat.F16)]
    [DataRow(OpenVinoKvCacheFormat.U8)]
    [DataRow(OpenVinoKvCacheFormat.U4)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3)]
    public void EmptyProductionOpenVinoCatalogOffersNoAutomaticOrManualDeadEnd(OpenVinoKvCacheFormat cache)
    {
        OpenVinoAdmittedConfiguration entry = CrossRouteTestData.OpenVino(
            "empty-catalog", OpenVinoWeightFormat.Int4, SupportLevel.Experimental, cache: cache);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            CrossRouteTestData.OpenVinoSnapshot(entry), CrossRouteTestData.Facts(),
            CrossRouteTestData.Workload(), CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string> { entry.EvidenceId },
            HardwareAuthority(), new OptimizationEvidenceCatalog([]), 3_000_000_000);
        Assert.AreEqual(0, generated.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel, generated.Exclusions.Single().Reason);
        Assert.IsNull(OptimizationPreferenceResolver.Resolve(generated.Candidates, OptimizationPreferenceSelection.Automatic()));
        for (int position = 0; position <= 100; position++)
            Assert.IsNull(OptimizationPreferenceResolver.Resolve(generated.Candidates, OptimizationPreferenceSelection.Manual(position)));
    }

    [TestMethod]
    [DataRow("AB-05")]
    [DataRow("AB-06")]
    [DataRow("AB-08Q")]
    [DataRow("AB-09")]
    [DataRow("AB-10")]
    public void EmptyMandatoryOutputCannotBeOverriddenByAnAboveFloorMean(string evidenceId)
    {
        OptimizationEvidenceRecord record = PublishedGgufOptimizationEvidence.Records()
            .Single(row => row.EvidenceId == evidenceId);
        Assert.IsTrue(record.Quality.Value >= 4m);
        Assert.IsFalse(record.OutputHealthPassed);
        Assert.IsFalse(record.IsAdmitted);
    }

    [TestMethod]
    public void MissingQualityCatalogCannotInventAProductionScore()
    {
        CrossRouteGenerationResult result = GenerateExactPublishedGguf(null);
        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsTrue(result.Exclusions.Any(exclusion =>
            exclusion.Reason == OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
    }

    [TestMethod]
    public void UnchangedEstimatedGgufBaselineWithoutAnExactQualityTupleGetsTheTypedAbsenceReason()
    {
        CrossRouteGenerationResult result = GenerateCurrentGgufBaseline(
            PublishedGgufOptimizationEvidence.CreateCatalog());

        Assert.AreEqual(0, result.Candidates.Count);
        OptimizationExclusion exclusion = result.Exclusions.Single();
        Assert.AreEqual("gguf-current-cpu", exclusion.EvidenceId);
        Assert.AreEqual(
            OptimizationExclusionReason.CurrentModelQualityEvidenceUnavailable,
            exclusion.Reason);
    }

    [TestMethod]
    public void GgufBaselineAbsenceReasonDoesNotForgiveExactMismatchedOrFailedEvidence()
    {
        OptimizationEvidenceRecord seed = PublishedGgufOptimizationEvidence
            .Records().Single(record => record.EvidenceId == "AB-KV3-F16-4K");
        OptimizationEvidenceKey exactKey = seed.Key with { ContextTokens = 32768 };
        OptimizationEvidenceRecord mismatchedIdentity = seed with
        {
            EvidenceId = "other-current-profile",
            Key = exactKey,
            OutputHealthPassed = true,
            StabilityPassed = true,
            ActivationPassed = true,
            IntegrityPassed = true
        };
        OptimizationEvidenceRecord failedExact = seed with
        {
            EvidenceId = "gguf-current-cpu",
            Key = exactKey
        };

        foreach (OptimizationEvidenceCatalog catalog in new[]
        {
            new OptimizationEvidenceCatalog([mismatchedIdentity]),
            new OptimizationEvidenceCatalog([failedExact])
        })
        {
            CrossRouteGenerationResult result = GenerateCurrentGgufBaseline(catalog);
            Assert.AreEqual(0, result.Candidates.Count);
            Assert.AreEqual(
                OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
                result.Exclusions.Single().Reason);
        }
    }

    [TestMethod]
    public void GgufBaselineAbsenceReasonRequiresTheExactVerifiedCurrentProfileShape()
    {
        CrossRouteGenerationResult[] refused =
        [
            GenerateCurrentGgufBaseline(null),
            GenerateCurrentGgufBaseline(
                PublishedGgufOptimizationEvidence.CreateCatalog(),
                runtimeBuild: "other-runtime"),
            GenerateCurrentGgufBaseline(
                PublishedGgufOptimizationEvidence.CreateCatalog(),
                runtimeCommit: "1111111111111111111111111111111111111111"),
            GenerateCurrentGgufBaseline(
                PublishedGgufOptimizationEvidence.CreateCatalog(),
                inspectedParameterCount: null),
            GenerateCurrentGgufBaseline(
                PublishedGgufOptimizationEvidence.CreateCatalog(),
                cache: GgufKvCacheFormat.Q8_0),
            GenerateCurrentGgufBaseline(
                PublishedGgufOptimizationEvidence.CreateCatalog(),
                level: SupportLevel.Experimental,
                requiresEvidence: true),
            GenerateCurrentGgufBaseline(
                PublishedGgufOptimizationEvidence.CreateCatalog(),
                profileEvidence: EvidenceGrade.Measured)
        ];

        foreach (CrossRouteGenerationResult result in refused)
        {
            Assert.AreEqual(0, result.Candidates.Count);
            Assert.AreEqual(
                OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
                result.Exclusions.Single().Reason);
        }
    }

    [TestMethod]
    public void AlreadyQ4NormalizationRetainsExactMeasuredEvidence()
    {
        OptimizationEvidenceRecord synthetic = PublishedGgufOptimizationEvidence.Records().Single(row => row.EvidenceId == "AB-04") with
        {
            EvidenceId = "synthetic-normalization", OutputHealthPassed = true,
            StabilityPassed = true, ActivationPassed = true, IntegrityPassed = true
        };
        CrossRouteGenerationResult result = GenerateExactPublishedGguf(
            new OptimizationEvidenceCatalog([synthetic]),
            weights: GgufWeightFormat.Q4KM, evidenceId: synthetic.EvidenceId);
        OptimizationCandidate candidate = result.Candidates.Single();
        Assert.IsNotNull(candidate.WeightNormalizationProof);
        Assert.IsNotNull(candidate.Evidence);
        Assert.AreEqual(6.725m, candidate.QualityScore);
        Assert.IsFalse(candidate.Metrics.RequiresPersistentChange);
    }

    [TestMethod]
    public void OpenVinoEvidenceCannotBeRelabelledAsAnotherCapability()
    {
        OptimizationEvidenceCatalog changedIdentity = new(
            PublishedOpenVinoOptimizationEvidence.Records().Select(record =>
                record with { EvidenceId = "other-capability" }));
        CrossRouteGenerationResult result = GenerateExactPublishedOpenVino(changedIdentity);
        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsTrue(result.Exclusions.Any(exclusion =>
            exclusion.Reason == OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
    }

    [TestMethod]
    public void HistoricalOpenVinoTupleCannotProveActualActivationOrExecutionClosure()
    {
        const string modelSha =
            "6da1029abd4a1464a94a571ac6a6a950c262b226c15912915a721e0643f7c708";
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "OV-TBQ3-CPU-INT4-01", OpenVinoWeightFormat.Int4,
            SupportLevel.Experimental, cache: OpenVinoKvCacheFormat.TurboQuantTbq3);
        OpenVinoExecutionAuthority execution = OpenVinoExecutionAuthority.Create(
            admitted.EvidenceId, "openvino.turboquant.cpu.int4.tbq3.v1",
            OpenVinoWeightPrecision.FourBit,
            OpenVinoBuildIdentity.Create(
                "2026.5.0-22950-f5f594dc0c9",
                "6fbc103538d30d42da4b0b5130a4792a20f728ba",
                "2026.5.0", Digest),
            new Dictionary<string, string> { ["openvino"] = "2026.5.0" },
            true,
            TurboQuantBuildIdentity.Create(Commit, Commit, Digest, Digest));
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-measured", Digest,
                OpenVinoCapabilityPayload.Create(
                    "2026.5.0-22950-f5f594dc0c9", [admitted], [execution]));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelSha, 1_832_903_073,
            "hw-run-1", Digest);

        CrossRouteGenerationResult result = CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(ByteCount.FromBytes(1_832_903_073),
                40, 4096, 32, 8, 131072, null, null),
            CrossRouteTestData.Workload(), binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { admitted.EvidenceId },
            OptimizationHardwareAuthorityTestData.AllEstablished(),
            PublishedOpenVinoOptimizationEvidence.CreateCatalog(),
            inspectedParameterCount: 3_000_000_000);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel, result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void PublishedAtomicBotTupleCannotOverrideCriticalPromptFailure()
    {
        const string modelSha =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "AB-04",
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None,
            512,
            32768,
            SupportLevel.Experimental,
            requiresEvidence: true);
        GgufRuntimeAuthority runtime = GgufRuntimeAuthority.Create(
            PublishedGgufOptimizationEvidence.RuntimeBuildId,
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
            [GgufExecutionProfileAuthority.Create(
                admitted.EvidenceId,
                EvidenceGrade.Measured,
                "cpu-q4-k-m-q8",
                flashAttention: false,
                threadCount: 4,
                batchSize: 128,
                maximumGeneratedTokens: 256)]);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "atomicbot-measured",
            Digest,
            GgufCapabilityPayload.Create(
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                [admitted],
                hasHigherPrecisionSource: false,
                runtimeAuthority: runtime));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelSha, 3 * Gibibyte,
            "hw-run-1", Digest);

        CrossRouteGenerationResult result = CrossRouteCandidateGenerator.Generate(
            snapshot,
            CrossRouteTestData.Facts(fileType: 15),
            CrossRouteTestData.Workload(),
            binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { admitted.EvidenceId },
            HardwareAuthority(),
            PublishedGgufOptimizationEvidence.CreateCatalog(),
            inspectedParameterCount: 3_000_000_000);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel, result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void FailedOpenVinoEvidenceHardGatesProduceTypedExclusions()
    {
        OptimizationEvidenceRecord exact = PublishedOpenVinoOptimizationEvidence
            .Records().Single(record =>
                record.EvidenceId == "OV-TBQ3-CPU-INT4-01"
                && record.Key.ModelIdentitySha256 ==
                    "6da1029abd4a1464a94a571ac6a6a950c262b226c15912915a721e0643f7c708");

        foreach (OptimizationEvidenceRecord rejected in FailedHardGateVariants(exact))
        {
            OptimizationEvidenceCatalog catalog = new(PublishedOpenVinoOptimizationEvidence
                .Records().Select(record => record.Key == exact.Key ? rejected : record));

            CrossRouteGenerationResult result = GenerateExactPublishedOpenVino(catalog);

            Assert.AreEqual(0, result.Candidates.Count);
            Assert.IsTrue(result.Exclusions.Any(exclusion =>
                exclusion.EvidenceId == exact.EvidenceId
                && exclusion.Reason ==
                    OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
        }
    }

    [TestMethod]
    public void FailedGgufEvidenceHardGatesProduceTypedExclusions()
    {
        OptimizationEvidenceRecord exact = PublishedGgufOptimizationEvidence
            .Records().Single(record => record.EvidenceId == "AB-04");

        foreach (OptimizationEvidenceRecord rejected in FailedHardGateVariants(exact))
        {
            OptimizationEvidenceCatalog catalog = new(PublishedGgufOptimizationEvidence
                .Records().Select(record => record.Key == exact.Key ? rejected : record));

            CrossRouteGenerationResult result = GenerateExactPublishedGguf(catalog);

            Assert.AreEqual(0, result.Candidates.Count);
            Assert.IsTrue(result.Exclusions.Any(exclusion =>
                exclusion.EvidenceId == exact.EvidenceId
                && exclusion.Reason ==
                    OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
        }
    }

    [TestMethod]
    public void EvidenceCatalogFailsClosedWhenTheInspectedParameterCountIsMissingOrOffByOne()
    {
        OptimizationEvidenceCatalog catalog =
            PublishedGgufOptimizationEvidence.CreateCatalog();

        foreach (ulong? inspectedParameterCount in new ulong?[]
            { null, 2_999_999_999, 3_000_000_001 })
        {
            CrossRouteGenerationResult result = GenerateExactPublishedGguf(
                catalog, inspectedParameterCount);

            Assert.AreEqual(0, result.Candidates.Count);
            Assert.IsTrue(result.Exclusions.Any(exclusion =>
                exclusion.EvidenceId == "AB-04"
                && exclusion.Reason ==
                    OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
        }
    }

    [TestMethod]
    public void GenerationResultOwnsTrulyImmutableCollections()
    {
        OptimizationCandidate candidate = Generate(CrossRouteTestData.OpenVinoSnapshot(
            CrossRouteTestData.OpenVino("ov-u8", OpenVinoWeightFormat.Fp16))).Candidates.Single();
        List<OptimizationCandidate> candidates = [candidate];
        List<OptimizationExclusion> exclusions =
        [
            new("excluded", "descriptor", OptimizationExclusionReason.ExperimentalNotAdmitted)
        ];
        CrossRouteGenerationResult result = new(candidates, exclusions);

        candidates.Clear();
        exclusions.Clear();

        Assert.AreEqual(1, result.Candidates.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.ThrowsExactly<NotSupportedException>(
            () => ((IList<OptimizationCandidate>)result.Candidates).Clear());
        Assert.ThrowsExactly<NotSupportedException>(
            () => ((IList<OptimizationExclusion>)result.Exclusions).Clear());
    }

    [TestMethod]
    public void UndefinedSupportCannotGenerateThroughAMutatedOpenVinoAdmission()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-invalid-support", OpenVinoWeightFormat.Int8);
        SetSupportLevel(admitted, (SupportLevel)(-1));

        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(admitted));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void UndefinedSupportCannotSealAMutatedGgufAdmission()
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf(
            "gguf-invalid-support", GgufWeightFormat.Q4KM);
        SetSupportLevel(admitted, (SupportLevel)3);

        Assert.ThrowsExactly<ArgumentException>(() =>
            CrossRouteTestData.GgufSnapshot(admitted));
    }

    private static void SetSupportLevel(object admitted, SupportLevel level) =>
        admitted.GetType().GetField(
            "<Level>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(admitted, level);

    private static IEnumerable<OptimizationEvidenceRecord> FailedHardGateVariants(
        OptimizationEvidenceRecord evidence) =>
    [
        evidence with { OutputHealthPassed = false },
        evidence with { StabilityPassed = false },
        evidence with { ActivationPassed = false },
        evidence with { IntegrityPassed = false }
    ];

    private static CrossRouteGenerationResult GenerateExactPublishedOpenVino(
        OptimizationEvidenceCatalog catalog)
    {
        const string modelSha =
            "6da1029abd4a1464a94a571ac6a6a950c262b226c15912915a721e0643f7c708";
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "OV-TBQ3-CPU-INT4-01", OpenVinoWeightFormat.Int4,
            SupportLevel.Experimental, cache: OpenVinoKvCacheFormat.TurboQuantTbq3);
        OpenVinoExecutionAuthority execution = OpenVinoExecutionAuthority.Create(
            admitted.EvidenceId, "openvino.turboquant.cpu.int4.tbq3.v1",
            OpenVinoWeightPrecision.FourBit,
            OpenVinoBuildIdentity.Create(
                "2026.5.0-22950-f5f594dc0c9",
                "6fbc103538d30d42da4b0b5130a4792a20f728ba",
                "2026.5.0", Digest),
            new Dictionary<string, string> { ["openvino"] = "2026.5.0" },
            true,
            TurboQuantBuildIdentity.Create(Commit, Commit, Digest, Digest));
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-measured", Digest,
                OpenVinoCapabilityPayload.Create(
                    "2026.5.0-22950-f5f594dc0c9", [admitted], [execution]));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelSha, 1_832_903_073,
            "hw-run-1", Digest);

        return CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(ByteCount.FromBytes(1_832_903_073),
                40, 4096, 32, 8, 131072, null, null),
            CrossRouteTestData.Workload(), binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { admitted.EvidenceId },
            HardwareAuthority(),
            catalog,
            inspectedParameterCount: 3_000_000_000);
    }

    private static OptimizationEvidenceRecord[] VerifiedV4Records() =>
    [
        .. VerifiedGgufOptimizationEvidence.Records(
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            "93FA840C3DB0B623DDA3DAEBE27FE51564DCD292A420A9CF5A9A96D990E19DAA",
            PublishedGgufOptimizationEvidence.RuntimeBuildId,
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit)
    ];

    private static CrossRouteGenerationResult GenerateVerifiedBf16Q3(
        GgufQuantiserIdentity? quantizer,
        string? mutation = null)
    {
        IReadOnlySet<string> evidenceIds = new HashSet<string>
        {
            Bf16Q3EvidenceId,
        };
        string evidenceId = mutation == "evidence"
            ? "GGUF-V5-BF16-Q3-CPU-F16-MUTATED"
            : Bf16Q3EvidenceId;
        CompatibilityBackend backend = mutation is "backend" or "device" or "offload"
            ? CompatibilityBackend.IntelVulkan
            : CompatibilityBackend.Cpu;
        DeviceRouteId device = mutation == "device"
            ? DeviceRouteId.IntelDiscreteGpu
            : backend == CompatibilityBackend.IntelVulkan
                ? DeviceRouteId.IntelIntegratedGpu
                : DeviceRouteId.Cpu;
        GpuOffloadLevel offload = mutation == "offload"
            ? GpuOffloadLevel.Partial
            : backend == CompatibilityBackend.IntelVulkan
                ? GpuOffloadLevel.Full
                : GpuOffloadLevel.None;
        int contextTokens = mutation == "context" ? 8192 : 4096;
        GgufAdmittedConfiguration admission = GgufAdmittedConfiguration.Create(
            evidenceId,
            backend,
            device,
            GgufWeightFormat.Q3KM,
            mutation == "cache" ? GgufKvCacheFormat.Q8_0 : GgufKvCacheFormat.F16,
            offload,
            contextTokens,
            contextTokens,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);
        GgufExecutionProfileAuthority profile = GgufExecutionProfileAuthority.Create(
            evidenceId,
            EvidenceGrade.Measured,
            mutation == "profile" ? "other" : "cpu",
            mutation != "flash",
            mutation == "threads" ? 3 : 4,
            mutation == "batch" ? 511 : 512,
            mutation == "maximum" ? 255 : 256);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-bf16-q3",
            "mi-handoff-bf16-q3",
            Bf16SourceModelSha256,
            VerifiedGgufOptimizationEvidence.Bf16SourceModelLengthBytes,
            "hw-run-bf16-q3",
            Digest);
        GgufConversionSourceBinding source = GgufConversionSourceBinding.Create(
            WeightQuantisation.BF16, binding);
        GgufRuntimeAuthority runtime = GgufRuntimeAuthority.Create(
            PublishedGgufOptimizationEvidence.RuntimeBuildId,
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
            [profile]);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "verified-bf16-q3",
                Digest,
                GgufCapabilityPayload.Create(
                    PublishedGgufOptimizationEvidence.RuntimeBuildId,
                    [admission],
                    hasHigherPrecisionSource: true,
                    conversionSource: mutation == "converter" ? null : source,
                    admittedQuantiser: quantizer,
                    runtimeAuthority: runtime));
        OptimizationEvidenceCatalog catalog = new(
            VerifiedGgufOptimizationEvidence.Records(
                Bf16SourceModelSha256,
                VerifiedGgufOptimizationEvidence.Bf16SourceModelLengthBytes,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941",
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit));

        return CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(
                    VerifiedGgufOptimizationEvidence.Bf16SourceModelLengthBytes),
                40, 2560, 40, 8, 131072, 32, 2,
                VerifiedGgufOptimizationEvidence.ParameterCount),
            OptimizationWorkload.Create(
                "local-chat",
                contextTokens,
                OptimizationAssessment.Acceptable,
                [ContextTokenCount.FromTokens(contextTokens)]),
            binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            OptimizationHardwareAuthorityTestData.AllEstablished(),
            catalog,
            VerifiedGgufOptimizationEvidence.ParameterCount);
    }

    private static CrossRouteGenerationResult GenerateVerifiedV4Gguf(
        IReadOnlySet<string> optedIn,
        IReadOnlyList<GgufAdmittedConfiguration>? admissions = null,
        IReadOnlyList<GgufExecutionProfileAuthority>? profiles = null,
        IReadOnlyList<OptimizationEvidenceRecord>? evidence = null)
    {
        admissions ??= VerifiedGgufOptimizationEvidence.Admissions();
        profiles ??= VerifiedGgufOptimizationEvidence.ExecutionProfiles();
        evidence ??= VerifiedV4Records();
        OptimizationCapabilitySnapshot snapshot =
            VerifiedV4Snapshot(admissions, profiles);

        return CrossRouteCandidateGenerator.Generate(
            snapshot,
            InspectedModelFacts.Create(
                ByteCount.FromBytes(
                    VerifiedGgufOptimizationEvidence.SourceModelLengthBytes),
                40, 2560, 40, 8, 131072, 15, 2,
                VerifiedGgufOptimizationEvidence.ParameterCount),
            VerifiedV4Workload(),
            VerifiedV4Binding(),
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            optedIn,
            OptimizationHardwareAuthorityTestData.AllEstablished(),
            new OptimizationEvidenceCatalog(evidence),
            VerifiedGgufOptimizationEvidence.ParameterCount);
    }

    private static OptimizationCapabilitySnapshot VerifiedV4Snapshot(
        IReadOnlyList<GgufAdmittedConfiguration>? admissions = null,
        IReadOnlyList<GgufExecutionProfileAuthority>? profiles = null)
    {
        admissions ??= VerifiedGgufOptimizationEvidence.Admissions();
        profiles ??= VerifiedGgufOptimizationEvidence.ExecutionProfiles();
        GgufRuntimeAuthority runtime = GgufRuntimeAuthority.Create(
            PublishedGgufOptimizationEvidence.RuntimeBuildId,
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
            profiles);
        return OptimizationCapabilitySnapshot.ForGguf(
            "verified-v4-cpu",
            Digest,
            GgufCapabilityPayload.Create(
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                admissions,
                hasHigherPrecisionSource: false,
                turboQuantImplementation: admissions.Any(static item =>
                    item.KvCache is GgufKvCacheFormat.TurboQuant4Bit
                        or GgufKvCacheFormat.TurboQuant3Bit)
                    ? GgufTurboQuantImplementationIdentity.Create(
                        PublishedGgufOptimizationEvidence.RuntimeBuildId,
                        PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
                        CompatibilityBackend.Cpu,
                        DeviceRouteId.Cpu)
                    : null,
                runtimeAuthority: runtime));
    }

    private static OptimizationWorkload VerifiedV4Workload() =>
        OptimizationWorkload.Create(
            "local-chat",
            4096,
            OptimizationAssessment.Acceptable,
            [ContextTokenCount.FromTokens(4096)]);

    private static OptimizationJourneyBinding VerifiedV4Binding() =>
        OptimizationJourneyBinding.Create(
            "mi-run-v4",
            "mi-handoff-v4",
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            "hw-run-v4",
            Digest);

    private sealed class VerifiedV4Composer(
        GgufRuntimeAuthority runtime,
        string? mutation = null)
        : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;

        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;
            GgufExecutionProfileAuthority profile =
                runtime.Profiles[candidate.EvidenceId];
            return OptimizationExecutionPayload.ForGguf(
                GgufExecutionPayload.Create(
                    mutation == "runtime" ? "different-runtime" : runtime.RuntimeBuildId,
                    mutation == "commit"
                        ? "1111111111111111111111111111111111111111"
                        : runtime.RuntimeSourceCommit,
                    mutation == "backend" ? GgufRuntimeBackend.Vulkan : GgufRuntimeBackend.Cpu,
                    mutation == "device" ? "GPU" : "CPU",
                    mutation == "context"
                        ? candidate.Metrics.ContextTokens + 1
                        : candidate.Metrics.ContextTokens,
                    mutation == "cache"
                        ? GgufCacheType.Q8Zero
                        : ExecutionVocabularyMap.ToCacheType(configuration.KvCache),
                    mutation == "cache"
                        ? GgufCacheType.Q8Zero
                        : ExecutionVocabularyMap.ToCacheType(configuration.KvCache),
                    gpuLayerCount: 0,
                    mutation == "flash" ? !profile.FlashAttention : profile.FlashAttention,
                    mutation == "threads" ? profile.ThreadCount - 1 : profile.ThreadCount,
                    mutation == "batch" ? profile.BatchSize - 1 : profile.BatchSize,
                    mutation == "grade"
                        ? "Estimated"
                        : GgufEvidenceGradeMap.ToExecutionValue(profile.Evidence),
                    mutation == "profile" ? "other" : profile.ProfileId,
                    mutation == "maximum"
                        ? profile.MaximumGeneratedTokens - 1
                        : profile.MaximumGeneratedTokens,
                    GgufWeightFormat.Imported,
                    null));
        }
    }

    private sealed class EpochTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private static CrossRouteGenerationResult GenerateExactPublishedGguf(
        OptimizationEvidenceCatalog? catalog,
        ulong? inspectedParameterCount = 3_000_000_000,
        GgufWeightFormat weights = GgufWeightFormat.Imported, string evidenceId = "AB-04")
    {
        const string modelSha =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            evidenceId, CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            weights, GgufKvCacheFormat.Q8_0,
            GpuOffloadLevel.None, 512, 32768, SupportLevel.Experimental,
            requiresEvidence: true);
        GgufRuntimeAuthority runtime = GgufRuntimeAuthority.Create(
            PublishedGgufOptimizationEvidence.RuntimeBuildId,
            PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
            [GgufExecutionProfileAuthority.Create(
                admitted.EvidenceId, EvidenceGrade.Measured,
                "cpu-q4-k-m-q8", flashAttention: false, threadCount: 4,
                batchSize: 128, maximumGeneratedTokens: 256)]);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "atomicbot-measured", Digest,
            GgufCapabilityPayload.Create(
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                [admitted], hasHigherPrecisionSource: false,
                runtimeAuthority: runtime));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelSha, 3 * Gibibyte,
            "hw-run-1", Digest);

        return CrossRouteCandidateGenerator.Generate(
            snapshot, CrossRouteTestData.Facts(fileType: 15),
            CrossRouteTestData.Workload(), binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string> { admitted.EvidenceId },
            HardwareAuthority(), catalog,
            inspectedParameterCount: inspectedParameterCount);
    }

    private static CrossRouteGenerationResult GenerateCurrentGgufBaseline(
        OptimizationEvidenceCatalog? catalog,
        string runtimeBuild = PublishedGgufOptimizationEvidence.RuntimeBuildId,
        string runtimeCommit = PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
        ulong? inspectedParameterCount = 3_000_000_000,
        GgufKvCacheFormat cache = GgufKvCacheFormat.F16,
        SupportLevel level = SupportLevel.DeclaredSupported,
        bool requiresEvidence = false,
        EvidenceGrade profileEvidence = EvidenceGrade.Estimated)
    {
        const string modelSha =
            "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-current-cpu",
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GgufWeightFormat.Imported,
            cache,
            GpuOffloadLevel.None,
            512,
            32768,
            level,
            requiresEvidence);
        GgufRuntimeAuthority runtime = GgufRuntimeAuthority.Create(
            runtimeBuild,
            runtimeCommit,
            [GgufExecutionProfileAuthority.Create(
                admitted.EvidenceId,
                profileEvidence,
                "cpu-imported",
                flashAttention: false,
                threadCount: 4,
                batchSize: 128,
                maximumGeneratedTokens: 512)]);
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-current",
                Digest,
                GgufCapabilityPayload.Create(
                    runtimeBuild,
                    [admitted],
                    hasHigherPrecisionSource: false,
                    runtimeAuthority: runtime));
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "mi-run-1", "mi-handoff-1", modelSha, 3 * Gibibyte,
            "hw-run-1", Digest);

        return CrossRouteCandidateGenerator.Generate(
            snapshot,
            CrossRouteTestData.Facts(fileType: 15),
            OptimizationWorkload.Create(
                "chat",
                512,
                OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(32768)]),
            binding,
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(),
            catalog,
            inspectedParameterCount);
    }

    private static class CrossRouteTestData
    {
        internal static InspectedModelFacts Facts(int fileType = 15) =>
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), 32, 4096, 32, 8, 8192, fileType, 2);

        internal static OptimizationWorkload Workload(
            OptimizationAssessment floor = OptimizationAssessment.Poor,
            int minimumContext = 512) =>
            OptimizationWorkload.Create(
                "chat",
                minimumContext,
                floor,
                [ContextTokenCount.FromTokens(4096)]);

        internal static OpenVinoAdmittedConfiguration OpenVino(
            string id,
            OpenVinoWeightFormat weights,
            SupportLevel level = SupportLevel.DeclaredSupported,
            DeviceRouteId device = DeviceRouteId.Cpu,
            OpenVinoKvCacheFormat cache = OpenVinoKvCacheFormat.U8) =>
            OpenVinoAdmittedConfiguration.Create(
                id, device, weights, cache,
                OpenVinoPerformanceHint.Latency, OpenVinoCompiledCachePolicy.Disabled,
                1, 512, 32768, level, level == SupportLevel.Experimental);

        internal static GgufAdmittedConfiguration Gguf(
            string id, GgufWeightFormat weights,
            SupportLevel level = SupportLevel.DeclaredSupported) =>
            GgufAdmittedConfiguration.Create(
                id, CompatibilityBackend.Cpu, DeviceRouteId.Cpu, weights,
                GgufKvCacheFormat.F16, GpuOffloadLevel.None, 512, 32768, level,
                level == SupportLevel.Experimental);

        internal static OptimizationCapabilitySnapshot OpenVinoSnapshot(
            params OpenVinoAdmittedConfiguration[] admitted) =>
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", Digest, OpenVinoCapabilityPayload.Create(
                    SyntheticQualityEvidence.OpenVinoBuild, admitted,
                    [.. admitted.Select(entry => OpenVinoExecutionAuthority.Create(
                        entry.EvidenceId,
                        entry.EvidenceId,
                        OpenVinoWeightPrecision.Fp16,
                        OpenVinoBuildIdentity.Create(
                            SyntheticQualityEvidence.OpenVinoBuild, "2026.1.0", "2026.1.0", Digest),
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["openvino"] = "2026.1.0"
                        },
                        compiledCacheIsDisposable: true,
                        turboQuantBuild: entry.KvCache is OpenVinoKvCacheFormat.TurboQuantTbq4
                            or OpenVinoKvCacheFormat.TurboQuantTbq3
                            ? TurboQuantBuildIdentity.Create(
                                Commit, Commit, Digest, Digest)
                            : null))]));

        internal static OptimizationCapabilitySnapshot GgufSnapshot(
            params GgufAdmittedConfiguration[] admitted) =>
            GgufSnapshotWithPolicy(null, false, admitted);

        internal static OptimizationCapabilitySnapshot GgufSnapshotWithPolicy(
            GgufRequantisationPolicy? requantisationPolicy = null,
            bool hasHigherPrecisionSource = false,
            params GgufAdmittedConfiguration[] admitted)
        {
            GgufConversionSourceBinding? source = hasHigherPrecisionSource
                ? Source(WeightQuantisation.F16)
                : requantisationPolicy?.Source;

            return OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", Digest, GgufCapabilityPayload.Create(
                    PublishedGgufOptimizationEvidence.RuntimeBuildId, admitted, hasHigherPrecisionSource, requantisationPolicy,
                    source, source is null ? null : Quantiser(),
                    runtimeAuthority: GgufRuntimeAuthority.Create(
                        PublishedGgufOptimizationEvidence.RuntimeBuildId, PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
                        [.. admitted.Select(entry =>
                            GgufExecutionProfileAuthority.Create(
                                entry.EvidenceId, EvidenceGrade.Estimated, "profile",
                                false, 4, 128, 256))])));
        }

        internal static OptimizationJourneyBinding Binding() =>
            OptimizationJourneyBinding.Create(
                "mi-run-1", "mi-handoff-1", Digest, 3 * Gibibyte,
                "hw-run-1", Digest);

        internal static GgufQuantiserIdentity Quantiser() =>
            GgufQuantiserIdentity.Create("llama-quantize", "b4321", Digest);

        internal static GgufRuntimeAuthority RuntimeAuthority(
            string runtime,
            string sourceCommit,
            params GgufAdmittedConfiguration[] admitted) =>
            GgufRuntimeAuthority.Create(
                runtime, sourceCommit,
                [.. admitted.Select(entry => GgufExecutionProfileAuthority.Create(
                    entry.EvidenceId, EvidenceGrade.Estimated, "profile",
                    false, 4, 128, 256))]);

        internal static GgufConversionSourceBinding Source(
            WeightQuantisation precision = WeightQuantisation.Q4_K_M) =>
            GgufConversionSourceBinding.Create(precision, Binding());

        internal static GgufRequantisationPolicy Requantisation(
            string evidenceId = "gguf-q2",
            bool acknowledged = true,
            bool preserveOriginal = true,
            bool requireNewOutput = true) =>
            GgufRequantisationPolicy.Create(
                acknowledged,
                preserveOriginal,
                requireNewOutput,
                Gguf(evidenceId, GgufWeightFormat.Q2K),
                Quantiser(),
                Source());
    }

    private static CrossRouteGenerationResult Generate(
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload? workload = null,
        InspectedModelFacts? facts = null,
        ulong budgetGibibytes = 32,
        ulong diskGibibytes = 500,
        params string[] optedIn) =>
        SyntheticQualityEvidence.Generate(
            snapshot,
            facts ?? CrossRouteTestData.Facts(),
            workload ?? CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(),
            ByteCount.FromBytes(budgetGibibytes * Gibibyte),
            ByteCount.FromBytes(diskGibibytes * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(optedIn),
            HardwareAuthority());

    private static OptimizationHardwareAuthority HardwareAuthority() =>
        OptimizationHardwareAuthority.Create(
            Digest,
            [DeviceRouteId.Cpu, DeviceRouteId.IntelIntegratedGpu,
             DeviceRouteId.IntelDiscreteGpu, DeviceRouteId.IntelNpu],
            [CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl,
             CompatibilityBackend.IntelVulkan, CompatibilityBackend.OpenVinoCpu,
             CompatibilityBackend.OpenVinoGpu, CompatibilityBackend.OpenVinoNpu],
            ByteCount.FromBytes(64 * Gibibyte),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "test-freshness-v1");

    private static OptimizationHardwareAuthority HardwareAuthority(
        DeviceRouteId device,
        CompatibilityBackend backend,
        ulong? dedicatedBytes = 64 * Gibibyte) =>
        OptimizationHardwareAuthority.Create(
            Digest, [device], [backend],
            dedicatedBytes.HasValue
                ? ByteCount.FromBytes(dedicatedBytes.Value)
                : null,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            "test-freshness-v1");

    [TestMethod]
    public void BothRoutesGenerateCompleteCandidates()
    {
        CrossRouteGenerationResult openVino = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8)));

        CrossRouteGenerationResult gguf = Generate(
            CrossRouteTestData.GgufSnapshot(
                CrossRouteTestData.Gguf("gguf-q4", GgufWeightFormat.Q4KM)));

        Assert.AreEqual(1, openVino.Candidates.Count);
        Assert.AreEqual(1, gguf.Candidates.Count);
        Assert.AreEqual(OptimizationRoute.OpenVino, openVino.Candidates[0].Route);
        Assert.AreEqual(OptimizationRoute.Gguf, gguf.Candidates[0].Route);
    }

    [TestMethod]
    public void AlternativeClaimingAbsentOpenVinoNpuIsExcluded()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-npu", OpenVinoWeightFormat.Int8,
            device: DeviceRouteId.IntelNpu);
        CrossRouteGenerationResult result = SyntheticQualityEvidence.Generate(
            CrossRouteTestData.OpenVinoSnapshot(admitted),
            CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.Cpu, CompatibilityBackend.OpenVinoCpu));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.HardwareCapabilityUnavailable,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void AlternativeClaimingAbsentGgufGpuBackendIsExcluded()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-vulkan", CompatibilityBackend.IntelVulkan,
            DeviceRouteId.IntelIntegratedGpu, GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16, GpuOffloadLevel.Full, 512, 8192,
            SupportLevel.DeclaredSupported, false);
        CrossRouteGenerationResult result = SyntheticQualityEvidence.Generate(
            CrossRouteTestData.GgufSnapshot(admitted),
            CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.Cpu, CompatibilityBackend.Cpu));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.HardwareCapabilityUnavailable,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void DedicatedMemoryDemandRequiresEstablishedSufficientDedicatedBudget()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-gpu", OpenVinoWeightFormat.Fp16,
            device: DeviceRouteId.IntelDiscreteGpu);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.OpenVinoSnapshot(admitted);

        CrossRouteGenerationResult unknown = SyntheticQualityEvidence.Generate(
            snapshot, CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, null));
        CrossRouteGenerationResult insufficient = SyntheticQualityEvidence.Generate(
            snapshot, CrossRouteTestData.Facts(), CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(), ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte), EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, 1));

        Assert.AreEqual(
            OptimizationExclusionReason.DedicatedMemoryNotEstablished,
            unknown.Exclusions.Single().Reason);
        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsDedicatedDeviceMemory,
            insufficient.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void AlternativeDedicatedBudget_ExactBoundaryFits_OneByteLessFails()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-gpu-boundary", OpenVinoWeightFormat.Fp16,
            device: DeviceRouteId.IntelDiscreteGpu);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.OpenVinoSnapshot(admitted);
        InspectedModelFacts facts = CrossRouteTestData.Facts();
        OptimizationWorkload workload = CrossRouteTestData.Workload();
        ContextTokenCount context = workload.CandidateContexts.Single();
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            admitted.Weights, admitted.KvCache, admitted.Device,
            admitted.PerformanceHint, admitted.CompiledCache, admitted.Streams);
        ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
            facts, configuration, context, EstimatorPolicy.ProvisionalV1());
        ulong dedicatedPeak = ResourcePhaseComposer.Compose(estimate.Components)
            .PeakFor(ResourceTarget.DedicatedDeviceMemory).Bytes;

        CrossRouteGenerationResult exact = SyntheticQualityEvidence.Generate(
            snapshot, facts, workload, CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, dedicatedPeak));
        CrossRouteGenerationResult shortByOne = SyntheticQualityEvidence.Generate(
            snapshot, facts, workload, CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte), ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            HardwareAuthority(DeviceRouteId.IntelDiscreteGpu,
                CompatibilityBackend.OpenVinoGpu, dedicatedPeak - 1));

        Assert.AreEqual(1, exact.Candidates.Count);
        Assert.AreEqual(
            dedicatedPeak,
            exact.Candidates.Single().Metrics.DedicatedRequiredBytes);
        Assert.AreEqual(
            dedicatedPeak,
            exact.Candidates.Single().Metrics.DedicatedSafeBudgetBytes);
        Assert.AreEqual(
            0UL,
            exact.Candidates.Single().Metrics.DedicatedHeadroomBytes);
        OptimizationAdmissionProof proof =
            exact.Candidates.Single().AdmissionProof!;
        Assert.IsNotNull(proof);
        Assert.AreEqual(
            dedicatedPeak,
            proof.DedicatedRequiredBytes);
        Assert.AreEqual(
            dedicatedPeak,
            proof.DedicatedSafeBudgetBytes);
        Assert.AreEqual(
            0UL,
            proof.DedicatedHeadroomBytes);
        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsDedicatedDeviceMemory,
            shortByOne.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void CpuCandidateCarriesNoFabricatedDedicatedMemoryAxis()
    {
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-cpu", OpenVinoWeightFormat.Original))).Candidates.Single();

        Assert.IsNull(candidate.Metrics.DedicatedRequiredBytes);
        Assert.IsNull(candidate.Metrics.DedicatedSafeBudgetBytes);
        Assert.IsNull(candidate.Metrics.DedicatedHeadroomBytes);
    }

    [TestMethod]
    public void GgufCandidateWithoutExactRuntimeProfileAuthorityIsTypedExcluded()
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf(
            "gguf-no-runtime-authority", GgufWeightFormat.Imported);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create("b4321", [admitted]));

        CrossRouteGenerationResult result = Generate(snapshot);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExecutionAuthorityNotEstablished,
            result.Exclusions[0].Reason);
    }

    [TestMethod]
    public void OpenVinoCandidateWithoutExactExecutionAuthorityIsTypedExcluded()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-no-execution-authority", OpenVinoWeightFormat.Original);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForOpenVino(
            "ov-cap", Digest,
            OpenVinoCapabilityPayload.Create("2026.1.0", [admitted]));

        CrossRouteGenerationResult result = Generate(snapshot);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExecutionAuthorityNotEstablished,
            result.Exclusions[0].Reason);
    }

    [TestMethod]
    public void ReleasedEvidenceRequirementIsFailClosedWithoutItsExactOptIn()
    {
        GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
            "gguf-evidence-bound", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, requiresEvidence: true);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.GgufSnapshot(admitted);

        CrossRouteGenerationResult absent = Generate(snapshot);
        CrossRouteGenerationResult exact = Generate(snapshot, optedIn: admitted.EvidenceId);

        Assert.AreEqual(0, absent.Candidates.Count);
        Assert.AreEqual(1, absent.Exclusions.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            absent.Exclusions[0].Reason);
        Assert.AreEqual(1, exact.Candidates.Count);
    }

    [TestMethod]
    public void GgufTurboQuantRuntimeAliasesAndOtherForksCannotReuseAtomicBotEvidence()
    {
        var implementations = new[]
        {
            new
            {
                Evidence = "turbo3",
                Runtime = "turbo3",
                Source = "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                Backend = CompatibilityBackend.IntelVulkan,
                Device = DeviceRouteId.IntelIntegratedGpu
            },
            new
            {
                Evidence = "tq3_0",
                Runtime = "tq3_0",
                Source = "5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc",
                Backend = CompatibilityBackend.IntelSycl,
                Device = DeviceRouteId.IntelDiscreteGpu
            }
        };

        foreach (var implementation in implementations)
        {
            GgufAdmittedConfiguration admitted = GgufAdmittedConfiguration.Create(
                implementation.Evidence,
                implementation.Backend,
                implementation.Device,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.TurboQuant3Bit,
                GpuOffloadLevel.Full,
                512,
                32768,
                SupportLevel.Experimental,
                requiresEvidence: true);
            OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap",
                Digest,
                GgufCapabilityPayload.Create(
                    implementation.Runtime,
                    [admitted],
                    turboQuantImplementation:
                        GgufTurboQuantImplementationIdentity.Create(
                            implementation.Runtime,
                            implementation.Source,
                            implementation.Backend,
                            implementation.Device),
                    runtimeAuthority: CrossRouteTestData.RuntimeAuthority(
                        implementation.Runtime, implementation.Source, admitted)));

            CrossRouteGenerationResult absent = Generate(snapshot);
            Assert.AreEqual(0, absent.Candidates.Count);
            Assert.AreEqual(
                OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
                absent.Exclusions.Single().Reason);

            CrossRouteGenerationResult optedIn = Generate(
                snapshot,
                optedIn: implementation.Evidence);
            Assert.AreEqual(0, optedIn.Candidates.Count);
            Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
                optedIn.Exclusions.Single().Reason);

            Assert.AreEqual(implementation.Runtime, snapshot.Gguf!.RuntimeVersion);
            Assert.AreEqual(
                implementation.Source,
                snapshot.Gguf.TurboQuantImplementation!.SourceCommit);
        }
    }

    [TestMethod]
    public void CandidateRouteAlwaysAgreesWithItsConfiguration()
    {
        // the discriminator is derived, not supplied, so nothing can label a
        // GGUF configuration as OpenVINO and send it to the wrong executor.
        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8))).Candidates)
        {
            Assert.IsInstanceOfType<OpenVinoRouteConfiguration>(candidate.Configuration);
        }

        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.GgufSnapshot(
                CrossRouteTestData.Gguf("gguf-q4", GgufWeightFormat.Q4KM))).Candidates)
        {
            Assert.IsInstanceOfType<GgufRouteConfiguration>(candidate.Configuration);
        }
    }

    [TestMethod]
    public void SharedMetricsCarryNoRouteVocabulary()
    {
        // route-field leakage check. everything the shared layer ranks on must
        // be expressible for both routes, so the metrics record may not name a
        // representation from either
        string[] properties = typeof(OptimizationCandidateMetrics)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (string name in properties)
        {
            foreach (string foreign in new[] { "Gguf", "OpenVino", "Quant", "Kv" })
            {
                Assert.IsFalse(
                    name.Contains(foreign, StringComparison.OrdinalIgnoreCase),
                    $"Shared metrics expose the route-specific member {name}.");
            }
        }
    }

    [TestMethod]
    public void ExperimentalEntryIsAbsentWithoutAnExactOptIn()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-tbq4", OpenVinoWeightFormat.Int4, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq4)));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExperimentalNotAdmitted,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void OptingInToOneExperimentalEntryDoesNotAdmitAnother()
    {
        // Opting in is per evidence record. A user who accepted one
        // experimental route has not accepted every experimental route
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-standard-a", OpenVinoWeightFormat.Int4, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.U8),
                CrossRouteTestData.OpenVino(
                    "ov-standard-b", OpenVinoWeightFormat.Int4, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.F16)),
            optedIn: "ov-standard-a");

        Assert.AreEqual(1, result.Candidates.Count);
        Assert.AreEqual("ov-standard-a", result.Candidates[0].EvidenceId);
        Assert.IsTrue(result.Candidates[0].IsExperimental);
    }

    [TestMethod]
    public void TurboQuantCacheCannotUseSyntheticQualityWithoutActivationAuthority()
    {
        OpenVinoAdmittedConfiguration admitted = CrossRouteTestData.OpenVino(
            "ov-tbq4",
            OpenVinoWeightFormat.Fp16,
            SupportLevel.Experimental,
            cache: OpenVinoKvCacheFormat.TurboQuantTbq4);
        Assert.ThrowsExactly<ArgumentException>(() =>
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap", Digest, OpenVinoCapabilityPayload.Create(
                    SyntheticQualityEvidence.OpenVinoBuild,
                    [admitted],
                    [OpenVinoExecutionAuthority.Create(
                        admitted.EvidenceId,
                        admitted.EvidenceId,
                        OpenVinoWeightPrecision.Fp16,
                        OpenVinoBuildIdentity.Create(
                            SyntheticQualityEvidence.OpenVinoBuild,
                            "2026.1.0",
                            "2026.1.0",
                            Digest),
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["openvino"] = "2026.1.0"
                        }, compiledCacheIsDisposable: true)])));
    }

    [TestMethod]
    public void CacheCompressionPreservesWeightIdentityAndAddsItsOwnQualityEffect()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "f16-cache", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "u8-cache", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U8)));

        OptimizationCandidate f16 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "f16-cache");
        OptimizationCandidate u8 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "u8-cache");

        Assert.AreEqual(
            OpenVinoWeightFormat.Fp16,
            ((OpenVinoRouteConfiguration)f16.Configuration).Weights);
        Assert.AreEqual(
            OpenVinoWeightFormat.Fp16,
            ((OpenVinoRouteConfiguration)u8.Configuration).Weights);
        Assert.AreEqual(OptimizationAssessment.Excellent, f16.Metrics.Quality);
        Assert.AreEqual(OptimizationAssessment.Good, u8.Metrics.Quality);
    }

    [TestMethod]
    public void WeightCompressionPreservesCacheIdentityAndAddsItsOwnQualityEffect()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "fp16-weights", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "int8-weights", OpenVinoWeightFormat.Int8,
                    cache: OpenVinoKvCacheFormat.F16)));

        OptimizationCandidate fp16 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "fp16-weights");
        OptimizationCandidate int8 = result.Candidates.Single(
            candidate => candidate.EvidenceId == "int8-weights");

        Assert.AreEqual(
            OpenVinoKvCacheFormat.F16,
            ((OpenVinoRouteConfiguration)fp16.Configuration).KvCache);
        Assert.AreEqual(
            OpenVinoKvCacheFormat.F16,
            ((OpenVinoRouteConfiguration)int8.Configuration).KvCache);
        Assert.AreEqual(OptimizationAssessment.Excellent, fp16.Metrics.Quality);
        Assert.AreEqual(OptimizationAssessment.Good, int8.Metrics.Quality);
    }

    [TestMethod]
    public void BelowFloorTurboQuantEvidenceIsExcludedEvenWithConsent()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-tbq3",
                    OpenVinoWeightFormat.Fp16,
                    SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3)),
            workload: CrossRouteTestData.Workload(
                floor: OptimizationAssessment.Acceptable),
            optedIn: "ov-tbq3");

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void Q2KWithoutExactEvidenceIsExcluded()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                CrossRouteTestData.Requantisation(),
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)),
            workload: CrossRouteTestData.Workload(OptimizationAssessment.Acceptable));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(GgufWeightFormat.Imported)]
    [DataRow(GgufWeightFormat.Q2K)]
    public void AlreadyQ2KSourceWithoutExactEvidenceCannotAcquireAStaticScore(
        GgufWeightFormat admittedWeights)
    {
        OptimizationCapabilitySnapshot snapshot = CrossRouteTestData.GgufSnapshot(
            CrossRouteTestData.Gguf("gguf-existing-q2", admittedWeights));

        CrossRouteGenerationResult excluded = Generate(
            snapshot,
            workload: CrossRouteTestData.Workload(OptimizationAssessment.Acceptable),
            facts: CrossRouteTestData.Facts(fileType: 10));

        Assert.AreEqual(0, excluded.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            excluded.Exclusions.Single().Reason);

        CrossRouteGenerationResult admitted = Generate(
            snapshot,
            facts: CrossRouteTestData.Facts(fileType: 10));
        Assert.AreEqual(0, admitted.Candidates.Count);
    }

    [TestMethod]
    public void QuantisedSourceIsNotRequantisedByDefault()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.RequantisationNotAuthorized,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(WeightQuantisation.F32)]
    [DataRow(WeightQuantisation.BF16)]
    [DataRow(WeightQuantisation.F16)]
    public void HigherPrecisionSourceAloneCannotAdmitAnUnmeasuredConversion(
        WeightQuantisation sourcePrecision)
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        GgufConversionSourceBinding source =
            CrossRouteTestData.Source(sourcePrecision);
        CrossRouteGenerationResult result = Generate(
            OptimizationCapabilitySnapshot.ForGguf(
                "gguf-cap", Digest,
                GgufCapabilityPayload.Create(
                    "b4321", [admitted], hasHigherPrecisionSource: true,
                    conversionSource: source,
                    admittedQuantiser: CrossRouteTestData.Quantiser(),
                    runtimeAuthority: CrossRouteTestData.RuntimeAuthority(
                        "b4321", Commit, admitted))));
        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    public void ControlledRequantisationRequiresEveryExplicitSafetyFact(
        bool acknowledged, bool preserveOriginal, bool requireNewOutput)
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                CrossRouteTestData.Requantisation(
                    acknowledged: acknowledged,
                    preserveOriginal: preserveOriginal,
                    requireNewOutput: requireNewOutput),
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.RequantisationNotAuthorized,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void ControlledRequantisationConsentCannotReplaceMissingQualityEvidence()
    {
        Assert.AreEqual(
            0,
            Generate(
                CrossRouteTestData.GgufSnapshotWithPolicy(
                    CrossRouteTestData.Requantisation("different-evidence"),
                    admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)))
                .Candidates.Count);

        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.GgufSnapshotWithPolicy(
                CrossRouteTestData.Requantisation(),
                admitted: CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K)));
        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    [DataRow(7, WeightQuantisation.Q8_0, GgufWeightFormat.Q4KM,
        OptimizationCandidateNotice.Requantisation)]
    [DataRow(15, WeightQuantisation.Q4_K_M, GgufWeightFormat.Q3KM,
        OptimizationCandidateNotice.Requantisation)]
    [DataRow(15, WeightQuantisation.Q4_K_M, GgufWeightFormat.Q2K,
        OptimizationCandidateNotice.LowQualityRequantisation)]
    public void ControlledRequantisationRequiresEvidenceForTheExactSourceTargetPair(
        int fileType,
        WeightQuantisation sourcePrecision,
        GgufWeightFormat target,
        OptimizationCandidateNotice expectedNotice)
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf("same-id", target);
        GgufConversionSourceBinding source = CrossRouteTestData.Source(sourcePrecision);
        GgufQuantiserIdentity quantiser = CrossRouteTestData.Quantiser();
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, admitted, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], false, policy, source, quantiser,
                runtimeAuthority: CrossRouteTestData.RuntimeAuthority(
                    "b4321", Commit, admitted)));

        CrossRouteGenerationResult result = Generate(
            snapshot, facts: CrossRouteTestData.Facts(fileType));
        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(OptimizationExclusionReason.EvidenceBelowAdmissionLevel,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void BooleanAloneNeverAuthorizesOrdinaryConversion()
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], hasHigherPrecisionSource: true));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void BoundSourceWithoutAnAdmittedQuantiserNeverAuthorizesConversion()
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], hasHigherPrecisionSource: true,
                conversionSource: CrossRouteTestData.Source(WeightQuantisation.F16)));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void BoundSourceMustBeStrictlyHigherPrecisionThanTheTarget()
    {
        GgufAdmittedConfiguration admitted =
            CrossRouteTestData.Gguf("gguf-q2", GgufWeightFormat.Q2K);
        GgufConversionSourceBinding source =
            CrossRouteTestData.Source(WeightQuantisation.Q2_K);
        GgufQuantiserIdentity quantiser = CrossRouteTestData.Quantiser();
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, admitted, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [admitted], false, policy, source, quantiser));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void PolicyForSameEvidenceButDifferentTargetDoesNotAuthorize()
    {
        GgufAdmittedConfiguration authorized =
            CrossRouteTestData.Gguf("same-id", GgufWeightFormat.Q3KM);
        GgufAdmittedConfiguration requested =
            CrossRouteTestData.Gguf("same-id", GgufWeightFormat.Q2K);
        GgufConversionSourceBinding source = CrossRouteTestData.Source();
        GgufQuantiserIdentity quantiser = CrossRouteTestData.Quantiser();
        GgufRequantisationPolicy policy = GgufRequantisationPolicy.Create(
            true, true, true, authorized, quantiser, source);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf(
            "gguf-cap", Digest,
            GgufCapabilityPayload.Create(
                "b4321", [requested], false, policy, source, quantiser));

        Assert.AreEqual(0, Generate(snapshot).Candidates.Count);
    }

    [TestMethod]
    public void RequantisationPolicyCarriesIdentitiesButNoPath()
    {
        GgufRequantisationPolicy policy = CrossRouteTestData.Requantisation();

        Assert.AreEqual(CrossRouteTestData.Quantiser(), policy.Quantiser);
        Assert.AreEqual(CrossRouteTestData.Binding(), policy.Source.Journey);
        Assert.IsFalse(typeof(GgufRequantisationPolicy).GetProperties().Any(property =>
            property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("File", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Directory", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void AutomaticAvoidsTbq3WhileAFittingAcceptableOrBetterCacheExists()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-u8", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U8),
                CrossRouteTestData.OpenVino(
                    "ov-tbq3",
                    OpenVinoWeightFormat.Fp16,
                    SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3)),
            optedIn: "ov-tbq3");

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            result.Candidates,
            OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("No candidate was selected.");

        Assert.AreEqual("ov-u8", selection.Candidate.EvidenceId);
        Assert.IsTrue(
            selection.Candidate.Metrics.Quality >= OptimizationAssessment.Acceptable);
    }

    [TestMethod]
    public void OpenVinoFallbackSetContainsOnlyExactlyAdmittedEvidence()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "tbq3", OpenVinoWeightFormat.Fp16, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq3),
                CrossRouteTestData.OpenVino(
                    "tbq4", OpenVinoWeightFormat.Fp16, SupportLevel.Experimental,
                    cache: OpenVinoKvCacheFormat.TurboQuantTbq4),
                CrossRouteTestData.OpenVino(
                    "u4", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U4),
                CrossRouteTestData.OpenVino(
                    "u8", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.U8),
                CrossRouteTestData.OpenVino(
                    "f16", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "bf16", OpenVinoWeightFormat.Fp16,
                    cache: OpenVinoKvCacheFormat.Bf16)),
            optedIn: "tbq4");

        CollectionAssert.AreEquivalent(
            new[] { "tbq4", "u4", "u8", "f16" },
            result.Candidates.Select(candidate => candidate.EvidenceId).ToArray());
        Assert.AreEqual(
            OptimizationExclusionReason.ExperimentalNotAdmitted,
            result.Exclusions.Single(exclusion => exclusion.EvidenceId == "tbq3").Reason);
    }

    [TestMethod]
    public void AutomaticMayChooseConversionWhenCurrentRepresentationDoesNotFit()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "current", OpenVinoWeightFormat.Original,
                    cache: OpenVinoKvCacheFormat.F16),
                CrossRouteTestData.OpenVino(
                    "converted", OpenVinoWeightFormat.Int4,
                    cache: OpenVinoKvCacheFormat.U4)),
            budgetGibibytes: 2);

        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsSafeMemoryBudget,
            result.Exclusions.Single(exclusion => exclusion.EvidenceId == "current").Reason);

        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            result.Candidates,
            OptimizationPreferenceSelection.Automatic())
            ?? throw new AssertFailedException("No fitting conversion was selected.");

        Assert.AreEqual("converted", selection.Candidate.EvidenceId);
        Assert.IsTrue(selection.Candidate.Metrics.RequiresPersistentChange);
    }

    [TestMethod]
    public void CandidateExceedingTheSafeBudgetIsExcludedWithAReason()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-fp16", OpenVinoWeightFormat.Fp16)),
            budgetGibibytes: 1);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.ExceedsSafeMemoryBudget,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void CandidateWithoutDiskSpaceIsExcludedWithAReason()
    {
        // a conversion that cannot be written is not a cheaper conversion
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8)),
            diskGibibytes: 0);

        Assert.AreEqual(0, result.Candidates.Count);
        OptimizationExclusion exclusion = result.Exclusions.Single();
        Assert.AreEqual(
            OptimizationExclusionReason.InsufficientDiskSpace,
            exclusion.Reason);
        Assert.IsNotNull(
            exclusion.EstimatedRequiredBytes,
            "A disk shortage must not erase an already-established runtime RAM estimate.");
        Assert.IsTrue(exclusion.EstimatedRequiredBytes > 0);
        Assert.IsNotNull(exclusion.EstimatedDiskRequiredBytes);
        Assert.IsTrue(exclusion.EstimatedDiskRequiredBytes > 0);
        Assert.AreEqual(0UL, exclusion.AvailableDiskBytes);
    }

    [TestMethod]
    public void DiskBlockedGgufConversionRetainsItsEstablishedRuntimeRamEstimate()
    {
        GgufAdmittedConfiguration admitted = CrossRouteTestData.Gguf(
            "gguf-q4", GgufWeightFormat.Q4KM);
        OptimizationCapabilitySnapshot snapshot =
            CrossRouteTestData.GgufSnapshotWithPolicy(
                hasHigherPrecisionSource: true,
                admitted: admitted);

        CrossRouteGenerationResult result = Generate(snapshot, diskGibibytes: 0);

        OptimizationExclusion exclusion = result.Exclusions.Single();
        Assert.AreEqual(
            OptimizationExclusionReason.InsufficientDiskSpace,
            exclusion.Reason);
        Assert.IsNotNull(
            exclusion.EstimatedRequiredBytes,
            "GGUF and OpenVINO must preserve the same established RAM fact when disk is the blocker.");
        Assert.IsTrue(exclusion.EstimatedRequiredBytes > 0);
        Assert.IsNotNull(exclusion.EstimatedDiskRequiredBytes);
        Assert.AreEqual(0UL, exclusion.AvailableDiskBytes);
    }

    [TestMethod]
    public void DiskShortageCannotPromoteABelowFloorCandidateIntoTheMemoryTargetSet()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int4", OpenVinoWeightFormat.Int4)),
            workload: CrossRouteTestData.Workload(
                floor: OptimizationAssessment.Excellent),
            diskGibibytes: 0);

        OptimizationExclusion exclusion = result.Exclusions.Single();
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            exclusion.Reason,
            "Only formats that pass the quality floor may become the smallest acceptable RAM target.");
        Assert.IsNull(exclusion.EstimatedRequiredBytes);
    }

    [TestMethod]
    public void MemoryShortageCannotPromoteABelowFloorCandidateIntoTheMemoryTargetSet()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int4", OpenVinoWeightFormat.Int4)),
            workload: CrossRouteTestData.Workload(
                floor: OptimizationAssessment.Excellent),
            budgetGibibytes: 1);

        OptimizationExclusion exclusion = result.Exclusions.Single();
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            exclusion.Reason);
        Assert.IsNull(exclusion.EstimatedRequiredBytes);
    }

    [TestMethod]
    public void MemoryShortageCannotPromoteABelowContextCandidateIntoTheMemoryTargetSet()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int4", OpenVinoWeightFormat.Int4)),
            workload: CrossRouteTestData.Workload(minimumContext: 8192),
            budgetGibibytes: 1);

        OptimizationExclusion exclusion = result.Exclusions.Single();
        Assert.AreEqual(
            OptimizationExclusionReason.ContextBelowWorkloadMinimum,
            exclusion.Reason);
        Assert.IsNull(exclusion.EstimatedRequiredBytes);
    }

    [TestMethod]
    public void RuntimeOnlyCandidateAlsoFailsClosedWhenDiskObservationIsZero()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-original", OpenVinoWeightFormat.Original)),
            diskGibibytes: 0);

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.InsufficientDiskSpace,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void GeneratedCandidateCarriesTheExactDiskObservationUsedForAdmission()
    {
        const ulong availableGibibytes = 123;
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int8", OpenVinoWeightFormat.Int8)),
            diskGibibytes: availableGibibytes).Candidates.Single();

        Assert.AreEqual(
            availableGibibytes * Gibibyte,
            candidate.Metrics.AvailableDiskBytes);
        Assert.IsTrue(candidate.Metrics.FitsDiskSafely);
    }

    [TestMethod]
    public void UnestimableCandidateBecomesATypedExclusionNotAZero()
    {
        // the whole point. a zero peak would be the cheapest option on the
        // board and would win every efficiency band, so an estimate that could
        // not be established must leave nothing behind to rank
        CrossRouteGenerationResult result = SyntheticQualityEvidence.Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8)),
            InspectedModelFacts.Create(
                ByteCount.FromBytes(3 * Gibibyte), null, null, null, null, 8192, 15, 2),
            CrossRouteTestData.Workload(),
            CrossRouteTestData.Binding(),
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            EstimatorPolicy.ProvisionalV1(),
            new HashSet<string>(),
            HardwareAuthority());

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.EstimateNotEstablished,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void CandidateBelowTheQualityFloorIsExcluded()
    {
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int4", OpenVinoWeightFormat.Int4)),
            CrossRouteTestData.Workload(floor: OptimizationAssessment.Excellent));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            OptimizationExclusionReason.QualityBelowFloor,
            result.Exclusions.Single().Reason);
    }

    [TestMethod]
    public void EntryOutsideItsOwnContextBoundsGeneratesNothing()
    {
        OpenVinoAdmittedConfiguration narrow = OpenVinoAdmittedConfiguration.Create(
            "ov-narrow", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled, 1,
            minimumContextTokens: 8192, maximumContextTokens: 16384,
            SupportLevel.DeclaredSupported, false);

        Assert.AreEqual(
            0,
            Generate(CrossRouteTestData.OpenVinoSnapshot(narrow)).Candidates.Count);
    }

    [TestMethod]
    public void IdenticalConfigurationsFromTwoEntriesAreOfferedOnce()
    {
        // the same setup reached through two admitted records is one choice,
        // not two competing for the same band
        CrossRouteGenerationResult result = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-a", OpenVinoWeightFormat.Int8),
                CrossRouteTestData.OpenVino("ov-b", OpenVinoWeightFormat.Int8)));

        Assert.AreEqual(1, result.Candidates.Count);
    }

    [TestMethod]
    public void IdenticalConfigurationsChooseReleasedEvidenceRegardlessOfEntryOrder()
    {
        OpenVinoAdmittedConfiguration released = CrossRouteTestData.OpenVino(
            "released", OpenVinoWeightFormat.Int8);
        OpenVinoAdmittedConfiguration experimental = CrossRouteTestData.OpenVino(
            "experimental", OpenVinoWeightFormat.Int8, SupportLevel.Experimental);

        OptimizationCandidate Forward() => Generate(
            CrossRouteTestData.OpenVinoSnapshot(experimental, released),
            optedIn: "experimental").Candidates.Single();

        OptimizationCandidate Reversed() => Generate(
            CrossRouteTestData.OpenVinoSnapshot(released, experimental),
            optedIn: "experimental").Candidates.Single();

        Assert.AreEqual("released", Forward().EvidenceId);
        Assert.AreEqual(Forward().EvidenceId, Reversed().EvidenceId);
        Assert.IsFalse(Forward().IsExperimental);
    }

    [TestMethod]
    public void EveryAdmittedCandidateCarriesCompleteMetrics()
    {
        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8),
                CrossRouteTestData.OpenVino("ov-int4", OpenVinoWeightFormat.Int4))).Candidates)
        {
            OptimizationCandidateMetrics metrics = candidate.Metrics;

            Assert.AreNotEqual(EvidenceGrade.Unknown, metrics.Evidence);
            Assert.AreNotEqual(OptimizationAssessment.Unknown, metrics.Quality);
            Assert.AreNotEqual(OptimizationAssessment.Unknown, metrics.Performance);
            Assert.AreNotEqual(OptimizationAssessment.Unknown, metrics.Stability);
            Assert.IsTrue(metrics.PredictedPeakBytes > 0);
            Assert.IsTrue(metrics.FitsSafely);
        }
    }

    [TestMethod]
    public void ExactCatalogQualityRetainsItsMeasuredEvidenceGrade()
    {
        // Every figure comes from documented defaults. Grading one Measured
        // would let it outrank a real measurement later
        foreach (OptimizationCandidate candidate in Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8))).Candidates)
        {
            Assert.AreEqual(EvidenceGrade.Measured, candidate.Metrics.Evidence);
        }
    }

    [TestMethod]
    public void RuntimeOnlyCandidateClaimsNoPersistentChange()
    {
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-original", OpenVinoWeightFormat.Original))).Candidates.Single();

        Assert.IsFalse(candidate.Metrics.RequiresPersistentChange);
        Assert.AreEqual(0UL, candidate.Metrics.OutputDiskBytes);
    }

    [TestMethod]
    public void ConvertingCandidateDeclaresItsPersistentChange()
    {
        OptimizationCandidate candidate = Generate(
            CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino(
                    "ov-int4", OpenVinoWeightFormat.Int4))).Candidates.Single();

        Assert.IsTrue(candidate.Metrics.RequiresPersistentChange);
        Assert.IsTrue(candidate.Metrics.OutputDiskBytes > 0);
    }

    [TestMethod]
    public void GenerationIsDeterministic()
    {
        // two identical runs must order identically, or a plan issued twice
        // from the same inputs would bind two different configurations
        string[] First() =>
            [.. Generate(CrossRouteTestData.OpenVinoSnapshot(
                CrossRouteTestData.OpenVino("ov-int8", OpenVinoWeightFormat.Int8),
                CrossRouteTestData.OpenVino("ov-int4", OpenVinoWeightFormat.Int4),
                CrossRouteTestData.OpenVino("ov-fp16", OpenVinoWeightFormat.Fp16)))
                .Candidates.Select(candidate => candidate.CanonicalDescriptor)];

        CollectionAssert.AreEqual(First(), First());
    }
}
