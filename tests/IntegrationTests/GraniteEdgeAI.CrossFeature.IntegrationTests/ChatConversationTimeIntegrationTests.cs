using GraniteEdgeAI.Features.GgufRuntime.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class ChatConversationTimeIntegrationTests
{
    [TestMethod]
    public void ReplacementUsesTheSuppliedUtcTime()
    {
        DateTimeOffset created = new(2032, 4, 5, 6, 7, 8, TimeSpan.Zero);
        ChatMessage assistant = ChatMessage.Assistant(
            string.Empty,
            ChatCompletionStatus.Pending,
            created);
        ChatConversation conversation = ChatConversation.Create(
                Guid.NewGuid(),
                "model",
                "cpu",
                created)
            .Append(assistant);
        DateTimeOffset replaced = created.AddMinutes(3);

        ChatConversation updated = conversation.ReplaceMessage(
            assistant.WithContent("done", ChatCompletionStatus.Completed),
            replaced);

        Assert.AreEqual(replaced, updated.UpdatedUtc);
    }
}
