using System.Xml.Linq;

namespace GraniteEdgeAI.SecurityAudit.Tests;

[TestClass]
public sealed class PackageCapabilityPolicyTests
{
    private static readonly string[] ExpectedCapabilities =
        ["internetClient", "runFullTrust", "systemAIModels"];

    [TestMethod]
    public void ManifestDeclaresOnlyRequiredLocalAiAndVerifiedDownloadCapabilities()
    {
        string root = FindRepositoryRoot();
        XDocument manifest = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Package.appxmanifest"));
        XElement package = manifest.Root
            ?? throw new AssertFailedException("Package root is missing.");
        XElement capabilities = package.Elements()
            .Single(element => element.Name.LocalName == "Capabilities");
        string[] names = capabilities.Elements()
            .Select(element => (string?)element.Attribute("Name"))
            .Where(name => name is not null)
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            ExpectedCapabilities,
            names);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json"))
                && (Directory.Exists(Path.Combine(current.FullName, ".git"))
                    || File.Exists(Path.Combine(current.FullName, ".git"))))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("The repository root could not be resolved.");
    }
}
