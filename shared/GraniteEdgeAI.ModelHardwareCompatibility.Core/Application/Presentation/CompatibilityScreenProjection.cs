using System.Collections.Frozen;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// Explicit identity of the imported/current setup. It is source-bound and can
/// only describe the runtime-profile baseline; a list position is never an
/// identity.
/// </summary>
internal sealed record CompatibilityBaselineIdentity
{
    private CompatibilityBaselineIdentity(
        CandidateFingerprint fingerprint,
        CandidatePreparation preparation,
        string configurationDescriptor,
        int contextTokens,
        string sourceIdentitySha256)
    {
        Fingerprint = fingerprint;
        Preparation = preparation;
        ConfigurationDescriptor = configurationDescriptor;
        ContextTokens = contextTokens;
        SourceIdentitySha256 = sourceIdentitySha256;
    }

    internal CandidateFingerprint Fingerprint { get; }
    internal CandidatePreparation Preparation { get; }
    internal string ConfigurationDescriptor { get; }
    internal int ContextTokens { get; }
    internal string SourceIdentitySha256 { get; }

    internal string OptimizationDescriptor =>
        $"{ConfigurationDescriptor}|ctx={ContextTokens}";

    internal static CompatibilityBaselineIdentity Create(
        CandidatePreparation preparation,
        RouteConfiguration configuration,
        ContextTokenCount context,
        OptimizationJourneyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(binding);

        if (preparation != CandidatePreparation.RuntimeProfileOnly)
        {
            throw new ArgumentException(
                "The optimization baseline must be the imported source under a "
                + "runtime-only profile; conversion is an alternative, not a baseline.",
                nameof(preparation));
        }

        return new CompatibilityBaselineIdentity(
            CandidateFingerprint.Compute(configuration, context, preparation),
            preparation,
            configuration.CanonicalDescriptor,
            context.Tokens,
            DigestSource(
                configuration.CanonicalDescriptor,
                context.Tokens,
                CandidatePreparation.RuntimeProfileOnly,
                binding));
    }

    /// <summary>
    /// Maps the coordinator's published pre-optimization baseline. This is a
    /// separate identity kind: <see cref="CandidatePreparation.None"/> is
    /// accepted only from the exact evaluated source row and is never treated
    /// as an arbitrary runtime-profile candidate.
    /// </summary>
    internal static CompatibilityBaselineIdentity ForLegacyNone(
        EvaluatedCandidate baseline,
        OptimizationJourneyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(binding);
        if (!baseline.Candidate.IsBaseline
            || baseline.Preparation != CandidatePreparation.None)
        {
            throw new ArgumentException(
                "A legacy baseline identity requires the exact published None baseline.",
                nameof(baseline));
        }

        return new CompatibilityBaselineIdentity(
            baseline.Fingerprint,
            CandidatePreparation.None,
            baseline.Candidate.Configuration.CanonicalDescriptor,
            baseline.Context.Tokens,
            DigestSource(
                baseline.Candidate.Configuration.CanonicalDescriptor,
                baseline.Context.Tokens,
                CandidatePreparation.None,
                binding));
    }

    internal bool Matches(
        EvaluatedCandidate candidate,
        OptimizationJourneyBinding binding) =>
        candidate.Candidate.IsBaseline
        && candidate.Preparation == Preparation
        && candidate.Fingerprint == Fingerprint
        && candidate.Candidate.Configuration.CanonicalDescriptor
            == ConfigurationDescriptor
        && candidate.Context.Tokens == ContextTokens
        && SourceIdentitySha256 == DigestSource(
            candidate.Candidate.Configuration.CanonicalDescriptor,
            candidate.Context.Tokens,
            candidate.Preparation,
            binding);

    private static string DigestSource(
        string configurationDescriptor,
        int contextTokens,
        CandidatePreparation preparation,
        OptimizationJourneyBinding binding)
    {
        string[] fields =
        [
            configurationDescriptor,
            contextTokens.ToString(CultureInfo.InvariantCulture),
            ((int)preparation).ToString(
                CultureInfo.InvariantCulture),
            binding.ModelInspectionRunId,
            binding.ModelInspectionHandoffId,
            binding.ModelSha256,
            binding.ModelLengthBytes.ToString(CultureInfo.InvariantCulture),
            binding.ProductHardwareRunId,
            binding.HardwareSnapshotSha256
        ];
        string canonical = string.Concat(fields.Select(field =>
            $"{field.Length.ToString(CultureInfo.InvariantCulture)}:{field}"));
        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical))).ToLowerInvariant();
    }
}

/// <summary>Exact authority inputs paired with the generated frontier.</summary>
internal sealed record CompatibilityOptimizationProjectionInput
{
    private CompatibilityOptimizationProjectionInput(
        CrossRouteGenerationResult generated,
        OptimizationCapabilitySnapshot snapshot,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority,
        CompatibilityBaselineIdentity baseline)
    {
        Generated = generated;
        Snapshot = snapshot;
        Facts = facts;
        Workload = workload;
        Binding = binding;
        SafeBudget = safeBudget;
        AvailableDisk = availableDisk;
        Policy = policy;
        OptedInExperimentalEvidenceIds =
            optedInExperimentalEvidenceIds.ToFrozenSet(StringComparer.Ordinal);
        HardwareAuthority = hardwareAuthority;
        Baseline = baseline;
    }

    internal CrossRouteGenerationResult Generated { get; }
    internal OptimizationCapabilitySnapshot Snapshot { get; }
    internal InspectedModelFacts Facts { get; }
    internal OptimizationWorkload Workload { get; }
    internal OptimizationJourneyBinding Binding { get; }
    internal ByteCount SafeBudget { get; }
    internal ByteCount AvailableDisk { get; }
    internal EstimatorPolicy Policy { get; }
    internal IReadOnlySet<string> OptedInExperimentalEvidenceIds { get; }
    internal OptimizationHardwareAuthority HardwareAuthority { get; }
    internal CompatibilityBaselineIdentity Baseline { get; }

