using System.Buffers;
using System.Text.Json;

namespace GraniteEdgeAI.ModelInspection.Contracts;

public enum ModelInspectionRoute
{
    Gguf,
    OpenVino
}

public enum ModelInspectionOutcomeV2
{
    Ready,
    ReadyWithWarnings
}

public sealed record ModelSourceV2(
    string ModelType,
    ModelInspectionRoute Route,
    string ModelSha256,
    long ModelLengthBytes);

public sealed record ModelInspectionResultV2(
    Guid ModelInspectionRunId,
    ModelInspectionOutcomeV2 Outcome,
    ModelInspectionRoute Route,
    string ModelSha256,
    long ModelLengthBytes);

public sealed record ModelInspectionHandoffV2(
    ushort SchemaVersion,
    Guid ModelInspectionHandoffId,
    Guid ModelInspectionRunId,
    ModelInspectionOutcomeV2 Outcome,
    string ModelSha256,
    long ModelLengthBytes)
{
    public const ushort RequiredSchemaVersion = 2;
    public const int MaximumCanonicalUtf8Bytes = 512;

    public void Validate()
    {
        WorkerProtocolValidation.Require(
            SchemaVersion == RequiredSchemaVersion,
            nameof(SchemaVersion),
            "must equal 2");
        RequireUuidV4(ModelInspectionHandoffId, nameof(ModelInspectionHandoffId));
        RequireUuidV4(ModelInspectionRunId, nameof(ModelInspectionRunId));
        WorkerProtocolValidation.Require(
            ModelInspectionHandoffId != ModelInspectionRunId,
            nameof(ModelInspectionHandoffId),
            "must have a role distinct from modelInspectionRunId");
        WorkerProtocolValidation.RequireDefinedEnum(Outcome, nameof(Outcome));
        RequireIdentity(ModelSha256, ModelLengthBytes);
    }

    public byte[] ToCanonicalUtf8Json()
    {
        Validate();
        var buffer = new ArrayBufferWriter<byte>(MaximumCanonicalUtf8Bytes);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonicalJson(writer);
        }

        WorkerProtocolValidation.Require(
            buffer.WrittenCount <= MaximumCanonicalUtf8Bytes,
            nameof(ModelInspectionHandoffV2),
            "must stay within the canonical UTF-8 size bound");
        return buffer.WrittenSpan.ToArray();
    }

    public static ModelInspectionHandoffV2 Parse(ReadOnlySpan<byte> payload)
    {
        WorkerProtocolValidation.Require(
            !payload.IsEmpty && payload.Length <= MaximumCanonicalUtf8Bytes,
            nameof(payload),
            "must be present and within the canonical UTF-8 size bound");
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                payload.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 2
                });
            ModelInspectionHandoffV2 handoff = ParseElement(document.RootElement);
            WorkerProtocolValidation.Require(
                payload.SequenceEqual(handoff.ToCanonicalUtf8Json()),
                nameof(payload),
                "must use the exact canonical schema-v2 serialization");
            return handoff;
        }
        catch (WorkerProtocolException)
        {
            throw;
        }
        catch (Exception error) when (
            error is JsonException or InvalidOperationException or
            FormatException or OverflowException)
        {
            throw new WorkerProtocolException(
                "Model Inspection schema-v2 handoff is malformed.");
        }
    }

    internal static ModelInspectionHandoffV2 ParseElement(JsonElement root)
    {
        RequireProperties(
            root,
            "schemaVersion",
            "modelInspectionHandoffId",
            "modelInspectionRunId",
            "outcome",
            "modelSha256",
            "modelLengthBytes");
        var handoff = new ModelInspectionHandoffV2(
            root.GetProperty("schemaVersion").GetUInt16(),
            RequiredUuid(root, "modelInspectionHandoffId"),
            RequiredUuid(root, "modelInspectionRunId"),
            ParseOutcome(RequiredString(root, "outcome")),
            RequiredString(root, "modelSha256"),
            root.GetProperty("modelLengthBytes").GetInt64());
        handoff.Validate();
        return handoff;
    }

    internal void WriteCanonicalJson(Utf8JsonWriter writer)
    {
        Validate();
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteString(
            "modelInspectionHandoffId",
            ModelInspectionHandoffId.ToString("D"));
        writer.WriteString(
            "modelInspectionRunId",
            ModelInspectionRunId.ToString("D"));
        writer.WriteString("outcome", Outcome.ToString());
        writer.WriteString("modelSha256", ModelSha256);
        writer.WriteNumber("modelLengthBytes", ModelLengthBytes);
        writer.WriteEndObject();
    }

    private static void RequireIdentity(string digest, long length)
    {
        WorkerProtocolValidation.Require(
            digest is { Length: 64 } && digest.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            nameof(ModelSha256),
            "must contain a lowercase SHA-256 digest");
        WorkerProtocolValidation.Require(
            length > 0,
            nameof(ModelLengthBytes),
            "must be positive");
    }

    private static void RequireUuidV4(Guid value, string name)
    {
        string text = value.ToString("D");
        WorkerProtocolValidation.Require(
            value != Guid.Empty && text[14] == '4' && text[19] is '8' or '9' or 'a' or 'b',
            name,
            "must be a non-empty UUIDv4");
    }

    private static void RequireProperties(JsonElement element, params string[] expected)
    {
        WorkerProtocolValidation.Require(
            element.ValueKind == JsonValueKind.Object &&
            element.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(expected, StringComparer.Ordinal),
            nameof(element),
            "must contain only the exact ordered schema-v2 fields");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        WorkerProtocolValidation.Require(
            property.ValueKind == JsonValueKind.String && property.GetString() is not null,
            propertyName,
            "must be a string");
        return property.GetString()!;
    }

    private static Guid RequiredUuid(JsonElement element, string propertyName)
    {
        string text = RequiredString(element, propertyName);
        WorkerProtocolValidation.Require(
            Guid.TryParseExact(text, "D", out Guid value) &&
            string.Equals(text, value.ToString("D"), StringComparison.Ordinal),
            propertyName,
            "must be a lowercase canonical UUID");
        return value;
    }

    private static ModelInspectionOutcomeV2 ParseOutcome(string value) => value switch
    {
        "Ready" => ModelInspectionOutcomeV2.Ready,
        "ReadyWithWarnings" => ModelInspectionOutcomeV2.ReadyWithWarnings,
        _ => throw new WorkerProtocolException(
            "outcome must be Ready or ReadyWithWarnings.")
    };
}

