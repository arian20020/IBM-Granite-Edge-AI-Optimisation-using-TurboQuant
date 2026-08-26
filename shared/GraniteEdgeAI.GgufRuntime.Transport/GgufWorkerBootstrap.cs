using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.GgufRuntime.Transport;

public sealed record GgufWorkerBootstrap
{
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
            cliArgumentsOverride.Any(value => value.Contains('\0')))
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
            return JsonSerializer.Deserialize<GgufWorkerBootstrap>(payload, Options)
                ?? throw new GgufTransportException("The worker bootstrap is empty.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new GgufTransportException("The worker bootstrap is invalid.");
        }
    }
}
