using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Owns one initialized PROC_THREAD_ATTRIBUTE_LIST allocation together with
/// every pointer-valued attribute buffer referenced by that list. Windows
/// requires those value buffers to remain alive until process creation has
/// consumed the list and the list is destroyed.
/// </summary>
internal sealed class SafeAttributeListBuffer :
    SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly int _byteCount;
    private readonly List<RetainedValueBuffer> _retainedValueBuffers = [];
    private bool _initialized;

    private SafeAttributeListBuffer(IntPtr buffer, int byteCount)
        : base(ownsHandle: true)
    {
        _byteCount = byteCount;
        SetHandle(buffer);
    }

    /// <summary>
    /// Exposes only the retained-buffer count to the focused test assembly. The
    /// unmanaged addresses remain private so tests cannot accidentally assume
    /// ownership or mutate process-creation metadata.
    /// </summary>
    internal int RetainedValueBufferCountForTests =>
        _retainedValueBuffers.Count;

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

        owner._initialized = true;
        return owner;
    }

    /// <summary>
    /// Copies one or more borrowed handles into unmanaged memory, attaches the
    /// pointer array to the initialized attribute list, and retains ownership of
    /// that array until this SafeHandle is released.
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

        // Reserve managed capacity before the native update. Once Windows has
        // stored the pointer, freeing that buffer before CreateProcessW would be
        // a use-after-free security defect.
        _retainedValueBuffers.EnsureCapacity(
            checked(_retainedValueBuffers.Count + 1));

        int byteCount = checked(values.Count * IntPtr.Size);
        IntPtr valueBuffer = Marshal.AllocHGlobal(byteCount);
        bool ownershipTransferred = false;
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

            _retainedValueBuffers.Add(
                new RetainedValueBuffer(valueBuffer, byteCount));
            ownershipTransferred = true;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                ZeroAndFree(valueBuffer, byteCount);
            }
        }
    }

    protected override bool ReleaseHandle()
    {
        // The attribute list can retain pointers to every value buffer, so the
        // list must be deleted before those buffers are zeroed and released.
        if (_initialized)
        {
            NativeMethods.DeleteProcThreadAttributeList(handle);
            _initialized = false;
        }

        for (int index = _retainedValueBuffers.Count - 1; index >= 0; index--)
        {
            RetainedValueBuffer retained = _retainedValueBuffers[index];
            ZeroAndFree(retained.Pointer, retained.ByteCount);
        }

        _retainedValueBuffers.Clear();
        ZeroAndFree(handle, _byteCount);
        return true;
    }

    private static void ZeroAndFree(IntPtr buffer, int byteCount)
    {
        if (buffer == IntPtr.Zero)
        {
            return;
        }

        for (int index = 0; index < byteCount; index++)
        {
            Marshal.WriteByte(buffer, index, 0);
        }

        Marshal.FreeHGlobal(buffer);
    }

    private readonly record struct RetainedValueBuffer(
        IntPtr Pointer,
        int ByteCount);
}
