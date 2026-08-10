using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Locks the production x64 packaging boundary that keeps LLamaSharp and its
/// native CPU libraries out of the WinUI process while installing the worker
/// at one fixed application-relative location.
/// </summary>
[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class Gate4PackagingContractTests
{
    private const string AppProject =
        "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj";
    private const string PackagingTarget =
        "IBM Granite with TurboQuant (Intel)/ModelInspection.WorkerPackaging.targets";
    private const string CompositionSource =
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Infrastructure/ModelInspectionWorkerComposition.cs";
    private const string IntegritySource =
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Infrastructure/ManifestVerifyingInspectionWorkerClient.cs";
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void WinUiReferencesOnlyProductionDependenciesInReleaseAndAddsFixturesOnlyForDebugX64()
    {
        XDocument project = XDocument.Load(Absolute(AppProject));
        XElement[] modelInspectionReferences = project
            .Descendants("ProjectReference")
            .Where(reference => (reference.Attribute("Include")?.Value ?? string.Empty)
                .Contains("ModelInspection", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.HasCount(3, modelInspectionReferences);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "GraniteEdgeAI.ModelInspection.Contracts.csproj",
                "GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
                "GraniteEdgeAI.ModelInspection.Fixtures.csproj"
            },
            modelInspectionReferences
                .Select(reference => Path.GetFileName(
                    reference.Attribute("Include")!.Value))
                .ToArray());

        string[] releaseX64References = ProjectNamesFor(
            modelInspectionReferences,
            configuration: "Release",
            platform: "x64");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "GraniteEdgeAI.ModelInspection.Contracts.csproj",
                "GraniteEdgeAI.ModelInspection.WorkerClient.csproj"
            },
            releaseX64References);

        string[] debugX64References = ProjectNamesFor(
            modelInspectionReferences,
            configuration: "Debug",
            platform: "x64");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "GraniteEdgeAI.ModelInspection.Contracts.csproj",
                "GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
                "GraniteEdgeAI.ModelInspection.Fixtures.csproj"
            },
            debugX64References);

        string projectText = File.ReadAllText(Absolute(AppProject));
        Assert.IsFalse(projectText.Contains(
            "workers\\GraniteEdgeAI.ModelInspection.Worker",
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(projectText.Contains(
            "runtime\\GraniteEdgeAI.ModelInspection.LlamaSharp",
            StringComparison.OrdinalIgnoreCase));
        XElement? nonX64Removal = project
            .Descendants("Compile")
            .SingleOrDefault(item => string.Equals(
                item.Attribute("Remove")?.Value,
                "Features\\ModelInspection\\Infrastructure\\**\\*.cs",
                StringComparison.Ordinal));
        Assert.IsNotNull(nonX64Removal);
        Assert.AreEqual(
            "'$(Platform)' != 'x64'",
            nonX64Removal.Parent?.Attribute("Condition")?.Value);

        XElement? x64TrimPolicy = project
            .Descendants("PublishTrimmed")
            .SingleOrDefault(item => string.Equals(
                item.Attribute("Condition")?.Value,
                "'$(Platform)' == 'x64'",
                StringComparison.Ordinal));
        Assert.IsNotNull(
            x64TrimPolicy,
            "The reflection-based protected client requires an explicit x64 trim policy.");
        Assert.AreEqual("False", x64TrimPolicy.Value);
    }

    [TestMethod]
    public void WinUiImportsFixedWorkerPackagingTargetOnlyForX64()
    {
        XDocument project = XDocument.Load(Absolute(AppProject));
        XElement[] imports = project
            .Descendants("Import")
            .Where(import => string.Equals(
                import.Attribute("Project")?.Value,
                "ModelInspection.WorkerPackaging.targets",
                StringComparison.Ordinal))
            .ToArray();

        Assert.HasCount(1, imports);
        Assert.AreEqual(
            "'$(Platform)' == 'x64'",
            imports[0].Attribute("Condition")?.Value);
    }

    [TestMethod]
    public void PackagingTargetPublishesExactFixedCpuWorkerBoundary()
    {
        string target = File.ReadAllText(Absolute(PackagingTarget));
        string[] required =
        [
            "BeforeTargets=\"AssignTargetPaths\"",
            "ModelInspection\\Worker",
            "GraniteEdgeAI.ModelInspection.Worker.csproj",
            "<_ModelInspectionDotNetHost",
            "WorkingDirectory=\"$(_ModelInspectionRepositoryRoot)\"",
            "--configuration Release",
            "--artifacts-path &quot;$(_ModelInspectionWorkerArtifactsRoot)&quot;",
            "--no-restore",
            "--runtime win-x64",
            "--self-contained false",
            "UseAppHost=true",
            "PublishTrimmed=false",
            "PublishReadyToRun=false",
            "DebugSymbols=false",
            "DebugType=None",
            "CopyDebugSymbolToPublishDirectory=false",
            "New-ModelInspectionWorkerManifest.ps1",
            "Test-ModelInspectionWorkerManifest.ps1",
            "ModelInspection\\worker-manifest.json",
            "EmbeddedResource Include=\"$(_ModelInspectionWorkerManifestPath)\"",
            "GraniteEdgeAI.ModelInspection.WorkerManifest.json",
            "CopyToOutputDirectory",
            "CopyToPublishDirectory"
        ];

        foreach (string token in required)
        {
            StringAssert.Contains(target, token);
        }

        int restore = target.IndexOf(" restore &quot;", StringComparison.Ordinal);
        int publish = target.IndexOf(" publish &quot;", StringComparison.Ordinal);
        Assert.IsTrue(
            restore >= 0 && restore < publish,
            "The isolated worker graph must be restored before it is published.");
    }

    [TestMethod]
    public void ApplicationRootCompositionNeverUsesCurrentDirectoryOrPathSearch()
    {
        string source = File.ReadAllText(Absolute(CompositionSource));

        StringAssert.Contains(source, "Package.Current.InstalledLocation.Path");
        StringAssert.Contains(source, "AppContext.BaseDirectory");
        StringAssert.Contains(source, "GetCurrentPackageFullName(");
        StringAssert.Contains(source, "ExactSpelling = true");
        StringAssert.Contains(source, "DllImportSearchPath.System32");
        StringAssert.Contains(source, "AppModelErrorNoPackage");
        StringAssert.Contains(source, "new InspectionWorkerClient(");
        StringAssert.Contains(source, "WorkerClientOptions.CreateDefault(approvedRoot)");
        StringAssert.Contains(source, "LoadTrustedWorkerManifest()");
        StringAssert.Contains(source, "new ManifestVerifyingInspectionWorkerClient(");
        string integritySource = File.ReadAllText(Absolute(IntegritySource));
        StringAssert.Contains(integritySource, "ExpectedFileCount = 44");
        StringAssert.Contains(integritySource, "CryptographicOperations.FixedTimeEquals");
        StringAssert.Contains(integritySource, "SHA256.HashDataAsync");
        StringAssert.Contains(integritySource, "WorkerPackageIntegrityFailed");
        Assert.IsTrue(
            integritySource.IndexOf("WorkerPackageManifestVerifier.VerifyAsync", StringComparison.Ordinal) <
            integritySource.IndexOf("_innerClient.ExecuteAsync", StringComparison.Ordinal),
            "The complete worker closure must be verified before process delegation.");
        Assert.IsFalse(source.Contains(
            "Environment.CurrentDirectory",
            StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(
            "Directory.GetCurrentDirectory",
            StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(
            "Environment.GetEnvironmentVariable(\"PATH\")",
            StringComparison.OrdinalIgnoreCase));
    }

    private static string Absolute(string relative) =>
        Path.Combine(
            Root,
            relative.Replace('/', Path.DirectorySeparatorChar));

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
