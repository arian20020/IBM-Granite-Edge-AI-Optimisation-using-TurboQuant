using System.Runtime.InteropServices;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Proves the anonymous-pipe ownership model used by the future launcher. Only
/// the three child endpoints may remain inheritable; parent endpoints must stay
/// local and preserve the intended stdin/stdout/stderr direction.
/// </summary>
[TestClass]
public sealed class WindowsPipeSetTests
{
    [TestMethod]
    public void CreateMakesOnlyTheThreeChildEndpointsInheritable()
    {
        using WindowsPipeSet pipes = WindowsPipeSet.Create();

        AssertHandleInheritance(pipes.ChildStandardInputRead, expected: true);
        AssertHandleInheritance(pipes.ChildStandardOutputWrite, expected: true);
        AssertHandleInheritance(pipes.ChildStandardErrorWrite, expected: true);
        AssertHandleInheritance(pipes.ParentStandardInputWrite, expected: false);
        AssertHandleInheritance(pipes.ParentStandardOutputRead, expected: false);
        AssertHandleInheritance(pipes.ParentStandardErrorRead, expected: false);
    }

    [TestMethod]
    public void StandardInputFlowsFromParentWriterToChildReader()
    {
        using WindowsPipeSet pipes = WindowsPipeSet.Create();
        byte[] payload = [0x10, 0x20, 0x30, 0x40];
        byte[] received = new byte[payload.Length];

        Assert.IsTrue(
            NativeMethods.WriteFile(
                pipes.ParentStandardInputWrite,
                payload,
                checked((uint)payload.Length),
                out uint written,
                IntPtr.Zero));
        Assert.AreEqual((uint)payload.Length, written);
        Assert.IsTrue(
            NativeMethods.ReadFile(
                pipes.ChildStandardInputRead,
                received,
                checked((uint)received.Length),
                out uint read,
                IntPtr.Zero));
        Assert.AreEqual((uint)payload.Length, read);
        CollectionAssert.AreEqual(payload, received);
    }

    [TestMethod]
    public void StandardOutputAndErrorFlowFromChildWritersToParentReaders()
    {
        using WindowsPipeSet pipes = WindowsPipeSet.Create();

        AssertPipeDirection(
            pipes.ChildStandardOutputWrite,
            pipes.ParentStandardOutputRead,
            [0x51, 0x52]);
        AssertPipeDirection(
            pipes.ChildStandardErrorWrite,
            pipes.ParentStandardErrorRead,
            [0x61, 0x62, 0x63]);
    }

    [TestMethod]
    public void ClosingChildWriterProducesParentEndOfFile()
    {
        using WindowsPipeSet pipes = WindowsPipeSet.Create();
        pipes.ChildStandardOutputWrite.Dispose();
        byte[] buffer = new byte[1];

        bool succeeded = NativeMethods.ReadFile(
            pipes.ParentStandardOutputRead,
            buffer,
            1,
            out uint read,
            IntPtr.Zero);
        int error = Marshal.GetLastPInvokeError();

        Assert.IsFalse(succeeded);
        Assert.AreEqual(0u, read);
        Assert.AreEqual(NativeConstants.ErrorBrokenPipe, error);
    }

    private static void AssertHandleInheritance(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        bool expected)
    {
        Assert.IsTrue(NativeMethods.GetHandleInformation(handle, out uint flags));
        bool actual = (flags & NativeConstants.HandleFlagInherit) != 0;
        Assert.AreEqual(expected, actual);
    }

    private static void AssertPipeDirection(
        Microsoft.Win32.SafeHandles.SafeFileHandle writer,
        Microsoft.Win32.SafeHandles.SafeFileHandle reader,
        byte[] payload)
    {
        byte[] received = new byte[payload.Length];

        Assert.IsTrue(
            NativeMethods.WriteFile(
                writer,
                payload,
                checked((uint)payload.Length),
                out uint written,
                IntPtr.Zero));
        Assert.AreEqual((uint)payload.Length, written);
        Assert.IsTrue(
            NativeMethods.ReadFile(
                reader,
                received,
                checked((uint)received.Length),
                out uint read,
                IntPtr.Zero));
        Assert.AreEqual((uint)payload.Length, read);
        CollectionAssert.AreEqual(payload, received);
    }
}
