using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Retains the independently verified worker closure until all dependent
/// native loading and process activity has ended.
/// </summary>
internal sealed class VerifiedOpenVinoWorkerClosure : IDisposable
{
    private readonly IReadOnlyList<SafeFileHandle> _handles;
    private bool _disposed;

    internal VerifiedOpenVinoWorkerClosure(
        VerifiedWorkerExecutable executable,
        IReadOnlyList<SafeFileHandle> handles)
    {
        Executable = executable;
        _handles = handles;
    }

    internal VerifiedWorkerExecutable Executable { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (int index = _handles.Count - 1; index >= 0; index--)
        {
            _handles[index].Dispose();
        }

        Executable.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
