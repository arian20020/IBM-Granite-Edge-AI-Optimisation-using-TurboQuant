using System.Runtime.InteropServices;
using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.Worker;

/// <summary>
/// Opens the parent once with minimal query/synchronization rights, verifies
/// its creation time, retains that exact handle, and waits for it to signal.
/// </summary>
internal sealed class ParentProcessMonitor : IParentProcessMonitor
{
    private const uint Synchronize = 0x00100000;
    private const uint ProcessQueryLimitedInformation = 0x00001000;

    public async Task MonitorAsync(
        WorkerStartInspectionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        using SafeParentProcessHandle parent = OpenProcess(
            Synchronize | ProcessQueryLimitedInformation,
            inheritHandle: false,
            checked((uint)command.ParentProcessId));
        if (parent.IsInvalid)
        {
            throw new WorkerProtocolException(
                "Parent process identity could not be verified.");
        }

        if (!GetProcessTimes(
                parent,
                out FileTime creation,
                out _,
                out _,
                out _))
        {
            throw new WorkerProtocolException(
                "Parent process creation time could not be verified.");
        }

        DateTimeOffset actualStart = DateTimeOffset.FromFileTime(
            creation.ToInt64());
        if (actualStart.UtcDateTime.Ticks !=
            command.ParentProcessStartTimeUtc.UtcDateTime.Ticks)
        {
            throw new WorkerProtocolException(
                "Parent process identity does not match the start command.");
        }

        using ParentWaitHandle waitHandle = new(
            parent.DangerousGetHandle());
        await WaitForSignalAsync(waitHandle, cancellationToken)
            .ConfigureAwait(false);
        GC.KeepAlive(parent);
    }

    private static async Task WaitForSignalAsync(
        WaitHandle waitHandle,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RegisteredWaitHandle? registered = null;
        using CancellationTokenRegistration cancellation =
            cancellationToken.Register(
                static state => ((TaskCompletionSource)state!).TrySetCanceled(),
                completion);
        registered = ThreadPool.RegisterWaitForSingleObject(
            waitHandle,
            static (state, _) =>
                ((TaskCompletionSource)state!).TrySetResult(),
            completion,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: true);

        try
        {
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            registered.Unregister(waitObject: null);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        private readonly uint _low;
        private readonly uint _high;

        internal long ToInt64() =>
            unchecked(((long)_high << 32) | _low);
    }

    private sealed class ParentWaitHandle : WaitHandle
    {
        internal ParentWaitHandle(IntPtr borrowedHandle)
        {
            SafeWaitHandle = new SafeWaitHandle(
                borrowedHandle,
                ownsHandle: false);
        }
    }

    private sealed class SafeParentProcessHandle :
        SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeParentProcessHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern SafeParentProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        SafeParentProcessHandle process,
        out FileTime creationTime,
        out FileTime exitTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
