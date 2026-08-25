using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// The way in.
///
/// Everything the engine does is internal; this is the single door, and what it
/// returns is a decision plus the codes explaining it. A caller cannot reach the
/// estimator, the generator or the policies, which is deliberate — those are
/// reasoning, and a screen that touched them would be re-deriving the answer
/// rather than showing it.
/// </summary>
public static class CompatibilityEngine
{
    /// <summary>Runs C1 with validated production values supplied by owner adapters.</summary>
    public static CompatibilityScreenModel Run(
        CompatibilityProductionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            CompatibilityRunResult result = CompatibilityRunCoordinator.Execute(
                new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
                ProductionDependencies(input),
                cancellationToken);
            return ProjectProduction(result, input);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CompatibilityScreenModel.From(Failure());
        }
    }

    /// <summary>
    /// Runs a check with the adapters that exist today.
    ///
    /// None of them do. Every seam this needs — the paired handoffs, the model
    /// facts, the machine facts, the reading of memory free right now — ships a
    /// typed refusal until the teams that own them publish a contract. So this
    /// reliably returns "no answer yet", naming exactly what was missing, and
    /// that is the honest production behaviour rather than a placeholder.
    ///
    /// When an adapter is written it is supplied here, and the same code path
    /// starts producing real conclusions without anything else changing.
    /// </summary>
    public static CompatibilityScreenModel RunWithAvailableAdapters(
        CancellationToken cancellationToken = default)
    {
        CompatibilityRunResult result;

        try
        {
            result = CompatibilityRunCoordinator.Execute(
                new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
                Dependencies(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Assembling the dependencies happens before the coordinator's own
            // guard can reach it, so anything thrown while building them escapes
            // the whole engine.
            //
            // Nothing here throws today. But each of these becomes a real
            // adapter that reads a file, queries a driver or crosses a process
            // boundary, and the first one to throw during construction would
            // take the app down rather than produce the "we cannot tell you"
            // this feature exists to produce. The cause is withheld: section 14
            // forbids a native error reaching a screen.
            result = Failure();
        }

        return ProjectWithoutOptimizationAuthority(result);
    }

    private static CompatibilityRunDependencies Dependencies() =>
        CompatibilityRunDependencies.Create(
            UnavailablePorts.Gateway(),
            UnavailablePorts.ModelFacts(),
            UnavailablePorts.HardwareFacts(),
            UnavailablePorts.MemoryProbe(),
            SupportMatrix.ProvisionalV1(),
            EstimatorPolicy.ProvisionalV1(),
            SafetyPolicy.ProvisionalV1(),
            new HashSet<string>(),
            TrustedSourceAvailability.None(),
            // The llama.cpp default stands in for what the user actually
            // imported until an adapter can report it.
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            TimeProvider.System);

    private static CompatibilityRunDependencies ProductionDependencies(
        CompatibilityProductionInput input)
    {
        int baselineTokens = Math.Min(4096, input.Model.DeclaredContextLimit ?? 4096);
        return CompatibilityRunDependencies.Create(
            new ProductionGateway(input),
            new ProductionModelFactsSource(input),
            new ProductionHardwareFactsSource(input),
            new ProductionMemoryProbe(input),
            SupportMatrix.ProvisionalV1(),
            EstimatorPolicy.ProvisionalV1(),
            SafetyPolicy.ProportionalV2(),
            new HashSet<string>(),
            TrustedSourceAvailability.None(),
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(baselineTokens),
            TimeProvider.System);
    }

    private static CompatibilityScreenModel ProjectProduction(
        CompatibilityRunResult result,
        CompatibilityProductionInput input)
    {
        if (input.Optimization is not { } optimization
            || result.Outcome != CompatibilityRunOutcome.Completed
            || result.Assessment is not { } assessment)
        {
            return ProjectWithoutOptimizationAuthority(result);
        }

        EvaluatedCandidate[] baselines =
        [
            .. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Candidate.IsBaseline
                && candidate.Preparation == CandidatePreparation.None)
        ];
        if (baselines.Length != 1
            || assessment.BaselineFingerprint != baselines[0].Fingerprint)
        {
            return CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [], [], BaselineExclusionReason.None, false, false);
        }

        InspectedModelFacts facts = ToFacts(input.Model);
        ByteCount safeBudget = baselines[0].Fit.SafeBudget;
        ByteCount availableDisk = ByteCount.FromBytes(
            input.FreshResources.AvailableStorageBytes);
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            optimization.Snapshot,
            facts,
            optimization.Workload,
            optimization.Binding,
            safeBudget,
            availableDisk,
            policy,
            optimization.OptedInExperimentalEvidenceIds);

        CompatibilityOptimizationProjectionInput projection =
            CompatibilityOptimizationProjectionInput.Create(
                generated,
                optimization.Snapshot,
                facts,
                optimization.Workload,
                optimization.Binding,
                safeBudget,
                availableDisk,
                policy,
                optimization.OptedInExperimentalEvidenceIds,
                CompatibilityBaselineIdentity.ForLegacyNone(
                    baselines[0], optimization.Binding));
        return CompatibilityScreenModel.From(result, projection);
    }

    private static CompatibilityScreenModel ProjectWithoutOptimizationAuthority(
        CompatibilityRunResult result)
    {
        CompatibilityScreenModel legacy = CompatibilityScreenModel.From(result);
        return legacy.State == CompatibilityScreenState.OptimisationRequired
            ? CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                legacy.Findings,
                legacy.Modes,
                legacy.BaselineExclusionReason,
                useCurrentModelAvailable: false,
                continueEnabled: false,
                setup: legacy.CurrentSetup)
            : legacy;
    }

    private static InspectedModelFacts ToFacts(GgufCompatibilityModelInput model) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(model.FileLengthBytes),
            model.LayerCount,
            model.EmbeddingSize,
            model.AttentionHeadCount,
            model.KeyValueHeadCount,
            model.DeclaredContextLimit,
            model.FileType,
            model.QuantisationVersion);

    private sealed class ProductionGateway(CompatibilityProductionInput input)
        : ICompatibilityInputGateway
    {
        public HandoffClaim Claim() => HandoffClaim.Claimed(
            input.ModelInspectionRunId.ToString("N"),
            input.ProductHardwareRunId.ToString("N"));

        public void Rollback()
        {
        }

        public bool Commit(CompatibilityRunId runId) => !runId.IsEmpty;
    }

    private sealed class ProductionModelFactsSource(CompatibilityProductionInput input)
        : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId)
        {
            if (!string.Equals(
                modelInspectionRunId,
                input.ModelInspectionRunId.ToString("N"),
                StringComparison.Ordinal))
            {
                return ModelFactsResolution.Unavailable(PortUnavailableReason.HandoffUnavailable);
            }

            GgufCompatibilityModelInput model = input.Model;
            return ModelFactsResolution.Established(ToFacts(model));
        }
    }

    private sealed class ProductionHardwareFactsSource(CompatibilityProductionInput input)
        : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId)
        {
            if (!string.Equals(
                productHardwareRunId,
                input.ProductHardwareRunId.ToString("N"),
                StringComparison.Ordinal))
            {
                return HardwareFactsResolution.Unavailable(PortUnavailableReason.HandoffUnavailable);
            }

            CompatibilityHardwareInput hardware = input.Hardware;
            return HardwareFactsResolution.Established(HardwareFacts.Create(
                ByteCount.FromBytes(hardware.InstalledSystemMemoryBytes),
                ByteCount.FromBytes(hardware.InstalledDedicatedDeviceMemoryBytes),
                ByteCount.FromBytes(hardware.FreeStorageBytes),
                hardware.PresentDevices,
                hardware.VerifiedBackends));
        }
    }

    private sealed class ProductionMemoryProbe(CompatibilityProductionInput input)
        : IFreshSystemMemoryProbe
    {
        public FreshMemoryReading Probe()
        {
            CompatibilityFreshResourcesInput fresh = input.FreshResources;
            return FreshMemoryReading.Established(AvailableResources.Create(
                ByteCount.FromBytes(fresh.AvailableSystemMemoryBytes),
                ByteCount.FromBytes(fresh.AvailableDedicatedDeviceMemoryBytes),
                ByteCount.FromBytes(fresh.AvailableStorageBytes),
                fresh.ObservedAtUtc));
        }
    }

    /// <summary>
    /// A run that never started, described the same way as one that started and
    /// broke. There is no assessment and no policy list, because neither was
    /// ever established — reporting policies here would name versions that
    /// decided nothing.
    /// </summary>
    private static CompatibilityRunResult Failure()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return CompatibilityRunResult.Failed(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.UnexpectedFailure, FindingSeverity.Blocking)],
            [],
            now,
            now);
    }
}
