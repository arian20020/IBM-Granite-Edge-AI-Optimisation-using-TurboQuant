using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class OptimizationReducerIdentityTests
{
    [TestMethod]
    public void TerminalSuccessWithSubstitutedJourneyIdentityIsSuppressed()
    {
        OptimizationExecutionPlan selected =
            CrossFeaturePlanFixture.PersistentGgufPlan(new string('a', 64), 4096);
        var failures = new List<string>();
        foreach (string substitutedIdentity in new[] { "hardware", "source" })
        {
            OptimizationJourneyState running = RunningState(selected);
            OptimizationJourneyBinding substitutedBinding =
                OptimizationJourneyBinding.Create(
                    selected.Binding.ModelInspectionRunId,
                    selected.Binding.ModelInspectionHandoffId,
                    substitutedIdentity == "source"
                        ? new string('c', 64)
                        : selected.Binding.ModelSha256,
                    selected.Binding.ModelLengthBytes,
                    substitutedIdentity == "hardware"
                        ? "hardware-run-substituted"
                        : selected.Binding.ProductHardwareRunId,
                    substitutedIdentity == "hardware"
                        ? new string('9', 64)
                        : selected.Binding.HardwareSnapshotSha256);
            OptimizationExecutionPlan substituted = CopyPlan(
                selected, substitutedBinding);
            OptimizationExecutionResult result = OptimizationExecutionResult.Succeeded(
                substituted, "substituted-output", new string('b', 64), 10,
                sourceUnchanged: true, DateTimeOffset.UnixEpoch);
            OptimizationJourneyState after = OptimizationJourneyReducer.Apply(
                running, new OptimizationCompleted(1, result));
            if (after.Kind != OptimizationJourneyKind.Running || after.Result is not null)
                failures.Add(substitutedIdentity);
        }

        Assert.AreEqual(0, failures.Count,
            "Terminal success accepted substituted identities: "
            + string.Join(", ", failures));
    }

    [TestMethod]
    public void WrongGenerationSuppressesLateCancellationAndCompletion()
    {
        OptimizationExecutionPlan selected =
            CrossFeaturePlanFixture.PersistentGgufPlan(new string('a', 64), 4096);
        OptimizationJourneyState running = RunningState(selected);
        OptimizationJourneyState cancelling = OptimizationJourneyReducer.Apply(
            running,
            new OptimizationCancellationRequested(1));
        OptimizationExecutionResult late = OptimizationExecutionResult.Cancelled(
            selected,
            sourceUnchanged: true,
            DateTimeOffset.UnixEpoch);

        OptimizationJourneyState afterLateCompletion = OptimizationJourneyReducer.Apply(
            cancelling,
            new OptimizationCompleted(2, late));
        OptimizationJourneyState afterLateCancellation = OptimizationJourneyReducer.Apply(
            afterLateCompletion,
            new OptimizationCancelled(2));

        Assert.AreEqual(cancelling, afterLateCancellation);
        Assert.IsNull(afterLateCancellation.Result);
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
