using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

public sealed record OpenVinoOptimizationProvenance(
    int SchemaVersion,
    Guid OperationId,
    Guid ValidationRunId,
    string SourceManifestSha256,
    string ConfigurationId,
    OpenVinoWeightPrecision SourceWeightPrecision,
    OpenVinoWeightPrecision TargetWeightPrecision,
    OpenVinoKvCachePrecision RequestedKvCachePrecision,
    string ActualDevice,
    OpenVinoKvCachePrecision ActualKvCachePrecision,
    IReadOnlyDictionary<string, string> OptimizerVersions,
    IReadOnlyList<OpenVinoOutputArtifact> OutputFiles,
    string OutputManifestSha256,
    string ValidationDisposition,
    string GenerationDisposition,
    string QualityDisposition)
{
    public const string FileName = "granite-openvino-optimization.json";
    public const int CurrentSchemaVersion = 1;
    private static readonly IReadOnlyDictionary<string, string> RequiredVersions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static OpenVinoOptimizationProvenance() =>
        JsonOptions.Converters.Add(new JsonStringEnumConverter());

    internal void Write(string stagingDirectory)
    {
        Validate();
        File.WriteAllBytes(
            Path.Combine(stagingDirectory, FileName),
            JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions));
    }

    internal void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || OperationId == Guid.Empty ||
            ValidationRunId == Guid.Empty ||
            !OpenVinoOptimizationRegistry.Candidates.Any(candidate =>
                candidate.ConfigurationId == ConfigurationId &&
                candidate.PersistentArtifact.WeightPrecision == TargetWeightPrecision &&
                candidate.Runtime.KvCachePrecision == RequestedKvCachePrecision) ||
            ActualDevice != "CPU" || ActualKvCachePrecision != RequestedKvCachePrecision ||
            ValidationDisposition != "passed" || GenerationDisposition != "passed" ||
            QualityDisposition != "passed" || OutputFiles.Count == 0)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        RequireDigest(SourceManifestSha256);
        RequireDigest(OutputManifestSha256);
        if (TargetWeightPrecision == OpenVinoWeightPrecision.Fp16 &&
            SourceWeightPrecision != OpenVinoWeightPrecision.Fp16)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (OpenVinoOutputArtifact file in OutputFiles)
        {
            if (string.IsNullOrWhiteSpace(file.Path) || file.Path == FileName ||
                file.Path.Contains('/') || file.Path.Contains('\\') || file.Path.Contains(':') ||
                file.Length <= 0 || !names.Add(file.Path))
            {
                throw new InvalidDataException("optimization_output_invalid");
            }
            RequireDigest(file.Sha256);
        }
        if (OpenVinoProvenance.ComputeOutputManifestDigest(OutputFiles) != OutputManifestSha256)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        if (OptimizerVersions.Count != RequiredVersions.Count ||
            RequiredVersions.Any(required =>
                !OptimizerVersions.TryGetValue(required.Key, out string? value) ||
                value != required.Value))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    internal static OpenVinoWeightPrecision ReadSourcePrecision(
        OpenVinoPackageSnapshot snapshot)
    {
        if (!snapshot.TryGetEntry(FileName, out OpenVinoPackageSnapshotEntry entry))
        {
            return OpenVinoWeightPrecision.Fp16;
        }
        entry.Stream.Position = 0;
        using JsonDocument document = JsonDocument.Parse(entry.Stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16
        });
        entry.Stream.Position = 0;
        JsonElement value = document.RootElement.GetProperty("targetWeightPrecision");
        if (value.ValueKind != JsonValueKind.String ||
            !Enum.TryParse(value.GetString(), ignoreCase: false,
                out OpenVinoWeightPrecision precision))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        return precision;
    }

    internal static void ValidateJson(JsonElement root, OpenVinoPackageSnapshot snapshot)
    {
        string[] fields =
        [
            "schemaVersion", "operationId", "validationRunId", "sourceManifestSha256",
            "configurationId", "sourceWeightPrecision", "targetWeightPrecision",
            "requestedKvCachePrecision", "actualDevice", "actualKvCachePrecision",
            "optimizerVersions", "outputFiles", "outputManifestSha256",
            "validationDisposition", "generationDisposition", "qualityDisposition"
        ];
        RequireExactObject(root, fields);
        JsonElement schemaValue = root.GetProperty("schemaVersion");
        if (schemaValue.ValueKind != JsonValueKind.Number ||
            !schemaValue.TryGetInt32(out int schema) ||
            schema != CurrentSchemaVersion ||
            !Guid.TryParseExact(RequireString(root.GetProperty("operationId")), "D", out Guid operation) ||
            operation == Guid.Empty ||
            !Guid.TryParseExact(RequireString(root.GetProperty("validationRunId")), "D", out Guid validation) ||
            validation == Guid.Empty ||
            RequireString(root.GetProperty("actualDevice")) != "CPU" ||
            RequireString(root.GetProperty("validationDisposition")) != "passed" ||
            RequireString(root.GetProperty("generationDisposition")) != "passed" ||
            RequireString(root.GetProperty("qualityDisposition")) != "passed")
        {
            throw new InvalidDataException("Optimization provenance is invalid.");
        }
        string sourceDigest = RequireDigest(RequireString(root.GetProperty("sourceManifestSha256")));
        _ = sourceDigest;
        string outputDigest = RequireDigest(RequireString(root.GetProperty("outputManifestSha256")));
        OpenVinoWeightPrecision sourcePrecision = ParseEnum<OpenVinoWeightPrecision>(
            root.GetProperty("sourceWeightPrecision"));
        OpenVinoWeightPrecision targetPrecision = ParseEnum<OpenVinoWeightPrecision>(
            root.GetProperty("targetWeightPrecision"));
        OpenVinoKvCachePrecision requestedKv = ParseEnum<OpenVinoKvCachePrecision>(
            root.GetProperty("requestedKvCachePrecision"));
        OpenVinoKvCachePrecision actualKv = ParseEnum<OpenVinoKvCachePrecision>(
            root.GetProperty("actualKvCachePrecision"));
        string configurationId = RequireString(root.GetProperty("configurationId"));
        if (requestedKv != actualKv ||
            targetPrecision == OpenVinoWeightPrecision.Fp16 &&
            sourcePrecision != OpenVinoWeightPrecision.Fp16 ||
            !OpenVinoOptimizationRegistry.Candidates.Any(candidate =>
                candidate.ConfigurationId == configurationId &&
                candidate.PersistentArtifact.WeightPrecision == targetPrecision &&
                candidate.Runtime.KvCachePrecision == requestedKv))
        {
            throw new InvalidDataException("Optimization provenance candidate is invalid.");
        }

        JsonElement versions = root.GetProperty("optimizerVersions");
        string[] versionNames = RequiredVersions.Keys.ToArray();
        RequireExactObject(versions, versionNames);
        foreach ((string name, string expected) in RequiredVersions)
        {
            if (RequireString(versions.GetProperty(name)) != expected)
            {
                throw new InvalidDataException("Optimization provenance version is invalid.");
            }
        }

        JsonElement files = root.GetProperty("outputFiles");
        if (files.ValueKind != JsonValueKind.Array || files.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Optimization provenance output is invalid.");
        }
        List<OpenVinoOutputArtifact> artifacts = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement file in files.EnumerateArray())
        {
            RequireExactObject(file, ["path", "length", "sha256"]);
            string name = RequireString(file.GetProperty("path"));
            JsonElement lengthValue = file.GetProperty("length");
            if (lengthValue.ValueKind != JsonValueKind.Number ||
                !lengthValue.TryGetInt64(out long length) || length <= 0 ||
                string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') ||
                name.Contains(':') || !names.Add(name) ||
                !snapshot.TryGetEntry(name, out OpenVinoPackageSnapshotEntry entry) ||
                entry.Length != length)
            {
                throw new InvalidDataException("Optimization provenance output is invalid.");
            }
            string digest = RequireDigest(RequireString(file.GetProperty("sha256")));
            if (entry.Sha256 != digest)
            {
                throw new InvalidDataException("Optimization provenance output is invalid.");
            }
            artifacts.Add(new OpenVinoOutputArtifact(name, length, digest));
        }
        HashSet<string> actual = snapshot.Entries.Select(static entry => entry.RelativeName)
            .Where(static name => name != FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.SetEquals(actual) ||
            OpenVinoProvenance.ComputeOutputManifestDigest(artifacts) != outputDigest)
        {
            throw new InvalidDataException("Optimization provenance manifest is invalid.");
        }
    }

    internal static IReadOnlyList<OpenVinoOutputArtifact> CaptureOutput(string stagingDirectory)
    {
        string root = Path.GetFullPath(stagingDirectory);
        List<OpenVinoOutputArtifact> output = [];
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative == FileName || relative.Contains('/') || relative.Contains(':'))
            {
                throw new InvalidDataException("optimization_output_invalid");
            }
            FileInfo file = new(path);
            if (file.Length <= 0 || (file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("optimization_output_invalid");
            }
            output.Add(new OpenVinoOutputArtifact(
                relative,
                file.Length,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()));
        }
        output.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        return output;
    }

    private static string RequireDigest(string? value)
    {
        if (value is null || value.Length != 64 || value.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        return value;
    }

    private static string RequireString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        return value.GetString() ?? throw new InvalidDataException(
            "optimization_output_invalid");
    }

    private static void RequireExactObject(JsonElement value, string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.EnumerateObject().Count() != fields.Length ||
            fields.Any(field => !value.TryGetProperty(field, out _)))
        {
            throw new InvalidDataException("Optimization provenance schema is invalid.");
        }
    }

    private static T ParseEnum<T>(JsonElement value) where T : struct, Enum
    {
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!Enum.TryParse(text, ignoreCase: false, out T parsed) || parsed.ToString() != text)
        {
            throw new InvalidDataException("Optimization provenance enum is invalid.");
        }
        return parsed;
    }
}
