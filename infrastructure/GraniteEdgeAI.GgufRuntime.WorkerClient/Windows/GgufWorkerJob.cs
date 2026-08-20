using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal sealed class GgufWorkerJob : IDisposable
{
    private readonly SafeFileHandle _handle;

    private GgufWorkerJob(SafeFileHandle handle)
    {
        _handle = handle;
    }

    internal IntPtr Handle => _handle.DangerousGetHandle();

    internal uint ActiveProcessCount
    {
        get
        {
            if (!GgufNativeMethods.QueryInformationJobObject(
                    _handle,
                    GgufNativeConstants.JobObjectBasicAccountingInformationClass,
                    out GgufJobObjectBasicAccountingInformation information,
                    checked((uint)Marshal.SizeOf<GgufJobObjectBasicAccountingInformation>()),
                    IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "The GGUF worker Job could not be queried.");
            }

            return information.ActiveProcesses;
        }
    }

    internal static GgufWorkerJob Create()
    {
        SafeFileHandle handle = GgufNativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(error, "The GGUF worker Job could not be created.");
        }

        var information = new GgufJobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new GgufJobObjectBasicLimitInformation
            {
                LimitFlags = GgufNativeConstants.JobObjectLimitKillOnJobClose,
            },
        };
        if (!GgufNativeMethods.SetInformationJobObject(
                handle,
                GgufNativeConstants.JobObjectExtendedLimitInformationClass,
                ref information,
                checked((uint)Marshal.SizeOf<GgufJobObjectExtendedLimitInformation>())))
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new Win32Exception(error, "The GGUF worker Job policy could not be applied.");
        }

        return new GgufWorkerJob(handle);
    }

    internal IReadOnlyList<int> GetProcessIds()
    {
        const int capacity = 64;
        int byteCount = checked((sizeof(uint) * 2) + (capacity * IntPtr.Size));
        IntPtr buffer = Marshal.AllocHGlobal(byteCount);
        try
        {
            if (!GgufNativeMethods.QueryInformationJobObject(
                    _handle,
                    GgufNativeConstants.JobObjectBasicProcessIdListClass,
                    buffer,
                    checked((uint)byteCount),
                    IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "The GGUF worker Job process list could not be queried.");
            }

            int count = Marshal.ReadInt32(buffer, sizeof(uint));
            var result = new int[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = checked((int)Marshal.ReadIntPtr(
                    buffer,
                    checked((sizeof(uint) * 2) + (index * IntPtr.Size))).ToInt64());
            }

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal void Terminate()
    {
        if (!GgufNativeMethods.TerminateJobObject(_handle, 1))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "The GGUF worker Job could not be terminated.");
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}
