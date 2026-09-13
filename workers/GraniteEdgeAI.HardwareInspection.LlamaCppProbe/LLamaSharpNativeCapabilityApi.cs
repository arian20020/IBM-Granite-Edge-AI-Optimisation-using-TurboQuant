using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal sealed class LLamaSharpNativeCapabilityApi : ILlamaCppNativeCapabilityApi
{
    private readonly ILlamaCppNativeInterop interop;

    internal LLamaSharpNativeCapabilityApi(ILlamaCppNativeInterop interop) =>
        this.interop = interop ?? throw new ArgumentNullException(nameof(interop));

    public LlamaCppNativeCapabilityResult Capture()
    {
        try
        {
            interop.InitializeBackend();
        }
        catch (Exception exception) when (IsNativeBoundaryException(exception))
        {
            return LlamaCppNativeCapabilityResult.Unavailable();
        }

        LlamaCppNativeCapabilityResult result = LlamaCppNativeCapabilityResult.Unavailable();
        bool cleanupFailed = false;
        try
        {
            nuint count = interop.GetDeviceCount();
            if (count is < 1 or > 16)
            {
                return result;
            }

            var devices = new List<LlamaCppNativeDevice>(checked((int)count));
            for (nuint ordinal = 0; ordinal < count; ordinal++)
            {
                nint device = interop.GetDevice(ordinal);
                if (device == 0)
                {
                    return result;
                }

                nint bufferType = interop.GetDeviceBufferType(device);
                if (bufferType == 0)
                {
                    return result;
                }

                string? label = interop.GetBufferTypeName(bufferType);
                if (!LlamaCppProbeText.IsSafe(label, 128))
                {
                    return result;
                }

                devices.Add(new LlamaCppNativeDevice(checked((int)ordinal), label!));
            }

            result = LlamaCppNativeCapabilityResult.Available(devices);
        }
        catch (Exception exception) when (IsNativeBoundaryException(exception))
        {
            result = LlamaCppNativeCapabilityResult.Unavailable();
        }
        finally
        {
            try
            {
                interop.FreeBackend();
            }
            catch (Exception exception) when (IsNativeBoundaryException(exception))
            {
                cleanupFailed = true;
            }
        }

        return cleanupFailed ? LlamaCppNativeCapabilityResult.Unavailable() : result;
    }

    private static bool IsNativeBoundaryException(Exception exception) => exception is
        BadImageFormatException or
        DllNotFoundException or
        EntryPointNotFoundException or
        ExternalException or
        InvalidOperationException or
        TypeInitializationException;
}
