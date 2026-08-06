using System.Collections.ObjectModel;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Defines the complete stable public taxonomy for application-side worker
/// infrastructure failures.
/// </summary>
public static class WorkerClientFailureCodes
{
    public const string WorkerExecutableUntrusted =
        "worker_executable_untrusted";
    public const string WorkerArchitectureUnsupported =
        "worker_architecture_unsupported";
    public const string WorkerLaunchFailed = "worker_launch_failed";
    public const string WorkerContainmentFailed = "worker_containment_failed";
    public const string WorkerHandlePolicyFailed = "worker_handle_policy_failed";
    public const string WorkerEnvironmentPolicyFailed =
        "worker_environment_policy_failed";
    public const string WorkerHandshakeTimeout = "worker_handshake_timeout";
    public const string WorkerHandshakeInvalid = "worker_handshake_invalid";
    public const string WorkerProtocolInvalid = "worker_protocol_invalid";
    public const string WorkerOutputLimitExceeded =
        "worker_output_limit_exceeded";
    public const string WorkerCrashed = "worker_crashed";
    public const string WorkerOverallTimeout = "worker_overall_timeout";
    public const string WorkerCancellationForced = "worker_cancellation_forced";
    public const string WorkerExitMismatch = "worker_exit_mismatch";
    public const string WorkerProcessTreeIntegrityFailed =
        "worker_process_tree_integrity_failed";
    public const string WorkerCleanupFailed = "worker_cleanup_failed";

    private static readonly ReadOnlyCollection<string> KnownCodes =
        Array.AsReadOnly<string>(
        [
            WorkerExecutableUntrusted,
            WorkerArchitectureUnsupported,
            WorkerLaunchFailed,
            WorkerContainmentFailed,
            WorkerHandlePolicyFailed,
            WorkerEnvironmentPolicyFailed,
            WorkerHandshakeTimeout,
            WorkerHandshakeInvalid,
            WorkerProtocolInvalid,
            WorkerOutputLimitExceeded,
            WorkerCrashed,
            WorkerOverallTimeout,
            WorkerCancellationForced,
            WorkerExitMismatch,
            WorkerProcessTreeIntegrityFailed,
            WorkerCleanupFailed
        ]);

    /// <summary>
    /// Gets the immutable ordered failure-code catalogue used by validation,
    /// evidence, and tests.
    /// </summary>
    public static IReadOnlyList<string> All => KnownCodes;

    /// <summary>
    /// Determines whether a code belongs to the approved public taxonomy.
    /// </summary>
    internal static bool IsKnown(string? code)
    {
        return code is not null &&
            KnownCodes.Contains(code, StringComparer.Ordinal);
    }
}
