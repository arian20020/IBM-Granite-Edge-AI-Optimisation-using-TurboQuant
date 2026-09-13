using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class GgufAdapterHostTests
{
    [TestMethod]
    public async Task MalformedInputDuringGenerationDrainsBeforeProtocolFailure()
    {
        using var input = new BlockingConsoleReader();
        var engine = new StoppableEngine();
        using var output = new StringWriter();
        input.Add("G1START");
        input.Add("G1PROMPT Zmlyc3Q=");
        Task<int> running = Task.Run(() => new GgufAdapterHost(engine, input, output).RunAsync(CancellationToken.None));
        bool drainedAtExit;
        try
        {
            await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            input.Add("malformed");
            Assert.AreEqual(65, await running.WaitAsync(TimeSpan.FromSeconds(2)));
            drainedAtExit = engine.Drained;
        }
        finally { engine.Release.TrySetResult(); }
        Assert.IsTrue(drainedAtExit, "Protocol failure must not abandon a live generation pump.");
        StringAssert.EndsWith(output.ToString(), "G1FAIL protocol-violation\r\n");
    }

    [TestMethod]
    public async Task NormalStopCancelsAndDrainsBeforeAcceptingNextPrompt()
    {
        using var input = new BlockingConsoleReader();
        var engine = new StoppableEngine();
        using var output = new CompletionWriter(() => engine.Drained);
        input.Add("G1START");
        input.Add("G1PROMPT Zmlyc3Q=");
        Task<int> running = Task.Run(() => new GgufAdapterHost(engine, input, output).RunAsync(CancellationToken.None));
        bool stopped = false;
        int exitCode;
        try
        {
            await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            input.Add("G1STOP");
            try
            {
                await output.FirstCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));
                stopped = engine.Cancelled;
            }
            catch (TimeoutException) { }
        }
        finally
        {
            engine.Release.TrySetResult();
            input.Add("G1PROMPT bmV4dA==");
            input.Add("<EOF>");
            exitCode = await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.IsTrue(stopped, "Stop must be consumed while normal inference is running.");
        Assert.IsTrue(output.RestoredAtFirstCompletion, "DONE must follow iterator cleanup.");
        Assert.AreEqual(0, exitCode);
        Assert.HasCount(2, engine.Prompts);
        Assert.AreEqual("first", engine.Prompts[0]);
        Assert.AreEqual("next", engine.Prompts[1]);
        Assert.AreEqual(2, output.CompletionCount);
        Assert.AreEqual(5, input.ReadCount);
    }

    private sealed class StoppableEngine : IGgufInferenceEngine
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal List<string> Prompts { get; } = [];
        internal bool Cancelled { get; private set; }
        internal bool Drained { get; private set; }
        public ValueTask InitializeAsync(IReadOnlyList<GgufAdapterMessage> history, CancellationToken token) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            Prompts.Add(prompt);
            if (prompt == "first")
            {
                yield return new GgufAdapterTextDelta("partial");
                Started.TrySetResult();
                try { await Release.Task.WaitAsync(token); }
                finally { Cancelled = token.IsCancellationRequested; Drained = true; }
            }
            yield return new GgufAdapterCompleted(GgufAdapterCompletionReason.Stop);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task TitleCompletionDoesNotWaitForBlockingConsoleReadAndRetainsNextPrompt()
    {
        using var input = new BlockingConsoleReader();
        var engine = new TitleEngine();
        using var output = new CompletionWriter(() => engine.Restored);
        input.Add("G1START");
        input.Add("G1TITLE dGl0bGU=");
        Task<int> running = Task.Run(() => new GgufAdapterHost(engine, input, output).RunAsync(CancellationToken.None));
        bool completedWithoutMoreInput = false;
        int exitCode;
        try
        {
            try
            {
                await output.FirstCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));
                completedWithoutMoreInput = true;
            }
            catch (TimeoutException) { }
        }
        finally
        {
            // always release a synchronously blocked read, including the red baseline
            input.Add("G1PROMPT bmV4dA==");
            input.Add("<EOF>");
            exitCode = await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.IsTrue(completedWithoutMoreInput, "A restored title must publish DONE without another input line.");
        Assert.IsTrue(engine.Restored);
        Assert.IsTrue(output.RestoredAtFirstCompletion);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("next", engine.NormalPrompt);
        Assert.AreEqual(4, input.ReadCount);
        Assert.AreEqual(2, output.CompletionCount);
    }

    private sealed class BlockingConsoleReader : TextReader
    {
        private readonly System.Collections.Concurrent.BlockingCollection<string> lines = new();
        internal int ReadCount { get; private set; }
        internal void Add(string line) => lines.Add(line);
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            string line = lines.Take(cancellationToken); // Models Console.In's synchronous prefix.
            ReadCount++;
            return ValueTask.FromResult(line == "<EOF>" ? null : line);
        }
        protected override void Dispose(bool disposing) { if (disposing) lines.Dispose(); base.Dispose(disposing); }
    }

    private sealed class CompletionWriter(Func<bool> restored) : StringWriter
    {
        internal TaskCompletionSource FirstCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int CompletionCount { get; private set; }
        internal bool RestoredAtFirstCompletion { get; private set; }
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Span.StartsWith("G1DONE", StringComparison.Ordinal))
            {
                if (CompletionCount == 0) RestoredAtFirstCompletion = restored();
                CompletionCount++;
                FirstCompletion.TrySetResult();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class TitleEngine : IGgufInferenceEngine, IGgufTitleInferenceEngine
    {
        internal bool Restored { get; private set; }
        internal string? NormalPrompt { get; private set; }
        public ValueTask InitializeAsync(IReadOnlyList<GgufAdapterMessage> history, CancellationToken token) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateTitleAsync(string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            try
            {
                await Task.Delay(50, token);
                yield return new GgufAdapterTextDelta("Cache quantisation");
                yield return new GgufAdapterCompleted(GgufAdapterCompletionReason.Stop);
            }
            finally { Restored = true; }
        }
        public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            NormalPrompt = prompt;
            await Task.Yield();
            yield return new GgufAdapterCompleted(GgufAdapterCompletionReason.Stop);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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
