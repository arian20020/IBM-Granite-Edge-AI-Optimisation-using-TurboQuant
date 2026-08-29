using System.Text;
using GraniteEdgeAI.ModelInspection.Contracts.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class M1R3DebugFixtureRootContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void MutationWithNewProductionRootIsDiscoveredAndRejectedFromRelease()
    {
        using TemporaryProject project = TemporaryProject.Create();

        IReadOnlyList<ProductionDebugFixtureRoot> roots =
            ProductionDebugFixtureInventory.Discover(project.ProjectPath);
        IReadOnlyList<string> failures =
            ProductionDebugFixtureInventory.Validate(project.ProjectPath);

        Assert.AreEqual(1, roots.Count);
        Assert.AreEqual("Features\\NewFeature\\DebugFixtures", roots[0].RelativePath);
        Assert.IsTrue(failures.Any(failure =>
            failure.Contains("Release|x64", StringComparison.Ordinal) &&
            failure.Contains("Leak.cs", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void EveryRealProductionRootIsExcludedOutsideApprovedDebugX64Closures()
    {
        string projectPath = Path.Combine(
            Root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj");
        IReadOnlyList<ProductionDebugFixtureRoot> roots =
            ProductionDebugFixtureInventory.Discover(projectPath);
        IReadOnlyList<string> failures =
            ProductionDebugFixtureInventory.Validate(projectPath);

        CollectionAssert.AreEqual(
            new[]
            {
                "Features\\HardwareInspection\\DebugFixtures",
                "Features\\ModelHardwareCompatibility\\DebugFixtures",
                "Features\\ModelInspection\\DebugFixtures",
                "Features\\ModelOptimization\\DebugFixtures",
                "Features\\Onboarding\\DebugFixtures",
            },
            roots.Select(root => root.RelativePath).ToArray());
        Assert.AreEqual(
            0,
            failures.Count,
            string.Join(Environment.NewLine, failures));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TemporaryProject : IDisposable
    {
        private readonly string _root;
        private TemporaryProject(string root, string projectPath)
        {
            _root = root;
            ProjectPath = projectPath;
        }

        internal string ProjectPath { get; }

        internal static TemporaryProject Create()
        {
            string root = Directory.CreateTempSubdirectory(
                "m1-r3-fixture-root-").FullName;
            string fixture = Path.Combine(
                root,
                "Features",
                "NewFeature",
                "DebugFixtures");
            Directory.CreateDirectory(fixture);
            File.WriteAllText(
                Path.Combine(fixture, "Leak.cs"),
                "internal sealed class Leak { }\n",
                new UTF8Encoding(false));
            string project = Path.Combine(root, "Application.csproj");
            File.WriteAllText(
                project,
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>\n",
                new UTF8Encoding(false));
            return new TemporaryProject(root, project);
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
