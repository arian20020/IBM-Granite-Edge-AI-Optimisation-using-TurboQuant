using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Turns a configuration into the exact bytes its digest is taken over.
///
/// The rules exist so two processes agree. An executor recomputes this digest on
/// a different machine, in a different culture, possibly in a different build,
/// and compares it ordinally to the one in the plan - so anything that could
/// vary between those runs is excluded or pinned.
///
/// Fields are emitted in a fixed order with invariant formatting. No paths, no
/// free text a user typed, no timestamps: those would make the digest differ for
/// two configurations that are in fact identical, and drift would be reported
/// where none exists.
///
/// Every execution-affecting field is inside the digest. That is the point of
/// version 2: in version 1 the digest covered a coarse route configuration, so
/// an executor could change a thread count, a cache type or a GPU layer count
/// without the plan's identity changing. A setting outside the digest is a
/// setting nobody confirmed.
/// </summary>
internal static class OptimizationCanonicalizer
{
    /// <summary>The immutable version-2 layout retained for digest verification.</summary>
    internal static string CanonicalizeV2(
        OptimizationCandidate candidate, OptimizationExecutionPayload payload)
    {
        RequireVersionTwoCacheVocabulary(payload);
        return CanonicalizeCore(
            candidate, payload, contractVersion: 2, includeV3CacheAlgorithm: false);
    }

    internal static string CanonicalizeV3(
        OptimizationCandidate candidate, OptimizationExecutionPayload payload) =>
        CanonicalizeCore(candidate, payload, contractVersion: 3, includeV3CacheAlgorithm: true);

