using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.GgufRuntime.ViewModels;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.ViewModels;

[TestClass]
public sealed class ChatPageViewModelTests
{
    [TestMethod]
    public void SelectedConversationProjectsEveryPromptAndResponse()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatConversation conversation = ChatConversation.Create(
            Guid.NewGuid(), "model", "cpu", now)
            .Append(ChatMessage.User("one", now))
            .Append(ChatMessage.Assistant("two", ChatCompletionStatus.Completed, now));

        ChatConversationViewModel viewModel = new(conversation);

        Assert.AreEqual(2, viewModel.Messages.Count);
        Assert.AreEqual("one", viewModel.Messages[0].Content);
        Assert.AreEqual("two", viewModel.Messages[1].Content);
    }
}
