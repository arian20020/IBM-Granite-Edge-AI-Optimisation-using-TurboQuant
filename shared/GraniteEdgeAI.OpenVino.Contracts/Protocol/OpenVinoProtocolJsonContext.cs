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
[JsonSerializable(typeof(StartInspectionCommand))]
[JsonSerializable(typeof(StartSessionCommand))]
[JsonSerializable(typeof(PromptCommand))]
[JsonSerializable(typeof(StopTurnCommand))]
[JsonSerializable(typeof(CancelSessionCommand))]
[JsonSerializable(typeof(CloseSessionCommand))]
[JsonSerializable(typeof(HelloEvent))]
[JsonSerializable(typeof(InspectionStartedEvent))]
[JsonSerializable(typeof(InspectionProgressEvent))]
[JsonSerializable(typeof(InspectionCompletedEvent))]
[JsonSerializable(typeof(InspectionFailedEvent))]
[JsonSerializable(typeof(SessionStartedEvent))]
[JsonSerializable(typeof(GenerationStartedEvent))]
[JsonSerializable(typeof(TokenEvent))]
[JsonSerializable(typeof(TurboQuantActivationEvent))]
[JsonSerializable(typeof(TurnCompletedEvent))]
[JsonSerializable(typeof(TurnFailedEvent))]
[JsonSerializable(typeof(SessionCompletedEvent))]
[JsonSerializable(typeof(SessionFailedEvent))]
[JsonSerializable(typeof(SessionCancelledEvent))]
[JsonSerializable(typeof(ModelInspectionHandoffV2))]
[JsonSerializable(typeof(OpenVinoBuildEvidence))]
[JsonSerializable(typeof(TurboQuantBuildEvidence))]
internal sealed partial class OpenVinoProtocolJsonContext : JsonSerializerContext
{
}
