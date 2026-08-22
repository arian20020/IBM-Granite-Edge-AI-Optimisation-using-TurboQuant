using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
[DoNotParallelize]
public sealed class ModelInspectionFixtureBuildBoundaryContractTests
{
    private const string ReleaseMainDllEnvironment =
        "MODEL_INSPECTION_FIXTURE_RELEASE_MAIN_DLL";
    private const string ReleaseInspectionResultEnvironment =
        "MODEL_INSPECTION_FIXTURE_RELEASE_INSPECTION_RESULT";
    private const string ReleaseEvidenceEnvironment =
        "MODEL_INSPECTION_FIXTURE_RELEASE_EVIDENCE";
    private const string ReleaseEvidenceReceiptEnvironment =
        "MODEL_INSPECTION_FIXTURE_RELEASE_EVIDENCE_RECEIPT";
    private const string IsolationOwnedRootEnvironment =
        "MODEL_INSPECTION_FIXTURE_ISOLATION_OWNED_ROOT";
    private const string DebugX64Condition =
        "'$(Configuration)|$(Platform)' == 'Debug|x64'";
    private const string GalleryConstant =
        "$(DefineConstants);MODEL_INSPECTION_FIXTURE_GALLERY";
    private const string FixtureProjectFileName =
        "GraniteEdgeAI.ModelInspection.Fixtures.csproj";
    private const string ScenarioLinkRoot =
        "TestFixtures\\ModelInspectionScenarios\\";
    private const string ScenarioPackageRoot = "Fixtures\\";

    private static readonly string Root = FindRepositoryRoot();

    private static readonly string[] FixtureRoots =
    [
        "Features\\ModelInspection\\DebugFixtures",
        "Features\\Onboarding\\DebugFixtures"
    ];

    private static readonly string[] RemovedItemTypes =
    [
        "Compile",
        "Page",
        "None",
        "Content",
        "EmbeddedResource",
        "PRIResource"
    ];

    private static readonly string[] EvaluatedItemTypes =
    [
        .. RemovedItemTypes,
        "ProjectReference"
    ];

    private static readonly ProjectSpec[] Projects =
    [
        new(
            "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj",
            "..\\tests\\TestFixtures\\ModelInspectionScenarios\\",
            "..\\shared\\GraniteEdgeAI.ModelInspection.Fixtures\\" +
                FixtureProjectFileName),
        new(
            "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj",
            "..\\..\\TestFixtures\\ModelInspectionScenarios\\",
            "..\\..\\..\\shared\\GraniteEdgeAI.ModelInspection.Fixtures\\" +
                FixtureProjectFileName)
    ];

