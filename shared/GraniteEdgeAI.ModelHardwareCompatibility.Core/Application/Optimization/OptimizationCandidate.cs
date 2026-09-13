using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

internal sealed record GgufWeightNormalizationProof
{
    private GgufWeightNormalizationProof(
        int fileType,
        int quantisationVersion,
        WeightQuantisation source,
        GgufWeightFormat admittedWeight)
    {
        FileType = fileType;
        QuantisationVersion = quantisationVersion;
        Source = source;
        AdmittedWeight = admittedWeight;
    }

    internal int FileType { get; }

    internal int QuantisationVersion { get; }

    internal WeightQuantisation Source { get; }

    internal GgufWeightFormat AdmittedWeight { get; }

    internal static GgufWeightNormalizationProof FromInspection(
        int? fileType,
        int? quantisationVersion,
        GgufWeightFormat admittedWeight)
    {
        WeightQuantisation source = WeightQuantisationMap.FromGgufFileType(
            fileType, quantisationVersion);
        if (fileType is not { } exactFileType
            || quantisationVersion is not { } exactVersion
            || admittedWeight == GgufWeightFormat.Imported
            || source != GgufWeightFormatMap.ToCanonical(admittedWeight))
        {
            throw new ArgumentException(
                "A normalization proof requires exact inspected GGUF metadata "
                + "matching the admitted named weight format.",
                nameof(admittedWeight));
        }

        return new GgufWeightNormalizationProof(
            exactFileType, exactVersion, source, admittedWeight);
    }
}

/// <summary>
/// How good a candidate is on one axis, in terms both routes can be scored on.
///
/// Coarse on purpose. A finer scale would imply a precision the evidence does
/// not support and would make two candidates look separable when nothing
/// distinguishes them, which is how a slider band ends up inventing a
/// difference to justify itself.
/// </summary>
public enum OptimizationAssessment
{
    /// <summary>Never planned against. Unknown fails closed.</summary>
    Unknown = 0,
    Poor = 1,
    Acceptable = 2,
    Good = 3,
    Excellent = 4
}

/// <summary>
/// Why a candidate could not be offered.
///
/// A typed exclusion rather than a silent absence: a configuration that
/// vanished without explanation looks like one that was never possible, and the
/// user cannot tell which of those they are looking at.
/// </summary>
public enum OptimizationExclusionReason
{
    None = 0,
    EstimateNotEstablished,
    ExceedsSafeMemoryBudget,
    InsufficientDiskSpace,
    ContextBelowWorkloadMinimum,
    EvidenceBelowAdmissionLevel,
    ExperimentalNotAdmitted,
    QualityBelowFloor,
    Dominated,
    RequantisationNotAuthorized,
    ExecutionAuthorityNotEstablished,
    HardwareCapabilityUnavailable,
    DedicatedMemoryNotEstablished,
    ExceedsDedicatedDeviceMemory,
    CurrentModelQualityEvidenceUnavailable
}

/// <summary>A closed presentation notice derived from admitted candidate facts.</summary>
public enum OptimizationCandidateNotice
{
    None = 0,
    LowQuality,
    Requantisation,
    LowQualityRequantisation
}

public enum OptimizationConversionProvenance
{
    None = 0,
    HigherPrecisionSource,
    ControlledRequantisation
}

