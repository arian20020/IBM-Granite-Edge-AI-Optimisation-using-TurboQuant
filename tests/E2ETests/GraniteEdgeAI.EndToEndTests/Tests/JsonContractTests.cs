using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class JsonContractTests
{
    [TestMethod]
    public void Open_rejects_manifest_larger_than_closed_bound()
    {
        using TestDirectory directory = TestDirectory.Create();
        string path = directory.WriteBytes(
            "oversized.json",
            new byte[(1024 * 1024) + 1]);

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(
            () => JsonContract.Open(path));

        StringAssert.Contains(failure.Message, "closed size bound");
    }
}
