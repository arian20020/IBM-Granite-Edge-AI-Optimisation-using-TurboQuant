using System.Xml.Linq;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class ArchitectureBoundaryTests
{
    [TestMethod]
    public void SharedProjectsDoNotDependOnRuntimeNativeConverterOrApplicationLayers()
    {
        foreach (string project in Directory.GetFiles(
                     RepoPath("shared"), "*.csproj", SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(project);
            string[] references = document.Descendants("ProjectReference")
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
                .ToArray();
            string[] packages = document.Descendants("PackageReference")
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
                .ToArray();

            Assert.IsFalse(
                references.Any(reference => ReferencesForbiddenLayer(project, reference)),
                project);
            Assert.IsFalse(packages.Any(package =>
                package.Contains("OpenVINO", StringComparison.OrdinalIgnoreCase) ||
                package.Contains("TurboQuant", StringComparison.OrdinalIgnoreCase) ||
                package.Contains("Python", StringComparison.OrdinalIgnoreCase)), project);
        }
    }

    [TestMethod]
    public void RouteNeutralPromptSurfaceHasNoConcreteBackendOrNativeDependency()
    {
        string root = RepoPath("IBM Granite with TurboQuant (Intel)/Features/Prompting");
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            foreach (string forbidden in new[]
            {
                "Features.OpenVinoRoute",
                "TurboQuant",
                "WorkerClient",
                "DllImport",
                "LoadLibrary",
                "python.exe"
            })
            {
                Assert.IsFalse(source.Contains(forbidden, StringComparison.Ordinal),
                    $"{file} contains concrete dependency {forbidden}");
            }
        }
    }

    [TestMethod]
    public void OfficialConverterAndTurboQuantClosuresRemainSeparate()
    {
        string[] roots =
        [
            "third-party/openvino-official",
            "third-party/openvino-converter",
            "third-party/openvino-turboquant",
            "workers/OpenVinoOfficial.Worker",
            "workers/OpenVinoConverter.Worker",
            "workers/OpenVinoTurboQuant.Worker"
        ];
        foreach (string root in roots)
        {
            Assert.IsTrue(Directory.Exists(RepoPath(root)), root);
        }

        string official = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets"));
        StringAssert.Contains(official, "OpenVino\\Official\\Worker");
        Assert.IsFalse(official.Contains("TurboQuant", StringComparison.Ordinal));
        Assert.IsFalse(official.Contains("Converter", StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(RepoPath(
            "scripts/openvino/Test-OpenVinoTurboQuantWorkerManifest.ps1")));
        Assert.IsTrue(File.Exists(RepoPath(
            "scripts/openvino/Test-OpenVinoConverterWorkerManifest.ps1")));

        string session = File.ReadAllText(RepoPath(
            "workers/OpenVinoOfficial.Worker/src/session.cpp"));
        StringAssert.Contains(
            session,
            "pipeline_ = std::move(active_pipeline);");
        string workerHost = File.ReadAllText(RepoPath(
            "workers/OpenVinoOfficial.Worker/src/main.cpp"));
        int cancelledTerminal = workerHost.IndexOf(
            "{\"eventType\", \"sessionCancelled\"}",
            StringComparison.Ordinal);
        int boundedExit = workerHost.IndexOf(
            "std::_Exit(EXIT_SUCCESS);",
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, cancelledTerminal);
        Assert.IsGreaterThan(
            cancelledTerminal,
            boundedExit,
            "The one-session worker must exit after publishing cancellation without awaiting pipeline destruction.");
    }

    private static string RepoPath(string relative) => Path.Combine(
        FindRepositoryRoot(),
        relative.Replace('/', Path.DirectorySeparatorChar));

    private static bool ReferencesForbiddenLayer(
        string project,
        string projectReference)
    {
        string repositoryRoot = FindRepositoryRoot();
        string referencedProject = Path.GetFullPath(
            projectReference,
            Path.GetDirectoryName(project)!);
        string relative = Path.GetRelativePath(repositoryRoot, referencedProject);
        if (relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            return true;
        }
        string topLevel = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries)[0];

        return topLevel.Equals("infrastructure", StringComparison.OrdinalIgnoreCase) ||
            topLevel.Equals("runtime", StringComparison.OrdinalIgnoreCase) ||
            topLevel.Equals("workers", StringComparison.OrdinalIgnoreCase) ||
            topLevel.Equals("IBM Granite with TurboQuant (Intel)",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
