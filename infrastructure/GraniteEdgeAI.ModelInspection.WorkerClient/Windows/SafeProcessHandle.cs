using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns or borrows one Windows process handle according to the constructor's
/// explicit ownership flag.
/// </summary>
internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeProcessHandle()
        : base(ownsHandle: true)
    {
    }

    internal SafeProcessHandle(IntPtr existingHandle, bool ownsHandle)
        : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    protected override bool ReleaseHandle() =>
        NativeMethods.CloseHandle(handle);
}
