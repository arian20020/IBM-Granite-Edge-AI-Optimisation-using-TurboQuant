using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

/// <summary>
/// The exact settings one route executor will use: one route, one payload.
///
/// V1 had no such thing, which is why G1 could not execute a plan without
/// inventing values. The union is closed and enforced by construction for the
/// same reason the capability snapshot is: a payload carrying both routes would
/// force the shared layer to decide which one it meant.
/// </summary>
public sealed record OptimizationExecutionPayload
{
    private OptimizationExecutionPayload(
        OptimizationRoute route,
        GgufExecutionPayload? gguf,
        OpenVinoExecutionPayload? openVino)
    {
        Route = route;
        Gguf = gguf;
        OpenVino = openVino;
    }

    public OptimizationRoute Route { get; }

    /// <summary>Non-null exactly when <see cref="Route"/> is Gguf.</summary>
    public GgufExecutionPayload? Gguf { get; }

    /// <summary>Non-null exactly when <see cref="Route"/> is OpenVino.</summary>
    public OpenVinoExecutionPayload? OpenVino { get; }

    /// <summary>
    /// Whether executing this writes a new model or package, asked of whichever
    /// payload is present. Derived so the plan and the payload cannot disagree
    /// about the one fact the confirmation surface must get right.
    /// </summary>
    public bool RequiresPersistentConversion => Route switch
    {
        OptimizationRoute.Gguf => Gguf!.RequiresPersistentConversion,
        OptimizationRoute.OpenVino => OpenVino!.RequiresPersistentConversion,
        _ => throw new InvalidOperationException(
            "A payload with no route cannot say whether it writes anything.")
    };

    public static OptimizationExecutionPayload ForGguf(GgufExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new OptimizationExecutionPayload(OptimizationRoute.Gguf, payload, null);
    }

    public static OptimizationExecutionPayload ForOpenVino(OpenVinoExecutionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new OptimizationExecutionPayload(OptimizationRoute.OpenVino, null, payload);
    }
}

/// <summary>
/// Turns a coarse offload category into the exact layer count an executor uses.
///
/// The compatibility layer plans in categories, because a category is what a
/// support matrix can honestly admit. An executor needs a number. Somebody has
/// to bridge that, and it must not be the executor: turning "Partial" into a
/// count decides how much of the model runs on the GPU, and doing it after
/// confirmation means the user agreed to a placement nobody showed them.
///
/// So the relationship is defined here, deterministically, and bound into the
/// plan digest. Same category and same layer count always give the same answer,
/// on every machine and in every run.
/// </summary>
public static class GgufOffloadPolicy
{
    /// <summary>
    /// The share of layers placed on the GPU for a partial offload.
    ///
    /// Half, rounded down. A round number chosen deliberately rather than tuned:
    /// nothing has been measured, so a more precise-looking fraction would imply
    /// evidence that does not exist. It is versioned through the plan's contract
    /// version, so changing it changes every digest and forces a replan rather
    /// than silently moving layers under existing plans.
    /// </summary>
    public const decimal PartialOffloadShare = 0.5m;

    /// <summary>
    /// The exact GPU layer count for a category and a model's layer count.
    ///
    /// Fails closed on an unknown category rather than defaulting to zero.
    /// Zero is a real, valid placement - everything on the CPU - so using it as
    /// a fallback would make "we do not know" indistinguishable from a decision.
    /// </summary>
    public static int ExactLayerCount(GpuOffloadLevel level, int modelLayerCount)
    {
        if (modelLayerCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelLayerCount),
                modelLayerCount,
                "A model with no layers has no placement to decide.");
        }

        return level switch
        {
            GpuOffloadLevel.None => 0,
            GpuOffloadLevel.Full => modelLayerCount,
            GpuOffloadLevel.Partial => (int)decimal.Floor(
                modelLayerCount * PartialOffloadShare),
            _ => throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "An unspecified offload level has no exact layer count. Defaulting "
                + "to zero would make an unknown indistinguishable from a decision "
                + "to run entirely on the CPU.")
        };
    }

    /// <summary>
    /// Whether an exact count is the one the category implies. Used at plan
    /// issuance so a payload cannot quietly disagree with the candidate.
    /// </summary>
    public static bool Agrees(GpuOffloadLevel level, int modelLayerCount, int gpuLayerCount) =>
        ExactLayerCount(level, modelLayerCount) == gpuLayerCount;
}

