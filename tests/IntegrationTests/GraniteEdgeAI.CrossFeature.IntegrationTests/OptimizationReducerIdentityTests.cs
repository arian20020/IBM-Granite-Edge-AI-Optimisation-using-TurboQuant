using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class OptimizationReducerIdentityTests
{
    [TestMethod]
    public void ResultWithSubstitutedHardwareBindingCannotBecomeTerminalSuccess()
    {
        OptimizationExecutionPlan selected =
            CrossFeaturePlanFixture.PersistentGgufPlan(
                new string('a', 64),
                4096);
        OptimizationJourneyState running = RunningState(selected);
        OptimizationJourneyBinding substitutedBinding =
            OptimizationJourneyBinding.Create(
                selected.Binding.ModelInspectionRunId,
                selected.Binding.ModelInspectionHandoffId,
                selected.Binding.ModelSha256,
                selected.Binding.ModelLengthBytes,
                "hw-run-substituted",
                new string('9', 64));
        OptimizationExecutionPlan substituted = CopyPlan(
            selected,
            substitutedBinding);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            substituted,
            "substituted-output",
            new string('b', 64),
            10,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        OptimizationJourneyState after = OptimizationJourneyReducer.Apply(
            running,
            new OptimizationCompleted(1, result));

        Assert.AreEqual(OptimizationJourneyKind.Running, after.Kind);
        Assert.IsNull(after.Result);
    }

    [TestMethod]
    public void ResultWithSubstitutedSourceBindingCannotBecomeTerminalSuccess()
    {
        OptimizationExecutionPlan selected =
            CrossFeaturePlanFixture.PersistentGgufPlan(
                new string('a', 64),
                4096);
        OptimizationJourneyState running = RunningState(selected);
        OptimizationJourneyBinding substitutedBinding =
            OptimizationJourneyBinding.Create(
                selected.Binding.ModelInspectionRunId,
                selected.Binding.ModelInspectionHandoffId,
                new string('c', 64),
                selected.Binding.ModelLengthBytes,
                selected.Binding.ProductHardwareRunId,
                selected.Binding.HardwareSnapshotSha256);
        OptimizationExecutionPlan substituted = CopyPlan(
            selected,
            substitutedBinding);
        OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
            substituted,
            "substituted-output",
            new string('b', 64),
            10,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        OptimizationJourneyState after = OptimizationJourneyReducer.Apply(
            running,
            new OptimizationCompleted(1, result));

        Assert.AreEqual(OptimizationJourneyKind.Running, after.Kind);
        Assert.IsNull(after.Result);
    }

    private static OptimizationJourneyState RunningState(
        OptimizationExecutionPlan plan)
    {
        Assert.IsTrue(OptimizationSelectionHandoff.TryCreate(
            plan,
            plan.Binding,
            plan.CapabilitySnapshot,
            plan.Preference,
            out OptimizationSelectionHandoff? handoff));
        var entry = new OptimizationJourneyEntryContext(
            handoff!,
            OptimizationJourneyOrigin.Required,
            currentModelFallback: null);
        return OptimizationJourneyReducer.Apply(
            OptimizationJourneyState.Initial(entry),
            new OptimizationStarted(1));
    }

    private static OptimizationExecutionPlan CopyPlan(
        OptimizationExecutionPlan selected,
        OptimizationJourneyBinding binding) => new(
            selected.ContractVersion,
            selected.OptimizationPlanId,
            binding,
            selected.CapabilitySnapshot,
            selected.Workload,
            selected.Candidate,
            selected.ExecutionPayload,
            selected.Preference,
            selected.SharedWithAdjacentBand,
            selected.ConfigurationSha256,
            selected.CreatedAtUtc);
}
