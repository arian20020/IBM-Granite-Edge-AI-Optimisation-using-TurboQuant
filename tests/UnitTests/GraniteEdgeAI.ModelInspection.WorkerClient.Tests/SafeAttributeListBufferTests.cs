using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Locks ownership of the unmanaged PROC_THREAD_ATTRIBUTE_LIST buffer. Task 6
/// will populate this buffer with the Job Object and exact handle allowlist.
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
    public void DisposeIsIdempotentAndClosesTheBuffer()
    {
        SafeAttributeListBuffer attributes =
            SafeAttributeListBuffer.Create(attributeCount: 2);

        attributes.Dispose();
        attributes.Dispose();

        Assert.IsTrue(attributes.IsClosed);
    }
}
