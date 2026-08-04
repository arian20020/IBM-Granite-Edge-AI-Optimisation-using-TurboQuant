using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Creates privacy-safe evidence for the embedded GGUF chat template without
/// storing the full template text.
/// </summary>
public static class ChatTemplateEvidenceFactory
{
    /// <summary>
    /// Records presence, exact character length and SHA-256 when the metadata
    /// key exists. Empty and whitespace-only values remain present evidence.
    /// </summary>
    public static ChatTemplateEvidence Create(
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!metadata.TryGetValue(
                "tokenizer.chat_template",
                out string? template))
        {
            return new ChatTemplateEvidence
            {
                Present = false
            };
        }

        byte[] bytes = Encoding.UTF8.GetBytes(template);

        return new ChatTemplateEvidence
        {
            Present = true,
            LengthCharacters = template.Length,
            Sha256 = Convert
                .ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant()
        };
    }
}
