namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Carries the already-verified executable, its derived working directory, the
/// allowlisted environment, and fixture-only arguments into the one reviewed
/// Windows process-creation path.
/// </summary>
internal sealed record WindowsProcessLaunchRequest
{
    internal WindowsProcessLaunchRequest(
        VerifiedWorkerExecutable executable,
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyList<string> testOnlyArguments,
        TimeSpan cleanupTimeout)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(testOnlyArguments);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            cleanupTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            cleanupTimeout,
            TimeSpan.FromSeconds(5));

        string canonicalWorkingDirectory;
        try
        {
            string? executableDirectory = Path.GetDirectoryName(
                executable.ExecutableFinalPath);
            if (string.IsNullOrWhiteSpace(executableDirectory) ||
                !Path.IsPathFullyQualified(executable.ApprovedRootFinalPath) ||
                !Path.IsPathFullyQualified(executable.ExecutableFinalPath))
            {
                throw UntrustedWorkingDirectory();
            }

            string canonicalRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(executable.ApprovedRootFinalPath));
            canonicalWorkingDirectory = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(executableDirectory));
            if (!Directory.Exists(canonicalWorkingDirectory) ||
                !IsSameOrDescendant(canonicalRoot, canonicalWorkingDirectory))
            {
                throw UntrustedWorkingDirectory();
            }
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (
            error is ArgumentException or
            IOException or
            NotSupportedException)
        {
            throw UntrustedWorkingDirectory();
        }

        foreach (string argument in testOnlyArguments)
        {
            if (argument is null || argument.Contains('\0'))
            {
                throw WorkerClientPolicyException.For(
                    WorkerClientFailureCodes.WorkerLaunchFailed,
                    "The Model Inspection worker command line is invalid.");
            }
        }

        Executable = executable;
        WorkingDirectory = canonicalWorkingDirectory;
        Environment = environment;
        TestOnlyArguments = Array.AsReadOnly([.. testOnlyArguments]);
        CleanupTimeout = cleanupTimeout;
    }

    internal VerifiedWorkerExecutable Executable { get; }

    internal string WorkingDirectory { get; }

    internal IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>
    /// Exists only so the isolated process fixture can select deterministic
    /// abnormal behaviour. Production composition always supplies an empty list.
    /// </summary>
    internal IReadOnlyList<string> TestOnlyArguments { get; }

    internal TimeSpan CleanupTimeout { get; }

    private static bool IsSameOrDescendant(string root, string candidate)
    {
        if (string.Equals(
                root,
                candidate,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidate.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static WorkerClientPolicyException UntrustedWorkingDirectory() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerContainmentFailed,
            "The Model Inspection worker working directory is not trusted.");
}
