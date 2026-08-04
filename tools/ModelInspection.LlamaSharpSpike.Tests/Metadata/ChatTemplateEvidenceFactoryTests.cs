using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies privacy-safe evidence for embedded GGUF chat templates.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ChatTemplateEvidenceFactoryTests
{
    [TestMethod]
    public void Create_WhenKeyIsAbsent_ReturnsNotPresent()
    {
        ChatTemplateEvidence result = ChatTemplateEvidenceFactory.Create(
            new Dictionary<string, string>());

        Assert.IsFalse(result.Present);
        Assert.IsNull(result.LengthCharacters);
        Assert.IsNull(result.Sha256);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("{% for message in messages %}{{ message.content }}{% endfor %}")]
    [DataRow("{{ 'こんにちは' }}")]
    public void Create_WhenKeyExists_RecordsExactLengthAndStableHash(
        string template)
    {
        ChatTemplateEvidence result = ChatTemplateEvidenceFactory.Create(
            new Dictionary<string, string>
            {
                ["tokenizer.chat_template"] = template
            });

        Assert.IsTrue(result.Present);
        Assert.AreEqual(template.Length, result.LengthCharacters);
        Assert.AreEqual(
            Convert
                .ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(template)))
                .ToLowerInvariant(),
            result.Sha256);
    }

    [TestMethod]
    public void Create_WithSameTemplate_ReturnsStableHash()
    {
        var metadata = new Dictionary<string, string>
        {
            ["tokenizer.chat_template"] = "{{ messages }}"
        };

        ChatTemplateEvidence first =
            ChatTemplateEvidenceFactory.Create(metadata);
        ChatTemplateEvidence second =
            ChatTemplateEvidenceFactory.Create(metadata);

        Assert.AreEqual(first.Sha256, second.Sha256);
        Assert.AreEqual(first.LengthCharacters, second.LengthCharacters);
    }

    [TestMethod]
    public void Create_WithNullMetadata_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ChatTemplateEvidenceFactory.Create(null!));
    }
}
