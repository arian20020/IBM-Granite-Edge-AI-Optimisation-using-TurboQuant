#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;

internal sealed record ModelInspectionFixturePreset
{
    internal ModelInspectionFixturePreset(
        string id,
        ModelInspectionFixtureWidthProfile width,
        ModelInspectionFixtureResourceProfile resources,
        ModelInspectionFixtureTextProfile text,
        ModelInspectionFixtureMotionProfile motion)
    {
        if (!IsCanonicalIdentity(id, width, resources, text, motion))
        {
            throw new ArgumentException(
                "The fixture preset identity does not match the closed " +
                "coverage-policy matrix.",
                nameof(id));
        }

        Id = id;
        Width = width;
        Resources = resources;
        Text = text;
        Motion = motion;
    }

    internal string Id { get; }

    internal ModelInspectionFixtureWidthProfile Width { get; }

    internal ModelInspectionFixtureResourceProfile Resources { get; }

    internal ModelInspectionFixtureTextProfile Text { get; }

    internal ModelInspectionFixtureMotionProfile Motion { get; }

    internal static ModelInspectionFixturePreset Canonical { get; } = new(
        "P01",
        ModelInspectionFixtureWidthProfile.Desktop1440,
        ModelInspectionFixtureResourceProfile.Light,
        ModelInspectionFixtureTextProfile.Standard100,
        ModelInspectionFixtureMotionProfile.Normal);

    internal static ModelInspectionFixturePreset FromPolicy(
        GraniteEdgeAI.ModelInspection.Fixtures.ModelInspectionFixturePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return new ModelInspectionFixturePreset(
            preset.Id,
            preset.Width,
            preset.Resources,
            preset.Text,
            preset.Motion);
    }

    private static bool IsCanonicalIdentity(
        string id,
        ModelInspectionFixtureWidthProfile width,
        ModelInspectionFixtureResourceProfile resources,
        ModelInspectionFixtureTextProfile text,
        ModelInspectionFixtureMotionProfile motion) => id switch
        {
            "P01" => width == ModelInspectionFixtureWidthProfile.Desktop1440 &&
                resources == ModelInspectionFixtureResourceProfile.Light &&
                text == ModelInspectionFixtureTextProfile.Standard100 &&
                motion == ModelInspectionFixtureMotionProfile.Normal,
            "P02" => width == ModelInspectionFixtureWidthProfile.Desktop1440 &&
                resources == ModelInspectionFixtureResourceProfile.Dark &&
                text == ModelInspectionFixtureTextProfile.Preview200 &&
                motion == ModelInspectionFixtureMotionProfile.Reduced,
            "P03" => width == ModelInspectionFixtureWidthProfile.Desktop1440 &&
                resources == ModelInspectionFixtureResourceProfile.HighContrastPreview &&
                text == ModelInspectionFixtureTextProfile.Standard100 &&
                motion == ModelInspectionFixtureMotionProfile.Reduced,
            "P04" => width == ModelInspectionFixtureWidthProfile.Medium600 &&
                resources == ModelInspectionFixtureResourceProfile.Light &&
                text == ModelInspectionFixtureTextProfile.Preview200 &&
                motion == ModelInspectionFixtureMotionProfile.Normal,
            "P05" => width == ModelInspectionFixtureWidthProfile.Medium600 &&
                resources == ModelInspectionFixtureResourceProfile.Dark &&
                text == ModelInspectionFixtureTextProfile.Standard100 &&
                motion == ModelInspectionFixtureMotionProfile.Reduced,
            "P06" => width == ModelInspectionFixtureWidthProfile.Medium600 &&
                resources == ModelInspectionFixtureResourceProfile.HighContrastPreview &&
                text == ModelInspectionFixtureTextProfile.Preview200 &&
                motion == ModelInspectionFixtureMotionProfile.Reduced,
            "P07" => width == ModelInspectionFixtureWidthProfile.Narrow360 &&
                resources == ModelInspectionFixtureResourceProfile.Light &&
                text == ModelInspectionFixtureTextProfile.Standard100 &&
                motion == ModelInspectionFixtureMotionProfile.Reduced,
            "P08" => width == ModelInspectionFixtureWidthProfile.Narrow360 &&
                resources == ModelInspectionFixtureResourceProfile.Dark &&
                text == ModelInspectionFixtureTextProfile.Preview200 &&
                motion == ModelInspectionFixtureMotionProfile.Normal,
            "P09" => width == ModelInspectionFixtureWidthProfile.Narrow360 &&
                resources == ModelInspectionFixtureResourceProfile.HighContrastPreview &&
                text == ModelInspectionFixtureTextProfile.Standard100 &&
                motion == ModelInspectionFixtureMotionProfile.Normal,
            _ => false
        };
}
#endif
