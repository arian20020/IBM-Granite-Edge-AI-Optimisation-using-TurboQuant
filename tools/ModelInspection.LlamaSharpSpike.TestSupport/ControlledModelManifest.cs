using System.Text.Json;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Records the exact identity and expected VocabOnly evidence for one trusted
/// model that remains outside Git.
/// </summary>
public sealed record ControlledModelManifest
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required long LengthBytes { get; init; }
    public required string Architecture { get; init; }
    public required string ModelName { get; init; }
    public required string FileType { get; init; }
    public required string QuantizationVersion { get; init; }
    public required string TokenizerModel { get; init; }
    public required int ContextSize { get; init; }
    public required int EmbeddingSize { get; init; }
    public required int LayerCount { get; init; }
    public required int HeadCount { get; init; }
    public required int KvHeadCount { get; init; }
    public required int MetadataCount { get; init; }
    public required int VocabularyCount { get; init; }
    public required bool ChatTemplatePresent { get; init; }
    public required int TokenizerSmokeTokenCount { get; init; }

    public static ControlledModelManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Controlled model manifest was not found.",
                path);
        }

        ControlledModelManifest manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<ControlledModelManifest>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidDataException(
                    "Controlled model manifest could not be deserialized.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Controlled model manifest is invalid JSON.",
                exception);
        }

        manifest.Validate();
        return manifest;
    }

    private void Validate()
    {
        string[] requiredStrings =
        {
            Id,
            FileName,
            Sha256,
            Architecture,
            ModelName,
            FileType,
            QuantizationVersion,
            TokenizerModel
        };

        if (requiredStrings.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException(
                "Controlled model manifest contains a blank required value.");
        }

        if (Sha256.Length != 64 ||
            Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                "Controlled model SHA-256 must contain 64 hexadecimal characters.");
        }

        if (LengthBytes <= 0 ||
            ContextSize < 0 ||
            EmbeddingSize < 0 ||
            LayerCount < 0 ||
            HeadCount < 0 ||
            KvHeadCount < 0 ||
            MetadataCount < 0 ||
            VocabularyCount < 0 ||
            TokenizerSmokeTokenCount < 0)
        {
            throw new InvalidDataException(
                "Controlled model numeric values must be nonnegative and length must be positive.");
        }
    }
}
