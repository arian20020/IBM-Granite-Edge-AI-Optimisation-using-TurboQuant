using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public static class GgufRuntimeManifestJson
{
    public const int MaximumManifestBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static GgufRuntimeManifest Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty || utf8Json.Length > MaximumManifestBytes)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-size-invalid");
        }

        try
        {
            ValidateNoDuplicateProperties(utf8Json);
            return JsonSerializer.Deserialize<GgufRuntimeManifest>(utf8Json, Options)
                ?? throw new GgufRuntimeTrustException("runtime-manifest-invalid");
        }
        catch (JsonException)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-json-invalid");
        }
        catch (NotSupportedException)
        {
            throw new GgufRuntimeTrustException("runtime-manifest-json-invalid");
        }
    }

    private static void ValidateNoDuplicateProperties(ReadOnlySpan<byte> utf8Json)
    {
        using JsonDocument document = JsonDocument.Parse(
            utf8Json.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });
        ValidateNoDuplicateProperties(document.RootElement);
    }

    private static void ValidateNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("A duplicate manifest property was rejected.");
                }

                ValidateNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateNoDuplicateProperties(item);
            }
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            MaxDepth = 16,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }
}
