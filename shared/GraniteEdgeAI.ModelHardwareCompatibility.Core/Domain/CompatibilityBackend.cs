namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// The execution backend a configuration binds to. Each value belongs to
/// exactly one runtime route.
/// </summary>
internal enum CompatibilityBackend
{
    Unspecified = 0,
    Cpu,
    IntelSycl,
    IntelVulkan,
    OpenVinoCpu,
    OpenVinoGpu,
    OpenVinoNpu
}
