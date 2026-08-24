namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

/// <summary>
/// Which way the runtime is told to trade latency against throughput.
///
/// It belongs in the configuration rather than beside it because it changes how
/// much memory the runtime commits, so two candidates differing only in this
/// are not the same candidate.
/// </summary>
public enum OpenVinoPerformanceHint
{
    Unspecified = 0,
    Latency = 1,
    Throughput = 2
}
