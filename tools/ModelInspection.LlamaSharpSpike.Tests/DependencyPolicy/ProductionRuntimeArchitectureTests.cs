using System.Xml.Linq;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Protects the production CPU/VocabOnly runtime boundary from drifting back
/// into the feasibility command-line project.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ProductionRuntimeArchitectureTests
{
    private const string RuntimeProjectRelativePath =
        "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/" +
        "GraniteEdgeAI.ModelInspection.LlamaSharp.csproj";

    private static readonly string[] RuntimeSourceRelativePaths =
    [
        "CpuNativeRuntimeConfiguration.cs",
        "NativeBackendSmokeProbe.cs",
        "NativeBackendSmokeResult.cs",
        "PinnedApplicationRuntime.cs",
        "ModelProbe/ChatTemplateEvidenceFactory.cs",
        "ModelProbe/IModelFileHasher.cs",
        "ModelProbe/IVocabOnlyModelProbe.cs",
        "ModelProbe/ModelFileSnapshot.cs",
        "ModelProbe/ModelFileSnapshotService.cs",
        "ModelProbe/ModelProbeSafetyValidator.cs",
        "ModelProbe/NativeLoadProgressRecorder.cs",
        "ModelProbe/ProbeFailure.cs",
        "ModelProbe/ProbeFailureMapper.cs",
        "ModelProbe/ProbeResultFinalizer.cs",
        "ModelProbe/SensitiveTextRedactor.cs",
        "ModelProbe/Sha256ModelFileHasher.cs",
        "ModelProbe/VocabOnlyEvidenceCollector.cs",
        "ModelProbe/VocabOnlyMetadataProjection.cs",
        "ModelProbe/VocabOnlyModelProbe.cs",
        "ModelProbe/VocabOnlyModelProbeResult.cs",
        "ModelProbe/VocabOnlyProbeProgress.cs",
        "ModelProbe/VocabOnlyProbeRequest.cs"
    ];

    [TestMethod]
    public void ProductionRuntimeProjectOwnsExactCpuPackages()
    {
        string projectPath = RepositoryPath(RuntimeProjectRelativePath);
        Assert.IsTrue(
            File.Exists(projectPath),
            $"Production runtime project not found: {projectPath}");

        IReadOnlyDictionary<string, string> packages =
            ReadPackageReferences(XDocument.Load(projectPath));

        Assert.AreEqual(2, packages.Count);
        Assert.AreEqual("0.27.0", packages["LLamaSharp"]);
        Assert.AreEqual(
            "0.27.0",
            packages["LLamaSharp.Backend.Cpu"]);
    }

    [TestMethod]
    public void ProductionRuntimeProjectHasExactBuildAndDependencyBoundary()
    {
        XDocument project = XDocument.Load(
            RepositoryPath(RuntimeProjectRelativePath));
        IReadOnlyDictionary<string, string> properties = project
            .Descendants("PropertyGroup")
            .Elements()
            .Where(element => !string.IsNullOrWhiteSpace(element.Value))
            .GroupBy(element => element.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value.Trim(),
                StringComparer.Ordinal);

        Assert.AreEqual("net8.0", properties["TargetFramework"]);
        Assert.AreEqual("enable", properties["Nullable"]);
        Assert.AreEqual("true", properties["TreatWarningsAsErrors"]);
        Assert.AreEqual("true", properties["EnableNETAnalyzers"]);
        Assert.AreEqual("true", properties["Deterministic"]);
        Assert.AreEqual("x64", properties["PlatformTarget"]);
        Assert.AreEqual("win-x64", properties["RuntimeIdentifier"]);

        string[] projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        Assert.HasCount(0, projectReferences);
        string projectText = File.ReadAllText(
            RepositoryPath(RuntimeProjectRelativePath));
        Assert.IsFalse(
            projectText.Contains(
                "GraniteEdgeAI.ModelInspection.Worker",
                StringComparison.Ordinal));
        Assert.IsFalse(
            projectText.Contains(
                "GraniteEdgeAI.ModelInspection.Contracts",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void ProductionRuntimeBoundaryDocumentationExists()
    {
        Assert.IsTrue(File.Exists(RepositoryPath("runtime/README.md")));
        Assert.IsTrue(
            File.Exists(
                RepositoryPath(
                    "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/README.md")));
    }

    [TestMethod]
    public void SpikeProjectOwnsNoLlamaPackagesAndReferencesRuntime()
    {
        string spikeProjectPath = RepositoryPath(
            "tools/ModelInspection.LlamaSharpSpike/" +
            "ModelInspection.LlamaSharpSpike.csproj");
        XDocument spikeProject = XDocument.Load(spikeProjectPath);

        Assert.AreEqual(
            0,
            ReadPackageReferences(spikeProject).Count,
            "The CLI spike must consume LLamaSharp only through the production runtime project.");

        string[] projectReferences = spikeProject
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Replace('\\', '/'))
            .ToArray();
        CollectionAssert.Contains(
            projectReferences,
            "../../runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/" +
            "GraniteEdgeAI.ModelInspection.LlamaSharp.csproj");
    }

    [TestMethod]
    public void VocabOnlyProbeBelongsToProductionRuntimeAssembly()
    {
        Assert.AreEqual(
            "GraniteEdgeAI.ModelInspection.LlamaSharp",
            typeof(VocabOnlyModelProbe).Assembly.GetName().Name);
    }

    [TestMethod]
    public void ProductionRuntimePhysicallyOwnsProbeConfigurationAndEvidenceSources()
    {
        string runtimeRoot = RepositoryPath(
            "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp");
        string spikeRoot = RepositoryPath(
            "tools/ModelInspection.LlamaSharpSpike");

        foreach (string relativePath in RuntimeSourceRelativePaths)
        {
            string runtimePath = Path.Combine(
                runtimeRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            string spikePath = Path.Combine(
                spikeRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.IsTrue(
                File.Exists(runtimePath),
                $"Production runtime source not found: {runtimePath}");
            Assert.IsFalse(
                File.Exists(spikePath),
                $"Runtime source remains duplicated in the spike: {spikePath}");
        }
    }

    private static string RepositoryPath(string relativePath) =>
        Path.Combine(
            RepositoryPaths.FindRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static IReadOnlyDictionary<string, string> ReadPackageReferences(
        XDocument project) => project
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