/// <summary>
/// The comparable part of a candidate: what it costs and how good it is.
///
/// This is what the shared layer is allowed to reason about. Everything
/// route-specific stays in the sealed configuration, so the planner can rank a
/// GGUF setup against an OpenVINO one without either route's vocabulary leaking
/// into the comparison.
///
/// Every field is required. There is no partially-populated metrics record,
/// because a zero in a memory field and an unmeasured memory field would then
/// be indistinguishable, and one of those is safe to compare while the other
/// is not.
/// </summary>
public sealed record OptimizationCandidateMetrics
{
    private OptimizationCandidateMetrics(
        EvidenceGrade evidence,
        OptimizationAssessment quality,
        OptimizationAssessment performance,
        OptimizationAssessment stability,
        int contextTokens,
        ulong predictedPeakBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        ulong workingDiskBytes,
        ulong outputDiskBytes,
        bool requiresPersistentChange,
        ulong? availableDiskBytes,
        ulong? dedicatedRequiredBytes,
        ulong? dedicatedSafeBudgetBytes,
        ulong? dedicatedHeadroomBytes)
    {
        Evidence = evidence;
        Quality = quality;
        Performance = performance;
        Stability = stability;
        ContextTokens = contextTokens;
        PredictedPeakBytes = predictedPeakBytes;
        SafeBudgetBytes = safeBudgetBytes;
        HeadroomBytes = headroomBytes;
        WorkingDiskBytes = workingDiskBytes;
        OutputDiskBytes = outputDiskBytes;
        RequiresPersistentChange = requiresPersistentChange;
        AvailableDiskBytes = availableDiskBytes;
        DedicatedRequiredBytes = dedicatedRequiredBytes;
        DedicatedSafeBudgetBytes = dedicatedSafeBudgetBytes;
        DedicatedHeadroomBytes = dedicatedHeadroomBytes;
    }

    public EvidenceGrade Evidence { get; }

    public OptimizationAssessment Quality { get; }

    public OptimizationAssessment Performance { get; }

    public OptimizationAssessment Stability { get; }

    public int ContextTokens { get; }

    /// <summary>
    /// Legacy V2 name for peak system/shared-memory pressure. Dedicated device
    /// memory is never included.
    /// </summary>
    public ulong PredictedPeakBytes { get; }

    public ulong SystemSharedPredictedPeakBytes => PredictedPeakBytes;

    public ulong SafeBudgetBytes { get; }

    public ulong SystemSharedSafeBudgetBytes => SafeBudgetBytes;

    /// <summary>Budget left over. Zero when the candidate does not fit.</summary>
    public ulong HeadroomBytes { get; }

    public ulong SystemSharedHeadroomBytes => HeadroomBytes;

    public ulong? DedicatedRequiredBytes { get; }
    public ulong? DedicatedSafeBudgetBytes { get; }
    public ulong? DedicatedHeadroomBytes { get; }

    /// <summary>
    /// Composite peak storage obligation during any working lifecycle phase.
    /// It may already include the final output, so it is never added to
    /// <see cref="OutputDiskBytes"/> when determining the disk obligation.
    /// </summary>
    public ulong WorkingDiskBytes { get; }

    /// <summary>Space the finished output occupies. Zero for runtime-only work.</summary>
    public ulong OutputDiskBytes { get; }

    /// <summary>
    /// Exact free-disk observation used to admit this candidate. Null exists
    /// only for the frozen version-two vocabulary; new resolution fails closed
    /// when no disk admission proof is present.
    /// </summary>
    public ulong? AvailableDiskBytes { get; }

    /// <summary>The peak disk obligation without double-counting overlapping phases.</summary>
    public ulong DiskObligationBytes => Math.Max(WorkingDiskBytes, OutputDiskBytes);

    /// <summary>
    /// Whether choosing this writes a new model or package.
    ///
    /// The one fact the confirmation surface must get right: it is the
    /// difference between changing a setting and creating a file, and the user
    /// is agreeing to different things in each case.
    /// </summary>
    public bool RequiresPersistentChange { get; }

