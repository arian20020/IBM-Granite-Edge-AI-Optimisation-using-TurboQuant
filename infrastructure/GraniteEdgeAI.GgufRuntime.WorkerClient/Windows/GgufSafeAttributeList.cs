using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal sealed class GgufSafeAttributeList : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly List<(IntPtr Pointer, int Length)> _values = [];
    private bool _initialized;

    private GgufSafeAttributeList(IntPtr pointer)
        : base(ownsHandle: true)
    {
        SetHandle(pointer);
    }

    internal static GgufSafeAttributeList Create(int attributeCount)
    {
        nuint size = 0;
        _ = GgufNativeMethods.InitializeProcThreadAttributeList(
            IntPtr.Zero,
            attributeCount,
            0,
            ref size);
        int firstError = Marshal.GetLastPInvokeError();
        if (size == 0 || firstError != GgufNativeConstants.ErrorInsufficientBuffer)
        {
            throw new Win32Exception(firstError, "The process attribute list size is unavailable.");
        }

        IntPtr pointer = Marshal.AllocHGlobal(checked((int)size));
        var list = new GgufSafeAttributeList(pointer);
        if (!GgufNativeMethods.InitializeProcThreadAttributeList(
                pointer,
                attributeCount,
                0,
                ref size))
        {
            int error = Marshal.GetLastPInvokeError();
            list.Dispose();
            throw new Win32Exception(error, "The process attribute list could not be initialized.");
        }

        list._initialized = true;
        return list;
    }

    internal void AddHandleList(nuint attribute, IReadOnlyList<IntPtr> handles)
    {
        int length = checked(handles.Count * IntPtr.Size);
        IntPtr values = Marshal.AllocHGlobal(length);
        bool retained = false;
        try
        {
            for (int index = 0; index < handles.Count; index++)
            {
                Marshal.WriteIntPtr(values, checked(index * IntPtr.Size), handles[index]);
            }

            if (!GgufNativeMethods.UpdateProcThreadAttribute(
                    DangerousGetHandle(),
                    0,
                    attribute,
                    values,
                    checked((nuint)length),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "The process attribute could not be applied.");
            }

            _values.Add((values, length));
            retained = true;
        }
        finally
        {
            if (!retained)
            {
                Marshal.FreeHGlobal(values);
            }
        }
    }

    protected override bool ReleaseHandle()
    {
        if (_initialized)
        {
            GgufNativeMethods.DeleteProcThreadAttributeList(handle);
            _initialized = false;
        }

        foreach ((IntPtr pointer, _) in _values)
        {
            Marshal.FreeHGlobal(pointer);
        }

        _values.Clear();
        Marshal.FreeHGlobal(handle);
        return true;
    }
}
