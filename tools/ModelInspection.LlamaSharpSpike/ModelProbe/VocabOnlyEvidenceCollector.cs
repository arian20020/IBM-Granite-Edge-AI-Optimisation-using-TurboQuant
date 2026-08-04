using System.Security.Cryptography;
using System.Text;
using LLama;
using LLama.Native;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Converts an active LLamaSharp model handle into project-owned lightweight
/// evidence. No LLamaSharp type escapes this class.
/// </summary>
internal static class VocabOnlyEvidenceCollector
{
    /// <summary>
    /// Collects metadata, vocabulary, tokenizer and chat-template evidence while
    /// the native handle remains valid.
    /// </summary>
    internal static VocabOnlyRuntimeModelEvidence Collect(
        LLamaWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        SafeLlamaModelHandle handle = weights.NativeHandle;
        SafeLlamaModelHandle.Vocabulary vocabulary = weights.Vocab;
        IReadOnlyDictionary<string, string> metadata = weights.Metadata;

        string? chatTemplate = handle.GetTemplate(
            name: null,
            strict: false);

        return new VocabOnlyRuntimeModelEvidence
        {
            Description = handle.Description,
            MetadataCount = handle.MetadataCount,
            MetadataKeys = metadata.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray(),
            Architecture = GetMetadata(metadata, "general.architecture"),
            ModelName = GetMetadata(metadata, "general.name"),
            FileType = GetMetadata(metadata, "general.file_type"),
            QuantizationVersion = GetMetadata(
                metadata,
                "general.quantization_version"),
            TokenizerModel = GetMetadata(
                metadata,
                "tokenizer.ggml.model"),
            ContextSize = weights.ContextSize,
            RuntimeReportedSizeBytes = weights.SizeInBytes,
            ParameterCount = weights.ParameterCount,
            EmbeddingSize = weights.EmbeddingSize,
            LayerCount = handle.LayerCount,
            HeadCount = handle.HeadCount,
            KvHeadCount = handle.KVHeadCount,
            HasEncoder = handle.HasEncoder,
            HasDecoder = handle.HasDecoder,
            IsRecurrent = handle.IsRecurrent,
            IsDiffusion = handle.IsDiffusion,
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
