using System.Text.RegularExpressions;
using System.Xml.Linq;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies actual project and source files implement the approved runtime,
/// test-runner and dependency-isolation decisions.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed partial class RuntimeDependencyPolicyTests
{
    [TestMethod]
    public void ProductionRuntimeProject_PinsApprovedManagedAndCpuBackendPackages()
    {
        XDocument project = LoadProject(RuntimeProjectPath());
        IReadOnlyDictionary<string, string> packages =
            ReadPackageReferences(project);

        Assert.AreEqual("0.27.0", packages["LLamaSharp"]);
        Assert.AreEqual("0.27.0", packages["LLamaSharp.Backend.Cpu"]);
    }

    [TestMethod]
    public void ProductionRuntimeProject_UsesOnlyExactNonFloatingPackageVersions()
    {
        IReadOnlyDictionary<string, string> packages =
            ReadPackageReferences(LoadProject(RuntimeProjectPath()));

        foreach ((string packageName, string version) in packages)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(version),
                $"{packageName} must declare an exact version.");
            Assert.IsFalse(
                version.Contains('*', StringComparison.Ordinal) ||
                version.Contains('[', StringComparison.Ordinal) ||
                version.Contains(']', StringComparison.Ordinal) ||
                version.Contains('(', StringComparison.Ordinal) ||
                version.Contains(')', StringComparison.Ordinal) ||
                version.Contains(',', StringComparison.Ordinal),
                $"{packageName} uses a floating or ranged version: {version}");
            Assert.IsTrue(
                Regex.IsMatch(version, "^[0-9]+\\.[0-9]+\\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?$"),
                $"{packageName} version is not an exact semantic version: {version}");
        }
    }

    [TestMethod]
    public void ProductionRuntimeProject_DoesNotReferenceGpuOrTurboQuantDependencies()
    {
        XDocument project = LoadProject(RuntimeProjectPath());
        string[] includes = ReadAllIncludes(project);
        string[] forbiddenFragments =
        {
            "LLamaSharp.Backend.Cuda",
            "LLamaSharp.Backend.Vulkan",
            "TurboQuant"
        };

        foreach (string fragment in forbiddenFragments)
        {
            Assert.IsFalse(
                includes.Any(include => include.Contains(
                    fragment,
                    StringComparison.OrdinalIgnoreCase)),
                $"CPU production runtime unexpectedly references {fragment}.");
        }
    }

    [TestMethod]
    public void WinUiApplicationProject_DoesNotDirectlyReferenceLlamaSharpPackages()
    {
        XDocument application = LoadProject(ApplicationProjectPath());
        string[] includes = ReadAllIncludes(application);

        Assert.IsFalse(
            includes.Any(include => include.Contains(
                "LLamaSharp",
                StringComparison.OrdinalIgnoreCase)),
            "The WinUI application must consume Model Inspection through its " +
            "owned runtime/worker boundary, not direct LLamaSharp packages.");
    }

    [TestMethod]
    public void DeterministicTestProject_RemainsConfiguredForMicrosoftTestingPlatform()
    {
        XDocument project = LoadProject(DeterministicTestProjectPath());

        AssertPropertyEquals(project, "OutputType", "Exe");
        AssertPropertyEquals(project, "IsTestProject", "true");
        AssertPropertyEquals(project, "EnableMSTestRunner", "true");
        AssertPropertyEquals(
            project,
            "TestingPlatformDotnetTestSupport",
            "true");
        AssertPropertyEquals(
            project,
            "TestingPlatformShowTestsFailure",
            "true");
    }

    [TestMethod]
    public void RuntimeIdentitySource_RecordsMappedAndResearchCommitsSeparately()
    {
        string sourcePath = Path.Combine(
            RepositoryPaths.FindRoot(),
            "runtime",
            "GraniteEdgeAI.ModelInspection.LlamaSharp",
            "PinnedApplicationRuntime.cs");
        IReadOnlyDictionary<string, string> values =
            ReadConstStringValues(sourcePath);

        Assert.AreEqual("LLamaSharp", values["ManagedPackageName"]);
        Assert.AreEqual("0.27.0", values["ManagedPackageVersion"]);
        Assert.AreEqual(
            "LLamaSharp.Backend.Cpu",
            values["BackendPackageName"]);
        Assert.AreEqual("0.27.0", values["BackendPackageVersion"]);
        Assert.AreEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            values["ExpectedLlamaCppCommit"]);
        Assert.AreEqual("b9870", values["ResearchRuntimeTag"]);
        Assert.AreEqual(
            "2d973636e292ee6f75fadcf08d29cb33511f509f",
            values["ResearchRuntimeCommit"]);
        Assert.AreNotEqual(
            values["ExpectedLlamaCppCommit"],
            values["ResearchRuntimeCommit"]);
    }

    private static string RuntimeProjectPath()
    {
        return Path.Combine(
            RepositoryPaths.FindRoot(),
            "runtime",
            "GraniteEdgeAI.ModelInspection.LlamaSharp",
            "GraniteEdgeAI.ModelInspection.LlamaSharp.csproj");
    }

    private static string DeterministicTestProjectPath()
    {
        return Path.Combine(
            RepositoryPaths.FindRoot(),
            "tools",
            "ModelInspection.LlamaSharpSpike.Tests",
            "ModelInspection.LlamaSharpSpike.Tests.csproj");
    }

    private static string ApplicationProjectPath()
    {
        return Path.Combine(
            RepositoryPaths.FindRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj");
    }

    private static XDocument LoadProject(string path)
    {
        Assert.IsTrue(File.Exists(path), $"Expected project file not found: {path}");
        return XDocument.Load(path);
    }

    private static IReadOnlyDictionary<string, string> ReadPackageReferences(
        XDocument project)
    {
        return project
            .Descendants("PackageReference")
            .Select(element => new
            {
                Name = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value ??
                    element.Element("Version")?.Value
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Name))
            .ToDictionary(
                reference => reference.Name!,
                reference => reference.Version ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ReadAllIncludes(XDocument project)
    {
        return project
            .Descendants()
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static void AssertPropertyEquals(
        XDocument project,
        string propertyName,
        string expected)
    {
        string? actual = project
            .Descendants(propertyName)
            .Select(element => element.Value.Trim())
            .FirstOrDefault();

        Assert.IsNotNull(actual, $"Project property {propertyName} is missing.");
        Assert.IsTrue(
            string.Equals(
                expected,
                actual,
                StringComparison.OrdinalIgnoreCase),
            $"Unexpected value for project property {propertyName}. " +
            $"Expected '{expected}', actual '{actual}'.");
    }

    private static IReadOnlyDictionary<string, string> ReadConstStringValues(
        string sourcePath)
    {
        Assert.IsTrue(File.Exists(sourcePath), $"Runtime identity source not found: {sourcePath}");
        string source = File.ReadAllText(sourcePath);

        return ConstStringRegex()
            .Matches(source)
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => match.Groups["value"].Value,
                StringComparer.Ordinal);
    }

    [GeneratedRegex(
        "public\\s+const\\s+string\\s+(?<name>[A-Za-z0-9_]+)\\s*=\\s*\"(?<value>[^\"]*)\"\\s*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConstStringRegex();
}
