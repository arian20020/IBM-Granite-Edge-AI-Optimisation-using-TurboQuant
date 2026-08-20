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
    public void SerializationRoundTripsEveryClosedCommandAndEventInsteadOfPermittingAnUnregisteredWireType()
    {
        object[] commands =
        [
            StartCommand(),
            new PromptCommand(SessionId, TurnId, "independent prompt literal", 128),
            new StopTurnCommand(SessionId, TurnId),
            new CancelSessionCommand(SessionId)
        ];
        object[] events =
        [
            new HelloEvent(OpenVinoProtocol.OfficialProtocolId),
            new SessionStartedEvent(SessionId),
            new GenerationStartedEvent(SessionId, TurnId),
            new TokenEvent(SessionId, TurnId, 0, "token"),
            new TurnCompletedEvent(SessionId, TurnId),
            new TurnFailedEvent(SessionId, TurnId, OpenVinoSupportCode.RuntimeTimedOut),
            new SessionCompletedEvent(SessionId),
            new SessionFailedEvent(SessionId, OpenVinoSupportCode.RuntimeLoadFailed),
            new SessionCancelledEvent(SessionId)
        ];

        foreach (object command in commands)
        {
            byte[] payload = OpenVinoProtocolJson.Serialize(command);
            Assert.AreEqual(command.GetType(), OpenVinoProtocolJson.DeserializeCommand(payload).GetType());
        }

        foreach (object @event in events)
        {
            byte[] payload = OpenVinoProtocolJson.Serialize(@event);
            Assert.AreEqual(@event.GetType(), OpenVinoProtocolJson.DeserializeEvent(payload).GetType());
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
