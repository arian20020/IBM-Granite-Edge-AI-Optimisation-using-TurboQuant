using System.Globalization;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Projects structural model facts from GGUF metadata that remains available
/// when llama.cpp is opened with <c>vocab_only</c>.
/// </summary>
public sealed record VocabOnlyMetadataProjection
{
    public string? Architecture { get; init; }

    public string? ModelName { get; init; }

    public string? FileType { get; init; }

    public string? QuantizationVersion { get; init; }

    public string? TokenizerModel { get; init; }

    public int? ContextSize { get; init; }

    public ulong? ParameterCount { get; init; }

    public int? EmbeddingSize { get; init; }

    public int? LayerCount { get; init; }

    public int? HeadCount { get; init; }

    public int? KvHeadCount { get; init; }

    /// <summary>
    /// Creates a projection without calling native hyperparameter accessors.
    /// Empty and whitespace-only metadata is treated as unavailable.
    /// </summary>
    public static VocabOnlyMetadataProjection Create(
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        string? architecture =
            GetValue(metadata, "general.architecture");

        string? architecturePrefix = architecture is null
            ? null
            : architecture + ".";

        return new VocabOnlyMetadataProjection
        {
            Architecture = architecture,
            ModelName = GetValue(metadata, "general.name"),
            FileType = GetValue(metadata, "general.file_type"),
            QuantizationVersion = GetValue(
                metadata,
                "general.quantization_version"),
            TokenizerModel = GetValue(
                metadata,
                "tokenizer.ggml.model"),
            ContextSize = TryReadNonNegativeInt32(
                metadata,
                BuildArchitectureKey(
                    architecturePrefix,
                    "context_length")),
            ParameterCount = TryReadUInt64(
                metadata,
                "general.parameter_count"),
            EmbeddingSize = TryReadNonNegativeInt32(
                metadata,
                BuildArchitectureKey(
                    architecturePrefix,
                    "embedding_length")),
            LayerCount = TryReadNonNegativeInt32(
                metadata,
                BuildArchitectureKey(
                    architecturePrefix,
                    "block_count")),
            HeadCount = TryReadNonNegativeInt32(
                metadata,
                BuildArchitectureKey(
                    architecturePrefix,
                    "attention.head_count")),
            KvHeadCount = TryReadNonNegativeInt32(
                metadata,
                BuildArchitectureKey(
                    architecturePrefix,
                    "attention.head_count_kv"))
        };
    }

    private static string? BuildArchitectureKey(
        string? architecturePrefix,
        string suffix)
    {
        return architecturePrefix is null
            ? null
            : architecturePrefix + suffix;
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, string> metadata,
        string? key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            !metadata.TryGetValue(key, out string? value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    private static int? TryReadNonNegativeInt32(
        IReadOnlyDictionary<string, string> metadata,
        string? key)
    {
        string? value = GetValue(metadata, key);

        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsedValue) ||
            parsedValue < 0)
        {
            return null;
        }

        return parsedValue;
    }

    private static ulong? TryReadUInt64(
        IReadOnlyDictionary<string, string> metadata,
        string key)
    {
        string? value = GetValue(metadata, key);

        return ulong.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out ulong parsedValue)
                ? parsedValue
                : null;
    }
}
