namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Carries the already-verified executable, controlled working directory,
/// allowlisted environment, and fixture-only arguments into the one reviewed
/// Windows process-creation path.
/// </summary>
internal sealed record WindowsProcessLaunchRequest
{
    internal WindowsProcessLaunchRequest(
        VerifiedWorkerExecutable executable,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyList<string> testOnlyArguments)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(testOnlyArguments);

        string canonicalWorkingDirectory = Path.GetFullPath(workingDirectory);
        if (!Path.IsPathFullyQualified(canonicalWorkingDirectory) ||
            !Directory.Exists(canonicalWorkingDirectory) ||
            !string.Equals(
                Path.TrimEndingDirectorySeparator(canonicalWorkingDirectory),
                Path.TrimEndingDirectorySeparator(executable.ApprovedRootFinalPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw WorkerClientPolicyException.For(
                WorkerClientFailureCodes.WorkerContainmentFailed,
                "The Model Inspection worker working directory is not trusted.");
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
    }

    internal VerifiedWorkerExecutable Executable { get; }

    internal string WorkingDirectory { get; }

    internal IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>
    /// Exists only so the isolated process fixture can select deterministic
    /// abnormal behaviour. Production composition always supplies an empty list.
    /// </summary>
    internal IReadOnlyList<string> TestOnlyArguments { get; }
}
