using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Tests;

[TestClass]
public sealed class GgufSessionCoordinatorTests
{
    private static readonly GgufSessionId SessionId = new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [TestMethod]
    public async Task StartThenSubmitThenOutputCompletionPreservesStateAndCorrelation()
    {
        await using var process = new RecordingCliProcess();
        var coordinator = new GgufSessionCoordinator(
            process,
            "C:\\Models\\granite.gguf");
        var start = CreateStartCommand();

        IReadOnlyList<GgufRuntimeEvent> startEvents =
            await coordinator.StartSessionAsync(start, CancellationToken.None);
        var submit = new SubmitPromptCommand(
            GgufProtocolVersion.Current,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SessionId,
            "hello");
        ResponseStartedEvent responseStarted =
            await coordinator.SubmitPromptAsync(submit, CancellationToken.None);
        TextDeltaEvent delta = (TextDeltaEvent)coordinator.HandleStandardOutput("answer")!;
        ResponseCompletedEvent completed =
            (ResponseCompletedEvent)coordinator.HandleStandardOutput("__G1_RESPONSE_DONE__")!;

        Assert.AreEqual(GgufSessionState.Ready, coordinator.State);
        Assert.HasCount(2, startEvents);
        Assert.IsInstanceOfType<SessionLoadingEvent>(startEvents[0]);
        Assert.IsInstanceOfType<SessionReadyEvent>(startEvents[1]);
        Assert.AreEqual("hello", process.Prompts.Single());
        Assert.AreEqual(submit.RequestId, responseStarted.RequestId);
        Assert.AreEqual(submit.RequestId, delta.RequestId);
        Assert.AreEqual(submit.RequestId, completed.RequestId);
        CollectionAssert.AreEqual(
            new long[] { 0, 1, 2, 3, 4 },
            startEvents.Select(runtimeEvent => runtimeEvent.Sequence)
                .Append(responseStarted.Sequence)
                .Append(delta.Sequence)
                .Append(completed.Sequence)
                .ToArray());
    }

    [TestMethod]
    public async Task SubmitBeforeStartRejectsWithoutWritingPromptOrChangingState()
    {
        await using var process = new RecordingCliProcess();
        var coordinator = new GgufSessionCoordinator(
            process,
            "C:\\Models\\granite.gguf");
        var submit = new SubmitPromptCommand(
            GgufProtocolVersion.Current,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SessionId,
            "hello");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.SubmitPromptAsync(submit, CancellationToken.None).AsTask());

        Assert.AreEqual(GgufSessionState.Created, coordinator.State);
        Assert.IsEmpty(process.Prompts);
    }

    private static StartSessionCommand CreateStartCommand()
    {
        var configuration = new GgufRuntimeConfiguration(
            "granite-3b",
            new string('a', 64),
            "cpu-test-build",
            new string('b', 40),
            GgufRuntimeBackend.Cpu,
            "cpu",
            4096,
            GgufCacheType.F16,
            GgufCacheType.F16,
            0,
            false,
            8,
            512,
            "controlled",
            "cpu-safe");
        return new StartSessionCommand(
            GgufProtocolVersion.Current,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SessionId,
            configuration,
            []);
    }

    private sealed class RecordingCliProcess : IGgufCliProcess
    {
        public List<string> Prompts { get; } = [];

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask StartAsync(
            string modelPath,
            GgufRuntimeConfiguration configuration,
            IReadOnlyList<GgufConversationTurn> initialTurns,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask WritePromptAsync(string content, CancellationToken cancellationToken)
        {
            Prompts.Add(content);
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> ReadOutputLineAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask<bool> TryInterruptAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(true);
        }

        public ValueTask TerminateAsync(CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
