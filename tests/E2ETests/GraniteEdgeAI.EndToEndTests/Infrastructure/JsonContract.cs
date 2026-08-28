using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal static class JsonContract
{
    internal static JsonDocument Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return JsonDocument.Parse(File.ReadAllBytes(Path.GetFullPath(path)), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException($"Unable to read strict JSON contract '{Path.GetFileName(path)}'.", error);
        }
    }

    internal static void RequireOnly(JsonElement element, params string[] allowed)
    {
        HashSet<string> names = new(allowed, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Contains(property.Name))
            {
                throw new InvalidDataException($"Unexpected property '{property.Name}'.");
            }
        }
    }

    internal static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Required string property '{name}' is missing.");
        }

        string? text = value.GetString();
        return !string.IsNullOrWhiteSpace(text) ? text : throw new InvalidDataException($"Property '{name}' must not be blank.");
    }

    internal static long RequiredInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || !value.TryGetInt64(out long number))
        {
            throw new InvalidDataException($"Required integer property '{name}' is missing.");
        }

        return number;
    }

    internal static void RequireSha256(string value, string name)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)) || value != value.ToLowerInvariant())
        {
            throw new InvalidDataException($"Property '{name}' must be a lowercase SHA-256 value.");
        }
    }
}
