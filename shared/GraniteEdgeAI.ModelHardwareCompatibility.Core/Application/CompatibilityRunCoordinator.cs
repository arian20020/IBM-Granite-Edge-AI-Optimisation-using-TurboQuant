using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;

/// <summary>
/// Everything a run needs, supplied by the caller.
///
/// BaselineConfiguration is an input rather than a derivation: what the user
/// already has is a fact about their import, and this feature cannot invent it.
/// Until the owner adapters exist, callers pass the llama.cpp default.
/// </summary>
internal sealed record CompatibilityRunDependencies
{
    private CompatibilityRunDependencies(
        ICompatibilityInputGateway gateway,
        IInspectedModelFactsSource modelFactsSource,
        IHardwareFactsSource hardwareFactsSource,
        IFreshSystemMemoryProbe memoryProbe,
        SupportMatrix matrix,
        EstimatorPolicy estimatorPolicy,
        SafetyPolicy safetyPolicy,
        IReadOnlySet<string> optedInExperimentalEntryIds,
        TrustedSourceAvailability trustedSource,
        GgufRouteConfiguration baselineConfiguration,
        ContextTokenCount baselineContext,
        TimeProvider clock)
    {
        Gateway = gateway;
        ModelFactsSource = modelFactsSource;
        HardwareFactsSource = hardwareFactsSource;
        MemoryProbe = memoryProbe;
        Matrix = matrix;
        EstimatorPolicy = estimatorPolicy;
        SafetyPolicy = safetyPolicy;
        OptedInExperimentalEntryIds = optedInExperimentalEntryIds;
        TrustedSource = trustedSource;
        BaselineConfiguration = baselineConfiguration;
        BaselineContext = baselineContext;
        Clock = clock;
    }

    internal ICompatibilityInputGateway Gateway { get; }

    internal IInspectedModelFactsSource ModelFactsSource { get; }

    internal IHardwareFactsSource HardwareFactsSource { get; }

    internal IFreshSystemMemoryProbe MemoryProbe { get; }

    internal SupportMatrix Matrix { get; }

    internal EstimatorPolicy EstimatorPolicy { get; }

    internal SafetyPolicy SafetyPolicy { get; }

    internal IReadOnlySet<string> OptedInExperimentalEntryIds { get; }

    internal TrustedSourceAvailability TrustedSource { get; }

    /// <summary>
    /// The configuration the user already has. The baseline-matching path
    /// expects its weight format to be <c>Imported</c>; an adapter passing the
    /// file's actual encoding instead would silently lose the baseline.
    /// </summary>
    internal GgufRouteConfiguration BaselineConfiguration { get; }

    internal ContextTokenCount BaselineContext { get; }

    internal TimeProvider Clock { get; }

    internal static CompatibilityRunDependencies Create(
        ICompatibilityInputGateway gateway,
        IInspectedModelFactsSource modelFactsSource,
        IHardwareFactsSource hardwareFactsSource,
        IFreshSystemMemoryProbe memoryProbe,
        SupportMatrix matrix,
        EstimatorPolicy estimatorPolicy,
        SafetyPolicy safetyPolicy,
        IReadOnlySet<string> optedInExperimentalEntryIds,
        TrustedSourceAvailability trustedSource,
        GgufRouteConfiguration baselineConfiguration,
        ContextTokenCount baselineContext,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(modelFactsSource);
        ArgumentNullException.ThrowIfNull(hardwareFactsSource);
        ArgumentNullException.ThrowIfNull(memoryProbe);
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(estimatorPolicy);
        ArgumentNullException.ThrowIfNull(safetyPolicy);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEntryIds);
        ArgumentNullException.ThrowIfNull(trustedSource);
        ArgumentNullException.ThrowIfNull(baselineConfiguration);
        ArgumentNullException.ThrowIfNull(clock);

        return new CompatibilityRunDependencies(
            gateway,
            modelFactsSource,
            hardwareFactsSource,
            memoryProbe,
            matrix,
            estimatorPolicy,
            safetyPolicy,
            new HashSet<string>(optedInExperimentalEntryIds),
            trustedSource,
            baselineConfiguration,
            baselineContext,
            clock);
    }
}

