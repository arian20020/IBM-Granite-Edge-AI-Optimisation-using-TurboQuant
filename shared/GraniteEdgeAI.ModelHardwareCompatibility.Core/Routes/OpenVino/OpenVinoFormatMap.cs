namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// How much space each OpenVINO representation actually takes.
///
/// These are the encoded widths, not nominal ones. INT4 weights are stored with
/// per-group scales, so four bits per weight understates the file by a margin
/// that grows with model size - and understating weights is the direction that
/// admits a configuration the machine cannot hold.
///
/// Every value here is a documented property of the representation rather than
/// a measurement of a particular build, which is why an estimate built from
/// them is graded Estimated and never Measured.
/// </summary>
internal static class OpenVinoFormatMap
{
    /// <summary>
    /// Effective bits per weight, including the group scale and zero point that
    /// the low-bit representations carry alongside the weight itself.
    ///
    /// INT8 and INT4 use a group size of 128 with an FP16 scale and an FP16
    /// zero point per group, which is 32 bits spread over 128 weights, so a
    /// quarter of a bit each.
    /// </summary>
    internal static decimal BitsPerWeight(OpenVinoWeightFormat format) => format switch
    {
        OpenVinoWeightFormat.Fp16 => 16m,
        OpenVinoWeightFormat.Int8 => 8m + 0.25m,
        OpenVinoWeightFormat.Int4 => 4m + 0.25m,
        OpenVinoWeightFormat.MxFp4 => 4m + 0.25m,

        // Original is not a width. Its size is the file that already exists,
        // which the caller measured, so deriving a figure here would contradict
        // something already known.
        _ => throw new ArgumentOutOfRangeException(
            nameof(format),
            format,
            "This representation has no bit width of its own; its size comes from "
            + "the source file.")
    };

    internal static bool HasBitWidth(OpenVinoWeightFormat format) =>
        format is OpenVinoWeightFormat.Fp16
            or OpenVinoWeightFormat.Int8
            or OpenVinoWeightFormat.Int4
            or OpenVinoWeightFormat.MxFp4;

    /// <summary>
    /// Whether running this representation means writing a new package first.
    ///
    /// Original runs what is already there. Everything else is a persistent
    /// conversion, and the difference decides whether the user is agreeing to
    /// disk writes at all.
    /// </summary>
    internal static bool RequiresPersistentConversion(OpenVinoWeightFormat format) =>
        format != OpenVinoWeightFormat.Original;

    /// <summary>
    /// Bytes per cached element per layer.
    ///
    /// RouteDefault resolves to F16, which is what the runtime uses when it is
    /// not told otherwise. Resolving it to the cheapest option instead would
    /// estimate a configuration nobody asked for.
    /// </summary>
    internal static decimal CacheBytesPerElement(OpenVinoKvCacheFormat format) => format switch
    {
        OpenVinoKvCacheFormat.RouteDefault => 2m,
        OpenVinoKvCacheFormat.F16 => 2m,
        OpenVinoKvCacheFormat.Bf16 => 2m,

        // Quantised caches carry a per-group scale in the same way the weights
        // do, so neither is exactly one byte or exactly half of one.
        OpenVinoKvCacheFormat.U8 => 1m + 0.0625m,
        OpenVinoKvCacheFormat.U4 => 0.5m + 0.0625m,

        _ => throw new ArgumentOutOfRangeException(
            nameof(format),
            format,
            "A cache format with no width cannot be estimated, and treating it as "
            + "free would understate every context length.")
    };

    /// <summary>
    /// Exact quantised-cache record layouts. These are whole records, not
    /// average scalar widths: each key and value head is padded independently.
    /// </summary>
    internal static bool TryGetCacheBlockLayout(
        OpenVinoKvCacheFormat format,
        out int valuesPerBlock,
        out int bytesPerBlock)
    {
        valuesPerBlock = 128;

        bytesPerBlock = format switch
        {
            OpenVinoKvCacheFormat.U8 => 136,
            OpenVinoKvCacheFormat.U4 => 72,
            OpenVinoKvCacheFormat.TurboQuantTbq4 => 68,
            OpenVinoKvCacheFormat.TurboQuantTbq3 => 52,
            _ => 0
        };

        return bytesPerBlock != 0;
    }
}
