using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoWorkerPackagingTargetTests
{
    [TestMethod]
    public void OfficialWorkerBuildCanonicalizesFixtureBeforeNativeContainmentTests()
    {
        string script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "openvino",
            "Build-OpenVinoOfficialWorker.ps1"));

        StringAssert.Contains(
            script,
            "[OpenVinoOfficialBuildPath.NativePath]::GetFinalPath($fixtureRootPath)");
        StringAssert.Contains(
            script,
            "-DOFFICIAL_FIXTURE_PACKAGE=$fixturePackage");
    }

    [TestMethod]
    public void RuntimeSpecificOutputHasAnExplicitVerifiedWorkerCopyTarget()
    {
        string target = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "OpenVino.WorkerPackaging.targets"));

        StringAssert.Contains(
            target,
            "Name=\"CopyOpenVinoOfficialWorkerToRuntimeOutput\"");
        StringAssert.Contains(
            target,
            "AfterTargets=\"CopyFilesToOutputDirectory\"");
        StringAssert.Contains(
            target,
            "DestinationFiles=\"@(_OpenVinoOfficialWorkerRuntimeOutput)\"");
        StringAssert.Contains(
            target,
            "Condition=\"'$(RuntimeIdentifier)' == ''\">$(OutDir)win-x64\\");
        StringAssert.Contains(
            target,
            "<OpenVinoOfficialWorkerPackageRelativeDirectory>OVRuntime</OpenVinoOfficialWorkerPackageRelativeDirectory>");
    }

    [TestMethod]
    public void RuntimeSpecificOutputReceivesTheAppAssemblyWithApprovedWorkerIdentity()
    {
        string target = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "OpenVino.WorkerPackaging.targets"));

        StringAssert.Contains(
            target,
            "Name=\"CopyOpenVinoApprovedAppAssemblyToRuntimeOutput\"");
        StringAssert.Contains(
            target,
            "Condition=\"'$(RuntimeIdentifier)' == ''");
        StringAssert.Contains(target, "SourceFiles=\"$(TargetPath)\"");
        StringAssert.Contains(
            target,
            "DestinationFiles=\"$(_OpenVinoApprovedRuntimeAppAssembly)\"");
    }

    [TestMethod]
    public async Task RuntimeSpecificCopyPreservesTheApprovedWorkerIdentityInTheAppAssembly()
    {
        string stage = RequireOfficialStage();
        string digest = ManifestDigest(stage);
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"ov-runtime-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string assemblyName = $"ApprovedRuntimeApp{Guid.NewGuid():N}";
        string project = Path.Combine(operationRoot, "RuntimeIdentityHarness.csproj");
        string target = Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "OpenVino.WorkerPackaging.targets");
        await File.WriteAllTextAsync(
            project,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net8.0</TargetFramework><Platform>x64</Platform>" +
            $"<AssemblyName>{assemblyName}</AssemblyName>" +
            "<OutDir>$(MSBuildProjectDirectory)\\framework\\</OutDir>" +
            $"<OpenVinoOfficialWorkerStageDirectory>{System.Security.SecurityElement.Escape(stage)}</OpenVinoOfficialWorkerStageDirectory>" +
            $"<OpenVinoOfficialWorkerManifestSha256>{digest}</OpenVinoOfficialWorkerManifestSha256>" +
            "</PropertyGroup><ItemGroup>" +
            $"<AssemblyMetadata Include=\"OpenVinoOfficialWorkerManifestSha256\" Value=\"{digest}\" />" +
            "</ItemGroup>" +
            $"<Import Project=\"{System.Security.SecurityElement.Escape(target)}\" />" +
            "</Project>");
        try
        {
            ProcessResult result = await RunDotNetAsync("build", project, "/nologo");
            Assert.AreEqual(0, result.ExitCode, result.Output);

            string runtimeAssembly = Path.Combine(
                operationRoot,
                "framework",
                "win-x64",
                assemblyName + ".dll");
            Assert.IsTrue(File.Exists(runtimeAssembly), runtimeAssembly);
            Assert.IsTrue(
                System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(runtimeAssembly))
                    .Contains(digest, StringComparison.Ordinal),
                "The runtime assembly did not retain the approved worker digest.");
        }
        finally
        {
            Directory.Delete(operationRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LocalLaunchGateRejectsAnAssemblyWhoseWorkerManifestChanged()
    {
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"ov-local-launch-identity-{Guid.NewGuid():N}");
        string worker = Path.Combine(operationRoot, "worker");
        Directory.CreateDirectory(worker);
        string manifest = Path.Combine(worker, "worker-manifest.json");
        await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":1}");
        string digest = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(manifest)))
            .ToLowerInvariant();
        string assemblyName = $"PinnedReviewApp{Guid.NewGuid():N}";
        string project = Path.Combine(operationRoot, "PinnedReviewApp.csproj");
        await File.WriteAllTextAsync(
            project,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net8.0</TargetFramework>" +
            $"<AssemblyName>{assemblyName}</AssemblyName>" +
            "</PropertyGroup><ItemGroup>" +
            $"<AssemblyMetadata Include=\"OpenVinoOfficialWorkerManifestSha256\" Value=\"{digest}\" />" +
            "</ItemGroup></Project>");
        string gate = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "verification",
            "Test-GraniteOpenVinoLaunchIdentity.ps1");
        try
        {
            ProcessResult build = await RunDotNetAsync("build", project, "/nologo");
            Assert.AreEqual(0, build.ExitCode, build.Output);
            string assembly = Path.Combine(
                operationRoot,
                "bin",
                "Debug",
                "net8.0",
                assemblyName + ".dll");

            ProcessResult accepted = await RunPowerShellAsync(
                gate,
                "-ApplicationAssembly",
                assembly,
                "-WorkerDirectory",
                worker);
            Assert.AreEqual(0, accepted.ExitCode, accepted.Output);
            StringAssert.Contains(accepted.Output, "openvino_launch_identity_valid");

            await File.WriteAllTextAsync(manifest, "{\"schemaVersion\":2}");
            ProcessResult rejected = await RunPowerShellAsync(
                gate,
                "-ApplicationAssembly",
                assembly,
                "-WorkerDirectory",
                worker);
            Assert.AreNotEqual(0, rejected.ExitCode, rejected.Output);
            StringAssert.Contains(
                rejected.Output,
                "openvino_launch_identity_mismatch",
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                await DeleteDirectoryWithRetryAsync(operationRoot);
            }
        }
    }

    [TestMethod]
    public async Task ValidVerifiedClosureAndPinnedDigestAreAccepted()
    {
        string stage = RequireOfficialStage();
        string digest = ManifestDigest(stage);

        ProcessResult result = await RunTargetAsync(stage, digest);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "worker_manifest_valid");
        StringAssert.Contains(result.Output, "worker_manifest_digest_valid");
    }

    [TestMethod]
    public async Task WrongCallerDigestFailsAfterClosureVerification()
    {
        string stage = RequireOfficialStage();

        ProcessResult result = await RunTargetAsync(stage, new string('0', 64));

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.Output, "worker_manifest_digest_mismatch");
    }

    [TestMethod]
    public async Task MissingCallerStageOrDigestFailsBeforePackaging()
    {
        string stage = RequireOfficialStage();

        ProcessResult missingStage = await RunTargetAsync(
            stage: null,
            digest: ManifestDigest(stage));
        Assert.AreNotEqual(0, missingStage.ExitCode);
        StringAssert.Contains(
            missingStage.Output,
            "OpenVinoOfficialWorkerStageDirectory is required");

        ProcessResult missingDigest = await RunTargetAsync(stage, digest: null);
        Assert.AreNotEqual(0, missingDigest.ExitCode);
        StringAssert.Contains(
            missingDigest.Output,
            "OpenVinoOfficialWorkerManifestSha256 must be a caller-supplied");
    }

    [TestMethod]
    public async Task ExplicitNonPackagingBuildMaySkipTheOfficialClosure()
    {
        ProcessResult result = await RunTargetAsync(
            stage: null,
            digest: null,
            packagingRequired: false);

        Assert.AreEqual(0, result.ExitCode, result.Output);
    }

    [TestMethod]
    public async Task PackageGenerationCannotSkipTheOfficialClosure()
    {
        ProcessResult result = await RunTargetAsync(
            stage: null,
            digest: null,
            packagingRequired: false,
            generateAppxPackage: true);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(
            result.Output,
            "OpenVinoOfficialWorkerPackagingRequired cannot be false");
    }

    [TestMethod]
    public async Task RemanifestedTamperedClosureCannotReplaceCallerPinnedDigest()
    {
        string stage = RequireOfficialStage();
        string expectedDigest = ManifestDigest(stage);
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"ov-remanifest-test-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(stage, operationRoot);
            string changedPath = Path.Combine(
                operationRoot,
                "licenses",
                "Apache_license.txt");
            await File.AppendAllTextAsync(changedPath, "tampered-and-remanifested");
            await UpdateManifestEntryAsync(
                Path.Combine(operationRoot, "worker-manifest.json"),
                "licenses/Apache_license.txt",
                changedPath);

            ProcessResult result = await RunTargetAsync(
                operationRoot,
                expectedDigest);

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.Output, "worker_manifest_valid");
            StringAssert.Contains(result.Output, "worker_manifest_digest_mismatch");
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task MissingOrTamperedInventoryFailsClosed()
    {
        string stage = RequireOfficialStage();
        string expectedDigest = ManifestDigest(stage);
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"ov-packaging-test-{Guid.NewGuid():N}");
        try
        {
            string missing = Path.Combine(operationRoot, "missing");
            CopyDirectory(stage, missing);
            File.Delete(Path.Combine(missing, "tbb12.dll"));
            ProcessResult missingResult = await RunTargetAsync(missing, expectedDigest);
            Assert.AreNotEqual(0, missingResult.ExitCode);
            StringAssert.Contains(missingResult.Output, "worker_manifest_invalid");

            string tampered = Path.Combine(operationRoot, "tampered");
            CopyDirectory(stage, tampered);
            await File.AppendAllTextAsync(
                Path.Combine(tampered, "licenses", "Apache_license.txt"),
                "tampered");
            ProcessResult tamperedResult = await RunTargetAsync(tampered, expectedDigest);
            Assert.AreNotEqual(0, tamperedResult.ExitCode);
            StringAssert.Contains(tamperedResult.Output, "worker_manifest_invalid");
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    private static async Task<ProcessResult> RunTargetAsync(
        string? stage,
        string? digest,
        bool packagingRequired = true,
        bool generateAppxPackage = false)
    {
        string repositoryRoot = FindRepositoryRoot();
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"ov-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string project = Path.Combine(operationRoot, "PackagingHarness.csproj");
        string target = Path.Combine(
            repositoryRoot,
            "IBM Granite with TurboQuant (Intel)",
            "OpenVino.WorkerPackaging.targets");
        await File.WriteAllTextAsync(project,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net8.0</TargetFramework><Platform>x64</Platform>" +
            "</PropertyGroup>" +
            $"<Import Project=\"{System.Security.SecurityElement.Escape(target)}\" />" +
            "</Project>");
        try
        {
            List<string> arguments =
            [
                "msbuild",
                project,
                "/nologo",
                "/v:minimal",
                "/t:VerifyAndPackageOpenVinoOfficialWorker"
            ];
            if (stage is not null)
            {
                arguments.Add($"/p:OpenVinoOfficialWorkerStageDirectory={stage}");
            }
            if (digest is not null)
            {
                arguments.Add($"/p:OpenVinoOfficialWorkerManifestSha256={digest}");
            }
            if (!packagingRequired)
            {
                arguments.Add("/p:OpenVinoOfficialWorkerPackagingRequired=false");
                arguments.Add(
                    $"/p:GenerateAppxPackageOnBuild={generateAppxPackage.ToString().ToLowerInvariant()}");
            }
            ProcessStartInfo start = new(TestDotNetHost.Resolve())
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            using Process process = Process.Start(start) ??
                throw new InvalidOperationException("Could not start MSBuild.");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ProcessResult(process.ExitCode, output + error);
        }
        finally
        {
            Directory.Delete(operationRoot, recursive: true);
        }
    }

    private static async Task<ProcessResult> RunDotNetAsync(
        params string[] arguments)
    {
        ProcessStartInfo start = new(TestDotNetHost.Resolve())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start dotnet.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output + error);
    }

    private static async Task<ProcessResult> RunPowerShellAsync(
        string script,
        params string[] arguments)
    {
        ProcessStartInfo start = new("pwsh.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
                 {
                     "-NoLogo",
                     "-NoProfile",
                     "-NonInteractive",
                     "-ExecutionPolicy",
                     "Bypass",
                     "-File",
                     script
                 }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output + error);
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }
    }

    private static string RequireOfficialStage()
    {
        string? stage = Environment.GetEnvironmentVariable(
            "GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE");
        if (string.IsNullOrWhiteSpace(stage) || !Directory.Exists(stage))
        {
            Assert.Inconclusive(
                "GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE is required.");
        }
        return Path.GetFullPath(stage!);
    }

    private static string ManifestDigest(string stage) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(Path.Combine(stage, "worker-manifest.json"))))
        .ToLowerInvariant();

    private static async Task UpdateManifestEntryAsync(
        string manifestPath,
        string relativePath,
        string changedPath)
    {
        JsonObject manifest = JsonNode.Parse(
            await File.ReadAllTextAsync(manifestPath))!.AsObject();
        JsonObject entry = manifest["files"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(file => string.Equals(
                file["path"]!.GetValue<string>(),
                relativePath,
                StringComparison.Ordinal));
        entry["length"] = new FileInfo(changedPath).Length;
        entry["sha256"] = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(changedPath)))
            .ToLowerInvariant();
        await File.WriteAllTextAsync(
            manifestPath,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(
                     source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.GetFiles(
                     source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
