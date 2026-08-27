using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>Contains the complete bounded policy for one OpenVINO worker.</summary>
public sealed record OpenVinoWorkerClientOptions(
    OpenVinoWorkerInstallation Installation,
    TimeSpan StartupTimeout,
    TimeSpan TurnTimeout,
    TimeSpan IdleTimeout,
    TimeSpan SessionTimeout,
    TimeSpan CancellationGrace,
    TimeSpan CleanupTimeout,
    int MaximumRetainedStandardErrorBytes,
    int MaximumLineBytes)
{
    public static OpenVinoWorkerClientOptions CreateDefault(
        OpenVinoWorkerInstallation installation)
    {
        OpenVinoWorkerClientOptions options = new(
            installation,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(60),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            256 * 1024,
            OpenVinoProtocol.MaximumLineBytes);
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Installation);
        Installation.Validate();
        RequirePositive(StartupTimeout, nameof(StartupTimeout));
        RequirePositive(TurnTimeout, nameof(TurnTimeout));
        RequirePositive(IdleTimeout, nameof(IdleTimeout));
        RequirePositive(SessionTimeout, nameof(SessionTimeout));
        RequirePositive(CancellationGrace, nameof(CancellationGrace));
        RequirePositive(CleanupTimeout, nameof(CleanupTimeout));
        RequireAtMost(StartupTimeout, TimeSpan.FromSeconds(5), nameof(StartupTimeout));
        RequireAtMost(TurnTimeout, TimeSpan.FromMinutes(10), nameof(TurnTimeout));
        RequireAtMost(IdleTimeout, TimeSpan.FromMinutes(5), nameof(IdleTimeout));
        RequireAtMost(SessionTimeout, TimeSpan.FromMinutes(60), nameof(SessionTimeout));
        RequireAtMost(CancellationGrace, TimeSpan.FromSeconds(5), nameof(CancellationGrace));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            CleanupTimeout,
            TimeSpan.FromSeconds(5));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            MaximumRetainedStandardErrorBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            MaximumRetainedStandardErrorBytes,
            256 * 1024);
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            MaximumLineBytes,
            OpenVinoProtocol.MaximumLineBytes);
    }

    private static void RequirePositive(TimeSpan value, string name) =>
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            value,
            TimeSpan.Zero,
            name);

    private static void RequireAtMost(
        TimeSpan value,
        TimeSpan maximum,
        string name) =>
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, maximum, name);
}
