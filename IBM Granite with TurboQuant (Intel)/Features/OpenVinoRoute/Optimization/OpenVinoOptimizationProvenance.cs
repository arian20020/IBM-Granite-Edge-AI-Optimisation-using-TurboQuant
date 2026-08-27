using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using ExecutionKvCachePrecision = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoKvCachePrecision;
using ExecutionWeightPrecision = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoWeightPrecision;

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenVinoDurablePlanContext? PlanContext { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExecutionConfigurationSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanBindingSha256 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExecutorContractVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OpenVinoExecutionPayloadJson { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OpenVinoExecutionPayloadSha256 { get; init; }
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
        OpenVinoOptimizationCandidate candidate,
        OpenVinoRuntimeOptimizationEvidence runtime,
        IReadOnlyDictionary<string, string> optimizerVersions,
        IReadOnlyList<OpenVinoOutputArtifact> outputFiles,
        string outputManifestSha256)
    {
        OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino ??
            throw new InvalidDataException("optimization_output_invalid");
        OpenVinoSerializedPayload serializedPayload =
            OpenVinoPayloadEvidence.Serialize(payload);
        OpenVinoWeightPrecision sourceWeightPrecision = OpenVinoPayloadEvidence.MapWeight(
            payload.SourceWeightPrecision);
        OpenVinoWeightPrecision targetWeightPrecision = OpenVinoPayloadEvidence.MapWeight(
            payload.TargetWeightPrecision);
        OpenVinoRuntimeTechnicalConfiguration runtimeConfiguration =
            OpenVinoRuntimeTechnicalConfiguration.From(candidate);
        OpenVinoDurablePlanContext planContext =
            OpenVinoDurablePlanContext.From(plan);
        string executionConfigurationSha256 =
            OpenVinoDurablePlanDigest.ExecutionConfigurationSha256(
                plan.ConfigurationSha256,
                serializedPayload.Sha256,
                payload.ConfigurationId,
                sourceWeightPrecision,
                targetWeightPrecision,
                runtimeConfiguration,
                optimizerVersions);
        string planBindingSha256 = OpenVinoDurablePlanDigest.PlanBindingSha256(
            plan,
            planContext,
            executionConfigurationSha256);
        return new(
            CurrentSchemaVersion,
            operationId,
            validationRunId,
            sourceManifestSha256,
            payload.ConfigurationId,
            sourceWeightPrecision,
            targetWeightPrecision,
            OpenVinoPayloadEvidence.MapKvCache(payload.KvCachePrecision),
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
            RuntimeConfiguration = runtimeConfiguration,
            PlanContext = planContext,
            ExecutionConfigurationSha256 = executionConfigurationSha256,
            PlanBindingSha256 = planBindingSha256,
            ExecutorContractVersion = plan.ContractVersion,
            OpenVinoExecutionPayloadJson = serializedPayload.Json,
            OpenVinoExecutionPayloadSha256 = serializedPayload.Sha256
        };
    }

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
        if (SchemaVersion == LegacySchemaVersion &&
            (OptimizerVersions.Count != RequiredVersions.Count ||
             RequiredVersions.Any(required =>
                 !OptimizerVersions.TryGetValue(required.Key, out string? value) ||
                 value != required.Value)) ||
            SchemaVersion == CurrentSchemaVersion &&
            (OptimizerVersions.Count == 0 || OptimizerVersions.Any(version =>
                !IsSafeIdentity(version.Key) || !IsSafeIdentity(version.Value))))
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
            ExecutorContractVersion != 2 ||
            !IsSafeIdentity(ConfigurationId) ||
            !IsSafeIdentity(CapabilitySnapshotId) ||
            !IsSafeIdentity(ModelInspectionRunId) ||
            !IsSafeIdentity(ModelInspectionHandoffId) ||
            !IsSafeIdentity(ProductHardwareRunId) ||
            ModelLengthBytes == 0 || RuntimeConfiguration is null ||
            PlanContext is null ||
            RuntimeConfiguration.KvCachePrecision != RequestedKvCachePrecision)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        RequireDigest(ConfigurationSha256);
        RequireDigest(CapabilitySnapshotSha256);
        RequireDigest(ModelSha256);
        RequireDigest(HardwareSnapshotSha256);
        RequireDigest(ExecutionConfigurationSha256);
        RequireDigest(PlanBindingSha256);
        OpenVinoPayloadEvidence.Validate(
            OpenVinoExecutionPayloadJson,
            OpenVinoExecutionPayloadSha256);
        if (!RuntimeConfiguration.IsSupported)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        PlanContext.Validate(RuntimeConfiguration.ContextTokens);
        string executionDigest =
            OpenVinoDurablePlanDigest.ExecutionConfigurationSha256(
                ConfigurationSha256!,
                OpenVinoExecutionPayloadSha256!,
                ConfigurationId,
                SourceWeightPrecision,
                TargetWeightPrecision,
                RuntimeConfiguration,
                OptimizerVersions);
        if (!string.Equals(
                executionDigest,
                ExecutionConfigurationSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                OpenVinoDurablePlanDigest.PlanBindingSha256(
                    OptimizationPlanId,
                    ConfigurationSha256!,
                    CapabilitySnapshotId!,
                    CapabilitySnapshotSha256!,
                    ModelInspectionRunId!,
                    ModelInspectionHandoffId!,
                    ModelSha256!,
                    ModelLengthBytes,
                    ProductHardwareRunId!,
                    HardwareSnapshotSha256!,
                    PlanContext,
                    executionDigest),
                PlanBindingSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    internal static OpenVinoWeightPrecision ReadSourcePrecision(
        OpenVinoPackageSnapshot snapshot,
        OpenVinoWeightPrecision? authoritativeSourcePrecision = null)
    {
        if (!snapshot.TryGetEntry(FileName, out OpenVinoPackageSnapshotEntry entry))
        {
            return authoritativeSourcePrecision ?? OpenVinoWeightPrecision.Fp16;
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
                    "hardwareSnapshotSha256", "runtimeConfiguration",
                    "planContext", "executionConfigurationSha256",
                    "planBindingSha256", "executorContractVersion",
                    "openVinoExecutionPayloadJson",
                    "openVinoExecutionPayloadSha256"
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
        if (schema == CurrentSchemaVersion)
        {
            RequireExactObject(root.GetProperty("runtimeConfiguration"),
            [
                "device", "performanceHint", "streams", "contextTokens",
                "compiledCacheEnabled", "kvCachePrecision"
            ]);
            RequireExactObject(root.GetProperty("planContext"),
            [
                "contractVersion", "route", "workloadId",
                "workloadMinimumContextTokens", "workloadMinimumQuality",
                "workloadCandidateContexts", "preferenceKind",
                "preferenceValue", "preferenceBand", "sharedWithAdjacentBand",
                "planCreatedAtUtc"
            ]);
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
            schema == LegacySchemaVersion &&
            !OpenVinoOptimizationLegacyRegistryV1.Candidates.Any(candidate =>
                candidate.ConfigurationId == configurationId &&
                candidate.PersistentArtifact.WeightPrecision == targetPrecision &&
                candidate.Runtime.KvCachePrecision == requestedKv))
        {
            throw new InvalidDataException("Optimization provenance candidate is invalid.");
        }

        JsonElement versions = root.GetProperty("optimizerVersions");
        if (schema == LegacySchemaVersion)
        {
            string[] versionNames = RequiredVersions.Keys.ToArray();
            RequireExactObject(versions, versionNames);
            foreach ((string name, string expected) in RequiredVersions)
            {
                if (RequireString(versions.GetProperty(name)) != expected)
                {
                    throw new InvalidDataException(
                        "Optimization provenance version is invalid.");
                }
            }
        }
        else if (versions.ValueKind != JsonValueKind.Object ||
                 !versions.EnumerateObject().Any() ||
                 versions.EnumerateObject().Any(version =>
                     !IsSafeIdentity(version.Name) ||
                     !IsSafeIdentity(RequireString(version.Value))))
        {
            throw new InvalidDataException("Optimization provenance version is invalid.");
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

public sealed record OpenVinoDurablePlanContext(
    int ContractVersion,
    OptimizationRoute Route,
    string WorkloadId,
    int WorkloadMinimumContextTokens,
    OptimizationAssessment WorkloadMinimumQuality,
    IReadOnlyList<int> WorkloadCandidateContexts,
    OptimizationPreferenceKind PreferenceKind,
    int? PreferenceValue,
    OptimizationPreferenceBand? PreferenceBand,
    bool SharedWithAdjacentBand,
    DateTimeOffset PlanCreatedAtUtc)
{
    internal static OpenVinoDurablePlanContext From(
        OptimizationExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(
            plan.ContractVersion,
            plan.Route,
            plan.Workload.WorkloadId,
            plan.Workload.MinimumContextTokens,
            plan.Workload.MinimumQuality,
            plan.Workload.CandidateContexts.Select(
                static context => context.Tokens).ToArray(),
            plan.Preference.Kind,
            plan.Preference.PreferenceValue,
            plan.Preference.Band,
            plan.SharedWithAdjacentBand,
            plan.CreatedAtUtc);
    }

    internal void Validate(int selectedContextTokens)
    {
        bool automatic = PreferenceKind == OptimizationPreferenceKind.Automatic &&
            PreferenceValue is null && PreferenceBand is null;
        bool manual = PreferenceKind == OptimizationPreferenceKind.Manual &&
            PreferenceValue is >= 0 and <= 100 &&
            PreferenceBand == BandFor(PreferenceValue.Value);
        if (ContractVersion < OptimizationExecutionPlan.MinimumExecutableContractVersion ||
            ContractVersion > OptimizationExecutionPlan.CurrentContractVersion ||
            Route != OptimizationRoute.OpenVino ||
            string.IsNullOrWhiteSpace(WorkloadId) || WorkloadId.Length > 128 ||
            WorkloadId.Contains('/') || WorkloadId.Contains('\\') ||
            WorkloadId.Contains(':') ||
            WorkloadMinimumContextTokens < 1 ||
            WorkloadMinimumQuality == OptimizationAssessment.Unknown ||
            WorkloadCandidateContexts is null ||
            WorkloadCandidateContexts.Count == 0 ||
            WorkloadCandidateContexts.Any(static context => context < 1) ||
            !WorkloadCandidateContexts.Contains(selectedContextTokens) ||
            (!automatic && !manual) ||
            PlanCreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    private static OptimizationPreferenceBand BandFor(int value) => value switch
    {
        < 20 => OptimizationPreferenceBand.MaximumEfficiency,
        < 40 => OptimizationPreferenceBand.Efficient,
        < 60 => OptimizationPreferenceBand.Balanced,
        < 80 => OptimizationPreferenceBand.HighCapability,
        _ => OptimizationPreferenceBand.MaximumCapability
    };
}

internal sealed record OpenVinoSerializedPayload(string Json, string Sha256);

internal static class OpenVinoPayloadEvidence
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static OpenVinoPayloadEvidence() =>
        PayloadJsonOptions.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));

    internal static OpenVinoSerializedPayload Serialize(
        OpenVinoExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        string json = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        return new OpenVinoSerializedPayload(json, ComputeSha256(json));
    }

    internal static void Validate(string? json, string? sha256)
    {
        if (string.IsNullOrEmpty(json) ||
            !string.Equals(ComputeSha256(json), sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
    }

    internal static OpenVinoWeightPrecision MapWeight(
        ExecutionWeightPrecision value) => value switch
        {
            ExecutionWeightPrecision.Fp16 => OpenVinoWeightPrecision.Fp16,
            ExecutionWeightPrecision.EightBit => OpenVinoWeightPrecision.EightBit,
            ExecutionWeightPrecision.FourBit => OpenVinoWeightPrecision.FourBit,
            _ => throw new InvalidDataException("optimization_output_invalid")
        };

    internal static OpenVinoKvCachePrecision MapKvCache(
        ExecutionKvCachePrecision value) => value switch
        {
            ExecutionKvCachePrecision.ReleasedDefault =>
                OpenVinoKvCachePrecision.ReleasedDefault,
            ExecutionKvCachePrecision.U8 => OpenVinoKvCachePrecision.U8,
            _ => throw new InvalidDataException("optimization_output_invalid")
        };

    private static string ComputeSha256(string json) =>
        Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false, true).GetBytes(json))).ToLowerInvariant();
}

internal static class OpenVinoDurablePlanDigest
{
    internal static string ExecutionConfigurationSha256(
        string planConfigurationSha256,
        string openVinoExecutionPayloadSha256,
        string configurationId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoWeightPrecision targetWeightPrecision,
        OpenVinoRuntimeTechnicalConfiguration runtime,
        IReadOnlyDictionary<string, string>? actualOptimizerVersions) =>
        Hash(
            ("version", "2"),
            ("planConfigurationSha256", planConfigurationSha256),
            ("openVinoExecutionPayloadSha256", openVinoExecutionPayloadSha256),
            ("configurationId", configurationId),
            ("sourceWeightPrecision", EnumValue(sourceWeightPrecision)),
            ("targetWeightPrecision", EnumValue(targetWeightPrecision)),
            ("device", runtime.Device),
            ("performanceHint", EnumValue(runtime.PerformanceHint)),
            ("streams", Number(runtime.Streams)),
            ("contextTokens", Number(runtime.ContextTokens)),
            ("compiledCacheEnabled", runtime.CompiledCacheEnabled ? "1" : "0"),
            ("kvCachePrecision", EnumValue(runtime.KvCachePrecision)),
            ("actualOptimizerVersionsSha256",
                OptimizerVersionsSha256(actualOptimizerVersions)));

    internal static string PlanBindingSha256(
        OptimizationExecutionPlan plan,
        OpenVinoDurablePlanContext context,
        string executionConfigurationSha256) =>
        PlanBindingSha256(
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
            context,
            executionConfigurationSha256);

    internal static string PlanBindingSha256(
        Guid optimizationPlanId,
        string configurationSha256,
        string capabilitySnapshotId,
        string capabilitySnapshotSha256,
        string modelInspectionRunId,
        string modelInspectionHandoffId,
        string modelSha256,
        ulong modelLengthBytes,
        string productHardwareRunId,
        string hardwareSnapshotSha256,
        OpenVinoDurablePlanContext context,
        string executionConfigurationSha256) =>
        Hash(
            ("version", "1"),
            ("optimizationPlanId", optimizationPlanId.ToString("D")),
            ("configurationSha256", configurationSha256),
            ("capabilitySnapshotId", capabilitySnapshotId),
            ("capabilitySnapshotSha256", capabilitySnapshotSha256),
            ("modelInspectionRunId", modelInspectionRunId),
            ("modelInspectionHandoffId", modelInspectionHandoffId),
            ("modelSha256", modelSha256),
            ("modelLengthBytes", Number(modelLengthBytes)),
            ("productHardwareRunId", productHardwareRunId),
            ("hardwareSnapshotSha256", hardwareSnapshotSha256),
            ("contractVersion", Number(context.ContractVersion)),
            ("route", EnumValue(context.Route)),
            ("workloadId", context.WorkloadId),
            ("workloadMinimumContextTokens",
                Number(context.WorkloadMinimumContextTokens)),
            ("workloadMinimumQuality", EnumValue(context.WorkloadMinimumQuality)),
            ("workloadCandidateContexts", string.Join(
                ",",
                context.WorkloadCandidateContexts.Select(Number))),
            ("preferenceKind", EnumValue(context.PreferenceKind)),
            ("preferenceValue", context.PreferenceValue is int preference
                ? Number(preference)
                : "-"),
            ("preferenceBand", context.PreferenceBand is
                OptimizationPreferenceBand band ? EnumValue(band) : "-"),
            ("sharedWithAdjacentBand", context.SharedWithAdjacentBand ? "1" : "0"),
            ("planCreatedAtUtc", context.PlanCreatedAtUtc.ToString(
                "O", CultureInfo.InvariantCulture)),
            ("executionConfigurationSha256", executionConfigurationSha256));

    private static string Hash(params (string Field, string Value)[] values)
    {
        StringBuilder canonical = new();
        foreach ((string field, string value) in values)
        {
            canonical.Append(field.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(field)
                .Append('=')
                .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value).Append('|');
        }
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false, true).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static string OptimizerVersionsSha256(
        IReadOnlyDictionary<string, string>? versions)
    {
        if (versions is null)
        {
            return "-";
        }
        StringBuilder canonical = new();
        foreach ((string name, string value) in versions.OrderBy(
                     static version => version.Key,
                     StringComparer.Ordinal))
        {
            canonical.Append(name.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(name)
                .Append('=')
                .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value).Append('|');
        }
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false, true).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static string EnumValue<T>(T value) where T : struct, Enum =>
        Convert.ToInt32(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Number(ulong value) =>
        value.ToString(CultureInfo.InvariantCulture);
}

public sealed record OpenVinoRuntimeOptimizationProfile(
    int SchemaVersion,
    Guid OptimizationPlanId,
    string ConfigurationSha256,
    int ExecutorContractVersion,
    string OpenVinoExecutionPayloadJson,
    string OpenVinoExecutionPayloadSha256,
    string CapabilitySnapshotId,
    string CapabilitySnapshotSha256,
    string ModelInspectionRunId,
    string ModelInspectionHandoffId,
    string ModelSha256,
    ulong ModelLengthBytes,
    string ProductHardwareRunId,
    string HardwareSnapshotSha256,
    string ConfigurationId,
    OpenVinoWeightPrecision WeightPrecision,
    OpenVinoRuntimeTechnicalConfiguration RuntimeConfiguration,
    OpenVinoDurablePlanContext PlanContext,
    string ExecutionConfigurationSha256,
    string PlanBindingSha256,
    bool SourceUnchanged,
    DateTimeOffset CreatedAtUtc)
{
    public const int CurrentSchemaVersion = 2;
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
        DateTimeOffset createdAtUtc)
    {
        OpenVinoExecutionPayload payload = plan.ExecutionPayload.OpenVino ??
            throw new InvalidDataException("optimization_output_invalid");
        if (payload.RequiresPersistentConversion || candidate.PersistentArtifact is not null)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        OpenVinoSerializedPayload serializedPayload =
            OpenVinoPayloadEvidence.Serialize(payload);
        OpenVinoRuntimeTechnicalConfiguration runtime =
            OpenVinoRuntimeTechnicalConfiguration.From(candidate);
        OpenVinoDurablePlanContext context = OpenVinoDurablePlanContext.From(plan);
        string executionDigest =
            OpenVinoDurablePlanDigest.ExecutionConfigurationSha256(
                plan.ConfigurationSha256,
                serializedPayload.Sha256,
                payload.ConfigurationId,
                OpenVinoPayloadEvidence.MapWeight(payload.SourceWeightPrecision),
                OpenVinoPayloadEvidence.MapWeight(payload.TargetWeightPrecision),
                runtime,
                actualOptimizerVersions: null);
        return new(
            CurrentSchemaVersion,
            plan.OptimizationPlanId,
            plan.ConfigurationSha256,
            plan.ContractVersion,
            serializedPayload.Json,
            serializedPayload.Sha256,
            plan.CapabilitySnapshot.SnapshotId,
            plan.CapabilitySnapshot.CapabilitySnapshotSha256,
            plan.Binding.ModelInspectionRunId,
            plan.Binding.ModelInspectionHandoffId,
            plan.Binding.ModelSha256,
            plan.Binding.ModelLengthBytes,
            plan.Binding.ProductHardwareRunId,
            plan.Binding.HardwareSnapshotSha256,
            payload.ConfigurationId,
            OpenVinoPayloadEvidence.MapWeight(payload.TargetWeightPrecision),
            runtime,
            context,
            executionDigest,
            OpenVinoDurablePlanDigest.PlanBindingSha256(
                plan, context, executionDigest),
            SourceUnchanged: true,
            createdAtUtc);
    }

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
            ExecutorContractVersion != 2 ||
            ModelLengthBytes == 0 || !SourceUnchanged ||
            !Enum.IsDefined(WeightPrecision) ||
            WeightPrecision == OpenVinoWeightPrecision.Original ||
            RuntimeConfiguration is null || PlanContext is null ||
            CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        RequireDigest(ConfigurationSha256);
        RequireDigest(CapabilitySnapshotSha256);
        RequireDigest(ModelSha256);
        RequireDigest(HardwareSnapshotSha256);
        RequireDigest(ExecutionConfigurationSha256);
        RequireDigest(PlanBindingSha256);
        OpenVinoPayloadEvidence.Validate(
            OpenVinoExecutionPayloadJson,
            OpenVinoExecutionPayloadSha256);
        RequireIdentity(CapabilitySnapshotId);
        RequireIdentity(ModelInspectionRunId);
        RequireIdentity(ModelInspectionHandoffId);
        RequireIdentity(ProductHardwareRunId);
        RequireIdentity(ConfigurationId);
        if (!RuntimeConfiguration.IsSupported)
        {
            throw new InvalidDataException("optimization_output_invalid");
        }
        PlanContext.Validate(RuntimeConfiguration.ContextTokens);
        string executionDigest =
            OpenVinoDurablePlanDigest.ExecutionConfigurationSha256(
                ConfigurationSha256,
                OpenVinoExecutionPayloadSha256,
                ConfigurationId,
                WeightPrecision,
                WeightPrecision,
                RuntimeConfiguration,
                actualOptimizerVersions: null);
        if (!string.Equals(
                executionDigest,
                ExecutionConfigurationSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                OpenVinoDurablePlanDigest.PlanBindingSha256(
                    OptimizationPlanId,
                    ConfigurationSha256,
                    CapabilitySnapshotId,
                    CapabilitySnapshotSha256,
                    ModelInspectionRunId,
                    ModelInspectionHandoffId,
                    ModelSha256,
                    ModelLengthBytes,
                    ProductHardwareRunId,
                    HardwareSnapshotSha256,
                    PlanContext,
                    executionDigest),
                PlanBindingSha256,
                StringComparison.Ordinal))
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
