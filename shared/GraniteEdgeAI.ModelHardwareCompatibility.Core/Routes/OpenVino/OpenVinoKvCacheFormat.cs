namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// How OpenVINO stores the context while it runs.
///
/// RouteDefault is a real choice rather than an absence: it means the runtime
/// picks, and the estimate must then use the runtime's documented default
/// rather than assuming the cheapest option.
/// </summary>
public enum OpenVinoKvCacheFormat
{
    Unspecified = 0,
    RouteDefault = 1,
    F16 = 2,
    Bf16 = 3,
    U8 = 4,
    U4 = 5
}
