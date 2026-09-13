using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class R3ReleaseVetoPreflightTests
{
    [TestMethod]
    public void Verify_accepts_only_candidate_committed_closure_and_subject_blobs()
    {
        using TestDirectory directory = TestDirectory.Create();
        Candidate candidate = CreateCandidate(directory);

        IReadOnlyList<R3IssueEvidence> issues = R3ReleaseVetoPreflight.Verify(
            candidate.Working,
            candidate.ManifestPath,
            "docs/r3-closure.json",
            candidate.Commit,
            candidate.Tree,
            "origin",
            "refs/heads/integration/r3-candidate");

        Assert.AreEqual(22, issues.Count);
    }

    [TestMethod]
    public void Verify_rejects_working_tree_only_closure_mutation()
    {
        using TestDirectory directory = TestDirectory.Create();
        Candidate candidate = CreateCandidate(directory);
        File.AppendAllText(candidate.ManifestPath, " ");

        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            R3ReleaseVetoPreflight.Verify(
                candidate.Working,
                candidate.ManifestPath,
                "docs/r3-closure.json",
                candidate.Commit,
                candidate.Tree,
                "origin",
                "refs/heads/integration/r3-candidate"));
        StringAssert.Contains(error.Message, "Git blob");
    }

    [TestMethod]
    [TestCategory("Preflight")]
    public void Exact_R3_candidate_and_issue_evidence_are_committed_and_pushed()
    {
        string repositoryRoot = RequireEnvironment("GRANITE_E2E_REPOSITORY_ROOT");
        IReadOnlyList<R3IssueEvidence> issues = R3ReleaseVetoPreflight.Verify(
            repositoryRoot,
            RequireEnvironment("GRANITE_E2E_R3_CLOSURE_MANIFEST"),
            RequireEnvironment("GRANITE_E2E_R3_CLOSURE_RELATIVE_PATH"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_COMMIT"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_TREE"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_REMOTE"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_REMOTE_REF"));
        Assert.AreEqual(22, issues.Count);
    }

    private static Candidate CreateCandidate(TestDirectory directory)
    {
        string working = Path.Combine(directory.Path, "working");
        string remote = Path.Combine(directory.Path, "remote.git");
        Directory.CreateDirectory(working);
        Git(directory.Path, "init", "--bare", remote);
        Git(working, "init");
        Git(working, "config", "user.name", "E1 Test");
        Git(working, "config", "user.email", "e1@example.invalid");
        Directory.CreateDirectory(Path.Combine(working, "docs", "evidence"));
        const string evidenceContents = "sanitized behavioral evidence";
        string evidencePath = Path.Combine(working, "docs", "evidence", "result.json");
        File.WriteAllText(evidencePath, evidenceContents);
        Git(working, "add", "docs/evidence/result.json");
        Git(working, "commit", "-m", "evidence subject");
        string subjectCommit = Git(working, "rev-parse", "HEAD").Trim();
        string subjectTree = Git(working, "rev-parse", "HEAD^{tree}").Trim();

        var issues = Enumerable.Range(1, 22).Select(index =>
        {
            var issue = new Dictionary<string, object?>
            {
                ["issueId"] = $"R3-{index:000}",
                ["productDefectRemains"] = false,
                ["evidenceSubjectCommit"] = subjectCommit,
                ["evidenceSubjectTree"] = subjectTree,
                ["evidenceBlob"] = new
                {
                    path = "docs/evidence/result.json",
                    sha256 = Sha256(evidenceContents),
                    bytes = Encoding.UTF8.GetByteCount(evidenceContents),
                },
                ["commands"] = new[]
                {
                    new
                    {
                        evidenceType = "behavioralTest",
                        discovered = 1,
                        executed = 1,
                        passed = 1,
                        failed = 0,
                        skipped = 0,
                    },
                },
            };
            if (index is >= 4 and <= 18)
            {
                issue["productionReachability"] = new
                {
                    definition = "src/Definition.cs",
                    productionCaller = "src/Caller.cs",
                    registrationPoint = "src/Composition.cs",
                    behavioralRegressionTest = "tests/BehaviorTests.cs",
                    observedRegistrations = 1,
                };
            }
            return issue;
        }).ToArray();
        string manifestPath = Path.Combine(working, "docs", "r3-closure.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new { schemaVersion = 3, issues }));
        Git(working, "add", "docs/r3-closure.json");
        Git(working, "commit", "-m", "R3 closure");
        Git(working, "remote", "add", "origin", remote);
        Git(working, "push", "origin", "HEAD:refs/heads/integration/r3-candidate");
        return new Candidate(
            working,
            manifestPath,
            Git(working, "rev-parse", "HEAD").Trim(),
            Git(working, "rev-parse", "HEAD^{tree}").Trim());
    }

    private static string RequireEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"CHANGES REQUIRED: {name} is required.");

    private static string Git(string workingDirectory, params string[] arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git.exe",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, error);
        return output;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record Candidate(string Working, string ManifestPath, string Commit, string Tree);
}
