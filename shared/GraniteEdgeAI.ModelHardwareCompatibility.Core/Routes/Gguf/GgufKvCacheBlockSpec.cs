namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// How many bytes one KV-cache format actually spends on a block of values.
///
/// This is deliberately the encoded block size rather than a nominal bit width.
/// The examined SYCL TQ3_0 stores a 32-value block in 14 bytes - about 3.5 bits
/// per value, not 3 - and Q8_0 spends 34 bytes on 32 values because each block
/// carries a scale alongside its quantised values. Using the nominal width would
/// understate the cache, which is a false-safe error.
/// </summary>
internal readonly record struct GgufKvCacheBlockSpec
{
    private GgufKvCacheBlockSpec(int valuesPerBlock, int bytesPerBlock)
    {
        ValuesPerBlock = valuesPerBlock;
        BytesPerBlock = bytesPerBlock;
    }

    internal int ValuesPerBlock { get; }

    internal int BytesPerBlock { get; }

    /// <summary>
    /// Returns false for a format with no established encoding, so a new enum
    /// member can never fall through to a silently free cache.
    /// </summary>
    internal static bool TryFor(GgufKvCacheFormat format, out GgufKvCacheBlockSpec spec)
    {
        spec = format switch
        {
            GgufKvCacheFormat.F16 => new GgufKvCacheBlockSpec(1, 2),
            GgufKvCacheFormat.Q8_0 => new GgufKvCacheBlockSpec(32, 34),
            GgufKvCacheFormat.TurboQuant3Bit => new GgufKvCacheBlockSpec(32, 14),
            _ => default
        };

        return spec.ValuesPerBlock > 0;
    }

    internal static GgufKvCacheBlockSpec For(GgufKvCacheFormat format) =>
        TryFor(format, out GgufKvCacheBlockSpec spec)
            ? spec
            : throw new ArgumentOutOfRangeException(
                nameof(format),
                "This KV-cache format has no recorded encoding; sizing it would "
                + "mean inventing a block layout.");
}
