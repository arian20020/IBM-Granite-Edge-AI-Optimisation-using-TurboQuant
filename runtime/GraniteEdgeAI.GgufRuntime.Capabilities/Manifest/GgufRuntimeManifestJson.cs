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
