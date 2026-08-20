using System.Text.Json;
using System.Text.Json.Serialization;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

internal sealed record ControlledGgufRuntimeConfiguration(
    string PackageRoot,
    string ManifestSha256,
    string ModelFile,
    string ModelSha256,
    GgufRuntimeConfiguration RuntimeConfiguration)
{
    private const int MaximumConfigurationBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = CreateOptions();

    internal static ControlledGgufRuntimeConfiguration Load(string path)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            byte[] json = File.ReadAllBytes(path);
            if (json.Length == 0 || json.Length > MaximumConfigurationBytes)
            {
                throw new InvalidDataException("Controlled runtime configuration size is invalid.");
            }

            Payload payload = JsonSerializer.Deserialize<Payload>(json, Options)
                ?? throw new InvalidDataException("Controlled runtime configuration is empty.");
            if (payload.SchemaVersion != 1 ||
                !Path.IsPathFullyQualified(payload.PackageRoot) ||
                !Path.IsPathFullyQualified(payload.ModelFile) ||
                !IsSha256(payload.ManifestSha256) ||
                !IsSha256(payload.ModelSha256))
            {
                throw new InvalidDataException("Controlled runtime configuration is invalid.");
            }

            var runtime = new GgufRuntimeConfiguration(
                payload.ModelId,
                payload.ModelSha256,
                payload.RuntimeBuildId,
                payload.RuntimeSourceCommit,
                payload.Backend,
                payload.DeviceId,
                payload.ContextSize,
                payload.KeyCacheType,
                payload.ValueCacheType,
                payload.GpuLayerCount,
                payload.FlashAttention,
                payload.ThreadCount,
                payload.BatchSize,
                payload.EvidenceGrade,
                payload.ProfileId);
            return new ControlledGgufRuntimeConfiguration(
                Path.GetFullPath(payload.PackageRoot),
                payload.ManifestSha256.ToUpperInvariant(),
                Path.GetFullPath(payload.ModelFile),
                payload.ModelSha256.ToUpperInvariant(),
                runtime);
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException(
                "Controlled runtime configuration is invalid.");
        }
    }

    private static bool IsSha256(string value) =>
        value?.Length == 64 && value.All(Uri.IsHexDigit);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }

    private sealed record Payload(
        int SchemaVersion,
        string PackageRoot,
        string ManifestSha256,
        string ModelFile,
        string ModelSha256,
        string ModelId,
        string RuntimeBuildId,
        string RuntimeSourceCommit,
        GgufRuntimeBackend Backend,
        string DeviceId,
        int ContextSize,
        GgufCacheType KeyCacheType,
        GgufCacheType ValueCacheType,
        int GpuLayerCount,
        bool FlashAttention,
        int ThreadCount,
        int BatchSize,
        string EvidenceGrade,
        string ProfileId);
}
