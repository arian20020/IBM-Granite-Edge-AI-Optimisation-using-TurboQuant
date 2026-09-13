using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

[TestClass]
public sealed class VerifiedGgufOptimizationEvidenceTests
{
    private const string CurrentTurbo3Id = "GGUF-CURRENT-08EF-CPU-TURBO3-COMPAT-01";

    [TestMethod]
    public void CurrentTurbo3CompatibilityRejectsForeignBackendAndConfiguration()
    {
        var catalog = new OptimizationEvidenceCatalog(VerifiedGgufOptimizationEvidence.Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes, VerifiedGgufOptimizationEvidence.ParameterCount,
            CurrentManifest, RuntimeBuild, RuntimeCommit));
        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolve(catalog, SourceSha,
            VerifiedGgufOptimizationEvidence.ParameterCount, "q4_k_m", "q4_k_m", "turbo3",
            OptimizationEvidenceBackend.Vulkan, OptimizationEvidenceDeviceClass.IntelIntegratedGpu, 4096, "cpu", out _));
        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolve(catalog, SourceSha,
            VerifiedGgufOptimizationEvidence.ParameterCount, "q4_k_m", "q3_k_m", "turbo3",
            OptimizationEvidenceBackend.Cpu, OptimizationEvidenceDeviceClass.Cpu, 4096, "cpu", out _));
        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolve(catalog, SourceSha,
            VerifiedGgufOptimizationEvidence.ParameterCount, "q4_k_m", "q4_k_m", "turbo3",
            OptimizationEvidenceBackend.Cpu, OptimizationEvidenceDeviceClass.Cpu, 8192, "cpu", out _));
    }

    [TestMethod]
    [DataRow("exact")]
    [DataRow("model")]
    [DataRow("runtime")]
    [DataRow("context")]
    [DataRow("threads")]
    [DataRow("profile")]
    public void CurrentTurbo3CompatibilityIsReleasedOnlyForItsExactVerifiedClosure(string variation)
        => AssertCurrentTurboRequiresExactEvidenceConsentAndProfile(
            variation, CurrentTurbo3Id, GgufCacheType.Turbo3, released: true);

    private const string CurrentTurbo4Id = "GGUF-CURRENT-08EF-CPU-TURBO4-01";
    private const string CurrentManifest = "08EF00CF8CD425BC5409071A77C12BE5A90292C121A5109EDDE63BB109B3B7C4";
    private const string PackagedCurrentManifest = "4C1EFEC8D10C2F4B2B477C9136F7E5284737C4E61B3BA6CE41B84D56A296201B";
    private const string CurrentApplicationManifest = "D53299AB05D5B31DB28BA4C233FC30798EECE1BA83EAA11CDED1B5727FFEADC1";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CurrentBf16Q3GeneratorRejectsAbsentOrUnapprovedQuantizer(bool present)
    {
        const string id = "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01";
        const ulong gib = 1024UL * 1024 * 1024;
        var ids = new HashSet<string> { id };
        var binding = OptimizationJourneyBinding.Create("mi-bf16", "handoff-bf16", Bf16SourceSha,
            Bf16SourceLength, "hw-bf16", new string('c', 64));
        var runtime = GgufRuntimeAuthority.Create(RuntimeBuild, RuntimeCommit,
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(ids));
        var snapshot = OptimizationCapabilitySnapshot.ForGguf("current-bf16", new string('c', 64),
            GgufCapabilityPayload.Create(RuntimeBuild, VerifiedGgufOptimizationEvidence.Admissions(ids), true,
                conversionSource: GgufConversionSourceBinding.Create(WeightQuantisation.BF16, binding),
                admittedQuantiser: present ? GgufQuantiserIdentity.Create("unapproved", "different", new string('a', 64)) : null,
                runtimeAuthority: runtime));
        var generated = CrossRouteCandidateGenerator.Generate(snapshot,
            InspectedModelFacts.Create(ByteCount.FromBytes(Bf16SourceLength), 40, 2560, 40, 8, 131072, 32, 2,
                VerifiedGgufOptimizationEvidence.ParameterCount),
            OptimizationWorkload.Create("local-chat", 4096, OptimizationAssessment.Acceptable,
                [ContextTokenCount.FromTokens(4096)]), binding, ByteCount.FromBytes(32 * gib),
            ByteCount.FromBytes(500 * gib), EstimatorPolicy.ProvisionalV1(), new HashSet<string>(),
            OptimizationHardwareAuthorityTestData.AllEstablished(),
            new OptimizationEvidenceCatalog(VerifiedGgufOptimizationEvidence.Records(Bf16SourceSha, Bf16SourceLength,
                VerifiedGgufOptimizationEvidence.ParameterCount, CurrentManifest, RuntimeBuild, RuntimeCommit)),
            VerifiedGgufOptimizationEvidence.ParameterCount);
        Assert.HasCount(0, generated.Candidates);
        Assert.IsTrue(generated.Exclusions.Any(item => item.Reason == OptimizationExclusionReason.RequantisationNotAuthorized));
    }

    [TestMethod]
    [DataRow("source")]
    [DataRow("length")]
    [DataRow("count")]
    [DataRow("manifest")]
    [DataRow("build")]
    [DataRow("commit")]
    public void CurrentBf16Q3CompatibilityRejectsChangedClosureFacts(string variation)
    {
        Assert.HasCount(0, VerifiedGgufOptimizationEvidence.Records(
            variation == "source" ? new string('a', 64) : Bf16SourceSha,
            variation == "length" ? Bf16SourceLength + 1 : Bf16SourceLength,
            variation == "count" ? VerifiedGgufOptimizationEvidence.ParameterCount + 1 : VerifiedGgufOptimizationEvidence.ParameterCount,
            variation == "manifest" ? new string('a', 64) : CurrentManifest,
            variation == "build" ? "changed-build" : RuntimeBuild,
            variation == "commit" ? new string('a', 40) : RuntimeCommit));
    }

    [TestMethod]
    [DataRow("exact")]
    [DataRow("model")]
    [DataRow("runtime")]
    [DataRow("context")]
    [DataRow("threads")]
    [DataRow("profile")]
    public void CurrentTurbo4ReleasedClosureRequiresExactEvidenceAndProfile(string variation)
        => AssertCurrentTurboRequiresExactEvidenceConsentAndProfile(
            variation, CurrentTurbo4Id, GgufCacheType.Turbo4, released: true);

    private static void AssertCurrentTurboRequiresExactEvidenceConsentAndProfile(
        string variation, string evidenceId, GgufCacheType cache, bool released)
    {
        const ulong gib = 1024UL * 1024 * 1024;
        string model = variation == "model" ? new string('a', 64) : SourceSha;
        var records = VerifiedGgufOptimizationEvidence.Records(model,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes, VerifiedGgufOptimizationEvidence.ParameterCount,
            variation == "runtime" ? new string('b', 64) : CurrentManifest, RuntimeBuild, RuntimeCommit);
        var ids = new HashSet<string> { evidenceId };
        var admissions = VerifiedGgufOptimizationEvidence.Admissions(ids);
        Assert.HasCount(1, admissions);
        Assert.AreEqual(released ? SupportLevel.DeclaredSupported : SupportLevel.Experimental,
            admissions[0].Level);
        Assert.AreEqual(!released, admissions[0].RequiresEvidence);
        var exactProfile = VerifiedGgufOptimizationEvidence.ExecutionProfiles(ids).Single();
        var profile = GgufExecutionProfileAuthority.Create(evidenceId, exactProfile.Evidence,
            variation == "profile" ? "other" : exactProfile.ProfileId, exactProfile.FlashAttention,
            variation == "threads" ? 8 : exactProfile.ThreadCount, exactProfile.BatchSize, exactProfile.MaximumGeneratedTokens);
        var runtime = GgufRuntimeAuthority.Create(RuntimeBuild, RuntimeCommit, [profile]);
        var snapshot = OptimizationCapabilitySnapshot.ForGguf("current-turbo4", new string('c', 64),
            GgufCapabilityPayload.Create(RuntimeBuild, admissions, false,
                turboQuantImplementation: GgufTurboQuantImplementationIdentity.Create(RuntimeBuild, RuntimeCommit,
                    CompatibilityBackend.Cpu, DeviceRouteId.Cpu), runtimeAuthority: runtime));
        var workload = OptimizationWorkload.Create("local-chat", 4096, OptimizationAssessment.Acceptable,
            [ContextTokenCount.FromTokens(variation == "context" ? 8192 : 4096)]);
        var binding = OptimizationJourneyBinding.Create("mi-current", "handoff-current", model,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes, "hw-current", new string('c', 64));
        var consent = variation == "absent-consent" ? new HashSet<string>()
            : new HashSet<string> { variation == "wrong-consent" ? "GGUF-V4-CPU-TURBO4-01" : evidenceId };
        var generated = CrossRouteCandidateGenerator.Generate(snapshot,
            InspectedModelFacts.Create(ByteCount.FromBytes(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes),
                40, 2560, 40, 8, 131072, 15, 2, VerifiedGgufOptimizationEvidence.ParameterCount),
            workload, binding, ByteCount.FromBytes(32 * gib), ByteCount.FromBytes(500 * gib),
            EstimatorPolicy.ProvisionalV1(), consent, OptimizationHardwareAuthorityTestData.AllEstablished(),
            new OptimizationEvidenceCatalog(records), VerifiedGgufOptimizationEvidence.ParameterCount);
        if (variation != "exact")
        {
            Assert.HasCount(0, generated.Candidates, variation);
            return;
        }
        var candidate = generated.Candidates.Single();
        Assert.AreEqual(evidenceId, candidate.EvidenceId);
        Assert.AreEqual(!released, candidate.IsExperimental);
        Assert.AreEqual(!released, candidate.AdmissionProof!.RequiresEvidence);
        Assert.IsFalse(candidate.Metrics.RequiresPersistentChange);
        var session = CompatibilityPlanningSession.Create(OptimizationRoute.Gguf, generated.Candidates,
            snapshot, workload, binding, 40, consent)!;
        var issuance = OptimizationIssuanceAuthority.FromGeneration(OptimizationHardwareAuthorityTestData.AllEstablished(),
            ByteCount.FromBytes(32 * gib), ByteCount.FromBytes(500 * gib));
        var preference = OptimizationPreferenceSelection.Exact(OptimizationPreferenceResolver.CandidateIdentity(candidate));
        var plan = session.Issue(preference, new CurrentTurbo4Composer(runtime, cache), issuance, new CurrentFixtureTime());
        Assert.AreEqual(evidenceId, plan.Candidate.EvidenceId);
        Assert.AreEqual(!released, plan.Candidate.IsExperimental);
        Assert.IsFalse(plan.ProducesPersistentArtifact);
        Assert.AreEqual(cache, plan.ExecutionPayload.Gguf!.KeyCacheType);
        Assert.AreEqual(cache, plan.ExecutionPayload.Gguf.ValueCacheType);
        Assert.AreEqual(4, plan.ExecutionPayload.Gguf.ThreadCount);
        Assert.AreEqual(4096, plan.ExecutionPayload.Gguf.ContextSize);
    }

    private sealed class CurrentTurbo4Composer(GgufRuntimeAuthority runtime, GgufCacheType cache) : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;
        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            var profile = runtime.Profiles[candidate.EvidenceId];
            return OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(runtime.RuntimeBuildId,
                runtime.RuntimeSourceCommit, GgufRuntimeBackend.Cpu, "CPU", candidate.Metrics.ContextTokens,
                cache, cache, 0, profile.FlashAttention, profile.ThreadCount,
                profile.BatchSize, "Measured", profile.ProfileId, profile.MaximumGeneratedTokens,
                GgufWeightFormat.Imported, null));
        }
    }

    private sealed class CurrentFixtureTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    [TestMethod]
    [DataRow(CurrentManifest)]
    [DataRow(PackagedCurrentManifest)]
    [DataRow(CurrentApplicationManifest)]
    public void CurrentCandidateAdmitsFreshStandardAndDistinctFourThreadTurbo4ExactClosure(string manifest)
    {
        var records = VerifiedGgufOptimizationEvidence.Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes, VerifiedGgufOptimizationEvidence.ParameterCount,
            manifest, RuntimeBuild, RuntimeCommit);
        Assert.HasCount(4, records);
        CollectionAssert.AreEquivalent(new[] { "GGUF-CURRENT-08EF-CPU-F16-01", "GGUF-CURRENT-08EF-CPU-Q8-01", "GGUF-CURRENT-08EF-CPU-TURBO4-01", CurrentTurbo3Id },
            records.Select(row => row.EvidenceId).ToArray());
        Assert.AreEqual(6.316666666666666m, records.Single(row => row.Key.CacheConfiguration == "f16").Quality.Value);
        Assert.AreEqual(5.783333333333334m, records.Single(row => row.Key.CacheConfiguration == "q8_0").Quality.Value);
        Assert.AreEqual(5.3166666666666664m, records.Single(row => row.Key.CacheConfiguration == "turbo4").Quality.Value);
        Assert.AreEqual(5.683333333333333m, records.Single(row => row.Key.CacheConfiguration == "turbo3").Quality.Value);
        var ids = records.Select(row => row.EvidenceId).ToHashSet();
        Assert.HasCount(4, VerifiedGgufOptimizationEvidence.Admissions(ids));
        var profiles = VerifiedGgufOptimizationEvidence.ExecutionProfiles(ids);
        Assert.HasCount(4, profiles);
        Assert.IsTrue(profiles.All(profile => profile.ThreadCount == 4 && profile.BatchSize == 512
            && profile.MaximumGeneratedTokens == 256 && profile.FlashAttention && profile.ProfileId == "cpu"));
        foreach (var record in records)
        {
            Assert.AreEqual("atomicbot-cpu-current-08ef00cf8cd425bc", record.Key.RuntimePackageIdentity);
            Assert.IsTrue(VerifiedGgufOptimizationEvidence.TryResolve(new OptimizationEvidenceCatalog(records),
                SourceSha, VerifiedGgufOptimizationEvidence.ParameterCount, "q4_k_m", "q4_k_m", record.Key.CacheConfiguration,
                OptimizationEvidenceBackend.Cpu, OptimizationEvidenceDeviceClass.Cpu, 4096, "cpu", out _));
        }
        Assert.IsTrue(VerifiedGgufOptimizationEvidence.TryResolve(new OptimizationEvidenceCatalog(records),
            SourceSha, VerifiedGgufOptimizationEvidence.ParameterCount, "q4_k_m", "q4_k_m", "turbo4",
            OptimizationEvidenceBackend.Cpu, OptimizationEvidenceDeviceClass.Cpu, 4096, "cpu", out _));
        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolve(new OptimizationEvidenceCatalog(records),
            SourceSha, VerifiedGgufOptimizationEvidence.ParameterCount, "q4_k_m", "q4_k_m", "q8_0",
            OptimizationEvidenceBackend.Cpu, OptimizationEvidenceDeviceClass.Cpu, 8192, "cpu", out _));
        Assert.HasCount(0, VerifiedGgufOptimizationEvidence.Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes, VerifiedGgufOptimizationEvidence.ParameterCount,
            new string('a', 64), RuntimeBuild, RuntimeCommit));
        IReadOnlyList<OptimizationEvidenceRecord> bf16Records = VerifiedGgufOptimizationEvidence.Records(Bf16SourceSha,
            Bf16SourceLength, VerifiedGgufOptimizationEvidence.ParameterCount, manifest, RuntimeBuild, RuntimeCommit);
        Assert.HasCount(1, bf16Records);
        Assert.AreEqual("GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01", bf16Records[0].EvidenceId);
    }
    private const string SourceSha =
        "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
    private const string ManifestV4 =
        "93FA840C3DB0B623DDA3DAEBE27FE51564DCD292A420A9CF5A9A96D990E19DAA";
    private const string ManifestV5 =
        "5E2204C791A44D2FC696F0F37EBB5568DDD40D7BD6BDE869D4844E029D326941";
    private const string RuntimeBuild =
        "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan";
    private const string RuntimeCommit =
        "519f0c594a8e31467d2e2f2cf17054c9e7e11536";
    private const string Bf16SourceSha =
        "e5fc3d677f42a9cba091ea6084cf619bd434ff5ac56b893d3bf5d4f604581091";
    private const ulong Bf16SourceLength = 6_809_655_904;

    [TestMethod]
    public void ExactSourceAndV4ClosurePublishFourCpuRows()
    {
        OptimizationEvidenceRecord[] records =
        [
            .. VerifiedGgufOptimizationEvidence.Records(
                SourceSha,
                VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                ManifestV4,
                RuntimeBuild,
                RuntimeCommit)
        ];

        Assert.HasCount(4, records);
        Assert.HasCount(4, records.Select(static row => row.Key).Distinct());
        Assert.IsTrue(records.All(static row => row.IsAdmitted));
        Assert.IsTrue(records.All(static row =>
            row.Key.Backend == OptimizationEvidenceBackend.Cpu
            && row.Key.DeviceClass == OptimizationEvidenceDeviceClass.Cpu
            && row.Key.ContextTokens == 4096
            && row.Key.ExecutionProfile == "cpu"));
        Assert.AreEqual(6.466666666666667m,
            records.Single(static row => row.Key.CacheConfiguration == "f16").Quality.Value);
        Assert.AreEqual(5.85m,
            records.Single(static row => row.Key.CacheConfiguration == "q8_0").Quality.Value);
        Assert.AreEqual(6.15m,
            records.Single(static row => row.Key.CacheConfiguration == "turbo4").Quality.Value);
        Assert.AreEqual(5.683333333333333m,
            records.Single(static row => row.Key.CacheConfiguration == "turbo3").Quality.Value);
    }

    [TestMethod]
    public void ExactSourceAndReleaseV5ClosurePublishesStandardAndThreads8Turbo4Rows()
    {
        OptimizationEvidenceRecord[] records =
        [
            .. VerifiedGgufOptimizationEvidence.Records(
                SourceSha,
                VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                ManifestV5,
                RuntimeBuild,
                RuntimeCommit)
        ];

        Assert.HasCount(3, records);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "GGUF-V5-CPU-F16-01",
                "GGUF-V5-CPU-Q8-01",
                "GGUF-V5-CPU-TURBO4-T8-01",
            },
            records.Select(static row => row.EvidenceId).ToArray());
        Assert.IsTrue(records.All(static row => row.IsAdmitted));
        Assert.IsTrue(records.All(static row =>
            row.Key.RuntimePackageIdentity == "atomicbot-cpu-v5-5e2204c791a44d2f"
            && row.Key.Backend == OptimizationEvidenceBackend.Cpu
            && row.Key.DeviceClass == OptimizationEvidenceDeviceClass.Cpu
            && row.Key.ContextTokens == 4096
            && row.Key.ExecutionProfile == "cpu"));
        Assert.AreEqual(6.466666666666667m,
            records.Single(static row => row.Key.CacheConfiguration == "f16").Quality.Value);
        Assert.AreEqual(5.85m,
            records.Single(static row => row.Key.CacheConfiguration == "q8_0").Quality.Value);
        OptimizationEvidenceRecord turbo4 = records.Single(static row =>
            row.EvidenceId == "GGUF-V5-CPU-TURBO4-T8-01");
        Assert.AreEqual("turbo4", turbo4.Key.CacheConfiguration);
        Assert.AreEqual(6.15m, turbo4.Quality.Value);
        Assert.AreEqual("granite-gguf-app-4k-runtime-v5-threads8-600s-v1",
            turbo4.Key.MethodologyIdentity);
        Assert.IsFalse(records.Any(static row =>
            row.Key.CacheConfiguration == "turbo3"));
    }

    [TestMethod]
    public void Threads8Turbo4AdmissionIsReleasedAndPinsOnlyEightThreadProfile()
    {
        IReadOnlySet<string> ids = new HashSet<string>
        {
            "GGUF-V5-CPU-TURBO4-T8-01",
        };
        GgufAdmittedConfiguration admission =
            VerifiedGgufOptimizationEvidence.Admissions(ids).Single();
        GgufExecutionProfileAuthority profile =
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(ids).Single();

        Assert.AreEqual(GgufWeightFormat.Imported, admission.Weights);
        Assert.AreEqual(GgufKvCacheFormat.TurboQuant4Bit, admission.KvCache);
        Assert.AreEqual(SupportLevel.DeclaredSupported, admission.Level);
        Assert.IsFalse(admission.RequiresEvidence);
        Assert.AreEqual(EvidenceGrade.Measured, profile.Evidence);
        Assert.AreEqual("cpu", profile.ProfileId);
        Assert.IsTrue(profile.FlashAttention);
        Assert.AreEqual(8, profile.ThreadCount);
        Assert.AreEqual(512, profile.BatchSize);
        Assert.AreEqual(256, profile.MaximumGeneratedTokens);
    }

    [TestMethod]
    public void Threads8Turbo4ResolutionRejectsEveryEvidenceKeyNearMiss()
    {
        OptimizationEvidenceRecord exact = VerifiedGgufOptimizationEvidence.Records(
            SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            ManifestV5,
            RuntimeBuild,
            RuntimeCommit).Single(static row =>
                row.EvidenceId == "GGUF-V5-CPU-TURBO4-T8-01");
        OptimizationEvidenceRecord[] nearMisses =
        [
            exact with { Key = exact.Key with { ModelIdentitySha256 = new string('a', 64) } },
            exact with { Key = exact.Key with { ParameterCount = exact.Key.ParameterCount - 1 } },
            exact with { Key = exact.Key with { RuntimePackageIdentity = "different-package" } },
            exact with { Key = exact.Key with { SourceWeightRepresentation = "f16" } },
            exact with { Key = exact.Key with { TargetWeightRepresentation = "q8_0" } },
            exact with { Key = exact.Key with { CacheConfiguration = "turbo3" } },
            exact with { Key = exact.Key with { Backend = OptimizationEvidenceBackend.Vulkan } },
            exact with { Key = exact.Key with { DeviceClass = OptimizationEvidenceDeviceClass.IntelIntegratedGpu } },
            exact with { Key = exact.Key with { ContextTokens = 4095 } },
            exact with { Key = exact.Key with { Workload = "different-workload" } },
            exact with { Key = exact.Key with { MethodologyIdentity = "different-methodology" } },
            exact with { Key = exact.Key with { MemoryPerformanceProtocol = "different-protocol" } },
            exact with { Key = exact.Key with { ExecutionProfile = "different-profile" } },
        ];

        foreach (OptimizationEvidenceRecord nearMiss in nearMisses)
        {
            Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolve(
                new OptimizationEvidenceCatalog([nearMiss]),
                SourceSha,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                "q4_k_m",
                "q4_k_m",
                "turbo4",
                OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu,
                4096,
                "cpu",
                out _), nearMiss.ToString());
        }
    }

    [TestMethod]
    public void Threads8Turbo4ProfileRejectsEveryAuthorityFieldNearMiss()
    {
        const string evidenceId = "GGUF-V5-CPU-TURBO4-T8-01";
        GgufExecutionProfileAuthority exact =
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(
                new HashSet<string> { evidenceId }).Single();
        GgufExecutionProfileAuthority[] nearMisses =
        [
            GgufExecutionProfileAuthority.Create(
                "GGUF-V5-CPU-TURBO4-T8-MUTATED", EvidenceGrade.Measured,
                "cpu", true, 8, 512, 256),
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Estimated,
                "cpu", true, 8, 512, 256),
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Measured,
                "different-profile", true, 8, 512, 256),
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Measured,
                "cpu", false, 8, 512, 256),
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Measured,
                "cpu", true, 4, 512, 256),
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Measured,
                "cpu", true, 8, 511, 256),
            GgufExecutionProfileAuthority.Create(
                evidenceId, EvidenceGrade.Measured,
                "cpu", true, 8, 512, 255),
        ];

        Assert.IsTrue(VerifiedGgufOptimizationEvidence.MatchesExecutionProfile(exact));
        Assert.IsTrue(nearMisses.All(static profile =>
            !VerifiedGgufOptimizationEvidence.MatchesExecutionProfile(profile)));
    }

    [TestMethod]
    public void ExactBf16SourceAndReleaseV5ClosurePublishesOnlyMeasuredQ3Conversion()
    {
        OptimizationEvidenceRecord[] records =
        [
            .. VerifiedGgufOptimizationEvidence.Records(
                Bf16SourceSha,
                Bf16SourceLength,
                VerifiedGgufOptimizationEvidence.ParameterCount,
                ManifestV5,
                RuntimeBuild,
                RuntimeCommit)
        ];

        Assert.HasCount(1, records);
        OptimizationEvidenceRecord row = records[0];
        Assert.AreEqual("GGUF-V5-BF16-Q3-CPU-F16-01", row.EvidenceId);
        Assert.AreEqual("bf16", row.Key.SourceWeightRepresentation);
        Assert.AreEqual("q3_k_m", row.Key.TargetWeightRepresentation);
        Assert.AreEqual("f16", row.Key.CacheConfiguration);
        Assert.AreEqual(6.9m, row.Quality.Value);
        Assert.IsTrue(row.IsAdmitted);
    }

    [TestMethod]
    public void ExactBf16SourceCurrentClosureRetainsQ3CompatibilityEvidence()
    {
        var records = VerifiedGgufOptimizationEvidence.Records(Bf16SourceSha, Bf16SourceLength,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            "08EF00CF8CD425BC5409071A77C12BE5A90292C121A5109EDDE63BB109B3B7C4", RuntimeBuild, RuntimeCommit);
        Assert.HasCount(1, records);
        Assert.AreEqual("GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01", records[0].EvidenceId);
        Assert.AreEqual("bf16", records[0].Key.SourceWeightRepresentation);
        Assert.AreEqual("q3_k_m", records[0].Key.TargetWeightRepresentation);
        Assert.AreEqual("f16", records[0].Key.CacheConfiguration);
        Assert.AreEqual(6.9m, records[0].Quality.Value);
    }

    [TestMethod]
    public void MissingBf16ParameterMetadataFallsBackOnlyForExactHeader()
    {
        Assert.IsTrue(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            Bf16SourceSha,
            Bf16SourceLength,
            40, 2560, 40, 8, 131072, 32, 2,
            out ulong parameterCount));
        Assert.AreEqual(VerifiedGgufOptimizationEvidence.ParameterCount, parameterCount);

        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            Bf16SourceSha,
            Bf16SourceLength,
            40, 2560, 40, 8, 131072, 31, 2,
            out _));
    }

    [TestMethod]
    public void Bf16Q3AdmissionAndProfilePinTheExactReleasedCpuConfiguration()
    {
        IReadOnlySet<string> ids = new HashSet<string>
        {
            "GGUF-V5-BF16-Q3-CPU-F16-01",
        };
        GgufAdmittedConfiguration admission =
            VerifiedGgufOptimizationEvidence.Admissions(ids).Single();
        GgufExecutionProfileAuthority profile =
            VerifiedGgufOptimizationEvidence.ExecutionProfiles(ids).Single();

        Assert.AreEqual(GgufWeightFormat.Q3KM, admission.Weights);
        Assert.AreEqual(GgufKvCacheFormat.F16, admission.KvCache);
        Assert.AreEqual(CompatibilityBackend.Cpu, admission.Backend);
        Assert.AreEqual(DeviceRouteId.Cpu, admission.Device);
        Assert.AreEqual(GpuOffloadLevel.None, admission.Offload);
        Assert.AreEqual(4096, admission.MinimumContextTokens);
        Assert.AreEqual(4096, admission.MaximumContextTokens);
        Assert.AreEqual(SupportLevel.DeclaredSupported, admission.Level);
        Assert.IsFalse(admission.RequiresEvidence);
        Assert.AreEqual(EvidenceGrade.Measured, profile.Evidence);
        Assert.AreEqual("cpu", profile.ProfileId);
        Assert.IsTrue(profile.FlashAttention);
        Assert.AreEqual(4, profile.ThreadCount);
        Assert.AreEqual(512, profile.BatchSize);
        Assert.AreEqual(256, profile.MaximumGeneratedTokens);
    }

    [TestMethod]
    [DataRow("sha")]
    [DataRow("length")]
    [DataRow("layers")]
    [DataRow("embedding")]
    [DataRow("attention")]
    [DataRow("kv")]
    [DataRow("context")]
    [DataRow("fileType")]
    [DataRow("quantVersion")]
    public void AnyBf16HeaderMutationRejectsParameterFallback(string mutation)
    {
        ulong length = mutation == "length" ? Bf16SourceLength + 1 : Bf16SourceLength;
        int layers = mutation == "layers" ? 39 : 40;
        int embedding = mutation == "embedding" ? 2559 : 2560;
        int attention = mutation == "attention" ? 39 : 40;
        int kv = mutation == "kv" ? 7 : 8;
        int context = mutation == "context" ? 131071 : 131072;
        int fileType = mutation == "fileType" ? 31 : 32;
        int quantVersion = mutation == "quantVersion" ? 1 : 2;

        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            mutation == "sha" ? new string('a', 64) : Bf16SourceSha,
            length, layers, embedding, attention, kv, context,
            fileType, quantVersion, out _));
    }

    [TestMethod]
    [DataRow("sha")]
    [DataRow("length")]
    [DataRow("parameters")]
    [DataRow("manifest")]
    [DataRow("build")]
    [DataRow("commit")]
    public void AnyBf16ClosureMutationPublishesNoRows(string mutation)
    {
        Assert.IsEmpty(VerifiedGgufOptimizationEvidence.Records(
            mutation == "sha" ? new string('a', 64) : Bf16SourceSha,
            mutation == "length" ? Bf16SourceLength + 1 : Bf16SourceLength,
            mutation == "parameters"
                ? VerifiedGgufOptimizationEvidence.ParameterCount - 1
                : VerifiedGgufOptimizationEvidence.ParameterCount,
            mutation == "manifest" ? new string('b', 64) : ManifestV5,
            mutation == "build" ? "different-build" : RuntimeBuild,
            mutation == "commit" ? new string('c', 40) : RuntimeCommit));
    }

    [TestMethod]
    public void MissingParameterMetadataFallsBackOnlyForExactInspectedSource()
    {
        Assert.IsTrue(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            layerCount: 40,
            embeddingSize: 2560,
            attentionHeadCount: 40,
            keyValueHeadCount: 8,
            declaredContextLimit: 131072,
            fileType: 15,
            quantisationVersion: 2,
            out ulong parameterCount));
        Assert.AreEqual(VerifiedGgufOptimizationEvidence.ParameterCount, parameterCount);

        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2560, 40, 8, 131072, 14, 2, out _));
        Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
            new string('a', 64),
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2560, 40, 8, 131072, 15, 2, out _));

        static void AssertRejected(
            ulong length,
            int layers,
            int embedding,
            int attentionHeads,
            int keyValueHeads,
            int context,
            int fileType,
            int quantisationVersion) =>
            Assert.IsFalse(VerifiedGgufOptimizationEvidence.TryResolveParameterCount(
                SourceSha,
                length,
                layers,
                embedding,
                attentionHeads,
                keyValueHeads,
                context,
                fileType,
                quantisationVersion,
                out _));

        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes + 1,
            40, 2560, 40, 8, 131072, 15, 2);
        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            39, 2560, 40, 8, 131072, 15, 2);
        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2559, 40, 8, 131072, 15, 2);
        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2560, 39, 8, 131072, 15, 2);
        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2560, 40, 7, 131072, 15, 2);
        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2560, 40, 8, 131071, 15, 2);
        AssertRejected(VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            40, 2560, 40, 8, 131072, 15, 1);
    }

    [TestMethod]
    public void AnySourceOrRuntimeMutationPublishesNoRows()
    {
        static IReadOnlyList<OptimizationEvidenceRecord> Records(
            string sourceSha,
            ulong length,
            ulong parameters,
            string manifest,
            string build,
            string commit) => VerifiedGgufOptimizationEvidence.Records(
                sourceSha, length, parameters, manifest, build, commit);

        Assert.IsEmpty(Records(new string('a', 64),
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            ManifestV4,
            RuntimeBuild,
            RuntimeCommit));
        Assert.IsEmpty(Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes + 1,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            ManifestV4,
            RuntimeBuild,
            RuntimeCommit));
        Assert.IsEmpty(Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            3_000_000_000,
            ManifestV4,
            RuntimeBuild,
            RuntimeCommit));
        Assert.IsEmpty(Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            new string('b', 64),
            RuntimeBuild,
            RuntimeCommit));
        Assert.IsEmpty(Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            ManifestV4,
            "different-build",
            RuntimeCommit));
        Assert.IsEmpty(Records(SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            ManifestV4,
            RuntimeBuild,
            new string('a', 40)));
    }

    [TestMethod]
    public void AdmissionsAndProfilesPinMeasuredCpuConfiguration()
    {
        GgufAdmittedConfiguration[] admissions =
        [.. VerifiedGgufOptimizationEvidence.Admissions()];
        GgufExecutionProfileAuthority[] profiles =
        [.. VerifiedGgufOptimizationEvidence.ExecutionProfiles()];

        Assert.HasCount(4, admissions);
        Assert.HasCount(4, profiles);
        Assert.IsTrue(admissions.All(static item =>
            item.Weights == GgufWeightFormat.Imported
            && item.Backend == CompatibilityBackend.Cpu
            && item.Device == DeviceRouteId.Cpu
            && item.Offload == GpuOffloadLevel.None
            && item.MinimumContextTokens == 4096
            && item.MaximumContextTokens == 4096));
        Assert.IsTrue(admissions.Where(static item => item.KvCache is
                GgufKvCacheFormat.TurboQuant4Bit or GgufKvCacheFormat.TurboQuant3Bit)
            .All(static item => item.Level == SupportLevel.Experimental && item.RequiresEvidence));
        Assert.IsTrue(admissions.Where(static item => item.KvCache is
                GgufKvCacheFormat.F16 or GgufKvCacheFormat.Q8_0)
            .All(static item => item.Level == SupportLevel.DeclaredSupported && !item.RequiresEvidence));
        Assert.IsTrue(profiles.All(static item =>
            item.Evidence == EvidenceGrade.Measured
            && item.ProfileId == "cpu"
            && item.FlashAttention
            && item.ThreadCount == 4
            && item.BatchSize == 512
            && item.MaximumGeneratedTokens == 256));
    }

    [TestMethod]
    public void AnyFailedEvidenceGateIsNotAdmitted()
    {
        OptimizationEvidenceRecord row = VerifiedGgufOptimizationEvidence.Records(
            SourceSha,
            VerifiedGgufOptimizationEvidence.SourceModelLengthBytes,
            VerifiedGgufOptimizationEvidence.ParameterCount,
            ManifestV4,
            RuntimeBuild,
            RuntimeCommit).Single(static item =>
                item.Key.CacheConfiguration == "turbo4");

        Assert.IsFalse((row with { OutputHealthPassed = false }).IsAdmitted);
        Assert.IsFalse((row with { StabilityPassed = false }).IsAdmitted);
        Assert.IsFalse((row with { ActivationPassed = false }).IsAdmitted);
        Assert.IsFalse((row with { IntegrityPassed = false }).IsAdmitted);
        Assert.IsFalse((row with { Quality = new OptimizationQualityScore(3.99m) }).IsAdmitted);
    }
}
