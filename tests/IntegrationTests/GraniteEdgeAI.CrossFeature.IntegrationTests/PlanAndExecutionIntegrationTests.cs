using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class PlanAndExecutionIntegrationTests
{
    [TestMethod]
    public void AdmittedPreferenceIssuesStableExactV3Plan()
    {
        OptimizationExecutionPlan first = CrossFeaturePlanFixture.Issue();
        OptimizationExecutionPlan second = CrossFeaturePlanFixture.Issue();

        Assert.AreEqual(3, first.ContractVersion);
        Assert.AreEqual(OptimizationRoute.OpenVino, first.Route);
        Assert.AreEqual(CrossFeaturePlanFixture.ModelDigest, first.Binding.ModelSha256);
        Assert.AreEqual(CrossFeaturePlanFixture.HardwareDigest,
            first.Binding.HardwareSnapshotSha256);
        Assert.AreEqual(first.ConfigurationSha256, second.ConfigurationSha256);
        Assert.AreNotEqual(first.OptimizationPlanId, second.OptimizationPlanId);
        StringAssert.Matches(first.ConfigurationSha256, new("^[0-9a-f]{64}$"));
        Assert.IsTrue(first.MatchesExecutionPayload(first.ExecutionPayload));
    }

    [TestMethod]
    public void ResultFactoryCopiesExactPersistentPlanAuthority()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.Issue();
        OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino!;
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan,
            "persistent-openvino-output",
            new string('8', 64),
            4096,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(OpenVinoWeightPrecision.Fp16, payload.SourceWeightPrecision);
        Assert.AreEqual(OpenVinoWeightPrecision.EightBit, payload.TargetWeightPrecision);
        Assert.AreEqual("ov-int8", payload.EvidenceId);
        Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
        Assert.AreEqual(plan.Binding.ModelSha256, result.SourceSha256);
        Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
        Assert.IsTrue(plan.MatchesExecutionPayload(plan.ExecutionPayload));
        Assert.IsTrue(plan.MatchesCapability(plan.CapabilitySnapshot));
    }

    [TestMethod]
    public void TurboQuantConfigurationCannotBeAdmittedWithoutExactBuildCapability()
    {
        OptimizationExecutionPlan ordinary = CrossFeaturePlanFixture.Issue();
        OpenVinoBuildIdentity build = ordinary.ExecutionPayload.OpenVino!.BuildIdentity;
        var admitted = OpenVinoAdmittedConfiguration.Create(
            "ov-turboquant-tbq4",
            DeviceRouteId.Cpu,
            OpenVinoWeightFormat.Original,
            OpenVinoKvCacheFormat.TurboQuantTbq4,
            OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Disabled,
            1,
            512,
            8192,
            SupportLevel.Experimental,
            requiresEvidence: true);
        OpenVinoExecutionAuthority releasedAuthority =
            OpenVinoExecutionAuthority.Create(
                admitted.EvidenceId,
                "openvino.standard.cpu.original.default.v1",
                OpenVinoWeightPrecision.Fp16,
                build,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["openvino"] = "2026.3.0"
                },
                compiledCacheIsDisposable: true,
                turboQuantBuild: null);

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoCapabilityPayload.Create(
                "2026.3.0",
                [admitted],
                [releasedAuthority]));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(10)]
    [DataRow(30)]
    [DataRow(50)]
    [DataRow(70)]
    [DataRow(90)]
    public void EveryVisiblePreferenceResolvesOnlyToCapabilityAdmittedCandidate(
        int preferenceValue)
    {
        OptimizationPreferenceSelection preference = preferenceValue < 0
            ? OptimizationPreferenceSelection.Automatic()
            : OptimizationPreferenceSelection.Manual(preferenceValue);
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.Issue(
            preference: preference);

        Assert.AreEqual("ov-int8", plan.Candidate.EvidenceId);
        Assert.IsTrue(plan.IsExecutableBy(OptimizationExecutionPlan.CurrentContractVersion));
        Assert.IsTrue(plan.MatchesCapability(plan.CapabilitySnapshot));
        Assert.AreEqual(preference, plan.Preference);
    }

    [TestMethod]
    public void PlanMatchingRejectsChangedSourceCapabilityAndPayload()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.Issue();
        OptimizationCapabilitySnapshot changedCapability =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-capability-changed",
                "4444444444444444444444444444444444444444444444444444444444444444",
                plan.CapabilitySnapshot.OpenVino!);

        Assert.IsTrue(plan.MatchesSource(
            CrossFeaturePlanFixture.ModelDigest,
            4 * CrossFeaturePlanFixture.GiB));
        Assert.IsFalse(plan.MatchesSource(
            "5555555555555555555555555555555555555555555555555555555555555555",
            4 * CrossFeaturePlanFixture.GiB));
        Assert.IsFalse(plan.MatchesSource(
            CrossFeaturePlanFixture.ModelDigest,
            4 * CrossFeaturePlanFixture.GiB + 1));
        Assert.IsTrue(plan.MatchesCapability(plan.CapabilitySnapshot));
        Assert.IsFalse(plan.MatchesCapability(changedCapability));

        OptimizationCandidate changedCandidate = CrossFeaturePlanFixture.Candidate(
            OpenVinoWeightFormat.Original,
            "ov-original");
        OptimizationExecutionPayload changedPayload =
            CrossFeaturePlanFixture.Payload(changedCandidate);
        Assert.IsTrue(plan.MatchesExecutionPayload(plan.ExecutionPayload));
        Assert.IsFalse(plan.MatchesExecutionPayload(changedPayload));
    }

    [TestMethod]
    public void ExactSelectedPlanAloneCanReachExecution()
    {
        OptimizationExecutionPlan selected = CrossFeaturePlanFixture.Issue();
        OptimizationCandidate differentCandidate = CrossFeaturePlanFixture.Candidate(
            OpenVinoWeightFormat.Original,
            "ov-original");
        OptimizationExecutionPayload differentPayload =
            CrossFeaturePlanFixture.Payload(differentCandidate);

        Assert.IsTrue(selected.MatchesExecutionPayload(selected.ExecutionPayload));
        Assert.IsFalse(selected.MatchesExecutionPayload(differentPayload));
        Assert.AreNotEqual(
            selected.ConfigurationSha256,
            differentPayload.ComputeRuntimeConfigurationSha256());
    }

    [TestMethod]
    public void RetryReducerRejectsPriorAttemptResultAsStale()
    {
        OptimizationExecutionPlan firstPlan = CrossFeaturePlanFixture.Issue();
        OptimizationExecutionResult firstResult = OptimizationExecutionResult.Failed(
            firstPlan,
            OptimizationSupportCode.UnexpectedFailure,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);
        OptimizationExecutionPlan retryPlan = CrossFeaturePlanFixture.Issue();

        Assert.AreNotEqual(firstPlan.OptimizationPlanId, retryPlan.OptimizationPlanId);
        Assert.AreEqual(firstPlan.ConfigurationSha256, retryPlan.ConfigurationSha256,
            "Retry may preserve the selected configuration while changing attempt authority.");
        Assert.IsTrue(OptimizationSelectionHandoff.TryCreate(
            retryPlan,
            retryPlan.Binding,
            retryPlan.CapabilitySnapshot,
            retryPlan.Preference,
            out OptimizationSelectionHandoff? handoff));
        var entry = new OptimizationJourneyEntryContext(
            handoff!,
            OptimizationJourneyOrigin.Required,
            currentModelFallback: null);
        OptimizationJourneyState running = OptimizationJourneyReducer.Apply(
            OptimizationJourneyState.Initial(entry),
            new OptimizationStarted(2));

        OptimizationJourneyState afterPriorAttempt = OptimizationJourneyReducer.Apply(
            running,
            new OptimizationCompleted(2, firstResult));

        Assert.AreEqual(running, afterPriorAttempt,
            "The real reducer must ignore a result issued by the prior plan attempt.");
        Assert.AreEqual(OptimizationJourneyKind.Running, afterPriorAttempt.Kind);
        Assert.IsNull(afterPriorAttempt.Result);
    }

    [TestMethod]
    [DataRow(OptimizationSupportCode.SourceIdentityMismatch)]
    [DataRow(OptimizationSupportCode.CapabilityDrift)]
    [DataRow(OptimizationSupportCode.ModelBindingMismatch)]
    [DataRow(OptimizationSupportCode.HardwareBindingMismatch)]
    [DataRow(OptimizationSupportCode.ToolNotAdmitted)]
    public void DriftPublishesReplanWithNoStaleOutput(
        OptimizationSupportCode supportCode)
    {
        OptimizationExecutionResult result = OptimizationExecutionResult.ReplanRequired(
            CrossFeaturePlanFixture.Issue(),
            supportCode,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(OptimizationExecutionStatus.ReplanRequired, result.Status);
        Assert.IsFalse(result.IsSuccessful);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
    }

    [TestMethod]
    [DataRow(OptimizationSupportCode.ConversionFailed)]
    [DataRow(OptimizationSupportCode.ValidationFailed)]
    [DataRow(OptimizationSupportCode.SmokeTestFailed)]
    [DataRow(OptimizationSupportCode.ReinspectionFailed)]
    [DataRow(OptimizationSupportCode.PublicationFailed)]
    [DataRow(OptimizationSupportCode.UnexpectedFailure)]
    public void FailuresPublishNoSuccessOrStaleOutput(
        OptimizationSupportCode supportCode)
    {
        OptimizationExecutionResult result = OptimizationExecutionResult.Failed(
            CrossFeaturePlanFixture.Issue(),
            supportCode,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(OptimizationExecutionStatus.Failed, result.Status);
        Assert.IsFalse(result.IsSuccessful);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
    }

    [TestMethod]
    public void CancellationPublishesNoSuccessOrStaleOutput()
    {
        OptimizationExecutionResult result = OptimizationExecutionResult.Cancelled(
            CrossFeaturePlanFixture.Issue(),
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(OptimizationExecutionStatus.Cancelled, result.Status);
        Assert.AreEqual(OptimizationSupportCode.CancelledByUser, result.SupportCode);
        Assert.IsFalse(result.IsSuccessful);
        Assert.IsNull(result.OutputIdentity);
        Assert.IsNull(result.OutputManifestSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
    }

    [TestMethod]
    public void RuntimeOnlyOpenVinoSuccessCannotMasqueradeAsDownloadableModel()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.Issue(
            OpenVinoWeightFormat.Original);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan,
            "runtime-profile-1",
            "7777777777777777777777777777777777777777777777777777777777777777",
            outputSizeBytes: 0,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.IsFalse(plan.ProducesPersistentArtifact);
        Assert.AreEqual(
            OptimizationExecutionStatus.SucceededRuntimeProfile,
            result.Status);
        Assert.IsTrue(result.IsSuccessful);
        Assert.IsFalse(result.ProducedPersistentArtifact);
        Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
        Assert.AreEqual(0UL, result.OutputSizeBytes);
    }

    [TestMethod]
    public void PersistentSuccessBindsExactPlanDigestAndRejectsEmptyArtifact()
    {
        OptimizationExecutionPlan plan = CrossFeaturePlanFixture.Issue();
        const string manifest =
            "8888888888888888888888888888888888888888888888888888888888888888";

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            OptimizationExecutionResult.Succeeded(
                plan,
                "persistent-output-1",
                manifest,
                outputSizeBytes: 0,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch));

        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            plan,
            "persistent-output-1",
            manifest,
            outputSizeBytes: 4096,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent, result.Status);
        Assert.IsTrue(result.ProducedPersistentArtifact);
        Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
        Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
        Assert.AreEqual(CrossFeaturePlanFixture.ModelDigest, result.SourceSha256);
        Assert.AreEqual(manifest, result.OutputManifestSha256);
        Assert.AreEqual(4096UL, result.OutputSizeBytes);
    }
}
