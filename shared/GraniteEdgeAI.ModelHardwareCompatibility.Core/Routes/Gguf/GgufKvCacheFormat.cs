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
    TurboQuant3Bit
}
