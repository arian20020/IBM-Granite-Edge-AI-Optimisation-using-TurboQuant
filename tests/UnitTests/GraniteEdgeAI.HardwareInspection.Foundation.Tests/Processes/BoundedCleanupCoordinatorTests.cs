using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Processes;

[TestClass]
public sealed class BoundedCleanupCoordinatorTests
{
    [TestMethod]
    public async Task EveryStageRunsExactlyOnceWhenEarlierStagesThrow()
    {
        int[] calls = new int[4];
        BoundedCleanupCoordinator cleanup = new(
            Throwing(OwnedCleanupStage.StandardInput, calls, 0, new IOException("private C:\\user\\temp")),
            Throwing(OwnedCleanupStage.ProcessTree, calls, 1, new TimeoutException("private timeout")),
            Successful(OwnedCleanupStage.Job, calls, 2),
            Throwing(OwnedCleanupStage.OperationEnvironment, calls, 3, new UnauthorizedAccessException("private path")));

        CleanupOutcome outcome = await cleanup.ExecuteAsync();

        Assert.IsTrue(calls.All(static call => call == 1));
        Assert.IsFalse(outcome.Succeeded);
        CollectionAssert.AreEqual(
            new[]
            {
                new CleanupFailureFact(OwnedCleanupStage.StandardInput, CleanupFailureKind.Io),
                new CleanupFailureFact(OwnedCleanupStage.ProcessTree, CleanupFailureKind.Timeout),
                new CleanupFailureFact(OwnedCleanupStage.OperationEnvironment, CleanupFailureKind.Access),
            },
            outcome.Failures.ToArray());
    }

    [TestMethod]
    public async Task ConcurrentAndRepeatedExecutionSharesOneExactlyOnceOutcome()
    {
        int calls = 0;
        BoundedCleanupCoordinator cleanup = new(new OwnedCleanupAction(
            OwnedCleanupStage.Session,
            async () =>
            {
                Interlocked.Increment(ref calls);
                await Task.Yield();
            }));

        Task<CleanupOutcome>[] attempts = Enumerable.Range(0, 16)
            .Select(_ => cleanup.ExecuteAsync())
            .ToArray();
        CleanupOutcome[] outcomes = await Task.WhenAll(attempts);
        CleanupOutcome repeated = await cleanup.ExecuteAsync();

        Assert.AreEqual(1, calls);
        Assert.IsTrue(outcomes.All(outcome => ReferenceEquals(outcomes[0], outcome)));
        Assert.AreSame(outcomes[0], repeated);
    }

    [TestMethod]
    public async Task FailureFactsAreBoundedTypedAndContainNoExceptionText()
    {
        OwnedCleanupAction[] actions = Enumerable.Range(0, 64)
            .Select(index => Throwing(
                OwnedCleanupStage.StandardError,
                new int[1],
                0,
                new InvalidOperationException("secret-" + index)))
            .ToArray();
        BoundedCleanupCoordinator cleanup = new(actions);

        CleanupOutcome outcome = await cleanup.ExecuteAsync();

        Assert.AreEqual(16, outcome.Failures.Count);
        Assert.IsTrue(outcome.Failures.All(fact =>
            fact == new CleanupFailureFact(
                OwnedCleanupStage.StandardError,
                CleanupFailureKind.InvalidState)));
        Assert.IsFalse(outcome.ToString()!.Contains("secret", StringComparison.Ordinal));
    }

    private static OwnedCleanupAction Successful(
        OwnedCleanupStage stage,
        int[] calls,
        int index) => new(stage, () =>
        {
            calls[index]++;
            return ValueTask.CompletedTask;
        });

    private static OwnedCleanupAction Throwing(
        OwnedCleanupStage stage,
        int[] calls,
        int index,
        Exception error) => new(stage, () =>
        {
            calls[index]++;
            return ValueTask.FromException(error);
        });
}