/// <summary>
/// Walks the run lifecycle in order.
///
/// Two properties matter more than the sequence itself. Fresh memory is probed
/// exactly once and that single observation is shared by every candidate, so no
/// two candidates are judged against different machines. And every unavailable
/// seam collapses the run to NotEstablished naming what was missing — this
/// feature would rather say "I cannot tell you" than produce a number it cannot
/// stand behind.
/// </summary>
internal static class CompatibilityRunCoordinator
{
    internal static CompatibilityRunResult Execute(
        CompatibilityRunRequest request,
        CompatibilityRunDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(dependencies);

        CompatibilityRunId runId = CompatibilityRunId.New();
        DateTimeOffset startedAt = dependencies.Clock.GetUtcNow();

        List<CompatibilityFinding> findings = [];
        IReadOnlyList<PolicyIdentity> policies = DescribePolicies(dependencies);

        // Mandatory while any policy in use is provisional: nothing built on
        // documented defaults may be presented as measured.
        if (IsProvisional(dependencies))
        {
            findings.Add(CompatibilityFinding.Create(
                CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Warning));
        }

        CompatibilityRunResult Stop(
            CompatibilityRunOutcome outcome, CompatibilityFindingCode? code)
        {
            if (code is { } value)
            {
                findings.Add(CompatibilityFinding.Create(value, FindingSeverity.Blocking));
            }

            DateTimeOffset completedAt = dependencies.Clock.GetUtcNow();

            return outcome switch
            {
                CompatibilityRunOutcome.Cancelled => CompatibilityRunResult.Cancelled(
                    runId, findings, policies, startedAt, completedAt),
                CompatibilityRunOutcome.Failed => CompatibilityRunResult.Failed(
                    runId, findings, policies, startedAt, completedAt),
                _ => CompatibilityRunResult.NotEstablished(
                    runId, findings, policies, startedAt, completedAt)
            };
        }

        try
        {
            // The claim is inside the boundary because building one can throw: an
            // adapter that takes the claim and then hands back a path-shaped
            // identity would otherwise leave the claim held and block every later
            // run.
            HandoffClaim claim = dependencies.Gateway.Claim();

            if (!claim.IsClaimed)
            {
                return Stop(
                    CompatibilityRunOutcome.Failed, CompatibilityFindingCode.HandoffClaimFailed);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            ModelFactsResolution model =
                dependencies.ModelFactsSource.Resolve(claim.ModelInspectionRunId);

            if (!model.IsEstablished || model.Facts is not { } modelFacts)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.ModelFactsUnavailable);
            }

            HardwareFactsResolution hardware =
                dependencies.HardwareFactsSource.Resolve(claim.ProductHardwareRunId);

            if (!hardware.IsEstablished || hardware.Facts is not { } machineFacts)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.HardwareFactsUnavailable);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            // InspectedModelFacts validates the limit as positive when present, so
            // the widening cast cannot turn a negative into an enormous unsigned.
            PlanningContextResolution context = PlanningContextPolicy.Resolve(
                request.Context,
                modelFacts.DeclaredContextLimit is { } declaredLimit
                    ? (ulong)declaredLimit
                    : null);

