using System.Xml.Linq;

namespace GraniteEdgeAI.SecurityAudit.Tests;

[TestClass]
public sealed class PackageMembershipPolicyTests
{
    private static readonly string[] RemovedItemTypes =
        ["Compile", "Content", "EmbeddedResource", "None", "PRIResource", "Page"];

    [TestMethod]
    public void NormalAppExcludesEveryOptimizationDebugFixtureItemType()
    {
        XDocument project = LoadProject();
        string[] removed = project.Descendants()
            .Where(element =>
                RemovedItemTypes.Contains(element.Name.LocalName, StringComparer.Ordinal) &&
                string.Equals(
                    (string?)element.Attribute("Remove"),
                    @"Features\ModelOptimization\DebugFixtures\**",
                    StringComparison.Ordinal))
            .Select(element => element.Name.LocalName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(RemovedItemTypes, removed);
    }

    [TestMethod]
    public void EvidenceTargetUsesEvaluatedAppxPayloadInsteadOfBinEnumeration()
    {
        XDocument project = LoadProject();
        XElement target = project.Descendants()
            .Single(element =>
                element.Name.LocalName == "Target" &&
                (string?)element.Attribute("Name") == "WriteS1EvaluatedAppxMembership");

        Assert.AreEqual(
            "_ComputeAppxPackagePayload",
            (string?)target.Attribute("DependsOnTargets"));
        XElement write = target.Elements()
            .Single(element => element.Name.LocalName == "WriteLinesToFile");
        string lines = (string?)write.Attribute("Lines") ?? string.Empty;
        StringAssert.Contains(lines, "@(AppxPackagePayload");
        Assert.IsFalse(lines.Contains("bin", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ClosureGeneratorConsumesOnlyEvaluatedMembershipAndFailsClosed()
    {
        string script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "verification",
            "New-S1PackageClosure.ps1"));

        StringAssert.Contains(script, "$EvaluatedMembershipFile");
        StringAssert.Contains(script, "AppxPackagePayload");
        StringAssert.Contains(script, @"\.pdb$");
        StringAssert.Contains(script, @"\.appxrecipe$");
        StringAssert.Contains(script, "debugfixtures");
        Assert.IsFalse(script.Contains(
            "EnumerateFiles($Build",
            StringComparison.OrdinalIgnoreCase));
    }

    private static XDocument LoadProject() => XDocument.Load(Path.Combine(
        FindRepositoryRoot(),
        "IBM Granite with TurboQuant (Intel)",
        "IBM Granite with TurboQuant (Intel).csproj"));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
                (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                 File.Exists(Path.Combine(current.FullName, ".git"))))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("The repository root could not be resolved.");
    }
}
