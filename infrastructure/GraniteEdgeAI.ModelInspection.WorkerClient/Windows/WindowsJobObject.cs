using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns one unnamed per-session Job Object configured with
/// JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE and no breakaway flags.
/// </summary>
internal sealed class WindowsJobObject : IDisposable
{
    private const int ProcessIdCapacity = 64;
    private const int ProcessIdListHeaderBytes = sizeof(uint) * 2;
    private static readonly TimeSpan EmptyPollInterval =
        TimeSpan.FromMilliseconds(25);
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

    internal uint GetActiveProcessCount() =>
        QueryBasicAccounting().ActiveProcesses;

    /// <summary>
    /// Waits for authoritative Job Object accounting to report no active
    /// process. Root-process exit alone is not accepted as tree cleanup.
    /// </summary>
    internal async Task<bool> WaitUntilEmptyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            timeout,
            TimeSpan.Zero);
        long startedAt = Stopwatch.GetTimestamp();

        while (GetActiveProcessCount() != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
            if (elapsed >= timeout)
            {
                return false;
            }

            TimeSpan remaining = timeout - elapsed;
            TimeSpan delay = remaining < EmptyPollInterval
                ? remaining
                : EmptyPollInterval;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Returns the process IDs currently assigned to this Job Object. This is
    /// used by process-tree verification and diagnostic tests rather than as a
    /// substitute for the authoritative active-process accounting value.
    /// </summary>
    internal IReadOnlyList<int> GetProcessIds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int byteCount = checked(
            ProcessIdListHeaderBytes + (ProcessIdCapacity * IntPtr.Size));
        IntPtr buffer = Marshal.AllocHGlobal(byteCount);
        try
        {
            for (int index = 0; index < byteCount; index++)
            {
                Marshal.WriteByte(buffer, index, 0);
            }

            if (!NativeMethods.QueryInformationJobObject(
                    Handle,
                    NativeConstants.JobObjectBasicProcessIdListClass,
                    buffer,
                    checked((uint)byteCount),
                    IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Windows could not enumerate the worker process tree.");
            }

            uint processCount = checked((uint)Marshal.ReadInt32(
                buffer,
                sizeof(uint)));
            if (processCount > ProcessIdCapacity)
            {
                throw new InvalidOperationException(
                    "The worker process tree exceeded its diagnostic capacity.");
            }

            List<int> processIds = new(checked((int)processCount));
            for (int index = 0; index < processCount; index++)
            {
                IntPtr processId = Marshal.ReadIntPtr(
                    buffer,
                    checked(ProcessIdListHeaderBytes + (index * IntPtr.Size)));
                processIds.Add(checked((int)processId.ToInt64()));
            }

            return processIds.AsReadOnly();
        }
        finally
        {
            for (int index = 0; index < byteCount; index++)
            {
                Marshal.WriteByte(buffer, index, 0);
            }

            Marshal.FreeHGlobal(buffer);
        }
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
