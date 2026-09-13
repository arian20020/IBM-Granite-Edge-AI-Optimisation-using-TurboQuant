using System.Runtime.InteropServices;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Mirrors SECURITY_ATTRIBUTES for x64 Windows anonymous-pipe creation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SecurityAttributes
{
    internal uint Size;
    internal IntPtr SecurityDescriptor;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool InheritHandle;

    internal static SecurityAttributes CreateInheritable() => new()
    {
        Size = checked((uint)Marshal.SizeOf<SecurityAttributes>()),
        SecurityDescriptor = IntPtr.Zero,
        InheritHandle = true
    };
}

/// <summary>
/// Mirrors STARTUPINFOW. Handle fields are borrowed values; SafeHandle owners
/// remain in the surrounding session object.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StartupInfo
{
    internal uint Size;
    internal IntPtr Reserved;
    internal IntPtr Desktop;
    internal IntPtr Title;
    internal uint X;
    internal uint Y;
    internal uint XSize;
    internal uint YSize;
    internal uint XCountChars;
    internal uint YCountChars;
    internal uint FillAttribute;
    internal uint Flags;
    internal ushort ShowWindow;
    internal ushort ReservedByteCount;
    internal IntPtr ReservedBytes;
    internal IntPtr StandardInput;
    internal IntPtr StandardOutput;
    internal IntPtr StandardError;
}

/// <summary>
/// Mirrors STARTUPINFOEXW for creation-time attribute lists.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StartupInfoEx
{
    internal StartupInfo StartupInfo;
    internal IntPtr AttributeList;
}

/// <summary>
/// Mirrors PROCESS_INFORMATION. Task 6 immediately wraps the returned handles
/// in owning SafeHandle instances before any later operation can fail.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessInformation
{
    internal IntPtr ProcessHandle;
    internal IntPtr ThreadHandle;
    internal uint ProcessId;
    internal uint ThreadId;
}

/// <summary>
/// Mirrors JOBOBJECT_BASIC_LIMIT_INFORMATION.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectBasicLimitInformation
{
    internal long PerProcessUserTimeLimit;
    internal long PerJobUserTimeLimit;
    internal uint LimitFlags;
    internal UIntPtr MinimumWorkingSetSize;
    internal UIntPtr MaximumWorkingSetSize;
    internal uint ActiveProcessLimit;
    internal UIntPtr Affinity;
    internal uint PriorityClass;
    internal uint SchedulingClass;
}

/// <summary>
/// Mirrors IO_COUNTERS.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct IoCounters
{
    internal ulong ReadOperationCount;
    internal ulong WriteOperationCount;
    internal ulong OtherOperationCount;
    internal ulong ReadTransferCount;
    internal ulong WriteTransferCount;
    internal ulong OtherTransferCount;
}

/// <summary>
/// Mirrors JOBOBJECT_EXTENDED_LIMIT_INFORMATION.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectExtendedLimitInformation
{
    internal JobObjectBasicLimitInformation BasicLimitInformation;
    internal IoCounters IoInformation;
    internal UIntPtr ProcessMemoryLimit;
    internal UIntPtr JobMemoryLimit;
    internal UIntPtr PeakProcessMemoryUsed;
    internal UIntPtr PeakJobMemoryUsed;
}

/// <summary>
/// Mirrors JOBOBJECT_BASIC_ACCOUNTING_INFORMATION.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectBasicAccountingInformation
{
    internal long TotalUserTime;
    internal long TotalKernelTime;
    internal long ThisPeriodTotalUserTime;
    internal long ThisPeriodTotalKernelTime;
    internal uint TotalPageFaultCount;
    internal uint TotalProcesses;
    internal uint ActiveProcesses;
    internal uint TotalTerminatedProcesses;
}
