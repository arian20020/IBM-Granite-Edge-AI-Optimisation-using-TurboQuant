using System.Security.Cryptography;
using System.Text;
using LLama;
using LLama.Native;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Converts an active LLamaSharp VocabOnly handle into project-owned
/// lightweight evidence. No LLamaSharp type escapes this class.
/// </summary>
internal static class VocabOnlyEvidenceCollector
{
    /// <summary>
    /// Collects only evidence that remains valid when llama.cpp returns before
    /// loading native hyperparameters.
    /// </summary>
    internal static VocabOnlyRuntimeModelEvidence Collect(
        LLamaWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        SafeLlamaModelHandle.Vocabulary vocabulary = weights.Vocab;
        IReadOnlyDictionary<string, string> metadata = weights.Metadata;
        VocabOnlyMetadataProjection projection =
            VocabOnlyMetadataProjection.Create(metadata);

        // The embedded default template is ordinary GGUF metadata. Reading the
        // metadata value directly avoids asking a partially initialised native
        // model object to derive additional state in VocabOnly mode.
        string? chatTemplate = GetMetadata(
            metadata,
            "tokenizer.chat_template");

        return new VocabOnlyRuntimeModelEvidence
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
            ContextSize = projection.ContextSize,

            // llama.cpp deliberately does not populate model hyperparameters or
            // tensor-derived values when vocab_only is enabled. The original
            // file size is already recorded by ModelFileSnapshot.
            RuntimeReportedSizeBytes = null,
            ParameterCount = projection.ParameterCount,
            EmbeddingSize = projection.EmbeddingSize,
            LayerCount = projection.LayerCount,
            HeadCount = projection.HeadCount,
            KvHeadCount = projection.KvHeadCount,
            HasEncoder = null,
            HasDecoder = null,
            IsRecurrent = null,
            IsDiffusion = null,
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
            ChatTemplate = CreateChatTemplateEvidence(chatTemplate)
        };
    }

    private static string? GetMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key)
    {
        return metadata.TryGetValue(key, out string? value)
            ? value
            : null;
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

    private static ChatTemplateEvidence CreateChatTemplateEvidence(
        string? chatTemplate)
    {
        if (chatTemplate is null)
        {
            return new ChatTemplateEvidence
            {
                Present = false
            };
        }

        byte[] templateBytes = Encoding.UTF8.GetBytes(chatTemplate);
        byte[] hash = SHA256.HashData(templateBytes);

        return new ChatTemplateEvidence
        {
            Present = true,
            LengthCharacters = chatTemplate.Length,
            Sha256 = Convert
                .ToHexString(hash)
                .ToLowerInvariant()
        };
    }
}
