using System.Runtime.InteropServices;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal static class PackageActivator
{
    internal static int Activate(string aumid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aumid);
        object managerObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"), throwOnError: true)!)!;
        IApplicationActivationManager manager = (IApplicationActivationManager)managerObject;
        try
        {
            int result = manager.ActivateApplication(aumid, null, ActivateOptions.None, out uint processId);
            Marshal.ThrowExceptionForHR(result);
            return checked((int)processId);
        }
        finally
        {
            Marshal.FinalReleaseComObject(manager);
        }
    }

    [Flags]
    private enum ActivateOptions : uint
    {
        None = 0,
    }

    [ComImport]
    [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.LPWStr)] string? arguments, ActivateOptions options, out uint processId);

        [PreserveSig]
        int ActivateForFile(IntPtr appUserModelId, IntPtr itemArray, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);

        [PreserveSig]
        int ActivateForProtocol(IntPtr appUserModelId, IntPtr itemArray, out uint processId);
    }
}
