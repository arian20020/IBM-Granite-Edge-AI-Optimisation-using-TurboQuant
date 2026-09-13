using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Proves the unmanaged Windows environment block is deterministic, correctly
/// terminated, rejects ambiguous input and releases memory exactly once.
/// </summary>
[TestClass]
public sealed class WindowsEnvironmentBlockTests
{
    [TestMethod]
    public void CreateSortsEntriesAndWritesExactDoubleNulTerminator()
    {
        Dictionary<string, string> environment =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["zeta"] = "last",
                ["Alpha"] = "first"
            };

        using WindowsEnvironmentBlock block =
            WindowsEnvironmentBlock.Create(environment);
        char[] characters = block.CopyCharactersForTests();

        CollectionAssert.AreEqual(
            "Alpha=first\0zeta=last\0\0".ToCharArray(),
            characters);
        Assert.AreNotEqual(IntPtr.Zero, block.Pointer);
    }

    [TestMethod]
    public void CreateRejectsNulInKeyOrValue()
    {
        Dictionary<string, string>[] invalid =
        [
            new() { ["bad\0key"] = "value" },
            new() { ["key"] = "bad\0value" }
        ];

        foreach (Dictionary<string, string> environment in invalid)
        {
            WorkerClientPolicyException error =
                Assert.ThrowsExactly<WorkerClientPolicyException>(
                    () => WindowsEnvironmentBlock.Create(environment));
            Assert.AreEqual(
                WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
                error.Failure.Code);
        }
    }

    [TestMethod]
    public void DisposeIsIdempotentAndClearsPointer()
    {
        WindowsEnvironmentBlock block = WindowsEnvironmentBlock.Create(
            new Dictionary<string, string> { ["A"] = "B" });

        block.Dispose();
        block.Dispose();

        Assert.AreEqual(IntPtr.Zero, block.Pointer);
        Assert.ThrowsExactly<ObjectDisposedException>(block.CopyCharactersForTests);
    }
}
