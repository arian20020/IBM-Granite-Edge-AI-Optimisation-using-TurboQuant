using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies that packaged GGUF binaries exactly match the generated manifest.
/// </summary>
[TestClass]
public sealed class GgufFixtureIntegrityTests
{
    /// <summary>
    /// Verifies the complete deployed binary set, byte lengths, and SHA-256 hashes.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task PackagedGgufFixtures_MatchIntegrityManifest()
    {
        string fixtureRoot = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures");
        string manifestPath = Path.Combine(
            fixtureRoot,
            "fixture-manifest.json");
        Assert.IsTrue(
            File.Exists(manifestPath),
            $"The packaged fixture manifest was not found at: {manifestPath}");

        FixtureManifest? manifest = JsonSerializer.Deserialize<FixtureManifest>(
            await File.ReadAllTextAsync(manifestPath));
        Assert.IsNotNull(manifest, "The fixture manifest deserialized to null.");
        Assert.IsNotEmpty(
            manifest.GeneratedGgufFixtures,
            "The fixture manifest contained no GGUF integrity records.");

        string[] expectedRelativePaths = manifest.GeneratedGgufFixtures
            .Select(record => record.FixtureFile)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualRelativePaths = Directory
            .EnumerateFiles(fixtureRoot, "*.gguf", SearchOption.AllDirectories)
            .Select(path => Path
                .GetRelativePath(fixtureRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            expectedRelativePaths,
            actualRelativePaths,
            "The packaged GGUF file set differs from the generated manifest.");

        string rootedFixturePrefix =
            Path.GetFullPath(fixtureRoot) + Path.DirectorySeparatorChar;
        foreach (FixtureIntegrityRecord record in manifest.GeneratedGgufFixtures)
        {
            string fixturePath = Path.GetFullPath(
                Path.Combine(
                    fixtureRoot,
                    record.FixtureFile.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            Assert.IsTrue(
                fixturePath.StartsWith(
                    rootedFixturePrefix,
                    StringComparison.OrdinalIgnoreCase),
                $"Manifest path escaped the fixture root: {record.FixtureFile}");
            Assert.IsTrue(
                File.Exists(fixturePath),
                $"Manifest fixture was not packaged: {record.FixtureFile}");

            FileInfo fixtureInfo = new(fixturePath);
            Assert.AreEqual(
                record.ByteLength,
                fixtureInfo.Length,
                $"Byte length mismatch for {record.FixtureFile}.");
            using FileStream fixtureStream = File.OpenRead(fixturePath);
            string actualSha256 = Convert
                .ToHexString(SHA256.HashData(fixtureStream))
                .ToLowerInvariant();
            Assert.AreEqual(
                record.Sha256,
                actualSha256,
                $"SHA-256 mismatch for {record.FixtureFile}.");
        }
    }

    private sealed record FixtureManifest
    {
        [JsonPropertyName("generatedGgufFixtures")]
        public required List<FixtureIntegrityRecord> GeneratedGgufFixtures { get; init; }
    }

    private sealed record FixtureIntegrityRecord
    {
        [JsonPropertyName("fixtureId")]
        public required string FixtureId { get; init; }

        [JsonPropertyName("fixtureFile")]
        public required string FixtureFile { get; init; }

        [JsonPropertyName("byteLength")]
        public required long ByteLength { get; init; }

        [JsonPropertyName("sha256")]
        public required string Sha256 { get; init; }
    }
}
