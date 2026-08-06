using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Contains the reviewed Windows API surface required by the protected worker
/// boundary. Every library lookup is restricted to System32, and every method
/// that reports GetLastError captures it immediately through SetLastError.
/// </summary>
internal static class NativeMethods
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateJobObjectW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    internal static extern SafeJobHandle CreateJobObject(
        IntPtr jobAttributes,
        string? name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(
        SafeJobHandle job,
        int informationClass,
        ref JobObjectExtendedLimitInformation information,
        uint informationLength);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryInformationJobObject(
        SafeJobHandle job,
        int informationClass,
        out JobObjectExtendedLimitInformation information,
        uint informationLength,
        IntPtr returnLength);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryInformationJobObject(
        SafeJobHandle job,
        int informationClass,
        out JobObjectBasicAccountingInformation information,
        uint informationLength,
        IntPtr returnLength);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateJobObject(
        SafeJobHandle job,
        uint exitCode);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreatePipe(
        out SafeFileHandle readPipe,
        out SafeFileHandle writePipe,
        ref SecurityAttributes pipeAttributes,
        uint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetHandleInformation(
        SafeFileHandle handle,
        uint mask,
        uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetHandleInformation(
        SafeFileHandle handle,
        out uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadFile(
        SafeFileHandle file,
        [Out] byte[] buffer,
        uint bytesToRead,
        out uint bytesRead,
        IntPtr overlapped);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteFile(
        SafeFileHandle file,
        byte[] buffer,
        uint bytesToWrite,
        out uint bytesWritten,
        IntPtr overlapped);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref nuint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        nuint attribute,
        IntPtr value,
        nuint size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern void DeleteProcThreadAttributeList(
        IntPtr attributeList);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateProcessW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcess(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern uint WaitForSingleObject(
        SafeProcessHandle process,
        uint milliseconds);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetExitCodeProcess(
        SafeProcessHandle process,
        out uint exitCode);
}
