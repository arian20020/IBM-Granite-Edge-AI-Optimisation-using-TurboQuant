using System.Runtime.InteropServices;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using ModelInspectionSafeProcessHandle =
    GraniteEdgeAI.ModelInspection.WorkerClient.Windows.SafeProcessHandle;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Owns one launched worker session. Standard streams and the process handle
/// are released before the private Job Object, so kill-on-close remains the
/// final containment guarantee throughout cleanup.
/// </summary>
internal sealed class WorkerProcessSession : IAsyncDisposable
{
    private readonly ModelInspectionSafeProcessHandle _processHandle;
    private readonly TimeSpan _cleanupTimeout;
    private readonly BoundedCleanupCoordinator _cleanup;
    private bool _disposed;

    internal WorkerProcessSession(
        uint processId,
        ModelInspectionSafeProcessHandle processHandle,
        WindowsJobObject job,
        FileStream standardInput,
        FileStream standardOutput,
        FileStream standardError,
        TimeSpan cleanupTimeout)
    {
        ArgumentNullException.ThrowIfNull(processHandle);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            cleanupTimeout,
            TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            cleanupTimeout,
            TimeSpan.FromSeconds(5));

        ProcessId = processId;
        _processHandle = processHandle;
        Job = job;
        StandardInput = standardInput;
        StandardOutput = standardOutput;
        StandardError = standardError;
        _cleanupTimeout = cleanupTimeout;
        _cleanup = new BoundedCleanupCoordinator(
            new OwnedCleanupAction(OwnedCleanupStage.StandardInput, () =>
                StandardInput.DisposeAsync()),
            new OwnedCleanupAction(OwnedCleanupStage.ProcessTree, async () =>
            {
                _ = await TerminateAndVerifyEmptyAsync().ConfigureAwait(false);
            }),
            new OwnedCleanupAction(OwnedCleanupStage.StandardOutput, () =>
                StandardOutput.DisposeAsync()),
            new OwnedCleanupAction(OwnedCleanupStage.StandardError, () =>
                StandardError.DisposeAsync()),
            Sync(OwnedCleanupStage.ProcessHandle, _processHandle.Dispose),
            Sync(OwnedCleanupStage.Job, Job.Dispose));
    }

    internal uint ProcessId { get; }

    internal WindowsJobObject Job { get; }

    internal Stream StandardInput { get; }

    internal Stream StandardOutput { get; }

    internal Stream StandardError { get; }

    internal Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return WindowsProcessWaiter.WaitForExitAsync(
            _processHandle,
            cancellationToken);
    }

    internal int GetExitCode()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!NativeMethods.GetExitCodeProcess(
                _processHandle,
                out uint exitCode))
        {
            _ = Marshal.GetLastPInvokeError();
            throw WorkerClientPolicyException.For(
                WorkerClientFailureCodes.WorkerExitMismatch,
                "The Model Inspection worker exit code could not be verified.");
        }

        return checked((int)exitCode);
    }

    /// <summary>
    /// Terminates every active process assigned to this session's Job Object and
    /// then verifies authoritative active-process accounting reaches zero.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when TerminateJobObject was required; otherwise
    /// <see langword="false"/> because the Job Object was already empty.
    /// </returns>
    internal async Task<bool> TerminateAndVerifyEmptyAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool terminationRequired = Job.GetActiveProcessCount() != 0;
        if (terminationRequired)
        {
            Job.Terminate(exitCode: 1);
        }

        // Root exit and complete tree cleanup are independent facts. Wait for
        // both, then trust Job Object accounting as the tree-empty authority.
        try
        {
            await WaitForExitAsync(CancellationToken.None)
                .WaitAsync(_cleanupTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw WorkerClientPolicyException.For(
                WorkerClientFailureCodes.WorkerCleanupFailed,
                "The Model Inspection worker root process did not exit during cleanup.");
        }

        if (!await Job.WaitUntilEmptyAsync(
                _cleanupTimeout,
                CancellationToken.None)
            .ConfigureAwait(false))
        {
            throw WorkerClientPolicyException.For(
                WorkerClientFailureCodes.WorkerCleanupFailed,
                "The Model Inspection worker process tree did not become empty during cleanup.");
        }

        return terminationRequired;
    }

    /// <summary>
    /// Waits for the Job Object to become empty without forcing termination.
    /// </summary>
    internal Task<bool> WaitForTreeEmptyAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Job.WaitUntilEmptyAsync(_cleanupTimeout, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        CleanupOutcome outcome = await _cleanup.ExecuteAsync().ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);

        if (!outcome.Succeeded)
        {
            throw new WorkerClientPolicyException(
                new WorkerClientFailure(
                    WorkerClientFailureCodes.WorkerCleanupFailed,
                    "The Model Inspection worker process tree could not be fully verified during cleanup."));
        }
    }

    private static OwnedCleanupAction Sync(
        OwnedCleanupStage stage,
        Action action) => new(stage, () =>
        {
            action();
            return ValueTask.CompletedTask;
        });

    private static bool IsExpectedCleanupError(Exception error) =>
        error is IOException or
        UnauthorizedAccessException or
        ObjectDisposedException or
        InvalidOperationException or
        System.ComponentModel.Win32Exception or
        TimeoutException;
}
