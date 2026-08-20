using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

public interface IOpenVinoEvent
{
    string EventType { get; }

    void Validate();
}

public sealed record HelloEvent(string ProtocolId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "hello";

    public void Validate() => OpenVinoProtocol.Require(
        ProtocolId is OpenVinoProtocol.OfficialProtocolId or OpenVinoProtocol.TurboQuantProtocolId,
        nameof(ProtocolId) + " must be an approved OpenVINO protocol identity.");
}

public sealed record InspectionCompletedEvent(Guid InspectionRunId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "inspectionCompleted";

    public void Validate() => OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
}

public sealed record InspectionFailedEvent(Guid InspectionRunId, OpenVinoSupportCode SupportCode) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "inspectionFailed";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
        SupportCode.Validate();
    }
}

public sealed record SessionStartedEvent(Guid SessionId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionStarted";

    public void Validate() => OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}

public sealed record GenerationStartedEvent(Guid SessionId, Guid TurnId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "generationStarted";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
    }
}

public sealed record TokenEvent(Guid SessionId, Guid TurnId, long Sequence, string Text) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "token";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        OpenVinoProtocol.Require(Sequence >= 0, nameof(Sequence) + " must not be negative.");
        OpenVinoProtocol.RequireUtf8Limit(Text, OpenVinoProtocol.MaximumOperationTextUtf8Bytes, nameof(Text));
    }
}

public sealed record TurnCompletedEvent(Guid SessionId, Guid TurnId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "turnCompleted";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
    }
}

public sealed record TurnFailedEvent(Guid SessionId, Guid TurnId, OpenVinoSupportCode SupportCode) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "turnFailed";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        SupportCode.Validate();
    }
}

public sealed record SessionCompletedEvent(Guid SessionId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionCompleted";

    public void Validate() => OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}

public sealed record SessionFailedEvent(Guid SessionId, OpenVinoSupportCode SupportCode) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionFailed";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        SupportCode.Validate();
    }
}

public sealed record SessionCancelledEvent(Guid SessionId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionCancelled";

    public void Validate() => OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}