    internal static CompatibilityOptimizationProjectionInput Create(
        CrossRouteGenerationResult generated,
        OptimizationCapabilitySnapshot snapshot,
        InspectedModelFacts facts,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        ByteCount safeBudget,
        ByteCount availableDisk,
        EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority,
        CompatibilityBaselineIdentity baseline)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEvidenceIds);
        ArgumentNullException.ThrowIfNull(hardwareAuthority);
        ArgumentNullException.ThrowIfNull(baseline);

        return new CompatibilityOptimizationProjectionInput(
            generated, snapshot, facts, workload, binding, safeBudget,
            availableDisk, policy, optedInExperimentalEvidenceIds,
            hardwareAuthority, baseline);
    }
}

/// <summary>
/// One finding, flattened for a caller outside this assembly.
/// </summary>
public sealed record CompatibilityFindingView(
    CompatibilityFindingCode Code,
    FindingSeverity Severity);

/// <summary>
/// One mode's answer, flattened for a caller outside this assembly.
///
/// The ordered selection factors that decided it stay internal: they are
/// engine reasoning, and a screen that needed them would be re-deriving the
/// decision rather than presenting it.
/// </summary>
public sealed record CompatibilityModeView(
    CompatibilityMode Mode,
    ModeAvailability Availability,
    ModeAdmissionReason Reason,
    bool HasSelection);

/// <summary>
/// Exact storage facts for the smallest acceptable optimisation when storage,
/// rather than runtime RAM, is the remaining admission blocker.
/// </summary>
public sealed record CompatibilityStorageRequirementView(
    ulong RequiredBytes,
    ulong AvailableBytes);

/// <summary>
/// What a screen needs from a run, decided once and handed over whole.
///
/// This is the engine's entire public surface. Everything that produced it —
/// the estimator, the generator, the policies, the candidates — stays internal,
/// because a screen has no business reaching into any of it. What crosses the
/// boundary is a decision and the codes explaining it.
///
/// It carries no wording, no colour and no layout. Presentation owns all of
/// that, which is also what keeps a message the engine wrote from reaching a
/// user in a language they did not choose.
/// </summary>
public sealed record CompatibilityScreenModel
{
    private CompatibilityScreenModel(
        CompatibilityScreenState state,
        IReadOnlyList<CompatibilityFindingView> findings,
        IReadOnlyList<CompatibilityModeView> modes,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable,
        bool continueEnabled,
        CompatibilitySetupView? setup,
        CompatibilityOptimizationView? optimization,
        bool isActionAuthoritative,
        ulong? smallestOptimizedRequiredBytes = null,
        CompatibilityStorageRequirementView? optimizationStorageRequirement = null)
    {
        CurrentSetup = setup;
        Setup = state == CompatibilityScreenState.OptimisationRequired ? null : setup;
        State = state;
        Findings = findings;
        Modes = modes;
        BaselineExclusionReason = baselineExclusionReason;
        UseCurrentModelAvailable = useCurrentModelAvailable
            && isActionAuthoritative;
        IsActionAuthoritative = isActionAuthoritative;
        ContinueEnabled = continueEnabled && isActionAuthoritative;
        Optimization = optimization;
        RecommendedSetup = optimization?.RecommendedMode;
        SmallestOptimizedRequiredBytes = smallestOptimizedRequiredBytes;
        OptimizationStorageRequirement = optimizationStorageRequirement;
    }

    public CompatibilityScreenState State { get; }

    /// <summary>
    /// What the screen must explain. Screen 06 in particular has to list the
    /// exact missing evidence with recovery actions, and it can only do that if
    /// the engine hands over what was missing.
    /// </summary>
    public IReadOnlyList<CompatibilityFindingView> Findings { get; }

    /// <summary>
    /// All four modes, always. An unavailable mode is disabled with its reason
    /// rather than hidden — a hidden option looks like one that never existed.
    /// Empty only when no assessment was reached at all.
    /// </summary>
    public IReadOnlyList<CompatibilityModeView> Modes { get; }

    /// <summary>Why the user's current configuration is not offered, when it is not.</summary>
    public BaselineExclusionReason BaselineExclusionReason { get; }

    public bool UseCurrentModelAvailable { get; }

    /// <summary>
    /// Continue is enabled only from a state that concluded something the user
    /// can act on. Elsewhere it stays visible and disabled.
    /// </summary>
    public bool ContinueEnabled { get; }

    internal bool IsActionAuthoritative { get; }

    /// <summary>
    /// The setup the screen is describing, or null when nothing was evaluated.
    ///
    /// Compatibility alias for non-optimization screens. It is null when
    /// optimization is required; callers must then use CurrentSetup and
    /// RecommendedSetup so the failing import cannot be confused with the
    /// admitted Continue target.
    /// </summary>
    public CompatibilitySetupView? Setup { get; }

    /// <summary>The exact imported/current setup that was assessed.</summary>
    public CompatibilitySetupView? CurrentSetup { get; }

    /// <summary>
    /// The admitted alternative Continue will select, only when optimisation is
    /// required. This is deliberately a different type from CurrentSetup so a
    /// failed baseline cannot be mistaken for the continue target.
    /// </summary>
    public CompatibilityOptimizationModeView? RecommendedSetup { get; }

    /// <summary>
    /// Fully resolved choices only for <see cref="CompatibilityScreenState.OptimisationRequired"/>.
    /// Null elsewhere; WinUI must not rebuild a frontier to populate it.
    /// </summary>
    public CompatibilityOptimizationView? Optimization { get; }

    /// <summary>
    /// The smallest established system/shared-memory requirement among optimized
    /// candidates rejected only because the current safe budget was too low.
    /// This includes the same calibration margin used by fit assessment so it
    /// can be compared directly with the displayed safe budget. It is
    /// informational and never grants execution authority.
    /// </summary>
    public ulong? SmallestOptimizedRequiredBytes { get; }

    public CompatibilityStorageRequirementView? OptimizationStorageRequirement { get; }

