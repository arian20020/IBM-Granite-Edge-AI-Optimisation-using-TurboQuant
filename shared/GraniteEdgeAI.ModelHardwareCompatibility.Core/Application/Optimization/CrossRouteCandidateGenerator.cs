using System.Collections;
using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>One candidate that was considered and refused, and why.</summary>
public sealed record OptimizationExclusion(
    string EvidenceId, string CanonicalDescriptor, OptimizationExclusionReason Reason)
{
    /// <summary>
    /// Established system/shared-memory peak for a candidate rejected only
    /// because it exceeded the current safe budget. Other exclusion kinds do
    /// not carry a number: presenting one there would turn missing evidence
    /// into an estimate.
    /// </summary>
    public ulong? EstimatedRequiredBytes { get; init; }

    /// <summary>
    /// Established peak storage obligation and the exact free-disk observation
    /// for a candidate rejected by the storage gate. They remain separate from
    /// runtime RAM so presentation cannot mistake one shortage for the other.
    /// </summary>
    public ulong? EstimatedDiskRequiredBytes { get; init; }

    public ulong? AvailableDiskBytes { get; init; }
}

/// <summary>
/// Exact, path-free machine and observation authority consumed while generating
/// a frontier. RAM and dedicated memory remain separate safety axes.
/// </summary>
internal sealed record OptimizationHardwareAuthority
{
    private OptimizationHardwareAuthority(
        string hardwareFactsSha256,
        IReadOnlySet<DeviceRouteId> presentDevices,
        IReadOnlySet<CompatibilityBackend> verifiedBackends,
        ByteCount? safeDedicatedDeviceMemoryBudget,
        DateTimeOffset observedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        string freshnessPolicyVersion)
    {
        HardwareFactsSha256 = hardwareFactsSha256;
        PresentDevices = presentDevices;
        VerifiedBackends = verifiedBackends;
        SafeDedicatedDeviceMemoryBudget = safeDedicatedDeviceMemoryBudget;
        ObservedAtUtc = observedAtUtc;
        EvaluatedAtUtc = evaluatedAtUtc;
        FreshnessPolicyVersion = freshnessPolicyVersion;
    }

    internal string HardwareFactsSha256 { get; }
    internal IReadOnlySet<DeviceRouteId> PresentDevices { get; }
    internal IReadOnlySet<CompatibilityBackend> VerifiedBackends { get; }
    internal ByteCount? SafeDedicatedDeviceMemoryBudget { get; }
    internal DateTimeOffset ObservedAtUtc { get; }
    internal DateTimeOffset EvaluatedAtUtc { get; }
    internal string FreshnessPolicyVersion { get; }

    internal static OptimizationHardwareAuthority Create(
        string hardwareFactsSha256,
        IEnumerable<DeviceRouteId> presentDevices,
        IEnumerable<CompatibilityBackend> verifiedBackends,
        ByteCount? safeDedicatedDeviceMemoryBudget,
        DateTimeOffset observedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        string freshnessPolicyVersion)
    {
        if (hardwareFactsSha256 is null || hardwareFactsSha256.Length != 64
            || hardwareFactsSha256.Any(
                c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("Hardware facts digest must be lowercase SHA-256.", nameof(hardwareFactsSha256));
        }
        ArgumentNullException.ThrowIfNull(presentDevices);
        ArgumentNullException.ThrowIfNull(verifiedBackends);
        if (string.IsNullOrWhiteSpace(freshnessPolicyVersion))
        {
            throw new ArgumentException("Freshness policy is required.", nameof(freshnessPolicyVersion));
        }

        FrozenSet<DeviceRouteId> devices = presentDevices.ToFrozenSet();
        FrozenSet<CompatibilityBackend> backends = verifiedBackends.ToFrozenSet();
        if (devices.Count == 0
            || devices.Any(device =>
                !Enum.IsDefined(device) || device == DeviceRouteId.Unspecified)
            || backends.Any(backend =>
                !Enum.IsDefined(backend)
                || backend == CompatibilityBackend.Unspecified))
        {
            throw new ArgumentException("Hardware inventory contains an undefined value.");
        }
        return new OptimizationHardwareAuthority(
            hardwareFactsSha256, devices, backends,
            safeDedicatedDeviceMemoryBudget, observedAtUtc.ToUniversalTime(),
            evaluatedAtUtc.ToUniversalTime(), freshnessPolicyVersion);
    }

    internal bool Supports(RouteConfiguration configuration) => configuration switch
    {
        GgufRouteConfiguration gguf =>
            PresentDevices.Contains(gguf.Device)
            && VerifiedBackends.Contains(gguf.Backend)
            && ((gguf.Backend == CompatibilityBackend.Cpu && gguf.Device == DeviceRouteId.Cpu)
                || (gguf.Backend is CompatibilityBackend.IntelSycl or CompatibilityBackend.IntelVulkan
                    && gguf.Device is DeviceRouteId.IntelIntegratedGpu or DeviceRouteId.IntelDiscreteGpu)),
        OpenVinoRouteConfiguration openVino =>
            PresentDevices.Contains(openVino.Device)
            && VerifiedBackends.Contains(openVino.Device switch
            {
                DeviceRouteId.Cpu => CompatibilityBackend.OpenVinoCpu,
                DeviceRouteId.IntelIntegratedGpu or DeviceRouteId.IntelDiscreteGpu => CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelNpu => CompatibilityBackend.OpenVinoNpu,
                _ => CompatibilityBackend.Unspecified
            }),
        _ => false
    };
}

/// <summary>
/// Opaque, path-free issuance authority rebuilt from the current hardware
/// observation. It binds the exact generation budgets and inventory without
/// putting raw hardware facts into an execution plan.
/// </summary>
public sealed record OptimizationIssuanceAuthority
{
    private OptimizationIssuanceAuthority(
        string authoritySha256,
        DateTimeOffset observedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        string freshnessPolicyVersion)
    {
        AuthoritySha256 = authoritySha256;
        ObservedAtUtc = observedAtUtc;
        EvaluatedAtUtc = evaluatedAtUtc;
        FreshnessPolicyVersion = freshnessPolicyVersion;
    }

    internal string AuthoritySha256 { get; }
    internal DateTimeOffset ObservedAtUtc { get; }
    internal DateTimeOffset EvaluatedAtUtc { get; }
    internal string FreshnessPolicyVersion { get; }

