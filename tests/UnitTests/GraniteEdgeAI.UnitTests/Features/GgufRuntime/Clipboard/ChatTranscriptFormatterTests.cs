using GraniteEdgeAI.Features.GgufRuntime.Clipboard;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Clipboard;

[TestClass]
public sealed class ChatTranscriptFormatterTests
{
    [TestMethod]
    public void FormatUsesApprovedRoleLabelledPlainText()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatMessage[] messages =
        [
            ChatMessage.User("Hello", now),
            ChatMessage.Assistant("Hi!", ChatCompletionStatus.Completed, now),
        ];

        Assert.AreEqual(
            $"You:{Environment.NewLine}Hello{Environment.NewLine}{Environment.NewLine}" +
            $"Granite Edge AI:{Environment.NewLine}Hi!",
            ChatTranscriptFormatter.Format(messages));
    }

    [TestMethod]
    public void FormatSkipsEmptyContentAndIncludesVisibleStreamingPartial()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatMessage[] messages =
        [
            ChatMessage.Assistant("", ChatCompletionStatus.Pending, now),
            ChatMessage.Assistant(
                "Partial answer",
                ChatCompletionStatus.Streaming,
                now),
        ];

        Assert.AreEqual(
            $"Granite Edge AI:{Environment.NewLine}Partial answer",
            ChatTranscriptFormatter.Format(messages));
    }

    [TestMethod]
    public void FormatPreservesVisibleWhitespaceInsideMessageContent()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatMessage[] messages =
        [
            ChatMessage.User("first line\r\n  indented line", now),
        ];

        Assert.AreEqual(
            $"You:{Environment.NewLine}first line\r\n  indented line",
            ChatTranscriptFormatter.Format(messages));
    }
}