    public static OptimizationCandidateMetrics Create(
        EvidenceGrade evidence,
        OptimizationAssessment quality,
        OptimizationAssessment performance,
        OptimizationAssessment stability,
        int contextTokens,
        ulong predictedPeakBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        ulong workingDiskBytes,
        ulong outputDiskBytes,
        bool requiresPersistentChange,
        ulong? availableDiskBytes = null,
        ulong? dedicatedRequiredBytes = null,
        ulong? dedicatedSafeBudgetBytes = null,
        ulong? dedicatedHeadroomBytes = null)
    {
        if (evidence == EvidenceGrade.Unknown || !Enum.IsDefined(evidence))
        {
            throw new ArgumentException(
                "Unknown or undefined evidence fails closed: an ungraded candidate must not "
                + "compete against a graded one.",
                nameof(evidence));
        }

        RequireAssessed(quality, nameof(quality));
        RequireAssessed(performance, nameof(performance));
        RequireAssessed(stability, nameof(stability));

        if (contextTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextTokens),
                contextTokens,
                "A candidate that can read nothing is not a candidate.");
        }

        if (predictedPeakBytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(predictedPeakBytes),
                predictedPeakBytes,
                "A peak of zero reads as a configuration that costs nothing, which "
                + "is the shape an unestimated candidate would take.");
        }

        bool anyDedicated = dedicatedRequiredBytes.HasValue
            || dedicatedSafeBudgetBytes.HasValue
            || dedicatedHeadroomBytes.HasValue;
        bool completeDedicated = dedicatedRequiredBytes.HasValue
            && dedicatedSafeBudgetBytes.HasValue
            && dedicatedHeadroomBytes.HasValue;
        if (anyDedicated != completeDedicated
            || completeDedicated
                && (dedicatedRequiredBytes == 0
                    || dedicatedSafeBudgetBytes == 0
                    || dedicatedRequiredBytes > dedicatedSafeBudgetBytes
                    || dedicatedHeadroomBytes
                        != dedicatedSafeBudgetBytes - dedicatedRequiredBytes))
        {
            throw new ArgumentException(
                "Dedicated memory admission must be complete, separate, and safe.");
        }

        return new OptimizationCandidateMetrics(
            evidence,
            quality,
            performance,
            stability,
            contextTokens,
            predictedPeakBytes,
            safeBudgetBytes,
            headroomBytes,
            workingDiskBytes,
            outputDiskBytes,
            requiresPersistentChange,
            availableDiskBytes,
            dedicatedRequiredBytes,
            dedicatedSafeBudgetBytes,
            dedicatedHeadroomBytes);
    }

    /// <summary>Whether the peak fits inside what this candidate was allowed.</summary>
    public bool FitsSafely => PredictedPeakBytes <= SafeBudgetBytes;

    /// <summary>
    /// Whether a positive, explicit disk observation covers the peak disk
    /// obligation. Working and output figures are alternative phase peaks, not
    /// additive reservations.
    /// </summary>
    public bool FitsDiskSafely => AvailableDiskBytes is > 0
        && DiskObligationBytes <= AvailableDiskBytes.Value;

    private static void RequireAssessed(OptimizationAssessment value, string parameter)
    {
        if (value == OptimizationAssessment.Unknown || !Enum.IsDefined(value))
        {
            throw new ArgumentException(
                "An unknown or undefined axis fails closed. Ranking against it would treat "
                + "'not established' as a score.",
                parameter);
        }
    }
}

/// <summary>
/// One complete thing the user could be given.
///
/// Not a weight precision. It carries the whole configuration - route, device,
/// representation, cache, context and route controls - because a preference
/// selects a setup rather than a number, and the page has to be able to show
/// what was actually chosen.
///
/// The configuration is the sealed route record. Shared code compares
/// <see cref="Metrics"/>; an executor pattern-matches the configuration to its
/// own type and refuses anything else.
/// </summary>
public sealed record OptimizationCandidate
{
    private OptimizationCandidate(
        OptimizationRoute route,
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental,
        OptimizationConversionProvenance conversionProvenance,
        OptimizationCandidateNotice notice,
        GgufWeightNormalizationProof? weightNormalizationProof,
        OptimizationAdmissionProof? admissionProof,
        OptimizationEvidenceRecord? evidence)
    {
        Route = route;
        Configuration = configuration;
        Metrics = metrics;
        EvidenceId = evidenceId;
        IsExperimental = isExperimental;
        ConversionProvenance = conversionProvenance;
        Notice = notice;
        WeightNormalizationProof = weightNormalizationProof;
        AdmissionProof = admissionProof;
        Evidence = evidence;
    }

