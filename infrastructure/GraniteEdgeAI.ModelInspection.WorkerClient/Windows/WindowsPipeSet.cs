using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns the six endpoints of three anonymous pipes. Only the child stdin-read,
/// stdout-write and stderr-write endpoints remain inheritable. Parent endpoints
/// can be transferred exactly once to a <see cref="WorkerProcessSession"/>.
/// </summary>
internal sealed class WindowsPipeSet : IDisposable
{
    private SafeFileHandle? _childStandardInputRead;
    private SafeFileHandle? _parentStandardInputWrite;
    private SafeFileHandle? _parentStandardOutputRead;
    private SafeFileHandle? _childStandardOutputWrite;
    private SafeFileHandle? _parentStandardErrorRead;
    private SafeFileHandle? _childStandardErrorWrite;
    private bool _disposed;

    private WindowsPipeSet(
        SafeFileHandle childStandardInputRead,
        SafeFileHandle parentStandardInputWrite,
        SafeFileHandle parentStandardOutputRead,
        SafeFileHandle childStandardOutputWrite,
        SafeFileHandle parentStandardErrorRead,
        SafeFileHandle childStandardErrorWrite)
    {
        _childStandardInputRead = childStandardInputRead;
        _parentStandardInputWrite = parentStandardInputWrite;
        _parentStandardOutputRead = parentStandardOutputRead;
        _childStandardOutputWrite = childStandardOutputWrite;
        _parentStandardErrorRead = parentStandardErrorRead;
        _childStandardErrorWrite = childStandardErrorWrite;
    }

    internal SafeFileHandle ChildStandardInputRead =>
        RequireOwned(_childStandardInputRead, nameof(ChildStandardInputRead));

    internal SafeFileHandle ParentStandardInputWrite =>
        RequireOwned(_parentStandardInputWrite, nameof(ParentStandardInputWrite));

    internal SafeFileHandle ParentStandardOutputRead =>
        RequireOwned(_parentStandardOutputRead, nameof(ParentStandardOutputRead));

    internal SafeFileHandle ChildStandardOutputWrite =>
        RequireOwned(_childStandardOutputWrite, nameof(ChildStandardOutputWrite));

    internal SafeFileHandle ParentStandardErrorRead =>
        RequireOwned(_parentStandardErrorRead, nameof(ParentStandardErrorRead));

    internal SafeFileHandle ChildStandardErrorWrite =>
        RequireOwned(_childStandardErrorWrite, nameof(ChildStandardErrorWrite));

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
    /// allowlist. Ownership remains with this pipe set until creation completes.
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
        DisposeAndClear(ref _childStandardInputRead);
        DisposeAndClear(ref _childStandardOutputWrite);
        DisposeAndClear(ref _childStandardErrorWrite);
    }

    internal SafeFileHandle TakeParentStandardInputWrite() =>
        TakeOwned(ref _parentStandardInputWrite, nameof(ParentStandardInputWrite));

    internal SafeFileHandle TakeParentStandardOutputRead() =>
        TakeOwned(ref _parentStandardOutputRead, nameof(ParentStandardOutputRead));

    internal SafeFileHandle TakeParentStandardErrorRead() =>
        TakeOwned(ref _parentStandardErrorRead, nameof(ParentStandardErrorRead));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeAndClear(ref _parentStandardInputWrite);
        DisposeAndClear(ref _parentStandardOutputRead);
        DisposeAndClear(ref _parentStandardErrorRead);
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

    private static SafeFileHandle RequireOwned(
        SafeFileHandle? handle,
        string endpointName)
    {
        return handle ?? throw new InvalidOperationException(
            $"Pipe endpoint {endpointName} is no longer owned by this pipe set.");
    }

    private static SafeFileHandle TakeOwned(
        ref SafeFileHandle? handle,
        string endpointName)
    {
        SafeFileHandle owned = RequireOwned(handle, endpointName);
        handle = null;
        return owned;
    }

    private static void DisposeAndClear(ref SafeFileHandle? handle)
    {
        handle?.Dispose();
        handle = null;
    }
}
