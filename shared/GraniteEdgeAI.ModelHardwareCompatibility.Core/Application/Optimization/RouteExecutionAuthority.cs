using System.Collections.ObjectModel;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>One GGUF evidence record's exact runtime profile authority.</summary>
public sealed record GgufExecutionProfileAuthority
{
    private GgufExecutionProfileAuthority(
        string evidenceId,
        EvidenceGrade evidence,
        string profileId,
        bool flashAttention,
        int threadCount,
        int batchSize,
        int maximumGeneratedTokens)
    {
        EvidenceId = evidenceId;
        Evidence = evidence;
        ProfileId = profileId;
        FlashAttention = flashAttention;
        ThreadCount = threadCount;
        BatchSize = batchSize;
        MaximumGeneratedTokens = maximumGeneratedTokens;
    }

    public string EvidenceId { get; }

    public EvidenceGrade Evidence { get; }

    public string ProfileId { get; }

    public bool FlashAttention { get; }

    public int ThreadCount { get; }

    public int BatchSize { get; }

    public int MaximumGeneratedTokens { get; }

    public static GgufExecutionProfileAuthority Create(
        string evidenceId,
        EvidenceGrade evidence,
        string profileId,
        bool flashAttention,
        int threadCount,
        int batchSize,
        int maximumGeneratedTokens)
    {
        OptimizationIdentifier.Require(
            evidenceId, nameof(evidenceId), "The GGUF evidence record");
        OptimizationIdentifier.Require(
            profileId, nameof(profileId), "The GGUF runtime profile");
        if (evidence == EvidenceGrade.Unknown || !Enum.IsDefined(evidence))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidence), evidence, "Runtime profile evidence must be established.");
        }
        RequirePositive(threadCount, nameof(threadCount));
        RequirePositive(batchSize, nameof(batchSize));
        RequirePositive(maximumGeneratedTokens, nameof(maximumGeneratedTokens));

        return new GgufExecutionProfileAuthority(
            evidenceId, evidence, profileId, flashAttention,
            threadCount, batchSize, maximumGeneratedTokens);
    }

    private static void RequirePositive(int value, string parameter)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameter, value, "A GGUF profile execution quantity must be positive.");
        }
    }
}

/// <summary>
/// Exact standard or TurboQuant GGUF runtime build and its admitted profiles.
/// </summary>
public sealed record GgufRuntimeAuthority
{
    private GgufRuntimeAuthority(
        string runtimeBuildId,
        string runtimeSourceCommit,
        IReadOnlyDictionary<string, GgufExecutionProfileAuthority> profiles)
    {
        RuntimeBuildId = runtimeBuildId;
        RuntimeSourceCommit = runtimeSourceCommit;
        Profiles = profiles;
    }

    public string RuntimeBuildId { get; }

    public string RuntimeSourceCommit { get; }

    public IReadOnlyDictionary<string, GgufExecutionProfileAuthority> Profiles { get; }

    public static GgufRuntimeAuthority Create(
        string runtimeBuildId,
        string runtimeSourceCommit,
        IReadOnlyList<GgufExecutionProfileAuthority> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        OptimizationIdentifier.Require(
            runtimeBuildId, nameof(runtimeBuildId), "The GGUF runtime build");
        OptimizationGitCommit.Require(runtimeSourceCommit, nameof(runtimeSourceCommit));
        SortedDictionary<string, GgufExecutionProfileAuthority> copied =
            new(StringComparer.Ordinal);
        foreach (GgufExecutionProfileAuthority profile in profiles)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (!copied.TryAdd(profile.EvidenceId, profile))
            {
                throw new ArgumentException(
                    "GGUF execution profile evidence identifiers must be unique.",
                    nameof(profiles));
            }
        }
        if (copied.Count == 0)
        {
            throw new ArgumentException(
                "GGUF runtime authority requires at least one exact profile.",
                nameof(profiles));
        }

