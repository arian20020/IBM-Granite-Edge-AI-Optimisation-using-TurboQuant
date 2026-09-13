using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal enum ChatTitleOwnership
{
    LegacyProtected,
    Pending,
    Generated,
    Manual,
}

internal sealed record ChatConversation
{
    internal const int CurrentVersion = 1;

    [JsonConstructor]
    public ChatConversation(
        int version,
        Guid id,
        string title,
        string modelId,
        string profileId,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc,
        IReadOnlyList<ChatMessage> messages,
        ChatTitleOwnership titleOwnership = ChatTitleOwnership.LegacyProtected)
    {
        if (version != CurrentVersion || id == Guid.Empty)
        {
            throw new ArgumentException("The conversation identity is invalid.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(messages);
        if (!Enum.IsDefined(titleOwnership))
        {
            throw new ArgumentOutOfRangeException(nameof(titleOwnership));
        }
        if (title.Length > ChatTitlePolicy.MaximumTitleLength ||
            messages.Count > ChatHistoryPolicy.MaximumMessagesPerConversation)
        {
            throw new ArgumentOutOfRangeException(nameof(messages));
        }

        Version = version;
        Id = id;
        Title = title;
        ModelId = modelId;
        ProfileId = profileId;
        CreatedUtc = createdUtc.ToUniversalTime();
        UpdatedUtc = updatedUtc.ToUniversalTime();
        Messages = messages.ToArray();
        TitleOwnership = titleOwnership;
    }

    public int Version { get; }
    public Guid Id { get; }
    public string Title { get; }
    public string ModelId { get; }
    public string ProfileId { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }
    public IReadOnlyList<ChatMessage> Messages { get; }
    public ChatTitleOwnership TitleOwnership { get; }

    internal static ChatConversation Create(
        Guid id,
        string modelId,
        string profileId,
        DateTimeOffset createdUtc) => new(
            CurrentVersion,
            id,
            "New chat",
            modelId,
            profileId,
            createdUtc,
            createdUtc,
            [],
            ChatTitleOwnership.Pending);

    internal ChatConversation Append(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        string title = TitleOwnership == ChatTitleOwnership.Pending &&
            Title == "New chat" && message.Role == ChatMessageRole.User
            ? ChatTitlePolicy.FromPrompt(message.Content)
            : Title;
        return new ChatConversation(
            Version,
            Id,
            title,
            ModelId,
            ProfileId,
            CreatedUtc,
            message.CreatedUtc,
            [.. Messages, message],
            TitleOwnership);
    }

    internal ChatConversation Rename(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new ChatConversation(Version, Id, title.Trim(), ModelId, ProfileId,
            CreatedUtc, UpdatedUtc, Messages, ChatTitleOwnership.Manual);
    }

    internal ChatConversation WithGeneratedTitle(string title)
    {
        if (TitleOwnership != ChatTitleOwnership.Pending)
            throw new InvalidOperationException("This title is protected.");
        return new ChatConversation(Version, Id, title, ModelId, ProfileId,
            CreatedUtc, UpdatedUtc, Messages, ChatTitleOwnership.Generated);
    }

    internal ChatConversation ReplaceMessage(
        ChatMessage message,
        DateTimeOffset updatedUtc)
    {
        ArgumentNullException.ThrowIfNull(message);
        ChatMessage[] updated = Messages
            .Select(existing => existing.Id == message.Id ? message : existing)
            .ToArray();
        if (!updated.Any(existing => existing.Id == message.Id))
        {
            throw new InvalidOperationException("The message does not belong to this conversation.");
        }

        return new ChatConversation(
            Version,
            Id,
            Title,
            ModelId,
            ProfileId,
            CreatedUtc,
            updatedUtc,
            updated,
            TitleOwnership);
    }
}
