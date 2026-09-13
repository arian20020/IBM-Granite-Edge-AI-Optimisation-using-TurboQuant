using System.Xml.Linq;

namespace GraniteEdgeAI.GgufQuantization.Capabilities.Tests;

[TestClass]
public sealed class GgufQuantizerPackagingTests
{
    [TestMethod]
    public void VerifiedQuantizerBuildPinsTheResolvedMasmCompilerForCmake()
    {
        string script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "gguf-quantization",
            "Build-VerifiedQuantizer.ps1"));

        StringAssert.Contains(script, "ml64.exe");
        StringAssert.Contains(script, "CMAKE_ASM_COMPILER");
    }

    [TestMethod]
    public void ApplicationImportsASeparateFailClosedQuantizerPackage()
    {
        string root = FindRepositoryRoot();
        string targetPath = Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "GgufQuantization.WorkerPackaging.targets");
        Assert.IsTrue(File.Exists(targetPath));

        XDocument target = XDocument.Load(targetPath, LoadOptions.PreserveWhitespace);
        string text = File.ReadAllText(targetPath);
        StringAssert.Contains(text, "GgufQuantizerStageDirectory");
        StringAssert.Contains(text, "GgufQuantizerManifestSha256");
        StringAssert.Contains(text, "GgufQuantizerPackagingRequired");
        StringAssert.Contains(text, "Tools\\GgufQuantizer");
        StringAssert.Contains(text, "Test-GgufQuantizerPackage.ps1");
        Assert.IsTrue(target.Descendants("Error").Count() >= 2);

        string project = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj"));
        StringAssert.Contains(project, "GgufQuantization.WorkerPackaging.targets");
        StringAssert.Contains(project, "GraniteEdgeAI.GgufQuantization.WorkerClient.csproj");
    }

    [TestMethod]
    public void VerifiedAtomicBotPackageIsIncludedInDebugBuildWhenPresent()
    {
        string root = FindRepositoryRoot();
        string targetPath = Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "GgufQuantization.WorkerPackaging.targets");
        string text = File.ReadAllText(targetPath);

        StringAssert.Contains(text, "GgufQuantizerDevelopmentStageDirectory");
        StringAssert.Contains(
            text,
            @"C:\AI\granite-gguf-validation-20260907\atomicbot-quantizer-standalone-03");
        StringAssert.Contains(
            text,
            "2d039fdba954b7c9b17f4e552e7a69d5082f988861cc08db9bee3263f293b1b8");
        StringAssert.Contains(text, "'$(Configuration)' == 'Debug'");
        StringAssert.Contains(text, "Exists('$(GgufQuantizerDevelopmentStageDirectory)");
        StringAssert.Contains(text, "'$(Configuration)' == 'Release'");
        StringAssert.Contains(text, "GgufQuantizerStageDirectory is required");
        StringAssert.Contains(text, "GgufQuantizerManifestSha256 is required");
        XElement manifestDefault = target.Descendants("GgufQuantizerManifestSha256").Single(
            element => element.Attribute("Condition") is not null);
        StringAssert.Contains(manifestDefault.Attribute("Condition")!.Value, "'$(Configuration)' == 'Debug'");
        Assert.IsFalse(
            manifestDefault.Attribute("Condition")!.Value.Contains("Release", StringComparison.Ordinal),
            "Release must never inherit the DEBUG manifest default.");
        Assert.IsFalse(
            text.Contains("llama-quantize-3f7c29d-x64", StringComparison.Ordinal),
            "The build target still defaults to the legacy package.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
