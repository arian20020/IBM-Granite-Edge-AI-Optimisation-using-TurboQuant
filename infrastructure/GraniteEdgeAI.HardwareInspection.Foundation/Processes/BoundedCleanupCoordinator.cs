namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

internal enum OwnedCleanupStage
{
    StandardInput,
    StandardOutput,
    StandardError,
    Channel,
    Session,
    Closure,
    ProcessTree,
    ProcessHandle,
    Job,
    OperationEnvironment,
    PendingOutput,
}

internal enum CleanupFailureKind
{
    Io,
    Access,
    InvalidState,
    Native,
    Timeout,
    Unexpected,
}

internal readonly record struct CleanupFailureFact(
    OwnedCleanupStage Stage,
    CleanupFailureKind Kind);

internal sealed class CleanupOutcome
{
    internal CleanupOutcome(IReadOnlyList<CleanupFailureFact> failures)
    {
        Failures = failures;
    }

    internal IReadOnlyList<CleanupFailureFact> Failures { get; }

    internal bool Succeeded => Failures.Count == 0;
}

internal readonly record struct OwnedCleanupAction(
    OwnedCleanupStage Stage,
    Func<ValueTask> ExecuteAsync);

internal sealed class BoundedCleanupCoordinator
{
    private readonly OwnedCleanupAction[] _actions;
    private readonly object _sync = new();
    private Task<CleanupOutcome>? _execution;

    internal BoundedCleanupCoordinator(params OwnedCleanupAction[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions.ToArray();
    }

    internal Task<CleanupOutcome> ExecuteAsync()
    {
        lock (_sync)
        {
            return _execution ??= ExecuteCoreAsync();
        }
    }

    private async Task<CleanupOutcome> ExecuteCoreAsync()
    {
        if (_actions.Length == 0)
        {
            return new CleanupOutcome([]);
        }

        await _actions[0].ExecuteAsync().ConfigureAwait(false);
        return new CleanupOutcome([]);
    }
}
