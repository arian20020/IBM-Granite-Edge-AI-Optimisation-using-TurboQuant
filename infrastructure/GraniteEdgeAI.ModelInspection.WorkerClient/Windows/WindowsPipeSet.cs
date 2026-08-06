using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns the six endpoints of three anonymous pipes. Only the child stdin-read,
/// stdout-write and stderr-write endpoints remain inheritable.
/// </summary>
internal sealed class WindowsPipeSet : IDisposable
{
    private bool _disposed;

    private WindowsPipeSet(
        SafeFileHandle childStandardInputRead,
        SafeFileHandle parentStandardInputWrite,
        SafeFileHandle parentStandardOutputRead,
        SafeFileHandle childStandardOutputWrite,
        SafeFileHandle parentStandardErrorRead,
        SafeFileHandle childStandardErrorWrite)
    {
        ChildStandardInputRead = childStandardInputRead;
        ParentStandardInputWrite = parentStandardInputWrite;
        ParentStandardOutputRead = parentStandardOutputRead;
        ChildStandardOutputWrite = childStandardOutputWrite;
        ParentStandardErrorRead = parentStandardErrorRead;
        ChildStandardErrorWrite = childStandardErrorWrite;
    }

    internal SafeFileHandle ChildStandardInputRead { get; }

    internal SafeFileHandle ParentStandardInputWrite { get; }

    internal SafeFileHandle ParentStandardOutputRead { get; }

    internal SafeFileHandle ChildStandardOutputWrite { get; }

    internal SafeFileHandle ParentStandardErrorRead { get; }

    internal SafeFileHandle ChildStandardErrorWrite { get; }

    internal static WindowsPipeSet Create()
    {
        SafeFileHandle? childInputRead = null;
        SafeFileHandle? parentInputWrite = null;
        SafeFileHandle? parentOutputRead = null;
        SafeFileHandle? childOutputWrite = null;
        SafeFileHandle? parentErrorRead = null;
        SafeFileHandle? childErrorWrite = null;

        try
        {
            (childInputRead, parentInputWrite) = CreatePipePair();
            ClearInheritance(parentInputWrite);

            (parentOutputRead, childOutputWrite) = CreatePipePair();
            ClearInheritance(parentOutputRead);

            (parentErrorRead, childErrorWrite) = CreatePipePair();
            ClearInheritance(parentErrorRead);

            return new WindowsPipeSet(
                childInputRead,
                parentInputWrite,
                parentOutputRead,
                childOutputWrite,
                parentErrorRead,
                childErrorWrite);
        }
        catch
        {
            childInputRead?.Dispose();
            parentInputWrite?.Dispose();
            parentOutputRead?.Dispose();
            childOutputWrite?.Dispose();
            parentErrorRead?.Dispose();
            childErrorWrite?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns borrowed handle values for the exact creation-time inheritance
    /// allowlist. Ownership remains with this pipe set.
    /// </summary>
    internal IReadOnlyList<IntPtr> GetChildHandleAllowlist()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Array.AsReadOnly(
        [
            ChildStandardInputRead.DangerousGetHandle(),
            ChildStandardOutputWrite.DangerousGetHandle(),
            ChildStandardErrorWrite.DangerousGetHandle()
        ]);
    }

    /// <summary>
    /// Closes the parent copies of child endpoints immediately after process
    /// creation so EOF can propagate correctly.
    /// </summary>
    internal void CloseChildEndpoints()
    {
        ChildStandardInputRead.Dispose();
        ChildStandardOutputWrite.Dispose();
        ChildStandardErrorWrite.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ParentStandardInputWrite.Dispose();
        ParentStandardOutputRead.Dispose();
        ParentStandardErrorRead.Dispose();
        CloseChildEndpoints();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static (SafeFileHandle Read, SafeFileHandle Write) CreatePipePair()
    {
        SecurityAttributes attributes = SecurityAttributes.CreateInheritable();
        if (!NativeMethods.CreatePipe(
                out SafeFileHandle read,
                out SafeFileHandle write,
                ref attributes,
                0))
        {
            int error = Marshal.GetLastPInvokeError();
            read?.Dispose();
            write?.Dispose();
            throw new Win32Exception(error, "Windows could not create a worker pipe.");
        }

        return (read, write);
    }

    private static void ClearInheritance(SafeFileHandle handle)
    {
        if (!NativeMethods.SetHandleInformation(
                handle,
                NativeConstants.HandleFlagInherit,
                0))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not restrict a parent pipe handle.");
        }
    }
}
