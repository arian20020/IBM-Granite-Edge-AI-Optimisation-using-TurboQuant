using System;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal sealed record ChatMessage
{
    [JsonConstructor]
    public ChatMessage(
        Guid id,
        ChatMessageRole role,
        string content,
        ChatCompletionStatus status,
        DateTimeOffset createdUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A message identifier is required.", nameof(id));
        }

        if (!Enum.IsDefined(role) || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (content is null || content.Length > ChatHistoryPolicy.MaximumMessageCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(content));
        }

        Id = id;
        Role = role;
        Content = content;
        Status = status;
        CreatedUtc = createdUtc.ToUniversalTime();
    }

    public Guid Id { get; }

    public ChatMessageRole Role { get; }

    public string Content { get; }

    public ChatCompletionStatus Status { get; }

    public DateTimeOffset CreatedUtc { get; }

    internal static ChatMessage User(string content, DateTimeOffset createdUtc) =>
        new(Guid.NewGuid(), ChatMessageRole.User, content,
            ChatCompletionStatus.Completed, createdUtc);

    internal static ChatMessage Assistant(
        string content,
        ChatCompletionStatus status,
        DateTimeOffset createdUtc) =>
        new(Guid.NewGuid(), ChatMessageRole.Assistant, content, status, createdUtc);

    internal ChatMessage WithContent(string content, ChatCompletionStatus status) =>
        new(Id, Role, content, status, CreatedUtc);
}
