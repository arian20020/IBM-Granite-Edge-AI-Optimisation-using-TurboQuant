using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns one initialized PROC_THREAD_ATTRIBUTE_LIST allocation. Release deletes
/// the Windows attribute list before zeroing and freeing its backing memory.
/// </summary>
internal sealed class SafeAttributeListBuffer :
    SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly int _byteCount;

    private SafeAttributeListBuffer(IntPtr buffer, int byteCount)
        : base(ownsHandle: true)
    {
        _byteCount = byteCount;
        SetHandle(buffer);
    }

    internal static SafeAttributeListBuffer Create(int attributeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attributeCount, 0);

        nuint requiredSize = 0;
        bool firstCallSucceeded =
            NativeMethods.InitializeProcThreadAttributeList(
                IntPtr.Zero,
                attributeCount,
                0,
                ref requiredSize);
        int firstError = Marshal.GetLastPInvokeError();
        if (firstCallSucceeded ||
            firstError != NativeConstants.ErrorInsufficientBuffer ||
            requiredSize == 0)
        {
            throw new Win32Exception(
                firstError,
                "Windows did not report a valid attribute-list size.");
        }

        int byteCount = checked((int)requiredSize);
        IntPtr buffer = Marshal.AllocHGlobal(byteCount);
        SafeAttributeListBuffer owner = new(buffer, byteCount);
        if (!NativeMethods.InitializeProcThreadAttributeList(
                buffer,
                attributeCount,
                0,
                ref requiredSize))
        {
            int error = Marshal.GetLastPInvokeError();
            owner.Dispose();
            throw new Win32Exception(
                error,
                "Windows could not initialize the process attribute list.");
        }

        return owner;
    }

    /// <summary>
    /// Copies one or more borrowed handles into temporary unmanaged memory and
    /// adds them to the initialized attribute list. The temporary copy is
    /// always zeroed and freed before returning.
    /// </summary>
    internal void UpdatePointerList(nuint attribute, IReadOnlyList<IntPtr> values)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one handle is required for a process attribute.",
                nameof(values));
        }

        int byteCount = checked(values.Count * IntPtr.Size);
        IntPtr valueBuffer = Marshal.AllocHGlobal(byteCount);
        try
        {
            for (int index = 0; index < values.Count; index++)
            {
                Marshal.WriteIntPtr(
                    valueBuffer,
                    checked(index * IntPtr.Size),
                    values[index]);
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                    DangerousGetHandle(),
                    0,
                    attribute,
                    valueBuffer,
                    checked((nuint)byteCount),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Windows could not apply the process attribute.");
            }
        }
        finally
        {
            for (int index = 0; index < byteCount; index++)
            {
                Marshal.WriteByte(valueBuffer, index, 0);
            }

            Marshal.FreeHGlobal(valueBuffer);
        }
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.DeleteProcThreadAttributeList(handle);
        for (int index = 0; index < _byteCount; index++)
        {
            Marshal.WriteByte(handle, index, 0);
        }

        Marshal.FreeHGlobal(handle);
        return true;
    }
}
