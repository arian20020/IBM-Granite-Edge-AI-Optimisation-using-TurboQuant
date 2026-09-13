using System.Diagnostics;
using System.Xml.Linq;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufRuntimePackageTests
{
    private static readonly string[] ExpectedVerificationInvocations =
    [
        "run --project tests\\ContractTests\\GraniteEdgeAI.GgufRuntime.Contracts.Tests\\GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj -c Release -- --progress off",
        "run --project tests\\UnitTests\\GraniteEdgeAI.GgufRuntime.Transport.Tests\\GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj -c Release -- --progress off",
        "run --project tests\\UnitTests\\GraniteEdgeAI.GgufRuntime.Capabilities.Tests\\GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj -c Release -- --progress off",
        "run --project tests\\UnitTests\\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj -c Release -- --progress off",
        "run --project tests\\UnitTests\\GraniteEdgeAI.GgufRuntime.Worker.Tests\\GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj -c Release -- --progress off",
        "test --project tests\\UnitTests\\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests\\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false",
        "run --project tests\\IntegrationTests\\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj -c Release -- --progress off"
    ];

    [TestMethod]
    public void ManagedRuntimePublishesUseTheVerifiedRevisionInsteadOfApplicationHead()
    {
        XDocument target = XDocument.Load(Path.Combine(FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "GgufRuntime.WorkerPackaging.targets"));
        Assert.AreEqual("3f984202a3f87c0bee42cac552febfac78916f9e",
            target.Descendants("_GgufRuntimeEvidenceSourceRevision").Single().Value);
        string[] publishes = target.Descendants("Exec")
            .Select(element => element.Attribute("Command")!.Value)
            .Where(command => command.Contains(" publish ", StringComparison.Ordinal)).ToArray();
        Assert.HasCount(2, publishes);
        foreach (string command in publishes)
        {
            StringAssert.Contains(command, "-p:SourceRevisionId=$(_GgufRuntimeEvidenceSourceRevision)");
        }
        Assert.IsFalse(target.Descendants("SourceRevisionId").Any(),
            "The application revision must not be overridden globally.");
    }

    [TestMethod]
    public async Task VerificationScriptUsesMtpWithoutAnAppHostForWorkerClientOnly()
    {
        string root = FindRepositoryRoot();
        string operationRoot = Path.Combine(
            Path.GetTempPath(), $"gguf-chat-verification-{Guid.NewGuid():N}");
        string commandDirectory = Path.Combine(operationRoot, "commands");
        string invocationLog = Path.Combine(operationRoot, "dotnet-invocations.log");
        Directory.CreateDirectory(commandDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(commandDirectory, "dotnet.cmd"),
            "@echo off\r\necho %*>> \"%GGUF_CHAT_VERIFICATION_DOTNET_LOG%\"\r\nexit /b 0\r\n");

        try
        {
            ProcessStartInfo start = new("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = root
            };
            foreach (string argument in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(root, "scripts", "gguf-runtime",
                    "Invoke-GgufChatVerification.ps1"),
                "-SkipApplicationBuild"
            })
            {
                start.ArgumentList.Add(argument);
            }
            start.Environment["GGUF_CHAT_VERIFICATION_DOTNET_LOG"] = invocationLog;
            start.Environment["PATH"] = commandDirectory + Path.PathSeparator +
                Environment.GetEnvironmentVariable("PATH");

            using Process process = Process.Start(start) ??
                throw new InvalidOperationException("Could not start the verification script.");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.AreEqual(0, process.ExitCode, output + error);
            CollectionAssert.AreEqual(
                ExpectedVerificationInvocations,
                File.ReadAllLines(invocationLog));
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
    public void VerificationBuildUsesReleaseOutputSeparateFromRunningDebugPreview()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "gguf-runtime",
            "Invoke-GgufChatVerification.ps1"));

        StringAssert.Contains(script, "-c Release");
        Assert.IsFalse(
            script.Contains("-c Debug", StringComparison.Ordinal),
            "Verification must not overwrite the running Debug preview executable.");
    }

    [TestMethod]
    public void RuntimePackageClosureUsesPowerShellSuccessStateInsteadOfStaleNativeExitCode()
    {
        string root = FindRepositoryRoot();
        string closure = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "gguf-runtime",
            "Test-GgufRuntimePackageClosure.ps1"));

        StringAssert.Contains(closure, "if (-not $?)");
        Assert.IsFalse(closure.Contains("if ($LASTEXITCODE -ne 0)",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void ApplicationSelectsPublishProfileOnlyWhenItExists()
    {
        string root = FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj"));
        XElement publishProfile = project
            .Descendants("PublishProfile")
            .Single();

        string condition = publishProfile.Attribute("Condition")?.Value ?? string.Empty;
        StringAssert.Contains(condition, "Exists(");
    }

    [TestMethod]
    public void PackagingPublishesTheOwnedAdapterAndContainsNoNetworkAcquisition()
    {
        string root = FindRepositoryRoot();
        string targetPath = Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "GgufRuntime.WorkerPackaging.targets");
        string generatorPath = Path.Combine(
            root,
            "scripts",
            "gguf-runtime",
            "New-GgufRuntimeManifest.ps1");
        string verifierPath = Path.Combine(
            root,
            "scripts",
            "gguf-runtime",
            "Test-GgufRuntimeManifest.ps1");
        string closurePath = Path.Combine(
            root,
            "scripts",
            "gguf-runtime",
            "Test-GgufRuntimePackageClosure.ps1");

        Assert.IsTrue(File.Exists(targetPath), "Packaging target is required.");
        Assert.IsTrue(File.Exists(generatorPath), "Manifest generator is required.");
        Assert.IsTrue(File.Exists(verifierPath), "Manifest verifier is required.");
        Assert.IsTrue(File.Exists(closurePath), "Closure verifier is required.");

        _ = XDocument.Load(targetPath);
        string combined = string.Join(
            Environment.NewLine,
            File.ReadAllText(targetPath),
            File.ReadAllText(generatorPath),
            File.ReadAllText(verifierPath),
            File.ReadAllText(closurePath));

        StringAssert.Contains(combined, "GraniteEdgeAI.GgufRuntime.NativeAdapter.csproj");
        StringAssert.Contains(
            combined,
            "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan");
        StringAssert.Contains(combined, "GgufRuntime\\Worker");
        StringAssert.Contains(combined, "GgufRuntime\\Adapter");
        Assert.IsFalse(combined.Contains("GgufRuntimeInputRoot", StringComparison.Ordinal));
        Assert.IsFalse(combined.Contains("llama-cli.exe", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("Invoke-WebRequest", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("Start-BitsTransfer", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("curl.exe", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
