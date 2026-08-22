using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal enum ProcessorBufferQueryStatus
{
    Success,
    BufferRequired,
    Failed,
}

internal interface IKernel32ProcessorNative
{
    ushort GetNativeProcessorArchitecture();

    uint GetActiveProcessorCount(ushort groupNumber);

    ProcessorBufferQueryStatus QueryProcessorCoreInformation(IntPtr buffer, ref uint length);
}

internal sealed class Kernel32WindowsProcessorApi : IWindowsProcessorApi
{
    private const uint MaximumTopologyBytes = 1024 * 1024;
    private const ushort AllProcessorGroups = ushort.MaxValue;

    private readonly IProcessorNameSource _nameSource;
    private readonly IKernel32ProcessorNative _native;

    internal Kernel32WindowsProcessorApi()
        : this(new RegistryProcessorNameSource(), new Kernel32ProcessorNative())
    {
    }

    internal Kernel32WindowsProcessorApi(
        IProcessorNameSource nameSource,
        IKernel32ProcessorNative native)
    {
        _nameSource = nameSource ?? throw new ArgumentNullException(nameof(nameSource));
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public WindowsProcessorApiResult Capture()
    {
        try
        {
            return CaptureCore();
        }
        catch (Exception exception) when (WindowsNativeAvailability.IsExpected(exception))
        {
            return Failure(WindowsProcessorApiStatus.NativeApiUnavailable);
        }
    }

    private WindowsProcessorApiResult CaptureCore()
    {
        if (!_nameSource.TryGetName(out string? name))
        {
            return Failure(WindowsProcessorApiStatus.NameUnavailable);
        }

        WindowsProcessorApiArchitecture architecture =
            MapArchitecture(_native.GetNativeProcessorArchitecture());
        if (architecture == WindowsProcessorApiArchitecture.Unsupported)
        {
            return Failure(WindowsProcessorApiStatus.UnsupportedArchitecture);
        }

        uint requiredLength = 0;
        ProcessorBufferQueryStatus initial =
            _native.QueryProcessorCoreInformation(IntPtr.Zero, ref requiredLength);
        if (initial != ProcessorBufferQueryStatus.BufferRequired ||
            requiredLength == 0 ||
            requiredLength > MaximumTopologyBytes)
        {
            return Failure(WindowsProcessorApiStatus.TopologyUnavailable);
        }

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)requiredLength));
        try
        {
            uint returnedLength = requiredLength;
            ProcessorBufferQueryStatus completed =
                _native.QueryProcessorCoreInformation(buffer, ref returnedLength);
            if (completed != ProcessorBufferQueryStatus.Success ||
                returnedLength == 0 ||
                returnedLength > requiredLength)
            {
                return Failure(WindowsProcessorApiStatus.TopologyUnavailable);
            }

            byte[] topology = new byte[checked((int)returnedLength)];
            Marshal.Copy(buffer, topology, 0, checked((int)returnedLength));
            if (!WindowsProcessorTopologyParser.TryCountPhysicalCores(
                topology,
                out int physicalCoreCount))
            {
                return Failure(WindowsProcessorApiStatus.InvalidTopology);
            }

            uint logicalProcessorCount = _native.GetActiveProcessorCount(AllProcessorGroups);
            if (logicalProcessorCount is 0 or > 4096 || physicalCoreCount > logicalProcessorCount)
            {
                return Failure(WindowsProcessorApiStatus.InvalidTopology);
            }

            return new(
                WindowsProcessorApiStatus.Success,
                name,
                architecture,
                physicalCoreCount,
                checked((int)logicalProcessorCount));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static WindowsProcessorApiArchitecture MapArchitecture(ushort architecture) =>
        architecture switch
        {
            0 => WindowsProcessorApiArchitecture.X86,
            9 => WindowsProcessorApiArchitecture.X64,
            12 => WindowsProcessorApiArchitecture.Arm64,
            _ => WindowsProcessorApiArchitecture.Unsupported,
        };

    private static WindowsProcessorApiResult Failure(WindowsProcessorApiStatus status) =>
        new(status, null, WindowsProcessorApiArchitecture.Unsupported, 0, 0);

    private sealed class Kernel32ProcessorNative : IKernel32ProcessorNative
    {
        private const int ErrorInsufficientBuffer = 122;

        public ushort GetNativeProcessorArchitecture()
        {
            GetNativeSystemInfo(out SystemInfo information);
            return information.ProcessorArchitecture;
        }

        public uint GetActiveProcessorCount(ushort groupNumber) =>
            NativeGetActiveProcessorCount(groupNumber);

        public ProcessorBufferQueryStatus QueryProcessorCoreInformation(
            IntPtr buffer,
            ref uint length)
        {
            bool succeeded = GetLogicalProcessorInformationEx(
                LogicalProcessorRelationship.ProcessorCore,
                buffer,
                ref length);
            if (succeeded)
            {
                return ProcessorBufferQueryStatus.Success;
            }

            return buffer == IntPtr.Zero && Marshal.GetLastWin32Error() == ErrorInsufficientBuffer
                ? ProcessorBufferQueryStatus.BufferRequired
                : ProcessorBufferQueryStatus.Failed;
        }

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern void GetNativeSystemInfo(out SystemInfo systemInfo);

        [DllImport("kernel32.dll", EntryPoint = "GetActiveProcessorCount")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint NativeGetActiveProcessorCount(ushort groupNumber);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLogicalProcessorInformationEx(
            LogicalProcessorRelationship relationshipType,
            IntPtr buffer,
            ref uint returnedLength);
    }

    private enum LogicalProcessorRelationship
    {
        ProcessorCore = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemInfo
    {
        internal ushort ProcessorArchitecture;
        internal ushort Reserved;
        internal uint PageSize;
        internal IntPtr MinimumApplicationAddress;
        internal IntPtr MaximumApplicationAddress;
        internal IntPtr ActiveProcessorMask;
        internal uint NumberOfProcessors;
        internal uint ProcessorType;
        internal uint AllocationGranularity;
        internal ushort ProcessorLevel;
        internal ushort ProcessorRevision;
    }
}

internal static class WindowsProcessorTopologyParser
{
    private const int HeaderSize = 8;
    private const int MinimumProcessorCoreRecordSize = 48;
    private const int GroupCountOffset = 30;
    private const uint ProcessorRelationshipPrefixSize = 32;
    private const uint GroupAffinitySize = 16;
    private const ushort MaximumProcessorGroups = 64;
    private const int ProcessorCoreRelationship = 0;
    private const int MaximumPhysicalCores = 4096;

    internal static bool TryCountPhysicalCores(ReadOnlySpan<byte> buffer, out int coreCount)
    {
        coreCount = 0;
        int offset = 0;
        while (offset < buffer.Length)
        {
            int remaining = buffer.Length - offset;
            if (remaining < HeaderSize)
            {
                return false;
            }

            ReadOnlySpan<byte> header = buffer.Slice(offset, HeaderSize);
            int relationship = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
            uint recordSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            if (relationship != ProcessorCoreRelationship ||
                recordSize < MinimumProcessorCoreRecordSize ||
                recordSize > remaining)
            {
                return false;
            }

            ushort groupCount = BinaryPrimitives.ReadUInt16LittleEndian(
                buffer.Slice(offset + GroupCountOffset, sizeof(ushort)));
            uint requiredRecordSize = checked(
                ProcessorRelationshipPrefixSize + (groupCount * GroupAffinitySize));
            if (groupCount is 0 or > MaximumProcessorGroups || recordSize < requiredRecordSize)
            {
                return false;
            }

            coreCount++;
            if (coreCount > MaximumPhysicalCores)
            {
                coreCount = 0;
                return false;
            }

            offset += checked((int)recordSize);
        }

        return offset == buffer.Length && coreCount > 0;
    }
}
