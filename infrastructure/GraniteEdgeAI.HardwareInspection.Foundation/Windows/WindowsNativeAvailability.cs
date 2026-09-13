using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Windows;

internal static class WindowsNativeAvailability
{
    internal static bool IsExpected(Exception exception) => exception is
        DllNotFoundException or
        EntryPointNotFoundException or
        BadImageFormatException or
        PlatformNotSupportedException or
        MarshalDirectiveException;
}
