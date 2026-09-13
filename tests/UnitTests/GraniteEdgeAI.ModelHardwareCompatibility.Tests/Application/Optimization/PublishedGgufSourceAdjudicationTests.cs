using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

[TestClass]
public sealed class PublishedGgufSourceAdjudicationTests
{
    [TestMethod]
    public void EveryPublishedRowFailsClosedAgainstFrozenPromptAdjudications()
    {
        string root = FindSourceRoot();
        byte[] summaryBytes = File.ReadAllBytes(Path.Combine(root, "quality-summary.json"));
        byte[] adjudicationBytes = File.ReadAllBytes(Path.Combine(root, "quality-adjudications.json"));
        Assert.AreEqual("C7333FF5FAF191F9DDB9993C158D943A41D0D640B8C8D61055A4CDBE6C2E1FF9",
            Convert.ToHexString(SHA256.HashData(summaryBytes)));
        Assert.AreEqual("7C6BBFD833D30218445CF8EBE2DBAFB9FEA7560563CB3461EFF6DC55747C1E30",
            Convert.ToHexString(SHA256.HashData(adjudicationBytes)));
        using JsonDocument summary = JsonDocument.Parse(summaryBytes);
        using JsonDocument adjudications = JsonDocument.Parse(adjudicationBytes);
        Dictionary<string, JsonElement> rows = summary.RootElement.GetProperty("rows").EnumerateArray()
            .ToDictionary(row => row.GetProperty("test_id").GetString()!, StringComparer.Ordinal);
        foreach (OptimizationEvidenceRecord record in PublishedGgufOptimizationEvidence.Records())
        {
            Assert.IsTrue(rows.TryGetValue(record.EvidenceId, out JsonElement row), record.EvidenceId);
            JsonProperty[] prompts = row.GetProperty("prompts").EnumerateObject().ToArray();
            CollectionAssert.AreEquivalent(new[] { "P1", "P2", "P3", "P4", "P5", "P6" },
                prompts.Select(prompt => prompt.Name).ToArray());
            bool outputHealthPassed = true;
            foreach (JsonProperty prompt in prompts)
            {
                string sha = prompt.Value.GetProperty("response_sha256").GetString()!;
                Assert.IsTrue(adjudications.RootElement.TryGetProperty(prompt.Name + ":" + sha,
                    out JsonElement adjudication), record.EvidenceId + ":" + prompt.Name);
                outputHealthPassed &= sha != "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                    && adjudication.GetProperty("critical_caps").GetArrayLength() == 0
                    && adjudication.GetProperty("deterministic_pass").GetBoolean()
                    && adjudication.GetProperty("integrity_issue").GetString() == "No";
            }
            Assert.AreEqual(outputHealthPassed, record.OutputHealthPassed, record.EvidenceId);
            // these prompt artifacts contain no row-level package-integrity,
            // activation or repetition-stability proof. the quarantine must
            // not assert those unestablished gates as true
            Assert.IsFalse(record.IntegrityPassed, record.EvidenceId);
            Assert.IsFalse(record.ActivationPassed, record.EvidenceId);
            Assert.IsFalse(record.StabilityPassed, record.EvidenceId);
            Assert.IsFalse(record.IsAdmitted, record.EvidenceId);
        }
    }

    private static string FindSourceRoot()
    {
        const string relative = "experiments/raw-results/atomicbot-turboquant/2026-07-17/quality-all-rows";
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, relative);
            if (File.Exists(Path.Combine(path, "quality-adjudications.json"))) return path;
        }
        throw new AssertFailedException("Frozen source adjudications are required; missing evidence cannot be skipped.");
    }
}
