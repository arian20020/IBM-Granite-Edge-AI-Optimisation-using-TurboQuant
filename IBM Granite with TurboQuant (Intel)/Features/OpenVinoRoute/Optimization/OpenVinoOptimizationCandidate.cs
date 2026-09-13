using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

public enum OpenVinoOptimizationObjective
{
    Automatic,
    Quality,
    Balanced,
    Efficiency
}

public enum OpenVinoWeightPrecision
{
    Fp16,
    EightBit,
    FourBit,
    MxFp4,
    Original
}

public enum OpenVinoKvCachePrecision
{
    ReleasedDefault,
    U8,
    U4,
    Tbq4,
    Tbq3
}

public sealed class OpenVinoOptimizationException : Exception
{
    public OpenVinoOptimizationException(OpenVinoSupportCode supportCode)
        : base(supportCode.ToProtocolValue()) => SupportCode = supportCode;

    public OpenVinoSupportCode SupportCode { get; }
}

public sealed record OpenVinoPersistentArtifact
{
    private OpenVinoPersistentArtifact(OpenVinoWeightPrecision weightPrecision) =>
        WeightPrecision = weightPrecision;

    public OpenVinoWeightPrecision WeightPrecision { get; }
    public bool CreatesCompletePackage { get; } = true;

    public static OpenVinoPersistentArtifact Create(
        OpenVinoWeightPrecision targetPrecision,
        OpenVinoWeightPrecision sourcePrecision = OpenVinoWeightPrecision.Fp16)
    {
        if (targetPrecision == OpenVinoWeightPrecision.Original ||
            sourcePrecision == OpenVinoWeightPrecision.Original ||
            (targetPrecision == OpenVinoWeightPrecision.Fp16 &&
             sourcePrecision != OpenVinoWeightPrecision.Fp16))
        {
            throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported);
        }
        return new OpenVinoPersistentArtifact(targetPrecision);
    }
}

public sealed record OpenVinoCompiledCachePolicy
{
    private OpenVinoCompiledCachePolicy(bool enabled)
    {
        Enabled = enabled;
        IsDisposable = true;
        IsModelArtifact = false;
    }

    public bool Enabled { get; }
    public bool IsDisposable { get; }
    public bool IsModelArtifact { get; }

    public static OpenVinoCompiledCachePolicy Disabled { get; } = new(false);
    public static OpenVinoCompiledCachePolicy Disposable { get; } = new(true);
}

public sealed record OpenVinoRuntimeOptimization
{
    public OpenVinoRuntimeOptimization(
        OpenVinoKvCachePrecision kvCachePrecision,
        OpenVinoCompiledCachePolicy compiledCache)
    {
        KvCachePrecision = kvCachePrecision;
        CompiledCache = compiledCache;
        CreatesModelArtifact = false;
    }

    public OpenVinoKvCachePrecision KvCachePrecision { get; }
    public OpenVinoCompiledCachePolicy CompiledCache { get; }
    public bool CreatesModelArtifact { get; }
}

public enum OpenVinoCapabilityPerformanceHint
{
    Latency
}

public enum OpenVinoCapabilityMaturity
{
    Released,
    Experimental
}

public sealed record OpenVinoOptimizationToolVersions(
    string OpenVino,
    string OpenVinoGenAi,
    string Nncf,
    string Optimum,
    string OptimumIntel,
    string Transformers);

public sealed record OpenVinoOptimizationCapabilityAdmission(
    string EvidenceId,
    string Device,
    OpenVinoWeightPrecision WeightPrecision,
    OpenVinoRuntimeOptimization Runtime,
    OpenVinoCapabilityPerformanceHint PerformanceHint,
    int Streams,
    int MinimumContextTokens,
    int MaximumContextTokens,
    OpenVinoCapabilityMaturity Maturity);

public sealed record OpenVinoOptimizationCapabilityEvidence(
    OpenVinoBuildEvidence Builds,
    OpenVinoOptimizationToolVersions Versions,
    IReadOnlyList<OpenVinoOptimizationCapabilityAdmission> Admitted);