    public OptimizationRoute Route { get; }

    /// <summary>
    /// The sealed route configuration. Exactly one concrete type per route, and
    /// the route discriminator always agrees with it.
    /// </summary>
    public RouteConfiguration Configuration { get; }

    public OptimizationCandidateMetrics Metrics { get; }

    /// <summary>The capability record that admitted this combination.</summary>
    public string EvidenceId { get; }

    public bool IsExperimental { get; }

    public OptimizationConversionProvenance ConversionProvenance { get; }

    public OptimizationCandidateNotice Notice { get; }

    internal GgufWeightNormalizationProof? WeightNormalizationProof { get; }

    internal OptimizationAdmissionProof? AdmissionProof { get; }

    /// <summary>
    /// Exact measured evidence for newly issued evidence-backed candidates.
    /// Null is retained only for existing versioned candidates and fixtures.
    /// </summary>
    public OptimizationEvidenceRecord? Evidence { get; }

    /// <summary>
    /// Exact methodology-bound quality when available. Legacy candidates retain
    /// a conservative coarse projection so they remain readable but cannot
    /// masquerade as measured evidence.
    /// </summary>
    public decimal QualityScore => Evidence?.Quality.Value ?? Metrics.Quality switch
    {
        OptimizationAssessment.Poor => 0m,
        OptimizationAssessment.Acceptable => 4m,
        OptimizationAssessment.Good => 6m,
        OptimizationAssessment.Excellent => 8m,
        _ => 0m
    };

    public OptimizationQualityLevel QualityLevel =>
        Evidence?.Quality.Level ?? Metrics.Quality switch
        {
            OptimizationAssessment.Poor => OptimizationQualityLevel.BelowMinimum,
            OptimizationAssessment.Acceptable => OptimizationQualityLevel.Acceptable,
            OptimizationAssessment.Good => OptimizationQualityLevel.Good,
            OptimizationAssessment.Excellent => OptimizationQualityLevel.Excellent,
            _ => OptimizationQualityLevel.BelowMinimum
        };

    /// <summary>
    /// Ordinal, stable, and covering the whole candidate rather than the
    /// configuration alone. Context is folded in here because two candidates
    /// differing only in context length are different candidates, and the route
    /// descriptor deliberately does not carry it.
    /// </summary>
    public string CanonicalDescriptor =>
        $"{Configuration.CanonicalDescriptor}|ctx={Metrics.ContextTokens}";

    public static OptimizationCandidate Create(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental,
        OptimizationConversionProvenance conversionProvenance =
            OptimizationConversionProvenance.None)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(metrics);

        OptimizationIdentifier.Require(
            evidenceId, nameof(evidenceId), "The evidence behind a candidate");

        if (!Enum.IsDefined(conversionProvenance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(conversionProvenance), conversionProvenance,
                "An undefined conversion provenance cannot be planned.");
        }

        if (configuration is GgufRouteConfiguration
                { Weights: GgufWeightFormat.Q2K }
            && metrics.Quality != OptimizationAssessment.Poor)
        {
            throw new ArgumentException(
                "A Q2_K candidate is always Poor on the coarse quality scale. "
                + "Caller-supplied metrics cannot upgrade the product floor.",
                nameof(metrics));
        }

        if (conversionProvenance != OptimizationConversionProvenance.None
            && !metrics.RequiresPersistentChange)
        {
            throw new ArgumentException(
                "Conversion provenance requires a persistent new artifact.",
                nameof(conversionProvenance));
        }

        OptimizationCandidateNotice notice = ExpectedNotice(
            configuration, metrics, conversionProvenance);

