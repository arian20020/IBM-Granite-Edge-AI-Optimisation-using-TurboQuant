using GraniteEdgeAI.HardwareInspection.Foundation.Validation;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;

internal sealed class DxgiAdapterApi : IDxgiAdapterApi
{
    private const uint MaximumAdapters = 64;
    private const uint RemoteFlag = 0x1;
    private const uint SoftwareFlag = 0x2;

    private readonly IDxgiInterop _interop;

    internal DxgiAdapterApi(IDxgiInterop interop)
    {
        _interop = interop ?? throw new ArgumentNullException(nameof(interop));
    }

    public DxgiAdapterApiResult Capture()
    {
        DxgiFactoryCreateStatus factoryStatus = _interop.TryCreateFactory(
            out IDxgiFactoryHandle? factory);
        if (factoryStatus != DxgiFactoryCreateStatus.Success || factory is null)
        {
            factory?.Dispose();
            return Failure(DxgiAdapterApiStatus.FactoryUnavailable);
        }

        using (factory)
        {
            List<DxgiAdapterApiEntry> adapters = new(capacity: checked((int)MaximumAdapters));
            for (uint index = 0; ; index++)
            {
                DxgiAdapterEnumerationStatus enumeration = factory.TryGetAdapter(
                    index,
                    out IDxgiAdapterHandle? adapter);
                if (enumeration == DxgiAdapterEnumerationStatus.NotFound)
                {
                    adapter?.Dispose();
                    return Success(adapters);
                }

                if (enumeration != DxgiAdapterEnumerationStatus.Found || adapter is null)
                {
                    adapter?.Dispose();
                    return Failure(DxgiAdapterApiStatus.EnumerationFailed);
                }

                using (adapter)
                {
                    if (index >= MaximumAdapters)
                    {
                        return Failure(DxgiAdapterApiStatus.AdapterLimitExceeded);
                    }

                    if (!adapter.TryGetDescription(out DxgiNativeAdapterDescription description))
                    {
                        return Failure(DxgiAdapterApiStatus.InvalidDescription);
                    }

                    string name = NormalizeDescription(description.Name);
                    if (!HardwareText.IsSafe(name, 256))
                    {
                        return Failure(DxgiAdapterApiStatus.InvalidDescription);
                    }

                    adapters.Add(new DxgiAdapterApiEntry(
                        name,
                        MapKind(description.Flags),
                        description.VendorId,
                        description.DeviceId,
                        checked((ulong)description.DedicatedVideoMemory),
                        checked((ulong)description.DedicatedSystemMemory),
                        checked((ulong)description.SharedSystemMemory)));
                }
            }
        }
    }

    private static string NormalizeDescription(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        int terminator = value.IndexOf('\0', StringComparison.Ordinal);
        string terminated = terminator >= 0 ? value[..terminator] : value;
        return terminated.Trim();
    }

    private static DxgiAdapterApiKind MapKind(uint flags)
    {
        if ((flags & SoftwareFlag) != 0)
        {
            return DxgiAdapterApiKind.Software;
        }

        return (flags & RemoteFlag) != 0
            ? DxgiAdapterApiKind.Remote
            : DxgiAdapterApiKind.Hardware;
    }

    private static DxgiAdapterApiResult Success(IEnumerable<DxgiAdapterApiEntry> adapters) =>
        new(DxgiAdapterApiStatus.Success, Array.AsReadOnly(adapters.ToArray()));

    private static DxgiAdapterApiResult Failure(DxgiAdapterApiStatus status) =>
        new(status, Array.AsReadOnly(Array.Empty<DxgiAdapterApiEntry>()));
}
