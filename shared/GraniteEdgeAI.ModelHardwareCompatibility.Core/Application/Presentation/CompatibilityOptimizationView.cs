using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>Localization-owned label identity; this layer carries no wording.</summary>
public enum CompatibilityOptimizationLabelCode
{
    Unspecified = 0,
    Automatic,
    MaximumEfficiency,
    Efficient,
    Balanced,
    HighCapability,
    MaximumCapability
}

/// <summary>A coarse, evidence-honest warning; never a numeric quality claim.</summary>
public enum OptimizationQualityNotice
{
    None = 0,
    SomeQualityReduction,
    NoticeableQualityReduction,
    SignificantQualityReduction
}

/// <summary>One fully resolved choice, flattened for rendering only.</summary>
public sealed record CompatibilityOptimizationModeView
{
    internal CompatibilityOptimizationModeView(
        CompatibilityOptimizationLabelCode labelCode,
        int? sliderValue,
        OptimizationRoute route,
        GgufWeightFormat? ggufWeights,
        GgufKvCacheFormat? ggufKvCache,
        OpenVinoWeightFormat? openVinoWeights,
        OpenVinoKvCacheFormat? openVinoKvCache,
        DeviceRouteId device,
        OptimizationAssessment expectedQuality,
        int contextTokens,
        ulong predictedPeakBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        bool requiresPersistentArtifact,
        bool requiresRequantisationAcknowledgement,
        OptimizationQualityNotice qualityNotice,
        bool isExperimental,
        bool sharedWithAdjacentBand,
        ulong? dedicatedRequiredBytes = null,
        ulong? dedicatedSafeBudgetBytes = null,
        ulong? dedicatedHeadroomBytes = null)
    {
        LabelCode = labelCode;
        SliderValue = sliderValue;
        Route = route;
        GgufWeights = ggufWeights;
        GgufKvCache = ggufKvCache;
        OpenVinoWeights = openVinoWeights;
        OpenVinoKvCache = openVinoKvCache;
        Device = device;
        ExpectedQuality = expectedQuality;
        ContextTokens = contextTokens;
        PredictedPeakBytes = predictedPeakBytes;
        SafeBudgetBytes = safeBudgetBytes;
        HeadroomBytes = headroomBytes;
        RequiresPersistentArtifact = requiresPersistentArtifact;
        RequiresRequantisationAcknowledgement =
            requiresRequantisationAcknowledgement;
        QualityNotice = qualityNotice;
        IsExperimental = isExperimental;
        SharedWithAdjacentBand = sharedWithAdjacentBand;
        DedicatedRequiredBytes = dedicatedRequiredBytes;
        DedicatedSafeBudgetBytes = dedicatedSafeBudgetBytes;
        DedicatedHeadroomBytes = dedicatedHeadroomBytes;
    }

    public CompatibilityOptimizationLabelCode LabelCode { get; }
    public int? SliderValue { get; }
    public OptimizationRoute Route { get; }
    public GgufWeightFormat? GgufWeights { get; }
    public GgufKvCacheFormat? GgufKvCache { get; }
    public OpenVinoWeightFormat? OpenVinoWeights { get; }
    public OpenVinoKvCacheFormat? OpenVinoKvCache { get; }
    public DeviceRouteId Device { get; }
    public OptimizationAssessment ExpectedQuality { get; }
    public int ContextTokens { get; }
    /// <summary>
    /// Legacy name for peak system/shared-memory demand. Dedicated device
    /// memory is exposed separately and is never included here.
    /// </summary>
    public ulong PredictedPeakBytes { get; }
    public ulong SystemSharedPredictedPeakBytes => PredictedPeakBytes;
    /// <summary>Safe system/shared-memory budget after reserve.</summary>
    public ulong SafeBudgetBytes { get; }
    public ulong SystemSharedSafeBudgetBytes => SafeBudgetBytes;
    /// <summary>Remaining system/shared-memory headroom.</summary>
    public ulong HeadroomBytes { get; }
    public ulong SystemSharedHeadroomBytes => HeadroomBytes;
    /// <summary>
    /// Separate dedicated-device-memory admission axis, or null for a setup
    /// that does not consume dedicated device memory.
    /// </summary>
    public ulong? DedicatedRequiredBytes { get; }
    public ulong? DedicatedSafeBudgetBytes { get; }
    public ulong? DedicatedHeadroomBytes { get; }
    public bool RequiresPersistentArtifact { get; }
    public bool RequiresRequantisationAcknowledgement { get; }
    public OptimizationQualityNotice QualityNotice { get; }
    public bool IsExperimental { get; }
    public bool SharedWithAdjacentBand { get; }

