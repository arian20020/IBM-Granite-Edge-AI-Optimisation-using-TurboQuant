using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlamaCpp;

[TestClass]
public sealed class LlamaCppCapabilityCommandContractTests
{
    private static readonly string[] ExpectedIdentityArguments =
        ["identity", "--format", "json-v1"];
    private static readonly string[] ExpectedCapabilitiesArguments =
        ["capabilities", "--format", "json-v1"];

    [TestMethod]
    public void ContractCreatesOnlyTheTwoPinnedCommands()
    {
        var identity = LlamaCppCapabilityCommandContract.CreateIdentityCommand();
        var capabilities = LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand();

        Assert.AreEqual("identity", identity.Identity);
        CollectionAssert.AreEqual(
            ExpectedIdentityArguments,
            identity.Arguments.ToArray());
        Assert.AreEqual("capabilities", capabilities.Identity);
        CollectionAssert.AreEqual(
            ExpectedCapabilitiesArguments,
            capabilities.Arguments.ToArray());
    }
}
