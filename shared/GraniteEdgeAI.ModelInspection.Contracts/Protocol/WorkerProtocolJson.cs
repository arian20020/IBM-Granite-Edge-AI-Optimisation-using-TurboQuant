using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Serializes and parses the bounded version-1 JSON protocol shared by the
/// application adapter and the protected Model Inspection worker.
/// </summary>
public static class WorkerProtocolJson
{
    private const int MaximumJsonDepth = 32;

    // Throw on invalid byte sequences instead of silently replacing them.
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumJsonDepth
    };

    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    /// <summary>
    /// Serializes one validated command or message to compact UTF-8 JSON.
    /// </summary>
    public static byte[] Serialize<T>(T value)
    {
        if (value is null)
        {
            throw new WorkerProtocolException(
                "Protocol value must be present before serialization.");
        }

        ValidateSupportedProtocolValue(value);

        try
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                value,
                value.GetType(),
                SerializerOptions);

            EnsurePayloadLength(payload.Length);
            return payload;
        }
        catch (WorkerProtocolException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw CreateSerializationFailure(exception);
        }
        catch (NotSupportedException exception)
        {
            throw CreateSerializationFailure(exception);
        }
    }

    /// <summary>
    /// Parses one validated start or cancellation command.
    /// </summary>
    public static object DeserializeCommand(ReadOnlySpan<byte> payload)
    {
        return DeserializeEnvelope(
            payload,
            discriminatorName: "commandType",
            DeserializeCommandByKind);
    }

    /// <summary>
    /// Parses one validated hello, started, progress, or completed message.
    /// </summary>
    public static object DeserializeMessage(ReadOnlySpan<byte> payload)
    {
        return DeserializeEnvelope(
            payload,
            discriminatorName: "messageType",
            DeserializeMessageByKind);
    }

    private static object DeserializeEnvelope(
        ReadOnlySpan<byte> payload,
        string discriminatorName,
        Func<string, string, object> deserializeByKind)
    {
        EnsurePayloadLength(payload.Length);

        try
        {
            string json = StrictUtf8.GetString(payload);

            using JsonDocument document = JsonDocument.Parse(
                json,
                DocumentOptions);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new WorkerProtocolException(
                    "Protocol JSON root must be an object.");
            }

            RejectDuplicateProperties(root);
            ValidateProtocolVersion(root);

            string discriminator = ReadRequiredString(
                root,
                discriminatorName);

            return deserializeByKind(discriminator, json);
        }
        catch (WorkerProtocolException)
        {
            throw;
        }
        catch (DecoderFallbackException exception)
        {
            throw new WorkerProtocolException(
                $"Protocol JSON is not valid UTF-8: {exception.GetType().Name}.");
        }
        catch (JsonException exception)
        {
            throw new WorkerProtocolException(
                $"Protocol JSON is malformed: {exception.GetType().Name}.");
        }
        catch (NotSupportedException exception)
        {
            throw new WorkerProtocolException(
                $"Protocol JSON contains an unsupported value: {exception.GetType().Name}.");
        }
    }

    private static object DeserializeCommandByKind(
        string commandType,
        string json)
    {
        return commandType switch
        {
            "startInspection" => DeserializeAndValidate<
                WorkerStartInspectionCommand>(json),
            "cancelInspection" => DeserializeAndValidate<
                WorkerCancelInspectionCommand>(json),
            _ => throw new WorkerProtocolException(
                "commandType must identify a supported command.")
        };
    }

    private static object DeserializeMessageByKind(
        string messageType,
        string json)
    {
        return messageType switch
        {
            "hello" => DeserializeAndValidate<WorkerHelloMessage>(json),
            "started" => DeserializeAndValidate<WorkerStartedMessage>(json),
            "progress" => DeserializeAndValidate<WorkerProgressMessage>(json),
            "completed" => DeserializeAndValidate<WorkerCompletedMessage>(json),
            _ => throw new WorkerProtocolException(
                "messageType must identify a supported message.")
        };
    }

    private static T DeserializeAndValidate<T>(string json)
        where T : class
    {
        T? value = JsonSerializer.Deserialize<T>(json, SerializerOptions);
        if (value is null)
        {
            throw new WorkerProtocolException(
                "Protocol JSON did not produce the required record.");
        }

        ValidateSupportedProtocolValue(value);
        return value;
    }

    private static void ValidateSupportedProtocolValue(object value)
    {
        switch (value)
        {
            case WorkerHelloMessage hello:
                hello.Validate();
                return;
            case WorkerStartInspectionCommand start:
                start.Validate();
                return;
            case WorkerCancelInspectionCommand cancel:
                cancel.Validate();
                return;
            case WorkerStartedMessage started:
                started.Validate();
                return;
            case WorkerProgressMessage progress:
                progress.Validate();
                return;
            case WorkerCompletedMessage completed:
                completed.Validate();
                return;
            default:
                throw new WorkerProtocolException(
                    "Only approved worker commands and messages may cross the protocol boundary.");
        }
    }

    private static void ValidateProtocolVersion(JsonElement root)
    {
        if (!root.TryGetProperty("protocolVersion", out JsonElement property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out int protocolVersion))
        {
            throw new WorkerProtocolException(
                "protocolVersion must be an integer.");
        }

        if (protocolVersion != WorkerProtocol.Version)
        {
            throw new WorkerProtocolException(
                $"protocolVersion must equal {WorkerProtocol.Version}.");
        }
    }

    private static string ReadRequiredString(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw new WorkerProtocolException(
                $"{propertyName} must be a string.");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new WorkerProtocolException(
                $"{propertyName} must not be empty.");
        }

        return value;
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                HashSet<string> propertyNames = new(StringComparer.Ordinal);

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!propertyNames.Add(property.Name))
                    {
                        throw new WorkerProtocolException(
                            "Protocol JSON contains a duplicate property name.");
                    }

                    RejectDuplicateProperties(property.Value);
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    RejectDuplicateProperties(item);
                }

                break;
        }
    }

    private static void EnsurePayloadLength(int payloadLength)
    {
        if (payloadLength <= 0)
        {
            throw new WorkerProtocolException(
                "Protocol payload must not be empty.");
        }

        if (payloadLength > WorkerProtocol.MaximumMessageBytes)
        {
            throw new WorkerProtocolException(
                "Protocol payload exceeds the maximum permitted byte length.");
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            MaxDepth = MaximumJsonDepth,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }

    private static WorkerProtocolException CreateSerializationFailure(
        Exception exception)
    {
        return new WorkerProtocolException(
            $"Protocol value could not be serialized: {exception.GetType().Name}.");
    }
}
