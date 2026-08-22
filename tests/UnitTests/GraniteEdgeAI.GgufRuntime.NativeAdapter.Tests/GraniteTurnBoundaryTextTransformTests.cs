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
}