    /// <summary>
    /// Rebuilds issuance authority from a fresh owner-supplied hardware-facts
    /// digest and the exact current, path-free generation resources. The fixed
    /// policy identifier cannot be caller-substituted.
    /// </summary>
    public static OptimizationIssuanceAuthority CreateCurrent(
        string hardwareFactsSha256,
        IEnumerable<DeviceRouteId> presentDevices,
        IEnumerable<CompatibilityBackend> verifiedBackends,
        ulong safeSystemSharedBudgetBytes,
        ulong? safeDedicatedDeviceMemoryBudgetBytes,
        ulong availableDiskBytes,
        DateTimeOffset observedAtUtc,
        DateTimeOffset evaluatedAtUtc)
    {
        OptimizationHardwareAuthority hardware =
            OptimizationHardwareAuthority.Create(
                hardwareFactsSha256,
                presentDevices,
                verifiedBackends,
                safeDedicatedDeviceMemoryBudgetBytes.HasValue
                    ? ByteCount.FromBytes(
                        safeDedicatedDeviceMemoryBudgetBytes.Value)
                    : null,
                observedAtUtc,
                evaluatedAtUtc,
                OptimizationFreshnessPolicy.Version);
        return FromGeneration(
            hardware,
            ByteCount.FromBytes(safeSystemSharedBudgetBytes),
            ByteCount.FromBytes(availableDiskBytes));
    }

    internal static OptimizationIssuanceAuthority FromGeneration(
        OptimizationHardwareAuthority hardware,
        ByteCount safeSystemSharedBudget,
        ByteCount availableDisk)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        if (safeSystemSharedBudget == ByteCount.Zero
            || availableDisk == ByteCount.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(safeSystemSharedBudget),
                "Issuance authority requires established non-zero budgets.");
        }

