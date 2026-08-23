using System.Runtime.InteropServices;
using LLama.Exceptions;
using LLama.Native;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal sealed class LLamaSharpNativeInterop : ILlamaCppNativeInterop
{
    public void InitializeBackend()
    {
        Invoke(() =>
        {
            string baseDirectory = AppContext.BaseDirectory;
            NativeLibraryConfig.LLama.WithLibrary(Path.Combine(baseDirectory, "llama.dll"));
            NativeApi.llama_empty_call();
        });
    }

    public nuint GetDeviceCount() => Invoke(NativeApi.ggml_backend_dev_count);
    public nint GetDevice(nuint ordinal) => Invoke(() => NativeApi.ggml_backend_dev_get(ordinal));
    public nint GetDeviceBufferType(nint device) => Invoke(() => NativeApi.ggml_backend_dev_buffer_type(device));
    public string? GetBufferTypeName(nint bufferType) =>
        Invoke(() => Marshal.PtrToStringUTF8(NativeApi.ggml_backend_buft_name(bufferType)));
    public void FreeBackend() => Invoke(NativeApi.llama_backend_free);

    private static void Invoke(Action action)
    {
        try
        {
            action();
        }
        catch (RuntimeError exception)
        {
            throw new InvalidOperationException("The native llama.cpp boundary was unavailable.", exception);
        }
    }

    private static T Invoke<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (RuntimeError exception)
        {
            throw new InvalidOperationException("The native llama.cpp boundary was unavailable.", exception);
        }
    }
}
