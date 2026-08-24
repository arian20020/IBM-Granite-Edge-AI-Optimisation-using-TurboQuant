namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// How much of the model is placed on a GPU.
/// </summary>
public enum GpuOffloadLevel
{
    Unspecified = 0,
    None,
    Partial,
    Full
}
