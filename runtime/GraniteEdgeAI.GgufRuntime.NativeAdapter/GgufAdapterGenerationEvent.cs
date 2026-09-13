namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal abstract record GgufAdapterGenerationEvent;

internal sealed record GgufAdapterTextDelta(string Text)
    : GgufAdapterGenerationEvent;

internal sealed record GgufAdapterCompleted : GgufAdapterGenerationEvent
{
    internal GgufAdapterCompleted(GgufAdapterCompletionReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason = reason;
    }

    internal GgufAdapterCompletionReason Reason { get; }
}
