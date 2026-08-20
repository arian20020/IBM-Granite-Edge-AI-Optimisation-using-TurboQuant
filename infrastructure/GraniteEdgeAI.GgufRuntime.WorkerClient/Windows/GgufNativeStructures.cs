using System.Runtime.InteropServices;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

[StructLayout(LayoutKind.Sequential)]
internal struct GgufSecurityAttributes
{
    internal uint Size;
    internal IntPtr SecurityDescriptor;

    [MarshalAs(UnmanagedType.Bool)]
    internal bool InheritHandle;

    internal static GgufSecurityAttributes CreateInheritable() => new()
    {
        Size = checked((uint)Marshal.SizeOf<GgufSecurityAttributes>()),
        InheritHandle = true,
    };
}

[StructLayout(LayoutKind.Sequential)]
internal struct GgufStartupInfo
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

[StructLayout(LayoutKind.Sequential)]
internal struct GgufStartupInfoEx
{
    internal GgufStartupInfo StartupInfo;
    internal IntPtr AttributeList;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GgufProcessInformation
{
    internal IntPtr ProcessHandle;
    internal IntPtr ThreadHandle;
    internal uint ProcessId;
    internal uint ThreadId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GgufJobObjectBasicLimitInformation
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

[StructLayout(LayoutKind.Sequential)]
internal struct GgufIoCounters
{
    internal ulong ReadOperationCount;
    internal ulong WriteOperationCount;
    internal ulong OtherOperationCount;
    internal ulong ReadTransferCount;
    internal ulong WriteTransferCount;
    internal ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GgufJobObjectExtendedLimitInformation
{
    internal GgufJobObjectBasicLimitInformation BasicLimitInformation;
    internal GgufIoCounters IoInformation;
    internal UIntPtr ProcessMemoryLimit;
    internal UIntPtr JobMemoryLimit;
    internal UIntPtr PeakProcessMemoryUsed;
    internal UIntPtr PeakJobMemoryUsed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GgufJobObjectBasicAccountingInformation
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
