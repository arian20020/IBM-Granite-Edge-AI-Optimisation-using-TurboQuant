using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class SupportCodeTests
{
    [TestMethod]
    public void FailureSerializationEmitsEveryFixedSupportCodeInsteadOfAnArbitraryDiagnosticString()
    {
        Guid sessionId = Guid.Parse("d8e0372f-7cf0-47eb-b069-700acde19f1b");
        Guid turnId = Guid.Parse("ce258ce1-3b3d-4c14-8742-91ddc18f0774");

        foreach (OpenVinoSupportCode code in Enum.GetValues<OpenVinoSupportCode>())
        {
            string json = Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(
                new TurnFailedEvent(sessionId, turnId, code)));
            StringAssert.Contains(json, "\"supportCode\":\"" + code.ToProtocolValue() + "\"");
        }
    }

    [TestMethod]
    public void FailureParsingRejectsRawNativeExceptionTextInsteadOfLeakingItThroughSupportCode()
    {
        byte[] rawDiagnostic = Encoding.UTF8.GetBytes(
            """{"eventType":"turnFailed","sessionId":"d8e0372f-7cf0-47eb-b069-700acde19f1b","turnId":"ce258ce1-3b3d-4c14-8742-91ddc18f0774","supportCode":"native backend failed at C:\\users\\secret"}""");

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.DeserializeEvent(rawDiagnostic));
    }
}
