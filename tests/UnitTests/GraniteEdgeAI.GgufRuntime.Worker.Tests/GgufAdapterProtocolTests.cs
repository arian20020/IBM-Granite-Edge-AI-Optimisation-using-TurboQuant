using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Worker.Session;

namespace GraniteEdgeAI.GgufRuntime.Worker.Tests;

[TestClass]
public sealed class GgufAdapterProtocolTests
{
    private static readonly string[] ExpectedInitialFrames =
    [
        "G1TURN U aGVsbG8NCndvcmxk",
        "G1TURN A Z3Jhbml0w6k=",
        "G1START",
    ];

    [TestMethod]
    public void EncodeInitialTurnsPreservesRolesAndMultilineUtf8Content()
    {
        GgufConversationTurn[] turns =
        [
            new(GgufConversationRole.User, "hello\r\nworld"),
            new(GgufConversationRole.Assistant, "granité"),
        ];

        IReadOnlyList<string> frames = GgufAdapterProtocol.EncodeInitialTurns(turns);

        CollectionAssert.AreEqual(
            ExpectedInitialFrames,
            frames.ToArray());
    }

    [TestMethod]
    public void EncodePromptKeepsContentOutOfTheProtocolControlText()
    {
        string frame = GgufAdapterProtocol.EncodePrompt("private\ntext");

        Assert.AreEqual("G1PROMPT cHJpdmF0ZQp0ZXh0", frame);
        Assert.IsFalse(frame.Contains("private", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ParseDeltaDecodesExactUtf8Content()
    {
        GgufCliOutput output = GgufCliOutputParser.Parse(
            "G1DELTA bGluZSAxCmxpbmUgMg==",
            GgufCliOutputSource.StandardOutput);

        Assert.AreEqual(GgufCliOutputKind.TextDelta, output.Kind);
        Assert.AreEqual("line 1\nline 2", output.Text);
    }

    [TestMethod]
    public void ParseUnframedStandardOutputFailsClosed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            GgufCliOutputParser.Parse(
                "raw model output",
                GgufCliOutputSource.StandardOutput));
    }
}
