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
    private readonly IReadOnlyDictionary<string, OpenVinoWorkerFileIdentity> _identities;
    private bool _disposed;

    internal VerifiedOpenVinoWorkerClosure(
        VerifiedWorkerExecutable executable,
        IReadOnlyList<SafeFileHandle> handles,
        IReadOnlyDictionary<string, OpenVinoWorkerFileIdentity> identities)
    {
        Executable = executable;
        _handles = handles;
        _identities = identities;
    }

    internal VerifiedWorkerExecutable Executable { get; }

    internal IReadOnlyDictionary<string, OpenVinoWorkerFileIdentity> Identities =>
        _identities;

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

internal readonly record struct OpenVinoWorkerFileIdentity(
    uint VolumeSerialNumber,
    ulong FileIndex);
