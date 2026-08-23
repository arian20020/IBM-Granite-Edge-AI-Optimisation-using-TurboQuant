using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class GgufAdapterHostTests
{
    private static readonly GgufAdapterMessage[] ExpectedHistory =
    [
        new(GgufAdapterRole.User, "first\nquestion"),
        new(GgufAdapterRole.Assistant, "first answer"),
    ];

    [TestMethod]
    public async Task RunRestoresExactHistoryAndStreamsOnlyFramedOutput()
    {
        const string input =
            "G1TURN U Zmlyc3QKcXVlc3Rpb24=\n" +
            "G1TURN A Zmlyc3QgYW5zd2Vy\n" +
            "G1START\n" +
            "G1PROMPT Zm9sbG93IHVw\n";
        var engine = new RecordingEngine(
        [
            new GgufAdapterTextDelta("hello "),
            new GgufAdapterTextDelta("world"),
            new GgufAdapterCompleted(GgufAdapterCompletionReason.Stop),
        ]);
        using var reader = new StringReader(input);
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(engine, reader, writer);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            ExpectedHistory,
            engine.InitialHistory.ToArray());
        Assert.AreEqual("follow up", engine.Prompt);
        Assert.AreEqual(
            "G1READY\r\n" +
            "G1RESPONSE\r\n" +
            "G1DELTA aGVsbG8g\r\n" +
            "G1DELTA d29ybGQ=\r\n" +
            "G1DONE stop\r\n",
            writer.ToString());
    }

    [TestMethod]
    public async Task RunRejectsUnframedInputWithoutEchoingIt()
    {
        const string sensitiveInput = "unframed private prompt\n";
        var engine = new RecordingEngine([]);
        using var reader = new StringReader(sensitiveInput);
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(engine, reader, writer);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(65, exitCode);
        Assert.AreEqual("G1FAIL protocol-violation\r\n", writer.ToString());
        Assert.IsFalse(writer.ToString().Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunClassifiesEngineInitializationFailureWithoutRuntimeDetail()
    {
        using var reader = new StringReader("G1START\n");
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(new ThrowingEngine(), reader, writer);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(70, exitCode);
        Assert.AreEqual("G1FAIL model-load-failed\r\n", writer.ToString());
    }

    [TestMethod]
    public async Task RunClassifiesUnsupportedTemplateWithoutRuntimeDetail()
    {
        using var reader = new StringReader("G1START\n");
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(
            new ThrowingEngine(new GgufUnsupportedChatTemplateException()),
            reader,
            writer);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(70, exitCode);
        Assert.AreEqual(
            "G1FAIL chat-template-unsupported\r\n",
            writer.ToString());
    }

    [TestMethod]
    public async Task RunDoesNotEmitEmptyTextDeltaFrames()
    {
        using var reader = new StringReader("G1START\nG1PROMPT aGVsbG8=\n");
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(
            new RecordingEngine(
            [
                new GgufAdapterTextDelta(string.Empty),
                new GgufAdapterCompleted(GgufAdapterCompletionReason.Stop),
            ]),
            reader,
            writer);

        int exitCode = await host.RunAsync(CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "G1READY\r\nG1RESPONSE\r\nG1DONE stop\r\n",
            writer.ToString());
    }

    [TestMethod]
    [DataRow((int)GgufAdapterCompletionReason.Stop, "G1DONE stop")]
    [DataRow((int)GgufAdapterCompletionReason.Length, "G1DONE length")]
    public async Task RunEmitsExactTypedCompletionFrame(
        int reasonValue,
        string expectedFrame)
    {
        var reason = (GgufAdapterCompletionReason)reasonValue;
        using var reader = new StringReader("G1START\nG1PROMPT aGVsbG8=\n");
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(
            new RecordingEngine(
            [
                new GgufAdapterTextDelta("answer"),
                new GgufAdapterCompleted(reason),
            ]),
            reader,
            writer);

        Assert.AreEqual(0, await host.RunAsync(CancellationToken.None));
        StringAssert.Contains(writer.ToString(), expectedFrame);
    }

    [TestMethod]
    public async Task RunRejectsGenerationWithoutCompletion()
    {
        using var reader = new StringReader("G1START\nG1PROMPT aGVsbG8=\n");
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(
            new RecordingEngine([new GgufAdapterTextDelta("partial")]),
            reader,
            writer);

        Assert.AreEqual(65, await host.RunAsync(CancellationToken.None));
        StringAssert.EndsWith(writer.ToString(), "G1FAIL protocol-violation\r\n");
    }

    [TestMethod]
    public async Task RunRejectsEventsAfterCompletion()
    {
        using var reader = new StringReader("G1START\nG1PROMPT aGVsbG8=\n");
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var host = new GgufAdapterHost(
            new RecordingEngine(
            [
                new GgufAdapterCompleted(GgufAdapterCompletionReason.Stop),
                new GgufAdapterTextDelta("invalid"),
            ]),
            reader,
            writer);

        Assert.AreEqual(65, await host.RunAsync(CancellationToken.None));
        StringAssert.EndsWith(writer.ToString(), "G1FAIL protocol-violation\r\n");
    }

    private sealed class RecordingEngine(IReadOnlyList<GgufAdapterGenerationEvent> events)
        : IGgufInferenceEngine
    {
        internal IReadOnlyList<GgufAdapterMessage> InitialHistory { get; private set; } = [];

        internal string? Prompt { get; private set; }

        public ValueTask InitializeAsync(
            IReadOnlyList<GgufAdapterMessage> initialHistory,
            CancellationToken cancellationToken)
        {
            InitialHistory = initialHistory.ToArray();
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            Prompt = prompt;
            foreach (GgufAdapterGenerationEvent generationEvent in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return generationEvent;
                await Task.Yield();
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingEngine(Exception? exception = null) : IGgufInferenceEngine
    {
        public ValueTask InitializeAsync(
            IReadOnlyList<GgufAdapterMessage> initialHistory,
            CancellationToken cancellationToken) =>
            ValueTask.FromException(
                exception ?? new InvalidOperationException("private runtime detail"));

        public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
