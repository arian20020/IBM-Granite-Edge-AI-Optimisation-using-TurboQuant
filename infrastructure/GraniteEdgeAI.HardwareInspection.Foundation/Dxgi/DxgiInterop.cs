using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;

internal enum DxgiFactoryCreateStatus
{
    Success,
    Unavailable,
}

internal enum DxgiAdapterEnumerationStatus
{
    Found,
    NotFound,
    Failed,
}

internal readonly record struct DxgiNativeAdapterDescription(
    string? Name,
    uint VendorId,
    uint DeviceId,
    nuint DedicatedVideoMemory,
    nuint DedicatedSystemMemory,
    nuint SharedSystemMemory,
    uint Flags);

internal interface IDxgiInterop
{
    DxgiFactoryCreateStatus TryCreateFactory(out IDxgiFactoryHandle? factory);
}

internal interface IDxgiFactoryHandle : IDisposable
{
    DxgiAdapterEnumerationStatus TryGetAdapter(uint index, out IDxgiAdapterHandle? adapter);
}

internal interface IDxgiAdapterHandle : IDisposable
{
    bool TryGetDescription(out DxgiNativeAdapterDescription description);
}

internal sealed class DxgiInterop : IDxgiInterop
{
    private static readonly Guid FactoryInterfaceId = typeof(IDxgiFactory1).GUID;

    public DxgiFactoryCreateStatus TryCreateFactory(out IDxgiFactoryHandle? factory)
    {
        factory = null;
        IDxgiFactory1? nativeFactory = null;
        try
        {
            Guid interfaceId = FactoryInterfaceId;
            int result = CreateDXGIFactory1(ref interfaceId, out nativeFactory);
            if (result < 0 || nativeFactory is null)
            {
                ReleaseComObject(nativeFactory);
                return DxgiFactoryCreateStatus.Unavailable;
            }

            factory = new DxgiFactoryHandle(nativeFactory);
            return DxgiFactoryCreateStatus.Success;
        }
        catch (Exception exception) when (exception is
            DllNotFoundException or
            EntryPointNotFoundException or
            BadImageFormatException or
            COMException)
        {
            ReleaseComObject(nativeFactory);
            return DxgiFactoryCreateStatus.Unavailable;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _ = Marshal.ReleaseComObject(value);
        }
    }

    [DllImport("dxgi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CreateDXGIFactory1(
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IDxgiFactory1? factory);

    private sealed class DxgiFactoryHandle(IDxgiFactory1 factory) : IDxgiFactoryHandle
    {
        private IDxgiFactory1? _factory = factory;

        public DxgiAdapterEnumerationStatus TryGetAdapter(
            uint index,
            out IDxgiAdapterHandle? adapter)
        {
            adapter = null;
            IDxgiFactory1? current = _factory;
            if (current is null)
            {
                return DxgiAdapterEnumerationStatus.Failed;
            }

            IDXGIAdapter1? nativeAdapter = null;
            int result = current.EnumAdapters1(index, out nativeAdapter);
            if (result == DxgiErrorNotFound)
            {
                ReleaseComObject(nativeAdapter);
                return DxgiAdapterEnumerationStatus.NotFound;
            }

            if (result < 0 || nativeAdapter is null)
            {
                ReleaseComObject(nativeAdapter);
                return DxgiAdapterEnumerationStatus.Failed;
            }

            adapter = new DxgiAdapterHandle(nativeAdapter);
            return DxgiAdapterEnumerationStatus.Found;
        }

        public void Dispose()
        {
            IDxgiFactory1? current = Interlocked.Exchange(ref _factory, null);
            ReleaseComObject(current);
        }
    }

    private sealed class DxgiAdapterHandle(IDXGIAdapter1 adapter) : IDxgiAdapterHandle
    {
        private IDXGIAdapter1? _adapter = adapter;

        public bool TryGetDescription(out DxgiNativeAdapterDescription description)
        {
            description = default;
            IDXGIAdapter1? current = _adapter;
            if (current is null || current.GetDesc1(out DxgiAdapterDescription1 native) < 0)
            {
                return false;
            }

            description = new(
                native.Description,
                native.VendorId,
                native.DeviceId,
                native.DedicatedVideoMemory,
                native.DedicatedSystemMemory,
                native.SharedSystemMemory,
                native.Flags);
            return true;
        }

        public void Dispose()
        {
            IDXGIAdapter1? current = Interlocked.Exchange(ref _adapter, null);
            ReleaseComObject(current);
        }
    }

    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDescription1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string? Description;
        internal uint VendorId;
        internal uint DeviceId;
        internal uint SubSystemId;
        internal uint Revision;
        internal UIntPtr DedicatedVideoMemory;
        internal UIntPtr DedicatedSystemMemory;
        internal UIntPtr SharedSystemMemory;
        internal Luid AdapterLuid;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Luid(uint LowPart, int HighPart);

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        [PreserveSig]
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object? unknown);

        [PreserveSig]
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

        [PreserveSig]
        int GetParent(ref Guid interfaceId, out IntPtr parent);

        [PreserveSig]
        int EnumOutputs(uint output, out IntPtr outputInterface);

        [PreserveSig]
        int GetDesc(IntPtr description);

        [PreserveSig]
        int CheckInterfaceSupport(ref Guid interfaceName, out long userModeDriverVersion);

        [PreserveSig]
        int GetDesc1(out DxgiAdapterDescription1 description);
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDxgiFactory1
    {
        [PreserveSig]
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object? unknown);

        [PreserveSig]
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

        [PreserveSig]
        int GetParent(ref Guid interfaceId, out IntPtr parent);

        [PreserveSig]
        int EnumAdapters(uint adapter, out IntPtr adapterInterface);

        [PreserveSig]
        int MakeWindowAssociation(IntPtr windowHandle, uint flags);

        [PreserveSig]
        int GetWindowAssociation(out IntPtr windowHandle);

        [PreserveSig]
        int CreateSwapChain(IntPtr device, IntPtr description, out IntPtr swapChain);

        [PreserveSig]
        int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);

        [PreserveSig]
        int EnumAdapters1(
            uint adapter,
            [MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter1? adapterInterface);

        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsCurrent();
    }
}
