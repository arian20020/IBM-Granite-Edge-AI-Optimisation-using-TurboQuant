#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;

internal sealed record ModelInspectionFixturePreset(
    ModelInspectionFixtureWidthProfile Width,
    ModelInspectionFixtureResourceProfile Resources,
    ModelInspectionFixtureTextProfile Text,
    ModelInspectionFixtureMotionProfile Motion)
{
    internal static ModelInspectionFixturePreset Canonical { get; } = new(
        ModelInspectionFixtureWidthProfile.Desktop1440,
        ModelInspectionFixtureResourceProfile.Light,
        ModelInspectionFixtureTextProfile.Standard100,
        ModelInspectionFixtureMotionProfile.Normal);
}
#endif