    [TestMethod]
    public void Projects_DeclareExactFailClosedDebugX64FixtureOwnership()
    {
        foreach (ProjectSpec project in Projects)
        {
            XDocument document = Load(project);
            string[] errors = ValidateProject(project, document);
            Assert.AreEqual(
                0,
                errors.Length,
                $"Fixture boundary differs in {project.RelativePath}:{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors));

            ApplyMutation(document, project, "long-package-target");
            Assert.IsGreaterThan(
                0,
                ValidateProject(project, document).Length,
                $"A deployment-unsafe package target escaped {project.RelativePath}.");
        }

        AssertReleasePageAuditHookBoundary();
    }

    private static void AssertReleasePageAuditHookBoundary()
    {
        string source = File.ReadAllText(Absolute(
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/" +
            "ModelInspectionPage.xaml.cs"));

        StringAssert.Contains(
            source,
            "() => CompleteDispatcherAuditsForFixture()");
        Assert.IsFalse(
            source.Contains(
                "AttemptCleanup(CompleteDispatcherAuditsForFixture, ref error)",
                StringComparison.Ordinal),
            "An optional Debug-only partial method cannot be converted to a " +
            "delegate in the Release build.");
        Assert.IsFalse(
            source.Contains("_fixtureDisclosureAudit", StringComparison.Ordinal),
            "The Release page must not retain fixture-named audit metadata.");
        Assert.IsFalse(
            source.Contains(
                "CompleteDisclosureAuditForFixture",
                StringComparison.Ordinal),
            "The Release page must keep disclosure-audit ownership production-neutral.");
    }

    [TestMethod]
    public void DebugFixtureCSharpSources_UseOnlyTheExactGalleryGuard()
    {
        foreach (ProjectSpec project in Projects)
        {
            string projectDirectory = Path.GetDirectoryName(Absolute(project.RelativePath))!;
            foreach (string relativePath in EnumerateFixtureFiles(project, ".cs"))
            {
                string[] lines = File.ReadAllLines(Path.Combine(projectDirectory, relativePath));
                string[] errors = ValidateSourceGuard(lines);
                Assert.AreEqual(
                    0,
                    errors.Length,
                    $"Source guard differs in {project.RelativePath}/{relativePath}: " +
                    string.Join(" | ", errors));
            }
        }
    }

    [TestMethod]
    public void SourceGuardValidator_RejectsDebugInsteadOfGallerySymbol()
    {
        foreach (ProjectSpec project in Projects)
        {
            string projectDirectory = Path.GetDirectoryName(Absolute(project.RelativePath))!;
            foreach (string relativePath in EnumerateFixtureFiles(project, ".cs"))
            {
                string[] baseline = File.ReadAllLines(Path.Combine(projectDirectory, relativePath));
                Assert.AreEqual(0, ValidateSourceGuard(baseline).Length, relativePath);

                string[] mutated = (string[])baseline.Clone();
                mutated[0] = "#if DEBUG";

                Assert.IsGreaterThan(
                    0,
                    ValidateSourceGuard(mutated).Length,
                    $"The #if DEBUG mutation escaped {project.RelativePath}/{relativePath}.");
            }
        }
    }

    [TestMethod]
    [DataRow("debug-only-condition")]
    [DataRow("missing-default-remove")]
    [DataRow("wildcard-include")]
    [DataRow("update-instead-of-include")]
    [DataRow("none-update-leakage")]
    [DataRow("replacement-json")]
    [DataRow("alternate-slash")]
    [DataRow("alternate-case")]
    [DataRow("property-indirected-condition")]
    [DataRow("property-indirected-item")]
    [DataRow("metadata-indirected-item")]
    [DataRow("extra-fixture-item")]
    public void ProjectBoundaryValidator_RejectsInMemoryMutation(string mutation)
    {
        foreach (ProjectSpec project in Projects)
        {
            XDocument document = Load(project);
            string[] baselineErrors = ValidateProject(project, document);
            Assert.AreEqual(
                0,
                baselineErrors.Length,
                $"Mutation baseline is invalid for {project.RelativePath}: " +
                string.Join(" | ", baselineErrors));

            ApplyMutation(document, project, mutation);

            string[] errors = ValidateProject(project, document);
            Assert.IsGreaterThan(
                0,
                errors.Length,
                $"The {mutation} mutation escaped {project.RelativePath}.");
        }
    }

    [TestMethod]
    [Timeout(120_000)]
    public void EvaluatedProjects_ExposeExactClosureOnlyForDebugX64WithMatchingRid()
    {
        foreach (ProjectSpec project in Projects)
        {
            foreach (string configuration in new[] { "Debug", "Release" })
            {
                foreach ((string Platform, string RuntimeIdentifier) target in
                    new[]
                    {
                        ("x86", "win-x86"),
                        ("x64", "win-x64"),
                        ("ARM64", "win-arm64")
                    })
                {
                    JsonObject evaluation = Evaluate(
                        project,
                        configuration,
                        target.Platform,
                        target.RuntimeIdentifier);
                    Assert.AreEqual(
                        configuration,
                        evaluation["Properties"]!["Configuration"]!.GetValue<string>());
                    Assert.AreEqual(
                        target.Platform,
                        evaluation["Properties"]!["Platform"]!.GetValue<string>());
                    Assert.AreEqual(
                        target.RuntimeIdentifier,
                        evaluation["Properties"]!["RuntimeIdentifier"]!.GetValue<string>());

                    bool intended = configuration == "Debug" && target.Platform == "x64";
                    AssertEvaluatedGalleryConstant(evaluation, intended);
                    AssertEvaluatedClosure(project, evaluation["Items"]!.AsObject(), intended);
                }
            }
        }
    }

    [TestMethod]
    public void ReleaseIsolationEvidenceSchema_AcceptsOnlyCanonicalPassedEvidence()
    {
        DateTimeOffset generatedUtc = DateTimeOffset.UtcNow;
        JsonObject evidence = CreateCanonicalEvidence(generatedUtc);

        string[] errors = ValidateIsolationEvidence(evidence, generatedUtc.AddMinutes(1));

        Assert.AreEqual(0, errors.Length, string.Join(" | ", errors));
    }

    [TestMethod]
    [DataRow("missing-property")]
    [DataRow("extra-property")]
    [DataRow("wrong-schema-version")]
    [DataRow("failed-status")]
    [DataRow("stale-generated-time")]
    [DataRow("future-generated-time")]
    [DataRow("invalid-source-commit")]
    [DataRow("invalid-snapshot-digest")]
    [DataRow("missing-source-snapshot-file-count")]
    [DataRow("invalid-source-snapshot-file-count-type")]
    [DataRow("zero-source-snapshot-file-count")]
    [DataRow("rooted-package-path")]
    [DataRow("different-package-directory")]
    [DataRow("different-package-extension")]
    [DataRow("traversal-layout-path")]
    [DataRow("different-layout-path")]
    [DataRow("different-main-dll-package-path")]
    [DataRow("different-resources-pri-package-path")]
    [DataRow("zero-package-hash")]
    [DataRow("main-dll-not-ready-to-run")]
    [DataRow("zero-metadata-count")]
    [DataRow("zero-scanned-files")]
    [DataRow("forbidden-path-hit")]
    [DataRow("forbidden-token-hit")]
    [DataRow("forbidden-metadata-hit")]
    [DataRow("debug-closure-drift")]
    [DataRow("release-closure-leak")]
    [DataRow("zero-debug-identities")]
    [DataRow("different-debug-identities-digest")]
    [DataRow("failed-build-provenance")]
    [DataRow("unnormalized-build-command")]
    [DataRow("missing-inspector-provenance")]
    [DataRow("missing-evidence-validator-provenance")]
    public void ReleaseIsolationEvidenceValidator_RejectsMutation(string mutation)
    {
        DateTimeOffset generatedUtc = DateTimeOffset.UtcNow;
        JsonObject evidence = CreateCanonicalEvidence(generatedUtc);
        Assert.AreEqual(
            0,
            ValidateIsolationEvidence(evidence, generatedUtc.AddMinutes(1)).Length);

        ApplyEvidenceMutation(evidence, mutation);

        string[] errors = ValidateIsolationEvidence(evidence, generatedUtc.AddMinutes(1));
        Assert.IsGreaterThan(
            0,
            errors.Length,
            $"The {mutation} isolation-evidence mutation escaped validation.");
    }

    [TestMethod]
    public void ManagedMetadataInspector_DetectsFixtureAssemblyAsForbiddenControl()
    {
        ManagedAssemblyInspection contractInspection = InspectManagedAssembly(
            typeof(ModelInspectionFixtureBuildBoundaryContractTests).Assembly.Location);
        Assert.IsTrue(
            contractInspection.ForbiddenMetadataHits.Contains(
                "AssemblyReference:GraniteEdgeAI.ModelInspection.Fixtures",
                StringComparer.Ordinal),
            "The committed inspector did not detect the exact Fixtures assembly reference.");
        Assert.IsTrue(
            contractInspection.ForbiddenMetadataHits.Any(hit =>
                hit.StartsWith(
                    "TypeReference:GraniteEdgeAI.ModelInspection.Fixtures.",
                    StringComparison.Ordinal)),
            "The committed inspector did not detect a Fixtures type reference.");

        ManagedAssemblyInspection inspection = InspectManagedAssembly(
            typeof(ModelInspectionFixtureCatalogue).Assembly.Location);

        Assert.IsGreaterThan(0, inspection.MetadataTableCounts.AssemblyReference);
        Assert.IsGreaterThan(0, inspection.MetadataTableCounts.TypeDefinition);
        Assert.IsGreaterThan(0, inspection.MetadataTableCounts.TypeReference);
        Assert.IsGreaterThan(
            0,
            inspection.ForbiddenMetadataHits.Count,
            "The committed inspector did not detect its fixture-positive control.");
        Assert.IsTrue(inspection.ForbiddenMetadataHits.Any(hit =>
            hit.Contains("ModelInspectionFixture", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ReleaseMainAssemblyMetadata_IsFixtureFreeWhenInvokedByIsolationScript()
    {
        string? mainDllPath = Environment.GetEnvironmentVariable(
            ReleaseMainDllEnvironment);
        string? resultPath = Environment.GetEnvironmentVariable(
            ReleaseInspectionResultEnvironment);
        string? ownedRoot = Environment.GetEnvironmentVariable(
            IsolationOwnedRootEnvironment);
        bool noInvocation = mainDllPath is null && resultPath is null && ownedRoot is null;
        if (noInvocation)
        {
            ManagedAssemblyInspection control = InspectManagedAssembly(
                typeof(ModelInspectionFixtureCatalogue).Assembly.Location);
            Assert.IsGreaterThan(0, control.MetadataTableCounts.TypeDefinition);
            Assert.IsGreaterThan(0, control.ForbiddenMetadataHits.Count);
            return;
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(mainDllPath));
        Assert.IsFalse(string.IsNullOrWhiteSpace(resultPath));
        Assert.IsFalse(string.IsNullOrWhiteSpace(ownedRoot));
        string canonicalRoot = RequireCanonicalOwnedRoot(ownedRoot!);
        string canonicalMainDll = RequireCanonicalOwnedChild(
            mainDllPath!,
            canonicalRoot,
            mustExist: true);
        string canonicalResult = RequireCanonicalOwnedChild(
            resultPath!,
            canonicalRoot,
            mustExist: false);
        Assert.IsTrue(
            string.Equals(
                Path.Combine(
                    canonicalRoot,
                    "release",
                    "layout",
                    "IBM Granite with TurboQuant (Intel).dll"),
                canonicalMainDll,
                StringComparison.OrdinalIgnoreCase),
            "Metadata inspection must target the exact packaged application DLL.");
        Assert.IsTrue(
            string.Equals(
                Path.Combine(
                    canonicalRoot,
                    "release",
                    "inspection",
                    "metadata-result.json"),
                canonicalResult,
                StringComparison.OrdinalIgnoreCase),
            "Metadata inspection result has an unexpected owned path.");
        Assert.IsFalse(File.Exists(canonicalResult), "Inspection result must not pre-exist.");

        ManagedAssemblyInspection inspection = InspectManagedAssembly(canonicalMainDll);
        Assert.IsTrue(inspection.ReadyToRun, "Release main DLL is not ReadyToRun.");
        Assert.AreEqual(
            0,
            inspection.ForbiddenMetadataHits.Count,
            string.Join(Environment.NewLine, inspection.ForbiddenMetadataHits));

        WriteInspectionResult(canonicalResult, inspection);
    }

    [TestMethod]
    public void ReleaseIsolationEvidence_IsCanonicalWhenInvokedByIsolationScript()
    {
        ValidateReleaseEvidenceInvocation();
    }

    private static string[] ValidateProject(ProjectSpec project, XDocument document)
    {
        var errors = new List<string>();
        var accepted = new HashSet<XElement>();

        XElement[] galleryDefinitions = document.Descendants("DefineConstants")
            .Where(element => element.Value.Contains(
                "MODEL_INSPECTION_FIXTURE_GALLERY",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (galleryDefinitions.Length != 1)
        {
            errors.Add($"Expected one gallery constant definition, found {galleryDefinitions.Length}.");
        }
        else
        {
            XElement definition = galleryDefinitions[0];
            if (!string.Equals(definition.Value, GalleryConstant, StringComparison.Ordinal))
            {
                errors.Add("Gallery constant definition is not the exact literal.");
            }

            if (!HasExactParentCondition(definition, "PropertyGroup"))
            {
                errors.Add("Gallery constant is not owned by the literal Debug|x64 condition.");
            }
        }

        foreach (string fixtureRoot in FixtureRoots)
        {
            foreach (string itemType in RemovedItemTypes)
            {
                string expectedRemove = fixtureRoot + "\\**";
                XElement[] matches = document.Descendants(itemType)
                    .Where(item =>
                        string.Equals(
                            item.Attribute("Remove")?.Value,
                            expectedRemove,
                            StringComparison.Ordinal) &&
                        item.Attribute("Include") is null &&
                        item.Attribute("Update") is null &&
                        item.Attribute("Condition") is null &&
                        item.Parent?.Name.LocalName == "ItemGroup" &&
                        item.Parent.Attribute("Condition") is null &&
                        !item.Elements().Any())
                    .ToArray();
                if (matches.Length != 1)
                {
                    errors.Add($"Expected one unconditional {itemType} Remove={expectedRemove}, " +
                        $"found {matches.Length}.");
                }

                foreach (XElement match in matches)
                {
                    accepted.Add(match);
                }
            }
        }

        ValidatePhysicalIncludes(project, document, "Compile", ".cs", accepted, errors);
        ValidatePhysicalIncludes(project, document, "Page", ".xaml", accepted, errors);
        ValidateFixtureReference(project, document, accepted, errors);
        ValidateScenarioContent(project, document, accepted, errors);

        XElement[] unaccepted = document
            .Descendants()
            .Where(IsProjectItem)
            .Where(HasFixtureRelatedPath)
            .Where(item => !accepted.Contains(item))
            .ToArray();
        foreach (XElement item in unaccepted)
        {
            errors.Add("Unexpected fixture ownership: " + item.ToString(SaveOptions.DisableFormatting));
        }

        return errors.ToArray();
    }

    private static string[] ValidateSourceGuard(IReadOnlyList<string> lines)
    {
        var errors = new List<string>();
        if (lines.Count == 0 ||
            !string.Equals(
                lines[0],
                "#if MODEL_INSPECTION_FIXTURE_GALLERY",
                StringComparison.Ordinal))
        {
            errors.Add("First line is not the exact gallery guard.");
        }

        if (lines.Count == 0 ||
            !string.Equals(lines[^1], "#endif", StringComparison.Ordinal))
        {
            errors.Add("Final line is not the matching #endif.");
        }

        string[] conditionalDirectives = lines
            .Select(line => line.Trim())
            .Where(line =>
                line.StartsWith("#if", StringComparison.Ordinal) ||
                line.StartsWith("#elif", StringComparison.Ordinal) ||
                line.StartsWith("#else", StringComparison.Ordinal) ||
                line.StartsWith("#endif", StringComparison.Ordinal))
            .ToArray();
        string[] expectedDirectives =
        [
            "#if MODEL_INSPECTION_FIXTURE_GALLERY",
            "#endif"
        ];
        if (!expectedDirectives.SequenceEqual(conditionalDirectives, StringComparer.Ordinal))
        {
            errors.Add("Source contains a broader, alternate, or nested conditional guard.");
        }

        return errors.ToArray();
    }

    private static JsonObject CreateCanonicalEvidence(DateTimeOffset generatedUtc)
    {
        int compileCount = EnumerateFixtureFiles(Projects[0], ".cs").Length;
        int pageCount = EnumerateFixtureFiles(Projects[0], ".xaml").Length;
        string[] debugIdentities = ExpectedDebugIdentityLines();
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["status"] = "passed",
            ["generatedUtc"] = generatedUtc.UtcDateTime.ToString(
                "O",
                System.Globalization.CultureInfo.InvariantCulture),
            ["sourceCommit"] = new string('a', 40),
            ["sourceSnapshotSha256"] = new string('b', 64),
            ["sourceSnapshotFileCount"] = 600,
            ["ownedRoot"] = "<owned-root>",
            ["configuration"] = "Release",
            ["platform"] = "x64",
            ["runtime"] = "win-x64",
            ["packagePath"] = "release/package/GraniteEdgeAI.msix",
            ["packageSha256"] = new string('c', 64),
            ["layoutPath"] = "release/layout",
            ["mainDll"] = new JsonObject
            {
                ["packagePath"] = "IBM Granite with TurboQuant (Intel).dll",
                ["sha256"] = new string('d', 64),
                ["readyToRun"] = true,
                ["metadataTableCounts"] = new JsonObject
                {
                    ["assemblyReference"] = 12,
                    ["typeDefinition"] = 200,
                    ["nestedClass"] = 8,
                    ["typeReference"] = 80,
                    ["exportedType"] = 0,
                    ["manifestResource"] = 1
                }
            },
            ["resourcesPri"] = new JsonObject
            {
                ["packagePath"] = "resources.pri",
                ["sha256"] = new string('e', 64)
            },
            ["scannedFileCount"] = 30,
            ["releaseForbiddenPathHits"] = new JsonArray(),
            ["releaseForbiddenTokenHits"] = new JsonArray(),
            ["releaseForbiddenMetadataHits"] = new JsonArray(),
            ["debugEvaluatedCounts"] = EvaluatedCounts(
                projectReference: 1,
                compile: compileCount,
                page: pageCount,
                content: 52),
            ["debugEvaluatedIdentityCount"] = debugIdentities.Length,
            ["debugExactIdentitiesSha256"] = HashCanonicalLines(debugIdentities),
            ["releaseEvaluatedCounts"] = EvaluatedCounts(
                projectReference: 0,
                compile: 0,
                page: 0,
                content: 0),
            ["buildProvenance"] = new JsonArray
            {
                BuildProvenance(
                    "Release",
                    "msbuild <app-project> /t:Restore,Build /p:Configuration=Release " +
                    "/p:Platform=x64 /p:RuntimeIdentifier=win-x64 " +
                    "/p:GenerateAppxPackageOnBuild=true " +
                    "/p:AppxPackageDir=<owned-root>/release/package"),
                BuildProvenance(
                    "Debug",
                    "msbuild <app-project> /t:Restore,Build /p:Configuration=Debug " +
                    "/p:Platform=x64 /p:RuntimeIdentifier=win-x64 " +
                    "/p:GenerateAppxPackageOnBuild=false"),
                BuildProvenance(
                    "Release",
                    "contract-test <release-main-assembly-metadata-filter> <owned-root>"),
                BuildProvenance(
                    "Release",
                    "contract-test <release-isolation-evidence-filter> <owned-root>")
            }
        };
    }

    private static JsonObject EvaluatedCounts(
        int projectReference,
        int compile,
        int page,
        int content) => new()
    {
        ["projectReference"] = projectReference,
        ["compile"] = compile,
        ["page"] = page,
        ["none"] = 0,
        ["content"] = content,
        ["embeddedResource"] = 0,
        ["priResource"] = 0,
        ["jsonPackageContent"] = content
    };

    private static JsonObject BuildProvenance(string configuration, string command) => new()
    {
        ["configuration"] = configuration,
        ["platform"] = "x64",
        ["runtime"] = "win-x64",
        ["command"] = command,
        ["exitCode"] = 0
    };

    private static string[] ValidateIsolationEvidence(
        JsonObject evidence,
        DateTimeOffset observedUtc)
    {
        var errors = new List<string>();
        RequireExactProperties(
            evidence,
            new[]
            {
                "schemaVersion",
                "status",
                "generatedUtc",
                "sourceCommit",
                "sourceSnapshotSha256",
                "sourceSnapshotFileCount",
                "ownedRoot",
                "configuration",
                "platform",
                "runtime",
                "packagePath",
                "packageSha256",
                "layoutPath",
                "mainDll",
                "resourcesPri",
                "scannedFileCount",
                "releaseForbiddenPathHits",
                "releaseForbiddenTokenHits",
                "releaseForbiddenMetadataHits",
                "debugEvaluatedCounts",
                "debugEvaluatedIdentityCount",
                "debugExactIdentitiesSha256",
                "releaseEvaluatedCounts",
                "buildProvenance"
            },
            "root",
            errors);

        RequireInt(evidence, "schemaVersion", 1, errors);
        RequireString(evidence, "status", "passed", errors);
        ValidateGeneratedUtc(evidence, observedUtc, errors);
        RequireLowerHex(evidence, "sourceCommit", 40, errors);
        RequireLowerHex(evidence, "sourceSnapshotSha256", 64, errors);
        RequirePositiveInt(evidence, "sourceSnapshotFileCount", errors);
        RequireString(evidence, "ownedRoot", "<owned-root>", errors);
        RequireString(evidence, "configuration", "Release", errors);
        RequireString(evidence, "platform", "x64", errors);
        RequireString(evidence, "runtime", "win-x64", errors);
        RequireSafeRelativePath(evidence, "packagePath", errors);
        ValidatePackageEvidencePath(evidence, errors);
        RequireLowerHex(evidence, "packageSha256", 64, errors);
        RequireSafeRelativePath(evidence, "layoutPath", errors);
        RequireString(evidence, "layoutPath", "release/layout", errors);
        RequirePositiveInt(evidence, "scannedFileCount", errors);
        RequireEmptyArray(evidence, "releaseForbiddenPathHits", errors);
        RequireEmptyArray(evidence, "releaseForbiddenTokenHits", errors);
        RequireEmptyArray(evidence, "releaseForbiddenMetadataHits", errors);
        RequirePositiveInt(evidence, "debugEvaluatedIdentityCount", errors);
        RequireLowerHex(evidence, "debugExactIdentitiesSha256", 64, errors);

        JsonObject? mainDll = RequireObject(evidence, "mainDll", errors);
        if (mainDll is not null)
        {
            RequireExactProperties(
                mainDll,
                new[] { "packagePath", "sha256", "readyToRun", "metadataTableCounts" },
                "mainDll",
                errors);
            RequireSafeRelativePath(mainDll, "packagePath", errors);
            RequireString(
                mainDll,
                "packagePath",
                "IBM Granite with TurboQuant (Intel).dll",
                errors);
            RequireLowerHex(mainDll, "sha256", 64, errors);
            RequireBool(mainDll, "readyToRun", expected: true, errors);
            JsonObject? counts = RequireObject(mainDll, "metadataTableCounts", errors);
            if (counts is not null)
            {
                string[] countNames =
                [
                    "assemblyReference",
                    "typeDefinition",
                    "nestedClass",
                    "typeReference",
                    "exportedType",
                    "manifestResource"
                ];
                RequireExactProperties(counts, countNames, "metadataTableCounts", errors);
                foreach (string countName in countNames)
                {
                    RequireNonNegativeInt(counts, countName, errors);
                }

                foreach (string requiredPositive in new[]
                         {
                             "assemblyReference", "typeDefinition", "typeReference"
                         })
                {
                    RequirePositiveInt(counts, requiredPositive, errors);
                }
            }
        }

        JsonObject? resourcesPri = RequireObject(evidence, "resourcesPri", errors);
        if (resourcesPri is not null)
        {
            RequireExactProperties(
                resourcesPri,
                new[] { "packagePath", "sha256" },
                "resourcesPri",
                errors);
            RequireSafeRelativePath(resourcesPri, "packagePath", errors);
            RequireString(resourcesPri, "packagePath", "resources.pri", errors);
            RequireLowerHex(resourcesPri, "sha256", 64, errors);
        }

        JsonObject? debugCounts = RequireObject(evidence, "debugEvaluatedCounts", errors);
        if (debugCounts is not null)
        {
            int compileCount = EnumerateFixtureFiles(Projects[0], ".cs").Length;
            int pageCount = EnumerateFixtureFiles(Projects[0], ".xaml").Length;
            ValidateEvaluatedCounts(
                debugCounts,
                expectedProjectReference: 1,
                expectedCompile: compileCount,
                expectedPage: pageCount,
                expectedContent: 52,
                "debugEvaluatedCounts",
                errors);
            int? identities = TryGetInt(evidence, "debugEvaluatedIdentityCount", errors);
            string[] expectedIdentityLines = ExpectedDebugIdentityLines();
            int expectedIdentities = expectedIdentityLines.Length;
            if (identities is not null && identities.Value != expectedIdentities)
            {
                errors.Add($"debugEvaluatedIdentityCount must be {expectedIdentities}.");
            }

            RequireString(
                evidence,
                "debugExactIdentitiesSha256",
                HashCanonicalLines(expectedIdentityLines),
                errors);
        }

        JsonObject? releaseCounts = RequireObject(evidence, "releaseEvaluatedCounts", errors);
        if (releaseCounts is not null)
        {
            ValidateEvaluatedCounts(
                releaseCounts,
                expectedProjectReference: 0,
                expectedCompile: 0,
                expectedPage: 0,
                expectedContent: 0,
                "releaseEvaluatedCounts",
                errors);
        }

        ValidateBuildProvenance(evidence, errors);
        return errors.ToArray();
    }

    private static void ValidateGeneratedUtc(
        JsonObject evidence,
        DateTimeOffset observedUtc,
        ICollection<string> errors)
    {
        string? value = TryGetString(evidence, "generatedUtc", errors);
        if (value is null ||
            !DateTimeOffset.TryParseExact(
                value,
                "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTimeOffset generated) ||
            generated.Offset != TimeSpan.Zero)
        {
            errors.Add("generatedUtc must be an exact UTC round-trip timestamp.");
            return;
        }

        TimeSpan age = observedUtc - generated;
        if (age < TimeSpan.FromMinutes(-1) || age > TimeSpan.FromMinutes(30))
        {
            errors.Add("generatedUtc is stale or unreasonably in the future.");
        }
    }

    private static void ValidatePackageEvidencePath(
        JsonObject evidence,
        ICollection<string> errors)
    {
        string? value = TryGetString(evidence, "packagePath", errors);
        const string prefix = "release/package/";
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            errors.Add("packagePath must be under exact release/package/.");
            return;
        }

        string leaf = value[prefix.Length..];
        if (leaf.Length == 0 ||
            leaf.Contains('/') ||
            (!leaf.EndsWith(".msix", StringComparison.Ordinal) &&
             !leaf.EndsWith(".appx", StringComparison.Ordinal)))
        {
            errors.Add("packagePath must have one exact .msix or .appx package leaf.");
        }
    }

    private static void ValidateEvaluatedCounts(
        JsonObject counts,
        int expectedProjectReference,
        int expectedCompile,
        int expectedPage,
        int expectedContent,
        string label,
        ICollection<string> errors)
    {
        string[] names =
        [
            "projectReference",
            "compile",
            "page",
            "none",
            "content",
            "embeddedResource",
            "priResource",
            "jsonPackageContent"
        ];
        RequireExactProperties(counts, names, label, errors);
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["projectReference"] = expectedProjectReference,
            ["compile"] = expectedCompile,
            ["page"] = expectedPage,
            ["none"] = 0,
            ["content"] = expectedContent,
            ["embeddedResource"] = 0,
            ["priResource"] = 0,
            ["jsonPackageContent"] = expectedContent
        };
        foreach ((string name, int expectedValue) in expected)
        {
            RequireInt(counts, name, expectedValue, errors);
        }
    }

    private static void ValidateBuildProvenance(
        JsonObject evidence,
        ICollection<string> errors)
    {
        JsonArray? provenance = evidence["buildProvenance"] as JsonArray;
        if (provenance is null)
        {
            errors.Add("buildProvenance must be an array.");
            return;
        }

        (string Configuration, string Command)[] expected =
        [
            (
                "Release",
                "msbuild <app-project> /t:Restore,Build /p:Configuration=Release " +
                "/p:Platform=x64 /p:RuntimeIdentifier=win-x64 " +
                "/p:GenerateAppxPackageOnBuild=true " +
                "/p:AppxPackageDir=<owned-root>/release/package"),
            (
                "Debug",
                "msbuild <app-project> /t:Restore,Build /p:Configuration=Debug " +
                "/p:Platform=x64 /p:RuntimeIdentifier=win-x64 " +
                "/p:GenerateAppxPackageOnBuild=false"),
            (
                "Release",
                "contract-test <release-main-assembly-metadata-filter> <owned-root>"),
            (
                "Release",
                "contract-test <release-isolation-evidence-filter> <owned-root>")
        ];
        if (provenance.Count != expected.Length)
        {
            errors.Add($"buildProvenance must contain exactly {expected.Length} ordered rows.");
        }

        for (int index = 0; index < provenance.Count; index++)
        {
            JsonNode? entryNode = provenance[index];
            if (entryNode is not JsonObject entry)
            {
                errors.Add("Every buildProvenance entry must be an object.");
                continue;
            }

            RequireExactProperties(
                entry,
                new[] { "configuration", "platform", "runtime", "command", "exitCode" },
                "buildProvenance entry",
                errors);
            if (index >= expected.Length)
            {
                continue;
            }

            RequireString(entry, "configuration", expected[index].Configuration, errors);
            RequireString(entry, "platform", "x64", errors);
            RequireString(entry, "runtime", "win-x64", errors);
            RequireInt(entry, "exitCode", 0, errors);
            RequireString(entry, "command", expected[index].Command, errors);
        }
    }

    private static void RequireExactProperties(
        JsonObject value,
        IEnumerable<string> expected,
        string label,
        ICollection<string> errors)
    {
        string[] expectedNames = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] actualNames = value.Select(property => property.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal))
        {
            errors.Add($"{label} properties differ. Expected " +
                $"[{string.Join(", ", expectedNames)}], actual [{string.Join(", ", actualNames)}].");
        }
    }

    private static JsonObject? RequireObject(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        if (owner[name] is JsonObject value)
        {
            return value;
        }

        errors.Add($"{name} must be an object.");
        return null;
    }

    private static string? TryGetString(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        try
        {
            string? value = owner[name]?.GetValue<string>();
            if (value is null)
            {
                errors.Add($"{name} must be a string.");
            }

            return value;
        }
        catch (InvalidOperationException)
        {
            errors.Add($"{name} must be a string.");
            return null;
        }
    }

    private static int? TryGetInt(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        try
        {
            if (owner[name] is null)
            {
                errors.Add($"{name} must be an integer.");
                return null;
            }

            return owner[name]!.GetValue<int>();
        }
        catch (InvalidOperationException)
        {
            errors.Add($"{name} must be an integer.");
            return null;
        }
    }

    private static void RequireString(
        JsonObject owner,
        string name,
        string expected,
        ICollection<string> errors)
    {
        string? value = TryGetString(owner, name, errors);
        if (value is not null && !string.Equals(value, expected, StringComparison.Ordinal))
        {
            errors.Add($"{name} must be exact value {expected}.");
        }
    }

    private static void RequireInt(
        JsonObject owner,
        string name,
        int expected,
        ICollection<string> errors)
    {
        int? value = TryGetInt(owner, name, errors);
        if (value is not null && value.Value != expected)
        {
            errors.Add($"{name} must be {expected}.");
        }
    }

    private static void RequirePositiveInt(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        int? value = TryGetInt(owner, name, errors);
        if (value is not null && value.Value <= 0)
        {
            errors.Add($"{name} must be positive.");
        }
    }

    private static void RequireNonNegativeInt(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        int? value = TryGetInt(owner, name, errors);
        if (value is not null && value.Value < 0)
        {
            errors.Add($"{name} must be non-negative.");
        }
    }

    private static void RequireBool(
        JsonObject owner,
        string name,
        bool expected,
        ICollection<string> errors)
    {
        try
        {
            if (owner[name] is null || owner[name]!.GetValue<bool>() != expected)
            {
                errors.Add($"{name} must be {expected}.");
            }
        }
        catch (InvalidOperationException)
        {
            errors.Add($"{name} must be a Boolean.");
        }
    }

    private static void RequireLowerHex(
        JsonObject owner,
        string name,
        int length,
        ICollection<string> errors)
    {
        string? value = TryGetString(owner, name, errors);
        if (value is null)
        {
            return;
        }

        if (value.Length != length ||
            value.Any(character => character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')) ||
            value.All(character => character == '0'))
        {
            errors.Add($"{name} must be a nonzero lowercase {length}-digit hex value.");
        }
    }

    private static void RequireSafeRelativePath(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        string? value = TryGetString(owner, name, errors);
        if (value is null)
        {
            return;
        }

        string[] segments = value.Split('/');
        if (value.Length == 0 ||
            Path.IsPathRooted(value) ||
            value.Contains('\\') ||
            value.Contains(':') ||
            segments.Any(segment => segment.Length == 0 || segment is "." or "..") ||
            value.Any(char.IsControl))
        {
            errors.Add($"{name} must be a normalized safe relative path.");
        }
    }

    private static void RequireEmptyArray(
        JsonObject owner,
        string name,
        ICollection<string> errors)
    {
        if (owner[name] is not JsonArray array || array.Count != 0)
        {
            errors.Add($"{name} must be an exact empty array.");
        }
    }

    private static void ApplyEvidenceMutation(JsonObject evidence, string mutation)
    {
        switch (mutation)
        {
            case "missing-property":
                evidence.Remove("packageSha256");
                break;
            case "extra-property":
                evidence["unexpected"] = true;
                break;
            case "wrong-schema-version":
                evidence["schemaVersion"] = 2;
                break;
            case "failed-status":
                evidence["status"] = "failed";
                break;
            case "stale-generated-time":
                evidence["generatedUtc"] = DateTimeOffset.UtcNow.AddDays(-1)
                    .UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "future-generated-time":
                evidence["generatedUtc"] = DateTimeOffset.UtcNow.AddHours(1)
                    .UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                break;
            case "invalid-source-commit":
                evidence["sourceCommit"] = new string('A', 40);
                break;
            case "invalid-snapshot-digest":
                evidence["sourceSnapshotSha256"] = new string('0', 64);
                break;
            case "missing-source-snapshot-file-count":
                evidence.Remove("sourceSnapshotFileCount");
                break;
            case "invalid-source-snapshot-file-count-type":
                evidence["sourceSnapshotFileCount"] = "600";
                break;
            case "zero-source-snapshot-file-count":
                evidence["sourceSnapshotFileCount"] = 0;
                break;
            case "rooted-package-path":
                evidence["packagePath"] = "C:/private/package.msix";
                break;
            case "different-package-directory":
                evidence["packagePath"] = "release/packages/GraniteEdgeAI.msix";
                break;
            case "different-package-extension":
                evidence["packagePath"] = "release/package/GraniteEdgeAI.zip";
                break;
            case "traversal-layout-path":
                evidence["layoutPath"] = "../layout";
                break;
            case "different-layout-path":
                evidence["layoutPath"] = "release/other-layout";
                break;
            case "different-main-dll-package-path":
                evidence["mainDll"]!["packagePath"] = "bin/GraniteEdgeAI.dll";
                break;
            case "different-resources-pri-package-path":
                evidence["resourcesPri"]!["packagePath"] = "assets/resources.pri";
                break;
            case "zero-package-hash":
                evidence["packageSha256"] = new string('0', 64);
                break;
            case "main-dll-not-ready-to-run":
                evidence["mainDll"]!["readyToRun"] = false;
                break;
            case "zero-metadata-count":
                evidence["mainDll"]!["metadataTableCounts"]!["typeDefinition"] = 0;
                break;
            case "zero-scanned-files":
                evidence["scannedFileCount"] = 0;
                break;
            case "forbidden-path-hit":
                evidence["releaseForbiddenPathHits"]!.AsArray().Add("fixture.json");
                break;
            case "forbidden-token-hit":
                evidence["releaseForbiddenTokenHits"]!.AsArray().Add("Fixture gallery");
                break;
            case "forbidden-metadata-hit":
                evidence["releaseForbiddenMetadataHits"]!.AsArray().Add("DebugFixtures.Type");
                break;
            case "debug-closure-drift":
                evidence["debugEvaluatedCounts"]!["compile"] = 999;
                break;
            case "release-closure-leak":
                evidence["releaseEvaluatedCounts"]!["content"] = 1;
                break;
            case "zero-debug-identities":
                evidence["debugEvaluatedIdentityCount"] = 0;
                break;
            case "different-debug-identities-digest":
                evidence["debugExactIdentitiesSha256"] = new string('9', 64);
                break;
            case "failed-build-provenance":
                evidence["buildProvenance"]![0]!["exitCode"] = 1;
                break;
            case "unnormalized-build-command":
                evidence["buildProvenance"]![0]!["command"] =
                    "dotnet build C:\\Users\\private\\app.csproj";
                break;
            case "missing-inspector-provenance":
                evidence["buildProvenance"]!.AsArray().RemoveAt(2);
                break;
            case "missing-evidence-validator-provenance":
                evidence["buildProvenance"]!.AsArray().RemoveAt(3);
                break;
            default:
                Assert.Fail("Unknown evidence mutation: " + mutation);
                break;
        }
    }

    private static void ValidatePhysicalIncludes(
        ProjectSpec project,
        XDocument document,
        string itemType,
        string extension,
        ISet<XElement> accepted,
        ICollection<string> errors)
    {
        string[] expected = EnumerateFixtureFiles(project, extension);
        XElement[] exact = document.Descendants(itemType)
            .Where(item =>
                item.Attribute("Include") is not null &&
                expected.Contains(item.Attribute("Include")!.Value, StringComparer.Ordinal))
            .ToArray();

        string[] actual = exact
            .Where(item => HasExactParentCondition(item, "ItemGroup"))
            .Where(item => item.Attribute("Update") is null)
            .Where(item => !ContainsWildcard(item.Attribute("Include")!.Value))
            .Where(item => ItemMetadataIsExact(item, itemType))
            .Select(item => item.Attribute("Include")!.Value)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            errors.Add($"{itemType} physical closure differs. Expected " +
                $"[{string.Join(", ", expected)}], actual [{string.Join(", ", actual)}].");
        }

        foreach (XElement item in exact.Where(item =>
                     HasExactParentCondition(item, "ItemGroup") &&
                     item.Attribute("Update") is null &&
                     !ContainsWildcard(item.Attribute("Include")!.Value) &&
                     ItemMetadataIsExact(item, itemType)))
        {
            accepted.Add(item);
        }
    }

    private static void ValidateFixtureReference(
        ProjectSpec project,
        XDocument document,
        ISet<XElement> accepted,
        ICollection<string> errors)
    {
        XElement[] references = document.Descendants("ProjectReference")
            .Where(HasFixtureRelatedPath)
            .ToArray();
        XElement[] exact = references.Where(reference =>
                string.Equals(
                    reference.Attribute("Include")?.Value,
                    project.FixtureProjectReference,
                    StringComparison.Ordinal) &&
                reference.Attribute("Update") is null &&
                HasExactParentCondition(reference, "ItemGroup") &&
                !reference.Elements().Any())
            .ToArray();
        if (exact.Length != 1)
        {
            errors.Add($"Expected one exact conditional fixture ProjectReference, found {exact.Length}.");
        }

        foreach (XElement reference in exact)
        {
            accepted.Add(reference);
        }
    }

    private static void ValidateScenarioContent(
        ProjectSpec project,
        XDocument document,
        ISet<XElement> accepted,
        ICollection<string> errors)
    {
        string[] scenarioFiles = ExpectedScenarioFileNames();
        if (scenarioFiles.Length != 52)
        {
            errors.Add($"Expected 52 scenario JSON files, found {scenarioFiles.Length}.");
        }

        var actual = new List<string>();
        foreach (string fileName in scenarioFiles)
        {
            string include = project.ScenarioIncludePrefix + fileName;
            string linkPath = ScenarioLinkRoot + fileName;
            string packagePath = ScenarioPackageRoot + fileName;
            XElement[] exact = document.Descendants("Content")
                .Where(item =>
                    string.Equals(item.Attribute("Include")?.Value, include, StringComparison.Ordinal) &&
                    item.Attribute("Update") is null &&
                    HasExactParentCondition(item, "ItemGroup") &&
                    string.Equals(item.Element("Link")?.Value, linkPath, StringComparison.Ordinal) &&
                    string.Equals(item.Element("TargetPath")?.Value, packagePath, StringComparison.Ordinal) &&
                    string.Equals(
                        item.Element("CopyToOutputDirectory")?.Value,
                        "PreserveNewest",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        item.Element("CopyToPublishDirectory")?.Value,
                        "PreserveNewest",
                        StringComparison.Ordinal) &&
                    item.Elements().Select(element => element.Name.LocalName)
                        .SequenceEqual(
                            new[]
                            {
                                "Link",
                                "TargetPath",
                                "CopyToOutputDirectory",
                                "CopyToPublishDirectory"
                            },
                            StringComparer.Ordinal))
                .ToArray();
            if (exact.Length != 1)
            {
                errors.Add($"Scenario ownership for {fileName} has {exact.Length} exact entries.");
            }

            foreach (XElement item in exact)
            {
                accepted.Add(item);
                actual.Add(fileName);
            }
        }

        if (!scenarioFiles.SequenceEqual(actual.OrderBy(file => file, StringComparer.Ordinal)))
        {
            errors.Add("Scenario Content closure is not the exact 52-file set.");
        }
    }

    private static bool ItemMetadataIsExact(XElement item, string itemType)
    {
        if (item.Attribute("Condition") is not null)
        {
            return false;
        }

        if (itemType == "Compile")
        {
            return !item.Elements().Any();
        }

        return item.Elements().Select(element => element.Name.LocalName)
            .SequenceEqual(new[] { "Generator" }, StringComparer.Ordinal) &&
            string.Equals(item.Element("Generator")?.Value, "MSBuild:Compile", StringComparison.Ordinal);
    }

    private static bool HasExactParentCondition(XElement element, string parentName) =>
        element.Attribute("Condition") is null &&
        element.Parent?.Name.LocalName == parentName &&
        string.Equals(
            element.Parent.Attribute("Condition")?.Value,
            DebugX64Condition,
            StringComparison.Ordinal);

    private static bool IsProjectItem(XElement element) =>
        element.Parent?.Name.LocalName == "ItemGroup";

    private static bool HasFixtureRelatedPath(XElement item) =>
        new[] { "Include", "Update", "Remove" }
            .Select(attribute => item.Attribute(attribute)?.Value)
            .Concat(item.Elements().Select(metadata => metadata.Value))
            .Where(value => value is not null)
            .Any(value => IsFixtureRelatedValue(value!, item.Document?.Root));

    private static bool IsFixtureRelatedValue(string value, XElement? projectRoot)
    {
        string detected = value.Replace('/', '\\');
        if (detected.Contains("\\DebugFixtures", StringComparison.OrdinalIgnoreCase) ||
            detected.Contains("ModelInspectionScenarios", StringComparison.OrdinalIgnoreCase) ||
            detected.EndsWith(FixtureProjectFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (projectRoot is null)
        {
            return false;
        }

        foreach (XElement property in projectRoot.Elements("PropertyGroup").Elements())
        {
            string token = "$(" + property.Name.LocalName + ")";
            if (value.Contains(token, StringComparison.Ordinal) &&
                IsDirectFixtureValue(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDirectFixtureValue(string value)
    {
        string detected = value.Replace('/', '\\');
        return detected.Contains("\\DebugFixtures", StringComparison.OrdinalIgnoreCase) ||
            detected.Contains("ModelInspectionScenarios", StringComparison.OrdinalIgnoreCase) ||
            detected.EndsWith(FixtureProjectFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsWildcard(string path) =>
        path.Contains('*') || path.Contains('?');

    private static string[] EnumerateFixtureFiles(ProjectSpec project, string extension)
    {
        string projectDirectory = Path.GetDirectoryName(Absolute(project.RelativePath))!;
        return FixtureRoots
            .Select(root => Path.Combine(projectDirectory, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(
                root,
                "*" + extension,
                SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(projectDirectory, path).Replace('/', '\\'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ApplyMutation(
        XDocument document,
        ProjectSpec project,
        string mutation)
    {
        XElement FirstFixtureCompile() => document.Descendants("Compile")
            .First(item => item.Attribute("Include")?.Value.Contains(
                "DebugFixtures",
                StringComparison.Ordinal) == true);
        XElement FirstScenarioContent() => document.Descendants("Content")
            .First(item => item.Attribute("Include")?.Value.Contains(
                "ModelInspectionScenarios",
                StringComparison.Ordinal) == true);

        switch (mutation)
        {
            case "debug-only-condition":
                document.Descendants("DefineConstants")
                    .Single(element => element.Value.Contains(
                        "MODEL_INSPECTION_FIXTURE_GALLERY",
                        StringComparison.Ordinal))
                    .Parent!
                    .SetAttributeValue("Condition", "'$(Configuration)' == 'Debug'");
                break;
            case "missing-default-remove":
                document.Descendants("Compile")
                    .Single(item => string.Equals(
                        item.Attribute("Remove")?.Value,
                        FixtureRoots[0] + "\\**",
                        StringComparison.Ordinal))
                    .Remove();
                break;
            case "wildcard-include":
                FirstFixtureCompile().SetAttributeValue(
                    "Include",
                    FixtureRoots[0] + "\\**\\*.cs");
                break;
            case "update-instead-of-include":
            {
                XElement item = FirstScenarioContent();
                string value = item.Attribute("Include")!.Value;
                item.Attribute("Include")!.Remove();
                item.SetAttributeValue("Update", value);
                break;
            }
            case "none-update-leakage":
                FirstFixtureCompile().Parent!.Add(new XElement(
                    "None",
                    new XAttribute(
                        "Update",
                        FixtureRoots[0] + "\\leaked-default-item.cs")));
                break;
            case "replacement-json":
            {
                XElement item = FirstScenarioContent();
                const string replacement = "replacement-not-in-policy.fixture.json";
                item.SetAttributeValue(
                    "Include",
                    project.ScenarioIncludePrefix + replacement);
                item.Element("Link")!.Value = ScenarioLinkRoot + replacement;
                item.Element("TargetPath")!.Value = ScenarioPackageRoot + replacement;
                break;
            }
            case "alternate-slash":
            {
                XElement item = FirstFixtureCompile();
                item.SetAttributeValue("Include", item.Attribute("Include")!.Value.Replace('\\', '/'));
                break;
            }
            case "alternate-case":
            {
                XElement item = FirstFixtureCompile();
                item.SetAttributeValue(
                    "Include",
                    item.Attribute("Include")!.Value.Replace(
                        "DebugFixtures",
                        "debugfixtures",
                        StringComparison.Ordinal));
                break;
            }
            case "property-indirected-condition":
                FirstFixtureCompile().Parent!.SetAttributeValue(
                    "Condition",
                    "'$(ModelInspectionFixtureGallery)' == 'true'");
                break;
            case "property-indirected-item":
            {
                XElement root = document.Root!;
                root.AddFirst(new XElement(
                    "PropertyGroup",
                    new XElement(
                        "FixtureScenarioRoot",
                        project.ScenarioIncludePrefix.TrimEnd('\\'))));
                FirstFixtureCompile().Parent!.Add(new XElement(
                    "Content",
                    new XAttribute(
                        "Include",
                        "$(FixtureScenarioRoot)\\property-leak.fixture.json")));
                break;
            }
            case "metadata-indirected-item":
                FirstFixtureCompile().Parent!.Add(new XElement(
                    "Content",
                    new XAttribute("Include", "Assets\\ordinary.json"),
                    new XElement(
                        "Link",
                        ScenarioLinkRoot + "metadata-leak.fixture.json")));
                break;
            case "extra-fixture-item":
                FirstFixtureCompile().Parent!.Add(new XElement(
                    "None",
                    new XAttribute(
                        "Include",
                        FixtureRoots[0] + "\\unexpected-fixture.txt")));
                break;
            case "long-package-target":
            {
                XElement item = FirstScenarioContent();
                item.Element("TargetPath")!.Value = item.Element("Link")!.Value;
                break;
            }
            default:
                Assert.Fail($"Unknown mutation {mutation} for {project.RelativePath}.");
                break;
        }
    }

    private static void AssertEvaluatedClosure(
        ProjectSpec project,
        JsonObject items,
        bool intended)
    {
        var relatedByType = EvaluatedItemTypes.ToDictionary(
            itemType => itemType,
            itemType => items[itemType]!.AsArray()
                .Where(IsEvaluatedFixtureItem)
                .ToArray(),
            StringComparer.Ordinal);

        if (!intended)
        {
            foreach ((string itemType, JsonNode?[] related) in relatedByType)
            {
                Assert.AreEqual(
                    0,
                    related.Length,
                    $"{project.RelativePath} leaked {itemType}: " +
                    string.Join(", ", related.Select(EvaluatedIdentity)));
            }

            return;
        }

        string projectDirectory = Path.GetDirectoryName(Absolute(project.RelativePath))!;
        AssertEvaluatedPaths(
            EnumerateFixtureFiles(project, ".cs")
                .Select(path => Path.GetFullPath(path, projectDirectory)),
            relatedByType["Compile"],
            project.RelativePath + " Compile");
        AssertEvaluatedPaths(
            EnumerateFixtureFiles(project, ".xaml")
                .Select(path => Path.GetFullPath(path, projectDirectory)),
            relatedByType["Page"],
            project.RelativePath + " Page");
        AssertEvaluatedPaths(
            Directory.EnumerateFiles(
                Absolute("tests/TestFixtures/ModelInspectionScenarios"),
                "*.json",
                SearchOption.TopDirectoryOnly),
            relatedByType["Content"],
            project.RelativePath + " Content");
        AssertEvaluatedPaths(
            new[]
            {
                Absolute("shared/GraniteEdgeAI.ModelInspection.Fixtures/" +
                    FixtureProjectFileName)
            },
            relatedByType["ProjectReference"],
            project.RelativePath + " ProjectReference");

        foreach (string itemType in new[] { "None", "EmbeddedResource", "PRIResource" })
        {
            Assert.AreEqual(
                0,
                relatedByType[itemType].Length,
                $"{project.RelativePath} has unexpected evaluated {itemType} fixture items.");
        }

        foreach (JsonNode? content in relatedByType["Content"])
        {
            string fileName = Path.GetFileName(content!["FullPath"]!.GetValue<string>());
            string expectedLinkPath = ScenarioLinkRoot + fileName;
            string expectedPackagePath = ScenarioPackageRoot + fileName;
            Assert.AreEqual(expectedLinkPath, content["Link"]?.GetValue<string>());
            Assert.AreEqual(expectedPackagePath, content["TargetPath"]?.GetValue<string>());
            Assert.AreEqual("PreserveNewest", content["CopyToOutputDirectory"]?.GetValue<string>());
            Assert.AreEqual("PreserveNewest", content["CopyToPublishDirectory"]?.GetValue<string>());
        }
    }

    private static void AssertEvaluatedGalleryConstant(
        JsonObject evaluation,
        bool intended)
    {
        string constants = evaluation["Properties"]?["DefineConstants"]?
            .GetValue<string>() ?? string.Empty;
        string[] galleryTokens = constants
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Contains(
                "MODEL_INSPECTION_FIXTURE_GALLERY",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.AreEqual(intended ? 1 : 0, galleryTokens.Length, constants);
        if (intended)
        {
            Assert.AreEqual("MODEL_INSPECTION_FIXTURE_GALLERY", galleryTokens[0]);
        }
    }

    private static void AssertEvaluatedPaths(
        IEnumerable<string> expected,
        IEnumerable<JsonNode?> actualItems,
        string label)
    {
        string[] expectedPaths = expected
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] actualPaths = actualItems
            .Select(item => Path.GetFullPath(item!["FullPath"]!.GetValue<string>()))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(expectedPaths, actualPaths, label);
    }

    private static bool IsEvaluatedFixtureItem(JsonNode? item)
    {
        string identity = EvaluatedIdentity(item).Replace('/', '\\');
        string fullPath = item?["FullPath"]?.GetValue<string>()?.Replace('/', '\\') ?? string.Empty;
        return identity.Contains("\\DebugFixtures\\", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains("\\DebugFixtures\\", StringComparison.OrdinalIgnoreCase) ||
            identity.Contains("ModelInspectionScenarios", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains("ModelInspectionScenarios", StringComparison.OrdinalIgnoreCase) ||
            identity.EndsWith(FixtureProjectFileName, StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(FixtureProjectFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string EvaluatedIdentity(JsonNode? item) =>
        item?["Identity"]?.GetValue<string>() ?? "<missing-identity>";

    private static JsonObject Evaluate(
        ProjectSpec project,
        string configuration,
        string platform,
        string runtimeIdentifier)
    {
        string projectPath = Absolute(project.RelativePath);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(projectPath)!
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add($"-p:Configuration={configuration}");
        startInfo.ArgumentList.Add($"-p:Platform={platform}");
        startInfo.ArgumentList.Add($"-p:RuntimeIdentifier={runtimeIdentifier}");
        startInfo.ArgumentList.Add(
            "-getProperty:Configuration,Platform,RuntimeIdentifier,DefineConstants");
        startInfo.ArgumentList.Add(
            "-getItem:" + string.Join(',', EvaluatedItemTypes));

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"MSBuild evaluation timed out for {configuration}|{platform}.");
        }

        Task.WaitAll(outputTask, errorTask);
        string output = outputTask.Result;
        string error = errorTask.Result;
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"MSBuild evaluation failed for {configuration}|{platform}." +
            Environment.NewLine + output + error);
        int jsonStart = output.IndexOf('{');
        int jsonEnd = output.LastIndexOf('}');
        Assert.IsTrue(jsonStart >= 0 && jsonEnd >= jsonStart, output + error);
        return JsonNode.Parse(output[jsonStart..(jsonEnd + 1)])!.AsObject();
    }

    private static XDocument Load(ProjectSpec project) =>
        XDocument.Load(Absolute(project.RelativePath), LoadOptions.PreserveWhitespace);

    private static string[] ExpectedScenarioFileNames()
    {
        string directory = Absolute("tests/TestFixtures/ModelInspectionScenarios");
        const string schemaFileName = "model-inspection-fixture.schema.json";
        const string policyFileName =
            "model-inspection-fixture-coverage-policy.json";
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                schemaFileName,
                File.ReadAllBytes(Path.Combine(directory, schemaFileName))));
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                policyFileName,
                File.ReadAllBytes(Path.Combine(directory, policyFileName))), schema);
        string[] descriptors = policy.Value.Fixtures
            .Select(entry => entry.FileName)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();
        Assert.AreEqual(50, descriptors.Length, "Validated policy descriptor count.");
        Assert.AreEqual(
            descriptors.Length,
            descriptors.Distinct(StringComparer.Ordinal).Count(),
            "Validated policy descriptor filenames must be unique.");

        string[] expected = descriptors
            .Append(policyFileName)
            .Append(schemaFileName)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();
        string[] physical = Directory.EnumerateFiles(
                directory,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray()!;
        CollectionAssert.AreEqual(
            expected,
            physical,
            "Physical scenario JSON closure must be policy 50 plus schema and policy.");
        return expected;
    }

    private static string[] ExpectedDebugIdentityLines()
    {
        var identities = new List<string>
        {
            "ProjectReference|shared/GraniteEdgeAI.ModelInspection.Fixtures/" +
                FixtureProjectFileName
        };
        identities.AddRange(EnumerateFixtureFiles(Projects[0], ".cs")
            .Select(path => "Compile|" + path.Replace('\\', '/')));
        identities.AddRange(EnumerateFixtureFiles(Projects[0], ".xaml")
            .Select(path => "Page|" + path.Replace('\\', '/')));
        identities.AddRange(ExpectedScenarioFileNames()
            .Select(fileName => "Content|" + ScenarioLinkRoot.Replace('\\', '/') + fileName));
        return identities
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
    }

    private static string HashCanonicalLines(IEnumerable<string> lines)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines));
        byte[] digest = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static ManagedAssemblyInspection InspectManagedAssembly(string assemblyPath)
    {
        string canonicalPath = Path.GetFullPath(assemblyPath);
        using var stream = new FileStream(
            canonicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        string sha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(stream))
            .ToLowerInvariant();
        stream.Position = 0;
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata)
        {
            throw new BadImageFormatException("Managed assembly has no metadata.");
        }

        MetadataReader reader = pe.GetMetadataReader();
        var forbiddenHits = new SortedSet<string>(StringComparer.Ordinal);
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            AssemblyReference reference = reader.GetAssemblyReference(handle);
            InspectMetadataIdentity(
                forbiddenHits,
                "AssemblyReference",
                reader.GetString(reference.Name));
        }

        int nestedClassCount = 0;
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);
            string identity = JoinMetadataName(
                reader.GetString(definition.Namespace),
                reader.GetString(definition.Name));
            InspectMetadataIdentity(forbiddenHits, "TypeDefinition", identity);
            if (!definition.GetDeclaringType().IsNil)
            {
                nestedClassCount++;
            }
        }

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            TypeReference reference = reader.GetTypeReference(handle);
            InspectMetadataIdentity(
                forbiddenHits,
                "TypeReference",
                JoinMetadataName(
                    reader.GetString(reference.Namespace),
                    reader.GetString(reference.Name)));
        }

        foreach (ExportedTypeHandle handle in reader.ExportedTypes)
        {
            ExportedType exported = reader.GetExportedType(handle);
            InspectMetadataIdentity(
                forbiddenHits,
                "ExportedType",
                JoinMetadataName(
                    reader.GetString(exported.Namespace),
                    reader.GetString(exported.Name)));
        }

        foreach (ManifestResourceHandle handle in reader.ManifestResources)
        {
            ManifestResource resource = reader.GetManifestResource(handle);
            InspectMetadataIdentity(
                forbiddenHits,
                "ManifestResource",
                reader.GetString(resource.Name));
        }

        bool readyToRun = pe.PEHeaders.CorHeader is not null &&
            pe.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size > 0;
        return new(
            sha256,
            readyToRun,
            new(
                reader.AssemblyReferences.Count,
                reader.TypeDefinitions.Count,
                nestedClassCount,
                reader.TypeReferences.Count,
                reader.ExportedTypes.Count,
                reader.ManifestResources.Count),
            forbiddenHits.ToArray());
    }

    private static void InspectMetadataIdentity(
        ISet<string> hits,
        string table,
        string identity)
    {
        string[] forbiddenTokens =
        [
            "fixture",
            "gallery",
            "ModelInspectionScenarios",
            "DebugFixtures"
        ];
        if (!forbiddenTokens.Any(token => identity.Contains(
                token,
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (hits.Count >= 256)
        {
            hits.Add("<forbidden-hit-limit-exceeded>");
            return;
        }

        string boundedIdentity = new(
            identity
                .Take(256)
                .Select(character => char.IsControl(character) ? '?' : character)
                .ToArray());
        hits.Add(table + ":" + boundedIdentity);
    }

    private static string JoinMetadataName(string namespaceName, string name) =>
        namespaceName.Length == 0 ? name : namespaceName + "." + name;

    private static string RequireCanonicalOwnedRoot(string value)
    {
        Assert.IsTrue(Path.IsPathFullyQualified(value));
        string canonical = Path.GetFullPath(value)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.IsTrue(Directory.Exists(canonical), "Owned root does not exist.");
        Assert.IsFalse(
            (File.GetAttributes(canonical) & FileAttributes.ReparsePoint) != 0,
            "Owned root cannot be a reparse point.");
        string leaf = Path.GetFileName(canonical);
        Assert.AreEqual(32, leaf.Length, "Owned root leaf must be a GUID N token.");
        Assert.IsTrue(leaf.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
        Assert.AreEqual(
            "mi-release-isolation",
            Path.GetFileName(Path.GetDirectoryName(canonical)),
            "Owned root parent is not the isolation root.");
        return canonical;
    }

    private static string RequireCanonicalOwnedChild(
        string value,
        string ownedRoot,
        bool mustExist)
    {
        Assert.IsTrue(Path.IsPathFullyQualified(value));
        string canonical = Path.GetFullPath(value);
        string prefix = ownedRoot + Path.DirectorySeparatorChar;
        Assert.IsTrue(
            canonical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            "Inspector path escaped the owned root.");
        string parent = Path.GetDirectoryName(canonical)!;
        Assert.IsTrue(Directory.Exists(parent), "Inspector path parent does not exist.");
        AssertPathHasNoReparsePoint(parent, ownedRoot);
        if (mustExist)
        {
            Assert.IsTrue(File.Exists(canonical), "Inspector input does not exist.");
            Assert.IsFalse(
                (File.GetAttributes(canonical) & FileAttributes.ReparsePoint) != 0,
                "Inspector input cannot be a reparse point.");
        }

        return canonical;
    }

    private static void AssertPathHasNoReparsePoint(string path, string ownedRoot)
    {
        string current = Path.GetFullPath(path);
        while (current.Length >= ownedRoot.Length)
        {
            Assert.IsFalse(
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0,
                "Owned inspector path cannot traverse a reparse point.");
            if (string.Equals(current, ownedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string? parent = Path.GetDirectoryName(current);
            Assert.IsNotNull(parent);
            current = parent;
        }

        Assert.Fail("Owned inspector path did not terminate at the owned root.");
    }

    private static void WriteInspectionResult(
        string resultPath,
        ManagedAssemblyInspection inspection)
    {
        JsonArray hits = new();
        foreach (string hit in inspection.ForbiddenMetadataHits)
        {
            hits.Add(hit);
        }

        var result = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["status"] = "passed",
            ["mainDllSha256"] = inspection.Sha256,
            ["readyToRun"] = inspection.ReadyToRun,
            ["metadataTableCounts"] = new JsonObject
            {
                ["assemblyReference"] = inspection.MetadataTableCounts.AssemblyReference,
                ["typeDefinition"] = inspection.MetadataTableCounts.TypeDefinition,
                ["nestedClass"] = inspection.MetadataTableCounts.NestedClass,
                ["typeReference"] = inspection.MetadataTableCounts.TypeReference,
                ["exportedType"] = inspection.MetadataTableCounts.ExportedType,
                ["manifestResource"] = inspection.MetadataTableCounts.ManifestResource
            },
            ["forbiddenMetadataHits"] = hits
        };
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result.ToJsonString() + "\n");
        using var stream = new FileStream(
            resultPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void ValidateReleaseEvidenceInvocation()
    {
        string? evidencePath = Environment.GetEnvironmentVariable(
            ReleaseEvidenceEnvironment);
        string? ownedRoot = Environment.GetEnvironmentVariable(
            IsolationOwnedRootEnvironment);
        string? receiptPath = Environment.GetEnvironmentVariable(
            ReleaseEvidenceReceiptEnvironment);
        bool noInvocation = evidencePath is null && ownedRoot is null && receiptPath is null;
        if (noInvocation)
        {
            DateTimeOffset generatedUtc = DateTimeOffset.UtcNow;
            Assert.AreEqual(
                0,
                ValidateIsolationEvidence(
                    CreateCanonicalEvidence(generatedUtc),
                    generatedUtc.AddMinutes(1)).Length,
                "The committed evidence validator rejected its canonical control.");
            return;
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(evidencePath));
        Assert.IsFalse(string.IsNullOrWhiteSpace(ownedRoot));
        Assert.IsFalse(string.IsNullOrWhiteSpace(receiptPath));
        string canonicalRoot = RequireCanonicalOwnedRoot(ownedRoot!);
        string canonicalEvidence = RequireCanonicalOwnedChild(
            evidencePath!,
            canonicalRoot,
            mustExist: true);
        Assert.AreEqual(
            canonicalRoot,
            Path.GetDirectoryName(canonicalEvidence),
            "Release evidence must be a direct owned-root child.");
        Assert.AreEqual(
            "release-isolation-evidence.json",
            Path.GetFileName(canonicalEvidence),
            "Release evidence has an unexpected transient filename.");
        string canonicalReceipt = RequireCanonicalOwnedChild(
            receiptPath!,
            canonicalRoot,
            mustExist: false);
        Assert.AreEqual(
            canonicalRoot,
            Path.GetDirectoryName(canonicalReceipt),
            "Release evidence receipt must be a direct owned-root child.");
        Assert.AreEqual(
            "release-isolation-evidence-validation.json",
            Path.GetFileName(canonicalReceipt),
            "Release evidence receipt has an unexpected transient filename.");
        Assert.IsFalse(File.Exists(canonicalReceipt), "Validation receipt must not pre-exist.");

        byte[] bytes = File.ReadAllBytes(canonicalEvidence);
        Assert.IsGreaterThan(0, bytes.Length);
        Assert.IsTrue(bytes.Length <= 1024 * 1024, "Release evidence is unbounded.");
        Assert.IsFalse(
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF,
            "Release evidence cannot contain a UTF-8 BOM.");
        Assert.AreEqual(-1, Array.IndexOf(bytes, (byte)'\r'));
        Assert.AreEqual((byte)'\n', bytes[^1], "Release evidence needs one final LF.");
        Assert.IsTrue(
            bytes.Length == 1 || bytes[^2] != (byte)'\n',
            "Release evidence must have exactly one final LF.");

        string json = new System.Text.UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(bytes);
        JsonObject evidence = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("Release evidence root is absent.");
        string[] errors = ValidateIsolationEvidence(evidence, DateTimeOffset.UtcNow);
        Assert.AreEqual(
            0,
            errors.Length,
            "Release evidence differs from its committed contract:" +
            Environment.NewLine + string.Join(Environment.NewLine, errors));
        AssertEvidenceMatchesInspectedMainAssembly(evidence, canonicalRoot);

        string evidenceSha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes))
            .ToLowerInvariant();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["status"] = "passed",
            ["evidenceSha256"] = evidenceSha256
        };
        byte[] receiptBytes = System.Text.Encoding.UTF8.GetBytes(
            receipt.ToJsonString() + "\n");
        using var receiptStream = new FileStream(
            canonicalReceipt,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        receiptStream.Write(receiptBytes);
        receiptStream.Flush(flushToDisk: true);
    }

    private static void AssertEvidenceMatchesInspectedMainAssembly(
        JsonObject evidence,
        string canonicalRoot)
    {
        string canonicalMainDll = RequireCanonicalOwnedChild(
            Path.Combine(
                canonicalRoot,
                "release",
                "layout",
                "IBM Granite with TurboQuant (Intel).dll"),
            canonicalRoot,
            mustExist: true);
        string canonicalInspectionResult = RequireCanonicalOwnedChild(
            Path.Combine(
                canonicalRoot,
                "release",
                "inspection",
                "metadata-result.json"),
            canonicalRoot,
            mustExist: true);

        byte[] resultBytes = File.ReadAllBytes(canonicalInspectionResult);
        Assert.IsGreaterThan(0, resultBytes.Length);
        Assert.IsTrue(resultBytes.Length <= 1024 * 1024, "Inspection result is unbounded.");
        Assert.IsFalse(
            resultBytes.Length >= 3 &&
            resultBytes[0] == 0xEF &&
            resultBytes[1] == 0xBB &&
            resultBytes[2] == 0xBF,
            "Inspection result cannot contain a UTF-8 BOM.");
        Assert.AreEqual(-1, Array.IndexOf(resultBytes, (byte)'\r'));
        Assert.AreEqual((byte)'\n', resultBytes[^1]);
        Assert.IsTrue(resultBytes.Length == 1 || resultBytes[^2] != (byte)'\n');

        string resultJson = new System.Text.UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(resultBytes);
        JsonObject result = JsonNode.Parse(resultJson)?.AsObject()
            ?? throw new InvalidDataException("Inspection result root is absent.");
        string[] expectedRootProperties =
        [
            "schemaVersion",
            "status",
            "mainDllSha256",
            "readyToRun",
            "metadataTableCounts",
            "forbiddenMetadataHits"
        ];
        CollectionAssert.AreEqual(
            expectedRootProperties.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            result.Select(property => property.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        Assert.AreEqual(1, result["schemaVersion"]!.GetValue<int>());
        Assert.AreEqual("passed", result["status"]!.GetValue<string>());
        Assert.IsTrue(result["readyToRun"]!.GetValue<bool>());
        Assert.AreEqual(0, result["forbiddenMetadataHits"]!.AsArray().Count);

        string expectedMainDllSha256;
        using (var stream = new FileStream(
            canonicalMainDll,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan))
        {
            expectedMainDllSha256 = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(stream))
                .ToLowerInvariant();
        }

        Assert.AreEqual(
            expectedMainDllSha256,
            result["mainDllSha256"]!.GetValue<string>(),
            "Inspection result does not identify the exact packaged main DLL bytes.");
        JsonObject evidenceMainDll = evidence["mainDll"]!.AsObject();
        Assert.AreEqual(
            expectedMainDllSha256,
            evidenceMainDll["sha256"]!.GetValue<string>(),
            "Evidence main-DLL hash differs from the inspected package bytes.");
        Assert.AreEqual(
            result["readyToRun"]!.GetValue<bool>(),
            evidenceMainDll["readyToRun"]!.GetValue<bool>());

        JsonObject resultCounts = result["metadataTableCounts"]!.AsObject();
        JsonObject evidenceCounts = evidenceMainDll["metadataTableCounts"]!.AsObject();
        string[] expectedCountProperties =
        [
            "assemblyReference",
            "typeDefinition",
            "nestedClass",
            "typeReference",
            "exportedType",
            "manifestResource"
        ];
        CollectionAssert.AreEqual(
            expectedCountProperties.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            resultCounts.Select(property => property.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        foreach (string property in expectedCountProperties)
        {
            Assert.AreEqual(
                resultCounts[property]!.GetValue<int>(),
                evidenceCounts[property]!.GetValue<int>(),
                $"Evidence metadata count differs for {property}.");
        }

        Assert.AreEqual(0, evidence["releaseForbiddenMetadataHits"]!.AsArray().Count);
    }

    private static string Absolute(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record ManagedAssemblyInspection(
        string Sha256,
        bool ReadyToRun,
        MetadataCounts MetadataTableCounts,
        IReadOnlyList<string> ForbiddenMetadataHits);

    private sealed record MetadataCounts(
        int AssemblyReference,
        int TypeDefinition,
        int NestedClass,
        int TypeReference,
        int ExportedType,
        int ManifestResource);

    private sealed record ProjectSpec(
        string RelativePath,
        string ScenarioIncludePrefix,
        string FixtureProjectReference);
}
