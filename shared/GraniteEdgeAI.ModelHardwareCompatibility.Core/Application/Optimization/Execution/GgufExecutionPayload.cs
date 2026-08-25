using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

/// <summary>
/// The backend the GGUF runtime is launched with.
///
/// Mirrors <c>GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeBackend</c>
/// member for member. The names are identical so G1 can construct a
/// <c>GgufRuntimeConfiguration</c> straight from this payload rather than
/// translating, and translation is exactly where a default gets invented.
///
/// This is deliberately not <see cref="Domain.CompatibilityBackend"/>. That
/// enum is a planning vocabulary covering both routes and includes OpenVINO
/// members a GGUF runtime cannot accept; a payload typed on it would let a plan
/// name a backend the executor has no arm for.
/// </summary>
public enum GgufRuntimeBackend
{
    Cpu,
    Vulkan,
    Sycl
}

/// <summary>
/// How the runtime stores one half of the attention cache.
///
/// Mirrors <c>GgufCacheType</c> member for member, including the spellings
/// (<c>Q8Zero</c>, not <c>Q8_0</c>). Key and value are configured separately by
/// the runtime, so the plan carries them separately too - collapsing them into
/// one field would force the executor to decide what the other one was.
/// </summary>
public enum GgufCacheType
{
    F16,
    Q8Zero,
    Q4Zero,
    Turbo3,
    Turbo4
}

/// <summary>
/// The quantiser that would produce a persistent GGUF artifact.
///
/// A distinct dependency with its own identity, and required in full whenever a
/// conversion is planned. The digest is not decoration: the plan pins which
/// executable is allowed to run, and G1 recomputes it before launch. A
/// conversion tool admitted by name alone is a tool that can be swapped.
/// </summary>
public sealed record GgufQuantiserIdentity
{
    private GgufQuantiserIdentity(
        string packageId, string toolVersion, string executableSha256)
    {
        PackageId = packageId;
        ToolVersion = toolVersion;
        ExecutableSha256 = executableSha256;
    }

    /// <summary>The admitted package or tool this executable comes from.</summary>
    public string PackageId { get; }

    /// <summary>Version or build identity of the quantiser itself.</summary>
    public string ToolVersion { get; }

    /// <summary>
    /// Digest of the executable that is permitted to run. Recomputed by the
    /// executor immediately before launch; a mismatch fails closed.
    /// </summary>
    public string ExecutableSha256 { get; }

    public static GgufQuantiserIdentity Create(
        string packageId, string toolVersion, string executableSha256)
    {
        OptimizationIdentifier.Require(
            packageId, nameof(packageId), "The quantiser package");
        OptimizationIdentifier.Require(
            toolVersion, nameof(toolVersion), "The quantiser tool version");

        if (!OptimizationDigest.IsCanonical(executableSha256))
        {
            throw new ArgumentException(
                "A quantiser must pin its executable digest as 64 lowercase hex "
                + "characters. A tool admitted by name alone is one that can be "
                + "swapped between planning and execution.",
                nameof(executableSha256));
        }

        return new GgufQuantiserIdentity(packageId, toolVersion, executableSha256);
    }
}

/// <summary>
/// Everything the GGUF executor needs, with nothing left to infer.
///
/// V1 gave G1 a coarse route configuration and one runtime version, which meant
/// the executor had to invent a thread count, a batch size, an exact GPU layer
/// count, and separate key and value cache types before it could launch
/// anything. Values invented after confirmation are values the user never
/// agreed to, so every one of them is now in the plan and inside its digest.
///
/// Field names and types mirror
/// <c>GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeConfiguration</c>
/// so G1 constructs one directly. This project does not reference that assembly
/// - it lives on another branch and depending on it would invert the layering -
/// so the mapping is held by name and pinned by the handoff document.
/// </summary>
public sealed record GgufExecutionPayload
{
    private GgufExecutionPayload(
        string runtimeBuildId,
        string runtimeSourceCommit,
        GgufRuntimeBackend backend,
        string deviceId,
        int contextSize,
        GgufCacheType keyCacheType,
        GgufCacheType valueCacheType,
        int gpuLayerCount,
        bool flashAttention,
        int threadCount,
        int batchSize,
        string evidenceGrade,
        string profileId,
        int maximumGeneratedTokens,
        GgufWeightFormat persistentTargetWeightFormat,
        GgufQuantiserIdentity? quantiser,
        GgufConversionSourceBinding? conversionSource,
        GgufRequantisationPolicy? requantisationPolicy)
    {
        RuntimeBuildId = runtimeBuildId;
        RuntimeSourceCommit = runtimeSourceCommit;
        Backend = backend;
        DeviceId = deviceId;
        ContextSize = contextSize;
        KeyCacheType = keyCacheType;
        ValueCacheType = valueCacheType;
        GpuLayerCount = gpuLayerCount;
        FlashAttention = flashAttention;
        ThreadCount = threadCount;
        BatchSize = batchSize;
        EvidenceGrade = evidenceGrade;
        ProfileId = profileId;
        MaximumGeneratedTokens = maximumGeneratedTokens;
        PersistentTargetWeightFormat = persistentTargetWeightFormat;
        Quantiser = quantiser;
        ConversionSource = conversionSource;
        RequantisationPolicy = requantisationPolicy;
    }

    public string RuntimeBuildId { get; }

    /// <summary>Lowercase 40-character Git object identity of the runtime source.</summary>
    public string RuntimeSourceCommit { get; }

    public GgufRuntimeBackend Backend { get; }

    public string DeviceId { get; }

    public int ContextSize { get; }

    public GgufCacheType KeyCacheType { get; }

    public GgufCacheType ValueCacheType { get; }