/// <summary>
/// Maps planning vocabulary onto the two routes' execution vocabulary.
///
/// These exist so plan issuance can prove a payload agrees with the candidate it
/// claims to implement. Every mapping is total and fails closed: a planning
/// value with no execution counterpart throws rather than picking the nearest
/// one, because the nearest one is a substitution.
/// </summary>
internal static class ExecutionVocabularyMap
{
    internal static GgufRuntimeBackend ToRuntimeBackend(CompatibilityBackend backend) =>
        backend switch
        {
            CompatibilityBackend.Cpu => GgufRuntimeBackend.Cpu,
            CompatibilityBackend.IntelSycl => GgufRuntimeBackend.Sycl,
            CompatibilityBackend.IntelVulkan => GgufRuntimeBackend.Vulkan,
            _ => throw new ArgumentOutOfRangeException(
                nameof(backend),
                backend,
                "This backend has no GGUF runtime counterpart. The OpenVINO "
                + "backends belong to the other route, and Unspecified is not a "
                + "backend at all.")
        };

    internal static GgufCacheType ToCacheType(GgufKvCacheFormat format) =>
        format switch
        {
            GgufKvCacheFormat.F16 => GgufCacheType.F16,
            GgufKvCacheFormat.Q8_0 => GgufCacheType.Q8Zero,
            GgufKvCacheFormat.TurboQuant3Bit => GgufCacheType.Turbo3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "This cache format has no runtime counterpart, and choosing the "
                + "nearest one would change how the context is stored.")
        };

    /// <summary>
    /// The device string the GGUF runtime expects. Fixed spellings rather than
    /// <c>ToString</c> on the enum, so renaming a planning member cannot change
    /// what an executor is told to open.
    /// </summary>
    internal static string ToDeviceId(DeviceRouteId device) => device switch
    {
        DeviceRouteId.Cpu => "CPU",
        DeviceRouteId.IntelIntegratedGpu => "GPU.0",
        DeviceRouteId.IntelDiscreteGpu => "GPU.1",
        DeviceRouteId.IntelNpu => "NPU",
        _ => throw new ArgumentOutOfRangeException(
            nameof(device), device, "An unspecified device names nothing to open.")
    };

    internal static OpenVinoWeightPrecision ToWeightPrecision(OpenVinoWeightFormat format) =>
        format switch
        {
            OpenVinoWeightFormat.Fp16 => OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightFormat.Int8 => OpenVinoWeightPrecision.EightBit,
            OpenVinoWeightFormat.Int4 => OpenVinoWeightPrecision.FourBit,
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "The published OpenVINO route defines Fp16, EightBit and FourBit "
                + "only. Original is a source-package state rather than an "
                + "execution precision, so a plan cannot use it as a target.")
        };

    internal static OpenVinoKvCacheAlgorithm ToKvCacheAlgorithm(
        OpenVinoKvCacheFormat format) => format switch
        {
            OpenVinoKvCacheFormat.TurboQuantTbq4
                or OpenVinoKvCacheFormat.TurboQuantTbq3 =>
                OpenVinoKvCacheAlgorithm.TurboQuant,
            OpenVinoKvCacheFormat.RouteDefault
                or OpenVinoKvCacheFormat.F16
                or OpenVinoKvCacheFormat.Bf16
                or OpenVinoKvCacheFormat.U8
                or OpenVinoKvCacheFormat.U4 => OpenVinoKvCacheAlgorithm.Released,
            _ => throw new ArgumentOutOfRangeException(
                nameof(format), format, "This cache format has no execution algorithm.")
        };

    internal static OpenVinoKvCachePrecision ToKvCachePrecision(
        OpenVinoKvCacheFormat format) =>
        format switch
        {
            OpenVinoKvCacheFormat.RouteDefault => OpenVinoKvCachePrecision.ReleasedDefault,
            OpenVinoKvCacheFormat.F16 => OpenVinoKvCachePrecision.F16,
            OpenVinoKvCacheFormat.Bf16 => OpenVinoKvCachePrecision.Bf16,
            OpenVinoKvCacheFormat.U8 => OpenVinoKvCachePrecision.U8,
            OpenVinoKvCacheFormat.U4 => OpenVinoKvCachePrecision.U4,
            OpenVinoKvCacheFormat.TurboQuantTbq4 => OpenVinoKvCachePrecision.Tbq4,
            OpenVinoKvCacheFormat.TurboQuantTbq3 => OpenVinoKvCachePrecision.Tbq3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "This cache format has no execution precision.")
        };
}
