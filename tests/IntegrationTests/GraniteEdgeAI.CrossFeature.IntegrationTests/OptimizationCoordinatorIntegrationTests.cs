using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class OptimizationCoordinatorIntegrationTests
{
    [TestMethod]
    public async Task CoordinatorForwardsTheExactSelectedPlanToRouteExecutor()
    {
        OptimizationExecutionPlan selected = CrossFeaturePlanFixture.Issue();
        Assert.IsTrue(OptimizationSelectionHandoff.TryCreate(
            selected,
            selected.Binding,
            selected.CapabilitySnapshot,
            selected.Preference,
            out OptimizationSelectionHandoff? handoff));
        var entry = new OptimizationJourneyEntryContext(
            handoff!, OptimizationJourneyOrigin.Required, currentModelFallback: null);
        var executor = new RecordingExecutor();
        await using var coordinator = new OptimizationJourneyCoordinator(
            entry,
            new OptimizationExecutorRouter([executor]),
            new CurrentRevalidator(),
            new ContextFactory(selected));

        await coordinator.ConfirmAsync();

        Assert.AreSame(selected, executor.ReceivedPlan);
        Assert.AreEqual(selected.OptimizationPlanId,
            executor.ReceivedPlan!.OptimizationPlanId);
        Assert.AreEqual(selected.ConfigurationSha256,
            executor.ReceivedPlan.ConfigurationSha256);
        Assert.AreEqual(OptimizationJourneyKind.Failed, coordinator.State.Kind);
    }

    private sealed class RecordingExecutor : IOptimizationExecutor
    {
        internal OptimizationExecutionPlan? ReceivedPlan { get; private set; }
        public OptimizationRoute Route => OptimizationRoute.OpenVino;

        public Task<OptimizationExecutionResult> ExecuteAsync(
            OptimizationExecutionPlan plan,
            OptimizationAttemptContext context,
            IProgress<OptimizationProgress> progress,
            CancellationToken cancellationToken)
        {
            ReceivedPlan = plan;
            return Task.FromResult(OptimizationExecutionResult.Failed(
                plan,
                OptimizationSupportCode.UnexpectedFailure,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch));
        }
    }

    private sealed class CurrentRevalidator : IOptimizationRevalidator
    {
        public Task<OptimizationRevalidationResult> RevalidateAsync(
            OptimizationExecutionPlan plan,
            CancellationToken cancellationToken) =>
            Task.FromResult(OptimizationRevalidationResult.Current);
    }

    private sealed class ContextFactory(OptimizationExecutionPlan plan)
        : IOptimizationAttemptContextFactory
    {
        public Task<OptimizationAttemptContext> CreateAsync(
            long generation,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OptimizationAttemptContext(
                generation,
                new StagedSourceSnapshot(
                    plan.Binding.ModelSha256,
                    plan.Binding.ModelLengthBytes,
                    "sealed-source-t1-r2"),
                "operation-t1-r2"));
    }
}
