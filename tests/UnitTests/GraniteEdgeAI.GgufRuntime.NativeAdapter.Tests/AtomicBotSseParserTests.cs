namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class AtomicBotSseParserTests
{
    [TestMethod]
    public void ParseExtractsTextAndLengthCompletion()
    {
        AtomicBotSseEvent delta = AtomicBotSseParser.Parse(
            "data: {\"choices\":[{\"delta\":{\"content\":\"hello\"},\"finish_reason\":null}]}");
        AtomicBotSseEvent complete = AtomicBotSseParser.Parse(
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}");

        Assert.AreEqual("hello", delta.Text);
        Assert.AreEqual(GgufAdapterCompletionReason.Length, complete.CompletionReason);
    }

    [TestMethod]
    public void ParseRecognisesDoneWithoutInventingText()
    {
        AtomicBotSseEvent result = AtomicBotSseParser.Parse("data: [DONE]");

        Assert.IsTrue(result.Done);
        Assert.IsNull(result.Text);
    }

    [TestMethod]
    [DataRow(":")]
    [DataRow(": keep-alive")]
    public void ParseAcceptsSseCommentsWithoutInventingContent(string comment)
    {
        AtomicBotSseEvent result = AtomicBotSseParser.Parse(comment);

        Assert.IsFalse(result.Done);
        Assert.IsNull(result.Text);
        Assert.IsNull(result.CompletionReason);
    }

    [TestMethod]
    public void CommentsCanBeInterleavedWithTokensAndCompletion()
    {
        string[] lines =
        [
            ": keep-alive",
            "data: {\"choices\":[{\"delta\":{\"content\":\"hello\"},\"finish_reason\":null}]}",
            ":",
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}",
            "data: [DONE]",
        ];

        AtomicBotSseEvent[] parsed = lines.Select(AtomicBotSseParser.Parse).ToArray();

        Assert.IsNull(parsed[0].Text);
        Assert.AreEqual("hello", parsed[1].Text);
        Assert.IsNull(parsed[2].Text);
        Assert.AreEqual(
            GgufAdapterCompletionReason.Stop,
            parsed[3].CompletionReason);
        Assert.IsTrue(parsed[4].Done);
    }

    [TestMethod]
    public void ParseRejectsNonDataAndMalformedFrames()
    {
        Assert.ThrowsExactly<FormatException>(() => AtomicBotSseParser.Parse("event: message"));
        Assert.ThrowsExactly<FormatException>(() => AtomicBotSseParser.Parse("data: { nope"));
    }
}
