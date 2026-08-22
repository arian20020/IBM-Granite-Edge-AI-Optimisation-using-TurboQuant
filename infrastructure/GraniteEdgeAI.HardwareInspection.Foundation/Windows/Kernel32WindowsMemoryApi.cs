using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal sealed class Kernel32WindowsMemoryApi : IWindowsMemoryApi
{
    public bool TryGetPhysicallyInstalledKilobytes(out ulong value) =>
        GetPhysicallyInstalledSystemMemory(out value);

    public bool TryGetMemoryStatus(out ulong totalPhysicalBytes, out ulong availablePhysicalBytes)
    {
        MemoryStatusEx status = new()
        {
            Length = checked((uint)Marshal.SizeOf<MemoryStatusEx>()),
        };
        bool succeeded = GlobalMemoryStatusEx(ref status);
        totalPhysicalBytes = status.TotalPhysical;
        availablePhysicalBytes = status.AvailablePhysical;
        return succeeded;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        internal uint Length;
        internal uint MemoryLoad;
        internal ulong TotalPhysical;
        internal ulong AvailablePhysical;
        internal ulong TotalPageFile;
        internal ulong AvailablePageFile;
        internal ulong TotalVirtual;
        internal ulong AvailableVirtual;
        internal ulong AvailableExtendedVirtual;
    }
}
