using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Owns the verified worker path and the open file handle that keeps the
/// executable identity available until the process-launch operation completes.
/// </summary>
internal sealed class VerifiedWorkerExecutable : IDisposable
{
    private bool _disposed;

    public VerifiedWorkerExecutable(
        string approvedRootFinalPath,
        string executableFinalPath,
        SafeFileHandle verificationHandle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedRootFinalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executableFinalPath);
        ArgumentNullException.ThrowIfNull(verificationHandle);

        ApprovedRootFinalPath = approvedRootFinalPath;
        ExecutableFinalPath = executableFinalPath;
        VerificationHandle = verificationHandle;
    }

    public string ApprovedRootFinalPath { get; }

    public string ExecutableFinalPath { get; }

    public SafeFileHandle VerificationHandle { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        VerificationHandle.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