public sealed record OpenVinoOptimizationCandidate(
    string ConfigurationId,
    string Device,
    OpenVinoWeightPrecision WeightPrecision,
    OpenVinoPersistentArtifact PersistentArtifact,
    OpenVinoRuntimeOptimization Runtime,
    OpenVinoCapabilityPerformanceHint PerformanceHint,
    int Streams,
    int ContextTokens,
    string Maturity,
    string EvidenceId)
{
    public OpenVinoOptimizationObjective? LegacyObjectiveV1 { get; init; }
    public OpenVinoWeightPrecision SourceWeightPrecision { get; init; } =
        OpenVinoWeightPrecision.Fp16;
    public OpenVinoExecutionPayload? ExecutionPayload { get; init; }

    public void Validate()
    {
        bool legacy = LegacyObjectiveV1.HasValue;
        bool experimental = Maturity == "Experimental candidate";
        bool turbo = Runtime?.KvCachePrecision is OpenVinoKvCachePrecision.Tbq3 or OpenVinoKvCachePrecision.Tbq4;
        bool releasedTurboShape = turbo && !experimental
            && WeightPrecision == OpenVinoWeightPrecision.FourBit
            && ContextTokens == 4096 && Streams == 1
            && Runtime?.CompiledCache.Enabled == false && ExecutionPayload is not null
            && (Runtime.KvCachePrecision, EvidenceId, ConfigurationId) is
                (OpenVinoKvCachePrecision.Tbq3, "OV-TBQ3-CPU-INT4-01", "openvino.turboquant.cpu.int4.tbq3.v1") or
                (OpenVinoKvCachePrecision.Tbq4, "OV-TBQ4-CPU-INT4-01", "openvino.turboquant.cpu.int4.tbq4.v1");
        string configurationPrefix = turbo
            ? "openvino.turboquant.cpu."
            : "openvino.standard.cpu.";
        bool validConfigurationId = ConfigurationId is { Length: > 0 and <= 128 } &&
            ConfigurationId.StartsWith(
                configurationPrefix, StringComparison.Ordinal) &&
            ConfigurationId.All(static character => char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '-' or '_');
        bool persistentShape = legacy
            ? WeightPrecision == OpenVinoWeightPrecision.Original
                ? PersistentArtifact is null
                : PersistentArtifact?.WeightPrecision == WeightPrecision
            : WeightPrecision == SourceWeightPrecision
                ? PersistentArtifact is null
                : PersistentArtifact?.WeightPrecision == WeightPrecision;

        if (string.IsNullOrWhiteSpace(ConfigurationId) ||
            !validConfigurationId ||
            Device != "CPU" ||
            Maturity is not ("Standard candidate" or "Experimental candidate") ||
            turbo && !experimental && !releasedTurboShape ||
            string.IsNullOrWhiteSpace(EvidenceId) ||
            !persistentShape || Runtime is null || Runtime.CompiledCache is null ||
            PerformanceHint != OpenVinoCapabilityPerformanceHint.Latency ||
            Streams < 1 || ContextTokens < 1 ||
            ExecutionPayload is not null &&
            (ExecutionPayload.ConfigurationId != ConfigurationId ||
             ExecutionPayload.Device != Device ||
             ExecutionPayload.Maturity != Maturity ||
             ExecutionPayload.EvidenceId != EvidenceId ||
             ExecutionPayload.RequiresPersistentConversion !=
                (PersistentArtifact is not null)))
        {
            throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported);
        }
    }
}

public static class OpenVinoOptimizationLegacyRegistryV1
{
    public static IReadOnlyList<OpenVinoOptimizationCandidate> Candidates { get; } =
    [
        Candidate(
            "openvino.standard.cpu.int8.default.v1",
            OpenVinoOptimizationObjective.Automatic,
            OpenVinoWeightPrecision.EightBit,
            OpenVinoKvCachePrecision.ReleasedDefault,
            compiledCache: false,
            "OV-STD-CPU-AUTO-01"),
        Candidate(
            "openvino.standard.cpu.fp16.default.v1",
            OpenVinoOptimizationObjective.Quality,
            OpenVinoWeightPrecision.Fp16,
            OpenVinoKvCachePrecision.ReleasedDefault,
            compiledCache: false,
            "OV-STD-CPU-FP16-01"),
        Candidate(
            "openvino.standard.cpu.int8.u8.v1",
            OpenVinoOptimizationObjective.Balanced,
            OpenVinoWeightPrecision.EightBit,
            OpenVinoKvCachePrecision.U8,
            compiledCache: true,
            "OV-STD-CPU-INT8-U8-01"),
        Candidate(
            "openvino.standard.cpu.int4.u8.v1",
            OpenVinoOptimizationObjective.Efficiency,
            OpenVinoWeightPrecision.FourBit,
            OpenVinoKvCachePrecision.U8,
            compiledCache: true,
            "OV-STD-CPU-INT4-U8-01")
    ];

    public static OpenVinoOptimizationCandidate GetRequired(
        OpenVinoOptimizationObjective objective) =>
        Candidates.Single(candidate => candidate.LegacyObjectiveV1 == objective);

    public static bool IsRegistered(OpenVinoOptimizationCandidate candidate) =>
        candidate is not null && Candidates.Contains(candidate);

    private static OpenVinoOptimizationCandidate Candidate(
        string id,
        OpenVinoOptimizationObjective objective,
        OpenVinoWeightPrecision weight,
        OpenVinoKvCachePrecision kv,
        bool compiledCache,
        string evidenceId)
    {
        OpenVinoOptimizationCandidate candidate = new(
            id,
            "CPU",
            weight,
            OpenVinoPersistentArtifact.Create(weight),
            new OpenVinoRuntimeOptimization(
                kv,
                compiledCache
                    ? OpenVinoCompiledCachePolicy.Disposable
                    : OpenVinoCompiledCachePolicy.Disabled),
            OpenVinoCapabilityPerformanceHint.Latency,
            Streams: 1,
            ContextTokens: 4_096,
            "Standard candidate",
            evidenceId)
        {
            LegacyObjectiveV1 = objective
        };
        candidate.Validate();
        return candidate;
    }
}

public sealed record OpenVinoCompiledCacheIdentity(
    string RuntimeIdentity,
    string PluginIdentity,
    string Device,
    string DriverIdentity,
    string ModelSha256,
    string ConfigurationId)
{
    public string ComputeDirectoryKey()
    {
        string[] values =
        [
            RuntimeIdentity, PluginIdentity, Device, DriverIdentity,
            ModelSha256, ConfigurationId
        ];
        if (values.Any(static value => !IsSafeIdentity(value)) ||
            ModelSha256.Length != 64 || ModelSha256.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported);
        }
        StringBuilder canonical = new();
        foreach (string value in values)
        {
            canonical.Append(value.Length).Append(':').Append(value);
        }
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false, true).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static bool IsSafeIdentity(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        !value.Contains('/') && !value.Contains('\\') && !value.Contains(':') &&
        value.All(static character => !char.IsControl(character));
}
