using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Protects the project boundaries approved for the Gate 2 worker and process
/// adapter before any production process implementation is added.
/// </summary>
[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class Gate2ProjectGraphTests
{
    private const string SolutionPath =
        "IBM Granite with TurboQuant (Intel).slnx";

    private static readonly string Root = RepositoryRoot.Find();

    private static readonly string[] ApprovedGate2ProjectPaths =
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

    private static readonly string[] WindowsX64ProjectPaths =
    [
        "workers/GraniteEdgeAI.ModelInspection.Worker/GraniteEdgeAI.ModelInspection.Worker.csproj",
        "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj",
        "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/GraniteEdgeAI.ModelInspection.ProtocolTestWorker.csproj",
        "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj"
    ];

    private static readonly string[] ForbiddenPackageNames =
    [
        "LLamaSharp",
        "Microsoft.WindowsAppSDK",
        "OpenVINO",
        "TurboQuant"
    ];

    /// <summary>
    /// Requires every approved Gate 2 project to exist at its documented path.
    /// </summary>
    [TestMethod]
    public void ApprovedGate2ProjectsExist()
    {
        foreach (string path in ApprovedGate2ProjectPaths)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(Root, path)),
                $"Missing {path}");
        }
    }

    /// <summary>
    /// Requires the solution to expose every Gate 2 project explicitly. This
    /// prevents a project from building only through an ad-hoc CI path while
    /// remaining invisible to Visual Studio and normal solution-level review.
    /// </summary>
    [TestMethod]
    public void SolutionRegistersEveryApprovedGate2Project()
    {
        XDocument solution = XDocument.Load(Path.Combine(Root, SolutionPath));

        string[] registeredGate2Projects = solution
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizeSeparators(path!))
            .Where(path => ApprovedGate2ProjectPaths.Contains(
                path,
                StringComparer.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] expectedProjects = ApprovedGate2ProjectPaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            expectedProjects,
            registeredGate2Projects,
            "The solution must register every approved Gate 2 project exactly once.");
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
    /// Requires the defensive build settings approved for the gate. These are
    /// executable architecture rules rather than conventions written only in a
    /// design document.
    /// </summary>
    [TestMethod]
    public void ApprovedGate2ProjectsUseRequiredBuildPolicies()
    {
        foreach (string projectPath in ApprovedGate2ProjectPaths)
        {
            ProjectFileAssert.HasProperty(
                Root,
                projectPath,
                "Nullable",
                "enable");
            ProjectFileAssert.HasProperty(
                Root,
                projectPath,
                "TreatWarningsAsErrors",
                "true");
            ProjectFileAssert.HasProperty(
                Root,
                projectPath,
                "EnableNETAnalyzers",
                "true");
            ProjectFileAssert.HasProperty(
                Root,
                projectPath,
                "Deterministic",
                "true");
        }

        foreach (string projectPath in WindowsX64ProjectPaths)
        {
            ProjectFileAssert.HasProperty(
                Root,
                projectPath,
                "PlatformTarget",
                "x64");
            ProjectFileAssert.HasProperty(
                Root,
                projectPath,
                "RuntimeIdentifier",
                "win-x64");
        }
    }

    /// <summary>
    /// Prevents Gate 3 native libraries, WinUI, and unrelated optimization
    /// packages from entering any Gate 2 project through a package reference.
    /// </summary>
    [TestMethod]
    public void ApprovedGate2ProjectsContainNoForbiddenPackageReference()
    {
        foreach (string projectPath in ApprovedGate2ProjectPaths)
        {
            ProjectFileAssert.ContainsNoPackageReference(
                Root,
                projectPath,
                ForbiddenPackageNames);
        }
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
                        SolutionPath);

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
    }

    /// <summary>
    /// Provides focused assertions for individual project-file policies.
    /// </summary>
    private static class ProjectFileAssert
    {
        public static void HasProperty(
            string repositoryRoot,
            string projectPath,
            string propertyName,
            string expectedValue)
        {
            XDocument project = LoadProject(repositoryRoot, projectPath);
            string[] values = project
                .Descendants(propertyName)
                .Select(element => element.Value.Trim())
                .ToArray();

            Assert.HasCount(
                1,
                values,
                $"{projectPath} must define {propertyName} exactly once.");
            Assert.AreEqual(
                expectedValue,
                values[0],
                ignoreCase: true,
                culture: null,
                message: $"Unexpected {propertyName} in {projectPath}.");
        }

        public static void ContainsNoPackageReference(
            string repositoryRoot,
            string projectPath,
            IEnumerable<string> forbiddenPackageNames)
        {
            XDocument project = LoadProject(repositoryRoot, projectPath);
            string[] packageNames = project
                .Descendants("PackageReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToArray();

            string[] forbiddenMatches = packageNames
                .Where(packageName => forbiddenPackageNames.Any(
                    forbiddenName => packageName.Contains(
                        forbiddenName,
                        StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.IsEmpty(
                forbiddenMatches,
                $"{projectPath} contains forbidden Gate 2 packages: " +
                string.Join(", ", forbiddenMatches));
        }

        private static XDocument LoadProject(
            string repositoryRoot,
            string projectPath)
        {
            string absoluteProjectPath = Path.Combine(repositoryRoot, projectPath);
            Assert.IsTrue(
                File.Exists(absoluteProjectPath),
                $"Missing project file {projectPath}");

            return XDocument.Load(absoluteProjectPath);
        }
    }

    private static string NormalizeSeparators(string path)
    {
        return path.Replace('\\', '/');
    }
}
