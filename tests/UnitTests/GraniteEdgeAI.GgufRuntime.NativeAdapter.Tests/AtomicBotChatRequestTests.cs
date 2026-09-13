using System.Text.Json;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class AtomicBotChatRequestTests
{
    [TestMethod]
    public void BuildIncludesBoundedGenerationAndCompleteConversation()
    {
        IReadOnlyList<GgufAdapterMessage> history =
        [
            new(GgufAdapterRole.User, "first"),
            new(GgufAdapterRole.Assistant, "answer"),
        ];

        byte[] request = AtomicBotChatRequest.Build(history, "next", 64);
        using JsonDocument document = JsonDocument.Parse(request);

        Assert.IsTrue(document.RootElement.GetProperty("stream").GetBoolean());
        Assert.AreEqual(64, document.RootElement.GetProperty("max_tokens").GetInt32());
        JsonElement messages = document.RootElement.GetProperty("messages");
        Assert.AreEqual(4, messages.GetArrayLength());
        Assert.AreEqual("system", messages[0].GetProperty("role").GetString());
        Assert.AreEqual("first", messages[1].GetProperty("content").GetString());
        Assert.AreEqual("answer", messages[2].GetProperty("content").GetString());
        Assert.AreEqual("next", messages[3].GetProperty("content").GetString());
    }
}
