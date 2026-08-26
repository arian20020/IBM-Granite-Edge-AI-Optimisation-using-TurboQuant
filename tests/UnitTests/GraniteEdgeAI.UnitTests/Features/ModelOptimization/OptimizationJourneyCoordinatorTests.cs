using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationJourneyCoordinatorTests
{
    [TestMethod]
    public async Task LateSuccessAfterCancellationCannotPublish()
    {
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry();
        var executor = new ControllableExecutor(entry.OptimizationHandoff.Plan);
        await using var coordinator = Coordinator(entry, executor);

        Task attempt = coordinator.ConfirmAsync();
        Assert.AreEqual(OptimizationJourneyKind.Running, coordinator.State.Kind);
        await coordinator.CancelAsync();
        executor.CompleteSuccess();
        await attempt;

        Assert.AreEqual(OptimizationJourneyKind.Cancelled, coordinator.State.Kind);
        Assert.AreEqual(1, executor.Calls);
    }

    [TestMethod]
    public async Task DuplicateConfirmStartsExactlyOneExecutor()
    {
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry();
        var executor = new ControllableExecutor(entry.OptimizationHandoff.Plan);
        await using var coordinator = Coordinator(entry, executor);

        Task first = coordinator.ConfirmAsync();
        await coordinator.ConfirmAsync();
        executor.CompleteSuccess();
        await first;

        Assert.AreEqual(1, executor.Calls);
        Assert.AreEqual(
            OptimizationJourneyKind.SucceededRuntimeProfile,
            coordinator.State.Kind);
    }

    [TestMethod]
    public async Task GenerationOverflowPermanentlyRetiresCoordinator()
    {
        OptimizationJourneyEntryContext entry =
            OptimizationSelectionHandoffTests.RequiredJourneyEntry();
        var executor = new ControllableExecutor(entry.OptimizationHandoff.Plan);
        await using var coordinator = Coordinator(
            entry,
            executor,
            initialGeneration: long.MaxValue);

        await coordinator.ConfirmAsync();

        Assert.IsTrue(coordinator.IsRetired);
        Assert.AreEqual(0, executor.Calls);
        Assert.AreEqual(OptimizationJourneyKind.Confirmation, coordinator.State.Kind);
    }

    private static OptimizationJourneyCoordinator Coordinator(
        OptimizationJourneyEntryContext entry,
        ControllableExecutor executor,
        long initialGeneration = 0) =>
        new(
            entry,
            new OptimizationExecutorRouter([executor]),
            new CurrentRevalidator(),
            new ContextFactory(entry.OptimizationHandoff.Plan),
            initialGeneration: initialGeneration);

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
                    "sealed-source-1"),
                "operation-root-1"));
    }

    private sealed class ControllableExecutor(OptimizationExecutionPlan plan)
        : IOptimizationExecutor
    {
        private readonly TaskCompletionSource<OptimizationExecutionResult> _result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int Calls { get; private set; }
        public OptimizationRoute Route => plan.Route;

        public Task<OptimizationExecutionResult> ExecuteAsync(
            OptimizationExecutionPlan exactPlan,
            OptimizationAttemptContext context,
            IProgress<OptimizationProgress> progress,
            CancellationToken cancellationToken)
        {
            Calls++;
            return _result.Task;
        }

        internal void CompleteSuccess() => _result.TrySetResult(
            OptimizationExecutionResult.Succeeded(
                plan,
                "runtime-profile-1",
                "6666666666666666666666666666666666666666666666666666666666666666",
                0,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch));
    }
}
