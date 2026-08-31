using System.Diagnostics;
using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class E1EndToEndRunnerInvocationTests
{
    private const string CandidateCommit = "b5d2cd34c57368efb9b122cddf16c2ffa2d3895e";
    private const string CandidateTree = "a3e4d82095caa9688f30d2463fa5971c788fd33f";

    [TestMethod]
    public void Missing_required_arguments_fail_before_build_or_lock()
    {
        using RunnerFixture fixture = RunnerFixture.Create();

        ProcessResult result = fixture.Run("-CandidateManifest", fixture.CandidateManifest);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(File.Exists(fixture.DotNetLog));
        Assert.IsFalse(Directory.Exists(fixture.LockPath));
    }

    [TestMethod]
    public void Wrong_candidate_ref_or_subject_identity_fails_closed_before_build_or_lock()
    {
        using RunnerFixture fixture = RunnerFixture.Create();
        string[][] mutations =
        [
            ["-CandidateCommit", new string('0', 40)],
            ["-CandidateTree", new string('0', 40)],
            ["-IntegrationCandidateRemoteRef", "refs/heads/integration/not-the-issued-candidate"],
            ["-ImplementationCommit", new string('0', 40)],
            ["-ImplementationTree", new string('0', 40)],
        ];

        foreach (string[] mutation in mutations)
        {
            ProcessResult result = fixture.RunExact(mutation);
            Assert.AreNotEqual(0, result.ExitCode, $"Mutation unexpectedly passed: {string.Join(' ', mutation)}");
        }

        Assert.IsFalse(File.Exists(fixture.DotNetLog));
        Assert.IsFalse(File.Exists(fixture.VsTestLog));
        Assert.IsFalse(Directory.Exists(fixture.LockPath));
    }

    [TestMethod]
    public void Complete_exact_arguments_invoke_authoritative_preflight_and_post_evaluator()
    {
        using RunnerFixture fixture = RunnerFixture.Create();

        ProcessResult result = fixture.RunExact();

        Assert.AreEqual(0, result.ExitCode, result.Output);
        string[] invocations = File.ReadAllLines(fixture.VsTestLog);
        Assert.AreEqual(2, invocations.Length);
        StringAssert.Contains(invocations[0], "/TestCaseFilter:TestCategory=Preflight");
        StringAssert.Contains(invocations[1], "/TestCaseFilter:TestCategory=PostAcceptance");
        Assert.IsFalse(File.Exists(fixture.DotNetLog));
        Assert.IsFalse(Directory.Exists(fixture.LockPath));
    }

    private sealed class RunnerFixture : IDisposable
    {
        private readonly TestDirectory _directory;
        private readonly string _repositoryRoot;
        private readonly string _implementationCommit;
        private readonly string _implementationTree;

        private RunnerFixture(TestDirectory directory, string repositoryRoot)
        {
            _directory = directory;
            _repositoryRoot = repositoryRoot;
            _implementationCommit = Git(repositoryRoot, "rev-parse", "HEAD");
            _implementationTree = Git(repositoryRoot, "show", "-s", "--format=%T", "HEAD");
            CandidateManifest = Path.Combine(directory.Path, "candidate.json");
            File.WriteAllText(CandidateManifest, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                sourceCommit = CandidateCommit,
                sourceTree = CandidateTree,
                packageFamilyName = "fixture",
                applicationId = "App",
                executablePath = Path.Combine(directory.Path, "fixture.exe"),
                executableSha256 = new string('a', 64),
                executableBytes = 1,
            }));
            DotNetLog = Path.Combine(directory.Path, "dotnet.log");
            VsTestLog = Path.Combine(directory.Path, "vstest.log");
            DotNetPath = WriteRecorder("dotnet.cmd", "E1_RUNNER_DOTNET_LOG");
            VsTestPath = WriteRecorder("vstest.cmd", "E1_RUNNER_VSTEST_LOG");
            LockPath = Path.Combine(directory.Path, "native.lock");
        }

        internal string CandidateManifest { get; }
        internal string DotNetLog { get; }
        internal string DotNetPath { get; }
        internal string VsTestLog { get; }
        internal string VsTestPath { get; }
        internal string LockPath { get; }

        internal static RunnerFixture Create()
        {
            string root = FindRepositoryRoot();
            return new RunnerFixture(TestDirectory.Create(), root);
        }

        internal ProcessResult RunExact(params string[][] mutations)
        {
            var arguments = new List<string>
            {
                "-CandidateManifest", CandidateManifest,
                "-Stage", "Evidence",
                "-IntegrationCandidateRemote", "origin",
                "-IntegrationCandidateRemoteRef", "refs/heads/integration/ucl-r4-e1-issued-base-v2",
                "-R3ClosureManifest", Path.Combine(_repositoryRoot, "docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json"),
                "-R3ClosureRelativePath", "docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json",
                "-CandidateCommit", CandidateCommit,
                "-CandidateTree", CandidateTree,
                "-ImplementationCommit", _implementationCommit,
                "-ImplementationTree", _implementationTree,
                "-DotNetHostPath", DotNetPath,
                "-EvidenceTestAssembly", typeof(E1EndToEndRunnerInvocationTests).Assembly.Location,
                "-VSTestPath", VsTestPath,
                "-NativeLockPath", LockPath,
            };
            foreach (string[] mutation in mutations)
            {
                int index = arguments.IndexOf(mutation[0]);
                arguments[index + 1] = mutation[1];
            }
            return Run(arguments.ToArray());
        }

        internal ProcessResult Run(params string[] arguments)
        {
            string script = Path.Combine(_repositoryRoot,
                "tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/Invoke-E1EndToEnd.ps1");
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.Environment["E1_RUNNER_DOTNET_LOG"] = DotNetLog;
            start.Environment["E1_RUNNER_VSTEST_LOG"] = VsTestLog;
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);
            return new ProcessResult(process.ExitCode, output);
        }

        private string WriteRecorder(string name, string variable)
        {
            string path = Path.Combine(_directory.Path, name);
            File.WriteAllText(path, $"@echo off{Environment.NewLine}echo %*>>\"%{variable}%\"{Environment.NewLine}exit /b 0{Environment.NewLine}");
            return path;
        }

        private static string Git(string root, params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Git did not start.");
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Git query failed.");
            return output.Trim();
        }

        private static string FindRepositoryRoot()
        {
            string? path = AppContext.BaseDirectory;
            while (path is not null)
            {
                if (File.Exists(Path.Combine(path, "Directory.Build.props"))
                    && Directory.Exists(Path.Combine(path, "tests/E2ETests"))) return path;
                path = Directory.GetParent(path)?.FullName;
            }
            return path ?? throw new InvalidOperationException("Repository root not found.");
        }

        public void Dispose() => _directory.Dispose();
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
