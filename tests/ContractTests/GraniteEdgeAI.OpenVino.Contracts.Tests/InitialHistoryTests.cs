using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class InitialHistoryTests
{
    [TestMethod]
    public void TooManyTurnsAreRejectedWithoutTrimming()
    {
        JsonObject input = Command();
        var turns = new JsonArray();
        for (int index = 0; index < 513; index++) turns.Add(new JsonObject { ["role"] = "user", ["content"] = "x" });
        input["initialHistory"] = turns;
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.DeserializeCommand(Encoding.UTF8.GetBytes(input.ToJsonString())));
        Assert.HasCount(513, turns);
    }
    [TestMethod]
    public void OrderedInitialHistorySurvivesWireRoundTripWithoutRoleSubstitution()
    {
        JsonObject input = Command();
        input["initialHistory"] = JsonNode.Parse("""[{"role":"user","content":"violet"},{"role":"assistant","content":"remembered"}]""");
        IOpenVinoCommand parsed = OpenVinoProtocolJson.DeserializeCommand(Encoding.UTF8.GetBytes(input.ToJsonString()));
        JsonNode result = JsonNode.Parse(OpenVinoProtocolJson.Serialize(parsed))!;
        Assert.AreEqual("user", result["initialHistory"]![0]!["role"]!.GetValue<string>());
        Assert.AreEqual("assistant", result["initialHistory"]![1]!["role"]!.GetValue<string>());
        Assert.AreEqual("remembered", result["initialHistory"]![1]!["content"]!.GetValue<string>());
    }

    [TestMethod]
    public void UnknownRoleIsRejectedRatherThanDisguisedAsUser()
    {
        JsonObject input = Command();
        input["initialHistory"] = JsonNode.Parse("""[{"role":"tool","content":"private"}]""");
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.DeserializeCommand(Encoding.UTF8.GetBytes(input.ToJsonString())));
    }

    private static JsonObject Command() => JsonNode.Parse("""{"sessionId":"00000000-0000-0000-0000-000000000001","inspectionRunId":"00000000-0000-0000-0000-000000000002","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":1,"device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":1024,"maximumNewTokens":128},"runtime":{"kvCachePrecision":"released-default"},"commandType":"startSession"}""")!.AsObject();
}
