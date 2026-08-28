using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Locks the application execution boundary that turns the protected worker's
/// validated GGUF evidence into an application-owned inspection result.
/// </summary>
[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class Gate5ApplicationBoundaryContractTests
{
    private const string AppRoot = "IBM Granite with TurboQuant (Intel)";
    private const string AppProject =
        "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj";
    private const string CompositionSource =
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Infrastructure/ModelInspectionWorkerComposition.cs";
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void ApplicationExecutionLayerHasEveryRequiredResponsibility()
    {
        string[] requiredSources =
        [
            "Features/ModelInspection/Runtime/ILlamaModelProbe.cs",
            "Features/ModelInspection/Runtime/ModelInspectionProbeResult.cs",
            "Features/ModelInspection/Runtime/WorkerRequestMapper.cs",
            "Features/ModelInspection/Runtime/WorkerResultMapper.cs",
            "Features/ModelInspection/Runtime/WorkerProcessLlamaModelProbe.cs",
            "Features/ModelInspection/Classification/IModelInspectionClassifier.cs",
            "Features/ModelInspection/Classification/ModelInspectionClassifier.cs",
            "Features/ModelInspection/Services/IModelInspectionService.cs",
            "Features/ModelInspection/Services/ModelInspectionService.cs",
            "Features/ModelInspection/Services/ModelInspectionServiceComposition.cs"
        ];

        foreach (string relativeSource in requiredSources)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(
                    Root,
                    AppRoot,
                    relativeSource.Replace('/', Path.DirectorySeparatorChar))),
                $"Missing application execution responsibility: {relativeSource}");
        }
    }

    [TestMethod]
    public void WinUiExecutionLayerCannotLoadLlamaSharpOrWorkerHost()
    {
        XDocument project = XDocument.Load(Absolute(AppProject));
        string[] references = project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        Assert.IsFalse(references.Any(reference => reference.Contains(
            "ModelInspection.LlamaSharp",
            StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(references.Any(reference => reference.Contains(
            "ModelInspection.Worker\\",
            StringComparison.OrdinalIgnoreCase)));
        string[] packages = project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
        string[] forbiddenRuntimePackages =
        [
            "LLamaSharp",
            "OpenVINO",
            "TurboQuant"
        ];
        foreach (string forbiddenPackage in forbiddenRuntimePackages)
        {
            Assert.IsFalse(
                packages.Any(package => package.Contains(
                    forbiddenPackage,
                    StringComparison.OrdinalIgnoreCase)),
                $"Native/runtime package '{forbiddenPackage}' entered WinUI.");
        }

        foreach (string path in Directory.EnumerateFiles(
                     Absolute($"{AppRoot}/Features/ModelInspection"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            Assert.IsFalse(
                source.Contains(
                    "GraniteEdgeAI.ModelInspection.LlamaSharp",
                    StringComparison.Ordinal),
                $"LLamaSharp leaked into WinUI source: {Relative(path)}");
            Assert.IsFalse(
                source.Contains("Process.Start(", StringComparison.Ordinal),
                $"A direct process launch leaked into WinUI source: {Relative(path)}");
            Assert.IsFalse(
                source.Contains("Assembly.Load(", StringComparison.Ordinal) ||
                source.Contains("NativeLibrary.Load(", StringComparison.Ordinal),
                $"A dynamic runtime load leaked into WinUI source: {Relative(path)}");
        }
    }

    [TestMethod]
    public void WinUiReferencesWorkerSourcesOnlyForX64AndFixturesOnlyForDebugX64()
    {
        XDocument project = XDocument.Load(Absolute(AppProject));
        XElement[] references = project
            .Descendants("ProjectReference")
            .Where(reference => (reference.Attribute("Include")?.Value ?? string.Empty)
                .Contains("ModelInspection", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.HasCount(3, references);
        string[] expectedProjectNames =
        [
            "GraniteEdgeAI.ModelInspection.Contracts.csproj",
            "GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
            "GraniteEdgeAI.ModelInspection.Fixtures.csproj"
        ];
        CollectionAssert.AreEquivalent(
            expectedProjectNames,
            references
                .Select(reference => Path.GetFileName(
                    reference.Attribute("Include")!.Value))
                .ToArray());

        CollectionAssert.AreEquivalent(
            expectedProjectNames[..2],
            ProjectNamesFor(references, configuration: "Release", platform: "x64"));
        CollectionAssert.AreEquivalent(
            expectedProjectNames,
            ProjectNamesFor(references, configuration: "Debug", platform: "x64"));

        string[] removals = project
            .Descendants("Compile")
            .Where(element => string.Equals(
                element.Parent?.Attribute("Condition")?.Value,
                "'$(Platform)' != 'x64'",
                StringComparison.Ordinal))
            .Select(element => element.Attribute("Remove")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        CollectionAssert.Contains(
            removals,
            "Features\\ModelInspection\\Infrastructure\\**\\*.cs");
        CollectionAssert.Contains(
            removals,
            "Features\\ModelInspection\\Runtime\\WorkerRequestMapper.cs");
        CollectionAssert.Contains(
            removals,
            "Features\\ModelInspection\\Runtime\\WorkerResultMapper.cs");
        CollectionAssert.Contains(
            removals,
            "Features\\ModelInspection\\Runtime\\WorkerProcessLlamaModelProbe.cs");
    }

    [TestMethod]
    public void ProtocolAndProcessTypesStayInsideTheApprovedExecutionBoundary()
    {
        string[] approvedSources =
        [
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Infrastructure/ManifestVerifyingInspectionWorkerClient.cs",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Infrastructure/ModelInspectionProjectionFactory.cs",
            CompositionSource,
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Runtime/WorkerProcessLlamaModelProbe.cs",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Runtime/WorkerRequestMapper.cs",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Runtime/WorkerResultMapper.cs"
        ];

        foreach (string path in Directory.EnumerateFiles(
                     Absolute($"{AppRoot}/Features/ModelInspection"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            if (source.Contains(
                    "GraniteEdgeAI.ModelInspection.WorkerClient",
                    StringComparison.Ordinal) ||
                source.Contains(
                    "using GraniteEdgeAI.ModelInspection.Contracts;",
                    StringComparison.Ordinal))
            {
                CollectionAssert.Contains(approvedSources, Relative(path));
            }
        }
    }

    [TestMethod]
    public void DefaultCompositionJoinsProbeClassifierAndService()
    {
        string source = File.ReadAllText(Absolute(CompositionSource));

        StringAssert.Contains(source, "CreateDefaultService()");
        StringAssert.Contains(source, "new WorkerProcessLlamaModelProbe(CreateDefaultClient())");
        StringAssert.Contains(source, "new ModelInspectionClassifier()");
        StringAssert.Contains(source, "new ModelInspectionService(");
    }

    [TestMethod]
    public void DefaultServiceEntryPointIsExplicitlyX64Only()
    {
        XDocument project = XDocument.Load(Absolute(AppProject));
        XElement? x64Constants = project
            .Descendants("DefineConstants")
            .SingleOrDefault(element => string.Equals(
                element.Attribute("Condition")?.Value,
                "'$(Platform)' == 'x64'",
                StringComparison.Ordinal));
        Assert.IsNotNull(x64Constants);
        StringAssert.Contains(x64Constants.Value, "MODEL_INSPECTION_X64");

        string entryPoint = File.ReadAllText(Absolute(
            $"{AppRoot}/Features/ModelInspection/Services/ModelInspectionServiceComposition.cs"));
        StringAssert.Contains(entryPoint, "#if MODEL_INSPECTION_X64");
        StringAssert.Contains(
            entryPoint,
            "ModelInspectionWorkerComposition.CreateDefaultService()");
        StringAssert.Contains(entryPoint, "PlatformNotSupportedException");
    }

    private static string Absolute(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string[] ProjectNamesFor(
        IEnumerable<XElement> references,
        string configuration,
        string platform) =>
        references
            .Where(reference => IsIncluded(reference, configuration, platform))
            .Select(reference => Path.GetFileName(reference.Attribute("Include")!.Value))
            .ToArray();

    private static bool IsIncluded(
        XElement reference,
        string configuration,
        string platform)
    {
        string? condition = reference.Attribute("Condition")?.Value ??
            reference.Parent?.Attribute("Condition")?.Value;

        return condition switch
        {
            "'$(Platform)' == 'x64'" => platform == "x64",
            "'$(Configuration)|$(Platform)' == 'Debug|x64'" =>
                configuration == "Debug" && platform == "x64",
            _ => throw new InvalidDataException(
                "A Model Inspection reference has an unrecognized condition.")
        };
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
