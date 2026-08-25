using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class OpenVinoCompatibilityEvaluatorTests
{
    [TestMethod]
    public void ExactOpenVinoAndFreshHardwareEvidence_UsesOnlyOpenVinoCandidates()
    {
        OpenVinoCompatibilityEvaluation result = Evaluate(DateTimeOffset.UtcNow);

        Assert.IsNotNull(result.Candidate);
        Assert.IsNotNull(result.Plan);
        Assert.IsTrue(result.Plan.IsExecutableBy(2));
        Assert.AreEqual(2, result.Plan.ContractVersion);
        Assert.AreEqual(OptimizationRoute.OpenVino, result.Plan.ExecutionPayload.Route);
        Assert.IsNotNull(result.Plan.ExecutionPayload.OpenVino);
        Assert.IsNull(result.Plan.ExecutionPayload.Gguf);
        Assert.AreEqual(OptimizationRoute.OpenVino, result.Candidate.Route);
        Assert.IsNotNull(result.CapabilitySnapshot);
        Assert.AreEqual(OptimizationRoute.OpenVino, result.CapabilitySnapshot.Route);
        Assert.AreEqual(5, result.CapabilitySnapshot.OpenVino!.Admitted.Count);
        Assert.IsTrue(result.Presentation.PrimaryActionEnabled);
        string visible = string.Join(" ", result.Presentation.RuntimeRows.Select(row =>
            $"{row.Title} {row.Subtitle} {row.Value}"));
        StringAssert.Contains(visible, "OpenVINO GenAI");
        Assert.IsFalse(visible.Contains("llama.cpp", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(visible.Contains("GGUF", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void StaleAvailableMemory_FailsClosedWithoutCandidate()
    {
        OpenVinoCompatibilityEvaluation result = Evaluate(
            DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2));

        Assert.IsNull(result.Candidate);
        Assert.IsFalse(result.Presentation.PrimaryActionEnabled);
        StringAssert.Contains(result.Presentation.OutcomeTitle, "could not be established");
    }

    [TestMethod]
    public void NonRepresentableSourcePrecision_CanEstimateButCannotIssueAPlan()
    {
        OpenVinoCompatibilityEvaluation result = Evaluate(
            DateTimeOffset.UtcNow,
            precision: "bfloat16");

        Assert.IsNotNull(result.Candidate);
        Assert.IsNull(result.Plan);
        Assert.IsFalse(result.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public void ModelIdentityMismatch_FailsClosedBeforeCandidateGeneration()
    {
        OpenVinoCompatibilityEvaluation result = Evaluate(
            DateTimeOffset.UtcNow,
            factsDigest: new string('9', 64));

        Assert.IsNull(result.Candidate);
        Assert.IsNull(result.Plan);
        Assert.IsFalse(result.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public void IssuedPlan_BindsEveryExactOpenVinoExecutionField()
    {
        OpenVinoCompatibilityEvaluation result = Evaluate(DateTimeOffset.UtcNow);
        Assert.IsNotNull(result.Plan);
        OptimizationExecutionPlan plan = result.Plan;
        Assert.IsNotNull(plan.ExecutionPayload.OpenVino);
        OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino;

        Assert.AreEqual("CPU", payload.Device);
        Assert.AreEqual(OpenVinoWeightPrecision.Fp16, payload.SourceWeightPrecision);
        Assert.IsFalse(payload.CompiledCacheIsModelArtifact);
        Assert.IsTrue(payload.CompiledCacheIsDisposable);
        Assert.IsNull(payload.TurboQuantBuild);
        Assert.AreEqual(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            payload.BuildIdentity.RuntimeBuild);
        Assert.AreEqual(
            "2026.3.0.0-3277-bd8d6542e3c",
            payload.BuildIdentity.GenAiBuild);
        Assert.AreEqual(
            "2026.3.0.0-703-183c6f25cda",
            payload.BuildIdentity.TokenizersBuild);
        Assert.AreEqual(new string('3', 64),
            payload.BuildIdentity.WorkerManifestDigest);
        CollectionAssert.AreEquivalent(
            new[] { "nncf", "openvino", "openvino-genai", "optimum",
                "optimum-intel", "transformers" },
            payload.OptimizerVersions.Keys.ToArray());
        Assert.AreEqual(payload.RequiresPersistentConversion,
            plan.ProducesPersistentArtifact);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            plan.ConfigurationSha256,
            "^[0-9a-f]{64}$"));
    }

    private static OpenVinoCompatibilityEvaluation Evaluate(
        DateTimeOffset capturedAtUtc,
        string precision = "float16",
        string? factsDigest = null)
    {
        const string digest =
            "1111111111111111111111111111111111111111111111111111111111111111";
        var handoff = new ModelInspectionHandoffV2(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ModelInspectionOutcome.Ready,
            factsDigest ?? digest,
            3L * 1024 * 1024 * 1024);
        var facts = new OpenVinoStaticPackageEvidence(
            1,
            new string('2', 64),
            digest,
            handoff.ModelLengthBytes,
            "granite",
            "GraniteForCausalLM",
            "text-generation-with-past",
            4096,
            precision,
            "PreTrainedTokenizerFast",
            32,
            4096,
            32,
            8,
            9,
            true);
        var builds = new OpenVinoBuildEvidence(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            new string('3', 64));
        var hardware = HardwareInspectionHandoff.Create(
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        return OpenVinoCompatibilityEvaluator.Evaluate(
            handoff,
            facts,
            builds,
            hardware,
            new AvailableMemorySnapshot(24UL * 1024 * 1024 * 1024, capturedAtUtc));
    }
}
