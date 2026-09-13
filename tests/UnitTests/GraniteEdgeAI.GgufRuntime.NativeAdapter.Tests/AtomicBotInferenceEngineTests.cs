using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class AtomicBotInferenceEngineTests
{
    [TestMethod]
    public async Task StopAfterVisibleDeltaRetainsExactPartialTurn()
    {
        var history = new List<GgufAdapterMessage>();
        using var cancellation = new CancellationTokenSource();
        using var reader = new StringReader(Delta("partial answer") + Environment.NewLine);
        await using var events = AtomicBotInferenceEngine.ReadResponseAsync(
            reader, "question", history, cancellation.Token).GetAsyncEnumerator();
        Assert.IsTrue(await events.MoveNextAsync());
        Assert.AreEqual("partial answer", ((GgufAdapterTextDelta)events.Current).Text);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await events.MoveNextAsync());
        Assert.HasCount(2, history);
        Assert.AreEqual(new GgufAdapterMessage(GgufAdapterRole.User, "question"), history[0]);
        Assert.AreEqual(new GgufAdapterMessage(GgufAdapterRole.Assistant, "partial answer"), history[1]);
    }

    [TestMethod]
    public async Task DeltaThenEndOfStreamFailsWithoutCommittingHistory()
    {
        var history = new List<GgufAdapterMessage>();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => DrainAsync(
            [Delta("partial")],
            history));

        Assert.AreEqual(0, history.Count);
    }

    [TestMethod]
    public async Task HeartbeatThenEndOfStreamFailsWithoutCompletion()
    {
        var history = new List<GgufAdapterMessage>();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => DrainAsync(
            [": keep-alive"],
            history));

        Assert.AreEqual(0, history.Count);
    }

    [TestMethod]
    public async Task DoneWithoutFinishReasonFailsWithoutCommittingHistory()
    {
        var history = new List<GgufAdapterMessage>();

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => DrainAsync(
            [Delta("partial"), "data: [DONE]"],
            history));

        Assert.AreEqual(0, history.Count);
    }

    [TestMethod]
    public async Task SupportedFinishReasonsCompleteAndCommitExactHistory()
    {
        foreach (string finishReason in new[] { "stop", "length" })
        {
            var history = new List<GgufAdapterMessage>();
            IReadOnlyList<GgufAdapterGenerationEvent> events = await CollectAsync(
                [
                    ":",
                    Delta("answer"),
                    Finish(finishReason),
                    "data: [DONE]",
                ],
                history,
                CancellationToken.None);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("answer", ((GgufAdapterTextDelta)events[0]).Text);
            Assert.AreEqual(
                finishReason == "stop"
                    ? GgufAdapterCompletionReason.Stop
                    : GgufAdapterCompletionReason.Length,
                ((GgufAdapterCompleted)events[1]).Reason);
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(GgufAdapterRole.User, history[0].Role);
            Assert.AreEqual("question", history[0].Content);
            Assert.AreEqual(GgufAdapterRole.Assistant, history[1].Role);
            Assert.AreEqual("answer", history[1].Content);
        }
    }

    [TestMethod]
    public async Task SupportedFinishReasonBeforeEndOfStreamCompletesNormally()
    {
        var history = new List<GgufAdapterMessage>();

        IReadOnlyList<GgufAdapterGenerationEvent> events = await CollectAsync(
            [Delta("answer"), Finish("stop")],
            history,
            CancellationToken.None);

        Assert.IsInstanceOfType<GgufAdapterCompleted>(events[^1]);
        Assert.AreEqual(2, history.Count);
    }

    [TestMethod]
    public async Task CallerCancellationStillPropagatesWithoutCommittingHistory()
    {
        var history = new List<GgufAdapterMessage>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CollectAsync(
            [Delta("answer"), Finish("stop")],
            history,
            cancellation.Token));

        Assert.AreEqual(0, history.Count);
    }

    private static async Task DrainAsync(
        IReadOnlyList<string> lines,
        ICollection<GgufAdapterMessage> history)
    {
        await CollectAsync(lines, history, CancellationToken.None);
    }

    private static async Task<IReadOnlyList<GgufAdapterGenerationEvent>> CollectAsync(
        IReadOnlyList<string> lines,
        ICollection<GgufAdapterMessage> history,
        CancellationToken cancellationToken)
    {
        using var reader = new StringReader(string.Join(Environment.NewLine, lines));
        var events = new List<GgufAdapterGenerationEvent>();
        await foreach (GgufAdapterGenerationEvent generated in
                       AtomicBotInferenceEngine.ReadResponseAsync(
                           reader,
                           "question",
                           history,
                           cancellationToken))
        {
            events.Add(generated);
        }
        return events;
    }

    private static string Delta(string text) =>
        $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{text}\"}},\"finish_reason\":null}}]}}";

    private static string Finish(string reason) =>
        $"data: {{\"choices\":[{{\"delta\":{{}},\"finish_reason\":\"{reason}\"}}]}}";
}
