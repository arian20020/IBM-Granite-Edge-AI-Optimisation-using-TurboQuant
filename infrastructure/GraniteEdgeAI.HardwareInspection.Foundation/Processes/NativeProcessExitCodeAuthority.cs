using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

/// <summary>
/// Reads completion from the exact process handle returned by CreateProcess.
/// A PID-derived Process is useful for managed waiting, but is not durable exit
/// authority once a short-lived child has left the system process table.
/// </summary>
internal static class NativeProcessExitCodeAuthority
{
    private const uint StillActive = 259;

    internal static bool TryRead(
        SafeProcessHandle processHandle,
        out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(processHandle);
        if (processHandle.IsInvalid || processHandle.IsClosed)
        {
            exitCode = 0;
            return false;
        }

        bool querySucceeded = GetExitCodeProcess(
            processHandle,
            out uint nativeExitCode);
        return TryInterpret(querySucceeded, nativeExitCode, out exitCode);
    }

    internal static bool TryInterpret(
        bool querySucceeded,
        uint nativeExitCode,
        out int exitCode)
    {
        if (!querySucceeded || nativeExitCode == StillActive)
        {
            exitCode = 0;
            return false;
        }

        exitCode = unchecked((int)nativeExitCode);
        return true;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(
        SafeProcessHandle process,
        out uint exitCode);
}

internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeProcessHandle(IntPtr existingHandle, bool ownsHandle)
        : base(ownsHandle)
    {
        SetHandle(existingHandle);
    }

    protected override bool ReleaseHandle() => CloseHandle(handle);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
