namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal enum ChatCompletionStatus
{
    Pending,
    Streaming,
    Completed,
    Stopped,
    Incomplete,
    Failed,
    LimitReached,
}

internal enum ChatMessageRole
{
    User,
    Assistant,
    Control,
}
