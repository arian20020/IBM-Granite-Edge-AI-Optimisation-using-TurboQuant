using System.Diagnostics;
using System.Security.Cryptography;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoAdditionalPackagingTargetTests
{
    [TestMethod]
    public async Task VerifiedConverterAndTurboQuantClosuresAreAccepted()
    {
        PackagingRole converter = Converter();
        PackagingRole turboQuant = TurboQuant();
        string converterStage = RequireConfiguredStage(converter);
        string turboQuantStage = RequireConfiguredStage(turboQuant);

        ProcessResult converterResult = await RunTargetAsync(
            converter, converterStage, ManifestDigest(converter, converterStage));
        ProcessResult turboQuantResult = await RunTargetAsync(
            turboQuant, turboQuantStage, ManifestDigest(turboQuant, turboQuantStage));

        Assert.AreEqual(0, converterResult.ExitCode, converterResult.Output);
        StringAssert.Contains(converterResult.Output, "converter_manifest_valid");
        StringAssert.Contains(converterResult.Output,
            "converter_manifest_digest_valid");
        Assert.AreEqual(0, turboQuantResult.ExitCode, turboQuantResult.Output);
        StringAssert.Contains(turboQuantResult.Output,
            "turboquant_worker_manifest_valid");
        StringAssert.Contains(turboQuantResult.Output,
            "turboquant_manifest_digest_valid");
    }

    [TestMethod]
    public async Task WrongOrNonLowercaseCallerDigestsFailClosed()
    {
        PackagingRole converter = Converter();
        PackagingRole turboQuant = TurboQuant();
        string converterStage = RequireConfiguredStage(converter);
        string turboQuantStage = RequireConfiguredStage(turboQuant);

        ProcessResult wrong = await RunTargetAsync(
            converter, converterStage, new string('0', 64));
        ProcessResult uppercase = await RunTargetAsync(
            turboQuant,
            turboQuantStage,
            ManifestDigest(turboQuant, turboQuantStage).ToUpperInvariant());

        Assert.AreNotEqual(0, wrong.ExitCode);
        StringAssert.Contains(wrong.Output, "converter_manifest_digest_mismatch");
        Assert.AreNotEqual(0, uppercase.ExitCode);
        StringAssert.Contains(uppercase.Output,
            "must be a caller-supplied lowercase SHA-256 digest");
    }

    [TestMethod]
    public async Task CrossRouteNativeEvidenceCannotDisableEitherClosure()
    {
        foreach (PackagingRole role in new[] { Converter(), TurboQuant() })
        {
            ProcessResult result = await RunTargetAsync(
                role,
                stage: null,
                digest: null,
                packagingRequired: false,
                crossRouteNativeEvidence: true);

            Assert.AreNotEqual(0, result.ExitCode, role.Name);
            StringAssert.Contains(result.Output,
                $"{role.RequiredProperty} cannot be false");
        }
    }

    [TestMethod]
    public async Task ExplicitSourceOnlyBuildMaySkipBothClosures()
    {
        foreach (PackagingRole role in new[] { Converter(), TurboQuant() })
        {
            ProcessResult result = await RunTargetAsync(
                role,
                stage: null,
                digest: null,
                packagingRequired: false,
                crossRouteNativeEvidence: false);

            Assert.AreEqual(0, result.ExitCode, result.Output);
        }
    }

    private static PackagingRole Converter() => new(
        "converter",
        "OpenVino.ConverterPackaging.targets",
        "VerifyAndPackageOpenVinoConverter",
        "OpenVinoConverterStageDirectory",
        "OpenVinoConverterManifestSha256",
        "OpenVinoConverterPackagingRequired",
        "converter-manifest.json",
        "GRANITE_OPENVINO_CONVERTER_STAGE");

    private static PackagingRole TurboQuant() => new(
        "TurboQuant",
        "OpenVino.TurboQuantPackaging.targets",
        "VerifyAndPackageOpenVinoTurboQuantWorker",
        "OpenVinoTurboQuantWorkerStageDirectory",
        "OpenVinoTurboQuantWorkerManifestSha256",
        "OpenVinoTurboQuantPackagingRequired",
        "worker-manifest.json",
        "OPENVINO_TURBOQUANT_WORKER_STAGE");

    private static async Task<ProcessResult> RunTargetAsync(
        PackagingRole role,
        string? stage,
        string? digest,
        bool packagingRequired = true,
        bool crossRouteNativeEvidence = false)
    {
        string operationRoot = Path.Combine(
            Path.GetTempPath(), $"ov-additional-target-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        string project = Path.Combine(operationRoot, "PackagingHarness.csproj");
        string target = Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            role.TargetFile);
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
                $"/t:{role.TargetName}",
                $"/p:{role.RequiredProperty}=" +
                    packagingRequired.ToString().ToLowerInvariant(),
                "/p:GenerateAppxPackageOnBuild=false",
                "/p:CrossRouteNativeEvidenceBuild=" +
                    crossRouteNativeEvidence.ToString().ToLowerInvariant()
            ];
            if (stage is not null)
            {
                arguments.Add($"/p:{role.StageProperty}={stage}");
            }
            if (digest is not null)
            {
                arguments.Add($"/p:{role.DigestProperty}={digest}");
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

    private static string ManifestDigest(PackagingRole role, string stage) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Path.Combine(stage, role.ManifestFile))))
        .ToLowerInvariant();

    private static string RequireConfiguredStage(PackagingRole role)
    {
        string? stage = Environment.GetEnvironmentVariable(role.StageVariable);
        if (string.IsNullOrWhiteSpace(stage) || !Directory.Exists(stage))
        {
            Assert.Inconclusive($"{role.StageVariable} is required.");
        }
        return Path.GetFullPath(stage!);
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

    private sealed record PackagingRole(
        string Name,
        string TargetFile,
        string TargetName,
        string StageProperty,
        string DigestProperty,
        string RequiredProperty,
        string ManifestFile,
        string StageVariable);

    private sealed record ProcessResult(int ExitCode, string Output);
}
