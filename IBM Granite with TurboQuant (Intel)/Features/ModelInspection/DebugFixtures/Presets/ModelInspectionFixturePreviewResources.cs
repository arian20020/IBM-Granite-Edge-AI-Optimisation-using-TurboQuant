#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;

internal static class ModelInspectionFixturePreviewResources
{
    private const string ThemeSourceSuffix =
        "/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml";

    private static readonly (string Key, double StandardValue)[] Typography =
    [
        ("InspectionPageTitleFontSize", 32d),
        ("InspectionSectionTitleFontSize", 18d),
        ("InspectionBodyFontSize", 14d),
        ("InspectionHelperFontSize", 12d),
        ("InspectionLabelFontSize", 10d)
    ];

    internal static void Configure(
        ResourceDictionary resources,
        ModelInspectionFixturePreset preset)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(preset);

        double scale = preset.Text switch
        {
            ModelInspectionFixtureTextProfile.Standard100 => 1d,
            ModelInspectionFixtureTextProfile.Preview200 => 2d,
            _ => throw new ArgumentOutOfRangeException(
                nameof(preset), preset.Text, "Unknown fixture text profile.")
        };

        foreach ((string key, double standardValue) in Typography)
        {
            resources[key] = standardValue * scale;
        }

        if (preset.Resources ==
            ModelInspectionFixtureResourceProfile.HighContrastPreview)
        {
            MaterializeHighContrastPreview(resources);
        }
    }

    internal static string ResourceLabel(
        ModelInspectionFixtureResourceProfile profile) => profile switch
        {
            ModelInspectionFixtureResourceProfile.Light => "Light",
            ModelInspectionFixtureResourceProfile.Dark => "Dark",
            ModelInspectionFixtureResourceProfile.HighContrastPreview =>
                "High Contrast Preview",
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile), profile, "Unknown fixture resource profile.")
        };

    internal static string TextLabel(
        ModelInspectionFixtureTextProfile profile) => profile switch
        {
            ModelInspectionFixtureTextProfile.Standard100 => "100%",
            ModelInspectionFixtureTextProfile.Preview200 => "200% Preview",
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile), profile, "Unknown fixture text profile.")
        };

    internal static ElementTheme RequestedTheme(
        ModelInspectionFixtureResourceProfile profile) => profile switch
        {
            ModelInspectionFixtureResourceProfile.Dark => ElementTheme.Dark,
            ModelInspectionFixtureResourceProfile.Light or
            ModelInspectionFixtureResourceProfile.HighContrastPreview =>
                ElementTheme.Light,
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile), profile, "Unknown fixture resource profile.")
        };

    private static void MaterializeHighContrastPreview(
        ResourceDictionary destination)
    {
        ResourceDictionary source = HighContrastDictionary();
        foreach (object key in source.Keys)
        {
            if (source[key] is not SolidColorBrush brush)
            {
                throw new InvalidOperationException(
                    $"The High Contrast preview resource '{key}' is not a " +
                    "resolved solid-color brush.");
            }

            destination[key] = new SolidColorBrush(brush.Color);
        }
    }

    private static ResourceDictionary HighContrastDictionary()
    {
        Application application = Application.Current ??
            throw new InvalidOperationException(
                "High Contrast preview resources require an active application.");
        foreach (ResourceDictionary dictionary in
                 application.Resources.MergedDictionaries)
        {
            if (dictionary.Source?.OriginalString.EndsWith(
                    ThemeSourceSuffix,
                    StringComparison.OrdinalIgnoreCase) == true &&
                dictionary.ThemeDictionaries["HighContrast"] is
                    ResourceDictionary highContrast)
            {
                return highContrast;
            }
        }

        throw new InvalidOperationException(
            "The Model Inspection High Contrast resource dictionary was not found.");
    }
}
#endif
