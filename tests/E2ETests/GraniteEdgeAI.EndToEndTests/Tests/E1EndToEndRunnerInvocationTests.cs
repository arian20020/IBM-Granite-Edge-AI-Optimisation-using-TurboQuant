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
            ["-IntegrationCandidateRemote", "https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant.git"],
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
    public void Native_stage_requires_every_exact_manifest_before_build_or_lock()
    {
        using RunnerFixture fixture = RunnerFixture.Create();

        ProcessResult result = fixture.RunExact(["-Stage", "Smoke"]);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(File.Exists(fixture.DotNetLog));
        Assert.IsFalse(File.Exists(fixture.VsTestLog));
        Assert.IsFalse(Directory.Exists(fixture.LockPath));
    }

    [TestMethod]
    public void Structurally_invalid_candidate_asset_or_producer_manifest_fails_before_build_or_lock()
    {
        foreach (string mutation in new[] { "candidate-extra", "candidate-drive-root-relative", "asset-duplicate", "producer-ledger" })
        {
            using RunnerFixture fixture = RunnerFixture.Create();
            fixture.ApplyManifestMutation(mutation);

            ProcessResult result = fixture.RunNativeExact();

            Assert.AreNotEqual(0, result.ExitCode, $"Malformed manifest unexpectedly passed: {mutation}");
            Assert.IsFalse(File.Exists(fixture.DotNetLog));
            Assert.IsFalse(Directory.Exists(fixture.LockPath));
        }
    }

    [TestMethod]
    public void Caller_supplied_evidence_executable_and_assembly_paths_are_rejected()
    {
        using RunnerFixture fixture = RunnerFixture.Create();

        ProcessResult result = fixture.RunExact(
            ["-EvidenceTestAssembly", typeof(E1EndToEndRunnerInvocationTests).Assembly.Location],
            ["-VSTestPath", fixture.VsTestPath]);

        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.IsFalse(File.Exists(fixture.VsTestLog));
        Assert.IsFalse(File.Exists(fixture.DotNetLog));
        Assert.IsFalse(Directory.Exists(fixture.LockPath));
    }

    [TestMethod]
    public void Production_Evidence_mode_validates_the_complete_result_root_before_VSTest()
    {
        using RunnerFixture fixture = RunnerFixture.Create();
        string script = File.ReadAllText(Path.Combine(fixture.RepositoryRoot,
            "tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/Invoke-E1EndToEnd.ps1"));
        const string creation = "New-E1SafeDirectoryChain -Path $evidenceResultRoot -RepositoryRoot $repositoryRoot";
        int validation = script.IndexOf(creation, StringComparison.Ordinal);
        int invocation = script.IndexOf("& $vstestExecutable @arguments", StringComparison.Ordinal);

        Assert.IsTrue(validation >= 0, "The authoritative result root is not created through the safe-chain validator.");
        Assert.IsTrue(invocation > validation, "VSTest can run before result-root validation.");
        Assert.IsFalse(script.Contains("New-Item -ItemType Directory -Force -Path $evidenceResultRoot", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("AuthoritativeIntegration")]
    public void Production_Evidence_mode_builds_and_parses_two_fresh_exact_TRX_results()
    {
        string implementation = RequireEnvironment("GRANITE_E1_RUNNER_IMPLEMENTATION_COMMIT");
        string tree = RequireEnvironment("GRANITE_E1_RUNNER_IMPLEMENTATION_TREE");
        using RunnerFixture fixture = RunnerFixture.Create();
        string results = Path.Combine(fixture.RepositoryRoot, "TestResults", "Audit-20260830", "E1-Evidence");
        HashSet<string> before = Directory.Exists(results)
            ? Directory.GetFiles(results, "E1-*.trx").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        ProcessResult result = fixture.RunExact(
            ["-ImplementationCommit", implementation],
            ["-ImplementationTree", tree]);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        string[] created = Directory.GetFiles(results, "E1-*.trx")
            .Where(path => !before.Contains(path)).ToArray();
        Assert.AreEqual(2, created.Length);
        Assert.AreEqual(1, created.Count(path => Path.GetFileName(path).StartsWith("E1-Preflight-", StringComparison.Ordinal)));
        Assert.AreEqual(1, created.Count(path => Path.GetFileName(path).StartsWith("E1-PostAcceptance-", StringComparison.Ordinal)));
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
            string executable = Path.Combine(directory.Path, "fixture.exe");
            File.WriteAllBytes(executable, [42]);
            string executableSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(executable))).ToLowerInvariant();
            File.WriteAllText(CandidateManifest, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1, ["sourceCommit"] = CandidateCommit, ["sourceTree"] = CandidateTree,
                ["packageFamilyName"] = "fixture.package", ["applicationId"] = "App",
                ["executablePath"] = executable, ["executableSha256"] = executableSha256, ["executableBytes"] = 1,
            }));
            AssetManifest = Path.Combine(directory.Path, "assets.json");
            File.WriteAllText(AssetManifest, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["assets"] = new object[] { new Dictionary<string, object?> { ["id"] = "asset", ["route"] = "gguf", ["sha256"] = new string('a', 64), ["bytes"] = 1 } },
            }));
            H1Manifest = WriteProducer("H1", ["hardwareSnapshot", "availableMemory", "safetyBudget"]);
            M1Manifest = WriteProducer("M1", ["modelSource", "modelInspectionResult", "modelInspectionHandoff"]);
            Q1Manifest = WriteProducer("Q1", ["optimizationPlan", "executionResult", "chatTarget", "exportTarget"]);
            DotNetLog = Path.Combine(directory.Path, "dotnet.log");
            VsTestLog = Path.Combine(directory.Path, "vstest.log");
            EnvironmentLog = Path.Combine(directory.Path, "environment.log");
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
        internal string AssetManifest { get; }
        internal string H1Manifest { get; }
        internal string M1Manifest { get; }
        internal string Q1Manifest { get; }
        internal string EnvironmentLog { get; }
        internal string ImplementationCommit => _implementationCommit;
        internal string ImplementationTree => _implementationTree;
        internal string RepositoryRoot => _repositoryRoot;

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
                "-NativeLockPath", LockPath,
            };
            foreach (string[] mutation in mutations)
            {
                int index = arguments.IndexOf(mutation[0]);
                if (index >= 0) arguments[index + 1] = mutation[1];
                else { arguments.Add(mutation[0]); arguments.Add(mutation[1]); }
            }
            return Run(arguments.ToArray());
        }

        internal ProcessResult RunNativeExact() => RunExact(
            ["-Stage", "Smoke"], ["-AssetManifest", AssetManifest], ["-H1Manifest", H1Manifest],
            ["-M1Manifest", M1Manifest], ["-Q1Manifest", Q1Manifest]);

        internal void ApplyManifestMutation(string mutation)
        {
            if (mutation == "candidate-extra")
            {
                Dictionary<string, object?> candidate = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(CandidateManifest))!;
                candidate["unexpected"] = true;
                File.WriteAllText(CandidateManifest, JsonSerializer.Serialize(candidate));
                return;
            }
            if (mutation == "candidate-drive-root-relative")
            {
                Dictionary<string, object?> candidate = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(CandidateManifest))!;
                string executable = ((JsonElement)candidate["executablePath"]!).GetString()!;
                candidate["executablePath"] = executable[2..];
                File.WriteAllText(CandidateManifest, JsonSerializer.Serialize(candidate));
                return;
            }
            if (mutation == "asset-duplicate")
            {
                File.WriteAllText(AssetManifest, JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["schemaVersion"] = 1,
                    ["assets"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = "duplicate", ["route"] = "gguf", ["sha256"] = new string('a', 64), ["bytes"] = 1 },
                        new Dictionary<string, object?> { ["id"] = "duplicate", ["route"] = "openvino", ["sha256"] = new string('b', 64), ["bytes"] = 1 },
                    },
                }));
                return;
            }
            if (mutation == "producer-ledger")
            {
                Dictionary<string, object?> producer = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(H1Manifest))!;
                producer.Remove("commands");
                File.WriteAllText(H1Manifest, JsonSerializer.Serialize(producer));
                return;
            }
            throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        private string WriteProducer(string worker, string[] kinds)
        {
            string report = Path.Combine(_directory.Path, $"{worker}-report.md");
            File.WriteAllText(report, "evidence");
            string path = Path.Combine(_directory.Path, $"{worker}.json");
            object[] items = kinds.Select(kind => (object)new Dictionary<string, object?>
            {
                ["kind"] = kind, ["id"] = $"{worker}-{kind}", ["evidenceGrade"] = "verified",
            }).ToArray();
            var producer = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 2, ["workerId"] = worker,
                ["frozenSourceCommit"] = "4748fe04f19afdf6b27c4c12502b84db325e7294",
                ["evidenceSubjectCommit"] = CandidateCommit, ["evidenceSubjectTree"] = CandidateTree,
                ["createdAtUtc"] = "2026-08-31T20:00:00Z", ["route"] = new[] { "shared" }, ["evidenceStatus"] = "passed",
                ["report"] = new Dictionary<string, object?> { ["path"] = Path.GetFileName(report), ["sha256"] = new string('a', 64), ["bytes"] = 8 },
                ["inputs"] = items, ["outputs"] = items,
                ["commands"] = new object[] { new Dictionary<string, object?>
                {
                    ["id"] = "GREEN", ["exitCode"] = 0, ["discovered"] = 1, ["executed"] = 1,
                    ["passed"] = 1, ["failed"] = 0, ["skipped"] = 0, ["disposition"] = "passed",
                } },
                ["blockers"] = Array.Empty<string>(), ["nonClaims"] = new[] { "none" },
            };
            File.WriteAllText(path, JsonSerializer.Serialize(producer));
            return path;
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
            start.Environment["E1_RUNNER_ENV_LOG"] = EnvironmentLog;
            start.Environment["GRANITE_E2E_ASSET_MANIFEST"] = "C:\\stale\\asset.json";
            start.Environment["GRANITE_E2E_H1_MANIFEST"] = "C:\\stale\\h1.json";
            start.Environment["GRANITE_E2E_M1_MANIFEST"] = "C:\\stale\\m1.json";
            start.Environment["GRANITE_E2E_Q1_MANIFEST"] = "C:\\stale\\q1.json";
            start.Environment["MSBuildSDKsPath"] = _directory.Path;
            start.Environment["MSBuildExtensionsPath"] = _directory.Path;
            start.Environment["MSBUILD_EXE_PATH"] = Path.Combine(_directory.Path, "fake-msbuild.exe");
            start.Environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = _directory.Path;
            start.Environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER"] = "99.0.0";
            start.Environment["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = _directory.Path;
            start.Environment["NUGET_PLUGIN_PATHS"] = _directory.Path;
            start.Environment["CORECLR_ENABLE_PROFILING"] = "1";
            start.Environment["CORECLR_PROFILER"] = "{11111111-1111-1111-1111-111111111111}";
            start.Environment["CORECLR_PROFILER_PATH_64"] = Path.Combine(_directory.Path, "fake-profiler.dll");
            start.Environment["COR_ENABLE_PROFILING"] = "1";
            start.Environment["COMPlus_ReadyToRun"] = "0";
            start.Environment["DOTNET_DiagnosticPorts"] = Path.Combine(_directory.Path, "diagnostic-port");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(script);
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(120_000);
            return new ProcessResult(process.ExitCode, output);
        }

        private string WriteRecorder(string name, string variable)
        {
            string path = Path.Combine(_directory.Path, name);
            File.WriteAllText(path,
                $"@echo off{Environment.NewLine}" +
                $"echo %*>>\"%{variable}%\"{Environment.NewLine}" +
                "echo candidate=%GRANITE_E2E_CANDIDATE_COMMIT%/%GRANITE_E2E_CANDIDATE_TREE%^|implementation=%GRANITE_E2E_IMPLEMENTATION_COMMIT%/%GRANITE_E2E_IMPLEMENTATION_TREE%^|remote=%GRANITE_E2E_CANDIDATE_REMOTE%/%GRANITE_E2E_CANDIDATE_REMOTE_REF%^|root=%GRANITE_E2E_REPOSITORY_ROOT%^|optional=%GRANITE_E2E_ASSET_MANIFEST%,%GRANITE_E2E_H1_MANIFEST%,%GRANITE_E2E_M1_MANIFEST%,%GRANITE_E2E_Q1_MANIFEST%>>\"%E1_RUNNER_ENV_LOG%\"" + Environment.NewLine +
                $"exit /b 0{Environment.NewLine}");
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

    private static string RequireEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"{name} is required for the authoritative runner integration test.");
}
