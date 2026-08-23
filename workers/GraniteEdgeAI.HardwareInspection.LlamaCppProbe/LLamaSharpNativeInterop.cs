using System.ComponentModel;
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
            ConfigureVerifiedDependencyDirectory(baseDirectory);
            NativeLibraryConfig.LLama.WithLibrary(Path.Combine(baseDirectory, "llama.dll"));
            NativeApi.llama_empty_call();
            LlamaBackendInit();
        });
    }

    public nuint GetDeviceCount() => Invoke(NativeApi.ggml_backend_dev_count);
    public nint GetDevice(nuint ordinal) => Invoke(() => NativeApi.ggml_backend_dev_get(ordinal));
    public nint GetDeviceBufferType(nint device) => Invoke(() => NativeApi.ggml_backend_dev_buffer_type(device));
    public string? GetBufferTypeName(nint bufferType) =>
        Invoke(() => Marshal.PtrToStringUTF8(NativeApi.ggml_backend_buft_name(bufferType)));
    public void FreeBackend() => Invoke(LlamaBackendFree);

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

    private static void ConfigureVerifiedDependencyDirectory(string directory)
    {
        const uint LoadLibrarySearchSystem32 = 0x00000800;
        const uint LoadLibrarySearchUserDirs = 0x00000400;
        if (!SetDefaultDllDirectories(
                LoadLibrarySearchSystem32 | LoadLibrarySearchUserDirs))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The native dependency search policy could not be restricted.");
        }

        if (AddDllDirectory(directory) == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The verified native dependency directory could not be registered.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint AddDllDirectory(string newDirectory);

    [DllImport("llama.dll", EntryPoint = "llama_backend_init", CallingConvention = CallingConvention.Cdecl)]
    private static extern void LlamaBackendInit();

    [DllImport("llama.dll", EntryPoint = "llama_backend_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void LlamaBackendFree();
}