    /// <summary>
    /// The exact number of layers placed on the GPU.
    ///
    /// Exact, not a category. <see cref="GpuOffloadLevel"/> is a planning
    /// bucket, and an executor turning "Partial" into a number would be
    /// choosing how much of the model runs where - after the user confirmed.
    /// <see cref="GgufOffloadPolicy"/> defines the deterministic relationship
    /// and is what fills this in.
    /// </summary>
    public int GpuLayerCount { get; }

    public bool FlashAttention { get; }

    public int ThreadCount { get; }

    public int BatchSize { get; }

    /// <summary>
    /// The grade of the evidence behind this profile, as the runtime contract
    /// expects it: a string, not this assembly's
    /// <see cref="Domain.EvidenceGrade"/>. Kept as text so the value crosses to
    /// G1 unchanged.
    /// </summary>
    public string EvidenceGrade { get; }

    public string ProfileId { get; }

    public int MaximumGeneratedTokens { get; }

    /// <summary>
    /// What the weights would be after conversion, or
    /// <see cref="GgufWeightFormat.Imported"/> when nothing is converted.
    ///
    /// Imported and a named format are different promises: one runs the file
    /// that exists, the other writes a new one.
    /// </summary>
    public GgufWeightFormat PersistentTargetWeightFormat { get; }

    /// <summary>
    /// Required whenever <see cref="RequiresPersistentConversion"/> is true, and
    /// absent otherwise. A quantiser identity on a runtime-only plan would
    /// suggest a conversion nobody agreed to.
    /// </summary>
    public GgufQuantiserIdentity? Quantiser { get; }

    public GgufConversionSourceBinding? ConversionSource { get; }

    public GgufRequantisationPolicy? RequantisationPolicy { get; }

    /// <summary>
    /// Whether executing this writes a new GGUF file. Derived from the target
    /// format rather than supplied alongside it, so the two cannot disagree.
    /// </summary>
    public bool RequiresPersistentConversion =>
        PersistentTargetWeightFormat != GgufWeightFormat.Imported;

    public static GgufExecutionPayload Create(
        string runtimeBuildId,
        string runtimeSourceCommit,
        GgufRuntimeBackend backend,
        string deviceId,
        int contextSize,
        GgufCacheType keyCacheType,
        GgufCacheType valueCacheType,
        int gpuLayerCount,
        bool flashAttention,
        int threadCount,
        int batchSize,
        string evidenceGrade,
        string profileId,
        int maximumGeneratedTokens,
        GgufWeightFormat persistentTargetWeightFormat,
        GgufQuantiserIdentity? quantiser = null,
        GgufConversionSourceBinding? conversionSource = null,
        GgufRequantisationPolicy? requantisationPolicy = null)
    {
        OptimizationIdentifier.Require(
            runtimeBuildId, nameof(runtimeBuildId), "The runtime build");
        OptimizationIdentifier.Require(
            evidenceGrade, nameof(evidenceGrade), "The evidence grade");
        OptimizationIdentifier.Require(profileId, nameof(profileId), "The profile");
        OptimizationIdentifier.Require(deviceId, nameof(deviceId), "The device");

        RequireGitCommit(runtimeSourceCommit, nameof(runtimeSourceCommit));

        RequireDefined(backend, nameof(backend));
        RequireDefined(keyCacheType, nameof(keyCacheType));
        RequireDefined(valueCacheType, nameof(valueCacheType));
        RequireDefined(
            persistentTargetWeightFormat, nameof(persistentTargetWeightFormat));

        if (persistentTargetWeightFormat == GgufWeightFormat.Unspecified)
        {
            throw new ArgumentException(
                "A payload must state its target weight format. Unspecified would "
                + "leave the executor to decide whether it is writing a file.",
                nameof(persistentTargetWeightFormat));
        }

        RequirePositive(contextSize, nameof(contextSize));
        RequirePositive(threadCount, nameof(threadCount));
        RequirePositive(batchSize, nameof(batchSize));
        RequirePositive(maximumGeneratedTokens, nameof(maximumGeneratedTokens));

        if (gpuLayerCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gpuLayerCount),
                gpuLayerCount,
                "A negative layer count describes no placement at all.");
        }

        bool converts = persistentTargetWeightFormat != GgufWeightFormat.Imported;

        if (converts && quantiser is null)
        {
            throw new ArgumentException(
                "A persistent conversion must pin the quantiser that performs it, "
                + "including its executable digest. Without one the executor would "
                + "choose which tool writes the user's model.",
                nameof(quantiser));
        }

        if (!converts && quantiser is not null)
        {
            throw new ArgumentException(
                "A runtime-only payload must carry no quantiser. Naming one would "
                + "advertise a conversion this plan does not perform.",
                nameof(quantiser));
        }

        return new GgufExecutionPayload(
            runtimeBuildId,
            runtimeSourceCommit,
            backend,
            deviceId,
            contextSize,
            keyCacheType,
            valueCacheType,
            gpuLayerCount,
            flashAttention,
            threadCount,
            batchSize,
            evidenceGrade,
            profileId,
            maximumGeneratedTokens,
            persistentTargetWeightFormat,
            quantiser,
            conversionSource,
            requantisationPolicy);
    }

    private static void RequireGitCommit(string value, string parameter)
    {
        bool valid = value is { Length: 40 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        if (!valid)
        {
            throw new ArgumentException(
                "The runtime source commit must be a lowercase 40-character Git "
                + "object identity, matching what the runtime contract requires.",
                parameter);
        }
    }

    private static void RequireDefined<T>(T value, string parameter)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameter, value, "An undefined value cannot be executed.");
        }
    }

    private static void RequirePositive(int value, string parameter)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameter, value, "A non-positive value cannot be executed.");
        }
    }
}