            if (context.Status != PlanningContextStatus.Resolved
                || context.ResolvedTokens is not { } preservationTarget)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.PlanningContextNotEstablished);
            }

            if (dependencies.Matrix.Provenance is PolicyProvenance.Absent
                or PolicyProvenance.Unspecified)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.SupportMatrixUnavailable);
            }

            CandidateGenerationResult generated = CandidateGenerator.Generate(
                CandidateGenerationRequest.Create(
                    dependencies.Matrix,
                    CapabilityProjection.Project(
                        dependencies.Matrix,
                        machineFacts,
                        dependencies.OptedInExperimentalEntryIds),
                    modelFacts,
                    dependencies.BaselineConfiguration,
                    dependencies.BaselineContext,
                    preservationTarget,
                    dependencies.TrustedSource));

            if (generated.Candidates.Count == 0)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.NoCandidateGenerated);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            WeightQuantisation importedEncoding = WeightQuantisationMap.FromGgufFileType(
                modelFacts.FileType, modelFacts.QuantisationVersion);

            // Estimation first, then one probe, then the gate. Spec section 6 puts
            // the reading immediately before the gate so the figure the budget is
            // compared against is as fresh as it can be.
            List<(CompatibilityCandidate Candidate, ResourceEstimate Estimate,
                ResourcePeakProfile Peaks)> sized = [];

            foreach (CompatibilityCandidate candidate in generated.Candidates)
            {
                ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                    modelFacts, candidate, dependencies.EstimatorPolicy);

                if (estimate.Status != EstimationStatus.Established)
                {
                    continue;
                }

                sized.Add((
                    candidate, estimate, ResourcePhaseComposer.Compose(estimate.Components)));
            }

            // Nothing sized is a different answer from everything sized and
            // nothing fitting. Reporting the second when the first happened would
            // tell the user we checked their machine when we never computed a
            // requirement to check it against.
            if (sized.Count == 0)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.NoCandidateCouldBeEstimated);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            // One observation, taken here and shared by every candidate below.
            // Probing per candidate would let two candidates be judged against
            // different machines, and the comparison between them would be a lie.
            FreshMemoryReading reading = dependencies.MemoryProbe.Probe();

            if (!reading.IsEstablished || reading.Resources is not { } available)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.FreshMemoryUnavailable);
            }

            List<EvaluatedCandidate> evaluated =
            [
                .. sized.Select(entry => EvaluatedCandidate.Create(
                    entry.Candidate,
                    entry.Estimate,
                    entry.Peaks,
                    FitPolicy.Assess(entry.Peaks, available, dependencies.SafetyPolicy),
                    EffectiveEncoding(entry.Candidate, importedEncoding),
                    EvidenceGrade.Estimated,
                    PerformanceIndicator.NotEstablished(),
                    entry.Peaks.PeakFor(ResourceTarget.Storage)))
            ];

            IReadOnlySet<string> evidenceRequiring =
                new HashSet<string>(dependencies.Matrix.Entries
                    .Where(entry => entry.RequiresEvidence)
                    .Select(entry => entry.EntryId));

            ModeSelectionOutcome modes = ModeSelector.SelectAll(
                ModeSelectionRequest.Create(
                    evaluated,
                    evidenceRequiring,
                    preservationTarget,
                    dependencies.BaselineConfiguration));

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            // Every candidate was sized and compared, and none fit. That is a
            // real conclusion, unlike the empty case handled above.
            if (!modes.Selections.Any(selection =>
                selection.Availability == ModeAvailability.Available))
            {
                findings.Add(CompatibilityFinding.Create(
                    CompatibilityFindingCode.NoSafeConfigurationFound,
                    FindingSeverity.Warning));
            }

            if (!generated.BaselineIncluded)
            {
                findings.Add(CompatibilityFinding.Create(
                    CompatibilityFindingCode.BaselineConfigurationUnavailable,
                    FindingSeverity.Warning));
            }

            // Only publish the baseline's fingerprint if it survived estimation;
            // naming one absent from the evaluated set would point at nothing.
            //
            // Read off the baseline candidate itself rather than the first
            // generated one. The generator does put the baseline at index 0, but
            // that is a promise in another file with nothing holding it: if it
            // ever changed, this would publish a stranger's fingerprint as the
            // user's current setup, and "use what I have" would silently mean
            // something else.
            CandidateFingerprint? baselineFingerprint = generated.BaselineIncluded
                ? evaluated
                    .FirstOrDefault(candidate => candidate.Candidate.IsBaseline)
                    ?.Fingerprint
                : null;

            CompatibilityAssessment assessment = CompatibilityAssessment.Create(
                evaluated,
                modes.Selections,
                baselineFingerprint,
                generated.BaselineExclusionReason,
                modes.UseCurrentModelAvailable);

            return CompatibilityRunResult.Completed(
                runId,
                assessment,
                findings,
                policies,
                startedAt,
                dependencies.Clock.GetUtcNow());
        }
        finally
        {
            // Nothing commits here. The transfer commits only when the user
            // confirms a choice, which is a separate action on a later screen —
            // so every exit from a run releases what it claimed.
            dependencies.Gateway.Rollback();
        }
    }

    /// <summary>
    /// What a candidate actually runs: its own encoding when it converts, the
    /// imported file's when it does not.
    /// </summary>
    private static WeightQuantisation EffectiveEncoding(
        CompatibilityCandidate candidate,
        WeightQuantisation importedEncoding)
    {
        // A route this method does not understand is not a route whose weights
        // happen to be the imported ones. Returning the imported encoding for
        // it would state a quality tier nothing established, and that tier
        // orders every mode - so the wrong answer here does not look wrong, it
        // just quietly ranks a setup above one that deserved to win.
        //
        // Unknown is the honest value. Comparers already treat it as carrying
        // no bits rather than as a tier, and it keeps this dormant while GGUF
        // is the only route.
        if (candidate.Configuration is not GgufRouteConfiguration configuration)
        {
            return WeightQuantisation.Unknown;
        }

        return configuration.Weights == GgufWeightFormat.Imported
            ? importedEncoding
            : GgufWeightFormatMap.ToCanonical(configuration.Weights);
    }

    private static bool IsProvisional(CompatibilityRunDependencies dependencies) =>
        dependencies.EstimatorPolicy.Provenance == PolicyProvenance.Provisional
        || dependencies.SafetyPolicy.Provenance == PolicyProvenance.Provisional
        || dependencies.Matrix.Provenance == PolicyProvenance.Provisional;

    private static IReadOnlyList<PolicyIdentity> DescribePolicies(
        CompatibilityRunDependencies dependencies) =>
    [
        PolicyIdentity.Create(
            "estimator",
            dependencies.EstimatorPolicy.PolicyVersion,
            dependencies.EstimatorPolicy.Provenance),
        PolicyIdentity.Create(
            "safety",
            dependencies.SafetyPolicy.PolicyVersion,
            dependencies.SafetyPolicy.Provenance),
        PolicyIdentity.Create(
            "support-matrix",
            dependencies.Matrix.MatrixVersion,
            dependencies.Matrix.Provenance)
    ];
}
