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

internal enum CleanupPrimaryFailureKind
{
    None,
    Cancellation,
    Io,
    Access,
    InvalidState,
    Native,
    Timeout,
    Policy,
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

internal sealed class CleanupIntegrityException : InvalidOperationException
{
    internal CleanupIntegrityException(
        IReadOnlyList<CleanupFailureFact> failures,
        Exception? primaryFailure)
        : base("Cleanup integrity could not be verified.")
    {
        Failures = failures.Take(16).ToArray();
        PrimaryFailureKind = ClassifyPrimary(primaryFailure);
    }

    internal IReadOnlyList<CleanupFailureFact> Failures { get; }

    internal CleanupPrimaryFailureKind PrimaryFailureKind { get; }

    internal static Exception PreserveCancellation(
        IReadOnlyList<CleanupFailureFact> failures,
        Exception? primaryFailure)
    {
        var integrity = new CleanupIntegrityException(failures, primaryFailure);
        return primaryFailure is OperationCanceledException cancellation
            ? new OperationCanceledException(
                "The operation was cancelled and cleanup integrity failed.",
                integrity,
                cancellation.CancellationToken)
            : integrity;
    }

    private static CleanupPrimaryFailureKind ClassifyPrimary(Exception? error) =>
        error switch
        {
            null => CleanupPrimaryFailureKind.None,
            OperationCanceledException => CleanupPrimaryFailureKind.Cancellation,
            IOException => CleanupPrimaryFailureKind.Io,
            UnauthorizedAccessException => CleanupPrimaryFailureKind.Access,
            TimeoutException => CleanupPrimaryFailureKind.Timeout,
            System.ComponentModel.Win32Exception => CleanupPrimaryFailureKind.Native,
            InvalidOperationException or ObjectDisposedException or ArgumentException =>
                CleanupPrimaryFailureKind.InvalidState,
            _ when error.GetType().Name.EndsWith(
                "PolicyException",
                StringComparison.Ordinal) => CleanupPrimaryFailureKind.Policy,
            _ => CleanupPrimaryFailureKind.Unexpected,
        };
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
