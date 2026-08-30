using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ProtocolJsonTests
{
    [TestMethod]
    public void OpenVinoContractsAssemblyDoesNotDeclareModelInspectionHandoffAuthority()
    {
        Assert.IsNull(
            typeof(OpenVinoProtocolJson).Assembly.GetType(
                "GraniteEdgeAI.OpenVino.Contracts.ModelInspectionHandoffV2",
                throwOnError: false),
            "Model Inspection contracts must be the sole schema-v2 handoff authority.");
    }

    private static readonly Guid SessionId = Guid.Parse("9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0");
    private static readonly Guid RunId = Guid.Parse("6e1ff10c-fd83-4b03-9a12-d35247e5a6a3");
    private static readonly Guid TurnId = Guid.Parse("2ff65f3b-4ee0-4e04-8c48-73f95af43a6f");
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ModelDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string WorkerDigest = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string PackagePath = @"C:\operation\package";

    [TestMethod]
    public void SerializationMatchesHandWrittenClosedWireLiteralsInsteadOfOnlyRoundTrippingClrTypes()
    {
        (IOpenVinoCommand Value, string Json)[] commands =
        [
            (StartInspection(), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":88,"commandType":"startInspection"}"""),
            (StartCommand(), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":88,"device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":1024,"maximumNewTokens":128},"runtime":{"kvCachePrecision":"released-default"},"commandType":"startSession"}"""),
            (new PromptCommand(SessionId, TurnId, "independent prompt literal", 128), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","prompt":"independent prompt literal","requestedNewTokens":128,"commandType":"prompt"}"""),
            (new StopTurnCommand(SessionId, TurnId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","commandType":"stopTurn"}"""),
            (new CancelSessionCommand(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","commandType":"cancelSession"}"""),
            (new CloseSessionCommand(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","commandType":"closeSession"}""")
        ];
        (IOpenVinoEvent Value, string Json)[] events =
        [
            (new HelloEvent("openvino.official/1", BuildEvidence()), """{"protocolId":"openvino.official/1","buildEvidence":{"runtimeBuild":"2026.3.0-22451-8a17657b995-releases/2026/3","genAiBuild":"2026.3.0.0-3277-bd8d6542e3c","tokenizersBuild":"2026.3.0.0-703-183c6f25cda","workerManifestDigest":"1111111111111111111111111111111111111111111111111111111111111111"},"eventType":"hello"}"""),
            (new InspectionStartedEvent(RunId), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","eventType":"inspectionStarted"}"""),
            (new InspectionProgressEvent(RunId, OpenVinoInspectionStage.ManifestVerified), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","stage":"manifestVerified","eventType":"inspectionProgress"}"""),
            (InspectionCompleted(), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":88,"mainModelParsed":true,"tokenizerParsed":true,"detokenizerParsed":true,"buildEvidence":{"runtimeBuild":"2026.3.0-22451-8a17657b995-releases/2026/3","genAiBuild":"2026.3.0.0-3277-bd8d6542e3c","tokenizersBuild":"2026.3.0.0-703-183c6f25cda","workerManifestDigest":"1111111111111111111111111111111111111111111111111111111111111111"},"eventType":"inspectionCompleted"}"""),
            (new InspectionFailedEvent(RunId, OpenVinoSupportCode.PackageUnreadable), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","supportCode":"package_unreadable","eventType":"inspectionFailed"}"""),
            (SessionStarted(), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","requestedDevice":"CPU","actualExecutionDevices":["CPU"],"requestedKvCachePrecision":"released-default","actualKvCachePrecision":"released-default","protocolId":"openvino.official/1","buildEvidence":{"runtimeBuild":"2026.3.0-22451-8a17657b995-releases/2026/3","genAiBuild":"2026.3.0.0-3277-bd8d6542e3c","tokenizersBuild":"2026.3.0.0-703-183c6f25cda","workerManifestDigest":"1111111111111111111111111111111111111111111111111111111111111111"},"eventType":"sessionStarted"}"""),
            (new GenerationStartedEvent(SessionId, TurnId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","eventType":"generationStarted"}"""),
            (new TokenEvent(SessionId, TurnId, 0, "token"), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","sequence":0,"text":"token","eventType":"token"}"""),
            (new TurnCompletedEvent(SessionId, TurnId, 4, 1, OpenVinoTurnDisposition.Completed), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","promptTokenCount":4,"generatedTokenCount":1,"disposition":"completed","eventType":"turnCompleted"}"""),
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
            IOpenVinoEvent parsed = OpenVinoProtocolJson.DeserializeEvent(
                Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(
                json,
                Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(parsed)));
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

    private static StartSessionCommand StartCommand() => new(
        SessionId,
        RunId,
        PackagePath,
        Digest,
        ModelDigest,
        88,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(1024, 128));

    private static StartInspectionCommand StartInspection() => new(
        RunId,
        PackagePath,
        Digest,
        ModelDigest,
        88);

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        WorkerDigest);

    private static InspectionCompletedEvent InspectionCompleted() => new(
        RunId,
        Digest,
        ModelDigest,
        88,
        true,
        true,
        true,
        BuildEvidence());

    private static SessionStartedEvent SessionStarted() => new(
        SessionId,
        "CPU",
        ["CPU"],
        OpenVinoProtocol.OfficialProtocolId,
        BuildEvidence());
}
