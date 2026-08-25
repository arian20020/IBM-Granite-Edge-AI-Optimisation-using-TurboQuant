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
        List<LlamaCppNativeDevice> copy = new(16);
        foreach (LlamaCppNativeDevice device in devices)
        {
            if (copy.Count == 16 ||
                device is null ||
                device.Ordinal != copy.Count ||
                !LlamaCppProbeText.IsSafe(device.BufferType, 128))
            {
                throw new ArgumentException("Native devices violate the closed capability contract.", nameof(devices));
            }

            copy.Add(device);
        }

        if (copy.Count == 0)
        {
            throw new ArgumentException("Native devices violate the closed capability contract.", nameof(devices));
        }

        return new(true, Array.AsReadOnly(copy.ToArray()));
    }

    internal static LlamaCppNativeCapabilityResult Unavailable() => new(false, NoDevices);
}
