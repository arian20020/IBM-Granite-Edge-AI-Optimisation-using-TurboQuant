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

            Assert.IsFalse(references.Any(reference =>
                reference.Contains("infrastructure", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains("workers", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains("IBM Granite", StringComparison.OrdinalIgnoreCase)), project);
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
    }

    private static string RepoPath(string relative) => Path.Combine(
        FindRepositoryRoot(),
        relative.Replace('/', Path.DirectorySeparatorChar));

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
