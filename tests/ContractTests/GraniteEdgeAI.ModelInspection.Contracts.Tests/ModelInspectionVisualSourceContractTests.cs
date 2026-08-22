using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ModelInspectionVisualSourceContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    private const string WorkerPublishedFilesItemName =
        "_ModelInspectionWorkerPublishedFiles";

    private const string WorkerPublishedFilesRootExpression =
        "$(_ModelInspectionWorkerPublishRoot)\\**\\*";

    private const string WorkerPublishRootPropertyName =
        "_ModelInspectionWorkerPublishRoot";

    private const string WorkerPublishRootPropertyValue =
        "$([System.IO.Path]::GetFullPath('$(MSBuildProjectDirectory)\\$(BaseIntermediateOutputPath)model-inspection-worker\\$(Configuration)\\win-x64'))";

    private const string WorkerManifestPathPropertyName =
        "_ModelInspectionWorkerManifestPath";

    private const string WorkerManifestPathPropertyValue =
        "$([System.IO.Path]::GetFullPath('$(MSBuildProjectDirectory)\\$(BaseIntermediateOutputPath)model-inspection-worker\\$(Configuration)\\worker-manifest.json'))";

    private static readonly HashSet<string> WorkerPackagePathPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            WorkerPublishRootPropertyName,
            WorkerManifestPathPropertyName
        };

    private static readonly HashSet<string> PackageRelevantItemNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ApplicationDefinition",
            "Content",
            "EmbeddedResource",
            "None",
            "Page",
            "PRIResource",
            "Resource"
        };

    private static readonly IReadOnlyDictionary<string, string> LightPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InspectionTextPrimaryBrush"] = "#101828",
            ["InspectionTextSecondaryStrongBrush"] = "#344054",
            ["InspectionTextSecondaryBrush"] = "#475467",
            ["InspectionTextSecondaryMutedBrush"] = "#667085",
            ["InspectionTextMutedBrush"] = "#7A8797",
            ["InspectionTextSubtleBrush"] = "#98A2B3",
            ["InspectionPrimaryBlueBrush"] = "#0F62FE",
            ["InspectionBlueSurfaceBrush"] = "#EEF5FF",
            ["InspectionBlueBorderStrongBrush"] = "#BDD3FF",
            ["InspectionBlueBorderBrush"] = "#C9D9F2",
            ["InspectionSuccessSurfaceBrush"] = "#E9F7F1",
            ["InspectionSuccessTextBrush"] = "#067A57",
            ["InspectionSuccessTextStrongBrush"] = "#0B3B2F",
            ["InspectionSuccessBorderBrush"] = "#A5D8C4",
            ["InspectionWarningSurfaceBrush"] = "#FFF6E0",
            ["InspectionWarningTextBrush"] = "#B7791F",
            ["InspectionWarningAccentBrush"] = "#9A6700",
            ["InspectionWarningTextStrongBrush"] = "#604200",
            ["InspectionWarningBorderBrush"] = "#EEC86F",
            ["InspectionErrorSurfaceBrush"] = "#FFF0EF",
            ["InspectionErrorTextBrush"] = "#B42318",
            ["InspectionErrorTextStrongBrush"] = "#7A271A",
            ["InspectionErrorBorderBrush"] = "#EDB3AD",
            ["InspectionCanvasBrush"] = "#F6F8FB",
            ["InspectionSurfaceBrush"] = "#FFFFFF",
            ["InspectionSurfaceSubtleBrush"] = "#F7F9FC",
            ["InspectionSurfaceMutedBrush"] = "#F8FAFC",
            ["InspectionBorderLightBrush"] = "#E8EDF3",
            ["InspectionBorderStrongBrush"] = "#C9D5E3",
            ["InspectionBorderMutedBrush"] = "#D7E0EA",
            ["InspectionBorderControlBrush"] = "#BFC9D6"
        };

    private static readonly IReadOnlyDictionary<string, string> DarkPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InspectionTextPrimaryBrush"] = "#F9FAFB",
            ["InspectionTextSecondaryStrongBrush"] = "#EAECF0",
            ["InspectionTextSecondaryBrush"] = "#D0D5DD",
            ["InspectionTextSecondaryMutedBrush"] = "#98A2B3",
            ["InspectionTextMutedBrush"] = "#98A2B3",
            ["InspectionTextSubtleBrush"] = "#667085",
            ["InspectionPrimaryBlueBrush"] = "#78A9FF",
            ["InspectionBlueSurfaceBrush"] = "#102A56",
            ["InspectionBlueBorderStrongBrush"] = "#4589FF",
            ["InspectionBlueBorderBrush"] = "#2D5F9A",
            ["InspectionSuccessSurfaceBrush"] = "#12372E",
            ["InspectionSuccessTextBrush"] = "#6FD2AE",
            ["InspectionSuccessTextStrongBrush"] = "#A6E5CF",
            ["InspectionSuccessBorderBrush"] = "#368F70",
            ["InspectionWarningSurfaceBrush"] = "#3D2F12",
            ["InspectionWarningTextBrush"] = "#F4CE80",
            ["InspectionWarningAccentBrush"] = "#E6B84A",
            ["InspectionWarningTextStrongBrush"] = "#FFE3A3",
            ["InspectionWarningBorderBrush"] = "#9F762A",
            ["InspectionErrorSurfaceBrush"] = "#4A1D1A",
            ["InspectionErrorTextBrush"] = "#FDA29B",
            ["InspectionErrorTextStrongBrush"] = "#FECDCA",
            ["InspectionErrorBorderBrush"] = "#D92D20",
            ["InspectionCanvasBrush"] = "#0C111D",
            ["InspectionSurfaceBrush"] = "#101828",
            ["InspectionSurfaceSubtleBrush"] = "#1D2939",
            ["InspectionSurfaceMutedBrush"] = "#182230",
            ["InspectionBorderLightBrush"] = "#344054",
            ["InspectionBorderStrongBrush"] = "#475467",
            ["InspectionBorderMutedBrush"] = "#344054",
            ["InspectionBorderControlBrush"] = "#667085"
        };

    private static readonly IReadOnlyDictionary<string, string> HighContrastPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InspectionTextPrimaryBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionTextSecondaryStrongBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionTextSecondaryBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionTextSecondaryMutedBrush"] = "{ThemeResource SystemColorGrayTextColor}",
            ["InspectionTextMutedBrush"] = "{ThemeResource SystemColorGrayTextColor}",
            ["InspectionTextSubtleBrush"] = "{ThemeResource SystemColorGrayTextColor}",
            ["InspectionPrimaryBlueBrush"] = "{ThemeResource SystemColorHotlightColor}",
            ["InspectionBlueSurfaceBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionBlueBorderStrongBrush"] = "{ThemeResource SystemColorHotlightColor}",
            ["InspectionBlueBorderBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionSuccessSurfaceBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionSuccessTextBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionSuccessTextStrongBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionSuccessBorderBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionWarningSurfaceBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionWarningTextBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionWarningAccentBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionWarningTextStrongBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionWarningBorderBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionErrorSurfaceBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionErrorTextBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionErrorTextStrongBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionErrorBorderBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionCanvasBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionSurfaceBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionSurfaceSubtleBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionSurfaceMutedBrush"] = "{ThemeResource SystemColorWindowColor}",
            ["InspectionBorderLightBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionBorderStrongBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionBorderMutedBrush"] = "{ThemeResource SystemColorWindowTextColor}",
            ["InspectionBorderControlBrush"] = "{ThemeResource SystemColorWindowTextColor}"
        };

    [TestMethod]
    public void SuppliedBoard_MatchesApprovedIdentity()
    {
        string referenceRoot = Path.Combine(
            Root,
            "docs",
            "ux",
            "screenshots",
            "model-inspection",
            "reference");
        string boardPath = Path.Combine(
            referenceRoot,
            "model-inspection-complete-ordered-board-v2.svg");
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(boardPath)));

        Assert.AreEqual(8_190_259L, new FileInfo(boardPath).Length);
        Assert.AreEqual(
            "8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB",
            hash);

        XElement svg = XDocument.Load(boardPath).Root
            ?? throw new InvalidDataException("The approved board SVG has no root element.");
        Assert.AreEqual("svg", svg.Name.LocalName);
        Assert.AreEqual("4624", svg.Attribute("width")?.Value);
        Assert.AreEqual("5836", svg.Attribute("height")?.Value);
        Assert.AreEqual("0 0 4624 5836", svg.Attribute("viewBox")?.Value);

        string readme = File.ReadAllText(Path.Combine(referenceRoot, "README.md"));
        StringAssert.Contains(readme, "gAmBX1DYh71hqxHVqiivus");
        StringAssert.Contains(readme, "142:2148");
        StringAssert.Contains(readme, "4624 x 5836");
        StringAssert.Contains(readme, "0 0 4624 5836");
        (string State, string Node, int X, int Y)[] frames =
        [
            ("01", "142:2151", 60, 82),
            ("02", "142:2213", 1592, 82),
            ("03", "142:2280", 3124, 82),
            ("04", "142:2403", 60, 1244),
            ("05", "142:2476", 1592, 1244),
            ("06", "142:2599", 3124, 1244),
            ("07", "142:2664", 60, 2406),
            ("08", "142:2787", 1592, 2406),
            ("09", "142:2851", 3124, 2406),
            ("10", "142:2910", 60, 3568),
            ("11", "142:2973", 1592, 3568),
            ("12", "142:3096", 3124, 3568),
            ("13", "142:3154", 60, 4730)
        ];
        foreach ((string state, string node, int x, int y) in frames)
        {
            StringAssert.Contains(
                readme,
                $"| {state} | `{node}` | `{x}` | `{y}` | `1440 x 1024` |");
        }
        foreach (string nodeId in new[]
                 {
                     "142:2151", "142:2213", "142:2280", "142:2403", "142:2476",
                     "142:2599", "142:2664", "142:2787", "142:2851", "142:2910",
                     "142:2973", "142:3096", "142:3154"
                 })
        {
            StringAssert.Contains(readme, nodeId);
        }
        StringAssert.Contains(readme, "13 state nodes");
        StringAssert.Contains(readme, "1440 x 1024");
        StringAssert.Contains(readme, "x = 300");
        StringAssert.Contains(readme, "840 px");
    }

    [TestMethod]
    public void StrictNodeExports_AreExplicitlyBlockedAndNotFabricated()
    {
        string readme = Read(
            "docs/ux/screenshots/model-inspection/reference/README.md");
        string prose = Regex.Replace(readme, "\\s+", " ");
        StringAssert.Contains(prose, "STRICT PIXEL DOD: BLOCKED");
        StringAssert.Contains(prose, "exact Figma node exports");
        StringAssert.Contains(prose, "must not be cropped or rerasterized");
        StringAssert.Contains(prose, "DoD 2, 8, 11, 12, and 13 remain open");

        string[] deferredBundleFiles =
        [
            "visual-reference-manifest.json",
            "01-inspection-progress.png",
            "02-ready.png",
            "03-ready-expanded.png",
            "04-ready-with-warnings.png",
            "05-ready-with-warnings-expanded.png",
            "06-conversion-required.png",
            "07-conversion-required-expanded.png",
            "08-incomplete-package.png",
            "09-unsupported.png",
            "10-invalid.png",
            "11-invalid-expanded.png",
            "12-cancelled.png",
            "13-operational-failure.png"
        ];
        foreach (string deferred in deferredBundleFiles)
        {
            StringAssert.Contains(readme, deferred);
        }

        string fixtureRoot = Path.Combine(
            Root,
            "tests",
            "TestFixtures",
            "ModelInspectionVisual");
        Assert.IsFalse(
            Directory.Exists(fixtureRoot),
            "No visual fixture directory may exist before exact node exports are available.");

        foreach (string deferredTest in new[]
                 {
                     "ModelInspectionVisualReferenceIntegrityTests.cs",
                     "ModelInspectionVisualRegressionTests.cs",
                     "ModelInspectionControlledAccessibilityTests.cs"
                 })
        {
            string path = Path.Combine(
                Root,
                "tests",
                "UnitTests",
                "GraniteEdgeAI.UnitTests",
                "Features",
                "ModelInspection",
                "Visual",
                deferredTest);
            Assert.IsFalse(
                File.Exists(path),
                $"{deferredTest} must remain absent instead of reporting a false GREEN.");
            StringAssert.Contains(readme, deferredTest);
        }
    }

    [TestMethod]
    public void DurableBoardAndBlockedReferences_AreExcludedFromAppAndTestPackages()
    {
        string appProjectPath = Path.Combine(
            Root,
            "IBM Granite with TurboQuant (Intel)/" +
            "IBM Granite with TurboQuant (Intel).csproj");
        string testProjectPath = Path.Combine(
            Root,
            "tests/UnitTests/GraniteEdgeAI.UnitTests/" +
            "GraniteEdgeAI.UnitTests.csproj");
        string referenceRoot = Path.Combine(
            Root,
            "docs",
            "ux",
            "screenshots",
            "model-inspection",
            "reference");
        string[] prohibitedSources =
        [
            Path.Combine(
                referenceRoot,
                "model-inspection-complete-ordered-board-v2.svg"),
            Path.Combine(referenceRoot, "README.md")
        ];

        foreach (string projectPath in new[] { appProjectPath, testProjectPath })
        {
            ProjectPackageItemSpec[] itemSpecs =
                ProjectItemSpecs(projectPath).ToArray();
            int expectedControlledImportItems = string.Equals(
                projectPath,
                appProjectPath,
                StringComparison.OrdinalIgnoreCase)
                    ? 4
                    : 0;
            Assert.AreEqual(
                expectedControlledImportItems,
                itemSpecs.Count(itemSpec =>
                    itemSpec.IsControlledWorkerPackageExpression),
                $"{projectPath} must account for its exact controlled imported package items.");
            int expectedControlledBackingItems = string.Equals(
                projectPath,
                appProjectPath,
                StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0;
            Assert.AreEqual(
                expectedControlledBackingItems,
                itemSpecs.Count(itemSpec =>
                    itemSpec.IsControlledWorkerPublishedFilesExpression),
                $"{projectPath} must account for the exact worker-package backing item.");
            int expectedControlledPathProperties = string.Equals(
                projectPath,
                appProjectPath,
                StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 0;
            Assert.AreEqual(
                expectedControlledPathProperties,
                itemSpecs.Count(itemSpec =>
                    itemSpec.IsWorkerPackagePathPropertyDefinition),
                $"{projectPath} must not define or override worker-package path properties outside their exact trusted definitions.");
            Assert.AreEqual(
                expectedControlledPathProperties,
                itemSpecs.Count(itemSpec =>
                    itemSpec.IsControlledWorkerPackagePathPropertyDefinition),
                $"{projectPath} must retain the exact trusted worker-package path property definitions.");
            foreach (string prohibitedSource in prohibitedSources)
            {
                Assert.IsFalse(
                    itemSpecs.Any(itemSpec => ProjectItemSpecMatchesPath(
                        itemSpec,
                        prohibitedSource)),
                    $"{projectPath} must not package {prohibitedSource}.");
            }

            Assert.IsFalse(
                itemSpecs
                    .Select(itemSpec => NormalizeProjectPath(itemSpec.Value))
                    .Any(itemSpec => itemSpec?.Contains(
                        "TestFixtures/ModelInspectionVisual",
                        StringComparison.OrdinalIgnoreCase) is true),
                $"{projectPath} must not package blocked visual fixtures.");
        }
    }

    [TestMethod]
    public void VisualArtifactPrivacyScanner_AcceptsOnlySafeManifestAndApprovedPngMetadata()
    {
        string temporaryRoot = Directory.CreateTempSubdirectory(
            "model-inspection-visual-privacy-safe-").FullName;
        try
        {
            File.WriteAllText(
                Path.Combine(temporaryRoot, "run-manifest.json"),
                """
                {
                  "candidateCommit": "0123456789abcdef0123456789abcdef01234567",
                  "osBuild": "10.0.26100",
                  "rasterizer": "WinUI RenderTargetBitmap",
                  "resolution": "1440x1024",
                  "dpi": 96,
                  "textScale": 100,
                  "theme": "Light",
                  "animationsEnabled": false,
                  "states": [
                    "01", "02", "03", "04", "05", "06", "07",
                    "08", "09", "10", "11", "12", "13"
                  ]
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllBytes(
                Path.Combine(temporaryRoot, "01-actual.png"),
                MinimalPng());
            File.WriteAllBytes(
                Path.Combine(temporaryRoot, "02-actual.png"),
                PngWithEncoderMetadata(
                    gammaPayload: [0, 0, 177, 143],
                    repeatPhysicalDimensions: false));

            PrivacyScanResult result = RunPrivacyScanner(temporaryRoot);

            Assert.AreEqual(0, result.ExitCode, result.CombinedOutput);
            StringAssert.Contains(
                result.CombinedOutput,
                "Privacy scan passed for 1 JSON and 2 PNG artifact(s).");
            StringAssert.Contains(
                result.CombinedOutput,
                "Raw TRX is not approved by this scanner");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [TestMethod]
    public void VisualArtifactPrivacyScanner_RejectsPathsIdentityModelMetadataPngTextAndTrx()
    {
        string temporaryRoot = Directory.CreateTempSubdirectory(
            "model-inspection-visual-privacy-reject-").FullName;
        try
        {
            string currentIdentity = Environment.UserName.Length >= 3
                ? Environment.UserName
                : Environment.MachineName;
            var cases = new Dictionary<string, (string FileName, byte[] Bytes, string Error)>
            {
                ["absolute-path"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"output\":\"C:\\\\Private\\\\actual.png\"}"),
                    "absolute path"),
                ["posix-absolute-path"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"output\":\"/opt/private/actual.png\"}"),
                    "absolute path"),
                ["escaped-posix-absolute-path"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"output\":\"\\/opt\\/private\\/actual.png\"}"),
                    "absolute path"),
                ["escaped-current-drive-rooted-path"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"output\":\"\\u005cPrivate\\u005cactual.png\"}"),
                    "absolute path"),
                ["forward-slash-unc-path"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"output\":\"//private-server/share/actual.png\"}"),
                    "absolute path"),
                ["identity"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"userName\":\"private-user\",\"computerName\":\"private-host\"}"),
                    "identity metadata"),
                ["escaped-identity-key"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"user\\u004eame\":\"private-user\"}"),
                    "identity metadata"),
                ["identity-value-under-approved-key"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes(
                        $"{{\"rasterizer\":{JsonSerializer.Serialize(currentIdentity)}}}"),
                    "identity metadata"),
                ["model-metadata"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"modelName\":\"Private model\",\"file\":\"private.gguf\"}"),
                    "real-model metadata"),
                ["escaped-model-key"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"model\\u004eame\":\"Private model\"}"),
                    "real-model metadata"),
                ["gguf-value-under-approved-key"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"rasterizer\":\"private.gguf\"}"),
                    "real-model metadata"),
                ["duplicate-approved-property"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes(
                        """
                        {
                          "candidateCommit": "0123456789abcdef0123456789abcdef01234567",
                          "osBuild": "10.0.26100",
                          "ras\u0074erizer": "\u0043\u003a\u005cPrivate\u005cactual.png",
                          "rasterizer": "WinUI RenderTargetBitmap",
                          "resolution": "1440x1024",
                          "dpi": 96,
                          "textScale": 100,
                          "theme": "Light",
                          "animationsEnabled": false,
                          "states": [
                            "01", "02", "03", "04", "05", "06", "07",
                            "08", "09", "10", "11", "12", "13"
                          ]
                        }
                        """),
                    "duplicate JSON property"),
                ["unknown-owner-key"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"owner\":\"external-operator\"}"),
                    "unapproved JSON property"),
                ["unknown-model-key"] = (
                    "manifest.json",
                    Encoding.UTF8.GetBytes("{\"model\":\"Granite 4.1 3B\"}"),
                    "unapproved JSON property"),
                ["png-text"] = (
                    "actual.png",
                    PngWithTextChunk("Comment", "private metadata"),
                    "textual metadata"),
                ["png-profile"] = (
                    "actual.png",
                    PngWithChunk("iCCP", Encoding.Latin1.GetBytes("private-profile")),
                    "unapproved PNG chunk"),
                ["png-corrupt-crc"] = (
                    "actual.png",
                    PngWithCorruptCrc(),
                    "invalid PNG chunk CRC"),
                ["png-approved-type-bad-payload"] = (
                    "actual.png",
                    PngWithEncoderMetadata(
                        gammaPayload: [0, 0, 0, 1],
                        repeatPhysicalDimensions: false),
                    "unapproved PNG metadata payload"),
                ["png-repeated-approved-metadata"] = (
                    "actual.png",
                    PngWithEncoderMetadata(
                        gammaPayload: [0, 0, 177, 143],
                        repeatPhysicalDimensions: true),
                    "approved PNG metadata sequence"),
                ["png-approved-metadata-after-image-data"] = (
                    "actual.png",
                    PngWithChunk(
                        "pHYs",
                        [0, 0, 14, 195, 0, 0, 14, 195, 1]),
                    "invalid PNG chunk order"),
                ["raw-trx"] = (
                    "raw-results.trx",
                    Encoding.UTF8.GetBytes("<TestRun computerName=\"private-host\" />"),
                    "Raw TRX"),
                ["nested-raw-trx"] = (
                    Path.Combine("nested", "raw-results.trx"),
                    Encoding.UTF8.GetBytes("<TestRun computerName=\"private-host\" />"),
                    "Raw TRX"),
                ["unsupported-extension"] = (
                    "notes.txt",
                    Encoding.UTF8.GetBytes("otherwise harmless"),
                    "unsupported artifact extension")
            };

            foreach ((string caseName, var input) in cases)
            {
                string caseRoot = Path.Combine(temporaryRoot, caseName);
                Directory.CreateDirectory(caseRoot);
                string caseFile = Path.Combine(caseRoot, input.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(caseFile)!);
                File.WriteAllBytes(caseFile, input.Bytes);

                PrivacyScanResult result = RunPrivacyScanner(caseRoot);

                Assert.AreNotEqual(0, result.ExitCode, caseName);
                StringAssert.Contains(result.CombinedOutput, input.Error);
            }

            string hiddenRoot = Path.Combine(temporaryRoot, "hidden-raw-trx");
            Directory.CreateDirectory(hiddenRoot);
            string hiddenTrx = Path.Combine(hiddenRoot, "raw-results.trx");
            File.WriteAllText(
                hiddenTrx,
                "<TestRun computerName=\"private-host\" />",
                Encoding.UTF8);
            File.SetAttributes(hiddenTrx, FileAttributes.Hidden);
            try
            {
                PrivacyScanResult hiddenResult = RunPrivacyScanner(hiddenRoot);
                Assert.AreNotEqual(0, hiddenResult.ExitCode, "hidden-raw-trx");
                StringAssert.Contains(hiddenResult.CombinedOutput, "Raw TRX");
            }
            finally
            {
                File.SetAttributes(hiddenTrx, FileAttributes.Normal);
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ThemeSource_DefinesExactSemanticMappings()
    {
        XDocument theme = XDocument.Load(Path.Combine(
            Root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "Presentation",
            "ModelInspectionTheme.xaml"));

        AssertThemeMap(theme, "Light", LightPalette);
        AssertThemeMap(theme, "Dark", DarkPalette);
        AssertThemeMap(theme, "HighContrast", HighContrastPalette);
    }

    [TestMethod]
    public void ApplicationDefinitions_MergeAndPackageTheSameThemeAssets()
    {
        const string dictionary =
            "Features/ModelInspection/Presentation/ModelInspectionTheme.xaml";
        string appXaml = Read("IBM Granite with TurboQuant (Intel)/App.xaml");
        string testAppXaml = Read("tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml");
        Assert.AreEqual(1, Count(appXaml.Replace('\\', '/'), dictionary));
        Assert.AreEqual(1, Count(testAppXaml.Replace('\\', '/'), dictionary));

        XDocument appProject = ReadXml(
            "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj");
        XDocument testProject = ReadXml(
            "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj");
        foreach (string asset in new[]
                 {
                     "Inter-Regular.ttf", "Inter-Bold.ttf", "OFL.txt", "inter-manifest.json"
                 })
        {
            string appPath = $"Assets/Fonts/{asset}";
            Assert.AreEqual(
                0,
                ProjectItems(appProject, "Content", "Include", appPath).Count(),
                $"The app's implicit Assets glob must not be duplicated by Content Include for {asset}.");
            Assert.AreEqual(
                1,
                ProjectItems(appProject, "Content", "Update", appPath).Count(),
                $"The app must explicitly update the one implicit Content item for {asset}.");

            string testSource =
                $"../../../IBM Granite with TurboQuant (Intel)/Assets/Fonts/{asset}";
            XElement[] linkedTestItems = ProjectItems(
                    testProject,
                    "Content",
                    "Include",
                    testSource)
                .Where(item => string.Equals(
                    NormalizeProjectPath(item.Attribute("Link")?.Value),
                    appPath,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(
                1,
                linkedTestItems.Length,
                $"The test package must contain exactly one linked Include for {asset}.");
        }
    }

    [TestMethod]
    public void ModelInspectionControls_UseSharedSemanticColorsOnly()
    {
        string[] controls =
        [
            "InspectionOutcomeCard.xaml",
            "InspectionModelCard.xaml",
            "InspectionContentCard.xaml",
            "InspectionActionCard.xaml",
            "InspectionDisclosure.xaml"
        ];

        foreach (string control in controls)
        {
            string source = Read(
                $"IBM Granite with TurboQuant (Intel)/Features/" +
                $"ModelInspection/Controls/{control}");
            MatchCollection literals = Regex.Matches(
                source,
                "#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?",
                RegexOptions.CultureInvariant);

            Assert.AreEqual(
                0,
                literals.Count,
                $"{control} must consume shared semantic theme resources; " +
                $"found: {string.Join(", ", literals.Select(match => match.Value).Distinct())}");
        }

        foreach (string control in controls)
        {
            string source = Read(
                $"IBM Granite with TurboQuant (Intel)/Features/" +
                $"ModelInspection/Controls/{control}.cs");
            Assert.IsFalse(
                Regex.IsMatch(
                    source,
                    "(?:Color\\.FromArgb|new\\s+SolidColorBrush|CreateBrush\\s*\\()",
                    RegexOptions.CultureInvariant),
                $"{control}.cs must not cache a theme-specific color palette.");
        }

        Dictionary<string, string> progressPolishSources =
            ReadProgressPolishSources();
        Assert.AreEqual(
            0,
            ValidateProgressPolishSources(progressPolishSources).Length,
            string.Join(
                Environment.NewLine,
                ValidateProgressPolishSources(progressPolishSources)));

        (string Name, string Path, Func<string, string> Mutate)[] mutations =
        [
            ("runtime Task.Delay",
                "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyModelProbe.cs",
                source => source + "\ninternal static class RogueDelay { internal static async Task Wait() => await Task.Delay(1); }\n"),
            ("worker Thread.Sleep",
                "workers/GraniteEdgeAI.ModelInspection.Worker/LlamaSharpInspectionEngine.cs",
                source => source + "\ninternal static class RogueSleep { internal static void Wait() => Thread.Sleep(1); }\n"),
            ("service System.Threading.Timer",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionService.cs",
                source => source + "\ninternal sealed class RogueTimer { private System.Threading.Timer? timer; }\n"),
            ("DispatcherQueueTimer outside its adapter",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionService.cs",
                source => source + "\ninternal sealed class RogueUiTimer { private DispatcherQueueTimer? timer; }\n"),
            ("non-550 milestone dwell",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs",
                source => source.Replace(
                    "TimeSpan.FromMilliseconds(550)",
                    "TimeSpan.FromMilliseconds(551)",
                    StringComparison.Ordinal)),
            ("actual dwell bypasses pinned constant",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs",
                source => source.Replace(
                    "ScheduleDwell(MinimumVisibleStage);",
                    "ScheduleDwell(TimeSpan.FromMilliseconds(551));",
                    StringComparison.Ordinal)),
            ("scheduler declaration outside Presentation",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionService.cs",
                source => source + "\ninternal sealed class RogueModelInspectionMilestoneScheduler { }\n"),
            ("scheduler interface implementation outside Presentation",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionService.cs",
                source => source + "\ninternal sealed class RogueScheduler : IModelInspectionMilestoneScheduler { }\n"),
            ("safety terminal routed past its immediate branch",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs",
                source => source.Replace(
                    "if (IsImmediateSafetySnapshot(snapshot))",
                    "if (false && IsImmediateSafetySnapshot(snapshot))",
                    StringComparison.Ordinal)),
            ("determinate active ProgressRing",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml",
                source => source.Replace(
                    "<Grid",
                    "<Grid><ProgressRing IsIndeterminate=\"False\" /></Grid><Grid",
                    StringComparison.Ordinal)),
            ("determinate ProgressRing in shared glyph",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<ProgressRing IsIndeterminate=\"False\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("fraction-driven active glyph",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml.cs",
                source => source + "\n// production mutation\ninternal sealed class FractionOrbit { internal double StageFraction { get; set; } }\n"),
            ("fraction-driven active glyph XAML",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml",
                source => source.Replace(
                    "Tag=\"PrecisionOrbitRotationTarget\"",
                    "Tag=\"{Binding StageFraction}\"",
                    StringComparison.Ordinal)),
            ("stock status SymbolIcon",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<SymbolIcon Symbol=\"Accept\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("stock status SymbolIcon in shared glyph",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<SymbolIcon Symbol=\"Accept\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("unscoped Forward SymbolIcon",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<SymbolIcon Symbol=\"Forward\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("stock status FontIcon",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<FontIcon Glyph=\"&#xE73E;\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("Unicode status glyph",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<TextBlock Text=\"&#x2713;\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("old onboarding not-complete Unicode glyph",
                "IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml",
                source => source.Replace(
                    "</UserControl>",
                    "<TextBlock Text=\"‖\" /></UserControl>",
                    StringComparison.Ordinal)),
            ("old onboarding not-complete code-behind glyph",
                "IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml.cs",
                source => source +
                    "\ninternal sealed class RogueFooterGlyph { internal void Apply(TextBlock footer) => footer.Text = \"‖\"; }\n"),
            ("state-specific literal card gap",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml",
                source => source.Replace(
                    "Height=\"{StaticResource InspectionCardGap}\"",
                    "Height=\"17\"",
                    StringComparison.Ordinal)),
            ("legacy disclosure minimum height",
                "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml",
                source => source.Replace(
                    "<Grid",
                    "<Grid MinHeight=\"83\"",
                    StringComparison.Ordinal))
        ];

        foreach ((string name, string path, Func<string, string> mutate) in mutations)
        {
            var mutation = new Dictionary<string, string>(
                progressPolishSources,
                StringComparer.Ordinal);
            mutation[path] = mutate(mutation[path]);
            Assert.IsNotEmpty(
                ValidateProgressPolishSources(mutation),
                $"The progress-polish source guard accepted the {name} mutation.");
        }

        foreach ((string path, string harmlessText) in new[]
                 {
                     (
                         "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyModelProbe.cs",
                         "\n// Task.Delay(1); Thread.Sleep(1); System.Threading.Timer DispatcherQueueTimer\n"),
                     (
                         "workers/GraniteEdgeAI.ModelInspection.Worker/LlamaSharpInspectionEngine.cs",
                         "\ninternal const string GuardExample = \"Task.Delay Thread.Sleep System.Threading.Timer DispatcherQueueTimer\";\n")
                 })
        {
            var harmless = new Dictionary<string, string>(
                progressPolishSources,
                StringComparer.Ordinal)
            {
                [path] = progressPolishSources[path] + harmlessText
            };
            Assert.AreEqual(
                0,
                ValidateProgressPolishSources(harmless).Length,
                "Comments and string literals must not be treated as executable pacing code.");
        }
    }

    private static Dictionary<string, string> ReadProgressPolishSources()
    {
        string[] roots =
        [
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection",
            "IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls",
            "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp",
            "workers/GraniteEdgeAI.ModelInspection.Worker"
        ];
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string relativeRoot in roots)
        {
            string absoluteRoot = Path.Combine(
                Root,
                relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            foreach (string path in Directory.GetFiles(
                         absoluteRoot,
                         "*.*",
                         SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.Ordinal))
            {
                string relativePath = Path.GetRelativePath(Root, path)
                    .Replace('\\', '/');
                sources.Add(relativePath, File.ReadAllText(path));
            }
        }

        return sources;
    }

    private static string[] ValidateProgressPolishSources(
        IReadOnlyDictionary<string, string> sources)
    {
        const string SchedulerPath =
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherQueueModelInspectionMilestoneScheduler.cs";
        const string SequencerPath =
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs";
        const string FixtureSchedulerPath =
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs";
        const string PagePath =
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml";
        var errors = new List<string>();

        foreach ((string path, string source) in sources)
        {
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string code = MaskCSharpTrivia(source);
            bool progressBoundary =
                path.StartsWith(
                    "runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/",
                    StringComparison.Ordinal) ||
                path.StartsWith(
                    "workers/GraniteEdgeAI.ModelInspection.Worker/",
                    StringComparison.Ordinal) ||
                path.Contains(
                    "/Features/ModelInspection/Runtime/",
                    StringComparison.Ordinal) ||
                path.Contains(
                    "/Features/ModelInspection/Services/",
                    StringComparison.Ordinal);
            if (progressBoundary)
            {
                foreach ((string name, string pattern) in new[]
                         {
                             ("Task.Delay", @"\bTask\s*\.\s*Delay\s*\("),
                             ("Thread.Sleep", @"\bThread\s*\.\s*Sleep\s*\("),
                             ("System.Threading.Timer", @"\bSystem\s*\.\s*Threading\s*\.\s*Timer\b"),
                             ("DispatcherQueueTimer", @"\bDispatcherQueueTimer\b")
                         })
                {
                    if (Regex.IsMatch(
                            code,
                            pattern,
                            RegexOptions.CultureInvariant))
                    {
                        errors.Add($"{path} contains prohibited progress pacing: {name}.");
                    }
                }
            }

            if (!string.Equals(path, SchedulerPath, StringComparison.Ordinal) &&
                Regex.IsMatch(
                    code,
                    @"\bDispatcherQueueTimer\b",
                    RegexOptions.CultureInvariant))
            {
                errors.Add(
                    $"DispatcherQueueTimer is allowed only in {SchedulerPath}: {path}.");
            }

            bool declaresNamedScheduler = Regex.IsMatch(
                code,
                @"\b(?:class|interface|record|struct)\s+\w*ModelInspectionMilestoneScheduler\b",
                RegexOptions.CultureInvariant);
            bool implementsMilestoneScheduler = Regex.IsMatch(
                code,
                @"\b(?:class|record|struct)\s+[A-Za-z_]\w*(?:\s*<[^>{}]*>)?(?:\s*\([^;{}]*\))?\s*:\s*[^;{}]*\bIModelInspectionMilestoneScheduler\b",
                RegexOptions.CultureInvariant);
            if ((declaresNamedScheduler || implementsMilestoneScheduler) &&
                !path.StartsWith(
                    "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    path,
                    FixtureSchedulerPath,
                    StringComparison.Ordinal))
            {
                errors.Add($"A milestone scheduler is declared outside Presentation: {path}.");
            }
        }

        if (!sources.TryGetValue(SequencerPath, out string? sequencer))
        {
            errors.Add("The milestone sequencer source is missing.");
        }
        else
        {
            string code = CollapseWhitespace(MaskCSharpTrivia(sequencer));
            if (Count(code, "TimeSpan.FromMilliseconds(550)") != 1 ||
                Count(
                    code,
                    "MinimumVisibleStage = TimeSpan.FromMilliseconds(550)") != 1)
            {
                errors.Add("The milestone sequencer must pin exactly one 550 ms minimum.");
            }

            MatchCollection dwellCalls = Regex.Matches(
                code,
                @"\bScheduleDwell\s*\(\s*(?<argument>[^;]*)\)\s*;",
                RegexOptions.CultureInvariant);
            if (dwellCalls.Count != 1 ||
                !string.Equals(
                    dwellCalls[0].Groups["argument"].Value.Trim(),
                    "MinimumVisibleStage",
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "The milestone sequencer must schedule its only dwell from MinimumVisibleStage.");
            }

            const string ImmediateSafetyBlock =
                "if (IsImmediateSafetySnapshot(snapshot)) { ClearPlayback(); unpacedStage = -1; applySnapshot(snapshot); return; }";
            if (Count(code, ImmediateSafetyBlock) != 1)
            {
                errors.Add(
                    "Cancellation and operational-failure snapshots must bypass dwell scheduling through the exact immediate safety branch.");
            }

            foreach (string status in new[]
                     {
                         "snapshot.IsCancellationRequested",
                         "ModelInspectionStageStatus.Failed",
                         "ModelInspectionStageStatus.Cancelled",
                         "ModelInspectionExecutionStatus.Cancelled",
                         "ModelInspectionExecutionStatus.OperationalFailure"
                     })
            {
                if (!code.Contains(status, StringComparison.Ordinal))
                {
                    errors.Add($"The immediate safety classifier is missing {status}.");
                }
            }
        }

        string[] affectedXamlPaths =
        [
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml",
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml",
            "IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml"
        ];
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        char[] prohibitedUnicodeGlyphs =
            ['\u2713', '\u2714', '\u26A0', '\u2715', '\u2716', '\u00D7', '\u2139', '\u2016'];
        foreach (string path in affectedXamlPaths)
        {
            if (!sources.TryGetValue(path, out string? source))
            {
                errors.Add($"The affected status surface is missing: {path}.");
                continue;
            }

            string markup = Regex.Replace(
                source,
                "(?s)<!--.*?-->",
                string.Empty,
                RegexOptions.CultureInvariant);
            try
            {
                XDocument document = XDocument.Parse(markup);
                foreach (XElement element in document.Descendants())
                {
                    if (element.Name.LocalName == "ProgressRing")
                    {
                        errors.Add($"The active status surface must not use ProgressRing: {path}.");
                    }

                    if (element.Name.LocalName == "SymbolIcon")
                    {
                        bool approvedTechnicalDetailsChevron =
                            path.EndsWith(
                                "/InspectionContentCard.xaml",
                                StringComparison.Ordinal) &&
                            string.Equals(
                                element.Attribute("Symbol")?.Value,
                                "Forward",
                                StringComparison.Ordinal) &&
                            element.Ancestors().Any(ancestor =>
                                ancestor.Name.LocalName == "Button" &&
                                string.Equals(
                                    ancestor.Attribute(xamlNamespace + "Name")?.Value,
                                    "TechnicalDetailsButton",
                                    StringComparison.Ordinal));
                        if (!approvedTechnicalDetailsChevron)
                        {
                            errors.Add($"A stock status SymbolIcon remains in {path}.");
                        }
                    }

                    if (element.Name.LocalName == "FontIcon" &&
                        !(path.EndsWith(
                              "/InspectionDisclosure.xaml",
                              StringComparison.Ordinal) &&
                          string.Equals(
                              element.Attribute("Glyph")?.Value,
                              "\uE70D",
                              StringComparison.Ordinal)))
                    {
                        errors.Add($"A stock status FontIcon remains in {path}.");
                    }
                }
            }
            catch (Exception exception) when (
                exception is System.Xml.XmlException or InvalidOperationException)
            {
                errors.Add($"The affected status XAML is invalid: {path}: {exception.Message}");
            }

            string decodedMarkup = System.Net.WebUtility.HtmlDecode(markup);
            foreach (char glyph in prohibitedUnicodeGlyphs)
            {
                if (decodedMarkup.Contains(glyph, StringComparison.Ordinal))
                {
                    errors.Add($"A Unicode status glyph remains in {path}: U+{(int)glyph:X4}.");
                }
            }

            if (Regex.IsMatch(
                    markup,
                    @"\bMinHeight\s*=\s*[\""']83[\""']",
                    RegexOptions.CultureInvariant))
            {
                errors.Add($"The legacy 83 px disclosure minimum returned in {path}.");
            }

            string codeBehindPath = path + ".cs";
            if (sources.TryGetValue(codeBehindPath, out string? codeBehind))
            {
                string executableCodeBehind = MaskCSharpComments(codeBehind);
                foreach (char glyph in prohibitedUnicodeGlyphs)
                {
                    if (executableCodeBehind.Contains(glyph, StringComparison.Ordinal) ||
                        Regex.IsMatch(
                            executableCodeBehind,
                            $@"\\u{(int)glyph:X4}\b",
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        errors.Add(
                            $"A Unicode status glyph remains in {codeBehindPath}: U+{(int)glyph:X4}.");
                    }
                }
            }
        }

        const string GlyphCodePath =
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml.cs";
        const string GlyphXamlPath =
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml";
        bool glyphConsumesFraction = false;
        if (!sources.TryGetValue(GlyphCodePath, out string? glyphCode))
        {
            errors.Add("The Precision Orbit glyph code-behind is missing.");
        }
        else
        {
            glyphConsumesFraction = Regex.IsMatch(
                MaskCSharpTrivia(glyphCode),
                @"\bStageFraction\b",
                RegexOptions.CultureInvariant);
        }

        if (!sources.TryGetValue(GlyphXamlPath, out string? glyphXaml))
        {
            errors.Add("The Precision Orbit glyph XAML is missing.");
        }
        else
        {
            string glyphMarkup = Regex.Replace(
                glyphXaml,
                "(?s)<!--.*?-->",
                string.Empty,
                RegexOptions.CultureInvariant);
            glyphConsumesFraction |= Regex.IsMatch(
                glyphMarkup,
                @"\bStageFraction\b",
                RegexOptions.CultureInvariant);
        }

        if (glyphConsumesFraction)
        {
            errors.Add("Precision Orbit motion must not consume a stage fraction.");
        }

        if (!sources.TryGetValue(PagePath, out string? page))
        {
            errors.Add("The Model Inspection page XAML is missing.");
        }
        else
        {
            XDocument pageDocument = XDocument.Parse(page);
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            IReadOnlyDictionary<string, string> expectedGaps =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["HeaderToFirstCardGap"] = "{StaticResource InspectionHeaderToCardGap}",
                    ["OutcomeToModelCardGap"] = "{StaticResource InspectionCardGap}",
                    ["ModelToContentLiveGap"] = "{StaticResource InspectionCardGap}",
                    ["ModelToContentOutgoingGap"] = "{StaticResource InspectionCardGap}",
                    ["ContentToActionCardGap"] = "{StaticResource InspectionCardGap}"
                };
            foreach ((string name, string height) in expectedGaps)
            {
                XElement[] matches = pageDocument.Descendants()
                    .Where(element => string.Equals(
                        element.Attribute(xaml + "Name")?.Value,
                        name,
                        StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1 ||
                    !string.Equals(
                        matches[0].Attribute("Height")?.Value,
                        height,
                        StringComparison.Ordinal))
                {
                    errors.Add($"The shared card gap drifted: {name}.");
                }
            }
        }

        return errors.ToArray();
    }

    private static string MaskCSharpTrivia(string source)
    {
        char[] masked = source.ToCharArray();
        void Mask(int start, int end)
        {
            for (int index = start; index < end; index++)
            {
                if (masked[index] is not ('\r' or '\n'))
                {
                    masked[index] = ' ';
                }
            }
        }

        int offset = 0;
        while (offset < source.Length)
        {
            if (offset + 1 < source.Length &&
                source[offset] == '/' && source[offset + 1] == '/')
            {
                int end = source.IndexOf('\n', offset + 2);
                end = end < 0 ? source.Length : end;
                Mask(offset, end);
                offset = end;
                continue;
            }

            if (offset + 1 < source.Length &&
                source[offset] == '/' && source[offset + 1] == '*')
            {
                int close = source.IndexOf("*/", offset + 2, StringComparison.Ordinal);
                int end = close < 0 ? source.Length : close + 2;
                Mask(offset, end);
                offset = end;
                continue;
            }

            if (source[offset] is '\'' or '"')
            {
                char quote = source[offset];
                bool raw = quote == '"' && offset + 2 < source.Length &&
                    source[offset + 1] == '"' && source[offset + 2] == '"';
                int quoteCount = raw ? 3 : 1;
                bool verbatim = quote == '"' && offset > 0 && source[offset - 1] == '@';
                int end = offset + quoteCount;
                while (end < source.Length)
                {
                    if (raw && end + 2 < source.Length &&
                        source[end] == '"' && source[end + 1] == '"' && source[end + 2] == '"')
                    {
                        end += 3;
                        break;
                    }

                    if (!raw && verbatim && source[end] == '"' &&
                        end + 1 < source.Length && source[end + 1] == '"')
                    {
                        end += 2;
                        continue;
                    }

                    if (!raw && !verbatim && source[end] == '\\' &&
                        end + 1 < source.Length)
                    {
                        end += 2;
                        continue;
                    }

                    if (!raw && source[end++] == quote)
                    {
                        break;
                    }

                    if (raw)
                    {
                        end++;
                    }
                }

                Mask(offset, end);
                offset = end;
                continue;
            }

            offset++;
        }

        return new string(masked);
    }

    private static string MaskCSharpComments(string source)
    {
        char[] masked = source.ToCharArray();
        void Mask(int start, int end)
        {
            for (int index = start; index < end; index++)
            {
                if (masked[index] is not ('\r' or '\n'))
                {
                    masked[index] = ' ';
                }
            }
        }

        int offset = 0;
        while (offset < source.Length)
        {
            if (offset + 1 < source.Length &&
                source[offset] == '/' && source[offset + 1] == '/')
            {
                int end = source.IndexOf('\n', offset + 2);
                end = end < 0 ? source.Length : end;
                Mask(offset, end);
                offset = end;
                continue;
            }

            if (offset + 1 < source.Length &&
                source[offset] == '/' && source[offset + 1] == '*')
            {
                int close = source.IndexOf("*/", offset + 2, StringComparison.Ordinal);
                int end = close < 0 ? source.Length : close + 2;
                Mask(offset, end);
                offset = end;
                continue;
            }

            if (source[offset] is '\'' or '"')
            {
                char quote = source[offset];
                bool raw = quote == '"' && offset + 2 < source.Length &&
                    source[offset + 1] == '"' && source[offset + 2] == '"';
                int quoteCount = raw ? 3 : 1;
                bool verbatim = quote == '"' && offset > 0 && source[offset - 1] == '@';
                int end = offset + quoteCount;
                while (end < source.Length)
                {
                    if (raw && end + 2 < source.Length &&
                        source[end] == '"' && source[end + 1] == '"' && source[end + 2] == '"')
                    {
                        end += 3;
                        break;
                    }

                    if (!raw && verbatim && source[end] == '"' &&
                        end + 1 < source.Length && source[end + 1] == '"')
                    {
                        end += 2;
                        continue;
                    }

                    if (!raw && !verbatim && source[end] == '\\' &&
                        end + 1 < source.Length)
                    {
                        end += 2;
                        continue;
                    }

                    if (!raw && source[end++] == quote)
                    {
                        break;
                    }

                    if (raw)
                    {
                        end++;
                    }
                }

                offset = end;
                continue;
            }

            offset++;
        }

        return new string(masked);
    }

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(
            value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim();

    private static void AssertThemeMap(
        XDocument theme,
        string themeName,
        IReadOnlyDictionary<string, string> expected)
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement themeDictionaries = theme
            .Descendants(presentation + "ResourceDictionary.ThemeDictionaries")
            .Single();
        XElement dictionary = themeDictionaries
            .Elements(presentation + "ResourceDictionary")
            .Single(element => string.Equals(
                element.Attribute(xaml + "Key")?.Value,
                themeName,
                StringComparison.Ordinal));
        Dictionary<string, string> actual = dictionary
            .Elements(presentation + "SolidColorBrush")
            .ToDictionary(
                element => element.Attribute(xaml + "Key")?.Value
                    ?? throw new InvalidDataException($"{themeName} brush has no x:Key."),
                element => element.Attribute("Color")?.Value
                    ?? throw new InvalidDataException($"{themeName} brush has no Color."),
                StringComparer.Ordinal);

        Assert.AreEqual(expected.Count, actual.Count, $"{themeName} semantic brush count");
        foreach ((string key, string value) in expected)
        {
            Assert.IsTrue(actual.ContainsKey(key), $"{themeName} must define {key}.");
            Assert.AreEqual(value, actual[key], $"{themeName}/{key}");
        }
    }

    private static IEnumerable<XElement> ProjectItems(
        XDocument project,
        string itemName,
        string attributeName,
        string expectedPath) =>
        project
            .Descendants(itemName)
            .Where(item => string.Equals(
                NormalizeProjectPath(item.Attribute(attributeName)?.Value),
                expectedPath,
                StringComparison.Ordinal));

    private static IReadOnlyList<ProjectPackageItemSpec> ProjectItemSpecs(
        string projectPath)
    {
        string absoluteProjectPath = Path.GetFullPath(projectPath);
        string evaluationProjectDirectory = Path.GetDirectoryName(
            absoluteProjectPath)
            ?? throw new InvalidDataException(
                $"{absoluteProjectPath} has no directory.");
        var itemSpecs = new List<ProjectPackageItemSpec>();
        var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddProjectItemSpecs(
            absoluteProjectPath,
            evaluationProjectDirectory,
            visitedProjects,
            itemSpecs);
        return itemSpecs;
    }

    private static void AddProjectItemSpecs(
        string projectPath,
        string evaluationProjectDirectory,
        ISet<string> visitedProjects,
        ICollection<ProjectPackageItemSpec> itemSpecs)
    {
        string absoluteProjectPath = Path.GetFullPath(projectPath);
        if (!visitedProjects.Add(absoluteProjectPath))
        {
            return;
        }

        XDocument project = XDocument.Load(absoluteProjectPath);
        foreach (XElement item in project
                     .Descendants()
                     .Where(item =>
                         PackageRelevantItemNames.Contains(item.Name.LocalName) ||
                         string.Equals(
                             item.Name.LocalName,
                             WorkerPublishedFilesItemName,
                             StringComparison.OrdinalIgnoreCase) ||
                         WorkerPackagePathPropertyNames.Contains(
                             item.Name.LocalName)))
        {
            if (WorkerPackagePathPropertyNames.Contains(item.Name.LocalName))
            {
                itemSpecs.Add(new ProjectPackageItemSpec(
                    item.Value,
                    absoluteProjectPath,
                    evaluationProjectDirectory,
                    IsControlledWorkerPackageExpression: false,
                    IsControlledWorkerPublishedFilesExpression: false,
                    IsWorkerPackagePathPropertyDefinition: true,
                    IsControlledWorkerPackagePathPropertyDefinition:
                        IsControlledWorkerPackagePathPropertyDefinition(
                            absoluteProjectPath,
                            item.Name.LocalName,
                            item.Value)));
                continue;
            }

            foreach (string attributeName in new[] { "Include", "Update" })
            {
                string? value = item.Attribute(attributeName)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    itemSpecs.Add(new ProjectPackageItemSpec(
                        value,
                        absoluteProjectPath,
                        evaluationProjectDirectory,
                        IsControlledWorkerPackageExpression(
                            absoluteProjectPath,
                            item.Name.LocalName,
                            value),
                        IsControlledWorkerPublishedFilesExpression(
                            absoluteProjectPath,
                            item.Name.LocalName,
                            value),
                        IsWorkerPackagePathPropertyDefinition: false,
                        IsControlledWorkerPackagePathPropertyDefinition: false));
                }
            }
        }

        string projectDirectory = Path.GetDirectoryName(absoluteProjectPath)
            ?? throw new InvalidDataException(
                $"{absoluteProjectPath} has no directory.");
        foreach (XElement import in project
                     .Descendants()
                     .Where(element => string.Equals(
                         element.Name.LocalName,
                         "Import",
                         StringComparison.Ordinal)))
        {
            string importSpec = import.Attribute("Project")?.Value
                ?? throw new InvalidDataException(
                    $"{absoluteProjectPath} has an Import without Project.");
            if (ContainsMsBuildExpression(importSpec) ||
                importSpec.IndexOfAny(['*', '?']) >= 0)
            {
                Assert.Fail(
                    $"{absoluteProjectPath} has an unresolved explicit Import " +
                    $"'{importSpec}'.");
            }

            foreach (string importCandidate in importSpec.Split(
                         ';',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                string importedProject = Path.GetFullPath(Path.Combine(
                    projectDirectory,
                    NormalizeProjectPath(importCandidate)!));
                Assert.IsTrue(
                    File.Exists(importedProject),
                    $"Explicit import does not exist: {importedProject}");
                AddProjectItemSpecs(
                    importedProject,
                    evaluationProjectDirectory,
                    visitedProjects,
                    itemSpecs);
            }
        }
    }

    private static bool IsControlledWorkerPackageExpression(
        string sourcePath,
        string itemName,
        string itemSpec)
    {
        if (!IsWorkerPackagingTarget(sourcePath))
        {
            return false;
        }

        return (itemName, itemSpec) switch
        {
            ("Content", "@(_ModelInspectionWorkerPublishedFiles)") => true,
            ("Content", "$(_ModelInspectionWorkerManifestPath)") => true,
            ("EmbeddedResource", "$(_ModelInspectionWorkerManifestPath)") => true,
            ("Content", "@(_OpenVinoOfficialWorkerFile)") => true,
            _ => false
        };
    }

    private static bool IsControlledWorkerPublishedFilesExpression(
        string sourcePath,
        string itemName,
        string itemSpec) =>
        IsWorkerPackagingTarget(sourcePath) &&
        string.Equals(
            itemName,
            WorkerPublishedFilesItemName,
            StringComparison.Ordinal) &&
        string.Equals(
            itemSpec,
            WorkerPublishedFilesRootExpression,
            StringComparison.Ordinal);

    private static bool IsControlledWorkerPackagePathPropertyDefinition(
        string sourcePath,
        string propertyName,
        string propertyValue)
    {
        if (!IsWorkerPackagingTarget(sourcePath))
        {
            return false;
        }

        return (propertyName, propertyValue) switch
        {
            (WorkerPublishRootPropertyName, WorkerPublishRootPropertyValue) => true,
            (WorkerManifestPathPropertyName, WorkerManifestPathPropertyValue) => true,
            _ => false
        };
    }

    private static bool IsWorkerPackagingTarget(string sourcePath)
    {
        string relativeSource = NormalizeProjectPath(
            Path.GetRelativePath(Root, sourcePath))!;
        return relativeSource is
            "IBM Granite with TurboQuant (Intel)/ModelInspection.WorkerPackaging.targets" or
            "IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets";
    }

    private static bool ContainsMsBuildExpression(string value) =>
        value.Contains("$(", StringComparison.Ordinal) ||
        value.Contains("@(", StringComparison.Ordinal) ||
        value.Contains("%(", StringComparison.Ordinal);

    private static bool ProjectItemSpecMatchesPath(
        ProjectPackageItemSpec itemSpec,
        string targetPath)
    {
        string normalizedTarget = NormalizeProjectPath(Path.GetFullPath(targetPath))!;
        foreach (string candidate in itemSpec.Value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (ContainsMsBuildExpression(candidate))
            {
                if (!itemSpec.IsControlledWorkerPackageExpression &&
                    !itemSpec.IsControlledWorkerPublishedFilesExpression &&
                    !itemSpec.IsControlledWorkerPackagePathPropertyDefinition)
                {
                    Assert.Fail(
                        $"{itemSpec.SourcePath} contains unresolved " +
                        $"package-relevant item expression '{candidate}'.");
                }

                continue;
            }

            string normalizedCandidate = NormalizeProjectPath(candidate)!;
            int wildcardIndex = normalizedCandidate.IndexOfAny(['*', '?']);
            string absolutePattern;
            if (wildcardIndex < 0)
            {
                absolutePattern = NormalizeProjectPath(Path.GetFullPath(
                    Path.Combine(
                        itemSpec.EvaluationProjectDirectory,
                        normalizedCandidate)))!;
            }
            else
            {
                int directoryEnd = normalizedCandidate.LastIndexOf(
                    '/',
                    wildcardIndex);
                string staticDirectory = directoryEnd < 0
                    ? string.Empty
                    : normalizedCandidate[..(directoryEnd + 1)];
                string wildcardPattern = directoryEnd < 0
                    ? normalizedCandidate
                    : normalizedCandidate[(directoryEnd + 1)..];
                string absoluteDirectory = NormalizeProjectPath(Path.GetFullPath(
                    Path.Combine(
                        itemSpec.EvaluationProjectDirectory,
                        staticDirectory)))!;
                absolutePattern =
                    $"{absoluteDirectory.TrimEnd('/')}/{wildcardPattern}";
            }

            string regexPattern = Regex.Escape(absolutePattern)
                .Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal)
                .Replace(@"\*\*", ".*", StringComparison.Ordinal)
                .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
                .Replace(@"\?", "[^/]", StringComparison.Ordinal);
            if (Regex.IsMatch(
                    normalizedTarget,
                    $"^{regexPattern}$",
                    RegexOptions.CultureInvariant |
                    RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ProjectPackageItemSpec(
        string Value,
        string SourcePath,
        string EvaluationProjectDirectory,
        bool IsControlledWorkerPackageExpression,
        bool IsControlledWorkerPublishedFilesExpression,
        bool IsWorkerPackagePathPropertyDefinition,
        bool IsControlledWorkerPackagePathPropertyDefinition);

    private static string? NormalizeProjectPath(string? path) => path?.Replace('\\', '/');

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static XDocument ReadXml(string relativePath) =>
        XDocument.Load(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static PrivacyScanResult RunPrivacyScanner(string artifactRoot)
    {
        string script = Path.Combine(
            Root,
            "scripts",
            "model-inspection",
            "Test-ModelInspectionVisualArtifactPrivacy.ps1");
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("-ArtifactRoot");
        startInfo.ArgumentList.Add(artifactRoot);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start PowerShell.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(milliseconds: 20_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(milliseconds: 5_000);
            Assert.Fail("The visual-artifact privacy scanner timed out.");
        }

        Task.WaitAll(standardOutput, standardError);
        return new PrivacyScanResult(
            process.ExitCode,
            standardOutput.Result + Environment.NewLine + standardError.Result);
    }

    private static byte[] MinimalPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGD4DwABBAEAX+XDSwAAAABJRU5ErkJggg==");

    private static byte[] PngWithTextChunk(string keyword, string value)
        => PngWithChunk(
            "tEXt",
            Encoding.Latin1.GetBytes($"{keyword}\0{value}"));

    private static byte[] PngWithChunk(string chunkType, byte[] payload)
    {
        byte[] png = MinimalPng();
        byte[] chunk = BuildPngChunk(chunkType, payload);

        int insertionOffset = png.Length - 12;
        byte[] withText = new byte[checked(png.Length + chunk.Length)];
        png.AsSpan(0, insertionOffset).CopyTo(withText);
        chunk.CopyTo(withText, insertionOffset);
        png.AsSpan(insertionOffset).CopyTo(
            withText.AsSpan(insertionOffset + chunk.Length));
        return withText;
    }

    private static byte[] PngWithEncoderMetadata(
        byte[] gammaPayload,
        bool repeatPhysicalDimensions)
    {
        byte[] png = MinimalPng();
        byte[] physicalDimensions = [0, 0, 14, 195, 0, 0, 14, 195, 1];
        var chunks = new List<byte[]>
        {
            BuildPngChunk("sRGB", [0]),
            BuildPngChunk("gAMA", gammaPayload),
            BuildPngChunk("pHYs", physicalDimensions)
        };
        if (repeatPhysicalDimensions)
        {
            chunks.Add(BuildPngChunk("pHYs", physicalDimensions));
        }

        const int AfterHeaderOffset = 33;
        int metadataLength = chunks.Sum(chunk => chunk.Length);
        byte[] result = new byte[checked(png.Length + metadataLength)];
        png.AsSpan(0, AfterHeaderOffset).CopyTo(result);
        int offset = AfterHeaderOffset;
        foreach (byte[] chunk in chunks)
        {
            chunk.CopyTo(result, offset);
            offset += chunk.Length;
        }

        png.AsSpan(AfterHeaderOffset).CopyTo(result.AsSpan(offset));
        return result;
    }

    private static byte[] PngWithCorruptCrc()
    {
        byte[] png = MinimalPng();
        const int IdatCrcOffset = 54;
        png[IdatCrcOffset] ^= 0x01;
        return png;
    }

    private static byte[] BuildPngChunk(string chunkType, byte[] payload)
    {
        byte[] chunk = new byte[checked(payload.Length + 12)];
        BinaryPrimitives.WriteUInt32BigEndian(
            chunk.AsSpan(0, 4),
            checked((uint)payload.Length));
        Encoding.ASCII.GetBytes(chunkType, chunk.AsSpan(4, 4));
        payload.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(
            chunk.AsSpan(payload.Length + 8, 4),
            ComputePngCrc(chunk.AsSpan(4, payload.Length + 4)));
        return chunk;
    }

    private static uint ComputePngCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0
                    ? crc >> 1
                    : (crc >> 1) ^ 0xedb88320u;
            }
        }

        return ~crc;
    }

    private sealed record PrivacyScanResult(int ExitCode, string CombinedOutput);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
