using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ModelInspectionVisualSourceContractTests
{
    private static readonly string Root = FindRepositoryRoot();

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

    private static string? NormalizeProjectPath(string? path) => path?.Replace('\\', '/');

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static XDocument ReadXml(string relativePath) =>
        XDocument.Load(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
