using System.Xml.Linq;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that repository project files implement the runtime dependency
/// decision recorded by ADR-001.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class RuntimeDependencyPolicyTests
{
    /// <summary>
    /// Checks the actual spike project rather than comparing compile-time
    /// constants with the same literal values.
    /// </summary>
    [TestMethod]
    public void SpikeProject_PinsApprovedManagedAndCpuBackendPackages()
    {
        string repositoryRoot = FindRepositoryRoot();
        string spikeProjectPath = Path.Combine(
            repositoryRoot,
            "tools",
            "ModelInspection.LlamaSharpSpike",
            "ModelInspection.LlamaSharpSpike.csproj");

        IReadOnlyDictionary<string, string> packageReferences =
            ReadPackageReferences(spikeProjectPath);

        Assert.IsTrue(
            packageReferences.TryGetValue(
                PinnedApplicationRuntime.ManagedPackageName,
                out string? managedVersion),
            $"{PinnedApplicationRuntime.ManagedPackageName} is missing from " +
            "the LLamaSharp feasibility project.");

        Assert.AreEqual(
            PinnedApplicationRuntime.ManagedPackageVersion,
            managedVersion);

        Assert.IsTrue(
            packageReferences.TryGetValue(
                PinnedApplicationRuntime.BackendPackageName,
                out string? backendVersion),
            $"{PinnedApplicationRuntime.BackendPackageName} is missing from " +
            "the LLamaSharp feasibility project.");

        Assert.AreEqual(
            PinnedApplicationRuntime.BackendPackageVersion,
            backendVersion);
    }

    /// <summary>
    /// Protects the architectural boundary that keeps experimental native
    /// dependencies out of the WinUI application project.
    /// </summary>
    [TestMethod]
    public void WinUiApplicationProject_DoesNotReferenceLlamaSharpPackages()
    {
        string repositoryRoot = FindRepositoryRoot();
        string applicationProjectPath = Path.Combine(
            repositoryRoot,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj");

        IReadOnlyDictionary<string, string> packageReferences =
            ReadPackageReferences(applicationProjectPath);

        string[] llamaSharpReferences = packageReferences.Keys
            .Where(
                packageName => packageName.StartsWith(
                    "LLamaSharp",
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                packageName => packageName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.AreEqual(
            0,
            llamaSharpReferences.Length,
            "LLamaSharp dependencies must remain isolated under tools/ " +
            "until the feasibility gates pass. Unexpected references: " +
            string.Join(", ", llamaSharpReferences));
    }

    private static IReadOnlyDictionary<string, string>
        ReadPackageReferences(string projectPath)
    {
        Assert.IsTrue(
            File.Exists(projectPath),
            $"Expected project file was not found: {projectPath}");

        XDocument project = XDocument.Load(projectPath);

        return project
            .Descendants("PackageReference")
            .Select(
                element => new
                {
                    Name = element.Attribute("Include")?.Value,
                    Version = element.Attribute("Version")?.Value ??
                        element.Element("Version")?.Value
                })
            .Where(
                reference =>
                    !string.IsNullOrWhiteSpace(reference.Name))
            .ToDictionary(
                reference => reference.Name!,
                reference => reference.Version ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? currentDirectory =
            new(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            bool hasGlobalJson = File.Exists(
                Path.Combine(
                    currentDirectory.FullName,
                    "global.json"));

            bool hasToolsFolder = Directory.Exists(
                Path.Combine(
                    currentDirectory.FullName,
                    "tools"));

            bool hasApplicationFolder = Directory.Exists(
                Path.Combine(
                    currentDirectory.FullName,
                    "IBM Granite with TurboQuant (Intel)"));

            if (hasGlobalJson &&
                hasToolsFolder &&
                hasApplicationFolder)
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            "The repository root could not be located from the test output " +
            "directory.");
    }
}
