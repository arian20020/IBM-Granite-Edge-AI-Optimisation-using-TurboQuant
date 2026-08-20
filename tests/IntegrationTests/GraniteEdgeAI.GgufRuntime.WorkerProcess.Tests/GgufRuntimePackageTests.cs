using System.Xml.Linq;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufRuntimePackageTests
{
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
    public void PackagingRequiresExplicitLocalRuntimeAndContainsNoNetworkAcquisition()
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

        StringAssert.Contains(combined, "GgufRuntimeInputRoot");
        StringAssert.Contains(combined, "GgufRuntime\\Worker");
        StringAssert.Contains(combined, "GgufRuntime\\Cli");
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
