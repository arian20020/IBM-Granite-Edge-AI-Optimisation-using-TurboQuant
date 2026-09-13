namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal enum GgufAdapterCompletionReason
{
    Stop,
    Length,
}

internal sealed class GraniteGenerationBoundaryObserver
{
    internal GgufAdapterCompletionReason? Reason { get; private set; }

    internal void Complete(GgufAdapterCompletionReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason ??= reason;
    }

    internal void Reset() => Reason = null;
}
