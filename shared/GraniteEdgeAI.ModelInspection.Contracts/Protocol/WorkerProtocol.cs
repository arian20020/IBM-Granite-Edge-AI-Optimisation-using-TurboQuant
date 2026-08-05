namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Defines the stable identity, size limits, and time limits shared by the
/// Model Inspection application adapter and the protected worker process.
/// </summary>
public static class WorkerProtocol
{
    /// <summary>
    /// Gets the first supported worker-protocol version.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Gets the exact worker identity expected by the application.
    /// </summary>
    public const string WorkerId = "GraniteEdgeAI.ModelInspection.Worker";

    /// <summary>
    /// Gets the exact first verified LLamaSharp CPU runtime profile.
    /// </summary>
    public const string RuntimeProfile =
        "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1";

    /// <summary>
    /// Gets the maximum UTF-8 byte length of one command or message line.
    /// </summary>
    public const int MaximumMessageBytes = 1024 * 1024;

    /// <summary>
    /// Gets the maximum UTF-8 byte count retained from standard error.
    /// </summary>
    public const int MaximumRetainedStandardErrorBytes = 256 * 1024;

    /// <summary>
    /// Gets the maximum time allowed for the worker hello message.
    /// </summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the maximum duration of one lightweight inspection operation.
    /// </summary>
    public static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the cooperative-cancellation grace period before later process
    /// integration treats the worker as unresponsive.
    /// </summary>
    public static readonly TimeSpan CancellationGracePeriod =
        TimeSpan.FromSeconds(5);
}
