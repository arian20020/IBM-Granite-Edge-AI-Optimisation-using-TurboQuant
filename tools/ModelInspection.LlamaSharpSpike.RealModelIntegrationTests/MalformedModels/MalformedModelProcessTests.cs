using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.MalformedModels;

/// <summary>
/// Runs every committed malformed GGUF in an isolated child process and proves
/// one native abort cannot stop the remaining matrix.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class MalformedModelProcessTests
{
    [TestMethod]
    public async Task Run_EveryCommittedMalformedFixture_IsContainedAndPreserved()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string repositoryRoot = RepositoryRootLocator.Find();
        string fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "TestFixtures");
        string manifestPath = Path.Combine(
            fixtureRoot,
            "fixture-manifest.json");
        GgufFixtureEntry[] malformed = GgufFixtureManifest
            .Load(manifestPath)
            .Where(entry => entry.FixtureId.StartsWith(
                "I-",
                StringComparison.Ordinal))
            .OrderBy(entry => entry.FixtureId, StringComparer.Ordinal)
            .ToArray();

        Assert.IsTrue(
            malformed.Length >= 39,
            $"Expected at least 39 malformed fixtures, found {malformed.Length}.");

        var results = new List<MalformedScenarioResult>();

        foreach (GgufFixtureEntry entry in malformed)
        {
            string fixturePath = Path.Combine(
                fixtureRoot,
                entry.FixtureFile.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(fixturePath), $"Fixture missing: {entry.FixtureId}");
            Assert.AreEqual(entry.ByteLength, new FileInfo(fixturePath).Length);

            string hashBefore = await ModelFileHash.ComputeSha256Async(
                fixturePath,
                CancellationToken.None);
            Assert.AreEqual(entry.Sha256, hashBefore, $"Hash drift: {entry.FixtureId}");

            string evidencePath = context.CreateEvidencePath(
                $"malformed-{entry.FixtureId}");
            ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
                sandbox.CreateRequest(
                    new[]
                    {
                        "--model", fixturePath,
                        "--output", evidencePath
                    },
                    TimeSpan.FromSeconds(45)),
                CancellationToken.None);

            Assert.AreEqual(
                ProcessTerminationKind.Exited,
                process.TerminationKind,
                $"Fixture did not exit normally/through native termination: {entry.FixtureId}");
            Assert.IsNotNull(process.ExitCode);
            Assert.AreNotEqual(
                0,
                process.ExitCode.Value,
                $"Malformed fixture unexpectedly succeeded: {entry.FixtureId}");

            string hashAfter = await ModelFileHash.ComputeSha256Async(
                fixturePath,
                CancellationToken.None);
            Assert.AreEqual(hashBefore, hashAfter, $"Fixture changed: {entry.FixtureId}");

            EvidenceAssertions.AssertDoesNotContainCanonicalPath(
                process.StandardOutput,
                fixturePath);
            EvidenceAssertions.AssertDoesNotContainCanonicalPath(
                process.StandardError,
                fixturePath);

            string? failureCode = null;
            bool evidencePresent = File.Exists(evidencePath);

            if (evidencePresent)
            {
                string evidenceJson = await File.ReadAllTextAsync(evidencePath);
                EvidenceAssertions.AssertDoesNotContainCanonicalPath(
                    evidenceJson,
                    fixturePath);

                using JsonDocument document = JsonDocument.Parse(evidenceJson);
                JsonElement root = document.RootElement;
                Assert.IsFalse(root.GetProperty("succeeded").GetBoolean());
                failureCode = root.TryGetProperty(
                        "failureCode",
                        out JsonElement failureCodeElement) &&
                    failureCodeElement.ValueKind == JsonValueKind.String
                        ? failureCodeElement.GetString()
                        : null;
                Assert.IsFalse(string.IsNullOrWhiteSpace(failureCode));
            }

            results.Add(new MalformedScenarioResult(
                entry.FixtureId,
                entry.FixtureFile,
                entry.ByteLength,
                entry.Sha256,
                process.TerminationKind,
                process.ExitCode.Value,
                evidencePresent,
                failureCode));
        }

        await WriteSummaryAsync(context.EvidenceRoot, results);
        EvidenceAssertions.AssertNoGgufFiles(context.EvidenceRoot);
    }

    [TestMethod]
    public async Task Run_WithDeterministicRandomBytes_IsContainedAndPreserved()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string fixtureDirectory = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            "random-gguf",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        string fixturePath = Path.Combine(
            fixtureDirectory,
            "deterministic-random.gguf");

        try
        {
            byte[] bytes = new byte[4096];
            new Random(20260804).NextBytes(bytes);
            await File.WriteAllBytesAsync(fixturePath, bytes);
            string hashBefore = await ModelFileHash.ComputeSha256Async(
                fixturePath,
                CancellationToken.None);
            string evidencePath = context.CreateEvidencePath("random-bytes");

            ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
                sandbox.CreateRequest(
                    new[]
                    {
                        "--model", fixturePath,
                        "--output", evidencePath
                    },
                    TimeSpan.FromSeconds(45)),
                CancellationToken.None);

            Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
            Assert.IsNotNull(process.ExitCode);
            Assert.AreNotEqual(0, process.ExitCode.Value);
            Assert.AreEqual(
                hashBefore,
                await ModelFileHash.ComputeSha256Async(
                    fixturePath,
                    CancellationToken.None));
            EvidenceAssertions.AssertDoesNotContainCanonicalPath(
                process.StandardOutput,
                fixturePath);
            EvidenceAssertions.AssertDoesNotContainCanonicalPath(
                process.StandardError,
                fixturePath);
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    private static async Task WriteSummaryAsync(
        string evidenceRoot,
        IReadOnlyList<MalformedScenarioResult> results)
    {
        string jsonPath = Path.Combine(
            evidenceRoot,
            "malformed-matrix.json");
        string markdownPath = Path.Combine(
            evidenceRoot,
            "malformed-matrix.md");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(
                results,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));

        var markdown = new StringBuilder();
        markdown.AppendLine("# Malformed GGUF child-process matrix");
        markdown.AppendLine();
        markdown.AppendLine("| Fixture | Bytes | Exit | JSON | Failure code |");
        markdown.AppendLine("|---|---:|---:|---|---|");

        foreach (MalformedScenarioResult result in results)
        {
            markdown.AppendLine(
                $"| {result.FixtureId} | {result.ByteLength} | " +
                $"{result.ExitCode} | {result.EvidencePresent} | " +
                $"{result.FailureCode ?? "native termination before JSON"} |");
        }

        await File.WriteAllTextAsync(markdownPath, markdown.ToString());
    }

    private sealed record MalformedScenarioResult(
        string FixtureId,
        string FixtureFile,
        long ByteLength,
        string Sha256,
        ProcessTerminationKind TerminationKind,
        int ExitCode,
        bool EvidencePresent,
        string? FailureCode);
}
