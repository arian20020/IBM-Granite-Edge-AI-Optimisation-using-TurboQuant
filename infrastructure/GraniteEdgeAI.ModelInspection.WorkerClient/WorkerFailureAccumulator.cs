namespace GraniteEdgeAI.ModelInspection.WorkerClient;

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
}
