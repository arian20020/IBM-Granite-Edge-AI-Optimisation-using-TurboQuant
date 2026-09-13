using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal sealed class GgufWorkerPipeSet : IDisposable
{
    private GgufWorkerPipeSet(
        SafeFileHandle childInput,
        SafeFileHandle parentInput,
        SafeFileHandle parentOutput,
        SafeFileHandle childOutput,
        SafeFileHandle parentError,
        SafeFileHandle childError)
    {
        ChildInput = childInput;
        ParentInput = parentInput;
        ParentOutput = parentOutput;
        ChildOutput = childOutput;
        ParentError = parentError;
        ChildError = childError;
    }

    internal SafeFileHandle ChildInput { get; private set; }

    internal SafeFileHandle ParentInput { get; private set; }

    internal SafeFileHandle ParentOutput { get; private set; }

    internal SafeFileHandle ChildOutput { get; private set; }

    internal SafeFileHandle ParentError { get; private set; }

    internal SafeFileHandle ChildError { get; private set; }

    internal static GgufWorkerPipeSet Create()
    {
        GgufSecurityAttributes attributes = GgufSecurityAttributes.CreateInheritable();
        CreatePipe(ref attributes, out SafeFileHandle childInput, out SafeFileHandle parentInput);
        try
        {
            CreatePipe(ref attributes, out SafeFileHandle parentOutput, out SafeFileHandle childOutput);
            try
            {
                CreatePipe(ref attributes, out SafeFileHandle parentError, out SafeFileHandle childError);
                SetNotInheritable(parentInput);
                SetNotInheritable(parentOutput);
                SetNotInheritable(parentError);
                return new GgufWorkerPipeSet(
                    childInput,
                    parentInput,
                    parentOutput,
                    childOutput,
                    parentError,
                    childError);
            }
            catch
            {
                parentOutput.Dispose();
                childOutput.Dispose();
                throw;
            }
        }
        catch
        {
            childInput.Dispose();
            parentInput.Dispose();
            throw;
        }
    }

    internal void CloseChildHandles()
    {
        ChildInput.Dispose();
        ChildOutput.Dispose();
        ChildError.Dispose();
    }

    internal SafeFileHandle TakeParentInput()
    {
        SafeFileHandle result = ParentInput;
        ParentInput = new SafeFileHandle(IntPtr.Zero, ownsHandle: true);
        return result;
    }

    internal SafeFileHandle TakeParentOutput()
    {
        SafeFileHandle result = ParentOutput;
        ParentOutput = new SafeFileHandle(IntPtr.Zero, ownsHandle: true);
        return result;
    }

    internal SafeFileHandle TakeParentError()
    {
        SafeFileHandle result = ParentError;
        ParentError = new SafeFileHandle(IntPtr.Zero, ownsHandle: true);
        return result;
    }

    public void Dispose()
    {
        ChildInput.Dispose();
        ParentInput.Dispose();
        ParentOutput.Dispose();
        ChildOutput.Dispose();
        ParentError.Dispose();
        ChildError.Dispose();
    }

    private static void CreatePipe(
        ref GgufSecurityAttributes attributes,
        out SafeFileHandle read,
        out SafeFileHandle write)
    {
        if (!GgufNativeMethods.CreatePipe(out read, out write, ref attributes, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "A GGUF worker pipe could not be created.");
        }
    }

    private static void SetNotInheritable(SafeFileHandle handle)
    {
        if (!GgufNativeMethods.SetHandleInformation(
                handle,
                GgufNativeConstants.HandleFlagInherit,
                0))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "A GGUF worker parent pipe could not be protected.");
        }
    }

}
