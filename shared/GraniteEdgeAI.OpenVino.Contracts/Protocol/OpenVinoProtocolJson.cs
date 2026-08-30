using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>Strict source-generated JSON serialization for the closed OpenVINO wire contracts.</summary>
public static class OpenVinoProtocolJson
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = OpenVinoProtocol.MaximumJsonDepth
    };

    /// <summary>Serializes a known command or event after validation.</summary>
    public static byte[] Serialize(object value)
    {
        if (value is null)
        {
            throw new OpenVinoProtocolException("protocol value must be present.");
        }

        ValidateKnownValue(value);
        try
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, GetTypeInfo(value.GetType()));
            EnsureLineLength(payload.Length);
            return payload;
        }
        catch (OpenVinoProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new OpenVinoProtocolException("protocol value could not be serialized.");
        }
    }

    /// <summary>Parses one command with exact discriminator and member closure.</summary>
    public static IOpenVinoCommand DeserializeCommand(ReadOnlySpan<byte> payload) =>
        Deserialize(payload, "commandType", DeserializeCommandByType);

    /// <summary>Parses one event with exact discriminator and member closure.</summary>
    public static IOpenVinoEvent DeserializeEvent(ReadOnlySpan<byte> payload) =>
        Deserialize(payload, "eventType", DeserializeEventByType);

    private static T Deserialize<T>(
        ReadOnlySpan<byte> payload,
        string discriminatorName,
        Func<string, string, T> deserializeByType)
    {
        EnsureLineLength(payload.Length);
        try
        {
            string json = OpenVinoProtocol.StrictUtf8.GetString(payload);
            using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);
            JsonElement root = document.RootElement;
            OpenVinoProtocol.Require(root.ValueKind == JsonValueKind.Object, "protocol JSON root must be an object.");
            RejectDuplicateProperties(root);
            OpenVinoProtocol.Require(
                root.TryGetProperty(discriminatorName, out JsonElement discriminatorElement) && discriminatorElement.ValueKind == JsonValueKind.String,
                discriminatorName + " must be a string.");
            string? discriminator = discriminatorElement.GetString();
            OpenVinoProtocol.Require(!string.IsNullOrWhiteSpace(discriminator), discriminatorName + " must not be empty.");
            return deserializeByType(discriminator!, json);
        }
        catch (OpenVinoProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or JsonException or NotSupportedException)
        {
            throw new OpenVinoProtocolException("protocol JSON is invalid.");
        }
    }

    private static IOpenVinoCommand DeserializeCommandByType(string commandType, string json) => commandType switch
    {
        "startInspection" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.StartInspectionCommand),
        "startSession" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.StartSessionCommand),
        "prompt" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.PromptCommand),
        "stopTurn" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.StopTurnCommand),
        "cancelSession" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.CancelSessionCommand),
        "closeSession" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.CloseSessionCommand),
        _ => throw new OpenVinoProtocolException("commandType must identify an approved OpenVINO command.")
    };

    private static IOpenVinoEvent DeserializeEventByType(string eventType, string json) => eventType switch
    {
        "hello" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.HelloEvent),
        "inspectionStarted" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.InspectionStartedEvent),
        "inspectionProgress" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.InspectionProgressEvent),
        "inspectionCompleted" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.InspectionCompletedEvent),
        "inspectionFailed" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.InspectionFailedEvent),
        "sessionStarted" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.SessionStartedEvent),
        "generationStarted" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.GenerationStartedEvent),
        "token" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.TokenEvent),
        "turboQuantActivation" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.TurboQuantActivationEvent),
        "turnCompleted" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.TurnCompletedEvent),
        "turnFailed" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.TurnFailedEvent),
        "sessionCompleted" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.SessionCompletedEvent),
        "sessionFailed" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.SessionFailedEvent),
        "sessionCancelled" => DeserializeAndValidate(json, OpenVinoProtocolJsonContext.Default.SessionCancelledEvent),
        _ => throw new OpenVinoProtocolException("eventType must identify an approved OpenVINO event.")
    };

    private static T DeserializeAndValidate<T>(string json, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        T? value = JsonSerializer.Deserialize(json, typeInfo);
        if (value is null)
        {
            throw new OpenVinoProtocolException("protocol JSON did not produce a protocol value.");
        }

        ValidateKnownValue(value);
        return value;
    }

    private static void ValidateKnownValue(object value)
    {
        switch (value)
        {
            case IOpenVinoCommand command:
                command.Validate();
                return;
            case IOpenVinoEvent @event:
                @event.Validate();
                return;
            default:
                throw new OpenVinoProtocolException("only approved OpenVINO contracts may cross this boundary.");
        }
    }

    private static JsonTypeInfo GetTypeInfo(Type type) => type == typeof(StartInspectionCommand) ? OpenVinoProtocolJsonContext.Default.StartInspectionCommand :
        type == typeof(StartSessionCommand) ? OpenVinoProtocolJsonContext.Default.StartSessionCommand :
        type == typeof(PromptCommand) ? OpenVinoProtocolJsonContext.Default.PromptCommand :
        type == typeof(StopTurnCommand) ? OpenVinoProtocolJsonContext.Default.StopTurnCommand :
        type == typeof(CancelSessionCommand) ? OpenVinoProtocolJsonContext.Default.CancelSessionCommand :
        type == typeof(CloseSessionCommand) ? OpenVinoProtocolJsonContext.Default.CloseSessionCommand :
        type == typeof(HelloEvent) ? OpenVinoProtocolJsonContext.Default.HelloEvent :
        type == typeof(InspectionStartedEvent) ? OpenVinoProtocolJsonContext.Default.InspectionStartedEvent :
        type == typeof(InspectionProgressEvent) ? OpenVinoProtocolJsonContext.Default.InspectionProgressEvent :
        type == typeof(InspectionCompletedEvent) ? OpenVinoProtocolJsonContext.Default.InspectionCompletedEvent :
        type == typeof(InspectionFailedEvent) ? OpenVinoProtocolJsonContext.Default.InspectionFailedEvent :
        type == typeof(SessionStartedEvent) ? OpenVinoProtocolJsonContext.Default.SessionStartedEvent :
        type == typeof(GenerationStartedEvent) ? OpenVinoProtocolJsonContext.Default.GenerationStartedEvent :
        type == typeof(TokenEvent) ? OpenVinoProtocolJsonContext.Default.TokenEvent :
        type == typeof(TurboQuantActivationEvent) ? OpenVinoProtocolJsonContext.Default.TurboQuantActivationEvent :
        type == typeof(TurnCompletedEvent) ? OpenVinoProtocolJsonContext.Default.TurnCompletedEvent :
        type == typeof(TurnFailedEvent) ? OpenVinoProtocolJsonContext.Default.TurnFailedEvent :
        type == typeof(SessionCompletedEvent) ? OpenVinoProtocolJsonContext.Default.SessionCompletedEvent :
        type == typeof(SessionFailedEvent) ? OpenVinoProtocolJsonContext.Default.SessionFailedEvent :
        type == typeof(SessionCancelledEvent) ? OpenVinoProtocolJsonContext.Default.SessionCancelledEvent :
        throw new OpenVinoProtocolException("only approved OpenVINO contracts may cross this boundary.");

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                OpenVinoProtocol.Require(names.Add(property.Name), "protocol JSON contains a duplicate property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement value in element.EnumerateArray())
            {
                RejectDuplicateProperties(value);
            }
        }
    }

    private static void EnsureLineLength(int length)
    {
        OpenVinoProtocol.Require(length > 0, "protocol line must not be empty.");
        OpenVinoProtocol.Require(length <= OpenVinoProtocol.MaximumLineBytes, "protocol line exceeds the maximum permitted UTF-8 length.");
    }
}
