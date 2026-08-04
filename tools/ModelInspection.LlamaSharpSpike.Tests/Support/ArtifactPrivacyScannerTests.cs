using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies evidence upload scanning detects model-like and oversized files.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ArtifactPrivacyScannerTests
{
    [TestMethod]
    public async Task ScanAsync_WithOrdinaryEvidence_Passes()
    {
        using var directory = new TemporaryDirectory("artifact-clean");
        await TestFileBuilder.WriteTextAsync(
            directory,
            "report.json",
            "{\"succeeded\":true}");

        ArtifactPrivacyScanResult result =
            await ArtifactPrivacyScanner.ScanAsync(
                directory.Path,
                prohibitedModelLengthBytes: 1000,
                prohibitedModelSha256: new string('0', 64),
                CancellationToken.None,
                maximumEvidenceFileBytes: 100);

        Assert.IsTrue(result.Passed);
        Assert.AreEqual(0, result.Findings.Count);
    }

    [TestMethod]
    public async Task ScanAsync_WithGgufExtension_ReportsFinding()
    {
        using var directory = new TemporaryDirectory("artifact-gguf");
        await TestFileBuilder.WriteTextAsync(
            directory,
            "model.gguf",
            "not-a-model");

        ArtifactPrivacyScanResult result =
            await ArtifactPrivacyScanner.ScanAsync(
                directory.Path,
                prohibitedModelLengthBytes: 1000,
                prohibitedModelSha256: new string('0', 64),
                CancellationToken.None,
                maximumEvidenceFileBytes: 100);

        Assert.IsFalse(result.Passed);
        Assert.IsTrue(result.Findings.Any(
            finding => finding.Reason.Contains(
                "GGUF",
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ScanAsync_WithControlledModelLength_ReportsFinding()
    {
        using var directory = new TemporaryDirectory("artifact-length");
        string path = directory.Combine("renamed.bin");
        await File.WriteAllBytesAsync(path, new byte[64]);

        ArtifactPrivacyScanResult result =
            await ArtifactPrivacyScanner.ScanAsync(
                directory.Path,
                prohibitedModelLengthBytes: 64,
                prohibitedModelSha256: new string('0', 64),
                CancellationToken.None,
                maximumEvidenceFileBytes: 100);

        Assert.IsFalse(result.Passed);
        Assert.IsTrue(result.Findings.Any(
            finding => finding.Reason.Contains(
                "length matches",
                StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ScanAsync_WithOversizedEvidence_ReportsFinding()
    {
        using var directory = new TemporaryDirectory("artifact-oversized");
        string path = directory.Combine("oversized.bin");
        await File.WriteAllBytesAsync(path, new byte[101]);

        ArtifactPrivacyScanResult result =
            await ArtifactPrivacyScanner.ScanAsync(
                directory.Path,
                prohibitedModelLengthBytes: 1000,
                prohibitedModelSha256: new string('0', 64),
                CancellationToken.None,
                maximumEvidenceFileBytes: 100);

        Assert.IsFalse(result.Passed);
        Assert.IsTrue(result.Findings.Any(
            finding => finding.Reason.Contains(
                "exceeds",
                StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ScanAsync_WithMatchingContentHash_ReportsFinding()
    {
        using var directory = new TemporaryDirectory("artifact-hash");
        byte[] bytes = Encoding.UTF8.GetBytes("controlled-model-content");
        string path = directory.Combine("renamed.dat");
        await File.WriteAllBytesAsync(path, bytes);
        string prohibitedHash = Convert
            .ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();

        ArtifactPrivacyScanResult result =
            await ArtifactPrivacyScanner.ScanAsync(
                directory.Path,
                prohibitedModelLengthBytes: bytes.Length + 1,
                prohibitedModelSha256: prohibitedHash,
                CancellationToken.None,
                maximumEvidenceFileBytes: 1000);

        Assert.IsFalse(result.Passed);
        Assert.IsTrue(result.Findings.Any(
            finding => finding.Reason.Contains(
                "SHA-256 matches",
                StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ScanAsync_WithMissingRoot_ThrowsDirectoryNotFoundException()
    {
        string missingRoot = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(
            async () => await ArtifactPrivacyScanner.ScanAsync(
                missingRoot,
                prohibitedModelLengthBytes: 1,
                prohibitedModelSha256: new string('0', 64),
                CancellationToken.None));
    }
}
