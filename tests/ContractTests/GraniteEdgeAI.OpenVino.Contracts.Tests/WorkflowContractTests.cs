using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class WorkflowContractTests
{
    private const string CheckoutSha =
        "9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0";
    private const string SetupDotNetSha =
        "d4c94342e560b34958eacfc5d055d21461ed1c5d";
    private const string SetupMsBuildSha =
        "30375c66a4eea26614e0d39710365f22f8b0af57";
    private const string UploadArtifactSha =
        "bbbca2ddaa5d8feaa63e36b76fdaad77386f024f";
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void TaskNineWorkflowAndEvidenceFilesExist()
    {
        foreach (string relativePath in new[]
        {
            ".github/workflows/openvino-official-ci.yml",
            ".github/workflows/openvino-ucl-intel.yml",
            "scripts/openvino/Invoke-OpenVinoOfficialEvidence.ps1",
            "scripts/openvino/Test-OpenVinoEvidencePrivacy.ps1"
        })
        {
            Assert.IsTrue(File.Exists(RepoPath(relativePath)), relativePath);
        }
    }

    [TestMethod]
    public void NewWorkflowsUseOnlyRepositoryApprovedFullShaActionPins()
    {
        foreach (string workflow in Workflows())
        {
            string source = File.ReadAllText(workflow);
            MatchCollection uses = Regex.Matches(
                source,
                @"(?m)^\s*uses:\s*([^\s#]+)\s*$",
                RegexOptions.CultureInvariant);
            Assert.IsGreaterThan(0, uses.Count, workflow);
            foreach (Match match in uses)
            {
                string reference = match.Groups[1].Value;
                Assert.IsTrue(
                    Regex.IsMatch(
                        reference,
                        @"^[a-z0-9_.-]+/[a-z0-9_.-]+@[0-9a-f]{40}$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                    $"Floating action reference in {workflow}: {reference}");
            }
        }

        string combined = string.Join('\n', Workflows().Select(File.ReadAllText));
        StringAssert.Contains(combined, $"actions/checkout@{CheckoutSha}");
        StringAssert.Contains(combined, $"actions/setup-dotnet@{SetupDotNetSha}");
        StringAssert.Contains(combined, $"microsoft/setup-msbuild@{SetupMsBuildSha}");
        StringAssert.Contains(combined, $"actions/upload-artifact@{UploadArtifactSha}");
    }

    [TestMethod]
    public void HostedWorkflowIsImmutableLeastPrivilegeReleaseX64AndCountGated()
    {
        string source = OfficialWorkflow();
        StringAssert.Contains(source, "permissions:\n  contents: read");
        StringAssert.Contains(source, "runs-on: windows-2022");
        StringAssert.Contains(source, "timeout-minutes:");
        StringAssert.Contains(source, "OPENVINO_EVIDENCE_COMMIT:");
        StringAssert.Contains(source, "ref: ${{ env.OPENVINO_EVIDENCE_COMMIT }}");
        StringAssert.Contains(source, "persist-credentials: false");
        StringAssert.Contains(source, "git rev-parse HEAD");
        StringAssert.Contains(source, "--configuration Release");
        StringAssert.Contains(source, "-p:Platform=x64");
        StringAssert.Contains(source, "--minimum-expected-tests 60");
        StringAssert.Contains(source, "--minimum-expected-tests 177");
        StringAssert.Contains(source, "--minimum-expected-tests 13");
        StringAssert.Contains(source, "--minimum-expected-tests 40");
        StringAssert.Contains(source, "Test-OpenVinoDependencyLocks.ps1");
        StringAssert.Contains(source, "Test-OpenVinoGenAiFixture.ps1");
        StringAssert.Contains(source, "Test-OpenVinoOfficialWorkerManifest.ps1");
        StringAssert.Contains(source, "Build-OpenVinoOfficialWorker.ps1");
        StringAssert.Contains(source, "Invoke-OpenVinoOfficialEvidence.ps1");
    }

    [TestMethod]
    public void HostedWorkflowRunsEveryOfficialCpuEvidenceLayer()
    {
        string source = OfficialWorkflow();
        foreach (string required in new[]
        {
            "GraniteEdgeAI.OpenVino.Contracts.Tests.csproj",
            "GraniteEdgeAI.OpenVino.Tests.csproj",
            "GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj",
            "GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj",
            "OpenVinoPromptAdapterTests",
            "OpenVinoWorkerPackagingTargetTests",
            "ctest.exe",
            "--output-junit"
        })
        {
            StringAssert.Contains(source, required);
        }
    }

    [TestMethod]
    public void TrustedWorkflowIsManualOnlyAndFailsClosedOnEveryUclPrecondition()
    {
        string source = UclWorkflow();
        Assert.IsTrue(Regex.IsMatch(
            source,
            @"(?ms)^on:\s*\r?\n\s*workflow_dispatch:\s*\r?\n"));
        Assert.IsFalse(Regex.IsMatch(source, @"(?m)^\s*(push|pull_request|schedule):"));
        StringAssert.Contains(source, "permissions:\n  contents: read");
        foreach (string label in new[]
        {
            "self-hosted", "Windows", "X64", "workbook05", "intel-target"
        })
        {
            StringAssert.Contains(source, $"- {label}");
        }

        StringAssert.Contains(source, "environment: openvino-ucl-01");
        StringAssert.Contains(source, "OPENVINO_UCL_01_AUTHORIZED");
        StringAssert.Contains(source, "UCL-01-approved");
        StringAssert.Contains(source, "target_commit");
        StringAssert.Contains(source, "^[0-9a-f]{40}$");
        StringAssert.Contains(source, "ref: ${{ inputs.target_commit }}");
        StringAssert.Contains(source, "persist-credentials: false");
        StringAssert.Contains(source, "ModelOutsideWorkspace");
        StringAssert.Contains(source, "IsReadOnly");
        StringAssert.Contains(source, "ExpectedModelLength");
        StringAssert.Contains(source, "ExpectedModelSha256");
        StringAssert.Contains(source, "Get-FileHash");
        StringAssert.Contains(source, "GenuineIntel");
        StringAssert.Contains(source, "Test-OpenVinoOfficialWorkerManifest.ps1");
        StringAssert.Contains(source, "Invoke-OpenVinoOfficialEvidence.ps1");
    }

    [TestMethod]
    public void CleanupIntegrityPrivacyAndSanitizedArtifactRulesAlwaysRun()
    {
        foreach (string source in new[] { OfficialWorkflow(), UclWorkflow() })
        {
            Assert.IsGreaterThanOrEqualTo(
                2,
                Regex.Matches(source, @"if:\s*\$\{\{\s*always\(\)").Count);
            StringAssert.Contains(source, "Test-OpenVinoEvidencePrivacy.ps1");
            StringAssert.Contains(source, "retention-days: 30");
            StringAssert.Contains(source, "if-no-files-found: error");
            StringAssert.Contains(source, "artifacts/openvino/evidence/*.json");
            Assert.IsFalse(source.Contains("TestResults/**", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.trx", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.log", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.bin", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("*.xml", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void PrivacyVerifierAcceptsTheClosedDeterministicEvidenceSchema()
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidencePath = Path.Combine(temporary.Path, "evidence.json");
        File.WriteAllText(evidencePath, ValidEvidence());

        ScriptResult result = RunPrivacyVerifier(evidencePath);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.AreEqual("evidence_privacy_valid", result.StandardOutput.Trim());
    }

    [TestMethod]
    [DataRow("absolute-path")]
    [DataRow("unc-path")]
    [DataRow("username")]
    [DataRow("hostname")]
    [DataRow("prompt")]
    [DataRow("generated-text")]
    [DataRow("environment")]
    [DataRow("secret")]
    [DataRow("stdout")]
    [DataRow("stderr")]
    [DataRow("model-bytes")]
    [DataRow("unexpected-nested")]
    [DataRow("duplicate")]
    [DataRow("noncanonical-order")]
    [DataRow("zero-test-count")]
    [DataRow("wrong-type")]
    [DataRow("performance-overflow")]
    public void PrivacyVerifierRejectsHostileEvidence(string hostileCase)
    {
        using TemporaryDirectory temporary = TemporaryDirectory.Create();
        string evidencePath = Path.Combine(temporary.Path, "hostile.json");
        File.WriteAllText(evidencePath, HostileEvidence(hostileCase));

        ScriptResult result = RunPrivacyVerifier(evidencePath);

        Assert.AreNotEqual(0, result.ExitCode, hostileCase);
        Assert.AreEqual("evidence_privacy_invalid", result.StandardOutput.Trim());
    }

    private static string HostileEvidence(string hostileCase)
    {
        string valid = ValidEvidence();
        return hostileCase switch
        {
            "absolute-path" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"C:\\\\Users\\\\alice\\\\model\"",
                StringComparison.Ordinal),
            "unc-path" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"\\\\\\\\server\\\\share\"",
                StringComparison.Ordinal),
            "username" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"username=alice\"",
                StringComparison.Ordinal),
            "hostname" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"hostname=WORKBOOK05\"",
                StringComparison.Ordinal),
            "prompt" => AddRootField(valid, "\"prompt\":\"private question\""),
            "generated-text" => AddRootField(valid, "\"generatedText\":\"private answer\""),
            "environment" => AddRootField(valid, "\"environment\":{\"PATH\":\"private\"}"),
            "secret" => AddRootField(valid, "\"secret\":\"token-value\""),
            "stdout" => AddRootField(valid, "\"stdout\":\"raw native output\""),
            "stderr" => AddRootField(valid, "\"stderr\":\"raw native error\""),
            "model-bytes" => AddRootField(valid, "\"modelBytes\":\"AAECAwQ=\""),
            "unexpected-nested" => valid.Replace(
                "\"vendor\":\"Intel\"",
                "\"vendor\":\"Intel\",\"account\":\"alice\"",
                StringComparison.Ordinal),
            "duplicate" => valid.Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal),
            "noncanonical-order" => valid.Replace(
                "{\"schemaVersion\":1,\"evidenceKind\":\"ucl\"",
                "{\"evidenceKind\":\"ucl\",\"schemaVersion\":1",
                StringComparison.Ordinal),
            "zero-test-count" => valid.Replace(
                "\"contracts\":60",
                "\"contracts\":0",
                StringComparison.Ordinal),
            "wrong-type" => valid.Replace(
                "\"contracts\":60",
                "\"contracts\":true",
                StringComparison.Ordinal),
            "performance-overflow" => valid.Replace(
                "\"durationMillisecondsMaximum\":3000",
                "\"durationMillisecondsMaximum\":600001",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(hostileCase))
        };
    }

    private static string AddRootField(string json, string field) =>
        json[..^1] + "," + field + "}";

    private static string ValidEvidence() =>
        "{\"schemaVersion\":1," +
        "\"evidenceKind\":\"ucl\"," +
        "\"commitSha\":\"0123456789abcdef0123456789abcdef01234567\"," +
        "\"dependencyLockIdentities\":{" +
        "\"runtimeLockSha256\":\"1111111111111111111111111111111111111111111111111111111111111111\"," +
        "\"genAiLockSha256\":\"2222222222222222222222222222222222222222222222222222222222222222\"," +
        "\"tokenizersLockSha256\":\"3333333333333333333333333333333333333333333333333333333333333333\"}," +
        "\"fixtureManifestSha256\":\"4444444444444444444444444444444444444444444444444444444444444444\"," +
        "\"workerManifestSha256\":\"5555555555555555555555555555555555555555555555555555555555555555\"," +
        "\"requestedDevice\":\"CPU\"," +
        "\"actualExecutionDevices\":[\"CPU\"]," +
        "\"cpuIdentity\":{\"architecture\":\"X64\",\"vendor\":\"Intel\"}," +
        "\"runtimeBuildIdentity\":{" +
        "\"runtime\":\"2026.3.0-22451-8a17657b995-releases/2026/3\"," +
        "\"genAi\":\"2026.3.0.0-3277-bd8d6542e3c\"," +
        "\"tokenizers\":\"2026.3.0.0-703-183c6f25cda\"}," +
        "\"testCounts\":{\"contracts\":60,\"staticInspection\":177," +
        "\"nativeUnit\":7,\"workerClient\":13,\"processContainment\":40," +
        "\"appAdapter\":35,\"packageTamper\":5}," +
        "\"performanceAggregates\":{\"sampleCount\":3," +
        "\"durationMillisecondsMinimum\":1000,\"durationMillisecondsMedian\":2000," +
        "\"durationMillisecondsMaximum\":3000}," +
        "\"cancellationDisposition\":\"passed\"," +
        "\"cleanupDisposition\":\"zero_residue\"}";

    private static ScriptResult RunPrivacyVerifier(string evidencePath)
    {
        ProcessStartInfo start = new("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", RepoPath("scripts/openvino/Test-OpenVinoEvidencePrivacy.ps1"),
            "-EvidencePath", evidencePath
        })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ScriptResult(process.ExitCode, standardOutput, standardError);
    }

    private static IEnumerable<string> Workflows()
    {
        yield return RepoPath(".github/workflows/openvino-official-ci.yml");
        yield return RepoPath(".github/workflows/openvino-ucl-intel.yml");
    }

    private static string OfficialWorkflow() =>
        Normalize(File.ReadAllText(RepoPath(".github/workflows/openvino-official-ci.yml")));

    private static string UclWorkflow() =>
        Normalize(File.ReadAllText(RepoPath(".github/workflows/openvino-ucl-intel.yml")));

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string RepoPath(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(
                        directory.FullName,
                        "IBM Granite with TurboQuant (Intel).slnx")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GraniteEdgeAI.OpenVino.Evidence",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
