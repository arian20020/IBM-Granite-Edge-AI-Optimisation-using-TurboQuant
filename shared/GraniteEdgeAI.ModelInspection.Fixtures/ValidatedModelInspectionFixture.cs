using System.Collections.Immutable;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed class ValidatedModelInspectionFixtureInput
{
    internal ValidatedModelInspectionFixtureInput(ModelInspectionFixtureInput value)
    {
        Request = value.Request;
        Attempts = value.Attempts;
        SetupSteps = value.SetupSteps;
        ObservationCheckpoint = value.ObservationCheckpoint;
    }

    public ModelInspectionFixtureRequestDescriptor Request { get; }

    public IReadOnlyList<ModelInspectionFixtureAttemptDescriptor> Attempts { get; }

    public IReadOnlyList<ModelInspectionFixtureSetupStepDescriptor> SetupSteps { get; }

    public string ObservationCheckpoint { get; }
}

public sealed class ValidatedModelInspectionFixture
{
    internal ValidatedModelInspectionFixture(
        string fileName,
        ModelInspectionFixtureDescriptor descriptor,
        ImmutableArray<byte> rawUtf8,
        string sha256)
    {
        FileName = fileName;
        Schema = descriptor.Schema;
        SchemaVersion = descriptor.SchemaVersion;
        Id = descriptor.Id;
        TargetCondition = descriptor.TargetCondition;
        Variant = descriptor.Variant;
        Title = descriptor.Title;
        Category = descriptor.Category;
        Coverage = descriptor.Coverage;
        Input = new ValidatedModelInspectionFixtureInput(descriptor.Input);
        Expected = descriptor.Expected;
        PresetExpectations = descriptor.PresetExpectations;
        Interactions = descriptor.Interactions;
        VisibleInteractions = descriptor.Interactions
            .Where(interaction => interaction.SourceCheckpoint.Equals(
                descriptor.Input.ObservationCheckpoint,
                StringComparison.Ordinal))
            .ToImmutableArray();
        Presets = descriptor.Presets;
        RawUtf8 = rawUtf8;
        Sha256 = sha256;
    }

    public string FileName { get; }

    public string Schema { get; }

    public int SchemaVersion { get; }

    public string Id { get; }

    public string TargetCondition { get; }

    public string? Variant { get; }

    public string Title { get; }

    public ModelInspectionFixtureCategory Category { get; }

    public ModelInspectionFixtureCoverage Coverage { get; }

    public ValidatedModelInspectionFixtureInput Input { get; }

    public ModelInspectionExpectedScreen Expected { get; }

    public IReadOnlyDictionary<string, ModelInspectionPresetExpectation> PresetExpectations { get; }

    public IReadOnlyList<ModelInspectionFixtureInteraction> Interactions { get; }

    public IReadOnlyList<ModelInspectionFixtureInteraction> VisibleInteractions { get; }

    public IReadOnlyList<string> Presets { get; }

    public ImmutableArray<byte> RawUtf8 { get; }

    public string Sha256 { get; }
}

public sealed record ModelInspectionFixtureCatalogueIndexEntry(
    string Id,
    string FileName,
    string Sha256);

public sealed class ModelInspectionFixtureCatalogueIndex
{
    internal ModelInspectionFixtureCatalogueIndex(
        IReadOnlyDictionary<string, ModelInspectionFixtureCatalogueIndexEntry> byId,
        IReadOnlyDictionary<string, ModelInspectionFixtureCatalogueIndexEntry> byFileName)
    {
        ById = byId;
        ByFileName = byFileName;
    }

    public IReadOnlyDictionary<string, ModelInspectionFixtureCatalogueIndexEntry> ById { get; }

    public IReadOnlyDictionary<string, ModelInspectionFixtureCatalogueIndexEntry> ByFileName { get; }
}
