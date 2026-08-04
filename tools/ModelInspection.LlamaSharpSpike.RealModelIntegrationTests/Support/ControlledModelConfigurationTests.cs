using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;

/// <summary>
/// Verifies trusted-model preconditions fail before a native process is started.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class ControlledModelConfigurationTests
{
    [TestMethod]
    public void Load_WithoutConfiguredPath_FailsLoudly()
    {
        using var fixture = new ConfigurationFixture("model.gguf", "model");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => ControlledModelConfiguration.Load(
                fixture.ManifestPath,
                configuredModelPath: null));
    }

    [TestMethod]
    public void Load_WithMissingFile_ThrowsFileNotFoundException()
    {
        using var fixture = new ConfigurationFixture("model.gguf", "model");
        string missingPath = Path.Combine(
            fixture.Root,
            "missing.gguf");

        Assert.ThrowsExactly<FileNotFoundException>(
            () => ControlledModelConfiguration.Load(
                fixture.ManifestPath,
                missingPath));
    }

    [TestMethod]
    public void Load_WithWrongFilename_ThrowsInvalidDataException()
    {
        using var fixture = new ConfigurationFixture(
            "expected.gguf",
            "model");
        string actualPath = fixture.WriteFile("actual.gguf", "model");

        Assert.ThrowsExactly<InvalidDataException>(
            () => ControlledModelConfiguration.Load(
                fixture.ManifestPath,
                actualPath));
    }

    [TestMethod]
    public void Load_WithWrongLength_ThrowsInvalidDataException()
    {
        using var fixture = new ConfigurationFixture(
            "model.gguf",
            "expected");
        string actualPath = fixture.WriteFile("model.gguf", "different");

        Assert.ThrowsExactly<InvalidDataException>(
            () => ControlledModelConfiguration.Load(
                fixture.ManifestPath,
                actualPath));
    }

    [TestMethod]
    public async Task ValidateSha256Async_WithWrongHash_ThrowsInvalidDataException()
    {
        using var fixture = new ConfigurationFixture(
            "model.gguf",
            "model",
            sha256Override: new string('0', 64));
        string actualPath = fixture.WriteFile("model.gguf", "model");
        ControlledModelConfiguration configuration =
            ControlledModelConfiguration.Load(
                fixture.ManifestPath,
                actualPath);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () => await configuration.ValidateSha256Async(
                CancellationToken.None));
    }

    [TestMethod]
    public async Task LoadAndValidate_WithMatchingSmallFixture_Succeeds()
    {
        using var fixture = new ConfigurationFixture(
            "model.gguf",
            "model");
        string actualPath = fixture.WriteFile("model.gguf", "model");
        ControlledModelConfiguration configuration =
            ControlledModelConfiguration.Load(
                fixture.ManifestPath,
                actualPath);

        await configuration.ValidateSha256Async(
            CancellationToken.None);

        Assert.AreEqual(Path.GetFullPath(actualPath), configuration.ModelPath);
        Assert.AreEqual("controlled-test-model", configuration.Manifest.Id);
    }

    private sealed class ConfigurationFixture : IDisposable
    {
        internal ConfigurationFixture(
            string expectedFileName,
            string expectedContent,
            string? sha256Override = null)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI-LlamaSharpTests",
                "configuration",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
                expectedContent);
            string expectedHash = sha256Override ?? Convert
                .ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
                .ToLowerInvariant();

            var manifest = new ControlledModelManifest
            {
                Id = "controlled-test-model",
                FileName = expectedFileName,
                Sha256 = expectedHash,
                LengthBytes = bytes.Length,
                Architecture = "granite",
                ModelName = "Test Granite",
                FileType = "15",
                QuantizationVersion = "2",
                TokenizerModel = "gpt2",
                ContextSize = 1,
                EmbeddingSize = 1,
                LayerCount = 1,
                HeadCount = 1,
                KvHeadCount = 1,
                MetadataCount = 1,
                VocabularyCount = 1,
                ChatTemplatePresent = true,
                TokenizerSmokeTokenCount = 1
            };

            ManifestPath = Path.Combine(Root, "manifest.json");
            File.WriteAllText(
                ManifestPath,
                JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
        }

        internal string Root { get; }

        internal string ManifestPath { get; }

        internal string WriteFile(string fileName, string content)
        {
            string path = Path.Combine(Root, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