    /// <summary>Creates presentation-only fixture data; never an execution plan.</summary>
    public static CompatibilityOptimizationModeView ForPresentation(
        CompatibilityOptimizationLabelCode labelCode,
        int? sliderValue,
        OptimizationRoute route,
        GgufWeightFormat? ggufWeights,
        GgufKvCacheFormat? ggufKvCache,
        OpenVinoWeightFormat? openVinoWeights,
        OpenVinoKvCacheFormat? openVinoKvCache,
        DeviceRouteId device,
        OptimizationAssessment expectedQuality,
        int contextTokens,
        ulong predictedPeakBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        bool requiresPersistentArtifact,
        bool requiresRequantisationAcknowledgement,
        OptimizationQualityNotice qualityNotice,
        bool isExperimental,
        bool sharedWithAdjacentBand,
        ulong? dedicatedRequiredBytes = null,
        ulong? dedicatedSafeBudgetBytes = null,
        ulong? dedicatedHeadroomBytes = null)
    {
        if (labelCode == CompatibilityOptimizationLabelCode.Unspecified
            || !Enum.IsDefined(labelCode)
            || !Enum.IsDefined(route)
            || device == DeviceRouteId.Unspecified
            || !Enum.IsDefined(device)
            || expectedQuality == OptimizationAssessment.Unknown
            || !Enum.IsDefined(expectedQuality)
            || !Enum.IsDefined(qualityNotice)
            || contextTokens < 1
            || predictedPeakBytes == 0
            || safeBudgetBytes == 0
            || predictedPeakBytes > safeBudgetBytes
            || headroomBytes != safeBudgetBytes - predictedPeakBytes
            || requiresRequantisationAcknowledgement
                && !requiresPersistentArtifact
            || !ValidDedicatedAxis(
                dedicatedRequiredBytes, dedicatedSafeBudgetBytes,
                dedicatedHeadroomBytes))
        {
            throw new ArgumentException(
                "An optimization fixture must describe one complete, safe mode.");
        }

        bool automatic = labelCode == CompatibilityOptimizationLabelCode.Automatic;
        if (automatic != (sliderValue is null)
            || sliderValue is < 0 or > 100)
        {
            throw new ArgumentException(
                "Only Automatic omits a slider value; manual modes use 0 through 100.",
                nameof(sliderValue));
        }

        bool gguf = route == OptimizationRoute.Gguf;
        if (gguf != (ggufWeights is not null && ggufKvCache is not null)
            || gguf == (openVinoWeights is not null || openVinoKvCache is not null)
            || gguf && (!Enum.IsDefined(ggufWeights!.Value)
                || !Enum.IsDefined(ggufKvCache!.Value))
            || !gguf && (!Enum.IsDefined(openVinoWeights!.Value)
                || !Enum.IsDefined(openVinoKvCache!.Value)))
        {
            throw new ArgumentException(
                "Exactly the selected route's closed configuration fields are required.",
                nameof(route));
        }

        return new CompatibilityOptimizationModeView(
            labelCode, sliderValue, route, ggufWeights, ggufKvCache,
            openVinoWeights, openVinoKvCache, device, expectedQuality,
            contextTokens, predictedPeakBytes, safeBudgetBytes, headroomBytes,
            requiresPersistentArtifact, requiresRequantisationAcknowledgement,
            qualityNotice, isExperimental, sharedWithAdjacentBand,
            dedicatedRequiredBytes, dedicatedSafeBudgetBytes,
            dedicatedHeadroomBytes);
    }

    private static bool ValidDedicatedAxis(
        ulong? required, ulong? budget, ulong? headroom)
    {
        bool any = required.HasValue || budget.HasValue || headroom.HasValue;
        return !any || required is > 0 && budget is > 0
            && required <= budget && headroom == budget - required;
    }
}