    internal static string ConfigurationSha256V2(
        OptimizationCandidate candidate, OptimizationExecutionPayload payload)
    {
        RequireVersionTwoCacheVocabulary(payload);
        byte[] hash = SHA256.HashData(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(CanonicalizeV2(candidate, payload)));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void RequireVersionTwoCacheVocabulary(
        OptimizationExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        payload.OpenVino?.RequireVersionTwoCacheVocabulary();
    }

    /// <summary>
    /// Canonical form of the complete confirmed work: the candidate, the route
    /// payload, and the contract version that fixes how they are laid out.
    /// </summary>
    internal static string Canonicalize(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        int contractVersion) => contractVersion switch
        {
            2 => CanonicalizeV2(candidate, payload),
            3 => CanonicalizeV3(candidate, payload),
            _ => throw new ArgumentOutOfRangeException(
                nameof(contractVersion),
                contractVersion,
                "No canonical layout is defined for this plan contract version.")
        };

    private static string CanonicalizeCore(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        int contractVersion,
        bool includeV3CacheAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(payload);

        StringBuilder builder = new();

        // The version leads, so a layout change cannot produce a digest that
        // collides with one from a different layout.
        Append(builder, "v", contractVersion);
        Append(builder, "route", (int)candidate.Route);
        Append(builder, "config", candidate.Configuration.CanonicalDescriptor);
        Append(builder, "ctx", candidate.Metrics.ContextTokens);
        Append(builder, "persistent", candidate.Metrics.RequiresPersistentChange ? 1 : 0);
        Append(builder, "evidence", candidate.EvidenceId);
        Append(builder, "experimental", candidate.IsExperimental ? 1 : 0);

        switch (payload.Route)
        {
            case OptimizationRoute.Gguf:
                AppendGguf(builder, payload.Gguf!);
                break;

            case OptimizationRoute.OpenVino:
                AppendOpenVino(builder, payload.OpenVino!, includeV3CacheAlgorithm);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    payload.Route,
                    "A payload with no canonical form would hash to the candidate "
                    + "alone, leaving every runtime setting outside the digest.");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Lowercase hex, matching every other digest in this contract. A digest
    /// differing only in case would fail an ordinal comparison and report drift
    /// on a plan that had not changed.
    /// </summary>
    internal static string ConfigurationSha256(
        OptimizationCandidate candidate,
        OptimizationExecutionPayload payload,
        int contractVersion)
    {
        byte[] hash = SHA256.HashData(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(Canonicalize(candidate, payload, contractVersion)));

        // ToHexStringLower is .NET 9; this targets net8.0.
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Every GGUF execution field, in a fixed order. A field missing here is a
    /// field an executor could change without changing the plan's identity.
    /// </summary>
    private static void AppendGguf(StringBuilder builder, GgufExecutionPayload gguf)
    {
        Append(builder, "gguf.runtimeBuildId", gguf.RuntimeBuildId);
        Append(builder, "gguf.runtimeSourceCommit", gguf.RuntimeSourceCommit);
        Append(builder, "gguf.backend", (int)gguf.Backend);
        Append(builder, "gguf.deviceId", gguf.DeviceId);
        Append(builder, "gguf.contextSize", gguf.ContextSize);
        Append(builder, "gguf.keyCacheType", (int)gguf.KeyCacheType);
        Append(builder, "gguf.valueCacheType", (int)gguf.ValueCacheType);
        Append(builder, "gguf.gpuLayerCount", gguf.GpuLayerCount);
        Append(builder, "gguf.flashAttention", gguf.FlashAttention ? 1 : 0);
        Append(builder, "gguf.threadCount", gguf.ThreadCount);
        Append(builder, "gguf.batchSize", gguf.BatchSize);
        Append(builder, "gguf.evidenceGrade", gguf.EvidenceGrade);
        Append(builder, "gguf.profileId", gguf.ProfileId);
        Append(builder, "gguf.maximumGeneratedTokens", gguf.MaximumGeneratedTokens);
        Append(
            builder,
            "gguf.persistentTargetWeightFormat",
            (int)gguf.PersistentTargetWeightFormat);

        // Absence is itself canonicalised. If a missing quantiser emitted
        // nothing, a runtime-only plan and a converting plan whose quantiser
        // fields happened to be empty would hash identically.
        if (gguf.Quantiser is { } quantiser)
        {
            Append(builder, "gguf.quantiser.packageId", quantiser.PackageId);
            Append(builder, "gguf.quantiser.toolVersion", quantiser.ToolVersion);
            Append(builder, "gguf.quantiser.executableSha256", quantiser.ExecutableSha256);
        }
        else
        {
            Append(builder, "gguf.quantiser", "none");
        }
    }

    /// <summary>
    /// Every OpenVINO execution field, in a fixed order, under the published
    /// route's own names.
    /// </summary>
    private static void AppendOpenVino(
        StringBuilder builder,
        OpenVinoExecutionPayload openVino,
        bool includeV3CacheAlgorithm)
    {
        Append(builder, "ov.configurationId", openVino.ConfigurationId);
        Append(builder, "ov.device", openVino.Device);
        Append(builder, "ov.maturity", openVino.Maturity);
        Append(builder, "ov.evidenceId", openVino.EvidenceId);
        Append(builder, "ov.sourceWeightPrecision", (int)openVino.SourceWeightPrecision);
        Append(builder, "ov.targetWeightPrecision", (int)openVino.TargetWeightPrecision);

        if (includeV3CacheAlgorithm)
        {
            Append(builder, "ov.kvCacheAlgorithm", (int)openVino.KvCacheAlgorithm);
        }

        Append(builder, "ov.kvCachePrecision", (int)openVino.KvCachePrecision);
        Append(builder, "ov.compiledCacheEnabled", openVino.CompiledCacheEnabled ? 1 : 0);
        Append(
            builder,
            "ov.compiledCacheIsDisposable",
            openVino.CompiledCacheIsDisposable ? 1 : 0);
        Append(
            builder,
            "ov.compiledCacheIsModelArtifact",
            openVino.CompiledCacheIsModelArtifact ? 1 : 0);
        Append(builder, "ov.createsCompletePackage", openVino.CreatesCompletePackage ? 1 : 0);
        Append(builder, "ov.build.runtimeBuild", openVino.BuildIdentity.RuntimeBuild);
        Append(builder, "ov.build.genAiBuild", openVino.BuildIdentity.GenAiBuild);
        Append(builder, "ov.build.tokenizersBuild", openVino.BuildIdentity.TokenizersBuild);
        Append(
            builder,
            "ov.build.workerManifestDigest",
            openVino.BuildIdentity.WorkerManifestDigest);

        // Ordinal key order, so a dictionary enumerated in a different order on
        // another machine still produces the same bytes.
        //
        // Sorted through a keys list rather than a LINQ key selector: a lambda
        // returning a string is a string-carrying member the privacy canary has
        // to review, and a compiler-generated name shifts whenever surrounding
        // code moves. Nothing is gained by making the allowlist track that.
        List<string> optimizerNames = [.. openVino.OptimizerVersions.Keys];
        optimizerNames.Sort(StringComparer.Ordinal);

        foreach (string name in optimizerNames)
        {
            Append(builder, "ov.optimizer." + name, openVino.OptimizerVersions[name]);
        }

        if (openVino.TurboQuantBuild is { } turboQuant)
        {
            Append(builder, "ov.turboQuant.sourceCommit", turboQuant.SourceCommit);
            Append(
                builder,
                "ov.turboQuant.implementationCommit",
                turboQuant.ImplementationCommit);
            Append(builder, "ov.turboQuant.patchSeriesDigest", turboQuant.PatchSeriesDigest);
            Append(
                builder,
                "ov.turboQuant.runtimeManifestDigest",
                turboQuant.RuntimeManifestDigest);
        }
        else
        {
            Append(builder, "ov.turboQuant", "none");
        }
    }

    private static void Append(StringBuilder builder, string field, int value) =>
        Append(builder, field, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, string field, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append('|');
        }

        // Length-prefixed so a value containing the separator cannot be read as
        // two fields, and two different field layouts cannot produce the same
        // bytes.
        builder
            .Append(field)
            .Append('=')
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
