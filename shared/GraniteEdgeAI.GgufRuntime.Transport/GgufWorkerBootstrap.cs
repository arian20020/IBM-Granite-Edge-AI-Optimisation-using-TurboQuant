using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.GgufRuntime.Transport;

public sealed record GgufWorkerBootstrap
{
    private const int MaximumArgumentCount = 32;
    private const int MaximumArgumentCharacters = 4096;

    [JsonConstructor]
    public GgufWorkerBootstrap(
        string cliExecutable,
        string modelFile,
        IReadOnlyList<string> cliArgumentsOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelFile);
        ArgumentNullException.ThrowIfNull(cliArgumentsOverride);
        if (!Path.IsPathFullyQualified(cliExecutable) ||
            !Path.IsPathFullyQualified(modelFile) ||
            cliExecutable.Length >= short.MaxValue ||
            modelFile.Length >= short.MaxValue ||
            cliArgumentsOverride.Count > MaximumArgumentCount ||
            cliArgumentsOverride.Any(value =>
                value is null ||
                value.Length > MaximumArgumentCharacters ||
                value.Contains('\0')))
        {
            throw new ArgumentException("The worker bootstrap is invalid.");
        }

        CliExecutable = cliExecutable;
        ModelFile = modelFile;
        CliArgumentsOverride = cliArgumentsOverride.ToArray();
    }

    public string CliExecutable { get; }

    public string ModelFile { get; }

    public IReadOnlyList<string> CliArgumentsOverride { get; }
}

public static class GgufWorkerBootstrapCodec
{
    private const int MaximumBootstrapBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static byte[] Serialize(GgufWorkerBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        return JsonSerializer.SerializeToUtf8Bytes(bootstrap, Options);
    }

    public static GgufWorkerBootstrap Deserialize(ReadOnlySpan<byte> payload)
    {
        try
        {
            if (payload.IsEmpty || payload.Length > MaximumBootstrapBytes)
            {
                throw new GgufTransportException("The worker bootstrap is invalid.");
            }

            ValidateNoDuplicateProperties(payload);
            return JsonSerializer.Deserialize<GgufWorkerBootstrap>(payload, Options)
                ?? throw new GgufTransportException("The worker bootstrap is empty.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new GgufTransportException("The worker bootstrap is invalid.");
        }
    }

    private static void ValidateNoDuplicateProperties(ReadOnlySpan<byte> payload)
    {
        using JsonDocument document = JsonDocument.Parse(
            payload.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
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
                    throw new GgufTransportException(
                        "The worker bootstrap contains a duplicate property.");
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
}
