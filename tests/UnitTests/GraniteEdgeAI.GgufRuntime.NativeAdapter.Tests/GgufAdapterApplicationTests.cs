using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class GgufAdapterApplicationTests
{
    private static readonly string[] ValidArguments =
    [
        "--model", "C:\\Models\\granite.gguf",
        "--ctx-size", "2048",
        "--cache-type-k", "f16",
        "--cache-type-v", "f16",
        "--n-gpu-layers", "0",
        "--threads", "2",
        "--batch-size", "32",
        "--flash-attention", "off",
        "--max-tokens", "64",
    ];

    [TestMethod]
    public async Task RunUsesParsedConfigurationAndOwnsEngineDisposal()
    {
        var engine = new DisposableEngine();
        GgufAdapterOptions? captured = null;
        using var input = new StringReader("G1START\n");
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        int exitCode = await GgufAdapterApplication.RunAsync(
            ValidArguments,
            input,
            output,
            options =>
            {
                captured = options;
                return engine;
            },
            CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(2048u, captured!.ContextSize);
        Assert.AreEqual("G1READY\r\n", output.ToString());
        Assert.IsTrue(engine.Disposed);
    }

    [TestMethod]
    public async Task RunReportsTurboQuantRequiresThePinnedForkWithoutLaunching()
    {
        string[] arguments = ValidArguments.ToArray();
        arguments[5] = "turbo3";
        bool launched = false;
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        int exitCode = await GgufAdapterApplication.RunAsync(
            arguments,
            input,
            output,
            options =>
            {
                launched = true;
                return new DisposableEngine();
            },
            CancellationToken.None);

        Assert.AreEqual(64, exitCode);
        Assert.AreEqual("G1FAIL turboquant-runtime-required\r\n", output.ToString());
        Assert.IsFalse(launched);
    }

    private sealed class DisposableEngine : IGgufInferenceEngine
    {
        internal bool Disposed { get; private set; }

        public ValueTask InitializeAsync(
            IReadOnlyList<GgufAdapterMessage> initialHistory,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<GgufAdapterGenerationEvent> GenerateAsync(
            string prompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
