using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

[TestClass]
public sealed class ExecutableEvidenceFrontierTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    private const string Digest = "1111111111111111111111111111111111111111111111111111111111111111";
    private static readonly int[] Positions = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

    [TestMethod]
    [DataRow(3, false)]
    [DataRow(3, true)]
    [DataRow(8, false)]
    public void EverySyntheticBandIssuesTheSelectedPlanWithMonotonicQualityAndMemory(int billions, bool vulkan)
    {
        Fixture fixture = Create(billions, vulkan, synthetic: true);
        Assert.IsTrue(fixture.Generated.Candidates.Count > 0,
            string.Join(";", fixture.Generated.Exclusions.Select(row => row.EvidenceId + ":" + row.Reason)));
        decimal previousQuality = 0;
        ulong previousMemory = 0;
        CompatibilityPlanningSession session = CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf, fixture.Generated.Candidates, fixture.Snapshot,
            fixture.Workload, fixture.Binding, 32, fixture.Consent)!;
        Assert.IsNotNull(session);
        foreach (int position in Positions)
        {
            OptimizationPreferenceSelection preference = OptimizationPreferenceSelection.Manual(position);
            OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(fixture.Generated.Candidates, preference)!;
            OptimizationCandidate selected = selection.Candidate;
            Assert.IsNotNull(selected.Evidence);
            Assert.IsTrue(selected.Evidence.IsAdmitted);
            Assert.IsTrue(selected.QualityScore >= previousQuality);
            Assert.IsTrue(selected.Metrics.PredictedPeakBytes >= previousMemory);
            Assert.IsTrue(selected.Metrics.FitsSafely && selected.Metrics.FitsDiskSafely);
            Assert.IsFalse(selected.Metrics.RequiresPersistentChange);
            Assert.AreEqual(GgufWeightFormat.Imported, ((GgufRouteConfiguration)selected.Configuration).Weights);
            OptimizationExecutionPlan plan = session.Issue(preference, fixture.Composer,
                fixture.Issuance, new FixedTimeProvider());
            Assert.AreSame(selected, plan.Candidate);
            Assert.IsTrue(session.MatchesIssuedPlan(plan, preference));
            Assert.AreEqual(4096, plan.ExecutionPayload.Gguf!.ContextSize);
            previousQuality = selected.QualityScore;
            previousMemory = selected.Metrics.PredictedPeakBytes;
        }
        OptimizationExecutionPlan automatic = session.Issue(OptimizationPreferenceSelection.Automatic(),
            fixture.Composer, fixture.Issuance, new FixedTimeProvider());
        Assert.IsTrue(fixture.Generated.Candidates.Contains(automatic.Candidate));
    }

    [TestMethod]
    public void CpuFallbackIssuesADistinctPlanAndNeverRelabelsVulkanExecution()
    {
        Fixture cpu = Create(3, false, synthetic: true);
        Fixture vulkan = Create(3, true, synthetic: true);
        OptimizationExecutionPlan Issue(Fixture f) => CompatibilityPlanningSession.Create(
            OptimizationRoute.Gguf, f.Generated.Candidates, f.Snapshot, f.Workload,
            f.Binding, 32, f.Consent)!.Issue(OptimizationPreferenceSelection.Automatic(),
                f.Composer, f.Issuance, new FixedTimeProvider());
        OptimizationExecutionPlan cpuPlan = Issue(cpu);
        OptimizationExecutionPlan vulkanPlan = Issue(vulkan);
        Assert.AreEqual(GgufRuntimeBackend.Cpu, cpuPlan.ExecutionPayload.Gguf!.Backend);
        Assert.AreEqual(GgufRuntimeBackend.Vulkan, vulkanPlan.ExecutionPayload.Gguf!.Backend);
        Assert.AreNotEqual(cpuPlan.ConfigurationSha256, vulkanPlan.ConfigurationSha256);
        Assert.AreNotEqual(cpuPlan.OptimizationPlanId, vulkanPlan.OptimizationPlanId);
    }

    [TestMethod]
    public void PartialVulkanProfilesCannotAppearWithoutAnEstablishedResourceEstimate()
    {
        Fixture fixture = Create(8, true, synthetic: true);
        Assert.AreEqual(0, fixture.Generated.Candidates.Count);
        Assert.AreEqual(2, fixture.Generated.Exclusions.Count);
        Assert.IsTrue(fixture.Generated.Exclusions.All(row =>
            row.Reason == OptimizationExclusionReason.EstimateNotEstablished));
        foreach (int position in Positions)
            Assert.IsNull(OptimizationPreferenceResolver.Resolve(fixture.Generated.Candidates,
                OptimizationPreferenceSelection.Manual(position)));
    }

    [TestMethod]
    public void EveryPublishedProfileIsExcludedBeforeAnyBandCanSelectIt()
    {
        foreach (int billions in new[] { 3, 8 })
        foreach (bool vulkan in new[] { false, true })
        {
            Fixture fixture = Create(billions, vulkan);
            Assert.AreEqual(0, fixture.Generated.Candidates.Count);
            Assert.IsTrue(fixture.Generated.Exclusions.Count >= 2);
            Assert.IsTrue(fixture.Generated.Exclusions.All(exclusion =>
                exclusion.Reason == OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
            Assert.IsNull(OptimizationPreferenceResolver.Resolve(fixture.Generated.Candidates,
                OptimizationPreferenceSelection.Automatic()));
            for (int position = 0; position <= 100; position++)
                Assert.IsNull(OptimizationPreferenceResolver.Resolve(fixture.Generated.Candidates,
                    OptimizationPreferenceSelection.Manual(position)));
        }
    }

    [TestMethod]
    public void NoBandCanSelectAnOverBudgetConfiguration()
    {
        Fixture fixture = Create(3, true, safeBudget: 1, synthetic: true);
        Assert.AreEqual(0, fixture.Generated.Candidates.Count);
        Assert.IsTrue(fixture.Generated.Exclusions.All(exclusion =>
            exclusion.Reason is OptimizationExclusionReason.ExceedsSafeMemoryBudget
                or OptimizationExclusionReason.EstimateNotEstablished),
            string.Join(";", fixture.Generated.Exclusions.Select(row => row.EvidenceId + ":" + row.Reason)));
        foreach (int position in Positions)
            Assert.IsNull(OptimizationPreferenceResolver.Resolve(fixture.Generated.Candidates,
                OptimizationPreferenceSelection.Manual(position)));
    }

    private static Fixture Create(int billions, bool vulkan, ulong safeBudget = 32 * GiB, bool synthetic = false)
    {
        OptimizationEvidenceRecord[] records = [.. PublishedGgufOptimizationEvidence.Records().Where(row =>
            row.Key.ParameterCount == (ulong)billions * 1_000_000_000
            && row.Key.Backend == (vulkan ? OptimizationEvidenceBackend.Vulkan : OptimizationEvidenceBackend.Cpu))];
        // These invented IDs/gates test planning mechanics only. No historical
        // AB record (including AB-08Q) is promoted into executable evidence.
        if (synthetic)
            records = [.. records.Select((row, index) => row with
            {
                EvidenceId = "synthetic-frontier-" + index,
                OutputHealthPassed = true, StabilityPassed = true,
                ActivationPassed = true, IntegrityPassed = true
            })];
        CompatibilityBackend backend = vulkan ? CompatibilityBackend.IntelVulkan : CompatibilityBackend.Cpu;
        DeviceRouteId device = vulkan ? DeviceRouteId.IntelIntegratedGpu : DeviceRouteId.Cpu;
        GgufAdmittedConfiguration[] entries = [.. records.Select(row => GgufAdmittedConfiguration.Create(
            row.EvidenceId, backend, device, GgufWeightFormat.Imported,
            row.Key.CacheConfiguration switch
            {
                "f16" => GgufKvCacheFormat.F16,
                "q8_0" => GgufKvCacheFormat.Q8_0,
                "turbo4" => GgufKvCacheFormat.TurboQuant4Bit,
                "turbo3" => GgufKvCacheFormat.TurboQuant3Bit,
                _ => throw new AssertFailedException("Unexpected published cache")
            }, row.Key.ExecutionProfile == "vulkan-full" ? GpuOffloadLevel.Full
                : vulkan ? GpuOffloadLevel.Partial : GpuOffloadLevel.None,
            4096, 4096, SupportLevel.Experimental, true))];
        GgufRuntimeAuthority runtime = GgufRuntimeAuthority.Create(
            PublishedGgufOptimizationEvidence.RuntimeBuildId, PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
            [.. entries.Select(entry => GgufExecutionProfileAuthority.Create(entry.EvidenceId,
                EvidenceGrade.Measured, entry.EvidenceId, false, 4, 128, 256))]);
        OptimizationCapabilitySnapshot snapshot = OptimizationCapabilitySnapshot.ForGguf("exact-frontier", Digest,
            GgufCapabilityPayload.Create(runtime.RuntimeBuildId, entries,
                turboQuantImplementation: GgufTurboQuantImplementationIdentity.Create(runtime.RuntimeBuildId,
                    runtime.RuntimeSourceCommit, backend, device), runtimeAuthority: runtime));
        OptimizationWorkload workload = OptimizationWorkload.Create("local-chat", 4096,
            OptimizationAssessment.Acceptable, [ContextTokenCount.FromTokens(4096)]);
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create("mi-run", "mi-handoff",
            records[0].Key.ModelIdentitySha256, (ulong)billions * GiB, "hw-run", Digest);
        HashSet<string> consent = new(entries.Select(entry => entry.EvidenceId));
        OptimizationHardwareAuthority hardware = OptimizationHardwareAuthorityTestData.AllEstablished();
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(snapshot,
            InspectedModelFacts.Create(ByteCount.FromBytes((ulong)billions * GiB),
                32, 4096, 32, 8, 4096, 15, 2), workload, binding,
            ByteCount.FromBytes(safeBudget), ByteCount.FromBytes(500 * GiB),
            EstimatorPolicy.ProvisionalV1(), consent, hardware,
            new OptimizationEvidenceCatalog(records), (ulong)billions * 1_000_000_000);
        return new(snapshot, workload, binding, consent, generated, new Composer(runtime),
            OptimizationIssuanceAuthority.FromGeneration(hardware, ByteCount.FromBytes(safeBudget), ByteCount.FromBytes(500 * GiB)));
    }

    private sealed record Fixture(OptimizationCapabilitySnapshot Snapshot, OptimizationWorkload Workload,
        OptimizationJourneyBinding Binding, HashSet<string> Consent, CrossRouteGenerationResult Generated,
        Composer Composer, OptimizationIssuanceAuthority Issuance);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class Composer(GgufRuntimeAuthority runtime) : IOptimizationExecutionPayloadComposer
    {
        public OptimizationRoute Route => OptimizationRoute.Gguf;
        public OptimizationExecutionPayload Compose(OptimizationCandidate candidate)
        {
            GgufRouteConfiguration config = (GgufRouteConfiguration)candidate.Configuration;
            return OptimizationExecutionPayload.ForGguf(GgufExecutionPayload.Create(
                runtime.RuntimeBuildId, runtime.RuntimeSourceCommit,
                config.Backend == CompatibilityBackend.Cpu ? GgufRuntimeBackend.Cpu : GgufRuntimeBackend.Vulkan,
                config.Backend == CompatibilityBackend.Cpu ? "CPU" : "GPU.0", 4096,
                ExecutionVocabularyMap.ToCacheType(config.KvCache), ExecutionVocabularyMap.ToCacheType(config.KvCache),
                config.Offload == GpuOffloadLevel.Full ? 32 : config.Offload == GpuOffloadLevel.Partial ? 16 : 0,
                false, 4, 128, "Measured", candidate.EvidenceId, 256, GgufWeightFormat.Imported, null));
        }
    }
}
