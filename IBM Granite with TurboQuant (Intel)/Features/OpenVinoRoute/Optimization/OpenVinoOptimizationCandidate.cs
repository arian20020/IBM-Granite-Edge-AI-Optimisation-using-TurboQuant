using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;

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
    Original
}

public enum OpenVinoKvCachePrecision
{
    ReleasedDefault,
    U8
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
    Released
}

public sealed record OpenVinoOptimizationToolVersions(
    string OpenVino,
    string OpenVinoGenAi,
    string Nncf);

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
    OpenVinoOptimizationToolVersions Versions,
    IReadOnlyList<OpenVinoOptimizationCapabilityAdmission> Admitted);

public sealed record OpenVinoOptimizationCandidate(
    string ConfigurationId,
    OpenVinoOptimizationObjective Objective,
    string Device,
    OpenVinoPersistentArtifact PersistentArtifact,
    OpenVinoRuntimeOptimization Runtime,
    string Maturity,
    string EvidenceId)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConfigurationId) ||
            !ConfigurationId.StartsWith("openvino.standard.cpu.", StringComparison.Ordinal) ||
            Device != "CPU" || Maturity != "Standard candidate" ||
            string.IsNullOrWhiteSpace(EvidenceId) ||
            PersistentArtifact is null || Runtime is null || Runtime.CompiledCache is null)
        {
            throw new OpenVinoOptimizationException(
                OpenVinoSupportCode.OptimizationUnsupported);
        }
    }
}

public static class OpenVinoOptimizationRegistry
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
        Candidates.Single(candidate => candidate.Objective == objective);

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
            objective,
            "CPU",
            OpenVinoPersistentArtifact.Create(weight),
            new OpenVinoRuntimeOptimization(
                kv,
                compiledCache
                    ? OpenVinoCompiledCachePolicy.Disposable
                    : OpenVinoCompiledCachePolicy.Disabled),
            "Standard candidate",
            evidenceId);
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
