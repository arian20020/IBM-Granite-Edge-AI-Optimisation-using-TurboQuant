namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// Which runtime family a configuration belongs to. Facts never cross between
/// routes: a GGUF candidate cannot carry OpenVINO precision or device values,
/// and the absence of one route is never evidence about the other.
/// </summary>
internal enum RuntimeRouteId
{
    Unspecified = 0,
    LlamaCpp,
    OpenVinoGenAi
}
