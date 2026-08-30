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
    private const int MaximumRetainedFailures = 16;
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
        List<CleanupFailureFact>? failures = null;
        foreach (OwnedCleanupAction action in _actions)
        {
            try
            {
                await action.ExecuteAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                failures ??= new List<CleanupFailureFact>(MaximumRetainedFailures);
                if (failures.Count < MaximumRetainedFailures)
                {
                    failures.Add(new CleanupFailureFact(
                        action.Stage,
                        Classify(error)));
                }
            }
        }

        return new CleanupOutcome(failures?.ToArray() ?? []);
    }

    private static CleanupFailureKind Classify(Exception error) => error switch
    {
        IOException => CleanupFailureKind.Io,
        UnauthorizedAccessException => CleanupFailureKind.Access,
        TimeoutException => CleanupFailureKind.Timeout,
        System.ComponentModel.Win32Exception => CleanupFailureKind.Native,
        InvalidOperationException or ObjectDisposedException or ArgumentException =>
            CleanupFailureKind.InvalidState,
        _ => CleanupFailureKind.Unexpected,
    };
}
