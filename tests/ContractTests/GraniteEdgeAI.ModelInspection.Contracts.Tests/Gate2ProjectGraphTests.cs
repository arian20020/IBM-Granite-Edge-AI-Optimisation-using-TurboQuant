using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Protects the project boundaries approved for the Gate 2 worker and process
/// adapter before any production process implementation is added.
/// </summary>
[TestClass]
[TestCategory("Architecture")]
public sealed class Gate2ProjectGraphTests
{
    private static readonly string Root = RepositoryRoot.Find();

    /// <summary>
    /// Requires every approved Gate 2 project to exist at its documented path.
    /// The first red run intentionally fails until the project shells are added.
    /// </summary>
    [TestMethod]
    public void ApprovedGate2ProjectsExist()
    {
        string[] paths =
        [
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj",
            "workers/GraniteEdgeAI.ModelInspection.Worker/GraniteEdgeAI.ModelInspection.Worker.csproj",
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj",
            "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/GraniteEdgeAI.ModelInspection.ProtocolTestWorker.csproj",
            "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj"
        ];

        foreach (string path in paths)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(Root, path)),
                $"Missing {path}");
        }
    }

    /// <summary>
    /// Enforces the dependency direction agreed in the design. The transport
    /// project remains framework-neutral, while the worker and application-side
    /// client may depend only on Contracts and Transport.
    /// </summary>
    [TestMethod]
    public void ProductionProjectReferencesFollowApprovedDirection()
    {
        ProjectGraphAssert.HasExactly(
            Root,
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj");
        ProjectGraphAssert.HasExactly(
            Root,
            "workers/GraniteEdgeAI.ModelInspection.Worker/GraniteEdgeAI.ModelInspection.Worker.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj");
        ProjectGraphAssert.HasExactly(
            Root,
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj");
    }

    /// <summary>
    /// Locates the checkout without assuming a developer-specific directory.
    /// </summary>
    private static class RepositoryRoot
    {
        public static string Find()
        {
            string[] startingPaths =
            [
                AppContext.BaseDirectory,
                Environment.CurrentDirectory,
                Path.GetDirectoryName(
                    typeof(Gate2ProjectGraphTests).Assembly.Location)
                    ?? AppContext.BaseDirectory
            ];

            foreach (string startingPath in startingPaths)
            {
                DirectoryInfo? directory = new(startingPath);

                while (directory is not null)
                {
                    string globalJsonPath = Path.Combine(
                        directory.FullName,
                        "global.json");
                    string solutionPath = Path.Combine(
                        directory.FullName,
                        "IBM Granite with TurboQuant (Intel).slnx");

                    if (File.Exists(globalJsonPath) && File.Exists(solutionPath))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException(
                "Could not locate the repository root containing global.json and the solution file.");
        }
    }

    /// <summary>
    /// Reads project XML and compares the complete, normalized project-reference
    /// set. Exact comparison prevents hidden dependencies from entering later.
    /// </summary>
    private static class ProjectGraphAssert
    {
        public static void HasExactly(
            string repositoryRoot,
            string projectPath,
            params string[] expectedReferences)
        {
            string absoluteProjectPath = Path.Combine(repositoryRoot, projectPath);
            Assert.IsTrue(
                File.Exists(absoluteProjectPath),
                $"Missing project file {projectPath}");

            XDocument project = XDocument.Load(absoluteProjectPath);
            string projectDirectory = Path.GetDirectoryName(absoluteProjectPath)
                ?? throw new InvalidOperationException(
                    $"Project path has no parent directory: {projectPath}");

            string[] actualReferences = project
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFullPath(include!, projectDirectory))
                .Select(path => Path.GetRelativePath(repositoryRoot, path))
                .Select(NormalizeSeparators)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            string[] normalizedExpected = expectedReferences
                .Select(NormalizeSeparators)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                normalizedExpected,
                actualReferences,
                $"Unexpected project-reference graph for {projectPath}");
        }

        private static string NormalizeSeparators(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