    /// <summary>
    /// Builds a screen model directly, for Debug fixtures and tests that need to
    /// render a state without an engine run behind it.
    ///
    /// This exists so all ten screens can be looked at and tested before the
    /// owner adapters land — otherwise nine of them would be unreachable and
    /// therefore unreviewable. It is not a way to fabricate a conclusion: what
    /// it produces is presentation input, never a run result, and nothing
    /// downstream can mistake one for the other.
    /// </summary>
    public static CompatibilityScreenModel ForPresentation(
        CompatibilityScreenState state,
        IReadOnlyList<CompatibilityFindingView> findings,
        IReadOnlyList<CompatibilityModeView> modes,
        BaselineExclusionReason baselineExclusionReason,
        bool useCurrentModelAvailable,
        bool continueEnabled,
        CompatibilitySetupView? setup = null,
        CompatibilityOptimizationView? optimization = null,
        ulong? smallestOptimizedRequiredBytes = null,
        CompatibilityStorageRequirementView? optimizationStorageRequirement = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(modes);

        if (state == CompatibilityScreenState.Unspecified
            || !Enum.IsDefined(state)
            || (state == CompatibilityScreenState.OptimisationRequired)
                != (optimization is not null)
            || state == CompatibilityScreenState.OptimisationRequired
                && setup?.Fit != CompatibilityFitState.DoesNotFit
                && !(setup?.Route == RuntimeRouteId.OpenVinoGenAi
                    && setup.Fit is CompatibilityFitState.Safe or CompatibilityFitState.Narrow
                    && !useCurrentModelAvailable
                    && findings.Any(item => item.Code == CompatibilityFindingCode.BaselineConfigurationUnavailable)))
        {
            throw new ArgumentException(
                "A screen model must name the state it renders.", nameof(state));
        }

        return new CompatibilityScreenModel(
            state,
            [.. findings],
            [.. modes],
            baselineExclusionReason,
            useCurrentModelAvailable,
            continueEnabled,
            setup,
            optimization,
            isActionAuthoritative: false,
            smallestOptimizedRequiredBytes,
            optimizationStorageRequirement);
    }

    internal static CompatibilityScreenModel From(CompatibilityRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CompatibilityScreenState state = DecideState(result);
        CompatibilitySetupView? setup = DescribeSetup(result.Assessment);
        if ((state is CompatibilityScreenState.EstimatedCompatible
                or CompatibilityScreenState.OptimisationRequired)
            && setup is null)
        {
            state = CompatibilityScreenState.NotEstablished;
        }

        IReadOnlyList<CompatibilityFindingView> findings =
        [
            .. result.Findings.Select(finding =>
                new CompatibilityFindingView(finding.Code, finding.Severity))
        ];

        IReadOnlyList<CompatibilityModeView> modes =
        [
            .. (result.Assessment?.ModeSelections ?? []).Select(selection =>
                new CompatibilityModeView(
                    selection.Mode,
                    selection.Availability,
                    selection.Reason,
                    selection.SelectedFingerprint is not null))
        ];

        return new CompatibilityScreenModel(
            state,
            findings,
            modes,
            result.Assessment?.BaselineExclusionReason ?? BaselineExclusionReason.None,
            result.Assessment?.UseCurrentModelAvailable ?? false,
            state is CompatibilityScreenState.EstimatedCompatible
                or CompatibilityScreenState.OptimisationRequired,
            setup,
            optimization: null,
            isActionAuthoritative: true);
    }

    /// <summary>
    /// Projects the hardware-relative frontier using the exact authority that
    /// generated it. Any mismatch is unknown evidence, never a no-fit claim.
    /// </summary>
    internal static CompatibilityScreenModel From(
        CompatibilityRunResult result,
        CompatibilityOptimizationProjectionInput optimization)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(optimization);

        CompatibilityScreenModel original = From(result);
        if (result.Outcome != CompatibilityRunOutcome.Completed
            || result.Assessment is not { } assessment)
        {
            return original;
        }

        ProjectionDecision decision = DecideOptimizationState(assessment, optimization);
        bool actionable = decision.State is CompatibilityScreenState.EstimatedCompatible
            or CompatibilityScreenState.OptimisationRequired;

