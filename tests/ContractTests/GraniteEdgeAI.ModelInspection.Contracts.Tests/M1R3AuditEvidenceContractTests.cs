using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Audit")]
[TestCategory("Contract")]
public sealed class M1R3AuditEvidenceContractTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string ScriptPath = Path.Combine(
        Root, "scripts", "model-inspection", "Test-M1R3AuditEvidence.ps1");

    [TestMethod]
    public void ValidatorRejectsR2SkippedTestArithmetic()
    {
        string manifestPath = Path.Combine(
            Root, "docs", "audits", "2026-08-28", "evidence",
            "M1-model-inspection-evidence-v1.json");

        ProcessResult result = RunPowerShell("-ManifestPath", manifestPath);
        string output = Normalize(result.CombinedOutput);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(output, "OPENVINO-MANAGED-ROUTE");
        StringAssert.Contains(output, "executed must equal passed + failed + skipped");
    }

    [TestMethod]
    public void ValidatorAcceptsExactCommittedR3EvidenceAndRemoteIdentity()
    {
        using EvidenceRepository repository = EvidenceRepository.Create();

        ProcessResult result = Validate(repository);

        Assert.AreEqual(0, result.ExitCode, result.CombinedOutput);
        StringAssert.Contains(
            Normalize(result.CombinedOutput),
            "identity, arithmetic, hashes, Git blobs, ancestry, remote ref, and privacy passed");
    }

    [TestMethod]
    [DataRow("discovered-less-than-executed", "discovered must be greater than or equal to executed")]
    [DataRow("report-sha256", "report SHA-256")]
    [DataRow("report-bytes", "report byte count")]
    [DataRow("manifest-sha256", "manifest SHA-256")]
    [DataRow("manifest-bytes", "manifest byte count")]
    [DataRow("subject-tree", "Evidence subject tree")]
    [DataRow("subject-join", "Receipt evidence subject")]
    [DataRow("final-tree", "Final tree")]
    [DataRow("remote-ref", "Receipt remote ref")]
    [DataRow("aggregate-arithmetic", "Receipt testTotals")]
    [DataRow("missing-stable-kind", "modelInspectionHandoff")]
    [DataRow("manifest-extra-property", "Evidence manifest contains unsupported schema property")]
    [DataRow("receipt-extra-property", "M1 handoff receipt contains unsupported schema property")]
    [DataRow("r2-artifact-path", "R3 report path")]
    [DataRow("dirty-worktree", "worktree must be clean")]
    public void ValidatorRejectsMutatedEvidence(string mutation, string expectedMessage)
    {
        using EvidenceRepository repository = EvidenceRepository.Create(mutation);

        ProcessResult result = Validate(repository);

        Assert.AreNotEqual(0, result.ExitCode, mutation);
        StringAssert.Contains(Normalize(result.CombinedOutput), expectedMessage, mutation);
    }

    private static ProcessResult Validate(EvidenceRepository repository) =>
        RunPowerShell(
            "-ManifestPath", repository.ManifestPath,
            "-ReceiptPath", repository.ReceiptPath,
            "-RepositoryRoot", repository.WorktreePath,
            "-ExpectedFrozenCommit", repository.FrozenCommit,
            "-ExpectedFrozenTree", repository.FrozenTree,
            "-ExpectedBranch", "audit/ucl-m1-model-inspection-remediation-r3");

    private static ProcessResult RunPowerShell(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
                 {
                     "-NoLogo", "-NoProfile", "-NonInteractive",
                     "-ExecutionPolicy", "Bypass", "-File", ScriptPath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("PowerShell did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Evidence validator exceeded 30 seconds.");
        }
        return new ProcessResult(
            process.ExitCode,
            string.Concat(output, Environment.NewLine, error));
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", " ");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string CombinedOutput);

    private sealed class EvidenceRepository : IDisposable
    {
        private const string Branch = "audit/ucl-m1-model-inspection-remediation-r3";
        private readonly string _root;

        private EvidenceRepository(
            string root,
            string worktreePath,
            string manifestPath,
            string receiptPath,
            string frozenCommit,
            string frozenTree)
        {
            _root = root;
            WorktreePath = worktreePath;
            ManifestPath = manifestPath;
            ReceiptPath = receiptPath;
            FrozenCommit = frozenCommit;
            FrozenTree = frozenTree;
        }

        internal string WorktreePath { get; }
        internal string ManifestPath { get; }
        internal string ReceiptPath { get; }
        internal string FrozenCommit { get; }
        internal string FrozenTree { get; }

        internal static EvidenceRepository Create(string? mutation = null)
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"geai-m1-r3-evidence-{Guid.NewGuid():N}");
            string worktree = Path.Combine(root, "worktree");
            string remote = Path.Combine(root, "origin.git");
            Directory.CreateDirectory(worktree);
            RunGit(root, "init", "--bare", remote);
            RunGit(worktree, "init", "-b", Branch);
            RunGit(worktree, "config", "user.name", "M1 Contract Test");
            RunGit(worktree, "config", "user.email", "m1-contract@example.invalid");
            RunGit(worktree, "config", "core.autocrlf", "false");
            RunGit(worktree, "remote", "add", "origin", remote);

            WriteText(Path.Combine(worktree, "frozen.txt"), "frozen source\n");
            CommitAll(worktree, "test: frozen source");
            string frozenCommit = GitOutput(worktree, "rev-parse", "HEAD");
            string frozenTree = GitOutput(worktree, "rev-parse", "HEAD^{tree}");

            WriteText(
                Path.Combine(worktree, "implementation.txt"),
                "production implementation\n");
            CommitAll(worktree, "test: implementation subject");
            string subjectCommit = GitOutput(worktree, "rev-parse", "HEAD");
            string subjectTree = GitOutput(worktree, "rev-parse", "HEAD^{tree}");

            const string reportRelative =
                "docs/audits/2026-08-28/M1-model-inspection-remediation-r3.md";
            const string manifestRelative =
                "docs/audits/2026-08-28/evidence/M1-model-inspection-evidence-r3.json";
            string selectedReportRelative = mutation == "r2-artifact-path"
                ? "docs/audits/2026-08-27/M1-model-inspection-remediation-r2.md"
                : reportRelative;
            string reportPath = RepositoryPath(worktree, selectedReportRelative);
            string manifestPath = RepositoryPath(worktree, manifestRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            string schemaDirectory = RepositoryPath(
                worktree,
                "docs/audits/2026-08-28/schemas");
            Directory.CreateDirectory(schemaDirectory);
            foreach (string schemaName in new[]
                     {
                         "10-EVIDENCE-MANIFEST-SCHEMA.json",
                         "10-HANDOFF-RECEIPT-SCHEMA.json"
                     })
            {
                File.Copy(
                    Path.Combine(
                        Root,
                        "docs",
                        "audits",
                        "2026-08-28",
                        "schemas",
                        schemaName),
                    Path.Combine(schemaDirectory, schemaName));
            }
            WriteText(reportPath, "# M1 R3 evidence fixture\n\nNo private model data.\n");

            FileIdentity reportIdentity = Identity(reportPath);
            var manifest = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["workerId"] = "M1",
                ["frozenSourceCommit"] = frozenCommit,
                ["evidenceSubjectCommit"] = subjectCommit,
                ["evidenceSubjectTree"] = mutation == "subject-tree" ? new string('0', 40) : subjectTree,
                ["createdAtUtc"] = "2026-08-29T12:00:00Z",
                ["route"] = new JsonArray("shared", "gguf", "openvino"),
                ["evidenceStatus"] = "passed",
                ["report"] = new JsonObject
                {
                    ["path"] = selectedReportRelative,
                    ["sha256"] = mutation == "report-sha256" ? new string('0', 64) : reportIdentity.Sha256,
                    ["bytes"] = mutation == "report-bytes" ? reportIdentity.Bytes + 1 : reportIdentity.Bytes,
                },
                ["inputs"] = new JsonArray(EvidenceItem("modelSource", "source")),
                ["outputs"] = mutation == "missing-stable-kind"
                    ? new JsonArray(EvidenceItem("modelInspectionResult", "result"))
                    : new JsonArray(
                        EvidenceItem("modelInspectionResult", "result"),
                        EvidenceItem("modelInspectionHandoff", "handoff")),
                ["commands"] = new JsonArray(new JsonObject
                {
                    ["id"] = "M1-R3-FOCUSED",
                    ["exitCode"] = 0,
                    ["discovered"] = mutation == "discovered-less-than-executed" ? 1 : 2,
                    ["executed"] = 2,
                    ["passed"] = 1,
                    ["failed"] = 0,
                    ["skipped"] = 1,
                }),
                ["blockers"] = new JsonArray(),
                ["nonClaims"] = new JsonArray("No native result is claimed."),
            };
            if (mutation == "manifest-extra-property")
            {
                manifest["unapproved"] = true;
            }
            WriteJson(manifestPath, manifest);
            CommitAll(worktree, "docs: evidence artifacts");
            string finalTip = GitOutput(worktree, "rev-parse", "HEAD");
            string finalTree = GitOutput(worktree, "rev-parse", "HEAD^{tree}");
            RunGit(worktree, "push", "-u", "origin", Branch);
            RunGit(worktree, "fetch", "origin");

            FileIdentity manifestIdentity = Identity(manifestPath);
            string receiptPath = Path.Combine(root, "M1.json");
            var receipt = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["workerId"] = "M1",
                ["frozenSourceCommit"] = frozenCommit,
                ["frozenSourceTree"] = frozenTree,
                ["branch"] = Branch,
                ["finalTip"] = finalTip,
                ["finalTree"] = mutation == "final-tree" ? new string('2', 40) : finalTree,
                ["worktreeClean"] = true,
                ["completedAtUtc"] = "2026-08-29T12:01:00Z",
                ["report"] = IdentityNode(selectedReportRelative, reportIdentity),
                ["evidenceManifest"] = new JsonObject
                {
                    ["path"] = manifestRelative,
                    ["sha256"] = mutation == "manifest-sha256" ? new string('3', 64) : manifestIdentity.Sha256,
                    ["bytes"] = mutation == "manifest-bytes" ? manifestIdentity.Bytes + 1 : manifestIdentity.Bytes,
                    ["evidenceSubjectCommit"] = mutation == "subject-join" ? new string('1', 40) : subjectCommit,
                    ["evidenceSubjectTree"] = subjectTree,
                },
                ["transport"] = "remote",
                ["remoteRef"] = mutation == "remote-ref"
                    ? "refs/remotes/origin/not-the-m1-branch"
                    : $"refs/remotes/origin/{Branch}",
                ["bundle"] = null,
                ["testTotals"] = new JsonObject
                {
                    ["discovered"] = 2,
                    ["executed"] = mutation == "aggregate-arithmetic" ? 1 : 2,
                    ["passed"] = 1,
                    ["failed"] = 0,
                    ["skipped"] = 1,
                },
                ["nativeDisposition"] = "not-run",
            };
            if (mutation == "receipt-extra-property")
            {
                receipt["unapproved"] = true;
            }
            WriteJson(receiptPath, receipt);
            if (mutation == "dirty-worktree")
            {
                WriteText(Path.Combine(worktree, "untracked.txt"), "dirty\n");
            }

            return new EvidenceRepository(
                root, worktree, manifestPath, receiptPath, frozenCommit, frozenTree);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static JsonObject EvidenceItem(string kind, string id) => new()
        {
            ["kind"] = kind,
            ["id"] = id,
            ["sha256"] = new string('a', 64),
            ["bytes"] = 1,
            ["evidenceGrade"] = "verified",
        };

        private static JsonObject IdentityNode(string path, FileIdentity identity) => new()
        {
            ["path"] = path,
            ["sha256"] = identity.Sha256,
            ["bytes"] = identity.Bytes,
        };

        private static string RepositoryPath(string root, string relative) =>
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

        private static void WriteText(string path, string value) =>
            File.WriteAllText(path, value, new UTF8Encoding(false));

        private static void WriteJson(string path, JsonNode value) =>
            WriteText(
                path,
                value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

        private static FileIdentity Identity(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return new FileIdentity(
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.LongLength);
        }

        private static void CommitAll(string repository, string message)
        {
            RunGit(repository, "add", "--all");
            RunGit(repository, "commit", "-m", message);
        }

        private static string GitOutput(string repository, params string[] arguments) =>
            RunGit(repository, arguments).Trim();

        private static string RunGit(string repository, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = repository,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Git did not start.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Git exceeded 15 seconds.");
            }
            Assert.AreEqual(
                0,
                process.ExitCode,
                $"git {string.Join(' ', arguments)} failed:{Environment.NewLine}{output}{error}");
            return output;
        }
    }

    private sealed record FileIdentity(string Sha256, long Bytes);
}
