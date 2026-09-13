using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.GgufRuntime.Contracts.Commands;

public enum GgufConversationRole
{
    User,
    Assistant,
}

public sealed record GgufConversationTurn
{
    public GgufConversationTurn(GgufConversationRole role, string content)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > GgufProtocolLimits.MaxPromptCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(content));
        }

        Role = role;
        Content = content;
    }

    public GgufConversationRole Role { get; }

    public string Content { get; }
}

public abstract record GgufRuntimeCommand
{
    protected GgufRuntimeCommand(int protocolVersion, Guid requestId, GgufSessionId sessionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(protocolVersion);

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request identifier cannot be empty.", nameof(requestId));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        SessionId = sessionId;
    }

    public int ProtocolVersion { get; }

    public Guid RequestId { get; }

    public GgufSessionId SessionId { get; }
}

public sealed record StartSessionCommand : GgufRuntimeCommand
{
    public StartSessionCommand(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        GgufRuntimeConfiguration configuration,
        IReadOnlyList<GgufConversationTurn> initialTurns)
        : base(protocolVersion, requestId, sessionId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(initialTurns);
        if (initialTurns.Count > GgufProtocolLimits.MaxInitialTurns)
        {
            throw new ArgumentOutOfRangeException(nameof(initialTurns));
        }

        Configuration = configuration;
        InitialTurns = initialTurns.ToArray();
    }

    public GgufRuntimeConfiguration Configuration { get; }

    public IReadOnlyList<GgufConversationTurn> InitialTurns { get; }
}

public sealed record SubmitPromptCommand : GgufRuntimeCommand
{
    public SubmitPromptCommand(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        string content,
        bool isTransientTitle = false)
        : base(protocolVersion, requestId, sessionId)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > GgufProtocolLimits.MaxPromptCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(content));
        }

        Content = content;
        if (isTransientTitle && content.Length > 4096)
            throw new ArgumentOutOfRangeException(nameof(content));
        IsTransientTitle = isTransientTitle;
    }

    public string Content { get; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsTransientTitle { get; }
}

public sealed record StopGenerationCommand(
    int Version,
    Guid CorrelationId,
    GgufSessionId CorrelatedSessionId)
    : GgufRuntimeCommand(Version, CorrelationId, CorrelatedSessionId);

public sealed record CloseSessionCommand(
    int Version,
    Guid CorrelationId,
    GgufSessionId CorrelatedSessionId)
    : GgufRuntimeCommand(Version, CorrelationId, CorrelatedSessionId);
