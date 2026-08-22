using System.Text;
using LLama;
using LLama.Native;

namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

internal sealed record VocabOnlyConfigurationProjection
{
    internal string? Description { get; init; }
    internal int MetadataCount { get; init; }
    internal IReadOnlyList<string> MetadataKeys { get; init; } = [];
    internal string? Architecture { get; init; }
    internal string? ModelName { get; init; }
    internal string? FileType { get; init; }
    internal string? QuantizationVersion { get; init; }
    internal string? TokenizerModel { get; init; }
    internal int? ContextSize { get; init; }
}

internal sealed record VocabOnlyTokenizerProjection
{
    internal VocabularyEvidence Vocabulary { get; init; } = new();
    internal TokenizerSmokeEvidence TokenizerSmoke { get; init; } = new();
    internal ChatTemplateEvidence ChatTemplate { get; init; } = new();
}

internal sealed record VocabOnlyStructureProjection
{
    internal ulong? ParameterCount { get; init; }
    internal int? EmbeddingSize { get; init; }
    internal int? LayerCount { get; init; }
    internal int? HeadCount { get; init; }
    internal int? KvHeadCount { get; init; }
}

/// <summary>
/// Converts an active LLamaSharp VocabOnly handle into project-owned
/// lightweight evidence. No LLamaSharp type escapes this class.
/// </summary>
internal static class VocabOnlyEvidenceCollector
{
    internal static VocabOnlyConfigurationProjection CollectConfiguration(
        LLamaWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        IReadOnlyDictionary<string, string> metadata = weights.Metadata;
        VocabOnlyMetadataProjection projection =
            VocabOnlyMetadataProjection.Create(metadata);

        return new VocabOnlyConfigurationProjection
        {
            Description =
                projection.ModelName ?? projection.Architecture,
            MetadataCount = metadata.Count,
            MetadataKeys = metadata.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray(),
            Architecture = projection.Architecture,
            ModelName = projection.ModelName,
            FileType = projection.FileType,
            QuantizationVersion = projection.QuantizationVersion,
            TokenizerModel = projection.TokenizerModel,
            ContextSize = projection.ContextSize
        };
    }

    internal static VocabOnlyTokenizerProjection CollectTokenizerAndChat(
        LLamaWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        SafeLlamaModelHandle.Vocabulary vocabulary = weights.Vocab;
        IReadOnlyDictionary<string, string> metadata = weights.Metadata;

        return new VocabOnlyTokenizerProjection
        {
            Vocabulary = new VocabularyEvidence
            {
                Count = vocabulary.Count,
                Type = vocabulary.Type.ToString(),
                Bos = CreateSpecialTokenEvidence(
                    vocabulary,
                    vocabulary.BOS),
                Eos = CreateSpecialTokenEvidence(
                    vocabulary,
                    vocabulary.EOS),
                Newline = CreateSpecialTokenEvidence(
                    vocabulary,
                    vocabulary.Newline),
                Pad = CreateSpecialTokenEvidence(
                    vocabulary,
                    vocabulary.Pad),
                Mask = CreateSpecialTokenEvidence(
                    vocabulary,
                    vocabulary.Mask),
                Separator = CreateSpecialTokenEvidence(
                    vocabulary,
                    vocabulary.SEP)
            },
            TokenizerSmoke = RunTokenizerSmoke(weights),
            ChatTemplate = ChatTemplateEvidenceFactory.Create(metadata)
        };
    }

    internal static VocabOnlyStructureProjection CollectStructure(
        LLamaWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        IReadOnlyDictionary<string, string> metadata = weights.Metadata;
        VocabOnlyMetadataProjection projection =
            VocabOnlyMetadataProjection.Create(metadata);

        return new VocabOnlyStructureProjection
        {
            ParameterCount = projection.ParameterCount,
            EmbeddingSize = projection.EmbeddingSize,
            LayerCount = projection.LayerCount,
            HeadCount = projection.HeadCount,
            KvHeadCount = projection.KvHeadCount
        };
    }

    internal static VocabOnlyRuntimeModelEvidence Compose(
        VocabOnlyConfigurationProjection configuration,
        VocabOnlyTokenizerProjection tokenizer,
        VocabOnlyStructureProjection structure)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(tokenizer);
        ArgumentNullException.ThrowIfNull(structure);

        return new VocabOnlyRuntimeModelEvidence
        {
            Description = configuration.Description,
            MetadataCount = configuration.MetadataCount,
            MetadataKeys = configuration.MetadataKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray(),
            Architecture = configuration.Architecture,
            ModelName = configuration.ModelName,
            FileType = configuration.FileType,
            QuantizationVersion = configuration.QuantizationVersion,
            TokenizerModel = configuration.TokenizerModel,
            ContextSize = configuration.ContextSize,
            RuntimeReportedSizeBytes = null,
            ParameterCount = structure.ParameterCount,
            EmbeddingSize = structure.EmbeddingSize,
            LayerCount = structure.LayerCount,
            HeadCount = structure.HeadCount,
            KvHeadCount = structure.KvHeadCount,
            HasEncoder = null,
            HasDecoder = null,
            IsRecurrent = null,
            IsDiffusion = null,
            Vocabulary = tokenizer.Vocabulary,
            TokenizerSmoke = tokenizer.TokenizerSmoke,
            ChatTemplate = tokenizer.ChatTemplate
        };
    }

    private static SpecialTokenEvidence CreateSpecialTokenEvidence(
        SafeLlamaModelHandle.Vocabulary vocabulary,
        LLamaToken? token)
    {
        string? decodedText = null;

        if (token.HasValue)
        {
            try
            {
                decodedText = vocabulary.LLamaTokenToString(
                    token,
                    isSpecialToken: true);
            }
            catch
            {
                // The numeric identifier remains useful evidence even if the
                // optional human-readable token text cannot be decoded.
            }
        }

        return new SpecialTokenEvidence
        {
            TokenId = token?.ToString(),
            DecodedText = decodedText
        };
    }

    private static TokenizerSmokeEvidence RunTokenizerSmoke(
        LLamaWeights weights)
    {
        try
        {
            LLamaToken[] tokens = weights.Tokenize(
                TokenizerSmokeEvidence.InputText,
                add_bos: false,
                special: false,
                encoding: Encoding.UTF8);

            return new TokenizerSmokeEvidence
            {
                Succeeded = true,
                TokenCount = tokens.Length
            };
        }
        catch (Exception exception)
        {
            return new TokenizerSmokeEvidence
            {
                Succeeded = false,
                FailureType = exception.GetType().FullName,
                FailureMessage = exception.Message
            };
        }
    }
}
