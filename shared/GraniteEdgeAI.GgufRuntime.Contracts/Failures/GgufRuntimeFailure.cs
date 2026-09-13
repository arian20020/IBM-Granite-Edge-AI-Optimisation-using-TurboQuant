namespace GraniteEdgeAI.GgufRuntime.Contracts.Failures;

public enum GgufRuntimeFailureCategory
{
    RuntimeUnavailable,
    RuntimeUntrusted,
    UnsupportedConfiguration,
    ModelChanged,
    ModelLoadFailed,
    ContextLimitReached,
    GenerationFailed,
    GenerationStopped,
    OperationTimedOut,
    ProtocolViolation,
    OutputLimitExceeded,
    CleanupFailed,
    ChatHistoryUnavailable,
    InsufficientStorage,
}

public sealed record GgufRuntimeFailure
{
    public GgufRuntimeFailure(GgufRuntimeFailureCategory category, string code)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A stable failure code is required.", nameof(code));
        }

        Category = category;
        Code = code;
    }

    public GgufRuntimeFailureCategory Category { get; }

    public string Code { get; }
}
