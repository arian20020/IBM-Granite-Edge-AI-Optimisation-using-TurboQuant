namespace GraniteEdgeAI.ModelInspection.WorkerClient;

using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

/// <summary>
/// Preserves the earliest authoritative WorkerClient failure and records a
/// small bounded list of later exception types without retaining messages,
/// paths, identifiers, or environment values.
/// </summary>
internal sealed class WorkerFailureAccumulator
{
    private const int MaximumSecondaryDiagnostics = 8;
    private readonly object _sync = new();
    private readonly List<string> _secondaryDiagnostics = [];
    private WorkerClientFailure? _primaryFailure;
    private WorkerClientPolicyException? _cleanupIntegrityCause;
    private WorkerClientCleanupFailureFact[] _cleanupFailures = [];

    internal WorkerClientFailure? PrimaryFailure
    {
        get
        {
            lock (_sync)
            {
                return _primaryFailure;
            }
        }
    }

    internal IReadOnlyList<string> SecondaryDiagnostics
    {
        get
        {
            lock (_sync)
            {
                // Return an owned snapshot so later cleanup races cannot mutate
                // a result that has already crossed the client boundary.
                return Array.AsReadOnly(_secondaryDiagnostics.ToArray());
            }
        }
    }

    internal WorkerClientPolicyException? CleanupIntegrityCause
    {
        get
        {
            lock (_sync)
            {
                return _cleanupIntegrityCause;
            }
        }
    }

    internal IReadOnlyList<WorkerClientCleanupFailureFact> CleanupFailures
    {
        get
        {
            lock (_sync)
            {
                return Array.AsReadOnly(_cleanupFailures.ToArray());
            }
        }
    }

    internal bool TrySetPrimary(WorkerClientFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        failure.Validate();

        lock (_sync)
        {
            if (_primaryFailure is not null)
            {
                return false;
            }

            _primaryFailure = failure;
            return true;
        }
    }

    internal void AddSecondary(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        lock (_sync)
        {
            if (_secondaryDiagnostics.Count >= MaximumSecondaryDiagnostics)
            {
                return;
            }

            // Exception type is useful for engineering diagnosis but does not
            // carry the untrusted or sensitive text held in Exception.Message.
            _secondaryDiagnostics.Add(error.GetType().Name);
        }
    }

    internal void RetainCleanupIntegrity(WorkerClientPolicyException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Exception? current = error;
        for (int depth = 0; current is not null && depth < 8; depth++)
        {
            if (current is CleanupIntegrityException integrity)
            {
                lock (_sync)
                {
                    _cleanupIntegrityCause ??= error;
                    if (_cleanupFailures.Length == 0)
                    {
                        _cleanupFailures = integrity.Failures
                            .Select(MapCleanupFailure)
                            .ToArray();
                    }
                }

                return;
            }

            current = current.InnerException;
        }
    }

    private static WorkerClientCleanupFailureFact MapCleanupFailure(
        CleanupFailureFact fact) => new(
            fact.Stage switch
            {
                OwnedCleanupStage.StandardInput => WorkerClientCleanupStage.StandardInput,
                OwnedCleanupStage.StandardOutput => WorkerClientCleanupStage.StandardOutput,
                OwnedCleanupStage.StandardError => WorkerClientCleanupStage.StandardError,
                OwnedCleanupStage.Channel => WorkerClientCleanupStage.Channel,
                OwnedCleanupStage.Session => WorkerClientCleanupStage.Session,
                OwnedCleanupStage.Closure => WorkerClientCleanupStage.Closure,
                OwnedCleanupStage.ProcessTree => WorkerClientCleanupStage.ProcessTree,
                OwnedCleanupStage.ProcessHandle => WorkerClientCleanupStage.ProcessHandle,
                OwnedCleanupStage.Job => WorkerClientCleanupStage.Job,
                OwnedCleanupStage.OperationEnvironment =>
                    WorkerClientCleanupStage.OperationEnvironment,
                OwnedCleanupStage.PendingOutput => WorkerClientCleanupStage.PendingOutput,
                _ => throw new InvalidOperationException(
                    "The cleanup stage was not mapped."),
            },
            fact.Kind switch
            {
                CleanupFailureKind.Io => WorkerClientCleanupFailureKind.Io,
                CleanupFailureKind.Access => WorkerClientCleanupFailureKind.Access,
                CleanupFailureKind.InvalidState =>
                    WorkerClientCleanupFailureKind.InvalidState,
                CleanupFailureKind.Native => WorkerClientCleanupFailureKind.Native,
                CleanupFailureKind.Timeout => WorkerClientCleanupFailureKind.Timeout,
                CleanupFailureKind.Unexpected => WorkerClientCleanupFailureKind.Unexpected,
                _ => throw new InvalidOperationException(
                    "The cleanup failure kind was not mapped."),
            });
}
