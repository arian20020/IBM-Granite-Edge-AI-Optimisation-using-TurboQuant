namespace GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;

/// <summary>
/// Carries only route-neutral launch policy and fixed, non-sensitive process
/// selectors. Request paths, prompts, and protocol payloads must cross stdin
/// after the protected process has started.
/// </summary>
public sealed record ProtectedWorkerLaunchSpec(
    VerifiedWorkerExecutable Executable,
    IReadOnlyList<string> FixedArguments,
    IReadOnlyDictionary<string, string> Environment,
    int MaximumStandardErrorBytes,
    TimeSpan StartupTimeout,
    TimeSpan CancellationGrace,
    TimeSpan CleanupTimeout);