        // The discriminator is derived from the configuration rather than
        // supplied alongside it, so the two can never disagree and no caller
        // can label a GGUF configuration as an OpenVINO one.
        OptimizationRoute route = configuration switch
        {
            GgufRouteConfiguration => OptimizationRoute.Gguf,
            OpenVinoRouteConfiguration => OptimizationRoute.OpenVino,
            _ => throw new ArgumentException(
                $"{configuration.GetType().Name} belongs to no known route, so no "
                + "executor could claim it.",
                nameof(configuration))
        };

        return new OptimizationCandidate(
            route, configuration, metrics, evidenceId, isExperimental,
            conversionProvenance, notice, weightNormalizationProof: null,
            admissionProof: null, evidence: null);
    }

    public static OptimizationCandidate Create(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        OptimizationEvidenceRecord evidence,
        bool isExperimental,
        OptimizationConversionProvenance conversionProvenance =
            OptimizationConversionProvenance.None)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!evidence.IsAdmitted)
        {
            throw new ArgumentException(
                "Evidence-backed candidates require quality at or above the floor and every hard gate.",
                nameof(evidence));
        }

        OptimizationCandidate validated = Create(
            configuration,
            metrics,
            evidence.EvidenceId,
            isExperimental,
            conversionProvenance);
        if (!MatchesEvidenceBinding(validated, evidence.Key))
        {
            throw new ArgumentException(
                "Evidence must describe the candidate's exact route, configuration, and context.",
                nameof(evidence));
        }

        return new OptimizationCandidate(
            validated.Route,
            validated.Configuration,
            validated.Metrics,
            validated.EvidenceId,
            validated.IsExperimental,
            validated.ConversionProvenance,
            validated.Notice,
            validated.WeightNormalizationProof,
            validated.AdmissionProof,
            evidence);
    }

    private static bool MatchesEvidenceBinding(
        OptimizationCandidate candidate,
        OptimizationEvidenceKey key) =>
        key.Route == candidate.Route
        && key.ContextTokens == candidate.Metrics.ContextTokens
        && candidate.Configuration switch
        {
            GgufRouteConfiguration gguf => MatchesGgufEvidence(
                gguf, candidate.Metrics, key),
            OpenVinoRouteConfiguration openVino =>
                MatchesOpenVinoEvidence(openVino, candidate.Metrics, key),
            _ => false
        };

    private static bool MatchesGgufEvidence(
        GgufRouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        OptimizationEvidenceKey key)
    {
        string? targetWeights = configuration.Weights switch
        {
            GgufWeightFormat.BF16 => "bf16",
            GgufWeightFormat.F16 => "f16",
            GgufWeightFormat.Q8_0 => "q8_0",
            GgufWeightFormat.Q6K => "q6_k",
            GgufWeightFormat.Q5KM => "q5_k_m",
            GgufWeightFormat.Q4KM => "q4_k_m",
            GgufWeightFormat.Q3KM => "q3_k_m",
            GgufWeightFormat.Q2K => "q2_k",
            GgufWeightFormat.TQ4_1S => "tq4_1s",
            GgufWeightFormat.TQ3_1S => "tq3_1s",
            _ => null
        };
        string? cache = configuration.KvCache switch
        {
            GgufKvCacheFormat.F16 => "f16",
            GgufKvCacheFormat.Q8_0 => "q8_0",
            GgufKvCacheFormat.TurboQuant4Bit => "turbo4",
            GgufKvCacheFormat.TurboQuant3Bit => "turbo3",
            GgufKvCacheFormat.TurboQuant2Bit => "turbo2",
            _ => null
        };
        OptimizationEvidenceBackend? backend = configuration.Backend switch
        {
            CompatibilityBackend.Cpu => OptimizationEvidenceBackend.Cpu,
            CompatibilityBackend.IntelVulkan => OptimizationEvidenceBackend.Vulkan,
            CompatibilityBackend.IntelSycl => OptimizationEvidenceBackend.Sycl,
            _ => null
        };
        string? executionProfile = (configuration.Backend, configuration.Offload) switch
        {
            (CompatibilityBackend.Cpu, GpuOffloadLevel.None) => "cpu",
            (CompatibilityBackend.IntelVulkan, GpuOffloadLevel.Partial) =>
                "vulkan-partial",
            (CompatibilityBackend.IntelVulkan, GpuOffloadLevel.Full) =>
                "vulkan-full",
            _ => null
        };

        return cache is not null
            && backend is not null
            && executionProfile is not null
            && TryMapDevice(configuration.Device, out OptimizationEvidenceDeviceClass device)
            && key.Backend == backend
            && key.DeviceClass == device
            && key.CacheConfiguration == cache
            && key.ExecutionProfile == executionProfile
            && (metrics.RequiresPersistentChange
                || key.SourceWeightRepresentation == key.TargetWeightRepresentation)
            && (configuration.Weights == GgufWeightFormat.Imported
                ? key.SourceWeightRepresentation == "q4_k_m"
                    && key.TargetWeightRepresentation == "q4_k_m"
                : key.TargetWeightRepresentation == targetWeights);
    }

    private static bool MatchesOpenVinoEvidence(
        OpenVinoRouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        OptimizationEvidenceKey key)
    {
        string? weights = configuration.Weights switch
        {
            OpenVinoWeightFormat.Fp16 => "fp16",
            OpenVinoWeightFormat.Int8 => "int8",
            OpenVinoWeightFormat.Int4 => "int4",
            OpenVinoWeightFormat.MxFp4 => "mxfp4",
            _ => null
        };
        string? cache = configuration.KvCache switch
        {
            OpenVinoKvCacheFormat.RouteDefault => "released-default",
            OpenVinoKvCacheFormat.F16 => "f16",
            OpenVinoKvCacheFormat.Bf16 => "bf16",
            OpenVinoKvCacheFormat.U8 => "u8",
            OpenVinoKvCacheFormat.U4 => "u4",
            OpenVinoKvCacheFormat.TurboQuantTbq4 => "tbq4",
            OpenVinoKvCacheFormat.TurboQuantTbq3 => "tbq3",
            _ => null
        };
        OptimizationEvidenceBackend? backend = configuration.Device switch
        {
            DeviceRouteId.Cpu => OptimizationEvidenceBackend.OpenVinoCpu,
            DeviceRouteId.IntelIntegratedGpu or DeviceRouteId.IntelDiscreteGpu =>
                OptimizationEvidenceBackend.OpenVinoGpu,
            DeviceRouteId.IntelNpu => OptimizationEvidenceBackend.OpenVinoNpu,
            _ => null
        };
        string? executionProfile = configuration.Device switch
        {
            DeviceRouteId.Cpu => "openvino-cpu",
            DeviceRouteId.IntelIntegratedGpu or DeviceRouteId.IntelDiscreteGpu =>
                "openvino-gpu",
            DeviceRouteId.IntelNpu => "openvino-npu",
            _ => null
        };

        return cache is not null
            && backend is not null
            && executionProfile is not null
            && TryMapDevice(configuration.Device, out OptimizationEvidenceDeviceClass device)
            && key.Backend == backend
            && key.DeviceClass == device
            && key.CacheConfiguration == cache
            && key.ExecutionProfile == executionProfile
            && (metrics.RequiresPersistentChange
                || key.SourceWeightRepresentation == key.TargetWeightRepresentation)
            && (configuration.Weights == OpenVinoWeightFormat.Original
                ? key.SourceWeightRepresentation == "fp16"
                    && key.TargetWeightRepresentation == "fp16"
                : key.SourceWeightRepresentation == weights
                    && key.TargetWeightRepresentation == weights);
    }

    private static bool TryMapDevice(
        DeviceRouteId device,
        out OptimizationEvidenceDeviceClass evidenceDevice)
    {
        evidenceDevice = device switch
        {
            DeviceRouteId.Cpu => OptimizationEvidenceDeviceClass.Cpu,
            DeviceRouteId.IntelIntegratedGpu =>
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
            DeviceRouteId.IntelDiscreteGpu =>
                OptimizationEvidenceDeviceClass.IntelDiscreteGpu,
            DeviceRouteId.IntelNpu => OptimizationEvidenceDeviceClass.IntelNpu,
            _ => OptimizationEvidenceDeviceClass.Unspecified
        };
        return evidenceDevice != OptimizationEvidenceDeviceClass.Unspecified;
    }

    internal static OptimizationCandidate CreateWithGgufWeightNormalization(
        GgufRouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental,
        GgufWeightNormalizationProof proof,
        OptimizationEvidenceRecord? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (configuration.Weights != GgufWeightFormat.Imported
            || metrics.RequiresPersistentChange)
        {
            throw new ArgumentException(
                "Only an imported, runtime-only GGUF candidate can carry an "
                + "already-at-target normalization proof.",
                nameof(configuration));
        }

        if (evidence is not null && evidence.EvidenceId != evidenceId)
        {
            throw new ArgumentException("Normalized evidence must retain its exact identity.", nameof(evidence));
        }
        OptimizationCandidate validated = evidence is null
            ? Create(configuration, metrics, evidenceId, isExperimental)
            : Create(configuration, metrics, evidence, isExperimental);
        return new OptimizationCandidate(
            validated.Route, validated.Configuration, validated.Metrics,
            validated.EvidenceId, validated.IsExperimental,
            validated.ConversionProvenance, validated.Notice, proof,
            admissionProof: null, evidence: validated.Evidence);
    }

    internal static OptimizationCandidate AttachAdmissionProof(
        OptimizationCandidate candidate,
        OptimizationAdmissionProof proof)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(proof);
        if (!proof.MatchesCandidate(candidate))
        {
            throw new ArgumentException(
                "An admission proof must describe the exact candidate it admits.",
                nameof(proof));
        }

        return new OptimizationCandidate(
            candidate.Route, candidate.Configuration, candidate.Metrics,
            candidate.EvidenceId, candidate.IsExperimental,
            candidate.ConversionProvenance, candidate.Notice,
            candidate.WeightNormalizationProof, proof, candidate.Evidence);
    }

    internal static OptimizationCandidateNotice ExpectedNotice(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        OptimizationConversionProvenance provenance)
    {
        if (provenance == OptimizationConversionProvenance.ControlledRequantisation)
        {
            return configuration is GgufRouteConfiguration
                    { Weights: GgufWeightFormat.Q2K }
                ? OptimizationCandidateNotice.LowQualityRequantisation
                : OptimizationCandidateNotice.Requantisation;
        }

        return metrics.Quality == OptimizationAssessment.Poor
            ? OptimizationCandidateNotice.LowQuality
            : OptimizationCandidateNotice.None;
    }

    /// <summary>
    /// Reconstructs an authentic released-v2 candidate whose vocabulary had no
    /// notice or conversion provenance. Kept internal so new planning cannot
    /// silently discard a warning; the v2 canonical verifier is its sole use.
    /// </summary>
    internal static OptimizationCandidate CreateLegacyVersionTwo(
        RouteConfiguration configuration,
        OptimizationCandidateMetrics metrics,
        string evidenceId,
        bool isExperimental)
    {
        if (configuration is GgufRouteConfiguration
            { Weights: GgufWeightFormat.Q2K })
        {
            throw new ArgumentException(
                "Q2_K did not exist in the released version-two vocabulary.",
                nameof(configuration));
        }

        OptimizationCandidate validated = Create(
            configuration, metrics, evidenceId, isExperimental,
            OptimizationConversionProvenance.None);

        return new OptimizationCandidate(
            validated.Route, validated.Configuration, validated.Metrics,
            validated.EvidenceId, validated.IsExperimental,
            OptimizationConversionProvenance.None,
            OptimizationCandidateNotice.None,
            weightNormalizationProof: null,
            admissionProof: null,
            evidence: null);
    }
}
