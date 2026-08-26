using System.Xml.Linq;

namespace GraniteEdgeAI.GgufQuantization.Capabilities.Tests;

[TestClass]
public sealed class GgufQuantizerPackagingTests
{
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
