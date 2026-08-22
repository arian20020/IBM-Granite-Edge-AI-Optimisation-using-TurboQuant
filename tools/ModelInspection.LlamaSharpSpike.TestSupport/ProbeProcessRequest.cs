namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Defines one bounded, non-shell child-process invocation.
/// </summary>
public sealed record ProbeProcessRequest
{
    public required string ExecutablePath { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required string WorkingDirectory { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>
    /// Optional observer that runs while the child process is alive. The runner
    /// supplies the child PID and cancels the observer when the child exits,
    /// times out, or caller cancellation occurs.
    /// </summary>
    public Func<int, CancellationToken, Task>? WhileRunningObserver { get; init; }
}
