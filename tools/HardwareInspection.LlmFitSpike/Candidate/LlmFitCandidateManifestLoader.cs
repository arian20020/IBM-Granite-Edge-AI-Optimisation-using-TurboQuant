using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace HardwareInspection.LlmFitSpike.Candidate;

public static class LlmFitCandidateManifestLoader
{
    public static LlmFitCandidateManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    public static LlmFitCandidateManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return ParseManifest(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The candidate manifest is not valid JSON.", exception);
        }
    }

    private static LlmFitCandidateManifest ParseManifest(JsonElement root)
    {
        EnsureObject(root, "manifest");
        EnsureOnlyProperties(
            root,
            "schemaVersion",
            "candidateId",
            "version",
            "releaseTag",
            "releaseCommit",
            "publishedAtUtc",
            "archive",
            "executable",
            "requiredFiles",
            "commands",
            "license");

        string releaseCommit = GetRequiredString(root, "releaseCommit");
        ValidateLowercaseHex(releaseCommit, 40, "releaseCommit");

        return new LlmFitCandidateManifest(
            GetRequiredString(root, "schemaVersion"),
            GetRequiredString(root, "candidateId"),
            GetRequiredString(root, "version"),
            GetRequiredString(root, "releaseTag"),
            releaseCommit,
            GetRequiredUtcTimestamp(root, "publishedAtUtc"),
            ParseArchive(GetRequiredProperty(root, "archive")),
            ParseExecutable(GetRequiredProperty(root, "executable")),
            GetRequiredStringArray(root, "requiredFiles", requireDistinctFileNames: true),
            ParseCommands(GetRequiredProperty(root, "commands")),
            ParseLicense(GetRequiredProperty(root, "license")));
    }

    private static LlmFitCandidateArchive ParseArchive(JsonElement archive)
    {
        EnsureObject(archive, "archive");
        EnsureOnlyProperties(archive, "fileName", "downloadUri", "lengthBytes", "sha256");

        string downloadUri = GetRequiredString(archive, "downloadUri");
        if (!Uri.TryCreate(downloadUri, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("archive.downloadUri must be an HTTPS URI hosted by github.com.");
        }

        long lengthBytes = GetRequiredInt64(archive, "lengthBytes");
        if (lengthBytes <= 0)
        {
            throw new InvalidDataException("archive.lengthBytes must be positive.");
        }

        string sha256 = GetRequiredString(archive, "sha256");
        ValidateLowercaseHex(sha256, 64, "archive.sha256");

        string fileName = GetRequiredString(archive, "fileName");
        ValidateRelativePath(fileName, "archive.fileName");

        return new LlmFitCandidateArchive(fileName, uri, lengthBytes, sha256);
    }

    private static LlmFitCandidateExecutable ParseExecutable(JsonElement executable)
    {
        EnsureObject(executable, "executable");
        EnsureOnlyProperties(executable, "relativePath", "sha256", "peMachine", "authenticodePolicy");

        string relativePath = GetRequiredString(executable, "relativePath");
        ValidateRelativePath(relativePath, "executable.relativePath");

        string sha256 = GetRequiredString(executable, "sha256");
        ValidateLowercaseHex(sha256, 64, "executable.sha256");

        return new LlmFitCandidateExecutable(
            relativePath,
            sha256,
            GetRequiredString(executable, "peMachine"),
            GetRequiredString(executable, "authenticodePolicy"));
    }

    private static LlmFitCandidateCommands ParseCommands(JsonElement commands)
    {
        EnsureObject(commands, "commands");
        EnsureOnlyProperties(commands, "version", "system");

        IReadOnlyList<string> version = GetRequiredStringArray(commands, "version", requireDistinctFileNames: false);
        IReadOnlyList<string> system = GetRequiredStringArray(commands, "system", requireDistinctFileNames: false);

        if (!version.SequenceEqual(["--version"], StringComparer.Ordinal) ||
            !system.SequenceEqual(["--no-dashboard", "--json", "system"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("commands must contain only the approved read-only invocations.");
        }

        return new LlmFitCandidateCommands(version, system);
    }

    private static LlmFitCandidateLicense ParseLicense(JsonElement license)
    {
        EnsureObject(license, "license");
        EnsureOnlyProperties(license, "spdx", "relativePath");

        string relativePath = GetRequiredString(license, "relativePath");
        ValidateRelativePath(relativePath, "license.relativePath");

        return new LlmFitCandidateLicense(GetRequiredString(license, "spdx"), relativePath);
    }

    private static DateTimeOffset GetRequiredUtcTimestamp(JsonElement objectElement, string propertyName)
    {
        string value = GetRequiredString(objectElement, propertyName);
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp) ||
            timestamp.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException($"{propertyName} must be a UTC timestamp.");
        }

        return timestamp;
    }

    private static JsonElement GetRequiredProperty(JsonElement objectElement, string propertyName)
    {
        if (!objectElement.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidDataException($"{propertyName} is required.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement objectElement, string propertyName)
    {
        JsonElement property = GetRequiredProperty(objectElement, propertyName);
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"{propertyName} must be a non-empty string.");
        }

        return property.GetString()!;
    }

    private static long GetRequiredInt64(JsonElement objectElement, string propertyName)
    {
        JsonElement property = GetRequiredProperty(objectElement, propertyName);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out long value))
        {
            throw new InvalidDataException($"{propertyName} must be an integer.");
        }

        return value;
    }

    private static ReadOnlyCollection<string> GetRequiredStringArray(
        JsonElement objectElement,
        string propertyName,
        bool requireDistinctFileNames)
    {
        JsonElement property = GetRequiredProperty(objectElement, propertyName);
        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() == 0)
        {
            throw new InvalidDataException($"{propertyName} must be a non-empty array.");
        }

        var values = new List<string>(property.GetArrayLength());
        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new InvalidDataException($"{propertyName} must contain non-empty strings.");
            }

            string value = item.GetString()!;
            if (requireDistinctFileNames)
            {
                ValidateRelativePath(value, $"{propertyName} entry");
            }

            values.Add(value);
        }

        if (requireDistinctFileNames && values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
        {
            throw new InvalidDataException($"{propertyName} must not contain duplicate files.");
        }

        return values.AsReadOnly();
    }

    private static void EnsureObject(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{name} must be an object.");
        }
    }

    private static void EnsureOnlyProperties(JsonElement objectElement, params string[] allowedProperties)
    {
        var allowed = new HashSet<string>(allowedProperties, StringComparer.Ordinal);
        foreach (JsonProperty property in objectElement.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new InvalidDataException($"Unknown manifest property: {property.Name}.");
            }
        }
    }

    private static void ValidateLowercaseHex(string value, int length, string propertyName)
    {
        if (value.Length != length || value.Any(character => !((character is >= '0' and <= '9') || (character is >= 'a' and <= 'f'))))
        {
            throw new InvalidDataException($"{propertyName} must be a {length}-character lowercase hexadecimal value.");
        }
    }

    private static void ValidateRelativePath(string path, string propertyName)
    {
        if (Path.IsPathRooted(path) || path.Contains(':', StringComparison.Ordinal) ||
            path.Split(['/', '\\'], StringSplitOptions.None).Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"{propertyName} must be a non-traversing relative path.");
        }
    }
}
