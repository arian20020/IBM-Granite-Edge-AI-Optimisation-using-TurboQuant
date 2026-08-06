using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Converts the final Gate 2 security and modularity decisions into executable
/// architecture checks. These checks scan only production source and project
/// files; abnormal fixture behaviour remains confined to tests/ProcessFixtures.
/// </summary>
[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class Gate2ArchitectureFitnessTests
{
    private const string AppProjectPath =
        "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj";
    private const string AppSourceRoot =
        "IBM Granite with TurboQuant (Intel)";
    private const string WorkerClientRoot =
        "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient";
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string[] FixtureScenarioTokens =
    [
        "healthy-controlled-failure",
        "no-hello",
        "malformed-hello",
        "wrong-protocol-version",
        "wrong-worker-id",
        "wrong-worker-process-id",
        "wrong-runtime-profile",
        "wrong-architecture",
        "invalid-utf8",
        "oversized-stdout-line",
        "progress-before-started",
        "duplicate-terminal",
        "exit-without-terminal",
        "crash-before-hello",
        "hang-before-hello",
        "cooperative-cancellation",
        "ignore-cancellation",
        "spawn-child-and-wait",
        "exit-root-with-live-child",
        "flood-stderr",
        "terminal-exit-mismatch",
        "echo-environment-keys",
        "probe-unrelated-handle"
    ];

    [TestMethod]
    public void WinUiApplicationRemainsDisconnectedFromGate2WorkerClient()
    {
        string projectPath = Path.Combine(Root, AppProjectPath);
        XDocument project = XDocument.Load(projectPath);
        string[] projectReferences = project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        Assert.IsFalse(
            projectReferences.Any(reference => reference.Contains(
                "ModelInspection.WorkerClient",
                StringComparison.OrdinalIgnoreCase)),
            "Gate 2 must not connect WorkerClient to the WinUI application.");

        foreach (string path in EnumerateSourceFiles(AppSourceRoot))
        {
            string source = File.ReadAllText(path);
            Assert.IsFalse(
                source.Contains(
                    "GraniteEdgeAI.ModelInspection.WorkerClient",
                    StringComparison.Ordinal),
                $"Gate 2 WorkerClient leaked into WinUI source: {Relative(path)}");
            Assert.IsFalse(
                source.Contains(
                    "ProtocolTestWorker",
                    StringComparison.Ordinal),
                $"The abnormal fixture leaked into WinUI source: {Relative(path)}");
        }
    }

    [TestMethod]
    public void ProductionSourceContainsNoAbnormalFixtureScenarioToken()
    {
        string[] productionRoots =
        [
            "shared/GraniteEdgeAI.ModelInspection.Transport",
            "workers/GraniteEdgeAI.ModelInspection.Worker",
            WorkerClientRoot
        ];

        foreach (string productionRoot in productionRoots)
        {
            foreach (string path in EnumerateSourceFiles(productionRoot))
            {
                string source = File.ReadAllText(path);
                foreach (string token in FixtureScenarioTokens)
                {
                    Assert.IsFalse(
                        source.Contains(token, StringComparison.Ordinal),
                        $"Fixture scenario '{token}' leaked into {Relative(path)}.");
                }
            }
        }
    }

    [TestMethod]
    public void ProductionWorkerBoundaryContainsNoProcessStartOrLocalListener()
    {
        string[] forbiddenTokens =
        [
            "Process.Start(",
            "HttpListener",
            "TcpListener",
            "WebApplication.CreateBuilder",
            "UseUrls(",
            "localhost",
            "127.0.0.1"
        ];

        foreach (string path in EnumerateSourceFiles(WorkerClientRoot)
                     .Concat(EnumerateSourceFiles(
                         "workers/GraniteEdgeAI.ModelInspection.Worker")))
        {
            string source = File.ReadAllText(path);
            foreach (string token in forbiddenTokens)
            {
                Assert.IsFalse(
                    source.Contains(token, StringComparison.Ordinal),
                    $"Forbidden production boundary token '{token}' found in {Relative(path)}.");
            }
        }
    }

    [TestMethod]
    public void WindowsLauncherRetainsAtomicJobAndHandleAttributes()
    {
        string platformPath = Path.Combine(
            Root,
            WorkerClientRoot,
            "Windows",
            "WindowsWorkerProcessPlatform.cs");
        string launcherPath = Path.Combine(
            Root,
            WorkerClientRoot,
            "Windows",
            "WindowsWorkerProcessLauncher.cs");
        string platform = File.ReadAllText(platformPath);
        string launcher = File.ReadAllText(launcherPath);

        StringAssert.Contains(
            platform,
            "NativeConstants.ProcThreadAttributeHandleList");
        StringAssert.Contains(
            platform,
            "NativeConstants.ProcThreadAttributeJobList");
        StringAssert.Contains(
            platform,
            "attributeCount: 2");
        StringAssert.Contains(
            platform,
            "inheritHandles: true");
        StringAssert.Contains(
            platform,
            "NativeConstants.RequiredCreationFlags");
        StringAssert.Contains(launcher, "ApplyHandleAllowlist");
        StringAssert.Contains(launcher, "ApplyJobContainment");
        Assert.IsFalse(
            launcher.Contains(
                "CreateBreakawayFromJob",
                StringComparison.Ordinal),
            "The production launcher must not request Job Object breakaway.");
    }

    [TestMethod]
    public void ProductionProjectsRemainFreeOfGate3RuntimePackages()
    {
        string[] productionProjects =
        [
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj",
            "workers/GraniteEdgeAI.ModelInspection.Worker/GraniteEdgeAI.ModelInspection.Worker.csproj",
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/GraniteEdgeAI.ModelInspection.WorkerClient.csproj"
        ];
        string[] forbiddenPackages =
        [
            "LLamaSharp",
            "OpenVINO",
            "TurboQuant",
            "Microsoft.WindowsAppSDK"
        ];

        foreach (string relativeProjectPath in productionProjects)
        {
            XDocument project = XDocument.Load(
                Path.Combine(Root, relativeProjectPath));
            string[] packages = project
                .Descendants("PackageReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();

            foreach (string forbiddenPackage in forbiddenPackages)
            {
                Assert.IsFalse(
                    packages.Any(package => package.Contains(
                        forbiddenPackage,
                        StringComparison.OrdinalIgnoreCase)),
                    $"Gate 3 package '{forbiddenPackage}' entered {relativeProjectPath}.");
            }
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(
        string relativeRoot)
    {
        string absoluteRoot = Path.Combine(Root, relativeRoot);
        Assert.IsTrue(
            Directory.Exists(absoluteRoot),
            $"Missing source root {relativeRoot}.");

        return Directory.EnumerateFiles(
                absoluteRoot,
                "*.*",
                SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase));
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        string[] startingPaths =
        [
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(
                typeof(Gate2ArchitectureFitnessTests).Assembly.Location)
                ?? AppContext.BaseDirectory
        ];

        foreach (string startingPath in startingPaths)
        {
            DirectoryInfo? directory = new(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(
                        directory.FullName,
                        "IBM Granite with TurboQuant (Intel).slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root for Gate 2 architecture checks.");
    }
}
