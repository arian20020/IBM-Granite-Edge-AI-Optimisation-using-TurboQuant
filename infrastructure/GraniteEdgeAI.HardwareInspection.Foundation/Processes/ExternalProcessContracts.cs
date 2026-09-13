namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

public enum ExternalProcessTerminationReason
{
    Exited,
    TimedOut,
    Cancelled,
    OutputLimitExceeded,
    StartFailed,
    CleanupFailed,
}

public sealed record ExternalProcessRequest
{
    public ExternalProcessRequest(
        string commandIdentity,
        TimeSpan timeout,
        int standardOutputByteLimit,
        int standardErrorByteLimit)
    {
        if (string.IsNullOrWhiteSpace(commandIdentity))
        {
            throw new ArgumentException("Command identity is required.", nameof(commandIdentity));
        }

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (standardOutputByteLimit is <= 0 or > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(standardOutputByteLimit));
        }

        if (standardErrorByteLimit is <= 0 or > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(standardErrorByteLimit));
        }

        CommandIdentity = commandIdentity;
        Timeout = timeout;
        StandardOutputByteLimit = standardOutputByteLimit;
        StandardErrorByteLimit = standardErrorByteLimit;
    }

    public string CommandIdentity { get; }

    public TimeSpan Timeout { get; }

    public int StandardOutputByteLimit { get; }

    public int StandardErrorByteLimit { get; }
}

public sealed record ExternalProcessResult(
    ExternalProcessTerminationReason TerminationReason,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);
