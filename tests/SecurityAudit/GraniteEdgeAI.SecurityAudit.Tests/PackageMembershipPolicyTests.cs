using System.Xml.Linq;
using System.Diagnostics;

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
            "Build;_ComputeAppxPackagePayload",
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
        StringAssert.Contains(script, "ASCII and UTF-16LE privacy scan");
        StringAssert.Contains(script, "assembly-membership");
        StringAssert.Contains(script, "$approvedPaths.Contains");
        StringAssert.Contains(script, "package member contains private content");
        Assert.IsFalse(script.Contains(
            "EnumerateFiles($Build",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ReleaseBuildSuppressesPrivatePdbPathRecords()
    {
        XDocument properties = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "Directory.Build.props"));
        XElement release = properties.Root!.Elements()
            .Single(element =>
                element.Name.LocalName == "PropertyGroup" &&
                ((string?)element.Attribute("Condition"))?.Contains(
                    "$(Configuration)",
                    StringComparison.Ordinal) == true);

        Assert.AreEqual("none", release.Elements().Single(
            element => element.Name.LocalName == "DebugType").Value);
        Assert.AreEqual("false", release.Elements().Single(
            element => element.Name.LocalName == "DebugSymbols").Value);
    }

    [TestMethod]
    public void ClosureGeneratorBehaviorallyRejectsUnapprovedBinaryPath()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string payload = Path.Combine(root, "payload.dll");
            string membership = Path.Combine(root, "membership.txt");
            string previous = Path.Combine(root, "previous.json");
            string output = Path.Combine(root, "output.json");
            File.WriteAllBytes(payload, [0x4d, 0x5a]);
            File.WriteAllText(membership, $"Unknown.dll|{payload}{Environment.NewLine}");
            File.WriteAllText(previous, "{\"entries\":[{\"path\":\"approved.txt\"}]}");

            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(Path.Combine(
                FindRepositoryRoot(), "scripts", "verification", "New-S1PackageClosure.ps1"));
            Add(start, "-EvaluatedMembershipFile", membership);
            Add(start, "-OutputFile", output);
            Add(start, "-ImplementationSubjectCommit", new string('a', 40));
            Add(start, "-ImplementationSubjectTree", new string('b', 40));
            Add(start, "-BaseCommit", new string('c', 40));
            Add(start, "-BaseTree", new string('d', 40));
            Add(start, "-BuildCommandIdentity", "hostile-test");
            Add(start, "-PreviousClosureFile", previous);

            using Process process = Process.Start(start)!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreNotEqual(0, process.ExitCode);
            StringAssert.Contains(error, "failed closed policy");
            Assert.IsFalse(File.Exists(output));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Add(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
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
