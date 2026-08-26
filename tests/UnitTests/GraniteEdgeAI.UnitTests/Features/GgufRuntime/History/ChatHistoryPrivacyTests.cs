using System.Reflection;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.History;

[TestClass]
public sealed class ChatHistoryPrivacyTests
{
    [TestMethod]
    public void PersistentContractsExposeNoAbsoluteModelLocation()
    {
        Type[] types = [typeof(ChatConversation), typeof(ChatMessage)];
        Assert.IsFalse(types.SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .Any(property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void TitleIsLocalBoundedAndWhitespaceNormalized()
    {
        string title = ChatTitlePolicy.FromPrompt(
            "  This   is a long local prompt that should become a compact bounded title for history  ");

        Assert.IsTrue(title.Length <= ChatTitlePolicy.MaximumTitleLength);
        Assert.IsFalse(title.Contains("  ", StringComparison.Ordinal));
    }
}
