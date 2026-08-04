using System.Text.Json;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Describes one generated GGUF fixture recorded by the repository manifest.
/// </summary>
public sealed record GgufFixtureEntry
{
    public required string FixtureId { get; init; }
    public required string FixtureFile { get; init; }
    public required long ByteLength { get; init; }
    public required string Sha256 { get; init; }
}

/// <summary>
/// Reads the generated fixture section used by Model Import and runtime hostile
/// input tests.
/// </summary>
public static class GgufFixtureManifest
{
    public static IReadOnlyList<GgufFixtureEntry> Load(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "GGUF fixture manifest was not found.",
                manifestPath);
        }

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));

        if (!document.RootElement.TryGetProperty(
                "generatedGgufFixtures",
                out JsonElement generated) ||
            generated.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Fixture manifest does not contain generatedGgufFixtures.");
        }

        var entries = new List<GgufFixtureEntry>();

        foreach (JsonElement item in generated.EnumerateArray())
        {
            entries.Add(new GgufFixtureEntry
            {
                FixtureId = RequireString(item, "fixtureId"),
                FixtureFile = RequireString(item, "fixtureFile"),
                ByteLength = RequireInt64(item, "byteLength"),
                Sha256 = RequireString(item, "sha256").ToLowerInvariant()
            });
        }

        return entries;
    }

    private static string RequireString(
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException(
                $"Fixture property '{propertyName}' is missing or invalid.");
        }

        return property.GetString()!;
    }

    private static long RequireInt64(
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement property) ||
            !property.TryGetInt64(out long value) ||
            value < 0)
        {
            throw new InvalidDataException(
                $"Fixture property '{propertyName}' is missing or invalid.");
        }

        return value;
    }
}
