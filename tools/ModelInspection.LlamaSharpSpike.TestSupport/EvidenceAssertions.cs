using System.Text.Json;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Provides framework-neutral assertions shared by hosted and trusted
/// integration tests. Methods throw ordinary exceptions rather than depending
/// on MSTest.
/// </summary>
public static class EvidenceAssertions
{
    public static JsonDocument LoadJson(string path)
    {
        AssertFileExists(path);

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Evidence JSON is invalid: {path}",
                exception);
        }
    }

    public static void AssertFileExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Expected evidence file was not created.",
                path);
        }
    }

    public static void AssertDoesNotContainCanonicalPath(
        string value,
        string canonicalPath)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (value.Contains(canonicalPath, comparison))
        {
            throw new InvalidDataException(
                "Output exposed the canonical model path.");
        }
    }

    public static void AssertNoGgufFiles(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Evidence directory does not exist: {directory}");
        }

        string[] ggufFiles = Directory.GetFiles(
            directory,
            "*.gguf",
            SearchOption.AllDirectories);

        if (ggufFiles.Length > 0)
        {
            throw new InvalidDataException(
                "Evidence directory contains GGUF files: " +
                string.Join(", ", ggufFiles.Select(Path.GetFileName)));
        }
    }

    public static string RequireString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is missing or not a string.");
        }

        return property.GetString() ?? string.Empty;
    }

    public static bool RequireBoolean(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind is not
                (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is missing or not Boolean.");
        }

        return property.GetBoolean();
    }
}
