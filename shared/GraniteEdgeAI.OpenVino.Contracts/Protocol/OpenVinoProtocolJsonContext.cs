using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    WriteIndented = false,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    MaxDepth = OpenVinoProtocol.MaximumJsonDepth)]
[JsonSerializable(typeof(StartSessionCommand))]
[JsonSerializable(typeof(PromptCommand))]
[JsonSerializable(typeof(StopTurnCommand))]
[JsonSerializable(typeof(CancelSessionCommand))]
[JsonSerializable(typeof(HelloEvent))]
[JsonSerializable(typeof(SessionStartedEvent))]
[JsonSerializable(typeof(GenerationStartedEvent))]
[JsonSerializable(typeof(TokenEvent))]
[JsonSerializable(typeof(TurnCompletedEvent))]
[JsonSerializable(typeof(TurnFailedEvent))]
[JsonSerializable(typeof(SessionCompletedEvent))]
[JsonSerializable(typeof(SessionFailedEvent))]
[JsonSerializable(typeof(SessionCancelledEvent))]
[JsonSerializable(typeof(ModelInspectionHandoffV2))]
internal sealed partial class OpenVinoProtocolJsonContext : JsonSerializerContext
{
}
