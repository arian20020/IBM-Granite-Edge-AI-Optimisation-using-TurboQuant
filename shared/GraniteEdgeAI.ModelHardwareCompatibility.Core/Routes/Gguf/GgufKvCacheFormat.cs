namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Runtime KV-cache formats. TurboQuant is one logical option here; the
/// backend-specific implementation is selected when a backend is bound.
/// </summary>
public enum GgufKvCacheFormat
{
    Unspecified = 0,
    F16,
    Q8_0,
    TurboQuant3Bit,

    // Appended to preserve TurboQuant3Bit's existing persisted value.
    TurboQuant4Bit,
    TurboQuant2Bit
}

internal static class GgufTurboQuantFormatPolicy
{
    internal static bool IsTurboQuant(GgufKvCacheFormat format) =>
        format is GgufKvCacheFormat.TurboQuant4Bit
            or GgufKvCacheFormat.TurboQuant3Bit
            or GgufKvCacheFormat.TurboQuant2Bit;

    internal static bool IsTurboQuant(GgufWeightFormat format) =>
        format is GgufWeightFormat.TQ4_1S or GgufWeightFormat.TQ3_1S;

    internal static bool IsTurboQuant(
        GgufWeightFormat weights,
        GgufKvCacheFormat cache) =>
        IsTurboQuant(weights) || IsTurboQuant(cache);
}
