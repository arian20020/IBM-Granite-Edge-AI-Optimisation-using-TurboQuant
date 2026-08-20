using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ProtocolJsonTests
{
    private static readonly Guid SessionId = Guid.Parse("9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0");
    private static readonly Guid RunId = Guid.Parse("6e1ff10c-fd83-4b03-9a12-d35247e5a6a3");
    private static readonly Guid TurnId = Guid.Parse("2ff65f3b-4ee0-4e04-8c48-73f95af43a6f");
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void SerializationMatchesHandWrittenClosedWireLiteralsInsteadOfOnlyRoundTrippingClrTypes()
    {
        (IOpenVinoCommand Value, string Json)[] commands =
        [
            (new StartInspectionCommand(RunId), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","commandType":"startInspection"}"""),
            (StartCommand(), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":1024,"maximumNewTokens":128},"commandType":"startSession"}"""),
            (new PromptCommand(SessionId, TurnId, "independent prompt literal", 128), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","prompt":"independent prompt literal","requestedNewTokens":128,"commandType":"prompt"}"""),
            (new StopTurnCommand(SessionId, TurnId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","commandType":"stopTurn"}"""),
            (new CancelSessionCommand(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","commandType":"cancelSession"}""")
        ];
        (IOpenVinoEvent Value, string Json)[] events =
        [
            (new HelloEvent("openvino.official/1"), """{"protocolId":"openvino.official/1","eventType":"hello"}"""),
            (new InspectionCompletedEvent(RunId), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","eventType":"inspectionCompleted"}"""),
            (new InspectionFailedEvent(RunId, OpenVinoSupportCode.PackageUnreadable), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","supportCode":"package_unreadable","eventType":"inspectionFailed"}"""),
            (new SessionStartedEvent(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","eventType":"sessionStarted"}"""),
            (new GenerationStartedEvent(SessionId, TurnId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","eventType":"generationStarted"}"""),
            (new TokenEvent(SessionId, TurnId, 0, "token"), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","sequence":0,"text":"token","eventType":"token"}"""),
            (new TurnCompletedEvent(SessionId, TurnId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","eventType":"turnCompleted"}"""),
            (new TurnFailedEvent(SessionId, TurnId, OpenVinoSupportCode.RuntimeTimedOut), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","supportCode":"runtime_timed_out","eventType":"turnFailed"}"""),
            (new SessionCompletedEvent(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","eventType":"sessionCompleted"}"""),
            (new SessionFailedEvent(SessionId, OpenVinoSupportCode.RuntimeLoadFailed), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","supportCode":"runtime_load_failed","eventType":"sessionFailed"}"""),
            (new SessionCancelledEvent(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","eventType":"sessionCancelled"}""")
        ];

        foreach ((IOpenVinoCommand value, string json) in commands)
        {
            Assert.AreEqual(json, Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(value)));
            Assert.AreEqual(value, OpenVinoProtocolJson.DeserializeCommand(Encoding.UTF8.GetBytes(json)));
        }

        foreach ((IOpenVinoEvent value, string json) in events)
        {
            Assert.AreEqual(json, Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(value)));
            Assert.AreEqual(value, OpenVinoProtocolJson.DeserializeEvent(Encoding.UTF8.GetBytes(json)));
        }
    }

    [TestMethod]
    public void CommandParsingRejectsUnknownMemberInsteadOfSilentlyDroppingAProtocolInstruction()
    {
        byte[] json = Encoding.UTF8.GetBytes($$"""{"commandType":"startSession","sessionId":"{{SessionId:D}}","inspectionRunId":"{{RunId:D}}","packageManifestDigest":"{{Digest}}","device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":1024,"maximumNewTokens":128},"surprise":"value"}""");

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.DeserializeCommand(json));
    }

    [TestMethod]
    public void CommandParsingRejectsDuplicatePropertyInsteadOfAcceptingAnAmbiguousIdentity()
    {
        byte[] json = Encoding.UTF8.GetBytes($"{{\"commandType\":\"startSession\",\"sessionId\":\"{SessionId:D}\",\"sessionId\":\"{Guid.NewGuid():D}\",\"inspectionRunId\":\"{RunId:D}\",\"packageManifestDigest\":\"{Digest}\",\"device\":{{\"deviceId\":\"CPU\"}},\"limits\":{{\"maximumContextTokens\":1024,\"maximumNewTokens\":128}}}}");

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.DeserializeCommand(json));
    }

    [TestMethod]
    public void CommandParsingRejectsWrongPropertyCaseInsteadOfBroadeningTheClosedSchema()
    {
        byte[] json = Encoding.UTF8.GetBytes($"{{\"commandType\":\"startSession\",\"SessionId\":\"{SessionId:D}\",\"inspectionRunId\":\"{RunId:D}\",\"packageManifestDigest\":\"{Digest}\",\"device\":{{\"deviceId\":\"CPU\"}},\"limits\":{{\"maximumContextTokens\":1024,\"maximumNewTokens\":128}}}}");

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.DeserializeCommand(json));
    }

    [TestMethod]
    public void ParsingRejectsThirtyThirdNestedLevelInsteadOfExhaustingTheJsonBoundary()
    {
        string json = new string('[', 33) + new string(']', 33);

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.DeserializeEvent(Encoding.UTF8.GetBytes(json)));
    }

    [TestMethod]
    public void ParsingRejectsOneByteOverOneMiBLineInsteadOfAllocatingAnUnboundedFrame()
    {
        byte[] line = new byte[OpenVinoProtocol.MaximumLineBytes + 1];

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.DeserializeEvent(line));
    }

    [TestMethod]
    public void PromptValidationRejectsSixtyFourKiBPlusOneUtf8ByteInsteadOfPassingAnOversizedPrompt()
    {
        PromptCommand command = new(SessionId, TurnId, new string('x', OpenVinoProtocol.MaximumPromptUtf8Bytes + 1), 1);

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.Serialize(command));
    }

    [TestMethod]
    public void PromptValidationRejectsInvalidUtf16InsteadOfLeakingAnEncoderDiagnostic()
    {
        PromptCommand command = new(SessionId, TurnId, "invalid\ud800", 1);

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.Serialize(command));
    }

    [TestMethod]
    public void PromptValidationRejectsFiveHundredThirteenthNewTokenInsteadOfExceedingTheRouteLimit()
    {
        PromptCommand command = new(SessionId, TurnId, "bounded", OpenVinoProtocol.MaximumNewTokens + 1);

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.Serialize(command));
    }

    [TestMethod]
    public void TokenValidationRejectsFourMiBPlusOneOperationTextInsteadOfRetainingUnboundedOutput()
    {
        TokenEvent @event = new(SessionId, TurnId, 0, new string('x', OpenVinoProtocol.MaximumOperationTextUtf8Bytes + 1));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => OpenVinoProtocolJson.Serialize(@event));
    }

    [TestMethod]
    public void HandoffParsingRejectsPathsAndNoncanonicalIdentifiersInsteadOfLeakingOrAcceptingMutableModelIdentity()
    {
        string invalid = $$"""{"schemaVersion":2,"modelInspectionHandoffId":"{{Guid.NewGuid():D}}","modelInspectionRunId":"{{RunId:D}}","outcome":"Ready","modelSha256":"{{Digest}}","modelLengthBytes":1,"path":"C:\\model\\openvino_model.bin"}""";

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            ModelInspectionHandoffV2.Parse(Encoding.UTF8.GetBytes(invalid)));
    }

    [TestMethod]
    public void HandoffCanonicalSerializationContainsExactlyTheSixPathFreeFieldsForOpenvinoModelBinIdentity()
    {
        ModelInspectionHandoffV2 handoff = new(
            Guid.Parse("3b0ff45e-07a6-4a3a-8a2e-947ca3f8e9a6"),
            RunId,
            ModelInspectionOutcome.ReadyWithWarnings,
            Digest,
            42);

        byte[] payload = handoff.ToCanonicalUtf8Json();
        string json = Encoding.UTF8.GetString(payload);

        Assert.IsTrue(payload.Length <= ModelInspectionHandoffV2.MaximumCanonicalUtf8Bytes);
        StringAssert.Contains(json, "\"schemaVersion\":2");
        StringAssert.Contains(json, "\"modelSha256\":\"" + Digest + "\"");
        Assert.IsFalse(json.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(handoff, ModelInspectionHandoffV2.Parse(payload));
    }

    [TestMethod]
    public void HandoffParsingRejectsNoncanonicalPropertyOrderInsteadOfTreatingEquivalentJsonAsTheCanonicalHandoff()
    {
        string reordered = $$"""{"modelInspectionRunId":"{{RunId:D}}","schemaVersion":2,"modelInspectionHandoffId":"3b0ff45e-07a6-4a3a-8a2e-947ca3f8e9a6","outcome":"Ready","modelSha256":"{{Digest}}","modelLengthBytes":1}""";

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            ModelInspectionHandoffV2.Parse(Encoding.UTF8.GetBytes(reordered)));
    }

    [TestMethod]
    public void HandoffParsingRejectsWrongOutcomeCaseInsteadOfCoercingAnIneligibleOutcome()
    {
        string lowerCaseOutcome = $$"""{"schemaVersion":2,"modelInspectionHandoffId":"3b0ff45e-07a6-4a3a-8a2e-947ca3f8e9a6","modelInspectionRunId":"{{RunId:D}}","outcome":"ready","modelSha256":"{{Digest}}","modelLengthBytes":1}""";

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            ModelInspectionHandoffV2.Parse(Encoding.UTF8.GetBytes(lowerCaseOutcome)));
    }

    private static StartSessionCommand StartCommand() => new(
        SessionId,
        RunId,
        Digest,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(1024, 128));
}