/// <summary>
/// Complete immutable presentation data for the optimisation choice. It contains
/// no candidate, provider payload, path, evidence identifier, or authority text.
/// </summary>
public sealed record CompatibilityOptimizationView
{
    internal CompatibilityOptimizationView(
        CompatibilityOptimizationLabelCode recommendedLabelCode,
        int? recommendedSliderValue,
        IReadOnlyList<CompatibilityOptimizationModeView> modes,
        bool requiresPersistentArtifact,
        bool requiresRequantisationAcknowledgement,
        OptimizationQualityNotice qualityNotice)
    {
        RecommendedLabelCode = recommendedLabelCode;
        RecommendedSliderValue = recommendedSliderValue;
        Modes = Array.AsReadOnly([.. modes]);
        RequiresPersistentArtifact = requiresPersistentArtifact;
        RequiresRequantisationAcknowledgement =
            requiresRequantisationAcknowledgement;
        QualityNotice = qualityNotice;
    }

    public CompatibilityOptimizationLabelCode RecommendedLabelCode { get; }
    public int? RecommendedSliderValue { get; }
    public IReadOnlyList<CompatibilityOptimizationModeView> Modes { get; }
    /// <summary>The exact admitted setup selected for Continue.</summary>
    public CompatibilityOptimizationModeView RecommendedMode =>
        Modes.Single(mode => mode.LabelCode == RecommendedLabelCode);
    public bool RequiresPersistentArtifact { get; }
    public bool RequiresRequantisationAcknowledgement { get; }
    public OptimizationQualityNotice QualityNotice { get; }

    /// <summary>Creates immutable presentation-only fixture data.</summary>
    public static CompatibilityOptimizationView ForPresentation(
        CompatibilityOptimizationLabelCode recommendedLabelCode,
        int? recommendedSliderValue,
        IReadOnlyList<CompatibilityOptimizationModeView> modes,
        bool requiresPersistentArtifact,
        bool requiresRequantisationAcknowledgement,
        OptimizationQualityNotice qualityNotice)
    {
        ArgumentNullException.ThrowIfNull(modes);
        CompatibilityOptimizationLabelCode[] expectedLabels =
        [
            CompatibilityOptimizationLabelCode.Automatic,
            CompatibilityOptimizationLabelCode.MaximumEfficiency,
            CompatibilityOptimizationLabelCode.Efficient,
            CompatibilityOptimizationLabelCode.Balanced,
            CompatibilityOptimizationLabelCode.HighCapability,
            CompatibilityOptimizationLabelCode.MaximumCapability
        ];
        int?[] expectedSliders = [null, 10, 30, 50, 70, 90];
        CompatibilityOptimizationModeView[] recommendations =
        [.. modes.Where(mode => mode.LabelCode == recommendedLabelCode)];
        if (recommendedLabelCode == CompatibilityOptimizationLabelCode.Unspecified
            || !Enum.IsDefined(recommendedLabelCode)
            || !Enum.IsDefined(qualityNotice)
            || recommendedSliderValue is < 0 or > 100
            || requiresRequantisationAcknowledgement
                && !requiresPersistentArtifact
            || modes.Count != expectedLabels.Length
            || !modes.Select(mode => mode.LabelCode).SequenceEqual(expectedLabels)
            || !modes.Select(mode => mode.SliderValue).SequenceEqual(expectedSliders)
            || recommendations.Length != 1
            || recommendations[0].SliderValue != recommendedSliderValue
            || recommendations[0].RequiresPersistentArtifact
                != requiresPersistentArtifact
            || recommendations[0].RequiresRequantisationAcknowledgement
                != requiresRequantisationAcknowledgement
            || recommendations[0].QualityNotice != qualityNotice)
        {
            throw new ArgumentException(
                "An optimization fixture must carry a valid recommendation and "
                + "distinct resolved modes.");
        }

        return new CompatibilityOptimizationView(
            recommendedLabelCode,
            recommendedSliderValue,
            modes,
            requiresPersistentArtifact,
            requiresRequantisationAcknowledgement,
            qualityNotice);
    }
}
