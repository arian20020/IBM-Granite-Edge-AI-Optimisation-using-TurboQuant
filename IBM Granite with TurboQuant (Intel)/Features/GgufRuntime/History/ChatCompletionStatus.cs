namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal enum ChatCompletionStatus
{
    Pending,
    Streaming,
    Completed,
    Stopped,
    Incomplete,
    Failed,
}

internal enum ChatMessageRole
{
    User,
    Assistant,
}
