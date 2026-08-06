using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns one unnamed per-session Job Object configured with
/// JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE and no breakaway flags.
/// </summary>
internal sealed class WindowsJobObject : IDisposable
{
    private bool _disposed;

    private WindowsJobObject(SafeJobHandle handle)
    {
        Handle = handle;
    }

    internal SafeJobHandle Handle { get; }

    internal static WindowsJobObject CreateKillOnClose()
    {
        SafeJobHandle handle = NativeMethods.CreateJobObject(
            IntPtr.Zero,
            name: null);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(
                error,
                "Windows could not create the worker Job Object.");
        }

        JobObjectExtendedLimitInformation limits = new()
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = NativeConstants.JobObjectLimitKillOnJobClose
            }
        };

        if (!NativeMethods.SetInformationJobObject(
                handle,
                NativeConstants.JobObjectExtendedLimitInformationClass,
                ref limits,
                checked((uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>())))
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(
                error,
                "Windows could not configure worker process containment.");
        }

        return new WindowsJobObject(handle);
    }

    internal JobObjectExtendedLimitInformation QueryExtendedLimits()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!NativeMethods.QueryInformationJobObject(
                Handle,
                NativeConstants.JobObjectExtendedLimitInformationClass,
                out JobObjectExtendedLimitInformation limits,
                checked((uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()),
                IntPtr.Zero))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not query worker containment limits.");
        }

        return limits;
    }

    internal JobObjectBasicAccountingInformation QueryBasicAccounting()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!NativeMethods.QueryInformationJobObject(
                Handle,
                NativeConstants.JobObjectBasicAccountingInformationClass,
                out JobObjectBasicAccountingInformation accounting,
                checked((uint)Marshal.SizeOf<JobObjectBasicAccountingInformation>()),
                IntPtr.Zero))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not query the worker process tree.");
        }

        return accounting;
    }

    internal void Terminate(uint exitCode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!NativeMethods.TerminateJobObject(Handle, exitCode))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not terminate the worker process tree.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Handle.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