        return new CompatibilityScreenModel(
            decision.State,
            decision.BaselineUnavailable
                ? [.. original.Findings, new CompatibilityFindingView(
                    CompatibilityFindingCode.BaselineConfigurationUnavailable, FindingSeverity.Warning)]
                : original.Findings,
            original.Modes,
            original.BaselineExclusionReason,
            decision.State == CompatibilityScreenState.EstimatedCompatible,
            actionable,
            decision.Setup,
            decision.Optimization,
            isActionAuthoritative: true,
            decision.SmallestOptimizedRequiredBytes,
            decision.OptimizationStorageRequirement);
    }

    /// <summary>
    /// Retains the exact admitted frontier for a later planning action. Nothing
    /// is exposed publicly: the UI receives the planning session, not the
    /// candidate collection. A required-optimisation session excludes the
    /// failing baseline; an optional session also excludes the unchanged
    /// baseline because chatting with that model is a separate typed exit.
    /// Consequently every plan issued from this session represents an actual
    /// optimisation shown by the optional-selection UI.
    /// </summary>
    internal static IReadOnlyList<OptimizationCandidate> RetainPlanningCandidates(
        CompatibilityScreenModel screen,
        CompatibilityOptimizationProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(input);

        if (!screen.IsActionAuthoritative || !ValidGeneratedAuthority(input))
        {
            return [];
        }

        return screen.State switch
        {
            CompatibilityScreenState.EstimatedCompatible =>
                Array.AsReadOnly([
                    .. input.Generated.Candidates.Where(candidate =>
                        candidate.CanonicalDescriptor
                            != input.Baseline.OptimizationDescriptor)
                ]),
            CompatibilityScreenState.OptimisationRequired =>
                Array.AsReadOnly([
                    .. input.Generated.Candidates.Where(candidate =>
                        candidate.CanonicalDescriptor
                            != input.Baseline.OptimizationDescriptor)
                ]),
            _ => []
        };
    }

    internal static IReadOnlyList<CompatibilityExperimentalConsentOption>
        RetainExperimentalConsentOptions(
            CompatibilityOptimizationProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ValidGeneratedAuthority(input))
        {
            return [];
        }

        SortedSet<string> evidenceIds = new(StringComparer.Ordinal);
        foreach (OptimizationCandidate candidate in input.Generated.Candidates)
        {
            if (candidate.AdmissionProof?.OptedInEvidenceId is { } evidenceId)
            {
                evidenceIds.Add(evidenceId);
            }
        }
        foreach (OptimizationExclusion exclusion in input.Generated.Exclusions)
        {
            if (exclusion.Reason
                == OptimizationExclusionReason.ExperimentalNotAdmitted)
            {
                evidenceIds.Add(exclusion.EvidenceId);
            }
        }

        List<CompatibilityExperimentalConsentOption> options = [];
        foreach (string evidenceId in evidenceIds)
        {
            options.Add(new CompatibilityExperimentalConsentOption(
                input.Snapshot.Route,
                evidenceId));
        }
        return Array.AsReadOnly([.. options]);
    }

    internal static CompatibilityOptimizationView? RetainOptionalOptimization(
        CompatibilityScreenModel screen,
        CompatibilityOptimizationProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(input);
        if (screen.State != CompatibilityScreenState.EstimatedCompatible
            || !ValidGeneratedAuthority(input))
        {
            return null;
        }

        IReadOnlyList<OptimizationCandidate> alternatives =
        [
            .. input.Generated.Candidates.Where(candidate =>
                candidate.CanonicalDescriptor
                    != input.Baseline.OptimizationDescriptor)
        ];
        return alternatives.Count == 0
            ? null
            : BuildOptimizationView(alternatives);
    }

    private sealed record ProjectionDecision(
        CompatibilityScreenState State,
        CompatibilityOptimizationView? Optimization,
        CompatibilitySetupView? Setup,
        ulong? SmallestOptimizedRequiredBytes = null,
        CompatibilityStorageRequirementView? OptimizationStorageRequirement = null,
        bool BaselineUnavailable = false);

    private static ProjectionDecision DecideOptimizationState(
        CompatibilityAssessment assessment,
        CompatibilityOptimizationProjectionInput input)
    {
#if DEBUG
        foreach (OptimizationExclusionReason reason in
            Enum.GetValues<OptimizationExclusionReason>())
        {
            if (input.Generated.Exclusions.Any(exclusion =>
                exclusion.Reason == reason))
            {
                WriteCompatibilityDiagnostic(
                    $"Granite.Compatibility exclusion reason={reason}");
            }
        }
#endif
        EvaluatedCandidate[] matchingBaselines =
        [
            .. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Candidate.IsBaseline
                && input.Baseline.Matches(candidate, input.Binding))
        ];

        if (matchingBaselines.Length != 1
            || assessment.BaselineFingerprint != matchingBaselines[0].Fingerprint
            || input.Baseline.Preparation is not (
                CandidatePreparation.RuntimeProfileOnly or CandidatePreparation.None)
            || !ValidGeneratedAuthority(input))
        {
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-baseline-or-authority-rejected");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        EvaluatedCandidate baseline = matchingBaselines[0];
        if (!Enum.IsDefined(baseline.Fit.State))
        {
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-fit-state-invalid");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }
        CompatibilitySetupView? baselineSetup = Describe(baseline);
        if (baselineSetup is null)
        {
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-baseline-description-unavailable");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        // An incomplete frontier cannot support even a positive baseline
        // verdict for this screen: the choices it would present are not known
        // to be complete under the current authority.
        if (input.Generated.Exclusions.Any(exclusion =>
            exclusion.Reason is OptimizationExclusionReason.EstimateNotEstablished
                or OptimizationExclusionReason.ExecutionAuthorityNotEstablished))
        {
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-frontier-incomplete");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        IReadOnlyList<OptimizationCandidate> alternatives =
        [
            .. input.Generated.Candidates.Where(candidate =>
                candidate.CanonicalDescriptor != input.Baseline.OptimizationDescriptor)
        ];

        bool currentFrontierSaysBaselineFits = input.Generated.Candidates.Any(
            candidate => candidate.CanonicalDescriptor
                == input.Baseline.OptimizationDescriptor);
        if (baseline.Fit.State == CompatibilityFitState.DoesNotFit
            && currentFrontierSaysBaselineFits)
        {
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-baseline-fit-conflict");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        bool currentFrontierExplicitlyExcludesBaseline =
            !currentFrontierSaysBaselineFits
            && input.Generated.Exclusions.Any(exclusion =>
                exclusion.Reason != OptimizationExclusionReason.CurrentModelQualityEvidenceUnavailable
                && !IsNonConflictingGgufBaselineQualityExclusion(
                    input,
                    baseline,
                    exclusion)
                && string.Equals(
                exclusion.CanonicalDescriptor,
                input.Baseline.OptimizationDescriptor,
                StringComparison.Ordinal));
        if (baseline.Fit.State is CompatibilityFitState.Safe
                or CompatibilityFitState.Narrow
            && currentFrontierExplicitlyExcludesBaseline)
        {
            if (HasVerifiedAlternativeToKnownFailedDefault(input, baseline, alternatives))
            {
                CompatibilityOptimizationView? verified = BuildOptimizationView(alternatives);
                if (verified is not null)
                    return new ProjectionDecision(CompatibilityScreenState.OptimisationRequired,
                        verified, baselineSetup, BaselineUnavailable: true);
            }
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-baseline-exclusion-conflict");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        if (baseline.Fit.State is CompatibilityFitState.Safe
            or CompatibilityFitState.Narrow)
        {
            return new ProjectionDecision(
                CompatibilityScreenState.EstimatedCompatible,
                null,
                baselineSetup);
        }

        if (baseline.Fit.State != CompatibilityFitState.DoesNotFit)
        {
#if DEBUG
            WriteCompatibilityDiagnostic(
                "Granite.Compatibility branch=projection-baseline-fit-unsupported");
#endif
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        if (alternatives.Count > 0)
        {
            CompatibilityOptimizationView? view = BuildOptimizationView(alternatives);
#if DEBUG
            if (view is null)
            {
                WriteCompatibilityDiagnostic(
                    "Granite.Compatibility branch=projection-alternative-description-unavailable");
            }
#endif
            return view is null
                ? new ProjectionDecision(
                    CompatibilityScreenState.NotEstablished, null, null)
                : new ProjectionDecision(
                    CompatibilityScreenState.OptimisationRequired,
                    view,
                    baselineSetup);
        }

        OptimizationExclusion? smallestOptimizedExclusion = input.Generated.Exclusions
            .Where(exclusion =>
                exclusion.Reason is (
                    OptimizationExclusionReason.ExceedsSafeMemoryBudget
                    or OptimizationExclusionReason.InsufficientDiskSpace)
                && exclusion.CanonicalDescriptor != input.Baseline.OptimizationDescriptor
                && exclusion.EstimatedRequiredBytes.HasValue)
            .OrderBy(exclusion => RequiredWithCalibrationMargin(
                exclusion.EstimatedRequiredBytes!.Value))
            .ThenBy(exclusion => exclusion.CanonicalDescriptor, StringComparer.Ordinal)
            .FirstOrDefault();
        ulong? smallestOptimizedRequiredBytes = smallestOptimizedExclusion is null
            ? null
            : RequiredWithCalibrationMargin(
                smallestOptimizedExclusion.EstimatedRequiredBytes!.Value);
        CompatibilityStorageRequirementView? storageRequirement =
            smallestOptimizedExclusion is
            {
                Reason: OptimizationExclusionReason.InsufficientDiskSpace,
                EstimatedDiskRequiredBytes: { } requiredDisk,
                AvailableDiskBytes: { } availableDisk
            }
                ? new CompatibilityStorageRequirementView(requiredDisk, availableDisk)
                : null;

        return new ProjectionDecision(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            null,
            baselineSetup,
            smallestOptimizedRequiredBytes,
            storageRequirement);
    }

    private static bool HasVerifiedAlternativeToKnownFailedDefault(
        CompatibilityOptimizationProjectionInput input,
        EvaluatedCandidate baseline,
        IReadOnlyList<OptimizationCandidate> alternatives)
    {
        if (baseline.Candidate.Configuration is not OpenVinoRouteConfiguration configuration
            || input.Snapshot.OpenVino is not { } openVino
            || !openVino.ExecutionAuthorities.TryGetValue("OV-STD-CPU-INT4-DEFAULT-01", out var current)
            || !VerifiedOpenVinoOptimizationEvidence.IsKnownFailedRetainedDefault(
                input.Binding.ModelSha256, input.Facts.ParameterCount, current,
                configuration, baseline.Context.Tokens))
            return false;
        return alternatives.Count > 0 && alternatives.All(candidate =>
            candidate.Evidence is { } evidence
            && openVino.ExecutionAuthorities.TryGetValue(evidence.EvidenceId, out var execution)
            && VerifiedOpenVinoOptimizationEvidence.IsCurrentRetainedU4Evidence(evidence, execution));
    }

    private static ulong RequiredWithCalibrationMargin(ulong predictedPeakBytes)
    {
        ByteCount peak = ByteCount.FromBytes(predictedPeakBytes);
        SafetyPolicy safety = SafetyPolicy.ProportionalV2();
        return peak.Add(safety.CalibrationMarginFor(peak)).Bytes;
    }

    private static bool ValidGeneratedAuthority(
        CompatibilityOptimizationProjectionInput input)
    {
        if (!CrossRouteCandidateGenerator.HasMatchingAuthority(
                input.Generated, input.Snapshot, input.Facts, input.Workload,
                input.Binding, input.SafeBudget, input.AvailableDisk,
                input.Policy, input.OptedInExperimentalEvidenceIds,
                input.HardwareAuthority))
        {
            return false;
        }

        if (input.Generated.Candidates
                .GroupBy(candidate => candidate.CanonicalDescriptor, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            return false;
        }

        if (input.Generated.Exclusions
            .GroupBy(
                exclusion => (exclusion.EvidenceId, exclusion.CanonicalDescriptor),
                EqualityComparer<(string EvidenceId, string CanonicalDescriptor)>.Default)
            .Any(group => group.Count() != 1))
        {
            return false;
        }

        foreach (OptimizationExclusion exclusion in input.Generated.Exclusions)
        {
            if (string.IsNullOrWhiteSpace(exclusion.EvidenceId)
                || string.IsNullOrWhiteSpace(exclusion.CanonicalDescriptor)
                || exclusion.Reason == OptimizationExclusionReason.None
                || !Enum.IsDefined(exclusion.Reason))
            {
                return false;
            }
        }

        foreach (OptimizationCandidate candidate in input.Generated.Candidates)
        {
            OptimizationAdmissionProof? proof = candidate.AdmissionProof;
            if (candidate.Route != input.Snapshot.Route
                || !Enum.IsDefined(candidate.Route)
                || !Enum.IsDefined(candidate.Metrics.Quality)
                || !Enum.IsDefined(candidate.Metrics.Performance)
                || !Enum.IsDefined(candidate.Metrics.Stability)
                || !Enum.IsDefined(candidate.Notice)
                || !Enum.IsDefined(candidate.ConversionProvenance)
                || proof is null
                || !proof.MatchesCandidate(candidate)
                || !proof.MatchesAuthority(
                    input.Snapshot, input.Workload, input.Binding)
                || !OptimizationSupportLevelPolicy.IsAdmitted(proof.SupportLevel)
                || !candidate.Metrics.FitsSafely
                || !candidate.Metrics.FitsDiskSafely)
            {
                return false;
            }
        }

        return true;
    }

    private static CompatibilityOptimizationView? BuildOptimizationView(
        IReadOnlyList<OptimizationCandidate> alternatives)
    {
        OptimizationPreferenceSelection automatic =
            OptimizationPreferenceSelection.Automatic();
        OptimizationSelection? recommended =
            OptimizationPreferenceResolver.Resolve(alternatives, automatic);
        if (recommended is null)
        {
            return null;
        }

        List<CompatibilityOptimizationModeView> modes =
        [CreateMode(CompatibilityOptimizationLabelCode.Automatic, null, recommended)];
        HashSet<string> recommendedIdentities = new(StringComparer.Ordinal)
        {
            OptimizationPreferenceResolver.CandidateIdentity(recommended.Candidate)
        };

        (CompatibilityOptimizationLabelCode Label, int Slider)[] manual =
        [
            (CompatibilityOptimizationLabelCode.MaximumEfficiency, 10),
            (CompatibilityOptimizationLabelCode.Efficient, 30),
            (CompatibilityOptimizationLabelCode.Balanced, 50),
            (CompatibilityOptimizationLabelCode.HighCapability, 70),
            (CompatibilityOptimizationLabelCode.MaximumCapability, 90)
        ];

        foreach ((CompatibilityOptimizationLabelCode label, int slider) in manual)
        {
            OptimizationSelection? selection = OptimizationPreferenceResolver.Resolve(
                alternatives, OptimizationPreferenceSelection.Manual(slider));
            if (selection is null)
            {
                return null;
            }

            modes.Add(CreateMode(label, slider, selection));
            recommendedIdentities.Add(OptimizationPreferenceResolver.CandidateIdentity(selection.Candidate));
        }

        OptimizationCandidate chosen = recommended.Candidate;
        List<CompatibilityExactOptimizationModeView> exactModes = [];
        foreach (string identity in alternatives
            .OrderBy(candidate => candidate.IsExperimental)
            .ThenBy(candidate => OptimizationPreferenceResolver.CandidateIdentity(candidate), StringComparer.Ordinal)
            .Select(OptimizationPreferenceResolver.CandidateIdentity)
            .Distinct(StringComparer.Ordinal))
        {
            OptimizationSelection? exact = OptimizationPreferenceResolver.Resolve(
                alternatives, OptimizationPreferenceSelection.Exact(identity));
            if (exact is not null)
            {
                exactModes.Add(new CompatibilityExactOptimizationModeView(identity,
                    CreateMode(CompatibilityOptimizationLabelCode.Automatic, null, exact)));
            }
        }
        List<CompatibilityExactOptimizationModeView> safeSliderModes = [];
        Dictionary<string, CompatibilityExactOptimizationModeView> exactByIdentity = new(StringComparer.Ordinal);
        foreach (CompatibilityExactOptimizationModeView exactMode in exactModes)
            exactByIdentity.Add(exactMode.CandidateIdentity, exactMode);
        HashSet<string> sliderIdentities = new(StringComparer.Ordinal);
        int? selectedSliderIndex = null;
        foreach (OptimizationCandidate candidate in alternatives
            .Where(candidate => !candidate.IsExperimental && candidate.Evidence is { IsAdmitted: true }
                && candidate.Metrics.FitsSafely && candidate.Metrics.FitsDiskSafely)
            .OrderBy(candidate => candidate.Metrics.PredictedPeakBytes)
            .ThenBy(candidate => candidate.QualityScore)
            .ThenBy(candidate => candidate.CanonicalDescriptor, StringComparer.Ordinal))
        {
            string identity = OptimizationPreferenceResolver.CandidateIdentity(candidate);
            if (exactByIdentity.TryGetValue(identity, out CompatibilityExactOptimizationModeView? mode)
                && sliderIdentities.Add(identity))
            {
                if (identity == OptimizationPreferenceResolver.CandidateIdentity(chosen))
                    selectedSliderIndex = safeSliderModes.Count;
                safeSliderModes.Add(mode);
            }
        }
        return new CompatibilityOptimizationView(
            CompatibilityOptimizationLabelCode.Automatic,
            recommendedSliderValue: null,
            modes,
            chosen.Metrics.RequiresPersistentChange,
            chosen.ConversionProvenance
                == OptimizationConversionProvenance.ControlledRequantisation,
            NoticeFor(chosen), exactModes,
            exactModes.Any(mode => !recommendedIdentities.Contains(mode.CandidateIdentity)), safeSliderModes,
            selectedSliderIndex);
    }

    private static CompatibilityOptimizationModeView CreateMode(
        CompatibilityOptimizationLabelCode label,
        int? slider,
        OptimizationSelection selection)
    {
        OptimizationCandidate candidate = selection.Candidate;
        GgufRouteConfiguration? gguf =
            candidate.Configuration as GgufRouteConfiguration;
        OpenVinoRouteConfiguration? openVino =
            candidate.Configuration as OpenVinoRouteConfiguration;

        return new CompatibilityOptimizationModeView(
            label,
            slider,
            candidate.Route,
            gguf?.Weights,
            gguf?.KvCache,
            openVino?.Weights,
            openVino?.KvCache,
            gguf?.Device ?? openVino?.Device ?? DeviceRouteId.Unspecified,
            candidate.Metrics.Quality,
            candidate.Metrics.ContextTokens,
            candidate.Metrics.PredictedPeakBytes,
            candidate.Metrics.SafeBudgetBytes,
            candidate.Metrics.HeadroomBytes,
            candidate.Metrics.RequiresPersistentChange,
            candidate.ConversionProvenance
                == OptimizationConversionProvenance.ControlledRequantisation,
            NoticeFor(candidate),
            candidate.IsExperimental,
            selection.SharedWithAdjacentBand,
            candidate.Metrics.DedicatedRequiredBytes,
            candidate.Metrics.DedicatedSafeBudgetBytes,
            candidate.Metrics.DedicatedHeadroomBytes,
            candidate.QualityLevel);
    }

    private static OptimizationQualityNotice NoticeFor(
        OptimizationCandidate candidate)
    {
        if (candidate.Metrics.Quality == OptimizationAssessment.Poor)
        {
            return OptimizationQualityNotice.SignificantQualityReduction;
        }

        bool requantises = candidate.ConversionProvenance
            == OptimizationConversionProvenance.ControlledRequantisation;
        return candidate.Metrics.Quality switch
        {
            OptimizationAssessment.Excellent when !requantises =>
                OptimizationQualityNotice.None,
            OptimizationAssessment.Good when !requantises =>
                OptimizationQualityNotice.None,
            OptimizationAssessment.Good =>
                OptimizationQualityNotice.SomeQualityReduction,
            OptimizationAssessment.Acceptable when requantises =>
                OptimizationQualityNotice.NoticeableQualityReduction,
            OptimizationAssessment.Acceptable =>
                OptimizationQualityNotice.SomeQualityReduction,
            _ => OptimizationQualityNotice.NoticeableQualityReduction
        };
    }

    /// <summary>
    /// Picks the setup the screen speaks for and flattens it.
    ///
    /// Balanced is preferred because it is what a user who expresses no
    /// preference is given. Where no mode resolved, the best-fitting evaluated
    /// candidate stands in, so a screen saying nothing fits can still show how
    /// close the closest attempt came.
    /// </summary>
    private static CompatibilitySetupView? DescribeSetup(CompatibilityAssessment? assessment)
    {
        if (assessment is null || assessment.EvaluatedCandidates.Count == 0)
        {
            return null;
        }

        CandidateFingerprint? preferred = assessment.ModeSelections
            .FirstOrDefault(selection =>
                selection.Mode == CompatibilityMode.Balanced
                && selection.SelectedFingerprint is not null)
            ?.SelectedFingerprint
            ?? assessment.ModeSelections
                .FirstOrDefault(selection => selection.SelectedFingerprint is not null)
                ?.SelectedFingerprint;

        EvaluatedCandidate? chosen = preferred is { } fingerprint
            ? assessment.EvaluatedCandidates
                .FirstOrDefault(candidate => candidate.Fingerprint == fingerprint)
            : null;

        // Nothing was admitted, so the screen is explaining a refusal. The
        // candidate that came closest is the one worth showing, because it is
        // the one whose figures tell the user what would have to change.
        chosen ??= assessment.EvaluatedCandidates
            .OrderBy(candidate => Rank(candidate.Fit.State))
            .ThenBy(candidate => candidate.Fit.PressureRatio)
            .First();

        return Describe(chosen);
    }

    /// <summary>
    /// How close a fit state came to being usable, best first.
    ///
    /// Ranked explicitly rather than by the enum's own order, which is written
    /// for readability and would otherwise silently decide which setup a user
    /// is shown.
    /// </summary>
    /// <summary>
    /// The gap between what the components come to and what the fit policy
    /// demanded. Saturates at zero so a policy that stops adding a margin
    /// cannot produce a negative segment.
    /// </summary>
    private static ulong Allowance(EvaluatedCandidate candidate)
    {
        ulong peak = candidate.Peaks.SystemMemoryPressure.Bytes;
        ulong required = candidate.Fit.RequiredBytes.Bytes;

        return required > peak ? required - peak : 0;
    }

    private static int Rank(CompatibilityFitState state) => state switch
    {
        CompatibilityFitState.Safe => 0,
        CompatibilityFitState.Narrow => 1,
        CompatibilityFitState.DoesNotFit => 2,
        CompatibilityFitState.Unsupported => 3,
        _ => 4
    };

    private static CompatibilitySetupView? Describe(EvaluatedCandidate candidate)
    {
        GgufRouteConfiguration? gguf = candidate.Candidate.Configuration as GgufRouteConfiguration;
        OpenVinoRouteConfiguration? openVino =
            candidate.Candidate.Configuration as OpenVinoRouteConfiguration;
        DeviceRouteId device = gguf?.Device
            ?? openVino?.Device
            ?? DeviceRouteId.Unspecified;
        CompatibilityBackend backend = gguf?.Backend
            ?? openVino?.Device switch
            {
                DeviceRouteId.Cpu => CompatibilityBackend.OpenVinoCpu,
                DeviceRouteId.IntelIntegratedGpu
                    or DeviceRouteId.IntelDiscreteGpu =>
                    CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelNpu => CompatibilityBackend.OpenVinoNpu,
                _ => CompatibilityBackend.Unspecified
            };
        if (device == DeviceRouteId.Unspecified
            || backend == CompatibilityBackend.Unspecified
            || !Enum.IsDefined(device)
            || !Enum.IsDefined(backend))
        {
            return null;
        }

        return new CompatibilitySetupView(
            candidate.Candidate.RouteId,
            backend,
            device,
            candidate.EffectiveQuantisation,
            candidate.Context.Tokens,
            candidate.Fit.State,
            candidate.Fit.RequiredBytes.Bytes,
            candidate.Fit.SafeBudget.Bytes,
            candidate.Fit.Headroom.Bytes,
            Allowance(candidate),
            candidate.IsExperimental,
            candidate.Preparation == CandidatePreparation.WeightConversionRequired,
            BreakDown(candidate.Estimate.Components),
            candidate.Fit.DedicatedRequiredBytes == ByteCount.Zero
                ? null
                : candidate.Fit.DedicatedRequiredBytes.Bytes,
            candidate.Fit.DedicatedRequiredBytes == ByteCount.Zero
                ? null
                : candidate.Fit.DedicatedSafeBudget.Bytes,
            candidate.Fit.DedicatedRequiredBytes == ByteCount.Zero
                ? null
                : candidate.Fit.DedicatedHeadroom.Bytes,
            gguf?.KvCache,
            openVino?.KvCache);
    }

    /// <summary>
    /// The components that are live at the moment memory pressure peaks.
    ///
    /// A component only counts when it is present in the phase that decided the
    /// peak, so these sum to the requirement rather than exceeding it. Listing
    /// every component regardless of phase would produce a breakdown larger
    /// than the total it claims to explain.
    /// </summary>
    private static IReadOnlyList<CompatibilityComponentView> BreakDown(
        IReadOnlyList<ResourceComponent> components)
    {
        ResourceTarget[] charged =
        [
            ResourceTarget.SystemMemory,
            ResourceTarget.SharedDeviceMemory
        ];

        LifecyclePhase[] phases =
        [
            LifecyclePhase.Load,
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        ];

        LifecyclePhase peak = LifecyclePhase.SteadyStateGeneration;
        ulong largest = 0;

        foreach (LifecyclePhase phase in phases)
        {
            ulong total = 0;

            foreach (ResourceComponent component in components)
            {
                if (charged.Contains(component.Target) && component.Phases.Contains(phase))
                {
                    total += component.Bytes.Bytes;
                }
            }

            if (total > largest)
            {
                largest = total;
                peak = phase;
            }
        }

        return
        [
            .. components
                .Where(component =>
                    charged.Contains(component.Target) && component.Phases.Contains(peak))
                .GroupBy(component => component.Kind)
                .Select(group => new CompatibilityComponentView(
                    group.Key,
                    group.Aggregate(0UL, (sum, component) => sum + component.Bytes.Bytes)))
                .OrderByDescending(view => view.Bytes)
        ];
    }

    private static CompatibilityScreenState DecideState(CompatibilityRunResult result)
    {
        if (result.Outcome == CompatibilityRunOutcome.Cancelled)
        {
            return CompatibilityScreenState.Cancelled;
        }

        // A failed run reached no conclusion, so it shows the same screen as one
        // that could not establish anything — the user's position is identical.
        if (result.Outcome is CompatibilityRunOutcome.NotEstablished
            or CompatibilityRunOutcome.Failed
            or CompatibilityRunOutcome.Unspecified
            || result.Assessment is not { } assessment)
        {
            return CompatibilityScreenState.NotEstablished;
        }

        IReadOnlyList<EvaluatedCandidate> admitted =
        [
            .. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Fit.State is CompatibilityFitState.Safe
                    or CompatibilityFitState.Narrow)
        ];

        if (admitted.Count == 0)
        {
            return CompatibilityScreenState.NoEstimatedSafeConfiguration;
        }

        EvaluatedCandidate[] baselines = assessment.BaselineFingerprint is { } fingerprint
            ? [.. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Fingerprint == fingerprint)]
            : [];
        if (baselines.Length > 1)
        {
            return CompatibilityScreenState.NotEstablished;
        }

        EvaluatedCandidate? baseline = baselines.FirstOrDefault();

        // A narrow baseline still runs as imported. Narrowness is a warning on
        // that setup, not an instruction to create a different model. Only a
        // failed baseline with a separate admitted setup is optimisation-
        // required.
        if (baseline?.Fit.State is CompatibilityFitState.Safe
            or CompatibilityFitState.Narrow)
        {
            return CompatibilityScreenState.EstimatedCompatible;
        }

        return CompatibilityScreenState.OptimisationRequired;
    }

#if DEBUG
    private static void WriteCompatibilityDiagnostic(string message)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
        catch
        {
            // Diagnostics must never alter an operational outcome.
        }
    }
#endif

    private static bool IsNonConflictingGgufBaselineQualityExclusion(
        CompatibilityOptimizationProjectionInput input,
        EvaluatedCandidate baseline,
        OptimizationExclusion exclusion)
    {
        if (exclusion.Reason != OptimizationExclusionReason.EvidenceBelowAdmissionLevel
            || input.Snapshot.Route != OptimizationRoute.Gguf
            || input.Baseline.Preparation != CandidatePreparation.None
            || baseline.Fit.State is not (
                CompatibilityFitState.Safe or CompatibilityFitState.Narrow)
            || input.Snapshot.Gguf is not { RuntimeAuthority: { } runtime }
            || !string.Equals(
                runtime.RuntimeBuildId,
                PublishedGgufOptimizationEvidence.RuntimeBuildId,
                StringComparison.Ordinal)
            || !string.Equals(
                runtime.RuntimeSourceCommit,
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit,
                StringComparison.Ordinal)
            || WeightQuantisationMap.FromGgufFileType(
                input.Facts.FileType,
                input.Facts.QuantisationVersion) != WeightQuantisation.Q4_K_M
                && !IsPinnedDownloadCurrentSource(input))
        {
            return false;
        }

        GgufRouteConfiguration current = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);
        if (!string.Equals(
                input.Baseline.ConfigurationDescriptor,
                current.CanonicalDescriptor,
                StringComparison.Ordinal)
            || !string.Equals(
                exclusion.CanonicalDescriptor,
                input.Baseline.OptimizationDescriptor,
                StringComparison.Ordinal))
        {
            return false;
        }

        GgufAdmittedConfiguration[] matchingAdmissions =
        [
            .. input.Snapshot.Gguf.Admitted.Where(admission =>
                string.Equals(
                    admission.EvidenceId,
                    exclusion.EvidenceId,
                    StringComparison.Ordinal)
                && admission.Weights == GgufWeightFormat.Imported
                && admission.KvCache == GgufKvCacheFormat.F16
                && admission.Backend == CompatibilityBackend.Cpu
                && admission.Device == DeviceRouteId.Cpu
                && admission.Offload == GpuOffloadLevel.None
                && admission.Level == SupportLevel.DeclaredSupported
                && !admission.RequiresEvidence
                && admission.MinimumContextTokens <= input.Baseline.ContextTokens
                && admission.MaximumContextTokens >= input.Baseline.ContextTokens
                && runtime.Profiles.ContainsKey(admission.EvidenceId))
        ];
        if (matchingAdmissions.Length != 1)
        {
            return false;
        }

        return true;
    }

    private static bool IsPinnedDownloadCurrentSource(
        CompatibilityOptimizationProjectionInput input) =>
        // The unchanged pinned download can retain its independent memory
        // verdict without becoming an evidence-qualified optimisation candidate.
        // Match the exact catalogue bytes and encoding, never just a file name.
        (input.Facts.FileType switch
        {
            10 => input.Binding.ModelLengthBytes == 1_226_247_840UL
                && string.Equals(input.Binding.ModelSha256,
                    "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead",
                    StringComparison.Ordinal),
            12 => input.Binding.ModelLengthBytes == 1_555_472_032UL
                && string.Equals(input.Binding.ModelSha256,
                    "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29",
                    StringComparison.Ordinal),
            17 => input.Binding.ModelLengthBytes == 2_273_455_776UL
                && string.Equals(input.Binding.ModelSha256,
                    "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c",
                    StringComparison.Ordinal),
            7 => input.Binding.ModelLengthBytes == 3_397_676_704UL
                && string.Equals(input.Binding.ModelSha256,
                    "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59",
                    StringComparison.Ordinal),
            _ => false
        })
        && input.Facts.QuantisationVersion == 2
        && input.Facts.LayerCount == 40
        && input.Facts.EmbeddingSize == 2048
        && input.Facts.AttentionHeadCount == 32
        && input.Facts.KeyValueHeadCount == 8
        && input.Facts.DeclaredContextLimit == 1_048_576;
}
