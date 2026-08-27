using System.Diagnostics;
using System.IO.Compression;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class PackagingContractTests
{
    [TestMethod]
    public void ReleaseGateAssetsExist()
    {
        foreach (string relative in new[]
        {
            "scripts/openvino/Invoke-OpenVinoReleaseGate.ps1",
            "scripts/openvino/Test-OpenVinoCleanupInventory.ps1",
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "docs/evidence/openvino/README.md"
        })
        {
            Assert.IsTrue(File.Exists(RepoPath(relative)), relative);
        }
    }

    [TestMethod]
    public void OfficialPackagingUsesPinnedExplicitInventoryWithoutPythonOrWildcards()
    {
        string target = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets"));
        StringAssert.Contains(target, "OpenVinoOfficialWorkerManifestSha256");
        StringAssert.Contains(target, "worker_manifest_digest_valid");
        StringAssert.Contains(target, "OpenVino\\Official\\Worker");
        Assert.IsFalse(target.Contains("*.dll", StringComparison.Ordinal));
        Assert.IsFalse(target.Contains("python", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(target.Contains("site-packages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(target.Contains("TurboQuant", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ConverterAndTurboQuantPackagingAreSeparateAndDigestPinned()
    {
        string converter = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets"));
        StringAssert.Contains(converter, "OpenVinoConverterStageDirectory");
        StringAssert.Contains(converter, "OpenVinoConverterManifestSha256");
        StringAssert.Contains(converter, "Test-OpenVinoConverterWorkerManifest.ps1");
        StringAssert.Contains(converter, "OpenVino\\Converter\\Worker");
        Assert.IsFalse(converter.Contains("TurboQuant", StringComparison.Ordinal));
        Assert.IsFalse(converter.Contains("Official", StringComparison.Ordinal));

        string turboQuant = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets"));
        StringAssert.Contains(turboQuant, "OpenVinoTurboQuantWorkerStageDirectory");
        StringAssert.Contains(turboQuant, "OpenVinoTurboQuantWorkerManifestSha256");
        StringAssert.Contains(turboQuant,
            "Test-OpenVinoTurboQuantWorkerManifest.ps1");
        StringAssert.Contains(turboQuant, "OpenVino\\TurboQuant\\Worker");
        Assert.IsFalse(turboQuant.Contains("Converter", StringComparison.Ordinal));
        Assert.IsFalse(turboQuant.Contains("Official", StringComparison.Ordinal));

        string application = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj"));
        StringAssert.Contains(application, "OpenVino.ConverterPackaging.targets");
        StringAssert.Contains(application, "OpenVino.TurboQuantPackaging.targets");
        StringAssert.Contains(application,
            "Include=\"OpenVinoConverterManifestSha256\"");
        StringAssert.Contains(application,
            "Include=\"OpenVinoTurboQuantWorkerManifestSha256\"");
        StringAssert.Contains(converter, "CrossRouteNativeEvidenceBuild");
        StringAssert.Contains(turboQuant, "CrossRouteNativeEvidenceBuild");

        string unitTests = File.ReadAllText(RepoPath(
            "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj"));
        StringAssert.Contains(unitTests,
            "Condition=\"'$(CrossRouteNativeEvidenceBuild)' != 'true'\"");
        StringAssert.Contains(unitTests,
            "OpenVinoTurboQuantPackagingRequired=false");
    }

    [TestMethod]
    public void NativeRuntimeClosesAmbientDllResolutionBeforeOpenVinoLoad()
    {
        string official = File.ReadAllText(RepoPath(
            "workers/OpenVinoOfficial.Worker/src/runtime_evidence.cpp"));
        StringAssert.Contains(official, "SetDefaultDllDirectories");
        StringAssert.Contains(official, "SetDllDirectoryW(L\"\")");
        StringAssert.Contains(official, "AddDllDirectory(root.c_str())");
        StringAssert.Contains(official, "LOAD_LIBRARY_SEARCH_SYSTEM32");

        string turboCmake = File.ReadAllText(RepoPath(
            "workers/OpenVinoTurboQuant.Worker/CMakeLists.txt"));
        StringAssert.Contains(turboCmake, "runtime_evidence.cpp");
        StringAssert.Contains(turboCmake, "/DELAYLOAD:openvino.dll");
    }

    [TestMethod]
    public void ReleaseGateOrdersEveryRequiredLayerAndFailsClosedOnExternalEvidence()
    {
        string script = File.ReadAllText(RepoPath(
            "scripts/openvino/Invoke-OpenVinoReleaseGate.ps1"));
        string[] ordered =
        [
            "dependency-locks",
            "contracts",
            "inspection",
            "official-native",
            "converter",
            "optimization",
            "turboquant",
            "app-accessibility",
            "gguf-regression",
            "release-package",
            "hosted-evidence",
            "ucl-evidence",
            "artifact-privacy",
            "cleanup-inventory",
            "traceability"
        ];
        int previous = -1;
        foreach (string gate in ordered)
        {
            int current = script.IndexOf($"'{gate}'", StringComparison.Ordinal);
            Assert.IsGreaterThan(previous, current, gate);
            previous = current;
        }
        StringAssert.Contains(script, "openvino_release_blocked");
        StringAssert.Contains(script, "OPENVINO_UCL_EVIDENCE_ROOT");
        StringAssert.Contains(script, "OPENVINO_HOSTED_EVIDENCE_ROOT");
        foreach (string required in new[]
        {
            "GRANITE_OPENVINO_EVIDENCE_COMMIT",
            "GRANITE_OPENVINO_OPERATION_ROOT",
            "GRANITE_OPENVINO_MODEL_SHA256",
            "GRANITE_OPENVINO_MODEL_LENGTH",
            "GRANITE_OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256",
            "GRANITE_OPENVINO_HOSTED_CPU_EVIDENCE_PATH",
            "GRANITE_OPENVINO_UCL_CPU_EVIDENCE_PATH",
            "GRANITE_OPENVINO_UCL_TURBOQUANT_EVIDENCE_PATH",
            "GRANITE_OPENVINO_TURBOQUANT_CAMPAIGN_EVIDENCE_PATH",
            "Test-OpenVinoTurboQuantActivation.ps1",
            "securityReviewApproved",
            "licenseReviewApproved",
            "matchedOfficialBaseline",
            "GTQ-QUALITY-RUBRIC-v1"
        })
        {
            StringAssert.Contains(script, required);
        }
    }

    [TestMethod]
    public void ReleasePackageGateBuildsTheX64AppWithAnExplicitRuntimeIdentifier()
    {
        string script = File.ReadAllText(RepoPath(
            "scripts/openvino/Invoke-OpenVinoReleaseGate.ps1"));
        StringAssert.Contains(
            script,
            "IBM Granite with TurboQuant (Intel)\\IBM Granite with TurboQuant (Intel).csproj");
        StringAssert.Contains(script, "--runtime win-x64");
        Assert.IsFalse(script.Contains(
            "dotnet build (Join-Path $repository 'IBM Granite with TurboQuant (Intel).slnx')",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReleaseGateRevalidatesTheImmutableCleanCandidateAfterAllGates()
    {
        string script = File.ReadAllText(RepoPath(
            "scripts/openvino/Invoke-OpenVinoReleaseGate.ps1"));
        Assert.IsGreaterThanOrEqualTo(2, Count(script, "git -C $repository rev-parse HEAD"));
        Assert.IsGreaterThanOrEqualTo(2, Count(script, "git -C $repository status --porcelain"));
        int finalCheck = script.LastIndexOf("release-candidate-changed", StringComparison.Ordinal);
        int accepted = script.LastIndexOf("openvino_release_accepted", StringComparison.Ordinal);
        Assert.IsGreaterThan(finalCheck, accepted);
    }

    [TestMethod]
    public void PrivacyArchiveCopyAndCleanupRootAreExplicitlyBounded()
    {
        string privacy = File.ReadAllText(RepoPath(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1"));
        StringAssert.Contains(privacy, "$entry.Length - $copied");
        StringAssert.Contains(privacy, "$input.Read(");
        Assert.IsFalse(privacy.Contains("$input.CopyTo($output)", StringComparison.Ordinal));

        string cleanup = File.ReadAllText(RepoPath(
            "scripts/openvino/Test-OpenVinoCleanupInventory.ps1"));
        StringAssert.Contains(cleanup, "[Parameter(Mandatory)]");
        Assert.IsFalse(cleanup.Contains("[IO.Path]::GetTempPath()", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ArtifactPrivacyAcceptsBoundedTypedEvidenceAndRejectsSensitiveContent()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string evidence = Path.Combine(directory.Path, "evidence.json");
        await File.WriteAllTextAsync(evidence,
            "{\"schemaVersion\":1,\"commitSha\":\"0123456789abcdef0123456789abcdef01234567\",\"result\":\"passed\"}");
        ProcessResult valid = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);
        Assert.AreEqual(0, valid.ExitCode, valid.Output);
        Assert.AreEqual("artifact_privacy_valid", valid.Output.Trim());

        await File.WriteAllTextAsync(evidence,
            "{\"schemaVersion\":1,\"prompt\":\"private user text\"}");
        ProcessResult hostile = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);
        Assert.AreNotEqual(0, hostile.ExitCode);
        Assert.AreEqual("artifact_privacy_invalid", hostile.Output.Trim());
    }

    [TestMethod]
    [DataRow("{\"schemaVersion\":1,\"modelPath\":\"C:\\\\Users\\\\person\\\\model.xml\"}")]
    [DataRow("{\"schemaVersion\":1,\"generatedText\":\"private output\"}")]
    [DataRow("{\"schemaVersion\":1,\"environment\":{\"PATH\":\"value\"}}")]
    [DataRow("{\"schemaVersion\":1,\"stderr\":\"native diagnostic\"}")]
    [DataRow("{\"schemaVersion\":1,\"credential\":\"secret value\"}")]
    [DataRow("{\"schemaVersion\":1,\"tokenizerBytes\":\"AAEC\"}")]
    public async Task ArtifactPrivacyRejectsEverySensitiveEvidenceClass(string payload)
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "evidence.json"), payload);

        ProcessResult result = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual("artifact_privacy_invalid", result.Output.Trim());
    }

    [TestMethod]
    public async Task ArtifactPrivacyRejectsUntypedTextAndUnknownBinaryPayloads()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "run.log"),
            "the generated response was private");
        ProcessResult log = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);
        Assert.AreNotEqual(0, log.ExitCode);

        File.Delete(Path.Combine(directory.Path, "run.log"));
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "model.bin"), [1, 2, 3]);
        ProcessResult binary = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);
        Assert.AreNotEqual(0, binary.ExitCode);
    }

    [TestMethod]
    public async Task ArtifactPrivacyScansArchivesAndRejectsTraversalOrHiddenPromptData()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        string archivePath = Path.Combine(directory.Path, "evidence.zip");
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("safe/evidence.json");
            await using StreamWriter writer = new(entry.Open());
            await writer.WriteAsync("{\"schemaVersion\":1,\"result\":\"passed\"}");
        }
        ProcessResult safe = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);
        Assert.AreEqual(0, safe.ExitCode, safe.Output);

        File.Delete(archivePath);
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("../prompt.json");
            await using StreamWriter writer = new(entry.Open());
            await writer.WriteAsync("{\"prompt\":\"private\"}");
        }
        ProcessResult hostile = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1",
            "-ArtifactRoot", directory.Path);
        Assert.AreNotEqual(0, hostile.ExitCode);
        Assert.AreEqual("artifact_privacy_invalid", hostile.Output.Trim());
    }

    [TestMethod]
    public async Task CleanupInventoryRejectsOwnedStagingResidue()
    {
        using TemporaryDirectory directory = TemporaryDirectory.Create();
        using TemporaryDirectory official = TemporaryDirectory.Create();
        using TemporaryDirectory converter = TemporaryDirectory.Create();
        using TemporaryDirectory turboQuant = TemporaryDirectory.Create();
        ProcessResult clean = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoCleanupInventory.ps1",
            "-OperationRoot", directory.Path,
            "-OfficialWorkerRoot", official.Path,
            "-ConverterRoot", converter.Path,
            "-TurboQuantRoot", turboQuant.Path);
        Assert.AreEqual(0, clean.ExitCode, clean.Output);
        Assert.AreEqual("openvino_cleanup_valid", clean.Output.Trim());

        Directory.CreateDirectory(Path.Combine(
            directory.Path, ".granite-openvino-test.staging"));
        ProcessResult residue = await RunPowerShellAsync(
            "scripts/openvino/Test-OpenVinoCleanupInventory.ps1",
            "-OperationRoot", directory.Path,
            "-OfficialWorkerRoot", official.Path,
            "-ConverterRoot", converter.Path,
            "-TurboQuantRoot", turboQuant.Path);
        Assert.AreNotEqual(0, residue.ExitCode);
        Assert.AreEqual("openvino_cleanup_invalid", residue.Output.Trim());
    }

    private static async Task<ProcessResult> RunPowerShellAsync(
        string relativeScript,
        params string[] arguments)
    {
        ProcessStartInfo start = new("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive",
            "-ExecutionPolicy", "Bypass", "-File", RepoPath(relativeScript)
        }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode,
            (standardOutput + standardError).Trim());
    }

    private static string RepoPath(string relative) => Path.Combine(
        FindRepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static int Count(string value, string token)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(token, index,
                 StringComparison.Ordinal)) >= 0; index += token.Length)
        {
            count++;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"ov-release-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
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
