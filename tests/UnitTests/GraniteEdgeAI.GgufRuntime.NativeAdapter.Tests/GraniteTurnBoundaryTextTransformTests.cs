using System.Text;
using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class GraniteTurnBoundaryTextTransformTests
{
    [TestMethod]
    [DataRow("Me: Hello", "Hello")]
    [DataRow(" me: Hello", "Hello")]
    [DataRow("ME: Hello", "Hello")]
    public async Task LeadingMeLabelIsRemoved(string input, string expected) =>
        Assert.AreEqual(expected, await TransformAsync(input));

    [TestMethod]
    public async Task ChunkSplitLeadingLabelIsRemoved() =>
        Assert.AreEqual("Hello", await TransformAsync("M", "e", ": ", "Hello"));

    [TestMethod]
    public async Task OrdinaryMeTextIsPreserved() =>
        Assert.AreEqual("Tell me: why", await TransformAsync("Tell me: why"));

    [TestMethod]
    [DataRow("User: a person who uses a system")]
    [DataRow("Assistant: a person who provides help")]
    public async Task LeadingRoleDefinitionIsPreserved(string input) =>
        Assert.AreEqual(input, await TransformAsync(input));

    [TestMethod]
    [DataRow("Answer\nUser: fabricated")]
    [DataRow("Answer\r\nassistant: fabricated")]
    [DataRow("Answer\nMe: fabricated")]
    public async Task FabricatedNextTurnIsDiscarded(string input) =>
        Assert.AreEqual("Answer", (await TransformAsync(input)).TrimEnd());

    [TestMethod]
    public async Task ChunkSplitFabricatedTurnIsDiscarded() =>
        Assert.AreEqual(
            "Answer",
            (await TransformAsync("Answer\nUs", "er", ": fabricated")).TrimEnd());

    [TestMethod]
    public async Task SavedEmptyFenceLoopStopsAfterUsefulAnswer() =>
        Assert.AreEqual(
            "Hello! How can I assist you today?",
            (await TransformAsync(
                "Me: Hello! How can I assist you today?\n```",
                "\n```\n```\n```")).TrimEnd());

    [TestMethod]
    public async Task EmptyFenceLoopAcrossFragmentedNewlinesIsDiscarded() =>
        Assert.AreEqual(
            "Answer",
            (await TransformAsync("Answer\r\n`", "``\r\n\r", "\n```"))
                .TrimEnd());

    [TestMethod]
    public async Task LegitimateFencedCodeAndRoleTextArePreserved()
    {
        const string code = "Example:\n```csharp\nUser: value = input;\n```\nDone.";

        Assert.AreEqual(code, await TransformAsync(code));
    }

    [TestMethod]
    public async Task SeparateNonEmptyCodeFencesArePreserved()
    {
        const string code = "```text\none\n```\n\n```text\ntwo\n```";

        Assert.AreEqual(code, await TransformAsync(code));
    }

    [TestMethod]
    public async Task CloneStartsWithIndependentState()
    {
        var original = new GraniteTurnBoundaryTextTransform();
        LLama.Abstractions.ITextStreamTransform clone = original.Clone();

        Assert.AreNotSame(original, clone);
        Assert.AreEqual(
            "Hello",
            await CollectAsync(clone.TransformAsync(Chunks("Me: Hello"))));
    }

    [TestMethod]
    public async Task SourceEndLeavesTokenCompletionToTheSampler()
    {
        var observer = new GraniteGenerationBoundaryObserver();
        var transform = new GraniteTurnBoundaryTextTransform(2, observer);

        Assert.AreEqual(
            "one two",
            await CollectAsync(transform.TransformAsync(Chunks("one", " two"))));
        Assert.IsNull(observer.Reason);
    }

    [TestMethod]
    public async Task StreamChunksAreNotClippedAsTokens()
    {
        var observer = new GraniteGenerationBoundaryObserver();
        var transform = new GraniteTurnBoundaryTextTransform(2, observer);

        Assert.AreEqual(
            "one two hidden",
            await CollectAsync(transform.TransformAsync(
                Chunks("one", " two", " hidden"))));
        Assert.IsNull(observer.Reason);
    }

    [TestMethod]
    public async Task CompleteStreamAndNextTurnEmitTheirOwnText()
    {
        var observer = new GraniteGenerationBoundaryObserver();
        var source = new ContinuingTokenSource();
        var transform = new GraniteTurnBoundaryTextTransform(2, observer);

        string limited = await CollectAsync(transform.TransformAsync(source.ReadLimitedAsync()));

        Assert.AreEqual("one two hidden", limited);
        Assert.IsNull(observer.Reason);
        Assert.IsTrue(source.LimitedTurnCompleted);

        LlamaSharpInferenceEngine.BeginGeneration(observer);
        string continuation = await CollectAsync(
            transform.TransformAsync(source.ReadContinuationAsync()));

        Assert.AreEqual("continued", continuation);
        Assert.IsNull(observer.Reason);
        Assert.IsFalse(continuation.Contains("hidden", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CloneSharesCompletionObserver()
    {
        var observer = new GraniteGenerationBoundaryObserver();
        var original = new GraniteTurnBoundaryTextTransform(1, observer);
        LLama.Abstractions.ITextStreamTransform clone = original.Clone();

        Assert.AreEqual(
            "visible\n",
            await CollectAsync(clone.TransformAsync(
                Chunks("visible", "\nUser: hidden"))));
        Assert.AreEqual(GgufAdapterCompletionReason.Stop, observer.Reason);
    }

    [TestMethod]
    public void ResetClearsThePreviousTurnsReason()
    {
        var observer = new GraniteGenerationBoundaryObserver();
        observer.Complete(GgufAdapterCompletionReason.Length);

        observer.Reset();

        Assert.IsNull(observer.Reason);
    }

    private static Task<string> TransformAsync(params string[] chunks) =>
        CollectAsync(new GraniteTurnBoundaryTextTransform()
            .TransformAsync(Chunks(chunks)));

    private static async Task<string> CollectAsync(
        IAsyncEnumerable<string> chunks)
    {
        var result = new StringBuilder();
        await foreach (string chunk in chunks)
        {
            result.Append(chunk);
        }

        return result.ToString();
    }

    private static async IAsyncEnumerable<string> Chunks(params string[] chunks)
    {
        foreach (string chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
        }
    }

    private sealed class ContinuingTokenSource
    {
        internal bool LimitedTurnCompleted { get; private set; }

        internal async IAsyncEnumerable<string> ReadLimitedAsync()
        {
            foreach (string chunk in new[] { "one", " two", " hidden" })
            {
                yield return chunk;
                await Task.Yield();
            }

            LimitedTurnCompleted = true;
        }

        internal async IAsyncEnumerable<string> ReadContinuationAsync()
        {
            yield return LimitedTurnCompleted ? "continued" : " hidden";
            await Task.Yield();
        }
    }
}
