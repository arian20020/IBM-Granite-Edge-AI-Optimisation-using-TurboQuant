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
            DigestSource(configuration.CanonicalDescriptor, context.Tokens, binding));
    }

    internal bool Matches(
        EvaluatedCandidate candidate,
        OptimizationJourneyBinding binding) =>
        candidate.Candidate.IsBaseline
        && candidate.Candidate.Configuration.CanonicalDescriptor
            == ConfigurationDescriptor
        && candidate.Context.Tokens == ContextTokens
        && SourceIdentitySha256 == DigestSource(
            candidate.Candidate.Configuration.CanonicalDescriptor,
            candidate.Context.Tokens,
            binding);

    private static string DigestSource(
        string configurationDescriptor,
        int contextTokens,
        OptimizationJourneyBinding binding)
    {
        string[] fields =
        [
            configurationDescriptor,
            contextTokens.ToString(CultureInfo.InvariantCulture),
            ((int)CandidatePreparation.RuntimeProfileOnly).ToString(
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
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        CompatibilityBaselineIdentity baseline)
    {
        Generated = generated;
        Snapshot = snapshot;
        Workload = workload;
        Binding = binding;
        Baseline = baseline;
    }

    internal CrossRouteGenerationResult Generated { get; }
    internal OptimizationCapabilitySnapshot Snapshot { get; }
    internal OptimizationWorkload Workload { get; }
    internal OptimizationJourneyBinding Binding { get; }
    internal CompatibilityBaselineIdentity Baseline { get; }

    internal static CompatibilityOptimizationProjectionInput Create(
        CrossRouteGenerationResult generated,
        OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload,
        OptimizationJourneyBinding binding,
        CompatibilityBaselineIdentity baseline)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(baseline);

        return new CompatibilityOptimizationProjectionInput(
            generated, snapshot, workload, binding, baseline);
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
        CompatibilityOptimizationView? optimization)
    {
        Setup = setup;
        State = state;
        Findings = findings;
        Modes = modes;
        BaselineExclusionReason = baselineExclusionReason;
        UseCurrentModelAvailable = useCurrentModelAvailable;
        ContinueEnabled = continueEnabled;
        Optimization = optimization;
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

    /// <summary>
    /// The setup the screen is describing, or null when nothing was evaluated.
    ///
    /// This is the configuration a user would actually get if they continued:
    /// the balanced choice where one exists, and otherwise the best-fitting
    /// candidate found. A screen showing figures from a setup nobody would be
    /// given would be describing a decision that was never made.
    /// </summary>
    public CompatibilitySetupView? Setup { get; }

    /// <summary>
    /// Fully resolved choices only for <see cref="CompatibilityScreenState.OptimisationRequired"/>.
    /// Null elsewhere; WinUI must not rebuild a frontier to populate it.
    /// </summary>
    public CompatibilityOptimizationView? Optimization { get; }

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
        CompatibilityOptimizationView? optimization = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(modes);

        if (state == CompatibilityScreenState.Unspecified)
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
            optimization);
    }

    internal static CompatibilityScreenModel From(CompatibilityRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        CompatibilityScreenState state = DecideState(result);

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
            DescribeSetup(result.Assessment),
            optimization: null);
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
            original.Findings,
            original.Modes,
            original.BaselineExclusionReason,
            decision.State == CompatibilityScreenState.EstimatedCompatible,
            actionable,
            decision.Setup,
            decision.Optimization);
    }

    private sealed record ProjectionDecision(
        CompatibilityScreenState State,
        CompatibilityOptimizationView? Optimization,
        CompatibilitySetupView? Setup);

    private static ProjectionDecision DecideOptimizationState(
        CompatibilityAssessment assessment,
        CompatibilityOptimizationProjectionInput input)
    {
        EvaluatedCandidate[] matchingBaselines =
        [
            .. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Candidate.IsBaseline
                && input.Baseline.Matches(candidate, input.Binding))
        ];

        if (matchingBaselines.Length != 1
            || assessment.BaselineFingerprint != matchingBaselines[0].Fingerprint
            || input.Baseline.Preparation != CandidatePreparation.RuntimeProfileOnly
            || !ValidGeneratedAuthority(input))
        {
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        EvaluatedCandidate baseline = matchingBaselines[0];
        if (!Enum.IsDefined(baseline.Fit.State))
        {
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
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        if (baseline.Fit.State is CompatibilityFitState.Safe
            or CompatibilityFitState.Narrow)
        {
            return new ProjectionDecision(
                CompatibilityScreenState.EstimatedCompatible,
                null,
                Describe(baseline));
        }

        if (baseline.Fit.State != CompatibilityFitState.DoesNotFit)
        {
            return new ProjectionDecision(
                CompatibilityScreenState.NotEstablished, null, null);
        }

        if (alternatives.Count > 0)
        {
            CompatibilityOptimizationView? view = BuildOptimizationView(alternatives);
            return view is null
                ? new ProjectionDecision(
                    CompatibilityScreenState.NotEstablished, null, null)
                : new ProjectionDecision(
                    CompatibilityScreenState.OptimisationRequired,
                    view,
                    Describe(baseline));
        }

        return new ProjectionDecision(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            null,
            Describe(baseline));
    }

    private static bool ValidGeneratedAuthority(
        CompatibilityOptimizationProjectionInput input)
    {
        if (input.Generated.Authority is not { } generationAuthority
            || !generationAuthority.Matches(
                input.Snapshot, input.Workload, input.Binding))
        {
            return false;
        }

        if (input.Generated.Candidates
                .GroupBy(candidate => candidate.CanonicalDescriptor, StringComparer.Ordinal)
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
        }

        OptimizationCandidate chosen = recommended.Candidate;
        return new CompatibilityOptimizationView(
            CompatibilityOptimizationLabelCode.Automatic,
            recommendedSliderValue: null,
            modes,
            chosen.Metrics.RequiresPersistentChange,
            chosen.ConversionProvenance
                == OptimizationConversionProvenance.ControlledRequantisation,
            NoticeFor(chosen));
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
            selection.SharedWithAdjacentBand);
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

    private static CompatibilitySetupView Describe(EvaluatedCandidate candidate)
    {
        GgufRouteConfiguration? gguf = candidate.Candidate.Configuration as GgufRouteConfiguration;

        return new CompatibilitySetupView(
            candidate.Candidate.RouteId,
            gguf?.Backend ?? CompatibilityBackend.Unspecified,
            gguf?.Device ?? DeviceRouteId.Unspecified,
            candidate.EffectiveQuantisation,
            candidate.Context.Tokens,
            candidate.Fit.State,
            candidate.Fit.RequiredBytes.Bytes,
            candidate.Fit.SafeBudget.Bytes,
            candidate.Fit.Headroom.Bytes,
            Allowance(candidate),
            candidate.IsExperimental,
            candidate.Preparation == CandidatePreparation.WeightConversionRequired,
            BreakDown(candidate.Estimate.Components));
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
}
