using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.History;

[TestClass]
public sealed class ChatHistoryGrouperTests
{
    [TestMethod]
    public void GroupsConversationsIntoApprovedDateBuckets()
    {
        DateTimeOffset now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        ChatConversation[] conversations =
        [
            Create(now),
            Create(now.AddDays(-1)),
            Create(now.AddDays(-4)),
            Create(now.AddDays(-10)),
        ];

        IReadOnlyList<ChatHistoryGroup> groups = ChatHistoryGrouper.Group(
            conversations,
            now,
            TimeZoneInfo.Utc);

        CollectionAssert.AreEqual(
            new[] { "Today", "Yesterday", "Previous 7 Days", "Older" },
            groups.Select(group => group.Label).ToArray());
    }

    private static ChatConversation Create(DateTimeOffset time) =>
        ChatConversation.Create(Guid.NewGuid(), "model", "profile", time)
            .Append(ChatMessage.User("A meaningful saved conversation", time));
}
