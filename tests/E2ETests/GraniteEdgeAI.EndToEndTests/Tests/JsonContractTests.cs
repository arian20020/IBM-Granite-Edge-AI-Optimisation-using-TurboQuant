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

    [TestMethod]
    public void Open_rejects_duplicate_property_names_at_any_depth()
    {
        using TestDirectory directory = TestDirectory.Create();
        string path = directory.WriteText("duplicate.json", "{\"outer\":{\"id\":1,\"id\":2}}");

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() => JsonContract.Open(path));

        StringAssert.Contains(failure.Message, "Duplicate JSON property");
    }
}
