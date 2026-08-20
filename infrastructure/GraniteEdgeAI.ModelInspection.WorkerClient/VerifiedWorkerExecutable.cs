using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Owns the verified worker path and the open file handle that keeps the
/// executable identity available until the process-launch operation completes.
/// </summary>
public sealed class VerifiedWorkerExecutable : IDisposable
{
    private bool _disposed;

    internal VerifiedWorkerExecutable(
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

    internal SafeFileHandle VerificationHandle { get; }

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