/// <summary>
/// The single path-private schema-v2 projection emitted by route-specific
/// GGUF and OpenVINO inspection adapters.
/// </summary>
public sealed record ModelInspectionProjectionV2(
    ushort SchemaVersion,
    ModelSourceV2 ModelSource,
    ModelInspectionResultV2 ModelInspectionResult,
    ModelInspectionHandoffV2 ModelInspectionHandoff)
{
    public const ushort RequiredSchemaVersion = 2;
    public const int MaximumCanonicalUtf8Bytes = 1024;

    public static ModelInspectionProjectionV2 Create(
        ModelInspectionRoute route,
        string modelType,
        Guid modelInspectionRunId,
        Guid modelInspectionHandoffId,
        ModelInspectionOutcomeV2 outcome,
        string modelSha256,
        long modelLengthBytes)
    {
        var projection = new ModelInspectionProjectionV2(
            RequiredSchemaVersion,
            new ModelSourceV2(
                modelType,
                route,
                modelSha256,
                modelLengthBytes),
            new ModelInspectionResultV2(
                modelInspectionRunId,
                outcome,
                route,
                modelSha256,
                modelLengthBytes),
            new ModelInspectionHandoffV2(
                RequiredSchemaVersion,
                modelInspectionHandoffId,
                modelInspectionRunId,
                outcome,
                modelSha256,
                modelLengthBytes));
        projection.Validate();
        return projection;
    }

    public void Validate()
    {
        WorkerProtocolValidation.Require(
            SchemaVersion == RequiredSchemaVersion,
            nameof(SchemaVersion),
            "must equal 2");
        ModelSourceV2 source = WorkerProtocolValidation.RequireNotNull(
            ModelSource,
            nameof(ModelSource));
        ModelInspectionResultV2 result = WorkerProtocolValidation.RequireNotNull(
            ModelInspectionResult,
            nameof(ModelInspectionResult));
        ModelInspectionHandoffV2 handoff = WorkerProtocolValidation.RequireNotNull(
            ModelInspectionHandoff,
            nameof(ModelInspectionHandoff));

        WorkerProtocolValidation.RequireDefinedEnum(source.Route, nameof(source.Route));
        WorkerProtocolValidation.RequireDefinedEnum(result.Route, nameof(result.Route));
        WorkerProtocolValidation.RequireDefinedEnum(result.Outcome, nameof(result.Outcome));
        handoff.Validate();
        string expectedModelType = source.Route switch
        {
            ModelInspectionRoute.Gguf => "gguf",
            ModelInspectionRoute.OpenVino => "openvino-ir",
            _ => string.Empty
        };
        WorkerProtocolValidation.Require(
            string.Equals(source.ModelType, expectedModelType, StringComparison.Ordinal),
            nameof(source.ModelType),
            "must match the selected inspection route");
        RequireIdentity(source.ModelSha256, source.ModelLengthBytes, nameof(ModelSource));
        RequireIdentity(result.ModelSha256, result.ModelLengthBytes, nameof(ModelInspectionResult));
        RequireUuidV4(result.ModelInspectionRunId, nameof(result.ModelInspectionRunId));
        WorkerProtocolValidation.Require(
            source.Route == result.Route,
            nameof(result.Route),
            "must match modelSource.route");
        WorkerProtocolValidation.Require(
            result.ModelInspectionRunId == handoff.ModelInspectionRunId,
            nameof(handoff.ModelInspectionRunId),
            "must match modelInspectionResult.modelInspectionRunId");
        WorkerProtocolValidation.Require(
            result.Outcome == handoff.Outcome,
            nameof(handoff.Outcome),
            "must match modelInspectionResult.outcome");
        WorkerProtocolValidation.Require(
            string.Equals(source.ModelSha256, result.ModelSha256, StringComparison.Ordinal) &&
            string.Equals(source.ModelSha256, handoff.ModelSha256, StringComparison.Ordinal) &&
            source.ModelLengthBytes == result.ModelLengthBytes &&
            source.ModelLengthBytes == handoff.ModelLengthBytes,
            nameof(ModelSource),
            "must bind the exact result and handoff model identity");
    }

    public byte[] ToCanonicalUtf8Json()
    {
        Validate();
        var buffer = new ArrayBufferWriter<byte>(MaximumCanonicalUtf8Bytes);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WritePropertyName("modelSource");
            writer.WriteStartObject();
            writer.WriteString("modelType", ModelSource.ModelType);
            writer.WriteString("route", RouteText(ModelSource.Route));
            writer.WriteString("modelSha256", ModelSource.ModelSha256);
            writer.WriteNumber("modelLengthBytes", ModelSource.ModelLengthBytes);
            writer.WriteEndObject();
            writer.WritePropertyName("modelInspectionResult");
            writer.WriteStartObject();
            writer.WriteString(
                "modelInspectionRunId",
                ModelInspectionResult.ModelInspectionRunId.ToString("D"));
            writer.WriteString("outcome", ModelInspectionResult.Outcome.ToString());
            writer.WriteString("route", RouteText(ModelInspectionResult.Route));
            writer.WriteString("modelSha256", ModelInspectionResult.ModelSha256);
            writer.WriteNumber(
                "modelLengthBytes",
                ModelInspectionResult.ModelLengthBytes);
            writer.WriteEndObject();
            writer.WritePropertyName("modelInspectionHandoff");
            ModelInspectionHandoff.WriteCanonicalJson(writer);
            writer.WriteEndObject();
        }

        WorkerProtocolValidation.Require(
            buffer.WrittenCount <= MaximumCanonicalUtf8Bytes,
            nameof(ModelInspectionProjectionV2),
            "must stay within the canonical UTF-8 size bound");
        return buffer.WrittenSpan.ToArray();
    }

    public static ModelInspectionProjectionV2 Parse(ReadOnlySpan<byte> payload)
    {
        WorkerProtocolValidation.Require(
            !payload.IsEmpty && payload.Length <= MaximumCanonicalUtf8Bytes,
            nameof(payload),
            "must be present and within the canonical UTF-8 size bound");
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                payload.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4
                });
            JsonElement root = document.RootElement;
            RequireProperties(
                root,
                "schemaVersion",
                "modelSource",
                "modelInspectionResult",
                "modelInspectionHandoff");
            JsonElement source = root.GetProperty("modelSource");
            JsonElement result = root.GetProperty("modelInspectionResult");
            JsonElement handoff = root.GetProperty("modelInspectionHandoff");
            RequireProperties(source, "modelType", "route", "modelSha256", "modelLengthBytes");
            RequireProperties(
                result,
                "modelInspectionRunId",
                "outcome",
                "route",
                "modelSha256",
                "modelLengthBytes");

            var projection = new ModelInspectionProjectionV2(
                root.GetProperty("schemaVersion").GetUInt16(),
                new ModelSourceV2(
                    RequiredString(source, "modelType"),
                    ParseRoute(RequiredString(source, "route")),
                    RequiredString(source, "modelSha256"),
                    source.GetProperty("modelLengthBytes").GetInt64()),
                new ModelInspectionResultV2(
                    RequiredUuid(result, "modelInspectionRunId"),
                    ParseOutcome(RequiredString(result, "outcome")),
                    ParseRoute(RequiredString(result, "route")),
                    RequiredString(result, "modelSha256"),
                    result.GetProperty("modelLengthBytes").GetInt64()),
                ModelInspectionHandoffV2.ParseElement(handoff));
            projection.Validate();
            WorkerProtocolValidation.Require(
                payload.SequenceEqual(projection.ToCanonicalUtf8Json()),
                nameof(payload),
                "must use the exact canonical schema-v2 serialization");
            return projection;
        }
        catch (WorkerProtocolException)
        {
            throw;
        }
        catch (Exception error) when (
            error is JsonException or InvalidOperationException or
            FormatException or OverflowException)
        {
            throw new WorkerProtocolException(
                "Model Inspection schema-v2 projection is malformed.");
        }
    }

    private static void RequireIdentity(string digest, long length, string name)
    {
        WorkerProtocolValidation.Require(
            digest is { Length: 64 } && digest.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            name,
            "must contain a lowercase SHA-256 digest");
        WorkerProtocolValidation.Require(length > 0, name, "must contain a positive model length");
    }

    private static void RequireUuidV4(Guid value, string name)
    {
        string text = value.ToString("D");
        WorkerProtocolValidation.Require(
            value != Guid.Empty && text[14] == '4' && text[19] is '8' or '9' or 'a' or 'b',
            name,
            "must be a non-empty UUIDv4");
    }

    private static void RequireProperties(JsonElement element, params string[] expected)
    {
        WorkerProtocolValidation.Require(
            element.ValueKind == JsonValueKind.Object &&
            element.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(expected, StringComparer.Ordinal),
            nameof(element),
            "must contain only the exact ordered schema-v2 fields");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        WorkerProtocolValidation.Require(
            property.ValueKind == JsonValueKind.String && property.GetString() is not null,
            propertyName,
            "must be a string");
        return property.GetString()!;
    }

    private static Guid RequiredUuid(JsonElement element, string propertyName)
    {
        string text = RequiredString(element, propertyName);
        WorkerProtocolValidation.Require(
            Guid.TryParseExact(text, "D", out Guid value) &&
            string.Equals(text, value.ToString("D"), StringComparison.Ordinal),
            propertyName,
            "must be a lowercase canonical UUID");
        return value;
    }

    private static ModelInspectionRoute ParseRoute(string value) => value switch
    {
        "gguf" => ModelInspectionRoute.Gguf,
        "openvino" => ModelInspectionRoute.OpenVino,
        _ => throw new WorkerProtocolException("route must be gguf or openvino.")
    };

    private static ModelInspectionOutcomeV2 ParseOutcome(string value) => value switch
    {
        "Ready" => ModelInspectionOutcomeV2.Ready,
        "ReadyWithWarnings" => ModelInspectionOutcomeV2.ReadyWithWarnings,
        _ => throw new WorkerProtocolException(
            "outcome must be Ready or ReadyWithWarnings.")
    };

    private static string RouteText(ModelInspectionRoute route) => route switch
    {
        ModelInspectionRoute.Gguf => "gguf",
        ModelInspectionRoute.OpenVino => "openvino",
        _ => throw new WorkerProtocolException("route must be gguf or openvino.")
    };
}
