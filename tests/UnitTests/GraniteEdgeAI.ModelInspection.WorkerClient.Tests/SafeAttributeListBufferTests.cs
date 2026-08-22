using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Locks ownership of the unmanaged PROC_THREAD_ATTRIBUTE_LIST buffer and every
/// pointer-valued attribute stored inside it. Windows requires those value
/// buffers to remain alive until process creation has consumed the list.
/// </summary>
[TestClass]
public sealed class SafeAttributeListBufferTests
{
    [TestMethod]
    public void CreateAllocatesInitializedAttributeList()
    {
        using SafeAttributeListBuffer attributes =
            SafeAttributeListBuffer.Create(attributeCount: 2);

        Assert.IsFalse(attributes.IsInvalid);
        Assert.IsFalse(attributes.IsClosed);
        Assert.AreNotEqual(IntPtr.Zero, attributes.DangerousGetHandle());
    }

    [TestMethod]
    public void CreateRejectsNonPositiveAttributeCount()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => SafeAttributeListBuffer.Create(attributeCount: 0));
    }

    [TestMethod]
    public void UpdatesRetainJobAndHandleValueBuffersUntilDisposal()
    {
        using WindowsJobObject job = WindowsJobObject.CreateKillOnClose();
        using WindowsPipeSet pipes = WindowsPipeSet.Create();
        using SafeAttributeListBuffer attributes =
            SafeAttributeListBuffer.Create(attributeCount: 2);
        List<IntPtr> jobHandles =
        [
            job.Handle.DangerousGetHandle()
        ];

        attributes.UpdatePointerList(
            NativeConstants.ProcThreadAttributeJobList,
            jobHandles);
        attributes.UpdatePointerList(
            NativeConstants.ProcThreadAttributeHandleList,
            pipes.GetChildHandleAllowlist());

        Assert.AreEqual(2, attributes.RetainedValueBufferCountForTests);
    }

    [TestMethod]
    public void DisposeIsIdempotentAndClosesTheBuffer()
    {
        SafeAttributeListBuffer attributes =
            SafeAttributeListBuffer.Create(attributeCount: 2);

        attributes.Dispose();
        attributes.Dispose();

        Assert.IsTrue(attributes.IsClosed);
    }
}
