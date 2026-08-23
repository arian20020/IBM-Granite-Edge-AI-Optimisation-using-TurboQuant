using System.Collections.ObjectModel;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal interface ILlamaCppNativeCapabilityApi
{
    LlamaCppNativeCapabilityResult Capture();
}

internal sealed record LlamaCppNativeDevice(int Ordinal, string BufferType);

internal sealed class LlamaCppNativeCapabilityResult
{
    private static readonly ReadOnlyCollection<LlamaCppNativeDevice> NoDevices =
        Array.AsReadOnly(Array.Empty<LlamaCppNativeDevice>());

    private LlamaCppNativeCapabilityResult(
        bool isAvailable,
        ReadOnlyCollection<LlamaCppNativeDevice> devices)
    {
        IsAvailable = isAvailable;
        Devices = devices;
    }

    internal bool IsAvailable { get; }
    internal IReadOnlyList<LlamaCppNativeDevice> Devices { get; }

    internal static LlamaCppNativeCapabilityResult Available(IEnumerable<LlamaCppNativeDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        LlamaCppNativeDevice[] copy = devices.ToArray();
        if (copy.Length is < 1 or > 16 ||
            copy.Where(static (device, index) =>
                device.Ordinal != index ||
                !LlamaCppProbeText.IsSafe(device.BufferType, 128)).Any())
        {
            throw new ArgumentException("Native devices violate the closed capability contract.", nameof(devices));
        }

        return new(true, Array.AsReadOnly(copy));
    }

    internal static LlamaCppNativeCapabilityResult Unavailable() => new(false, NoDevices);
}
