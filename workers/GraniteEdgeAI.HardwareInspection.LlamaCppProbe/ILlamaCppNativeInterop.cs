namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal interface ILlamaCppNativeInterop
{
    void InitializeBackend();
    nuint GetDeviceCount();
    nint GetDevice(nuint ordinal);
    nint GetDeviceBufferType(nint device);
    string? GetBufferTypeName(nint bufferType);
    void FreeBackend();
}
