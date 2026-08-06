using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Collects the trusted installation root and bounded time or memory policies
/// applied to one WorkerClient execution.
/// </summary>
public sealed record WorkerClientOptions(
    string ApprovedWorkerRoot,
    TimeSpan StartupTimeout,
    TimeSpan OverallTimeout,
    TimeSpan CancellationGracePeriod,
    int MaximumRetainedStandardErrorBytes,
    TimeSpan ProcessTreeCleanupTimeout)
{
    /// <summary>
    /// Creates production defaults directly from the shared worker protocol so
    /// the client and worker cannot drift to different limits.
    /// </summary>
    /// <param name="approvedWorkerRoot">
    /// The controlled absolute root that later executable resolution must use.
    /// </param>
    /// <returns>A validated set of production WorkerClient policies.</returns>
    public static WorkerClientOptions CreateDefault(string approvedWorkerRoot)
    {
        WorkerClientOptions options = new(
            approvedWorkerRoot,
            WorkerProtocol.StartupTimeout,
            WorkerProtocol.OverallTimeout,
            WorkerProtocol.CancellationGracePeriod,
            WorkerProtocol.MaximumRetainedStandardErrorBytes,
            TimeSpan.FromSeconds(5));

        options.Validate();
        return options;
    }

    /// <summary>
    /// Rejects policies that would disable a safety boundary or leave worker
    /// resolution without a controlled root.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ApprovedWorkerRoot);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            StartupTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            OverallTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            CancellationGracePeriod,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            MaximumRetainedStandardErrorBytes,
            0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            ProcessTreeCleanupTimeout,
            TimeSpan.Zero);
    }
}
