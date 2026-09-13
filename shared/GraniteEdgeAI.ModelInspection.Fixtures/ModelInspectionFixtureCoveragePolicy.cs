using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed record ModelInspectionFixtureCoveragePolicy(
    [property: JsonPropertyName("$schema")] string Schema,
    int SchemaVersion,
    IReadOnlyList<ModelInspectionFixturePolicyEntry> Fixtures,
    IReadOnlyList<ModelInspectionFixturePreset> Presets,
    IReadOnlyList<ModelInspectionFixtureDisclosurePair> DisclosurePairs,
    IReadOnlyList<ModelInspectionFixtureGallerySwitchPair> GallerySwitchPairs,
    IReadOnlyDictionary<string, string> CopyRegistry,
    IReadOnlyList<ModelInspectionFixtureExternalEvidenceLink> ExternalEvidenceLinks);

public sealed record ModelInspectionFixturePolicyEntry(
    string Id,
    string FileName,
    string TargetCondition,
    string? Variant,
    string? PairedWithId,
    ModelInspectionFixtureFigmaState CanonicalFigmaState,
    IReadOnlyList<string> RequiredCoverageTags,
    IReadOnlyList<ModelInspectionFixtureInteractionKind> RequiredInteractions,
    IReadOnlyList<string> RequiredPresets);

public sealed record ModelInspectionFixturePreset(
    string Id,
    ModelInspectionFixtureWidthProfile Width,
    ModelInspectionFixtureResourceProfile Resources,
    ModelInspectionFixtureTextProfile Text,
    ModelInspectionFixtureMotionProfile Motion);

public sealed record ModelInspectionFixtureDisclosurePair(
    string CollapsedId,
    string ExpandedId);

public sealed record ModelInspectionFixtureGallerySwitchPair(
    string SourceId,
    string DestinationId);

public sealed record ModelInspectionFixtureExternalEvidenceLink(
    string FixtureId,
    string EvidenceId,
    string SourcePath,
    string JourneyTest);

public sealed class ValidatedModelInspectionFixtureCoveragePolicy
{
    internal ValidatedModelInspectionFixtureCoveragePolicy(
        ModelInspectionFixtureCoveragePolicy value,
        ImmutableArray<byte> rawUtf8,
        string sha256)
    {
        Value = value;
        RawUtf8 = rawUtf8;
        Sha256 = sha256;
    }

    public int SchemaVersion => Value.SchemaVersion;

    public ModelInspectionFixtureCoveragePolicy Value { get; }

    public ImmutableArray<byte> RawUtf8 { get; }

    public string Sha256 { get; }
}