        return new GgufRuntimeAuthority(
            runtimeBuildId, runtimeSourceCommit,
            new ReadOnlyDictionary<string, GgufExecutionProfileAuthority>(copied));
    }
}

internal static class GgufEvidenceGradeMap
{
    internal static string ToExecutionValue(EvidenceGrade evidence) => evidence switch
    {
        EvidenceGrade.Estimated => "Estimated",
        EvidenceGrade.Measured => "Measured",
        EvidenceGrade.Verified => "Verified",
        _ => throw new ArgumentOutOfRangeException(
            nameof(evidence), evidence, "Unknown evidence cannot authorize execution.")
    };
}

internal static class OptimizationGitCommit
{
    internal static void Require(string value, string parameter)
    {
        bool valid = value is { Length: 40 }
            && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (!valid)
        {
            throw new ArgumentException(
                "A runtime source commit must be a lowercase 40-character Git object ID.",
                parameter);
        }
    }
}

/// <summary>Exact executable authority for one admitted OpenVINO evidence record.</summary>
public sealed record OpenVinoExecutionAuthority
{
    private OpenVinoExecutionAuthority(
        string evidenceId,
        string configurationId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoBuildIdentity buildIdentity,
        IReadOnlyDictionary<string, string> optimizerVersions,
        bool compiledCacheIsDisposable,
        TurboQuantBuildIdentity? turboQuantBuild)
    {
        EvidenceId = evidenceId;
        ConfigurationId = configurationId;
        SourceWeightPrecision = sourceWeightPrecision;
        BuildIdentity = buildIdentity;
        OptimizerVersions = optimizerVersions;
        CompiledCacheIsDisposable = compiledCacheIsDisposable;
        TurboQuantBuild = turboQuantBuild;
    }

    public string EvidenceId { get; }
    public string ConfigurationId { get; }
    public OpenVinoWeightPrecision SourceWeightPrecision { get; }
    public OpenVinoBuildIdentity BuildIdentity { get; }
    public IReadOnlyDictionary<string, string> OptimizerVersions { get; }
    public bool CompiledCacheIsDisposable { get; }
    public TurboQuantBuildIdentity? TurboQuantBuild { get; }

    public static OpenVinoExecutionAuthority Create(
        string evidenceId,
        string configurationId,
        OpenVinoWeightPrecision sourceWeightPrecision,
        OpenVinoBuildIdentity buildIdentity,
        IReadOnlyDictionary<string, string> optimizerVersions,
        bool compiledCacheIsDisposable,
        TurboQuantBuildIdentity? turboQuantBuild = null)
    {
        ArgumentNullException.ThrowIfNull(buildIdentity);
        ArgumentNullException.ThrowIfNull(optimizerVersions);
        OptimizationIdentifier.Require(
            evidenceId, nameof(evidenceId), "The OpenVINO evidence record");
        OptimizationIdentifier.Require(
            configurationId, nameof(configurationId), "The OpenVINO configuration");
        if (!Enum.IsDefined(sourceWeightPrecision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceWeightPrecision), sourceWeightPrecision,
                "OpenVINO source precision must be defined.");
        }
        SortedDictionary<string, string> copied = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> version in optimizerVersions)
        {
            OptimizationIdentifier.Require(
                version.Key, nameof(optimizerVersions), "An optimizer name");
            OptimizationIdentifier.Require(
                version.Value, nameof(optimizerVersions), "An optimizer version");
            if (!copied.TryAdd(version.Key, version.Value))
            {
                throw new ArgumentException(
                    "OpenVINO optimizer authority keys must be unique.",
                    nameof(optimizerVersions));
            }
        }
        if (copied.Count == 0)
        {
            throw new ArgumentException(
                "OpenVINO execution authority requires the complete optimizer map.",
                nameof(optimizerVersions));
        }

        return new OpenVinoExecutionAuthority(
            evidenceId, configurationId, sourceWeightPrecision,
            buildIdentity,
            new ReadOnlyDictionary<string, string>(copied),
            compiledCacheIsDisposable,
            turboQuantBuild);
    }
}
