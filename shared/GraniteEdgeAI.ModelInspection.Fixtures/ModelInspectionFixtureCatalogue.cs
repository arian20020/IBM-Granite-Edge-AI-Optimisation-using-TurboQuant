using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed class ModelInspectionFixtureCatalogue
{
    private const int SupportedSchemaVersion = 1;

    private ModelInspectionFixtureCatalogue(
        IReadOnlyList<ValidatedModelInspectionFixture> fixtures,
        ValidatedModelInspectionFixtureCoveragePolicy policy,
        VerifiedModelInspectionFixtureSchema schema,
        ModelInspectionFixtureCatalogueIndex index)
    {
        Fixtures = fixtures;
        Policy = policy;
        Schema = schema;
        Index = index;
    }

    public IReadOnlyList<ValidatedModelInspectionFixture> Fixtures { get; }

    public ValidatedModelInspectionFixtureCoveragePolicy Policy { get; }

    public VerifiedModelInspectionFixtureSchema Schema { get; }

    public ModelInspectionFixtureCatalogueIndex Index { get; }

    public static VerifiedModelInspectionFixtureSchema VerifySchema(
        ModelInspectionFixtureDocumentSource schemaSource)
    {
        ArgumentNullException.ThrowIfNull(schemaSource);
        DocumentSnapshot snapshot = Snapshot(schemaSource);
        schemaSource = snapshot.Source;
        if (!schemaSource.FileName.Equals(
                "model-inspection-fixture.schema.json",
                StringComparison.Ordinal))
        {
            throw StrictModelInspectionFixtureJson.Failure(
                schemaSource,
                "$",
                "schema.filename");
        }

        using JsonDocument document =
            StrictModelInspectionFixtureJson.ParseDocument(schemaSource);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetExactString(root, "$schema", out string? schemaIdentity) ||
            !schemaIdentity.Equals(
                "https://json-schema.org/draft/2020-12/schema",
                StringComparison.Ordinal) ||
            !TryGetExactString(root, "$id", out string? id) ||
            !id.Equals("model-inspection-fixture.schema.json", StringComparison.Ordinal) ||
            !TryGetExactString(root, "title", out string? title) ||
            string.IsNullOrWhiteSpace(title) ||
            !TryGetExactString(root, "type", out string? type) ||
            !type.Equals("object", StringComparison.Ordinal) ||
            !root.TryGetProperty("schemaVersion", out JsonElement versionElement) ||
            !versionElement.TryGetInt32(out int version) ||
            version != SupportedSchemaVersion ||
            !root.TryGetProperty("additionalProperties", out JsonElement additionalProperties) ||
            additionalProperties.ValueKind != JsonValueKind.False)
        {
            throw StrictModelInspectionFixtureJson.Failure(
                schemaSource,
                "$",
                "schema.contract");
        }

        return new VerifiedModelInspectionFixtureSchema(
            schemaSource.FileName,
            version,
            snapshot.Bytes,
            Hash(snapshot.Bytes.AsSpan()));
    }

    public static ValidatedModelInspectionFixtureCoveragePolicy LoadPolicy(
        ModelInspectionFixtureDocumentSource policySource,
        VerifiedModelInspectionFixtureSchema schema)
    {
        ArgumentNullException.ThrowIfNull(policySource);
        ArgumentNullException.ThrowIfNull(schema);
        DocumentSnapshot snapshot = Snapshot(policySource);
        policySource = snapshot.Source;
        ModelInspectionFixtureCoveragePolicy parsed =
            StrictModelInspectionFixtureJson.Deserialize<ModelInspectionFixtureCoveragePolicy>(policySource);
        ModelInspectionFixtureCoveragePolicy validated =
            ModelInspectionFixtureValidator.ValidatePolicy(policySource, parsed, schema);
        return new ValidatedModelInspectionFixtureCoveragePolicy(
            validated,
            snapshot.Bytes,
            Hash(snapshot.Bytes.AsSpan()));
    }

    public static ModelInspectionFixtureCatalogue LoadDescriptors(
        IReadOnlyList<ModelInspectionFixtureDocumentSource> descriptorSources,
        ValidatedModelInspectionFixtureCoveragePolicy policy,
        VerifiedModelInspectionFixtureSchema schema)
    {
        ArgumentNullException.ThrowIfNull(descriptorSources);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(schema);
        if (descriptorSources.Count > StrictModelInspectionFixtureJson.MaximumCollectionLength)
        {
            throw StrictModelInspectionFixtureJson.Failure(
                new ModelInspectionFixtureDocumentSource("<catalogue>", ReadOnlyMemory<byte>.Empty),
                "$",
                "catalogue.document-limit");
        }

        List<ValidatedModelInspectionFixture> validated = new(descriptorSources.Count);
        foreach (ModelInspectionFixtureDocumentSource source in descriptorSources)
        {
            if (source is null)
            {
                throw new ModelInspectionFixtureValidationException(
                    "<invalid-filename>",
                    "$",
                    "catalogue.null-source");
            }

            DocumentSnapshot snapshot = Snapshot(source);
            ModelInspectionFixtureDescriptor parsed =
                StrictModelInspectionFixtureJson.Deserialize<ModelInspectionFixtureDescriptor>(snapshot.Source);
            ModelInspectionFixtureDescriptor frozen =
                ModelInspectionFixtureValidator.ValidateDescriptor(snapshot.Source, parsed, policy, schema);
            validated.Add(new ValidatedModelInspectionFixture(
                snapshot.Source.FileName,
                frozen,
                snapshot.Bytes,
                Hash(snapshot.Bytes.AsSpan())));
        }

        ModelInspectionFixtureDocumentSource diagnosticSource = descriptorSources.Count > 0
            ? descriptorSources[0]
            : new ModelInspectionFixtureDocumentSource(
                "model-inspection-fixture-coverage-policy.json",
                policy.RawUtf8.AsMemory());
        ModelInspectionFixtureValidator.ValidateCatalogue(validated, policy, diagnosticSource);

        ImmutableArray<ValidatedModelInspectionFixture> fixtures = validated
            .OrderBy(fixture => fixture.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        ImmutableDictionary<string, ModelInspectionFixtureCatalogueIndexEntry> byId = fixtures
            .ToImmutableDictionary(
                fixture => fixture.Id,
                fixture => new ModelInspectionFixtureCatalogueIndexEntry(
                    fixture.Id,
                    fixture.FileName,
                    fixture.Sha256),
                StringComparer.Ordinal);
        ImmutableDictionary<string, ModelInspectionFixtureCatalogueIndexEntry> byFileName = byId.Values
            .ToImmutableDictionary(entry => entry.FileName, StringComparer.Ordinal);
        return new ModelInspectionFixtureCatalogue(
            fixtures,
            policy,
            schema,
            new ModelInspectionFixtureCatalogueIndex(byId, byFileName));
    }

    public static ValidatedModelInspectionFixture RevalidateDescriptor(
        ModelInspectionFixtureDocumentSource descriptorSource,
        ValidatedModelInspectionFixtureCoveragePolicy policy,
        VerifiedModelInspectionFixtureSchema schema,
        ModelInspectionFixtureCatalogueIndex catalogueIndex)
    {
        ArgumentNullException.ThrowIfNull(descriptorSource);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(catalogueIndex);
        DocumentSnapshot snapshot = Snapshot(descriptorSource);
        descriptorSource = snapshot.Source;
        if (!catalogueIndex.ByFileName.TryGetValue(
                descriptorSource.FileName,
                out ModelInspectionFixtureCatalogueIndexEntry? indexed))
        {
            throw StrictModelInspectionFixtureJson.Failure(
                descriptorSource,
                "$",
                "revalidation.not-indexed");
        }

        ModelInspectionFixtureDescriptor parsed =
            StrictModelInspectionFixtureJson.Deserialize<ModelInspectionFixtureDescriptor>(descriptorSource);
        ModelInspectionFixtureDescriptor frozen =
            ModelInspectionFixtureValidator.ValidateDescriptor(
                descriptorSource,
                parsed,
                policy,
                schema);
        string sha256 = Hash(snapshot.Bytes.AsSpan());
        if (!indexed.Id.Equals(frozen.Id, StringComparison.Ordinal) ||
            !indexed.Sha256.Equals(sha256, StringComparison.Ordinal))
        {
            throw StrictModelInspectionFixtureJson.Failure(
                descriptorSource,
                "$",
                "revalidation.content-mismatch");
        }

        return new ValidatedModelInspectionFixture(
            descriptorSource.FileName,
            frozen,
            snapshot.Bytes,
            sha256);
    }

    private static bool TryGetExactString(
        JsonElement root,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        return root.TryGetProperty(propertyName, out JsonElement element) &&
               element.ValueKind == JsonValueKind.String &&
               (value = element.GetString() ?? string.Empty).Length > 0;
    }

    private static DocumentSnapshot Snapshot(
        ModelInspectionFixtureDocumentSource source)
    {
        byte[] copy = source.Utf8Json.ToArray();
        ImmutableArray<byte> bytes = ImmutableArray.CreateRange(copy);
        return new DocumentSnapshot(
            new ModelInspectionFixtureDocumentSource(source.FileName, bytes.AsMemory()),
            bytes);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private readonly record struct DocumentSnapshot(
        ModelInspectionFixtureDocumentSource Source,
        ImmutableArray<byte> Bytes);
}
