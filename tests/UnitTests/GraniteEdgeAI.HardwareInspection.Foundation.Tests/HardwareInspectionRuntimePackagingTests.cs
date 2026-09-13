using System.Diagnostics;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HardwareInspectionRuntimePackagingTests
{
    [TestMethod]
    public void RuntimeSpecificOutputHasAnExplicitVerifiedProbeCopyTarget()
    {
        string target = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "HardwareInspection.LlamaCppProbePackaging.targets"));

        StringAssert.Contains(
            target,
            "Name=\"CopyHardwareInspectionLlamaCppProbeToRuntimeOutput\"");
        StringAssert.Contains(
            target,
            "AfterTargets=\"CopyFilesToOutputDirectory\"");
        StringAssert.Contains(
            target,
            "DestinationFiles=\"@(_HardwareInspectionRuntimeOutput)\"");
        StringAssert.Contains(
            target,
            "Condition=\"'$(RuntimeIdentifier)' == ''\">$(OutDir)win-x64\\");
    }

    [TestMethod]
    public async Task RuntimeCopyTargetProducesAManifestVerifiedProbeClosure()
    {
        string repositoryRoot = FindRepositoryRoot();
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"hardware-runtime-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(operationRoot);
        try
        {
            string project = Path.Combine(
                repositoryRoot,
                "IBM Granite with TurboQuant (Intel)",
                "IBM Granite with TurboQuant (Intel).csproj");
            ProcessResult build = await RunAsync(
                ResolveDotNetHost(),
                "msbuild",
                project,
                "/nologo",
                "/v:minimal",
                "/t:CopyHardwareInspectionLlamaCppProbeToRuntimeOutput",
                "/p:Platform=x64",
                "/p:Configuration=Debug",
                $"/p:OutDir={Path.Combine(operationRoot, "framework")}{Path.DirectorySeparatorChar}",
                "/p:GenerateAppxPackageOnBuild=false");
            Assert.AreEqual(0, build.ExitCode, build.Output);

            string runtimeRoot = Path.Combine(
                operationRoot,
                "framework",
                "win-x64",
                "HardwareInspection");
            string probeRoot = Path.Combine(runtimeRoot, "LlamaCppProbe");
            string manifest = Path.Combine(runtimeRoot, "llamacpp-probe-manifest.json");
            Assert.IsTrue(File.Exists(manifest), manifest);
            Assert.IsTrue(
                Directory.EnumerateFiles(probeRoot).Any(),
                "The runtime-specific probe closure was empty.");

            ProcessResult verify = await RunAsync(
                "powershell.exe",
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                Path.Combine(
                    repositoryRoot,
                    "scripts",
                    "hardware-inspection",
                    "Test-LlamaCppProbeManifest.ps1"),
                "-ProbeDirectory",
                probeRoot,
                "-ManifestPath",
                manifest);
            Assert.AreEqual(0, verify.ExitCode, verify.Output);
        }
        finally
        {
            Directory.Delete(operationRoot, recursive: true);
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string executable,
        params string[] arguments)
    {
        ProcessStartInfo start = new(executable)
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
            throw new InvalidOperationException("Could not start verification process.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, output + error);
    }

    private static string ResolveDotNetHost() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host
            ? host
            : "dotnet";

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
