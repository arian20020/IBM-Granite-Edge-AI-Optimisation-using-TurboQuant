using GraniteEdgeAI.ModelInspection.Transport;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

/// <summary>
/// Exposes one contained process, bounded line-oriented standard I/O, bounded
/// stderr, and authoritative full-tree cleanup operations. Protocol parsing
/// remains the responsibility of the route-specific client.
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
        StandardInput = new BoundedUtf8LineWriter(
            session.StandardInput,
            spec.MaximumStandardInputLineBytes);
        StandardOutput = new BoundedUtf8LineReader(
            session.StandardOutput,
            spec.MaximumStandardOutputLineBytes);

        // Drain stderr from launch onward. Retention is bounded, but the pipe
        // is always consumed to EOF so a noisy child cannot deadlock itself.
        BoundedStandardErrorCollector collector = new(
            spec.MaximumStandardErrorBytes);
        _standardErrorTask = collector.DrainAsync(
            session.StandardError,
            CancellationToken.None);
    }

    public uint ProcessId => _session.ProcessId;

    public BoundedUtf8LineWriter StandardInput { get; }

    public BoundedUtf8LineReader StandardOutput { get; }

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
        _session.TerminateAndVerifyEmptyAsync();

    public Task<bool> WaitForTreeEmptyAsync() =>
        _session.WaitForTreeEmptyAsync();

    public ValueTask CompleteInputAsync() =>
        _session.StandardInput.DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        StandardInput.Dispose();
        await _session.DisposeAsync().ConfigureAwait(false);
    }
}
