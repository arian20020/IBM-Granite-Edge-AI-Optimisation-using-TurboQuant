using System.Runtime.InteropServices;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal sealed class GgufWindowsEnvironmentBlock : IDisposable
{
    private GgufWindowsEnvironmentBlock(IntPtr pointer)
    {
        Pointer = pointer;
    }

    internal IntPtr Pointer { get; private set; }

    internal static GgufWindowsEnvironmentBlock Create(
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        string[] entries = environment
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={pair.Value}")
            .ToArray();
        string block = string.Join('\0', entries) + "\0\0";
        return new GgufWindowsEnvironmentBlock(Marshal.StringToHGlobalUni(block));
    }

    public void Dispose()
    {
        if (Pointer == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(Pointer);
        Pointer = IntPtr.Zero;
    }
}