        return new OptimizationIssuanceAuthority(
            Digest(
                hardware.HardwareFactsSha256,
                hardware.PresentDevices,
                hardware.VerifiedBackends,
                safeSystemSharedBudget,
                hardware.SafeDedicatedDeviceMemoryBudget,
                availableDisk,
                hardware.ObservedAtUtc,
                hardware.EvaluatedAtUtc,
                hardware.FreshnessPolicyVersion),
            hardware.ObservedAtUtc,
            hardware.EvaluatedAtUtc,
            hardware.FreshnessPolicyVersion);
    }

    internal bool IsFreshAt(DateTimeOffset nowUtc) =>
        string.Equals(
            FreshnessPolicyVersion,
            OptimizationFreshnessPolicy.Version,
            StringComparison.Ordinal)
        && nowUtc.Offset == TimeSpan.Zero
        && ObservedAtUtc <= nowUtc + OptimizationFreshnessPolicy.FutureClockSkew
        && nowUtc - ObservedAtUtc <= OptimizationFreshnessPolicy.MaximumAge;

    private static string Digest(
        string hardwareFactsSha256,
        IReadOnlySet<DeviceRouteId> devices,
        IReadOnlySet<CompatibilityBackend> backends,
        ByteCount safeSystemSharedBudget,
        ByteCount? safeDedicatedDeviceMemoryBudget,
        ByteCount availableDisk,
        DateTimeOffset observedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        string freshnessPolicyVersion)
    {
        StringBuilder canonical = new();
        void Add(string value) => canonical.Append(value.Length)
            .Append(':').Append(value).Append('|');

        Add("hardware-issuance-authority-v1");
        Add(hardwareFactsSha256);
        Add(string.Join(",", devices.OrderBy(value => value)
            .Select(value => ((int)value).ToString(CultureInfo.InvariantCulture))));
        Add(string.Join(",", backends.OrderBy(value => value)
            .Select(value => ((int)value).ToString(CultureInfo.InvariantCulture))));
        Add(safeSystemSharedBudget.Bytes.ToString(CultureInfo.InvariantCulture));
        Add(safeDedicatedDeviceMemoryBudget?.Bytes.ToString(
            CultureInfo.InvariantCulture) ?? "none");
        Add(availableDisk.Bytes.ToString(CultureInfo.InvariantCulture));
        Add(observedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add(evaluatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add(freshnessPolicyVersion);

        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}

internal static class OptimizationFreshnessPolicy
{
    internal static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan FutureClockSkew = TimeSpan.FromSeconds(5);
    internal const string Version = "hardware-dynamic-memory-freshness-v1";
}

/// <summary>
/// Canonical digest of every input that can change generation and the exact
/// ordered output produced. The capability payload is hashed independently of
/// the caller-supplied evidence digest, so replaying that digest beside a
/// changed payload or changed result cannot preserve authority.
/// </summary>
internal static class OptimizationGenerationDigest
{
    internal static string Compute(
        OptimizationCapabilitySnapshot snapshot,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority,
        IReadOnlyList<OptimizationCandidate> candidates,
        IReadOnlyList<OptimizationExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(exclusions);

        StringBuilder canonical = new();
        Append(canonical, "generation-authority-v2");
        AppendObject(canonical, snapshot);
        // The sealed route payload is deliberately hashed a second time under
        // its own label. This makes the distinction between an asserted
        // capability digest and the payload actually consumed reviewable.
        Append(canonical, PayloadDigest(snapshot));
        AppendObject(canonical, facts);
        AppendObject(canonical, workload);
        AppendObject(canonical, binding);
        AppendObject(canonical, safeBudget);
        AppendObject(canonical, availableDisk);
        AppendObject(canonical, policy);
        AppendObject(canonical, hardwareAuthority);
        foreach (string evidenceId in optedInExperimentalEvidenceIds
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(canonical, evidenceId);
        }
        // Output order is part of the contract. The generator emits candidates
        // by canonical descriptor and exclusions by evidence/descriptor/reason;
        // a reordered or substituted row therefore cannot reuse the stamp.
        AppendObject(canonical, candidates);
        AppendObject(canonical, exclusions);

        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static string PayloadDigest(OptimizationCapabilitySnapshot snapshot)
    {
        StringBuilder canonical = new();
        AppendObject(canonical, snapshot.Gguf ?? (object?)snapshot.OpenVino);
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendObject(StringBuilder canonical, object? value)
    {
        if (value is null)
        {
            Append(canonical, "null");
            return;
        }

        Type type = value.GetType();
        if (value is string text)
        {
            Append(canonical, text);
            return;
        }

        if (value is Type runtimeType)
        {
            Append(canonical, runtimeType.FullName ?? runtimeType.Name);
            return;
        }

        if (value is EstimatorPolicy estimatorPolicy)
        {
            Append(canonical, estimatorPolicy.PolicyVersion);
            Append(canonical, ((int)estimatorPolicy.Provenance).ToString(
                CultureInfo.InvariantCulture));
            if (estimatorPolicy.Provenance != PolicyProvenance.Absent)
            {
                AppendObject(canonical, estimatorPolicy.Terms);
            }
            return;
        }

        if (type.IsEnum || type.IsPrimitive || value is decimal
            || value is DateTimeOffset || value is Guid)
        {
            Append(canonical, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
            return;
        }

        if (value is IEnumerable enumerable)
        {
            bool unordered = ImplementsSetOrDictionary(type);
            Append(canonical, unordered ? "unordered" : "sequence");
            List<string> entries = [];
            foreach (object? item in enumerable)
            {
                StringBuilder entry = new();
                AppendObject(entry, item);
                entries.Add(entry.ToString());
            }

            if (unordered)
            {
                entries.Sort(StringComparer.Ordinal);
            }

            foreach (string entry in entries)
            {
                Append(canonical, entry);
            }
            return;
        }

        Append(canonical, type.FullName ?? type.Name);
        foreach (PropertyInfo property in type.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetMethod is not null
                && property.Name != "EqualityContract"
                && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            Append(canonical, property.Name);
            AppendObject(canonical, property.GetValue(value));
        }
    }

    private static bool ImplementsSetOrDictionary(Type type) =>
        type.GetInterfaces().Any(contract => contract.IsGenericType
            && contract.GetGenericTypeDefinition() is { } definition
            && (definition == typeof(ISet<>)
                || definition == typeof(IReadOnlySet<>)
                || definition == typeof(IDictionary<,>)
                || definition == typeof(IReadOnlyDictionary<,>)));

    private static void Append(StringBuilder canonical, string value)
    {
        canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        canonical.Append(':');
        canonical.Append(value);
    }
}

/// <summary>
/// Everything the planner considered: what survived, and what did not and why.
/// </summary>
public sealed record CrossRouteGenerationResult
{
    internal CrossRouteGenerationResult(
        IReadOnlyList<OptimizationCandidate> candidates,
        IReadOnlyList<OptimizationExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(exclusions);

        Candidates = Array.AsReadOnly([.. candidates]);
        Exclusions = Array.AsReadOnly([.. exclusions]);
    }

    public IReadOnlyList<OptimizationCandidate> Candidates { get; }

    /// <summary>
    /// Kept rather than discarded. "No setup fits" and "we could not assess
    /// anything" are different answers, and only the exclusion list tells them
    /// apart.
    /// </summary>
    public IReadOnlyList<OptimizationExclusion> Exclusions { get; }

}

/// <summary>
/// What this machine could actually be asked to run.
///
/// One generator for both routes, because the alternative is two that drift.
/// It never invents a combination: every candidate comes from an entry the
/// capability snapshot admitted, and an entry naming a combination the route
/// records cannot construct is refused rather than approximated.
///
/// Unknown fails closed throughout. An estimate that could not be established
/// becomes a typed exclusion, never a zero - a zero would rank as the cheapest
/// option available and win every efficiency band.
/// </summary>
internal static class CrossRouteCandidateGenerator
{
    private sealed record GenerationAuthority(string AuthoritySha256);

    private static readonly ConditionalWeakTable<
        CrossRouteGenerationResult, GenerationAuthority> Authorities = new();

    internal static bool HasMatchingAuthority(
        CrossRouteGenerationResult result,
        OptimizationCapabilitySnapshot snapshot,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Authorities.TryGetValue(result, out GenerationAuthority? authority)
            && string.Equals(
                authority.AuthoritySha256,
                OptimizationGenerationDigest.Compute(
                    snapshot, facts, workload, binding, safeBudget, availableDisk,
                    policy, optedInExperimentalEvidenceIds, hardwareAuthority, result.Candidates,
                    result.Exclusions),
                StringComparison.Ordinal);
    }

    internal static CrossRouteGenerationResult Generate(
        OptimizationCapabilitySnapshot snapshot,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority,
        OptimizationEvidenceCatalog? qualityEvidence = null,
        ulong? inspectedParameterCount = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);
        ArgumentNullException.ThrowIfNull(hardwareAuthority);
        foreach (string evidenceId in optedInExperimentalEvidenceIds)
        {
            OptimizationIdentifier.Require(
                evidenceId,
                nameof(optedInExperimentalEvidenceIds),
                "An experimental capability opt-in");
        }

        List<OptimizationCandidate> candidates = [];
        List<OptimizationExclusion> exclusions = [];

        foreach (ContextTokenCount context in workload.CandidateContexts)
        {
            switch (snapshot.Route)
            {
                case OptimizationRoute.Gguf:
                    GenerateGguf(
                        snapshot, snapshot.Gguf!, facts, workload, binding, context,
                        safeBudget, availableDisk,
                        policy, optedInExperimentalEvidenceIds, hardwareAuthority,
                        qualityEvidence, inspectedParameterCount, candidates, exclusions);
                    break;

                case OptimizationRoute.OpenVino:
                    GenerateOpenVino(
                        snapshot, snapshot.OpenVino!, facts, workload, binding, context,
                        safeBudget, availableDisk,
                        policy, optedInExperimentalEvidenceIds, hardwareAuthority,
                        qualityEvidence, inspectedParameterCount, candidates, exclusions);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(snapshot),
                        snapshot.Route,
                        "A route with no generation arm would silently produce nothing "
                        + "and report it as a machine that fits nothing.");
            }
        }

        // De-duplicated by complete configuration, so the same setup reached
        // through two admitted entries is offered once rather than competing
        // with itself for a band.
        List<OptimizationCandidate> distinct =
        [
            .. candidates
                .GroupBy(
                    candidate => candidate.CanonicalDescriptor,
                    StringComparer.Ordinal)
                .Select(group => group.Aggregate(
                    (best, candidate) =>
                        OptimizationPreferenceResolver.PrefersFirst(candidate, best)
                            ? candidate
                            : best))
                .OrderBy(
                    candidate => candidate.CanonicalDescriptor,
                    StringComparer.Ordinal)
        ];

        List<OptimizationExclusion> orderedExclusions =
        [
            .. exclusions
                .OrderBy(exclusion => exclusion.EvidenceId, StringComparer.Ordinal)
                .ThenBy(exclusion => exclusion.CanonicalDescriptor, StringComparer.Ordinal)
                .ThenBy(exclusion => exclusion.Reason)
        ];

        CrossRouteGenerationResult result = new(distinct, orderedExclusions);
        Authorities.Add(
            result,
            new GenerationAuthority(OptimizationGenerationDigest.Compute(
                snapshot, facts, workload, binding, safeBudget, availableDisk,
                policy, optedInExperimentalEvidenceIds, hardwareAuthority, distinct,
                orderedExclusions)));
        return result;
    }

    private static void GenerateOpenVino(
        OptimizationCapabilitySnapshot snapshot,
        OpenVinoCapabilityPayload payload,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ContextTokenCount context,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedIn,
        OptimizationHardwareAuthority hardwareAuthority,
        OptimizationEvidenceCatalog? qualityEvidence,
        ulong? inspectedParameterCount,
        List<OptimizationCandidate> candidates,
        List<OptimizationExclusion> exclusions)
    {
        foreach (OpenVinoAdmittedConfiguration admitted in payload.Admitted)
        {
            if (!payload.ExecutionAuthorities.TryGetValue(
                    admitted.EvidenceId, out OpenVinoExecutionAuthority? execution))
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    $"weights={admitted.Weights}|ctx={context.Tokens}",
                    OptimizationExclusionReason.ExecutionAuthorityNotEstablished));
                continue;
            }

            if (context.Tokens < admitted.MinimumContextTokens
                || context.Tokens > admitted.MaximumContextTokens)
            {
                continue;
            }

            OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
                admitted.Weights,
                admitted.KvCache,
                admitted.Device,
                admitted.PerformanceHint,
                admitted.CompiledCache,
                admitted.Streams);

            string descriptor = $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}";

            if (Refused(
                admitted.Level, admitted.RequiresEvidence, admitted.EvidenceId,
                optedIn, descriptor, exclusions))
            {
                continue;
            }

            ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
                facts, configuration, context, policy);

            OptimizationEvidenceRecord? measuredEvidence =
                ResolveOpenVinoQualityEvidence(
                    qualityEvidence, binding, execution, admitted, context,
                    inspectedParameterCount);
            if (measuredEvidence is null)
            {
                // A verified original FP16 run does not need conversion-quality
                // rankings. Only distinguish genuine absence: any FP16 record
                // (including a mismatched or failed one) retains the refusal.
                bool originalQualityAbsent = admitted.Weights == OpenVinoWeightFormat.Original
                    && admitted.KvCache == OpenVinoKvCacheFormat.RouteDefault
                    && inspectedParameterCount is > 0
                    && VerifiedOpenVinoOptimizationEvidence.MatchesObservedClosure(execution)
                    && qualityEvidence is not null
                    && !qualityEvidence.ContainsWeightEvidence(OptimizationRoute.OpenVino, "fp16");
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    descriptor,
                    originalQualityAbsent
                        ? OptimizationExclusionReason.CurrentModelQualityEvidenceUnavailable
                        : OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
                continue;
            }
            if (measuredEvidence is { IsAdmitted: false })
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    descriptor,
                    OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
                continue;
            }

            if (admitted.Level != SupportLevel.Experimental
                && admitted.KvCache is OpenVinoKvCacheFormat.TurboQuantTbq3 or OpenVinoKvCacheFormat.TurboQuantTbq4
                && !VerifiedOpenVinoOptimizationEvidence.IsReleasedTurboQuantEvidence(measuredEvidence, execution))
            {
                exclusions.Add(new OptimizationExclusion(admitted.EvidenceId, descriptor,
                    OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
                continue;
            }

            OptimizationAssessment quality = CoarseQuality(measuredEvidence.Quality.Level);

            Admit(
                configuration,
                estimate,
                context,
                admitted.EvidenceId,
                admitted.Level,
                quality,
                RequiresOpenVinoWeightConversion(admitted.Weights, execution.SourceWeightPrecision),
                workload,
                safeBudget,
                availableDisk,
                descriptor,
                candidates,
                exclusions,
                snapshot,
                binding,
                admitted.RequiresEvidence,
                optedIn,
                hardwareAuthority,
                measuredEvidence: measuredEvidence);
        }
    }

    private static bool RequiresOpenVinoWeightConversion(
        OpenVinoWeightFormat target, OpenVinoWeightPrecision source) => target switch
    {
        OpenVinoWeightFormat.Original => false,
        OpenVinoWeightFormat.Fp16 => source != OpenVinoWeightPrecision.Fp16,
        OpenVinoWeightFormat.Int8 => source != OpenVinoWeightPrecision.EightBit,
        OpenVinoWeightFormat.Int4 => source != OpenVinoWeightPrecision.FourBit,
        OpenVinoWeightFormat.MxFp4 => source != OpenVinoWeightPrecision.MxFp4,
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    private static OptimizationEvidenceRecord? ResolveOpenVinoQualityEvidence(
        OptimizationEvidenceCatalog? catalog,
        OptimizationJourneyBinding binding,
        OpenVinoExecutionAuthority execution,
        OpenVinoAdmittedConfiguration admitted,
        ContextTokenCount context,
        ulong? inspectedParameterCount)
    {
        bool turbo = admitted.KvCache is OpenVinoKvCacheFormat.TurboQuantTbq3
            or OpenVinoKvCacheFormat.TurboQuantTbq4;
        // Turbo rows require the independently verified exact TurboQuant build;
        // released rows must never borrow that experimental identity.
        if (turbo != (execution.TurboQuantBuild is not null))
        {
            return null;
        }
        if (catalog is null
              || inspectedParameterCount is not { } parameterCount)
        {
            return null;
        }
        string weights = admitted.Weights switch
        {
            OpenVinoWeightFormat.Int4 => "int4",
            OpenVinoWeightFormat.MxFp4 => "mxfp4",
            OpenVinoWeightFormat.Int8 => "int8",
            OpenVinoWeightFormat.Fp16 or OpenVinoWeightFormat.Original => "fp16",
            _ => string.Empty
        };
        string cache = admitted.KvCache switch
        {
            OpenVinoKvCacheFormat.RouteDefault => "released-default",
            OpenVinoKvCacheFormat.F16 => "f16",
            OpenVinoKvCacheFormat.U8 => "u8",
            OpenVinoKvCacheFormat.U4 => "u4",
            OpenVinoKvCacheFormat.TurboQuantTbq4 => "tbq4",
            OpenVinoKvCacheFormat.TurboQuantTbq3 => "tbq3",
            _ => string.Empty
        };
        foreach ((string methodology, string protocol) in new[]
        {
            (
                VerifiedOpenVinoOptimizationEvidence.SectorQualityMethodologyIdentity,
                VerifiedOpenVinoOptimizationEvidence.SectorQualityMemoryPerformanceProtocol),
            (
                VerifiedOpenVinoOptimizationEvidence.MethodologyIdentity,
                VerifiedOpenVinoOptimizationEvidence.FreshInt8MemoryPerformanceProtocol),
            (
                VerifiedOpenVinoOptimizationEvidence.TurboMethodologyIdentity,
                VerifiedOpenVinoOptimizationEvidence.FreshInt8MemoryPerformanceProtocol),
            (
                VerifiedOpenVinoOptimizationEvidence.MethodologyIdentity,
                VerifiedOpenVinoOptimizationEvidence.MemoryPerformanceProtocol),
            (
                VerifiedOpenVinoOptimizationEvidence.TurboMethodologyIdentity,
                VerifiedOpenVinoOptimizationEvidence.TurboMemoryPerformanceProtocol),
            (
                PublishedOpenVinoOptimizationEvidence.MethodologyIdentity,
                PublishedOpenVinoOptimizationEvidence.MemoryPerformanceProtocol)
        })
        {
            if (catalog.TryResolveArtifactConfiguration(
                    OptimizationEvidenceModelFamily.Granite,
                    binding.ModelSha256,
                    parameterCount,
                    OptimizationRoute.OpenVino,
                    OpenVinoEvidencePackageIdentity(execution),
                    weights,
                    weights,
                    cache,
                    admitted.Device switch
                    {
                        DeviceRouteId.Cpu => OptimizationEvidenceBackend.OpenVinoCpu,
                        DeviceRouteId.IntelNpu => OptimizationEvidenceBackend.OpenVinoNpu,
                        _ => OptimizationEvidenceBackend.OpenVinoGpu
                    },
                    admitted.Device switch
                    {
                        DeviceRouteId.Cpu => OptimizationEvidenceDeviceClass.Cpu,
                        DeviceRouteId.IntelIntegratedGpu => OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                        DeviceRouteId.IntelDiscreteGpu => OptimizationEvidenceDeviceClass.IntelDiscreteGpu,
                        _ => OptimizationEvidenceDeviceClass.IntelNpu
                    },
                    context.Tokens,
                    "local-chat-v1",
                    methodology,
                    protocol,
                    admitted.Device == DeviceRouteId.Cpu ? "openvino-cpu"
                        : admitted.Device == DeviceRouteId.IntelNpu
                            ? "openvino-npu"
                            : "openvino-gpu",
                    out OptimizationEvidenceRecord? evidence)
                && string.Equals(
                    evidence!.EvidenceId,
                    admitted.EvidenceId,
                    StringComparison.Ordinal))
            {
                return evidence;
            }
        }
        return null;
    }

    internal static string OpenVinoEvidencePackageIdentity(OpenVinoExecutionAuthority execution)
    {
        // Length-prefixed fields prevent delimiter collisions; ordinal ordering
        // makes optimizer insertion order immaterial, not optimizer membership.
        StringBuilder canonical = new();
        void Append(string value) => canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(value);
        Append("openvino-execution-closure-v1");
        Append(execution.BuildIdentity.RuntimeBuild);
        Append(execution.BuildIdentity.GenAiBuild);
        Append(execution.BuildIdentity.TokenizersBuild);
        Append(execution.BuildIdentity.WorkerManifestDigest);
        Append(execution.OptimizerVersions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (KeyValuePair<string, string> version in execution.OptimizerVersions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Append(version.Key);
            Append(version.Value);
        }
        Append(execution.TurboQuantBuild is null ? "tq-absent" : "tq-present");
        if (execution.TurboQuantBuild is { } tq)
        {
            Append(tq.SourceCommit);
            Append(tq.ImplementationCommit);
            Append(tq.PatchSeriesDigest);
            Append(tq.RuntimeManifestDigest);
        }
        return "openvino-closure-v1-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static OptimizationAssessment CoarseQuality(
        OptimizationQualityLevel level) => level switch
        {
            OptimizationQualityLevel.Acceptable or OptimizationQualityLevel.Fair =>
                OptimizationAssessment.Acceptable,
            OptimizationQualityLevel.Good or OptimizationQualityLevel.VeryGood =>
                OptimizationAssessment.Good,
            OptimizationQualityLevel.Excellent => OptimizationAssessment.Excellent,
            _ => OptimizationAssessment.Poor
        };

    private static void GenerateGguf(
        OptimizationCapabilitySnapshot snapshot,
        GgufCapabilityPayload payload,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ContextTokenCount context,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedIn,
        OptimizationHardwareAuthority hardwareAuthority,
        OptimizationEvidenceCatalog? qualityEvidence,
        ulong? inspectedParameterCount,
        List<OptimizationCandidate> candidates,
        List<OptimizationExclusion> exclusions)
    {
        foreach (GgufAdmittedConfiguration admitted in payload.Admitted)
        {
            if (payload.RuntimeAuthority is not { } runtimeAuthority
                || !runtimeAuthority.Profiles.TryGetValue(
                    admitted.EvidenceId, out GgufExecutionProfileAuthority? profile)
                || profile.Evidence is not (EvidenceGrade.Estimated or EvidenceGrade.Measured))
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    $"weights={admitted.Weights}|ctx={context.Tokens}",
                    OptimizationExclusionReason.ExecutionAuthorityNotEstablished));
                continue;
            }

            if (context.Tokens < admitted.MinimumContextTokens
                || context.Tokens > admitted.MaximumContextTokens)
            {
                continue;
            }

            if (!TryResolveGgufPreparation(
                payload, facts, admitted, out GgufWeightFormat effectiveWeights,
                out bool requiresPersistentChange,
                out OptimizationConversionProvenance provenance))
            {
                string refusedDescriptor =
                    $"weights={admitted.Weights}|ctx={context.Tokens}";
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    refusedDescriptor,
                    OptimizationExclusionReason.RequantisationNotAuthorized));
                continue;
            }

            if (admitted.EvidenceId is
                    "GGUF-V5-BF16-Q3-CPU-F16-01" or "GGUF-CURRENT-08EF-BF16-Q3-COMPAT-01"
                && !VerifiedGgufOptimizationEvidence.MatchesBf16Q3Quantizer(
                    payload.AdmittedQuantiser))
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    $"weights={admitted.Weights}|ctx={context.Tokens}",
                    OptimizationExclusionReason.RequantisationNotAuthorized));
                continue;
            }

            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                effectiveWeights,
                admitted.KvCache,
                admitted.Backend,
                admitted.Device,
                admitted.Offload);
            string descriptor = $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}";

            OptimizationEvidenceRecord? measuredEvidence =
                ResolveGgufQualityEvidence(
                    qualityEvidence,
                    binding,
                    payload.RuntimeAuthority,
                    facts,
                    admitted,
                    configuration,
                    context,
                    inspectedParameterCount,
                    out bool exactQualityTupleLocated);
            if (measuredEvidence is null)
            {
                bool currentProfileQualityAbsent =
                    admitted.Level == SupportLevel.DeclaredSupported
                    && !admitted.RequiresEvidence
                    && !requiresPersistentChange
                    && admitted.Weights == GgufWeightFormat.Imported
                    && admitted.KvCache == GgufKvCacheFormat.F16
                    && configuration.Weights == GgufWeightFormat.Imported
                    && configuration.KvCache == GgufKvCacheFormat.F16
                    && configuration.Backend == CompatibilityBackend.Cpu
                    && configuration.Device == DeviceRouteId.Cpu
                    && configuration.Offload == GpuOffloadLevel.None
                    && profile.Evidence == EvidenceGrade.Estimated
                    && inspectedParameterCount is > 0
                    && (WeightQuantisationMap.FromGgufFileType(
                            facts.FileType, facts.QuantisationVersion)
                            == WeightQuantisation.Q4_K_M
                        || (WeightQuantisationMap.FromGgufFileType(
                                facts.FileType, facts.QuantisationVersion)
                                == WeightQuantisation.BF16
                            && VerifiedGgufOptimizationEvidence.MatchesInspectedSource(
                                binding.ModelSha256,
                                binding.ModelLengthBytes,
                                facts.LayerCount,
                                facts.EmbeddingSize,
                                facts.AttentionHeadCount,
                                facts.KeyValueHeadCount,
                                facts.DeclaredContextLimit,
                                facts.FileType,
                                facts.QuantisationVersion)))
                    && qualityEvidence is not null
                    && string.Equals(
                        runtimeAuthority.RuntimeBuildId,
                        PublishedGgufOptimizationEvidence.RuntimeBuildId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        runtimeAuthority.RuntimeSourceCommit,
                        PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
                        StringComparison.Ordinal)
                    && !exactQualityTupleLocated;
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    descriptor,
                    currentProfileQualityAbsent
                        ? OptimizationExclusionReason.CurrentModelQualityEvidenceUnavailable
                        : OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
                continue;
            }

            if (VerifiedGgufOptimizationEvidence.IsVerifiedEvidenceId(
                    admitted.EvidenceId)
                && !VerifiedGgufOptimizationEvidence.MatchesExecutionProfile(profile))
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    $"weights={admitted.Weights}|ctx={context.Tokens}",
                    OptimizationExclusionReason.ExecutionAuthorityNotEstablished));
                continue;
            }
            if (measuredEvidence is { IsAdmitted: false })
            {
                exclusions.Add(new OptimizationExclusion(
                    admitted.EvidenceId,
                    descriptor,
                    OptimizationExclusionReason.EvidenceBelowAdmissionLevel));
                continue;
            }

            OptimizationAssessment quality = CoarseQuality(measuredEvidence.Quality.Level);

            if (Refused(
                admitted.Level, admitted.RequiresEvidence, admitted.EvidenceId,
                optedIn, descriptor, exclusions))
            {
                continue;
            }

            CompatibilityCandidate candidate = CompatibilityCandidate.Create(
                configuration,
                context,
                !requiresPersistentChange
                    ? CandidatePreparation.RuntimeProfileOnly
                    : CandidatePreparation.WeightConversionRequired,
                admitted.EvidenceId,
                admitted.Level == SupportLevel.Experimental,
                isBaseline: false);

            ResourceEstimate estimate =
                GgufResourceEstimator.Estimate(facts, candidate, policy);
            GgufWeightNormalizationProof? normalizationProof =
                effectiveWeights == GgufWeightFormat.Imported
                    && admitted.Weights != GgufWeightFormat.Imported
                    ? GgufWeightNormalizationProof.FromInspection(
                        facts.FileType, facts.QuantisationVersion, admitted.Weights)
                    : null;

            Admit(
                configuration,
                estimate,
                context,
                admitted.EvidenceId,
                admitted.Level,
                quality,
                requiresPersistentChange,
                workload,
                safeBudget,
                availableDisk,
                descriptor,
                candidates,
                exclusions,
                snapshot,
                binding,
                admitted.RequiresEvidence,
                optedIn,
                hardwareAuthority,
                provenance,
                normalizationProof,
                measuredEvidence);
        }
    }

    private static OptimizationEvidenceRecord? ResolveGgufQualityEvidence(
        OptimizationEvidenceCatalog? catalog,
        OptimizationJourneyBinding binding,
        GgufRuntimeAuthority? runtime,
        InspectedModelFacts facts,
        GgufAdmittedConfiguration admitted,
        GgufRouteConfiguration configuration,
        ContextTokenCount context,
        ulong? inspectedParameterCount,
        out bool exactTupleLocated)
    {
        exactTupleLocated = false;
        if (catalog is null
            || runtime is null
            || inspectedParameterCount is not { } parameterCount
            || !string.Equals(
                runtime.RuntimeBuildId,
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                StringComparison.Ordinal)
            || !string.Equals(
                runtime.RuntimeSourceCommit,
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
                StringComparison.Ordinal))
        {
            return null;
        }

        string sourceWeights = WeightQuantisationMap.FromGgufFileType(
            facts.FileType,
            facts.QuantisationVersion) switch
        {
            WeightQuantisation.Q4_K_M => "q4_k_m",
            WeightQuantisation.BF16 => "bf16",
            _ => string.Empty
        };
        string targetWeights = configuration.Weights switch
        {
            GgufWeightFormat.Imported => sourceWeights,
            GgufWeightFormat.Q4KM => "q4_k_m",
            GgufWeightFormat.Q3KM => "q3_k_m",
            _ => string.Empty
        };
        string cache = configuration.KvCache switch
        {
            GgufKvCacheFormat.F16 => "f16",
            GgufKvCacheFormat.Q8_0 => "q8_0",
            GgufKvCacheFormat.TurboQuant4Bit => "turbo4",
            GgufKvCacheFormat.TurboQuant3Bit => "turbo3",
            GgufKvCacheFormat.TurboQuant2Bit => "turbo2",
            _ => string.Empty
        };
        OptimizationEvidenceBackend backend = configuration.Backend switch
        {
            CompatibilityBackend.Cpu => OptimizationEvidenceBackend.Cpu,
            CompatibilityBackend.IntelVulkan => OptimizationEvidenceBackend.Vulkan,
            _ => OptimizationEvidenceBackend.Unspecified
        };
        OptimizationEvidenceDeviceClass deviceClass = configuration.Device switch
        {
            DeviceRouteId.Cpu => OptimizationEvidenceDeviceClass.Cpu,
            DeviceRouteId.IntelIntegratedGpu =>
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
            DeviceRouteId.IntelDiscreteGpu =>
                OptimizationEvidenceDeviceClass.IntelDiscreteGpu,
            _ => OptimizationEvidenceDeviceClass.Unspecified
        };
        string executionProfile = (configuration.Backend, configuration.Offload) switch
        {
            (CompatibilityBackend.Cpu, GpuOffloadLevel.None) => "cpu",
            (CompatibilityBackend.IntelVulkan, GpuOffloadLevel.Partial) =>
                "vulkan-partial",
            (CompatibilityBackend.IntelVulkan, GpuOffloadLevel.Full) =>
                "vulkan-full",
            _ => string.Empty
        };

        if (sourceWeights.Length == 0
            || targetWeights.Length == 0
            || cache.Length == 0
            || backend == OptimizationEvidenceBackend.Unspecified
            || deviceClass == OptimizationEvidenceDeviceClass.Unspecified
            || executionProfile.Length == 0)
        {
            return null;
        }

        exactTupleLocated = VerifiedGgufOptimizationEvidence.TryResolve(
            catalog,
            binding.ModelSha256,
            parameterCount,
            sourceWeights,
            targetWeights,
            cache,
            backend,
            deviceClass,
            context.Tokens,
            executionProfile,
            out OptimizationEvidenceRecord? verifiedEvidence);
        if (exactTupleLocated
            && string.Equals(
                verifiedEvidence!.EvidenceId,
                admitted.EvidenceId,
                StringComparison.Ordinal))
        {
            return verifiedEvidence;
        }

        bool historicalTupleLocated = catalog.TryResolveArtifactConfiguration(
            OptimizationEvidenceModelFamily.Granite,
            binding.ModelSha256,
            parameterCount,
            OptimizationRoute.Gguf,
            PublishedGgufOptimizationEvidence.RuntimePackageIdentity,
            sourceWeights,
            targetWeights,
            cache,
            backend,
            deviceClass,
            context.Tokens,
            "local-chat-v1",
            PublishedGgufOptimizationEvidence.MethodologyIdentity,
            PublishedGgufOptimizationEvidence.MemoryPerformanceProtocol,
            executionProfile,
            out OptimizationEvidenceRecord? evidence);
        exactTupleLocated |= historicalTupleLocated;
        return historicalTupleLocated
            && string.Equals(
                evidence!.EvidenceId,
                admitted.EvidenceId,
                StringComparison.Ordinal)
                ? evidence
                : null;
    }

    private static bool TryResolveGgufPreparation(
        GgufCapabilityPayload payload,
        InspectedModelFacts facts,
        GgufAdmittedConfiguration admitted,
        out GgufWeightFormat effectiveWeights,
        out bool requiresPersistentChange,
        out OptimizationConversionProvenance provenance)
    {
        effectiveWeights = admitted.Weights;
        requiresPersistentChange = false;
        provenance = OptimizationConversionProvenance.None;

        WeightQuantisation source = WeightQuantisationMap.FromGgufFileType(
            facts.FileType, facts.QuantisationVersion);

        if (admitted.Weights == GgufWeightFormat.Imported)
        {
            return true;
        }

        WeightQuantisation target = GgufWeightFormatMap.ToCanonical(admitted.Weights);

        if (source == WeightQuantisation.Unknown || target == WeightQuantisation.Unknown)
        {
            return false;
        }

        if (source == target)
        {
            effectiveWeights = GgufWeightFormat.Imported;
            return true;
        }

        if (payload.ConversionSource is not { } conversionSource
            || payload.AdmittedQuantiser is null
            || !conversionSource.CanProduce(target))
        {
            return false;
        }

        if (conversionSource.IsAlreadyQuantised
            && (!(payload.RequantisationPolicy?.Authorizes(admitted) ?? false)
                || payload.RequantisationPolicy.Source != conversionSource
                || payload.RequantisationPolicy.Quantiser != payload.AdmittedQuantiser))
        {
            return false;
        }

        requiresPersistentChange = true;
        provenance = conversionSource.IsAlreadyQuantised
            ? OptimizationConversionProvenance.ControlledRequantisation
            : OptimizationConversionProvenance.HigherPrecisionSource;

        return true;
    }

    /// <summary>
    /// Experimental entries are absent unless the user opted in to that exact
    /// evidence record. Opting in to one experimental route must not admit
    /// another the user never saw.
    /// </summary>
    private static bool Refused(
        SupportLevel level,
        bool requiresEvidence,
        string evidenceId,
        IReadOnlySet<string> optedIn,
        string descriptor,
        List<OptimizationExclusion> exclusions)
    {
        if (!OptimizationSupportLevelPolicy.IsAdmitted(level))
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId,
                descriptor,
                OptimizationExclusionReason.EvidenceBelowAdmissionLevel));

            return true;
        }

        if ((level == SupportLevel.Experimental || requiresEvidence)
            && !optedIn.Contains(evidenceId))
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId,
                descriptor,
                level == SupportLevel.Experimental
                    ? OptimizationExclusionReason.ExperimentalNotAdmitted
                    : OptimizationExclusionReason.EvidenceBelowAdmissionLevel));

            return true;
        }

        return false;
    }

    private static ulong ProductionWorkingDiskBytes(bool gguf, ulong artifactDisk, ulong sourceBytes) =>
        gguf ? checked(artifactDisk + sourceBytes) : artifactDisk;

    private static void Admit(
        RouteConfiguration configuration,
        ResourceEstimate estimate,
        ContextTokenCount context,
        string evidenceId,
        SupportLevel level,
        OptimizationAssessment quality,
        bool requiresPersistentChange,
        OptimizationWorkload workload,
        ByteCount safeBudget,
        ByteCount availableDisk,
        string descriptor,
        List<OptimizationCandidate> candidates,
        List<OptimizationExclusion> exclusions,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationJourneyBinding binding,
        bool requiresEvidence,
        IReadOnlySet<string> optedInEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority,
        OptimizationConversionProvenance provenance =
            OptimizationConversionProvenance.None,
        GgufWeightNormalizationProof? normalizationProof = null,
        OptimizationEvidenceRecord? measuredEvidence = null)
    {
        if (estimate.Status != EstimationStatus.Established)
        {
            // A typed exclusion, never a zero. Zero would be the cheapest
            // option on the board and would win every efficiency band.
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.EstimateNotEstablished));

            return;
        }

        ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);
        if (!hardwareAuthority.Supports(configuration))
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor,
                OptimizationExclusionReason.HardwareCapabilityUnavailable));
            return;
        }

        if (context.Tokens < workload.MinimumContextTokens)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor,
                OptimizationExclusionReason.ContextBelowWorkloadMinimum));

            return;
        }

        if (quality < workload.MinimumQuality)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.QualityBelowFloor));

            return;
        }

        ByteCount peak = peaks.SystemMemoryPressure;

        if (peak > safeBudget)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.ExceedsSafeMemoryBudget)
            {
                EstimatedRequiredBytes = peak.Bytes
            });

            return;
        }

        ByteCount dedicatedPeak = peaks.PeakFor(ResourceTarget.DedicatedDeviceMemory);
        ByteCount? dedicatedBudgetForMetrics = null;
        ByteCount? dedicatedHeadroom = null;
        if (dedicatedPeak != ByteCount.Zero)
        {
            if (hardwareAuthority.SafeDedicatedDeviceMemoryBudget is not { } dedicatedBudget)
            {
                exclusions.Add(new OptimizationExclusion(
                    evidenceId, descriptor,
                    OptimizationExclusionReason.DedicatedMemoryNotEstablished));
                return;
            }
            if (dedicatedPeak > dedicatedBudget)
            {
                exclusions.Add(new OptimizationExclusion(
                    evidenceId, descriptor,
                    OptimizationExclusionReason.ExceedsDedicatedDeviceMemory));
                return;
            }
            dedicatedBudgetForMetrics = dedicatedBudget;
            _ = dedicatedBudget.TrySubtract(
                dedicatedPeak, out ByteCount remainingDedicated);
            dedicatedHeadroom = remainingDedicated;
        }

        ByteCount artifactDisk = peaks.PeakFor(ResourceTarget.Storage);
        // Production GGUF stages an immutable source copy for every plan,
        // including cache-only plans. Generic/current-model fit is unchanged.
        ByteCount disk = ByteCount.FromBytes(ProductionWorkingDiskBytes(
            configuration is GgufRouteConfiguration, artifactDisk.Bytes,
            binding.ModelLengthBytes));

        if (availableDisk == ByteCount.Zero || disk > availableDisk)
        {
            exclusions.Add(new OptimizationExclusion(
                evidenceId, descriptor, OptimizationExclusionReason.InsufficientDiskSpace)
            {
                EstimatedRequiredBytes = peak.Bytes,
                EstimatedDiskRequiredBytes = disk.Bytes,
                AvailableDiskBytes = availableDisk.Bytes
            });

            return;
        }

        _ = safeBudget.TrySubtract(peak, out ByteCount headroom);

        OptimizationCandidateMetrics metrics = OptimizationCandidateMetrics.Create(
            measuredEvidence is null ? EvidenceGrade.Estimated : EvidenceGrade.Measured,
            quality,
            // Not measured. Nothing has run, so performance and stability
            // are derived from the support level rather than observed, and
            // the evidence grade above says so.
            level == SupportLevel.Experimental
                ? OptimizationAssessment.Acceptable
                : OptimizationAssessment.Good,
            level == SupportLevel.Experimental
                ? OptimizationAssessment.Acceptable
                : OptimizationAssessment.Good,
            context.Tokens,
            peak.Bytes,
            safeBudget.Bytes,
            headroom.Bytes,
            disk.Bytes,
            requiresPersistentChange ? artifactDisk.Bytes : 0,
            requiresPersistentChange,
            availableDisk.Bytes,
            dedicatedPeak == ByteCount.Zero ? null : dedicatedPeak.Bytes,
            dedicatedBudgetForMetrics?.Bytes,
            dedicatedHeadroom?.Bytes);
        OptimizationCandidate admittedCandidate = normalizationProof is null
            ? measuredEvidence is null
                ? OptimizationCandidate.Create(
                    configuration, metrics, evidenceId,
                    level == SupportLevel.Experimental, provenance)
                : OptimizationCandidate.Create(
                    configuration, metrics, measuredEvidence,
                    level == SupportLevel.Experimental, provenance)
            : OptimizationCandidate.CreateWithGgufWeightNormalization(
                (GgufRouteConfiguration)configuration, metrics, evidenceId,
                level == SupportLevel.Experimental, normalizationProof, measuredEvidence);
        OptimizationAdmissionProof admissionProof = OptimizationAdmissionProof.Create(
            snapshot, workload, binding, admittedCandidate,
            level, requiresEvidence, optedInEvidenceIds,
            OptimizationIssuanceAuthority.FromGeneration(
                hardwareAuthority, safeBudget, availableDisk));
        candidates.Add(OptimizationCandidate.AttachAdmissionProof(
            admittedCandidate, admissionProof));
    }
}
