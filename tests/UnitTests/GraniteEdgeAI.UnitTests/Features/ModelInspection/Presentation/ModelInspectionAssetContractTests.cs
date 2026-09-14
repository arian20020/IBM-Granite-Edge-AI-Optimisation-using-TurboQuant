using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.ApplicationModel;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
[TestCategory("WinUI")]
public sealed class ModelInspectionAssetContractTests
{
    private const string ThemeSource =
        "ms-appx:///Features/ModelInspection/Presentation/ModelInspectionTheme.xaml";

    private static readonly IReadOnlyDictionary<string, string> LightPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InspectionTextPrimaryBrush"] = "#111827",
            ["InspectionTextSecondaryStrongBrush"] = "#344054",
            ["InspectionTextSecondaryBrush"] = "#475467",
            ["InspectionTextSecondaryMutedBrush"] = "#5B677A",
            ["InspectionTextMutedBrush"] = "#758196",
            ["InspectionTextSubtleBrush"] = "#758196",
            ["InspectionPrimaryBlueBrush"] = "#0F62FE",
            ["InspectionBlueSurfaceBrush"] = "#EEF5FF",
            ["InspectionWaitingSurfaceBrush"] = "#EEF1F5",
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
            ["InspectionCanvasBrush"] = "#F8FAFD",
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
            ["InspectionWaitingSurfaceBrush"] = "#273142",
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

    private static readonly PinnedAsset RegularFont = new(
        "Inter-Regular.ttf",
        411640,
        "40d692fce188e4471e2b3cba937be967878f631ad3ebbbdcd587687c7ebe0c82",
        "Regular",
        400);

    private static readonly PinnedAsset BoldFont = new(
        "Inter-Bold.ttf",
        420428,
        "288316099b1e0a47a4716d159098005eef7c0066921f34e3200393dbdb01947f",
        "Bold",
        700);

    private static readonly PinnedAsset LicenseFile = new(
        "OFL.txt",
        4380,
        "262481e844521b326f5ecd053e59b98c8b2da78c8ee1bdbb6e8174305e54935a");

    private static readonly PinnedAsset[] PinnedAssets =
    [
        RegularFont,
        BoldFont,
        LicenseFile
    ];

    [UITestMethod]
    public void ThemeResources_AreSharedAndPackaged()
    {
        ResourceDictionary applicationResources = Application.Current.Resources;
        ResourceDictionary theme = applicationResources.MergedDictionaries.Single(
            dictionary => string.Equals(
                dictionary.Source?.OriginalString,
                ThemeSource,
                StringComparison.Ordinal));

        Assert.AreEqual(
            "ms-appx:///Assets/Fonts/Inter-Regular.ttf#Inter",
            ((FontFamily)theme["InspectionFontFamilyRegular"]).Source);
        Assert.AreEqual(
            "ms-appx:///Assets/Fonts/Inter-Bold.ttf#Inter",
            ((FontFamily)theme["InspectionFontFamilyBold"]).Source);
        Assert.AreEqual((ushort)400, GetFontWeight(theme, "InspectionFontWeightRegular"));
        Assert.AreEqual((ushort)700, GetFontWeight(theme, "InspectionFontWeightBold"));
        AssertTypographyRole(
            theme,
            "InspectionPageTitleFontFamily",
            "InspectionPageTitleFontWeight",
            bold: true);
        AssertTypographyRole(
            theme,
            "InspectionSectionTitleFontFamily",
            "InspectionSectionTitleFontWeight",
            bold: true);
        AssertTypographyRole(
            theme,
            "InspectionBodyFontFamily",
            "InspectionBodyFontWeight",
            bold: false);
        AssertTypographyRole(
            theme,
            "InspectionStrongBodyFontFamily",
            "InspectionStrongBodyFontWeight",
            bold: true);
        AssertTypographyRole(
            theme,
            "InspectionHelperFontFamily",
            "InspectionHelperFontWeight",
            bold: false);
        AssertTypographyRole(
            theme,
            "InspectionLabelFontFamily",
            "InspectionLabelFontWeight",
            bold: true);

        AssertTokens(theme, new Dictionary<string, double>
        {
            ["InspectionPageTitleFontSize"] = 32,
            ["InspectionSectionTitleFontSize"] = 18,
            ["InspectionBodyFontSize"] = 14,
            ["InspectionHelperFontSize"] = 12,
            ["InspectionLabelFontSize"] = 10,
            ["InspectionContentColumnWidth"] = 1120,
            ["InspectionNestedRowWidth"] = 792,
            ["InspectionStandardButtonHeight"] = 46,
            ["InspectionMinimumTargetSize"] = 44,
            ["InspectionDesktopBreakpoint"] = 1008,
            ["InspectionCompactBreakpoint"] = 640
        });

        Assert.IsInstanceOfType<Thickness>(theme["InspectionCardPadding"]);
        Assert.IsInstanceOfType<CornerRadius>(theme["InspectionCardCornerRadius"]);

        AssertResolvedPalette(theme, "Light", LightPalette);
        AssertResolvedPalette(theme, "Dark", DarkPalette);
        AssertResolvedPalette(theme, "HighContrast", expectedColors: null);
    }

