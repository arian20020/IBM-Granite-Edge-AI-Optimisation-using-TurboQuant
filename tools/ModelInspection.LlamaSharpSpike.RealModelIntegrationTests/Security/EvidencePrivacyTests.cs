using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Security;

/// <summary>
/// Verifies real-model evidence contains useful hashes and metadata without
/// exposing the local path, full chat template, model bytes, or large files.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class EvidencePrivacyTests
{
    [TestMethod]
    public async Task Run_WithControlledGranite_ProducesPrivacySafeEvidenceTree()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string evidencePath = context.CreateEvidencePath("privacy");

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", context.Model.ModelPath,
                    "--output", evidencePath
                },
                TimeSpan.FromMinutes(2)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(0, process.ExitCode);

        string json = await File.ReadAllTextAsync(evidencePath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            json,
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardOutput,
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardError,
            context.Model.ModelPath);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        EvidenceAssertions.AssertSuccessfulGraniteVocabOnly(
            root,
            context.Model.Manifest);

        Assert.IsFalse(root.TryGetProperty("modelPath", out _));
        Assert.IsFalse(root.TryGetProperty("canonicalModelPath", out _));

        JsonElement before = root.GetProperty("beforeSnapshot");
        string[] snapshotProperties = before
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(
            new[]
            {
                "fileName",
                "canonicalPathSha256",
                "lengthBytes",
                "lastWriteTimeUtc",
                "sha256"
            },
            snapshotProperties);

        JsonElement chatTemplate = root
            .GetProperty("modelEvidence")
            .GetProperty("chatTemplate");
        string[] chatProperties = chatTemplate
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(
            new[]
            {
                "present",
                "lengthCharacters",
                "sha256"
            },
            chatProperties);
        Assert.IsFalse(chatTemplate.TryGetProperty("text", out _));
        Assert.IsFalse(chatTemplate.TryGetProperty("template", out _));
        Assert.IsFalse(chatTemplate.TryGetProperty("content", out _));
        Assert.IsFalse(chatTemplate.TryGetProperty("value", out _));

        ArtifactPrivacyScanResult firstScan =
            await ArtifactPrivacyScanner.ScanAsync(
                context.EvidenceRoot,
                context.Model.Manifest.LengthBytes,
                context.Model.Manifest.Sha256,
                CancellationToken.None);
        Assert.IsTrue(
            firstScan.Passed,
            string.Join(
                Environment.NewLine,
                firstScan.Findings.Select(
                    finding => $"{finding.RelativePath}: {finding.Reason}")));

        string scanReportPath = context.CreateEvidencePath(
            "privacy",
            "artifact-privacy-scan.json");
        await File.WriteAllTextAsync(
            scanReportPath,
            JsonSerializer.Serialize(
                firstScan,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));

        ArtifactPrivacyScanResult finalScan =
            await ArtifactPrivacyScanner.ScanAsync(
                context.EvidenceRoot,
                context.Model.Manifest.LengthBytes,
                context.Model.Manifest.Sha256,
                CancellationToken.None);
        Assert.IsTrue(finalScan.Passed);
    }
}
