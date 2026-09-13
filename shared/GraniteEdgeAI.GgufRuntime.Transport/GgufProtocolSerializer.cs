using System.Text.Json;
using System.Text.Json.Serialization;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;

namespace GraniteEdgeAI.GgufRuntime.Transport;

public static class GgufProtocolSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = 32,
    };

    public static byte[] SerializeCommand(GgufRuntimeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string kind = command switch
        {
            StartSessionCommand => "start-session",
            SubmitPromptCommand => "submit-prompt",
            StopGenerationCommand => "stop-generation",
            CloseSessionCommand => "close-session",
            _ => throw new GgufTransportException("The command kind is not supported."),
        };

        return SerializeEnvelope(kind, command, command.GetType());
    }

    public static GgufRuntimeCommand DeserializeCommand(ReadOnlySpan<byte> payload)
    {
        return DeserializeEnvelope<GgufRuntimeCommand>(
            payload,
            static (kind, element) => kind switch
        {
            "start-session" => Deserialize<StartSessionCommand>(element),
            "submit-prompt" => Deserialize<SubmitPromptCommand>(element),
            "stop-generation" => Deserialize<StopGenerationCommand>(element),
            "close-session" => Deserialize<CloseSessionCommand>(element),
            _ => throw new GgufTransportException("The command kind is not supported."),
            });
    }

    public static byte[] SerializeEvent(GgufRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);
        string kind = runtimeEvent switch
        {
            SessionLoadingEvent => "session-loading",
            SessionReadyEvent => "session-ready",
            ResponseStartedEvent => "response-started",
            TextDeltaEvent => "text-delta",
            UsageUpdatedEvent => "usage-updated",
            ResponseCompletedEvent => "response-completed",
            ResponseStoppedEvent => "response-stopped",
            RuntimeFailureEvent => "runtime-failure",
            SessionClosedEvent => "session-closed",
            _ => throw new GgufTransportException("The event kind is not supported."),
        };

        return SerializeEnvelope(kind, runtimeEvent, runtimeEvent.GetType());
    }

    public static GgufRuntimeEvent DeserializeEvent(ReadOnlySpan<byte> payload)
    {
        return DeserializeEnvelope<GgufRuntimeEvent>(
            payload,
            static (kind, element) => kind switch
        {
            "session-loading" => Deserialize<SessionLoadingEvent>(element),
            "session-ready" => Deserialize<SessionReadyEvent>(element),
            "response-started" => Deserialize<ResponseStartedEvent>(element),
            "text-delta" => Deserialize<TextDeltaEvent>(element),
            "usage-updated" => Deserialize<UsageUpdatedEvent>(element),
            "response-completed" => Deserialize<ResponseCompletedEvent>(element),
            "response-stopped" => Deserialize<ResponseStoppedEvent>(element),
            "runtime-failure" => Deserialize<RuntimeFailureEvent>(element),
            "session-closed" => Deserialize<SessionClosedEvent>(element),
            _ => throw new GgufTransportException("The event kind is not supported."),
            });
    }

    private static byte[] SerializeEnvelope(string kind, object value, Type valueType)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", kind);
            writer.WritePropertyName("payload");
            JsonSerializer.Serialize(writer, value, valueType, SerializerOptions);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static TBase DeserializeEnvelope<TBase>(
        ReadOnlySpan<byte> payload,
        Func<string, JsonElement, TBase> factory)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
            JsonElement root = document.RootElement;
            ValidateNoDuplicateMembers(root);
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("kind", out JsonElement kindElement) ||
                !root.TryGetProperty("payload", out JsonElement valueElement) ||
                kindElement.ValueKind != JsonValueKind.String ||
                valueElement.ValueKind != JsonValueKind.Object)
            {
                throw new GgufTransportException("The protocol envelope is invalid.");
            }

            string kind = kindElement.GetString()!;
            TBase result = factory(kind, valueElement);
            int protocolVersion = result switch
            {
                GgufRuntimeCommand command => command.ProtocolVersion,
                GgufRuntimeEvent runtimeEvent => runtimeEvent.ProtocolVersion,
                _ => 0,
            };
            if (protocolVersion != GgufProtocolVersion.Current)
            {
                throw new GgufTransportException("The protocol version is not supported.");
            }

            return result;
        }
        catch (GgufTransportException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw new GgufTransportException(
                $"The protocol payload is invalid: {exception.GetType().Name}.");
        }
    }

    private static T Deserialize<T>(JsonElement element)
    {
        return element.Deserialize<T>(SerializerOptions)
            ?? throw new GgufTransportException("The protocol payload is empty.");
    }

    private static void ValidateNoDuplicateMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new GgufTransportException(
                        "The protocol payload contains a duplicate member.");
                }

                ValidateNoDuplicateMembers(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateNoDuplicateMembers(item);
            }
        }
    }
}