    [TestMethod]
    public void InterManifest_MatchesPackagedFontBytes()
    {
        string packageRoot = Package.Current.InstalledLocation.Path;
        string fontRoot = Path.Combine(packageRoot, "Assets", "Fonts");
        string manifestPath = Path.Combine(fontRoot, "inter-manifest.json");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement root = manifest.RootElement;
        Assert.AreEqual("Inter", root.GetProperty("family").GetString());
        Assert.AreEqual("4.1", root.GetProperty("release").GetString());
        Assert.AreEqual(
            "https://github.com/rsms/inter",
            root.GetProperty("sourceRepository").GetString());
        Assert.AreEqual(
            "https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip",
            root.GetProperty("sourceArchive").GetString());
        Assert.AreEqual("SIL Open Font License 1.1", root.GetProperty("license").GetString());
        Assert.AreEqual(
            "9883fdd4a49d4fb66bd8177ba6625ef9a64aa45899767dde3d36aa425756b11e",
            root.GetProperty("sourceArchiveSha256").GetString());

        JsonElement.ArrayEnumerator files = root.GetProperty("files").EnumerateArray();
        JsonElement[] entries = files.ToArray();
        Assert.AreEqual(PinnedAssets.Length, entries.Length);
        CollectionAssert.AreEquivalent(
            PinnedAssets.Select(asset => asset.Path).ToArray(),
            entries.Select(entry => entry.GetProperty("path").GetString()).ToArray());

        foreach (PinnedAsset pin in PinnedAssets)
        {
            JsonElement entry = entries.Single(
                candidate => candidate.GetProperty("path").GetString() == pin.Path);
            string assetPath = Path.Combine(fontRoot, pin.Path);
            byte[] bytes = File.ReadAllBytes(assetPath);
            string manifestHash = entry.GetProperty("sha256").GetString()!;
            Assert.IsTrue(Regex.IsMatch(manifestHash, "^[0-9a-f]{64}$"));
            Assert.AreEqual(pin.Length, entry.GetProperty("length").GetInt64(), $"manifest {pin.Path} length");
            Assert.AreEqual(pin.Sha256, manifestHash, $"manifest {pin.Path} SHA-256");
            Assert.AreEqual(pin.Length, bytes.LongLength, $"packaged {pin.Path} length");
            Assert.AreEqual(pin.Sha256, Hash(bytes), $"packaged {pin.Path} SHA-256");

            if (pin.Style is not null)
            {
                Assert.AreEqual(pin.Style, entry.GetProperty("style").GetString());
                Assert.AreEqual(pin.Weight, entry.GetProperty("weight").GetInt32());
                AssertSfntMetadata(bytes, pin.Style, checked((ushort)pin.Weight));
            }
        }
        StringAssert.Contains(
            Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(fontRoot, "OFL.txt"))),
            "SIL OPEN FONT LICENSE Version 1.1");

        string[] packagedFiles = Directory
            .EnumerateFiles(fontRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(fontRoot, path).Replace('\\', '/'))
            .ToArray();
        foreach (string required in new[]
                 {
                     "Inter-Regular.ttf", "Inter-Bold.ttf", "OFL.txt", "inter-manifest.json"
                 })
        {
            Assert.AreEqual(
                1,
                packagedFiles.Count(path => path.Equals(required, StringComparison.Ordinal)),
                $"{required} must be packaged exactly once.");
        }
        CollectionAssert.AreEquivalent(
            new[] { "Inter-Regular.ttf", "Inter-Bold.ttf" },
            packagedFiles.Where(path => path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    [TestMethod]
    public void IndependentPins_RejectMatchingMutatedManifestAndFontBytes()
    {
        string regularPath = Path.Combine(
            Package.Current.InstalledLocation.Path,
            "Assets",
            "Fonts",
            RegularFont.Path);
        byte[] mutatedBytes = File.ReadAllBytes(regularPath);
        mutatedBytes[^1] ^= 0x01;
        string matchingMutatedManifestHash = Hash(mutatedBytes);

        Assert.AreEqual(RegularFont.Length, mutatedBytes.LongLength);
        Assert.AreEqual(matchingMutatedManifestHash, Hash(mutatedBytes));
        Assert.AreNotEqual(RegularFont.Sha256, matchingMutatedManifestHash);
        Assert.IsFalse(
            MatchesIndependentPin(
                mutatedBytes,
                mutatedBytes.LongLength,
                matchingMutatedManifestHash,
                RegularFont),
            "A mutually consistent mutated font and manifest must not satisfy the independent pin.");
    }

    private static ushort GetFontWeight(ResourceDictionary theme, string key) =>
        ((Windows.UI.Text.FontWeight)theme[key]).Weight;

    private static void AssertResolvedPalette(
        ResourceDictionary theme,
        string themeName,
        IReadOnlyDictionary<string, string>? expectedColors)
    {
        Assert.IsTrue(
            theme.ThemeDictionaries.ContainsKey(themeName),
            $"The shared dictionary must define a {themeName} semantic palette.");
        ResourceDictionary palette =
            (ResourceDictionary)theme.ThemeDictionaries[themeName];
        Assert.AreEqual(LightPalette.Count, palette.Count, $"{themeName} semantic brush count");

        foreach (string key in LightPalette.Keys)
        {
            Assert.IsTrue(palette.ContainsKey(key), $"{themeName} must define {key}.");
            SolidColorBrush brush = Assert.IsInstanceOfType<SolidColorBrush>(
                palette[key],
                $"{themeName} must resolve {key} as a SolidColorBrush.");
            if (expectedColors is not null)
            {
                string actual = $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                Assert.AreEqual(expectedColors[key], actual, $"{themeName}/{key}");
            }
        }
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool MatchesIndependentPin(
        byte[] bytes,
        long manifestLength,
        string manifestHash,
        PinnedAsset pin) =>
        manifestLength == pin.Length &&
        string.Equals(manifestHash, pin.Sha256, StringComparison.Ordinal) &&
        bytes.LongLength == pin.Length &&
        string.Equals(Hash(bytes), pin.Sha256, StringComparison.Ordinal);

    private static void AssertSfntMetadata(
        byte[] fontBytes,
        string expectedStyle,
        ushort expectedWeight)
    {
        CollectionAssert.Contains(
            ReadSfntNames(fontBytes, nameId: 1).ToArray(),
            "Inter",
            "The sfnt family name must be Inter.");
        CollectionAssert.Contains(
            ReadSfntNames(fontBytes, nameId: 2).ToArray(),
            expectedStyle,
            "The sfnt subfamily name must match the pinned face.");

        SfntTable os2 = FindSfntTable(fontBytes, "OS/2");
        EnsureRange(fontBytes, os2.Offset, Math.Min(os2.Length, 6));
        Assert.IsTrue(os2.Length >= 6, "The OS/2 table must contain usWeightClass.");
        ushort weight = BinaryPrimitives.ReadUInt16BigEndian(
            fontBytes.AsSpan(os2.Offset + 4, sizeof(ushort)));
        Assert.AreEqual(expectedWeight, weight, "OS/2 usWeightClass");
    }

    private static IReadOnlyList<string> ReadSfntNames(byte[] fontBytes, ushort nameId)
    {
        SfntTable name = FindSfntTable(fontBytes, "name");
        Assert.IsTrue(name.Length >= 6, "The sfnt name table is truncated.");
        ushort count = BinaryPrimitives.ReadUInt16BigEndian(
            fontBytes.AsSpan(name.Offset + 2, sizeof(ushort)));
        ushort stringOffset = BinaryPrimitives.ReadUInt16BigEndian(
            fontBytes.AsSpan(name.Offset + 4, sizeof(ushort)));
        int recordsLength = checked(count * 12);
        EnsureTableRange(name, 6, recordsLength);

        List<string> values = [];
        for (int index = 0; index < count; index++)
        {
            int recordOffset = checked(name.Offset + 6 + (index * 12));
            ushort platformId = BinaryPrimitives.ReadUInt16BigEndian(
                fontBytes.AsSpan(recordOffset, sizeof(ushort)));
            ushort currentNameId = BinaryPrimitives.ReadUInt16BigEndian(
                fontBytes.AsSpan(recordOffset + 6, sizeof(ushort)));
            if (currentNameId != nameId || (platformId != 0 && platformId != 1 && platformId != 3))
            {
                continue;
            }

            ushort length = BinaryPrimitives.ReadUInt16BigEndian(
                fontBytes.AsSpan(recordOffset + 8, sizeof(ushort)));
            ushort offset = BinaryPrimitives.ReadUInt16BigEndian(
                fontBytes.AsSpan(recordOffset + 10, sizeof(ushort)));
            int tableRelativeOffset = checked(stringOffset + offset);
            EnsureTableRange(name, tableRelativeOffset, length);
            ReadOnlySpan<byte> encoded = fontBytes.AsSpan(
                checked(name.Offset + tableRelativeOffset),
                length);
            string value = platformId == 1
                ? Encoding.Latin1.GetString(encoded)
                : Encoding.BigEndianUnicode.GetString(encoded);
            value = value.TrimEnd('\0').Trim();
            if (value.Length > 0)
            {
                values.Add(value);
            }
        }

        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SfntTable FindSfntTable(byte[] fontBytes, string expectedTag)
    {
        EnsureRange(fontBytes, 0, 12);
        ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(
            fontBytes.AsSpan(4, sizeof(ushort)));
        EnsureRange(fontBytes, 12, checked(tableCount * 16));

        for (int index = 0; index < tableCount; index++)
        {
            int recordOffset = checked(12 + (index * 16));
            string tag = Encoding.ASCII.GetString(fontBytes, recordOffset, 4);
            if (!string.Equals(tag, expectedTag, StringComparison.Ordinal))
            {
                continue;
            }

            uint offset = BinaryPrimitives.ReadUInt32BigEndian(
                fontBytes.AsSpan(recordOffset + 8, sizeof(uint)));
            uint length = BinaryPrimitives.ReadUInt32BigEndian(
                fontBytes.AsSpan(recordOffset + 12, sizeof(uint)));
            if (offset > int.MaxValue || length > int.MaxValue)
            {
                throw new InvalidDataException($"sfnt {expectedTag} table exceeds supported bounds.");
            }

            SfntTable table = new((int)offset, (int)length);
            EnsureRange(fontBytes, table.Offset, table.Length);
            return table;
        }

        throw new InvalidDataException($"The sfnt {expectedTag} table is missing.");
    }

    private static void EnsureTableRange(SfntTable table, int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > table.Length)
        {
            throw new InvalidDataException("The sfnt table contains an out-of-range record.");
        }
    }

    private static void EnsureRange(byte[] bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > bytes.LongLength)
        {
            throw new InvalidDataException("The sfnt file contains an out-of-range table.");
        }
    }

    private static void AssertTypographyRole(
        ResourceDictionary theme,
        string familyKey,
        string weightKey,
        bool bold)
    {
        object expectedFamily = theme[
            bold ? "InspectionFontFamilyBold" : "InspectionFontFamilyRegular"];
        object expectedWeight = theme[
            bold ? "InspectionFontWeightBold" : "InspectionFontWeightRegular"];
        Assert.AreSame(expectedFamily, theme[familyKey], familyKey);
        Assert.AreEqual(
            ((Windows.UI.Text.FontWeight)expectedWeight).Weight,
            ((Windows.UI.Text.FontWeight)theme[weightKey]).Weight,
            weightKey);
    }

    private static void AssertTokens(
        ResourceDictionary theme,
        IReadOnlyDictionary<string, double> expected)
    {
        foreach ((string key, double value) in expected)
        {
            Assert.AreEqual(value, (double)theme[key], 0.001, key);
        }
    }

    private sealed record PinnedAsset(
        string Path,
        long Length,
        string Sha256,
        string? Style = null,
        int Weight = 0);

    private readonly record struct SfntTable(int Offset, int Length);
}
