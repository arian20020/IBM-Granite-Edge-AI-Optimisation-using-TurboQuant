using System.Xml.Linq;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelDownloadPackageContractTests
{
    [TestMethod]
    public void Manifest_DeclaresOnlyTheRequiredOutboundNetworkCapability()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string manifestPath = Path.Combine(root, "IBM Granite with TurboQuant (Intel)", "Package.appxmanifest");
        XDocument manifest = XDocument.Load(manifestPath);
        XNamespace foundation = manifest.Root!.Name.Namespace;
        string[] ordinaryCapabilities = manifest.Descendants()
            .Where(value => value.Name == foundation + "Capability")
            .Select(value => value.Attribute("Name")?.Value ?? string.Empty)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "internetClient" }, ordinaryCapabilities);
        Assert.IsFalse(manifest.ToString().Contains("privateNetworkClientServer", StringComparison.Ordinal));
        Assert.IsFalse(manifest.ToString().Contains("internetClientServer", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Project_DoesNotPackageModelOrPartialArtifacts()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "IBM Granite with TurboQuant (Intel)", "IBM Granite with TurboQuant (Intel).csproj"));
        string[] packagedModels = project.Descendants()
            .Select(value => value.Attribute("Include")?.Value)
            .Where(value => value is not null &&
                (value.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
                 value.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)))
            .Cast<string>()
            .ToArray();

        Assert.AreEqual(0, packagedModels.Length);
    }

    [TestMethod]
    public void CompletionNotificationContract_IsPathFree()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "IBM Granite with TurboQuant (Intel)", "Features", "ModelImport", "ModelDownload", "ModelDownloadCoordinator.cs"));
        int start = source.IndexOf("internal sealed class VerifiedModelAvailableEventArgs", StringComparison.Ordinal);
        int end = source.IndexOf("internal sealed class ModelDownloadCoordinator", start, StringComparison.Ordinal);
        string eventContract = source[start..end];

        Assert.IsFalse(eventContract.Contains("Path", StringComparison.Ordinal));
        Assert.IsFalse(eventContract.Contains("Uri", StringComparison.Ordinal));
    }
}
