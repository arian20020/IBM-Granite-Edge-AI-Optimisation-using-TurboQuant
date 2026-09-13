using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;

namespace GraniteEdgeAI.GgufRuntime.Contracts.Events;

public abstract record GgufRuntimeEvent
{
    protected GgufRuntimeEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(protocolVersion);

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request identifier cannot be empty.", nameof(requestId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        SessionId = sessionId;
        Sequence = sequence;
    }

    public int ProtocolVersion { get; }

    public Guid RequestId { get; }

    public GgufSessionId SessionId { get; }

    public long Sequence { get; }
}

public sealed record TextDeltaEvent : GgufRuntimeEvent
{
    public TextDeltaEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        string text)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("A text delta cannot be empty.", nameof(text));
        }

        Text = text;
    }

    public string Text { get; }
}

public sealed record SessionLoadingEvent : GgufRuntimeEvent
{
    public SessionLoadingEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        string phase)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        if (string.IsNullOrWhiteSpace(phase))
        {
            throw new ArgumentException("A loading phase is required.", nameof(phase));
        }

        Phase = phase;
    }

    public string Phase { get; }
}

public sealed record SessionReadyEvent(
    int Version,
    Guid CorrelationId,
    GgufSessionId CorrelatedSessionId,
    long EventSequence)
    : GgufRuntimeEvent(Version, CorrelationId, CorrelatedSessionId, EventSequence);

public sealed record ResponseStartedEvent(
    int Version,
    Guid CorrelationId,
    GgufSessionId CorrelatedSessionId,
    long EventSequence)
    : GgufRuntimeEvent(Version, CorrelationId, CorrelatedSessionId, EventSequence);

public sealed record UsageUpdatedEvent : GgufRuntimeEvent
{
    public UsageUpdatedEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        int inputTokenCount,
        int outputTokenCount,
        int contextTokenCount)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokenCount);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokenCount);
        ArgumentOutOfRangeException.ThrowIfNegative(contextTokenCount);
        InputTokenCount = inputTokenCount;
        OutputTokenCount = outputTokenCount;
        ContextTokenCount = contextTokenCount;
    }

    public int InputTokenCount { get; }

    public int OutputTokenCount { get; }

    public int ContextTokenCount { get; }
}

public sealed record ResponseCompletedEvent : GgufRuntimeEvent
{
    public ResponseCompletedEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        GgufCompletionReason reason)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason = reason;
    }

    public GgufCompletionReason Reason { get; }
}

public enum GgufStopDisposition
{
    Stopped,
    StoppedNeedsReload,
}

public sealed record ResponseStoppedEvent : GgufRuntimeEvent
{
    public ResponseStoppedEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        GgufStopDisposition disposition)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        if (!Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        Disposition = disposition;
    }

    public GgufStopDisposition Disposition { get; }
}

public sealed record RuntimeFailureEvent : GgufRuntimeEvent
{
    public RuntimeFailureEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        GgufRuntimeFailure failure)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    public GgufRuntimeFailure Failure { get; }
}

public sealed record SessionClosedEvent : GgufRuntimeEvent
{
    public SessionClosedEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        bool cleanupSucceeded)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        CleanupSucceeded = cleanupSucceeded;
    }

    public bool CleanupSucceeded { get; }
}
