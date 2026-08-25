#if DEBUG
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.DebugFixtures;

/// <summary>One rendered state, named so it can be asked for.</summary>
internal sealed record CompatibilityFixture(
    string Id,
    string Title,
    CompatibilityPresentation Presentation);

/// <summary>
/// Every screen state, renderable without hardware, handoffs or a real model.
///
/// Nine of the ten states are unreachable today: the adapters that would feed
/// the engine do not exist, so a real run always ends at "no answer yet". Without
/// these, nine screens could not be looked at, reviewed, or regression-tested
/// until another team shipped — and a design nobody can see is a design nobody
/// can correct.
///
/// Compiled only in Debug. These are presentation inputs, never run results, so
/// nothing here can be mistaken for a conclusion about a real computer.
/// </summary>
internal static class CompatibilityFixtureCatalogue
{
    internal static IReadOnlyList<CompatibilityFixture> All { get; } =
    [
        new("CMP-001", "Working — step 1 of 4",
            CompatibilityPresentationFactory.Analysing(0)),
        new("CMP-002", "Working — step 2 of 4",
            CompatibilityPresentationFactory.Analysing(1)),
        new("CMP-003", "Working — step 3 of 4",
            CompatibilityPresentationFactory.Analysing(2)),
        new("CMP-004", "Working — step 4 of 4",
            CompatibilityPresentationFactory.Analysing(3)),

        new("CMP-010", "Yes, this should run",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.EstimatedCompatible,
                continueEnabled: true,
                useCurrentModel: true))),

        new("CMP-011", "Yes, but your current setup is not offered",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.EstimatedCompatible,
                continueEnabled: true,
                useCurrentModel: false,
                baseline: BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn))),

        new("CMP-012", "Yes, but we could not check your current setup",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.EstimatedCompatible,
                continueEnabled: true,
                useCurrentModel: false,
                baseline: BaselineExclusionReason.BaselineEntrySupportStateUnknown))),

        new("CMP-020", "Runs, but very little memory spare",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.OptimisationRequired,
                continueEnabled: true,
                useCurrentModel: true))),

        new("CMP-021", "GGUF Q2_K is the only safe choice",
            CompatibilityPresentationFactory.From(Optimisation(
                GgufWeightFormat.Q2K,
                GgufKvCacheFormat.Q8_0,
                OptimizationAssessment.Poor,
                requantisation: true,
                OptimizationQualityNotice.SignificantQualityReduction))),

        new("CMP-022", "GGUF requantisation keeps the original",
            CompatibilityPresentationFactory.From(Optimisation(
                GgufWeightFormat.Q3KM,
                GgufKvCacheFormat.Q8_0,
                OptimizationAssessment.Acceptable,
                requantisation: true,
                OptimizationQualityNotice.NoticeableQualityReduction))),

        new("CMP-023", "OpenVINO INT8 to INT4",
            CompatibilityPresentationFactory.From(OptimisationOpenVino(
                OpenVinoWeightFormat.Int4,
                OpenVinoKvCacheFormat.RouteDefault,
                OptimizationAssessment.Good))),

        new("CMP-024", "OpenVINO cache-only U8 and U4",
            CompatibilityPresentationFactory.From(OptimisationOpenVino(
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U4,
                OptimizationAssessment.Excellent))),

        new("CMP-025", "Experimental TurboQuant TBQ4 opt-in",
            CompatibilityPresentationFactory.From(OptimisationOpenVino(
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.TurboQuantTbq4,
                OptimizationAssessment.Good,
                experimental: true))),

        new("CMP-026", "Experimental TurboQuant TBQ3 opt-in warning",
            CompatibilityPresentationFactory.From(OptimisationOpenVino(
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.TurboQuantTbq3,
                OptimizationAssessment.Acceptable,
                experimental: true,
                OptimizationQualityNotice.SignificantQualityReduction))),

        new("CMP-027", "8 GB constrained-memory example",
            CompatibilityPresentationFactory.From(Optimisation(
                GgufWeightFormat.Q3KM,
                GgufKvCacheFormat.Q8_0,
                OptimizationAssessment.Acceptable,
                requantisation: true,
                OptimizationQualityNotice.SomeQualityReduction,
                safeBudget: 4_294_967_296))),

        new("CMP-028", "16 GB ordinary-memory example",
            CompatibilityPresentationFactory.From(Optimisation(
                GgufWeightFormat.Q4KM,
                GgufKvCacheFormat.Q8_0,
                OptimizationAssessment.Good,
                requantisation: false,
                OptimizationQualityNotice.None,
                safeBudget: 10_737_418_240))),

        new("CMP-030", "Nothing fits safely",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.NoEstimatedSafeConfiguration,
                continueEnabled: false,
                useCurrentModel: false,
                availability: ModeAvailability.Unavailable,
                reason: ModeAdmissionReason.FitStateNotSafeOrNarrow))),

        // The state this feature actually ships in.
        new("CMP-040", "No answer yet — nothing has been handed over",
            CompatibilityPresentationFactory.From(NotEstablished(
                CompatibilityFindingCode.ModelFactsUnavailable,
                CompatibilityFindingCode.HardwareFactsUnavailable,
                CompatibilityFindingCode.FreshMemoryUnavailable))),

        new("CMP-041", "No answer yet — the model's shape could not be read",
            CompatibilityPresentationFactory.From(NotEstablished(
                CompatibilityFindingCode.NoCandidateCouldBeEstimated))),

        new("CMP-042", "No answer yet — memory could not be read",
            CompatibilityPresentationFactory.From(NotEstablished(
                CompatibilityFindingCode.FreshMemoryUnavailable))),

        new("CMP-043", "No answer yet — something broke on our side",
            CompatibilityPresentationFactory.From(
                NotEstablished(CompatibilityFindingCode.UnexpectedFailure))),

        new("CMP-050", "Testing — step 1 of 4",
            CompatibilityPresentationFactory.Verifying(0)),
        new("CMP-051", "Testing — step 4 of 4",
            CompatibilityPresentationFactory.Verifying(3)),

        new("CMP-060", "Tested and it runs",
            CompatibilityPresentationFactory.VerifiedCompatible()),

        new("CMP-070", "Tested and it did not run",
            CompatibilityPresentationFactory.VerificationFailed()),

        new("CMP-080", "Check stopped",
            CompatibilityPresentationFactory.Cancelled())
    ];

    internal static CompatibilityFixture? ById(string id)
    {
        foreach (CompatibilityFixture fixture in All)
        {
            if (string.Equals(fixture.Id, id, System.StringComparison.Ordinal))
            {
                return fixture;
            }
        }

        return null;
    }

    private static CompatibilityScreenModel Concluded(
        CompatibilityScreenState state,
        bool continueEnabled,
        bool useCurrentModel,
        BaselineExclusionReason baseline = BaselineExclusionReason.None,
        ModeAvailability availability = ModeAvailability.Available,
        ModeAdmissionReason reason = ModeAdmissionReason.None) =>
        CompatibilityScreenModel.ForPresentation(
            state,
            [
                new CompatibilityFindingView(
                    CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Warning)
            ],
            [
                Mode(CompatibilityMode.Automatic, availability, reason),
                Mode(CompatibilityMode.Quality, availability, reason),
                Mode(CompatibilityMode.Balanced, availability, reason),
                Mode(CompatibilityMode.Efficiency, availability, reason)
            ],
            baseline,
            useCurrentModel,
            continueEnabled,
            Setup(state),
            state == CompatibilityScreenState.OptimisationRequired
                ? Optimization(
                    GgufWeightFormat.Q3KM,
                    GgufKvCacheFormat.Q8_0,
                    OptimizationAssessment.Good,
                    requantisation: true,
                    OptimizationQualityNotice.SomeQualityReduction)
                : null);

    private static CompatibilityScreenModel Optimisation(
        GgufWeightFormat weights,
        GgufKvCacheFormat cache,
        OptimizationAssessment quality,
        bool requantisation,
        OptimizationQualityNotice notice,
        ulong safeBudget = 6_442_450_944)
    {
        CompatibilityOptimizationModeView[] modes =
        [
            OptimizationMode(CompatibilityOptimizationLabelCode.Automatic, null,
                weights, cache, quality, requantisation, notice, safeBudget),
            OptimizationMode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10,
                weights, cache, quality, requantisation, notice, safeBudget),
            OptimizationMode(CompatibilityOptimizationLabelCode.Efficient, 30,
                weights, cache, quality, requantisation, notice, safeBudget),
            OptimizationMode(CompatibilityOptimizationLabelCode.Balanced, 50,
                weights, cache, quality, requantisation, notice, safeBudget),
            OptimizationMode(CompatibilityOptimizationLabelCode.HighCapability, 70,
                weights, cache, quality, requantisation, notice, safeBudget),
            OptimizationMode(CompatibilityOptimizationLabelCode.MaximumCapability, 90,
                weights, cache, quality, requantisation, notice, safeBudget)
        ];

        return OptimizationScreen(modes, safeBudget);
    }

    private static CompatibilityScreenModel OptimisationOpenVino(
        OpenVinoWeightFormat weights,
        OpenVinoKvCacheFormat cache,
        OptimizationAssessment quality,
        bool experimental = false,
        OptimizationQualityNotice notice = OptimizationQualityNotice.None)
    {
        CompatibilityOptimizationModeView[] modes =
        [
            OpenVinoMode(CompatibilityOptimizationLabelCode.Automatic, null),
            OpenVinoMode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10),
            OpenVinoMode(CompatibilityOptimizationLabelCode.Efficient, 30),
            OpenVinoMode(CompatibilityOptimizationLabelCode.Balanced, 50),
            OpenVinoMode(CompatibilityOptimizationLabelCode.HighCapability, 70),
            OpenVinoMode(CompatibilityOptimizationLabelCode.MaximumCapability, 90)
        ];

        return OptimizationScreen(modes, 8_589_934_592, openVino: true);

        CompatibilityOptimizationModeView OpenVinoMode(
            CompatibilityOptimizationLabelCode label, int? slider) =>
            CompatibilityOptimizationModeView.ForPresentation(
                label, slider, OptimizationRoute.OpenVino,
                null, null, weights, cache, DeviceRouteId.Cpu, quality,
                contextTokens: 4096,
                predictedPeakBytes: 4_294_967_296,
                safeBudgetBytes: 8_589_934_592,
                headroomBytes: 4_294_967_296,
                requiresPersistentArtifact: weights != OpenVinoWeightFormat.Original,
                requiresRequantisationAcknowledgement: false,
                qualityNotice: notice,
                isExperimental: experimental,
                sharedWithAdjacentBand: true);
    }

    private static CompatibilityScreenModel OptimizationScreen(
        IReadOnlyList<CompatibilityOptimizationModeView> modes,
        ulong safeBudget,
        bool openVino = false)
    {
        CompatibilityOptimizationView optimization =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                modes,
                modes[0].RequiresPersistentArtifact,
                modes[0].RequiresRequantisationAcknowledgement,
                modes[0].QualityNotice);

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.OptimisationRequired,
            [new CompatibilityFindingView(
                CompatibilityFindingCode.UncalibratedEstimate,
                FindingSeverity.Warning)],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true,
            Setup(
                CompatibilityFitState.DoesNotFit,
                4096,
                openVino ? WeightQuantisation.Q8_0 : WeightQuantisation.Q4_K_M,
                weights: 4_831_838_208,
                kvCache: 805_306_368,
                compute: 536_870_912,
                safeBudget: safeBudget),
            optimization);
    }

    private static CompatibilityOptimizationView Optimization(
        GgufWeightFormat weights,
        GgufKvCacheFormat cache,
        OptimizationAssessment quality,
        bool requantisation,
        OptimizationQualityNotice notice)
    {
        CompatibilityOptimizationModeView[] modes =
        [
            OptimizationMode(CompatibilityOptimizationLabelCode.Automatic, null,
                weights, cache, quality, requantisation, notice),
            OptimizationMode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10,
                GgufWeightFormat.Q2K, cache, OptimizationAssessment.Poor, true,
                OptimizationQualityNotice.SignificantQualityReduction),
            OptimizationMode(CompatibilityOptimizationLabelCode.Efficient, 30,
                GgufWeightFormat.Q3KM, cache, OptimizationAssessment.Acceptable, true,
                OptimizationQualityNotice.NoticeableQualityReduction),
            OptimizationMode(CompatibilityOptimizationLabelCode.Balanced, 50,
                GgufWeightFormat.Q3KM, cache, OptimizationAssessment.Good, true,
                OptimizationQualityNotice.SomeQualityReduction),
            OptimizationMode(CompatibilityOptimizationLabelCode.HighCapability, 70,
                GgufWeightFormat.Q4KM, cache, OptimizationAssessment.Good, false,
                OptimizationQualityNotice.None),
            OptimizationMode(CompatibilityOptimizationLabelCode.MaximumCapability, 90,
                GgufWeightFormat.Q5KM, cache, OptimizationAssessment.Excellent, false,
                OptimizationQualityNotice.None)
        ];

        return CompatibilityOptimizationView.ForPresentation(
            CompatibilityOptimizationLabelCode.Automatic,
            null,
            modes,
            modes[0].RequiresPersistentArtifact,
            modes[0].RequiresRequantisationAcknowledgement,
            modes[0].QualityNotice);
    }

    private static CompatibilityOptimizationModeView OptimizationMode(
        CompatibilityOptimizationLabelCode label,
        int? slider,
        GgufWeightFormat weights,
        GgufKvCacheFormat cache,
        OptimizationAssessment quality,
        bool requantisation,
        OptimizationQualityNotice notice,
        ulong safeBudget = 6_442_450_944) =>
        CompatibilityOptimizationModeView.ForPresentation(
            label, slider, OptimizationRoute.Gguf,
            weights, cache, null, null, DeviceRouteId.Cpu, quality,
            contextTokens: 4096,
            predictedPeakBytes: 3_221_225_472,
            safeBudgetBytes: safeBudget,
            headroomBytes: safeBudget - 3_221_225_472,
            requiresPersistentArtifact: requantisation,
            requiresRequantisationAcknowledgement: requantisation,
            qualityNotice: notice,
            isExperimental: cache == GgufKvCacheFormat.TurboQuant3Bit,
            sharedWithAdjacentBand: true);

    /// <summary>
    /// Figures for a screen to draw, shaped like a mid-sized model on a laptop
    /// with integrated graphics.
    ///
    /// The three cases differ only in how much context is asked for and how the
    /// weights are stored, which is the honest picture: the same model on the
    /// same machine moves between comfortable, marginal and impossible on those
    /// two choices alone. Components sum exactly to the requirement so the bar
    /// a reviewer sees is arithmetic, not a sketch.
    /// </summary>
    private static CompatibilitySetupView Setup(CompatibilityScreenState state) => state switch
    {
        CompatibilityScreenState.OptimisationRequired => Setup(
            CompatibilityFitState.DoesNotFit,
            contextTokens: 32768,
            WeightQuantisation.Q4_K_M,
            weights: 4_697_620_480,
            kvCache: 4_294_967_296,
            compute: 536_870_912),

        CompatibilityScreenState.NoEstimatedSafeConfiguration => Setup(
            CompatibilityFitState.DoesNotFit,
            contextTokens: 32768,
            WeightQuantisation.Q8_0,
            weights: 8_589_934_592,
            kvCache: 4_294_967_296,
            compute: 536_870_912),

        _ => Setup(
            CompatibilityFitState.Safe,
            contextTokens: 4096,
            WeightQuantisation.Q4_K_M,
            weights: 4_697_620_480,
            kvCache: 536_870_912,
            compute: 268_435_456)
    };

    private static CompatibilitySetupView Setup(
        CompatibilityFitState fit,
        int contextTokens,
        WeightQuantisation quantisation,
        ulong weights,
        ulong kvCache,
        ulong compute,
        ulong safeBudget = 9_663_676_416)
    {
        const ulong Backend = 67_108_864;
        const ulong Application = 33_554_432;

        ulong parts = weights + kvCache + compute + Backend + Application;

        // A tenth on top, standing in for the margin an uncalibrated estimator
        // demands. Present so the bar a reviewer sees has the same shape as a
        // real one, where the allowance is a visible slice rather than a
        // rounding difference nobody can account for.
        ulong allowance = parts / 10;
        ulong required = parts + allowance;

        return CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.IntelSycl,
            DeviceRouteId.IntelIntegratedGpu,
            quantisation,
            contextTokens,
            fit,
            required,
            safeBudget,
            required < safeBudget ? safeBudget - required : 0,
            allowance,
            isExperimental: false,
            requiresConversion: false,
            [
                new CompatibilityComponentView(ResourceComponentKind.Weights, weights),
                new CompatibilityComponentView(ResourceComponentKind.KvCache, kvCache),
                new CompatibilityComponentView(ResourceComponentKind.ComputeBuffer, compute),
                new CompatibilityComponentView(ResourceComponentKind.BackendAllocation, Backend),
                new CompatibilityComponentView(
                    ResourceComponentKind.ApplicationOverhead, Application)
            ]);
    }

    private static CompatibilityModeView Mode(
        CompatibilityMode mode,
        ModeAvailability availability,
        ModeAdmissionReason reason) =>
        new(mode, availability, reason, availability == ModeAvailability.Available);

    private static CompatibilityScreenModel NotEstablished(
        params CompatibilityFindingCode[] codes)
    {
        List<CompatibilityFindingView> findings = [];

        foreach (CompatibilityFindingCode code in codes)
        {
            findings.Add(new CompatibilityFindingView(code, FindingSeverity.Blocking));
        }

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NotEstablished,
            findings,
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false);
    }
}
#endif
