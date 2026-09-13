using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

public sealed record OpenVinoOutputArtifact(
    string Path,
    long Length,
    string Sha256);

public sealed record OpenVinoProvenance(
    int SchemaVersion,
    Guid OperationId,
    Guid ValidationRunId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    string SourceManifestSha256,
    string AllowlistId,
    string PythonRuntimeSha256,
    string WheelLockSha256,
    string ConverterManifestSha256,
    IReadOnlyDictionary<string, string> ConverterVersions,
    IReadOnlyDictionary<string, object> ConversionOptions,
    IReadOnlyList<OpenVinoOutputArtifact> OutputFiles,
    string OutputManifestSha256,
    string ValidationDisposition,
    string SmokeDisposition)
{
    private const ulong MaximumArtifactBytes = 1UL << 40;
    private const int HashBufferBytes = 128 * 1024;
    public const string FileName = "granite-openvino-provenance.json";
    public const int CurrentSchemaVersion = 1;
    public const string DenseGraniteAllowlist =
        "dense-granite-causal-lm.text-generation-with-past.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, string> RequiredConverterVersions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        };

    internal static IReadOnlyList<OpenVinoOutputArtifact> CaptureOutput(
        string stagingDirectory,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(stagingDirectory);
        List<OpenVinoOutputArtifact> files = [];
        ulong total = 0;
        foreach (string path in EnumerateRegularFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative == FileName || relative.StartsWith("../", StringComparison.Ordinal) ||
                relative.Contains(':'))
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
            FileInfo file = new(path);
            if (file.Length <= 0)
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
            total = checked(total + (ulong)file.Length);
            if (total > MaximumArtifactBytes)
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
            files.Add(new OpenVinoOutputArtifact(
                relative,
                file.Length,
                HashFile(path, checked((ulong)file.Length), cancellationToken)));
        }
        files.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        if (files.Count == 0)
        {
            throw new InvalidDataException("conversion_output_invalid");
        }
        return files;
    }

    internal static string HashFile(
        string path,
        ulong maximumBytes,
        CancellationToken cancellationToken = default)
    {
        FileInfo file = new(path);
        if (!file.Exists
            || file.Length <= 0
            || checked((ulong)file.Length) > maximumBytes
            || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("conversion_output_invalid");
        }
        byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferBytes);
        try
        {
            using FileStream stream = new(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                HashBufferBytes,
                FileOptions.SequentialScan);
            using IncrementalHash hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
            ulong readTotal = 0;
            int read;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }
                readTotal = checked(readTotal + (ulong)read);
                if (readTotal > maximumBytes
                    || readTotal > checked((ulong)file.Length))
                {
                    throw new InvalidDataException("conversion_output_invalid");
                }
                hash.AppendData(buffer, 0, read);
            }
            if (readTotal != checked((ulong)file.Length))
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static IEnumerable<string> EnumerateRegularFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            DirectoryInfo directory = new(pending.Pop());
            if (!directory.Exists
                || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
            {
                entry.Refresh();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("conversion_output_invalid");
                }
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry.FullName);
                }
                else if (entry is FileInfo)
                {
                    yield return entry.FullName;
                }
                else
                {
                    throw new InvalidDataException("conversion_output_invalid");
                }
            }
        }
    }

    internal static string ComputeOutputManifestDigest(
        IEnumerable<OpenVinoOutputArtifact> files)
    {
        StringBuilder canonical = new();
        foreach (OpenVinoOutputArtifact file in files.OrderBy(
                     static item => item.Path, StringComparer.Ordinal))
        {
            canonical.Append(file.Path).Append('\0')
                .Append(file.Length).Append('\0')
                .Append(file.Sha256).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false, true).GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    internal void Write(string stagingDirectory)
    {
        Validate();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
        File.WriteAllBytes(Path.Combine(stagingDirectory, FileName), json);
    }

    internal void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || OperationId == Guid.Empty ||
            ValidationRunId == Guid.Empty || CompletedUtc < StartedUtc ||
            AllowlistId != DenseGraniteAllowlist ||
            ValidationDisposition != "passed" || SmokeDisposition != "passed" ||
            OutputFiles.Count == 0)
        {
            throw new InvalidDataException("conversion_output_invalid");
        }
        foreach (string digest in new[]
        {
            SourceManifestSha256, PythonRuntimeSha256, WheelLockSha256,
            ConverterManifestSha256, OutputManifestSha256
        })
        {
            if (digest.Length != 64 || digest.Any(static value =>
                    value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
        }
        if (ConverterVersions.Count != RequiredConverterVersions.Count ||
            RequiredConverterVersions.Any(required =>
                !ConverterVersions.TryGetValue(required.Key, out string? actual) ||
                actual != required.Value) ||
            ConversionOptions.Count != 5 ||
            !ConversionOptions.TryGetValue("library", out object? library) ||
            !Equals(library, "transformers") ||
            !ConversionOptions.TryGetValue("localFilesOnly", out object? localOnly) ||
            !Equals(localOnly, true) ||
            !ConversionOptions.TryGetValue("task", out object? task) ||
            !Equals(task, "text-generation-with-past") ||
            !ConversionOptions.TryGetValue("trustRemoteCode", out object? trustRemoteCode) ||
            !Equals(trustRemoteCode, false) ||
            !ConversionOptions.TryGetValue("weightFormat", out object? weightFormat) ||
            !Equals(weightFormat, "fp16"))
        {
            throw new InvalidDataException("conversion_output_invalid");
        }
        HashSet<string> outputNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (OpenVinoOutputArtifact artifact in OutputFiles)
        {
            if (string.IsNullOrWhiteSpace(artifact.Path) ||
                artifact.Path == FileName || artifact.Path.Contains('/') ||
                artifact.Path.Contains('\\') || artifact.Path.Contains(':') ||
                artifact.Length <= 0 || !outputNames.Add(artifact.Path))
            {
                throw new InvalidDataException("conversion_output_invalid");
            }
            _ = RequireDigest(artifact.Sha256);
        }
        if (ComputeOutputManifestDigest(OutputFiles) != OutputManifestSha256)
        {
            throw new InvalidDataException("conversion_output_invalid");
        }
    }

    internal static void ValidateJson(
        JsonElement root,
        OpenVinoPackageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string[] fields =
        [
            "schemaVersion", "operationId", "validationRunId", "startedUtc",
            "completedUtc", "sourceManifestSha256", "allowlistId",
            "pythonRuntimeSha256", "wheelLockSha256", "converterManifestSha256",
            "converterVersions", "conversionOptions", "outputFiles",
            "outputManifestSha256", "validationDisposition", "smokeDisposition"
        ];
        RequireExactObject(root, fields);
        JsonElement schema = root.GetProperty("schemaVersion");
        if (schema.ValueKind != JsonValueKind.Number ||
            !schema.TryGetInt32(out int schemaVersion) ||
            schemaVersion != CurrentSchemaVersion ||
            !TryCanonicalGuid(root.GetProperty("operationId"), out _) ||
            !TryCanonicalGuid(root.GetProperty("validationRunId"), out _) ||
            !TryTimestamp(root.GetProperty("startedUtc"), out DateTimeOffset started) ||
            !TryTimestamp(root.GetProperty("completedUtc"), out DateTimeOffset completed) ||
            completed < started ||
            !StringEquals(root.GetProperty("allowlistId"), DenseGraniteAllowlist) ||
            !StringEquals(root.GetProperty("validationDisposition"), "passed") ||
            !StringEquals(root.GetProperty("smokeDisposition"), "passed"))
        {
            throw new InvalidDataException("Conversion provenance is invalid.");
        }
        foreach (string name in new[]
        {
            "sourceManifestSha256", "pythonRuntimeSha256", "wheelLockSha256",
            "converterManifestSha256", "outputManifestSha256"
        })
        {
            RequireDigest(root.GetProperty(name));
        }

        JsonElement versions = root.GetProperty("converterVersions");
        string[] versionNames = RequiredConverterVersions.Keys.ToArray();
        RequireExactObject(versions, versionNames);
        foreach (string name in versionNames)
        {
            JsonElement value = versions.GetProperty(name);
            if (!StringEquals(value, RequiredConverterVersions[name]))
            {
                throw new InvalidDataException("Conversion provenance version is invalid.");
            }
        }

        JsonElement options = root.GetProperty("conversionOptions");
        RequireExactObject(options,
            ["library", "localFilesOnly", "task", "trustRemoteCode", "weightFormat"]);
        JsonElement localOnly = options.GetProperty("localFilesOnly");
        JsonElement trustRemoteCode = options.GetProperty("trustRemoteCode");
        if (!StringEquals(options.GetProperty("library"), "transformers") ||
            localOnly.ValueKind != JsonValueKind.True ||
            !StringEquals(options.GetProperty("task"), "text-generation-with-past") ||
            trustRemoteCode.ValueKind != JsonValueKind.False ||
            !StringEquals(options.GetProperty("weightFormat"), "fp16"))
        {
            throw new InvalidDataException("Conversion provenance options are invalid.");
        }

        JsonElement outputFiles = root.GetProperty("outputFiles");
        if (outputFiles.ValueKind != JsonValueKind.Array || outputFiles.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Conversion provenance output is invalid.");
        }
        List<OpenVinoOutputArtifact> artifacts = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement item in outputFiles.EnumerateArray())
        {
            RequireExactObject(item, ["path", "length", "sha256"]);
            JsonElement relativeValue = item.GetProperty("path");
            string relative = relativeValue.ValueKind == JsonValueKind.String
                ? relativeValue.GetString() ?? string.Empty
                : string.Empty;
            JsonElement lengthValue = item.GetProperty("length");
            if (lengthValue.ValueKind != JsonValueKind.Number ||
                !lengthValue.TryGetInt64(out long length))
            {
                throw new InvalidDataException("Conversion provenance output is invalid.");
            }
            string digest = RequireDigest(item.GetProperty("sha256"));
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains('/') ||
                relative.Contains('\\') || relative.Contains(':') || length <= 0 ||
                !names.Add(relative) ||
                !snapshot.TryGetEntry(relative, out OpenVinoPackageSnapshotEntry entry) ||
                entry.Length != length || entry.Sha256 != digest)
            {
                throw new InvalidDataException("Conversion provenance output is invalid.");
            }
            artifacts.Add(new OpenVinoOutputArtifact(relative, length, digest));
        }
        HashSet<string> actual = snapshot.Entries
            .Select(static entry => entry.RelativeName)
            .Where(static name => name != FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.SetEquals(actual) ||
            ComputeOutputManifestDigest(artifacts) !=
                root.GetProperty("outputManifestSha256").GetString())
        {
            throw new InvalidDataException("Conversion provenance manifest is invalid.");
        }
    }

    private static void RequireExactObject(JsonElement value, string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != fields.Length ||
            fields.Any(name => !value.TryGetProperty(name, out _)))
        {
            throw new InvalidDataException("Conversion provenance schema is invalid.");
        }
    }

    private static string RequireDigest(JsonElement element)
    {
        string? value = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        return RequireDigest(value);
    }

    private static string RequireDigest(string? value)
    {
        if (value is null || value.Length != 64 || value.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException("Conversion provenance digest is invalid.");
        }
        return value;
    }

    private static bool StringEquals(JsonElement value, string expected) =>
        value.ValueKind == JsonValueKind.String && value.GetString() == expected;

    private static bool TryCanonicalGuid(JsonElement value, out Guid result)
    {
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return Guid.TryParseExact(text, "D", out result) && result != Guid.Empty &&
            result.ToString() == text;
    }

    private static bool TryTimestamp(JsonElement value, out DateTimeOffset result)
    {
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out result);
    }
}
