using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

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
    [DataRow(-1)]
    [DataRow(10)]
    [DataRow(30)]
    [DataRow(50)]
    [DataRow(70)]
    [DataRow(90)]
    public void EveryVisiblePreferenceResolvesOnlyToCapabilityAdmittedCandidate(
        int preferenceValue)
    {
        OptimizationCandidate candidate = CrossFeaturePlanFixture.Candidate();
        OptimizationCapabilitySnapshot snapshot =
            CrossFeaturePlanFixture.SnapshotFor(candidate);
        OptimizationCandidate admitted =
            CrossFeaturePlanFixture.Admit(candidate, snapshot);
        OptimizationPreferenceSelection preference = preferenceValue < 0
            ? OptimizationPreferenceSelection.Automatic()
            : OptimizationPreferenceSelection.Manual(preferenceValue);

        OptimizationSelection? selection = OptimizationPreferenceResolver.Resolve(
            [admitted], preference);

        Assert.IsNotNull(selection);
        Assert.AreEqual(admitted.CanonicalDescriptor,
            selection.Candidate.CanonicalDescriptor);
        Assert.IsNotNull(selection.Candidate.AdmissionProof);
        Assert.IsTrue(selection.Candidate.AdmissionProof!.MatchesCandidate(
            selection.Candidate));
    }

    [TestMethod]
    public void ExecutionBindingRejectsChangedModelCapabilityPayloadAndHardware()
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

        OptimizationJourneyBinding changedHardware = CrossFeaturePlanFixture.Binding(
            hardwareDigest:
                "6666666666666666666666666666666666666666666666666666666666666666");
        Assert.AreNotEqual(plan.Binding.HardwareSnapshotSha256,
            changedHardware.HardwareSnapshotSha256);
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
    public void RetryMintsNewPlanIdentityAndRejectsPriorResultAsStale()
    {
        OptimizationExecutionPlan firstPlan = CrossFeaturePlanFixture.Issue();
        OptimizationExecutionResult firstResult = OptimizationExecutionResult.Failed(
            firstPlan,
            OptimizationSupportCode.UnexpectedFailure,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);
        OptimizationExecutionPlan retryPlan = CrossFeaturePlanFixture.Issue();

        Assert.AreNotEqual(firstPlan.OptimizationPlanId, retryPlan.OptimizationPlanId);
        Assert.AreNotEqual(firstResult.OptimizationPlanId, retryPlan.OptimizationPlanId);
        Assert.AreEqual(firstPlan.ConfigurationSha256, retryPlan.ConfigurationSha256,
            "Retry may preserve the selected configuration while changing attempt authority.");
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
