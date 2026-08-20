namespace GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

/// <summary>
/// Exposes one contained process, its standard streams, bounded stderr result,
/// and authoritative full-tree cleanup operations. Protocol framing and
/// parsing remain the responsibility of the route-specific client.
/// </summary>
public sealed class ProtectedWorkerSession : IAsyncDisposable
{
    private readonly WorkerProcessSession _session;
    private readonly Task<StandardErrorSnapshot> _standardErrorTask;

    internal ProtectedWorkerSession(
        WorkerProcessSession session,
        ProtectedWorkerLaunchSpec spec)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(spec);

        _session = session;
        StartupTimeout = spec.StartupTimeout;
        CancellationGrace = spec.CancellationGrace;
        CleanupTimeout = spec.CleanupTimeout;

        // Drain stderr from launch onward. Retention is bounded, but the pipe
        // is always consumed to EOF so a noisy child cannot deadlock itself.
        BoundedStandardErrorCollector collector = new(
            spec.MaximumStandardErrorBytes);
        _standardErrorTask = collector.DrainAsync(
            session.StandardError,
            CancellationToken.None);
    }

    public uint ProcessId => _session.ProcessId;

    public Stream StandardInput => _session.StandardInput;

    public Stream StandardOutput => _session.StandardOutput;

    public TimeSpan StartupTimeout { get; }

    public TimeSpan CancellationGrace { get; }

    public TimeSpan CleanupTimeout { get; }

    public Task<StandardErrorSnapshot> ReadStandardErrorAsync() =>
        _standardErrorTask;

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _session.WaitForExitAsync(cancellationToken);

    public int GetExitCode() => _session.GetExitCode();

    public uint GetActiveProcessCount() =>
        _session.Job.GetActiveProcessCount();

    public Task<bool> TerminateAndVerifyEmptyAsync() =>
        _session.TerminateAndVerifyEmptyAsync(CleanupTimeout);

    public Task<bool> WaitForTreeEmptyAsync() =>
        _session.WaitForTreeEmptyAsync(CleanupTimeout);

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
