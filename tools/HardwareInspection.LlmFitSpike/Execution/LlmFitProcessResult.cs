namespace HardwareInspection.LlmFitSpike.Execution;

public sealed record LlmFitProcessResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int? ProcessId,
    int? ExitCode,
    bool ProcessStartFailed,
    bool ObserverFailed,
    bool TimedOut,
    bool Cancelled,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated)
{
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;

    public bool Succeeded =>
        !ProcessStartFailed &&
        !ObserverFailed &&
        !TimedOut &&
        !Cancelled &&
        ExitCode == 0;
}
