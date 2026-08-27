using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

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
    // Matches the approved Hardware Inspection dynamic-memory evidence policy.
    internal static readonly TimeSpan FreshResourceMaximumAge =
        OptimizationFreshnessPolicy.MaximumAge;
    internal static readonly TimeSpan FreshResourceFutureClockSkew =
        OptimizationFreshnessPolicy.FutureClockSkew;
    internal const string FreshResourcePolicyVersion =
        OptimizationFreshnessPolicy.Version;

    /// <summary>Runs C1 with validated production values supplied by owner adapters.</summary>
    public static CompatibilityScreenModel Run(
        CompatibilityProductionInput input,
        CancellationToken cancellationToken = default) =>
        EvaluateProduction(input, TimeProvider.System, cancellationToken).Screen;

    /// <summary>Runs C1 against an explicit execution-time clock.</summary>
    public static CompatibilityScreenModel Run(
        CompatibilityProductionInput input,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default) =>
        EvaluateProduction(input, timeProvider, cancellationToken).Screen;

    /// <summary>
    /// Evaluates compatibility and retains the immutable planning authority for
    /// the next screen when, and only when, the decision is actionable.
    /// </summary>
    public static CompatibilityEvaluation EvaluateProduction(
        CompatibilityProductionInput input,
        CancellationToken cancellationToken = default) =>
        EvaluateProduction(input, TimeProvider.System, cancellationToken);

    /// <summary>
    /// Rebuilds the exact issuance authority used by production candidate
    /// generation. Keeping this calculation beside the engine prevents a UI
    /// integration from copying or drifting the proportional memory policy.
    /// </summary>
    public static OptimizationIssuanceAuthority CreateOptimizationIssuanceAuthority(
        CompatibilityProductionInput input,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Optimization is null
            || input.JourneyAuthority?.HardwareFactsSha256 is not { } hardwareFacts
            || evaluatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Issuance requires complete production optimization authority and a UTC evaluation time.",
                nameof(input));
        }

        SafetyPolicy safety = SafetyPolicy.ProportionalV2();
        ByteCount available = ByteCount.FromBytes(
            input.FreshResources.AvailableSystemMemoryBytes);
        _ = available.TrySubtract(
            safety.AvailableMemoryReserveFor(available),
            out ByteCount fitBudget);
        ByteCount generationBudget = GenerationBudget(fitBudget, safety);
        ulong? dedicatedBudget = null;
        if (input.FreshResources.DedicatedDeviceMemoryEstablished)
        {
            dedicatedBudget = GenerationBudget(
                ByteCount.FromBytes(
                    input.FreshResources.AvailableDedicatedDeviceMemoryBytes),
                safety).Bytes;
        }

        return OptimizationIssuanceAuthority.CreateCurrent(
            hardwareFacts,
            input.Hardware.PresentDevices,
            input.Hardware.VerifiedBackends,
            generationBudget.Bytes,
            dedicatedBudget,
            input.FreshResources.AvailableStorageBytes,
            input.FreshResources.ObservedAtUtc,
            evaluatedAtUtc);
    }

    /// <summary>Evaluates against an explicit execution-time clock.</summary>
    public static CompatibilityEvaluation EvaluateProduction(
        CompatibilityProductionInput input,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset evaluatedAtUtc = timeProvider.GetUtcNow();
        if (!FreshResourcesAreCurrent(input.FreshResources, evaluatedAtUtc))
        {
            return WithoutPlanning(CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished, [], [],
                BaselineExclusionReason.None, useCurrentModelAvailable: false,
                continueEnabled: false));
        }

        try
        {
            CompatibilityRunResult result = input.CurrentModel.Route == OptimizationRoute.Gguf
                ? CompatibilityRunCoordinator.Execute(
                    new CompatibilityRunRequest(
                        CompatibilityContextRequest.ApplicationDefault()),
                    ProductionDependencies(input, timeProvider),
                    cancellationToken)
                : EvaluateOpenVinoBaseline(input, timeProvider, cancellationToken);
            CompatibilityEvaluation evaluation =
                ProjectProduction(result, input, evaluatedAtUtc);
            return evaluation.Screen.State is
                CompatibilityScreenState.EstimatedCompatible or
                CompatibilityScreenState.OptimisationRequired or
                CompatibilityScreenState.NoEstimatedSafeConfiguration
                    ? evaluation with
                    {
                        MachineMemory = MachineMemory(input)
                    }
                    : evaluation;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return WithoutPlanning(CompatibilityScreenModel.From(Failure()));
        }
    }

    private static bool FreshResourcesAreCurrent(
        CompatibilityFreshResourcesInput resources,
        DateTimeOffset evaluatedAtUtc) =>
        resources.ObservedAtUtc >= evaluatedAtUtc - FreshResourceMaximumAge
        && resources.ObservedAtUtc <= evaluatedAtUtc + FreshResourceFutureClockSkew;

    private static CompatibilityMachineMemory MachineMemory(
        CompatibilityProductionInput input)
    {
        SafetyPolicy safety = SafetyPolicy.ProportionalV2();
        ByteCount available = ByteCount.FromBytes(
            input.FreshResources.AvailableSystemMemoryBytes);
        ByteCount reserve = safety.AvailableMemoryReserveFor(available);
        _ = available.TrySubtract(reserve, out ByteCount safeBudget);
        return CompatibilityMachineMemory.Create(
            input.Hardware.InstalledSystemMemoryBytes,
            available.Bytes,
            reserve.Bytes,
            safeBudget.Bytes);
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
        CompatibilityProductionInput input,
        TimeProvider timeProvider)
    {
        int? declaredContext = input.CurrentModel.Gguf?.DeclaredContextLimit
            ?? input.CurrentModel.OpenVino?.DeclaredContextLimit;
        int baselineTokens = Math.Min(4096, declaredContext ?? 4096);
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
            input.CurrentModel.GgufConfiguration!,
            ContextTokenCount.FromTokens(baselineTokens),
            timeProvider);
    }

    private static CompatibilityRunResult EvaluateOpenVinoBaseline(
        CompatibilityProductionInput input,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = timeProvider.GetUtcNow();
        if (cancellationToken.IsCancellationRequested)
        {
            return CompatibilityRunResult.Cancelled(
                CompatibilityRunId.New(), [], [], started, timeProvider.GetUtcNow());
        }

        OpenVinoRouteConfiguration configuration =
            input.CurrentModel.OpenVinoConfiguration!;
        int declaredContext = input.CurrentModel.OpenVino!.DeclaredContextLimit ?? 4096;
        ContextTokenCount context = ContextTokenCount.FromTokens(
            Math.Min(4096, declaredContext));
        InspectedModelFacts facts = ToFacts(input.CurrentModel);
        EstimatorPolicy estimator = EstimatorPolicy.ProvisionalV1();
        SafetyPolicy safety = SafetyPolicy.ProportionalV2();
        ResourceEstimate estimate = OpenVinoResourceEstimator.Estimate(
            facts, configuration, context, estimator);
        if (estimate.Status != EstimationStatus.Established)
        {
            return CompatibilityRunResult.NotEstablished(
                CompatibilityRunId.New(),
                [CompatibilityFinding.Create(
                    CompatibilityFindingCode.NoCandidateCouldBeEstimated,
                    FindingSeverity.Blocking)],
                [PolicyIdentity.Create(
                    "estimator", estimator.PolicyVersion, estimator.Provenance)],
                started,
                timeProvider.GetUtcNow());
        }

        ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);
        CompatibilityFreshResourcesInput fresh = input.FreshResources;
        AvailableResources available = AvailableResources.Create(
            ByteCount.FromBytes(fresh.AvailableSystemMemoryBytes),
            ByteCount.FromBytes(fresh.AvailableDedicatedDeviceMemoryBytes),
            ByteCount.FromBytes(fresh.AvailableStorageBytes),
            fresh.ObservedAtUtc,
            dedicatedDeviceMemoryEstablished:
                fresh.DedicatedDeviceMemoryEstablished);
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration, context, CandidatePreparation.None,
            "openvino-current", isExperimental: false, isBaseline: true);
        EvaluatedCandidate evaluated = EvaluatedCandidate.Create(
            candidate,
            estimate,
            peaks,
            FitPolicy.Assess(peaks, available, safety),
            EffectiveOpenVinoEncoding(
                configuration.Weights,
                input.CurrentModel.OpenVinoSourcePrecision!.Value),
            EvidenceGrade.Estimated,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
        CompatibilityModeSelection[] modes =
        [
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic),
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Quality),
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Balanced),
            CompatibilityModeSelection.NotEstablished(CompatibilityMode.Efficiency)
        ];
        CompatibilityAssessment assessment = CompatibilityAssessment.Create(
            [evaluated], modes, evaluated.Fingerprint,
            BaselineExclusionReason.None,
            evaluated.Fit.State is CompatibilityFitState.Safe
                or CompatibilityFitState.Narrow);
        return CompatibilityRunResult.Completed(
            CompatibilityRunId.New(), assessment, [],
            [
                PolicyIdentity.Create(
                    "estimator", estimator.PolicyVersion, estimator.Provenance),
                PolicyIdentity.Create(
                    "safety", safety.PolicyVersion, safety.Provenance)
            ],
            started,
            timeProvider.GetUtcNow());
    }

    private static WeightQuantisation EffectiveOpenVinoEncoding(
        OpenVinoWeightFormat weights,
        OpenVinoWeightPrecision sourcePrecision) => weights switch
        {
            OpenVinoWeightFormat.Fp16 => WeightQuantisation.F16,
            OpenVinoWeightFormat.Int8 => WeightQuantisation.Q8_0,
            OpenVinoWeightFormat.Int4 => WeightQuantisation.Q4_K_M,
            OpenVinoWeightFormat.Original => sourcePrecision switch
            {
                OpenVinoWeightPrecision.Fp16 => WeightQuantisation.F16,
                OpenVinoWeightPrecision.EightBit => WeightQuantisation.Q8_0,
                OpenVinoWeightPrecision.FourBit => WeightQuantisation.Q4_K_M,
                _ => WeightQuantisation.Unknown
            },
            _ => WeightQuantisation.Unknown
        };

    private static CompatibilityEvaluation ProjectProduction(
        CompatibilityRunResult result,
        CompatibilityProductionInput input,
        DateTimeOffset evaluatedAtUtc)
    {
        if (input.Optimization is not { } optimization
            || result.Outcome != CompatibilityRunOutcome.Completed
            || result.Assessment is not { } assessment)
        {
            return WithoutPlanning(ProjectWithoutOptimizationAuthority(result));
        }

        EvaluatedCandidate[] baselines =
        [
            .. assessment.EvaluatedCandidates.Where(candidate =>
                candidate.Candidate.IsBaseline
                && candidate.Preparation == CandidatePreparation.None)
        ];
        if (baselines.Length != 1
            || assessment.BaselineFingerprint != baselines[0].Fingerprint
            || !CurrentModelMatchesCapability(
                input.CurrentModel, input.Hardware, optimization.Snapshot,
                baselines[0].Context))
        {
            return WithoutPlanning(CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [], [], BaselineExclusionReason.None, false, false));
        }

        InspectedModelFacts facts = ToFacts(input.CurrentModel);
        ByteCount safeBudget = GenerationBudget(
            baselines[0].Fit.SafeBudget, SafetyPolicy.ProportionalV2());
        ByteCount availableDisk = ByteCount.FromBytes(
            input.FreshResources.AvailableStorageBytes);
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();
        OptimizationHardwareAuthority hardwareAuthority =
            OptimizationHardwareAuthority.Create(
                input.JourneyAuthority!.HardwareFactsSha256!,
                input.Hardware.PresentDevices,
                input.Hardware.VerifiedBackends,
                input.FreshResources.DedicatedDeviceMemoryEstablished
                    ? GenerationBudget(
                        ByteCount.FromBytes(
                            input.FreshResources.AvailableDedicatedDeviceMemoryBytes),
                        SafetyPolicy.ProportionalV2())
                    : null,
                input.FreshResources.ObservedAtUtc,
                evaluatedAtUtc,
                FreshResourcePolicyVersion);
        CrossRouteGenerationResult generated = CrossRouteCandidateGenerator.Generate(
            optimization.Snapshot,
            facts,
            optimization.Workload,
            optimization.Binding,
            safeBudget,
            availableDisk,
            policy,
            optimization.OptedInExperimentalEvidenceIds,
            hardwareAuthority);

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
                hardwareAuthority,
                CompatibilityBaselineIdentity.ForLegacyNone(
                    baselines[0], optimization.Binding));
        CompatibilityScreenModel screen = CompatibilityScreenModel.From(
            result, projection);
        IReadOnlyList<OptimizationCandidate> retained =
            CompatibilityScreenModel.RetainPlanningCandidates(screen, projection);
        CompatibilityPlanningSession? planningSession = facts.LayerCount is { } layers
            ? CompatibilityPlanningSession.Create(
                input.CurrentModel.Route,
                retained,
                optimization.Snapshot,
                optimization.Workload,
                optimization.Binding,
                layers,
                optimization.OptedInExperimentalEvidenceIds)
            : null;

        IReadOnlyList<CompatibilityExperimentalConsentOption> consentOptions =
            CompatibilityScreenModel.RetainExperimentalConsentOptions(projection);
        CompatibilityOptimizationView? optionalOptimization =
            CompatibilityScreenModel.RetainOptionalOptimization(
                screen,
                projection);

        return new CompatibilityEvaluation(
            screen,
            planningSession,
            CurrentConfiguration: null)
        {
            ExperimentalConsentOptions = consentOptions,
            OptionalOptimization = optionalOptimization
        };
    }

    private static CompatibilityEvaluation WithoutPlanning(
        CompatibilityScreenModel screen) => new(screen, null, null);

    private static ByteCount GenerationBudget(
        ByteCount fitBudget,
        SafetyPolicy safety)
    {
        _ = fitBudget.TrySubtract(
            safety.CalibrationMarginFloor, out ByteCount afterFloor);
        ulong afterFraction = (ulong)Math.Floor(
            fitBudget.Bytes / (1m + safety.CalibrationMarginFraction));
        return afterFloor.Bytes < afterFraction
            ? afterFloor
            : ByteCount.FromBytes(afterFraction);
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

    private static InspectedModelFacts ToFacts(CompatibilityCurrentModelInput model)
    {
        if (model.Gguf is { } gguf)
        {
            return InspectedModelFacts.Create(
                ByteCount.FromBytes(gguf.FileLengthBytes), gguf.LayerCount,
                gguf.EmbeddingSize, gguf.AttentionHeadCount, gguf.KeyValueHeadCount,
                gguf.DeclaredContextLimit, gguf.FileType, gguf.QuantisationVersion);
        }

        OpenVinoCompatibilityModelInput openVino = model.OpenVino!;
        return InspectedModelFacts.Create(
            ByteCount.FromBytes(openVino.PackageLengthBytes), openVino.LayerCount,
            openVino.EmbeddingSize, openVino.AttentionHeadCount,
            openVino.KeyValueHeadCount, openVino.DeclaredContextLimit,
            fileType: null, quantisationVersion: null);
    }

    private static bool CurrentModelMatchesCapability(
        CompatibilityCurrentModelInput current,
        CompatibilityHardwareInput hardware,
        OptimizationCapabilitySnapshot snapshot,
        ContextTokenCount context)
    {
        if (snapshot.Route != current.Route)
        {
            return false;
        }

        if (current.Route == OptimizationRoute.Gguf
            && current.GgufConfiguration is { } gguf
            && snapshot.Gguf is { RuntimeAuthority: { } runtime } payload)
        {
            bool validHardwarePair =
                gguf.Backend == CompatibilityBackend.Cpu
                    && gguf.Device == DeviceRouteId.Cpu
                || gguf.Backend is CompatibilityBackend.IntelSycl
                    or CompatibilityBackend.IntelVulkan
                    && gguf.Device is DeviceRouteId.IntelIntegratedGpu
                    or DeviceRouteId.IntelDiscreteGpu;
            if (!validHardwarePair
                || !hardware.PresentDevices.Contains(gguf.Device)
                || !hardware.VerifiedBackends.Contains(gguf.Backend))
            {
                return false;
            }

            GgufAdmittedConfiguration[] matching =
            [
                .. payload.Admitted.Where(admission =>
                    admission.Weights == gguf.Weights
                    && admission.KvCache == gguf.KvCache
                    && admission.Backend == gguf.Backend
                    && admission.Device == gguf.Device
                    && admission.Offload == gguf.Offload
                    && context.Tokens >= admission.MinimumContextTokens
                    && context.Tokens <= admission.MaximumContextTokens
                    && runtime.Profiles.ContainsKey(admission.EvidenceId))
            ];
            return matching.Length == 1;
        }

        if (current.Route == OptimizationRoute.OpenVino
            && current.OpenVinoConfiguration is { } openVino
            && current.OpenVinoSourcePrecision is { } sourcePrecision
            && snapshot.OpenVino is { } openVinoPayload)
        {
            CompatibilityBackend requiredBackend = openVino.Device switch
            {
                DeviceRouteId.Cpu => CompatibilityBackend.OpenVinoCpu,
                DeviceRouteId.IntelIntegratedGpu
                    or DeviceRouteId.IntelDiscreteGpu =>
                    CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelNpu => CompatibilityBackend.OpenVinoNpu,
                _ => CompatibilityBackend.Unspecified
            };
            if (!hardware.PresentDevices.Contains(openVino.Device)
                || requiredBackend == CompatibilityBackend.Unspecified
                || !hardware.VerifiedBackends.Contains(requiredBackend))
            {
                return false;
            }

            OpenVinoAdmittedConfiguration[] matching =
            [
                .. openVinoPayload.Admitted.Where(admission =>
                    admission.Weights == openVino.Weights
                    && admission.KvCache == openVino.KvCache
                    && admission.Device == openVino.Device
                    && admission.PerformanceHint == openVino.PerformanceHint
                    && admission.CompiledCache == openVino.CompiledCache
                    && admission.Streams == openVino.Streams
                    && context.Tokens >= admission.MinimumContextTokens
                    && context.Tokens <= admission.MaximumContextTokens
                    && openVinoPayload.ExecutionAuthorities.TryGetValue(
                        admission.EvidenceId, out OpenVinoExecutionAuthority? authority)
                    && authority.SourceWeightPrecision == sourcePrecision)
            ];
            return matching.Length == 1;
        }

        return false;
    }

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

            return ModelFactsResolution.Established(ToFacts(input.CurrentModel));
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
                fresh.ObservedAtUtc,
                dedicatedDeviceMemoryEstablished:
                    fresh.DedicatedDeviceMemoryEstablished));
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
