using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

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
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid OptimizationPlanId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConfigurationSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CapabilitySnapshotId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CapabilitySnapshotSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelInspectionRunId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelInspectionHandoffId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong ModelLengthBytes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductHardwareRunId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HardwareSnapshotSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenVinoRuntimeTechnicalConfiguration? RuntimeConfiguration { get; init; }
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
        JsonOptions.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));

    internal void Write(string stagingDirectory)
    {
        Validate();
        File.WriteAllBytes(
            Path.Combine(stagingDirectory, FileName),
            JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions));
    }

    public static OpenVinoOptimizationProvenance Read(string publishedDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publishedDirectory);
        OpenVinoOptimizationProvenance? provenance = JsonSerializer.Deserialize<
            OpenVinoOptimizationProvenance>(
                File.ReadAllBytes(Path.Combine(publishedDirectory, FileName)),
                JsonOptions);
        if (provenance is null)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        provenance.Validate();
        return provenance;
    }

    internal static OpenVinoOptimizationProvenance CreatePlanBound(
        OptimizationExecutionPlan plan,
        Guid operationId,
        Guid validationRunId,
        string sourceManifestSha256,
        string configurationId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoWeightPrecision targetWeightPrecision,
        OpenVinoOptimizationCandidate candidate,
        OpenVinoRuntimeOptimizationEvidence runtime,
        IReadOnlyDictionary<string, string> optimizerVersions,
        IReadOnlyList<OpenVinoOutputArtifact> outputFiles,
        string outputManifestSha256) => new(
            CurrentSchemaVersion,
            operationId,
            validationRunId,
            sourceManifestSha256,
            configurationId,
            sourceWeightPrecision,
            targetWeightPrecision,
            candidate.Runtime.KvCachePrecision,
            runtime.ActualDevice,
            runtime.ActualKvCachePrecision,
            optimizerVersions,
            outputFiles,
            outputManifestSha256,
            "passed",
            runtime.GenerationDisposition,
            runtime.QualityDisposition)
        {
            OptimizationPlanId = plan.OptimizationPlanId,
            ConfigurationSha256 = plan.ConfigurationSha256,
            CapabilitySnapshotId = plan.CapabilitySnapshot.SnapshotId,
            CapabilitySnapshotSha256 =
                plan.CapabilitySnapshot.CapabilitySnapshotSha256,
            ModelInspectionRunId = plan.Binding.ModelInspectionRunId,
            ModelInspectionHandoffId = plan.Binding.ModelInspectionHandoffId,
            ModelSha256 = plan.Binding.ModelSha256,
            ModelLengthBytes = plan.Binding.ModelLengthBytes,
            ProductHardwareRunId = plan.Binding.ProductHardwareRunId,
            HardwareSnapshotSha256 = plan.Binding.HardwareSnapshotSha256,
            RuntimeConfiguration = OpenVinoRuntimeTechnicalConfiguration.From(candidate)
        };

    internal void Validate()
    {
        if (SchemaVersion is not (LegacySchemaVersion or CurrentSchemaVersion) ||
            OperationId == Guid.Empty ||
            ValidationRunId == Guid.Empty ||
            OptimizerVersions is null || OutputFiles is null ||
            ActualDevice != "CPU" || ActualKvCachePrecision != RequestedKvCachePrecision ||
            ValidationDisposition != "passed" || GenerationDisposition != "passed" ||
            QualityDisposition != "passed" || OutputFiles.Count == 0)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        if (SchemaVersion == LegacySchemaVersion)
        {
            ValidateLegacyCandidate();
        }
        else
        {
            ValidatePlanBinding();
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

    private void ValidateLegacyCandidate()
    {
        if (!OpenVinoOptimizationLegacyRegistryV1.Candidates.Any(candidate =>
                candidate.ConfigurationId == ConfigurationId &&
                candidate.PersistentArtifact.WeightPrecision == TargetWeightPrecision &&
                candidate.Runtime.KvCachePrecision == RequestedKvCachePrecision))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    private void ValidatePlanBinding()
    {
        if (OptimizationPlanId == Guid.Empty ||
            !IsSafeIdentity(ConfigurationId) ||
            !IsSafeIdentity(CapabilitySnapshotId) ||
            !IsSafeIdentity(ModelInspectionRunId) ||
            !IsSafeIdentity(ModelInspectionHandoffId) ||
            !IsSafeIdentity(ProductHardwareRunId) ||
            ModelLengthBytes == 0 || RuntimeConfiguration is null ||
            RuntimeConfiguration.KvCachePrecision != RequestedKvCachePrecision)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        RequireDigest(ConfigurationSha256);
        RequireDigest(CapabilitySnapshotSha256);
        RequireDigest(ModelSha256);
        RequireDigest(HardwareSnapshotSha256);
        if (!RuntimeConfiguration.IsSupported)
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
        string[] legacyFields =
        [
            "schemaVersion", "operationId", "validationRunId", "sourceManifestSha256",
            "configurationId", "sourceWeightPrecision", "targetWeightPrecision",
            "requestedKvCachePrecision", "actualDevice", "actualKvCachePrecision",
            "optimizerVersions", "outputFiles", "outputManifestSha256",
            "validationDisposition", "generationDisposition", "qualityDisposition"
        ];
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out JsonElement schemaValue) ||
            schemaValue.ValueKind != JsonValueKind.Number ||
            !schemaValue.TryGetInt32(out int schema))
        {
            throw new InvalidDataException("Optimization provenance schema is invalid.");
        }
        string[] fields = schema == LegacySchemaVersion
            ? legacyFields
            : schema == CurrentSchemaVersion
                ? legacyFields.Concat(
                [
                    "optimizationPlanId", "configurationSha256",
                    "capabilitySnapshotId", "capabilitySnapshotSha256",
                    "modelInspectionRunId", "modelInspectionHandoffId",
                    "modelSha256", "modelLengthBytes", "productHardwareRunId",
                    "hardwareSnapshotSha256", "runtimeConfiguration"
                ]).ToArray()
                : throw new InvalidDataException(
                    "Optimization provenance schema is invalid.");
        RequireExactObject(root, fields);
        if (
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
        OpenVinoOptimizationProvenance parsed = JsonSerializer.Deserialize<
            OpenVinoOptimizationProvenance>(root.GetRawText(), JsonOptions) ??
            throw new InvalidDataException("Optimization provenance is invalid.");
        parsed.Validate();
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
            schema == LegacySchemaVersion &&
            !OpenVinoOptimizationLegacyRegistryV1.Candidates.Any(candidate =>
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

    private static bool IsSafeIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        !value.Contains('/') && !value.Contains('\\') && !value.Contains(':') &&
        !value.Contains("..", StringComparison.Ordinal) &&
        value.All(static character => !char.IsControl(character));

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

public sealed record OpenVinoRuntimeTechnicalConfiguration(
    string Device,
    OpenVinoCapabilityPerformanceHint PerformanceHint,
    int Streams,
    int ContextTokens,
    bool CompiledCacheEnabled,
    OpenVinoKvCachePrecision KvCachePrecision)
{
    internal bool IsSupported =>
        Device == "CPU" &&
        PerformanceHint == OpenVinoCapabilityPerformanceHint.Latency &&
        Streams == 1 && ContextTokens == 4_096 && !CompiledCacheEnabled &&
        Enum.IsDefined(KvCachePrecision);

    internal static OpenVinoRuntimeTechnicalConfiguration From(
        OpenVinoOptimizationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new(
            candidate.Device,
            candidate.PerformanceHint,
            candidate.Streams,
            candidate.ContextTokens,
            candidate.Runtime.CompiledCache.Enabled,
            candidate.Runtime.KvCachePrecision);
    }

    internal void ValidateSupported()
    {
        if (!IsSupported)
        {
            throw new OpenVinoOptimizationException(
                GraniteEdgeAI.OpenVino.Contracts.OpenVinoSupportCode
                    .OptimizationUnsupported);
        }
    }
}

public sealed record OpenVinoRuntimeOptimizationProfile(
    int SchemaVersion,
    Guid OptimizationPlanId,
    string ConfigurationSha256,
    string CapabilitySnapshotId,
    string CapabilitySnapshotSha256,
    string ModelInspectionRunId,
    string ModelInspectionHandoffId,
    string ModelSha256,
    ulong ModelLengthBytes,
    string ProductHardwareRunId,
    string HardwareSnapshotSha256,
    OpenVinoRuntimeTechnicalConfiguration RuntimeConfiguration,
    bool SourceUnchanged,
    DateTimeOffset CreatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "granite-openvino-runtime-profile.json";
    private static readonly JsonSerializerOptions ProfileJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static OpenVinoRuntimeOptimizationProfile() =>
        ProfileJsonOptions.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));

    internal static OpenVinoRuntimeOptimizationProfile From(
        OptimizationExecutionPlan plan,
        OpenVinoOptimizationCandidate candidate,
        DateTimeOffset createdAtUtc) => new(
            CurrentSchemaVersion,
            plan.OptimizationPlanId,
            plan.ConfigurationSha256,
            plan.CapabilitySnapshot.SnapshotId,
            plan.CapabilitySnapshot.CapabilitySnapshotSha256,
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ModelSha256,
            plan.Binding.ModelLengthBytes,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256,
            OpenVinoRuntimeTechnicalConfiguration.From(candidate),
            SourceUnchanged: true,
            createdAtUtc);

    internal byte[] Serialize()
    {
        Validate();
        return JsonSerializer.SerializeToUtf8Bytes(this, ProfileJsonOptions);
    }

    public static OpenVinoRuntimeOptimizationProfile Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        OpenVinoRuntimeOptimizationProfile? profile = JsonSerializer.Deserialize<
            OpenVinoRuntimeOptimizationProfile>(
                File.ReadAllBytes(Path.Combine(directory, FileName)),
                ProfileJsonOptions);
        if (profile is null)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        profile.Validate();
        return profile;
    }

    internal void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || OptimizationPlanId == Guid.Empty ||
            ModelLengthBytes == 0 || !SourceUnchanged || RuntimeConfiguration is null)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        RequireDigest(ConfigurationSha256);
        RequireDigest(CapabilitySnapshotSha256);
        RequireDigest(ModelSha256);
        RequireDigest(HardwareSnapshotSha256);
        RequireIdentity(CapabilitySnapshotId);
        RequireIdentity(ModelInspectionRunId);
        RequireIdentity(ModelInspectionHandoffId);
        RequireIdentity(ProductHardwareRunId);
        if (!RuntimeConfiguration.IsSupported)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    private static void RequireDigest(string value)
    {
        if (value is null || value.Length != 64 || value.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    private static void RequireIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value.Contains('/') || value.Contains('\\') || value.Contains(':') ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Any(static character => char.IsControl(character)))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }
}

internal sealed record OpenVinoStoredRuntimeProfile(
    string OutputIdentity,
    string ManifestSha256,
    ulong SizeBytes);
