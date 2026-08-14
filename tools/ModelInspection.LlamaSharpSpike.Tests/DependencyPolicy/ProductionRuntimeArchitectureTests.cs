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
        "ModelProbe/VocabOnlyProbePhaseSequence.cs",
        "ModelProbe/VocabOnlyProbeProgress.cs",
        "ModelProbe/VocabOnlyProbeRequest.cs"
    ];

    private static readonly (string Name, string ExpectedValue)[]
        RequiredRuntimeProperties =
        [
            ("TargetFramework", "net8.0"),
            ("Nullable", "enable"),
            ("TreatWarningsAsErrors", "true"),
            ("EnableNETAnalyzers", "true"),
            ("Deterministic", "true"),
            ("PlatformTarget", "x64"),
            ("RuntimeIdentifier", "win-x64")
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
        IReadOnlyList<string> propertyViolations =
            GetRequiredRuntimePropertyViolations(project);
        Assert.AreEqual(
            0,
            propertyViolations.Count,
            string.Join(Environment.NewLine, propertyViolations));

        XDocument conditionBlindLastValueMutation = new(project);
        conditionBlindLastValueMutation.Root!.Add(
            new XElement(
                "PropertyGroup",
                new XAttribute(
                    "Condition",
                    "'$(Configuration)' == 'Release'"),
                new XElement("Nullable", "disable"),
                new XElement("PlatformTarget", "AnyCPU"),
                new XElement("RuntimeIdentifier", "win-arm64")),
            new XElement(
                "PropertyGroup",
                new XAttribute(
                    "Condition",
                    "'$(Configuration)' == 'Debug'"),
                new XElement("Nullable", "enable"),
                new XElement("PlatformTarget", "x64"),
                new XElement("RuntimeIdentifier", "win-x64")));
        IReadOnlyList<string> mutationViolations =
            GetRequiredRuntimePropertyViolations(
                conditionBlindLastValueMutation);
        foreach (string shadowedProperty in new[]
                 {
                     "Nullable",
                     "PlatformTarget",
                     "RuntimeIdentifier"
                 })
        {
            Assert.IsTrue(
                mutationViolations.Any(
                    violation => violation.StartsWith(
                        $"{shadowedProperty}:",
                        StringComparison.Ordinal)),
                $"A condition-blind last-value check accepted {shadowedProperty}.");
        }

        XDocument conditionalOnlyMutation = new(project);
        conditionalOnlyMutation
            .Descendants("RuntimeIdentifier")
            .Single()
            .SetAttributeValue(
                "Condition",
                "'$(Configuration)' == 'Release'");
        IReadOnlyList<string> conditionalOnlyViolations =
            GetRequiredRuntimePropertyViolations(conditionalOnlyMutation);
        Assert.IsTrue(
            conditionalOnlyViolations.Contains(
                "RuntimeIdentifier: declaration must be unconditional " +
                "and in a top-level PropertyGroup.",
                StringComparer.Ordinal),
            "A conditional-only RuntimeIdentifier declaration was accepted.");

        XDocument differentlyCasedDuplicateMutation = new(project);
        differentlyCasedDuplicateMutation.Root!.Add(
            new XElement(
                "PropertyGroup",
                new XElement("runtimeidentifier", "win-arm64")));
        IReadOnlyList<string> differentlyCasedViolations =
            GetRequiredRuntimePropertyViolations(
                differentlyCasedDuplicateMutation);
        Assert.IsTrue(
            differentlyCasedViolations.Contains(
                "RuntimeIdentifier: expected exactly one declaration, " +
                "but found 2.",
                StringComparer.Ordinal),
            "A differently cased MSBuild property override was accepted.");

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

    private static IReadOnlyList<string>
        GetRequiredRuntimePropertyViolations(XDocument project)
    {
        List<string> violations = [];

        foreach ((string propertyName, string expectedValue) in
                 RequiredRuntimeProperties)
        {
            XElement[] definitions = project
                .Descendants()
                .Where(
                    element => string.Equals(
                        element.Name.LocalName,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (definitions.Length != 1)
            {
                violations.Add(
                    $"{propertyName}: expected exactly one declaration, " +
                    $"but found {definitions.Length}.");
                continue;
            }

            XElement definition = definitions[0];
            XElement? propertyGroup = definition.Parent;
            bool isUnconditionalTopLevelProperty =
                propertyGroup is not null &&
                string.Equals(
                    propertyGroup.Name.LocalName,
                    "PropertyGroup",
                    StringComparison.Ordinal) &&
                ReferenceEquals(propertyGroup.Parent, project.Root) &&
                string.IsNullOrWhiteSpace(
                    propertyGroup.Attribute("Condition")?.Value) &&
                string.IsNullOrWhiteSpace(
                    definition.Attribute("Condition")?.Value);
            if (!isUnconditionalTopLevelProperty)
            {
                violations.Add(
                    $"{propertyName}: declaration must be unconditional " +
                    "and in a top-level PropertyGroup.");
            }

            string actualValue = definition.Value.Trim();
            if (!string.Equals(
                    expectedValue,
                    actualValue,
                    StringComparison.Ordinal))
            {
                violations.Add(
                    $"{propertyName}: expected '{expectedValue}', " +
                    $"but found '{actualValue}'.");
            }
        }

        return violations;
    }

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
