using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class Task7ProtocolExtensionTests
{
    private static readonly Guid SessionId =
        Guid.Parse("9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0");
    private static readonly Guid RunId =
        Guid.Parse("6e1ff10c-fd83-4b03-9a12-d35247e5a6a3");
    private static readonly Guid TurnId =
        Guid.Parse("2ff65f3b-4ee0-4e04-8c48-73f95af43a6f");
    private const string PackagePath = @"C:\operation\package";
    private const string PackageDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ModelDigest =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string WorkerDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";

    [TestMethod]
    public void HelloCarriesBuildEvidenceInOneClosedAtomicWireLiteral()
    {
        HelloEvent hello = new(OpenVinoProtocol.OfficialProtocolId, BuildEvidence());
        const string json = """{"protocolId":"openvino.official/1","buildEvidence":{"runtimeBuild":"2026.3.0-22451-8a17657b995-releases/2026/3","genAiBuild":"2026.3.0.0-3277-bd8d6542e3c","tokenizersBuild":"2026.3.0.0-703-183c6f25cda","workerManifestDigest":"1111111111111111111111111111111111111111111111111111111111111111"},"eventType":"hello"}""";

        Assert.AreEqual(json, Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(hello)));
        Assert.AreEqual(hello, OpenVinoProtocolJson.DeserializeEvent(Encoding.UTF8.GetBytes(json)));
    }

    [TestMethod]
    public void TurnCompletionRejectsCombinedPromptAndGeneratedCountsAboveContext()
    {
        OpenVinoConversationValidator validator = new();
        validator.Accept(Hello());
        validator.Accept(StartSession());
        validator.Accept(SessionStarted());
        validator.Accept(new PromptCommand(SessionId, TurnId, "hello", 2));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(
            new TurnCompletedEvent(
                SessionId,
                TurnId,
                63,
                2,
                OpenVinoTurnDisposition.Completed)));
    }

    [TestMethod]
    public void ExtendedCommandsSerializeToIndependentClosedWireLiterals()
    {
        (IOpenVinoCommand Value, string Json)[] commands =
        [
            (StartInspection(), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":88,"commandType":"startInspection"}"""),
            (StartSession(), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packagePath":"C:\\operation\\package","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":88,"device":{"deviceId":"CPU"},"limits":{"maximumContextTokens":64,"maximumNewTokens":2},"commandType":"startSession"}"""),
            (new CloseSessionCommand(SessionId), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","commandType":"closeSession"}""")
        ];

        foreach ((IOpenVinoCommand value, string json) in commands)
        {
            Assert.AreEqual(
                json,
                Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(value)));
            Assert.AreEqual(
                value,
                OpenVinoProtocolJson.DeserializeCommand(
                    Encoding.UTF8.GetBytes(json)));
        }
    }

    [TestMethod]
    public void ExtendedEventsSerializeToIndependentClosedWireLiterals()
    {
        OpenVinoBuildEvidence build = BuildEvidence();
        (IOpenVinoEvent Value, string Json)[] events =
        [
            (new InspectionStartedEvent(RunId), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","eventType":"inspectionStarted"}"""),
            (new InspectionProgressEvent(RunId, OpenVinoInspectionStage.ManifestVerified), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","stage":"manifestVerified","eventType":"inspectionProgress"}"""),
            (new InspectionCompletedEvent(RunId, PackageDigest, ModelDigest, 88, true, true, true, build), """{"inspectionRunId":"6e1ff10c-fd83-4b03-9a12-d35247e5a6a3","packageManifestDigest":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","modelSha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","modelLengthBytes":88,"mainModelParsed":true,"tokenizerParsed":true,"detokenizerParsed":true,"buildEvidence":{"runtimeBuild":"2026.3.0-22451-8a17657b995-releases/2026/3","genAiBuild":"2026.3.0.0-3277-bd8d6542e3c","tokenizersBuild":"2026.3.0.0-703-183c6f25cda","workerManifestDigest":"1111111111111111111111111111111111111111111111111111111111111111"},"eventType":"inspectionCompleted"}"""),
            (new SessionStartedEvent(SessionId, "CPU", ["CPU"], OpenVinoProtocol.OfficialProtocolId, build), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","requestedDevice":"CPU","actualExecutionDevices":["CPU"],"protocolId":"openvino.official/1","buildEvidence":{"runtimeBuild":"2026.3.0-22451-8a17657b995-releases/2026/3","genAiBuild":"2026.3.0.0-3277-bd8d6542e3c","tokenizersBuild":"2026.3.0.0-703-183c6f25cda","workerManifestDigest":"1111111111111111111111111111111111111111111111111111111111111111"},"eventType":"sessionStarted"}"""),
            (new TurnCompletedEvent(SessionId, TurnId, 1, 1, OpenVinoTurnDisposition.Stopped), """{"sessionId":"9d86e640-2f8f-4f35-8f51-c7e2bc3d07c0","turnId":"2ff65f3b-4ee0-4e04-8c48-73f95af43a6f","promptTokenCount":1,"generatedTokenCount":1,"disposition":"stopped","eventType":"turnCompleted"}""")
        ];

        foreach ((IOpenVinoEvent value, string json) in events)
        {
            Assert.AreEqual(
                json,
                Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(value)));
            IOpenVinoEvent parsed = OpenVinoProtocolJson.DeserializeEvent(
                Encoding.UTF8.GetBytes(json));
            Assert.AreEqual(
                json,
                Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(parsed)));
        }
    }

    [TestMethod]
    public void PackagePathsAndBuildEvidenceRejectUnsafeOrUnboundedValues()
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.Serialize(StartInspection() with
            {
                PackagePath = "relative-package"
            }));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.Serialize(StartInspection() with
            {
                PackagePath = "C:\\operation\\bad\npath"
            }));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.Serialize(
                SessionStarted() with
                {
                    BuildEvidence = BuildEvidence() with
                    {
                        RuntimeBuild = new string('r',
                            OpenVinoProtocol.MaximumBuildIdentityUtf8Bytes + 1)
                    }
                }));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoProtocolJson.Serialize(
                new SessionStartedEvent(
                    SessionId,
                    "CPU",
                    ["CPU", "CPU"],
                    OpenVinoProtocol.OfficialProtocolId,
                    BuildEvidence())));
    }

    [TestMethod]
    public void InspectionAndIdleCloseFollowTheExtendedOrderedConversation()
    {
        OpenVinoConversationValidator inspection = new();
        inspection.Accept(Hello());
        inspection.Accept(StartInspection());
        inspection.Accept(new InspectionStartedEvent(RunId));
        inspection.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.ManifestVerified));
        inspection.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.MainModelParsed));
        inspection.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.TokenizerParsed));
        inspection.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.DetokenizerParsed));
        inspection.Accept(InspectionCompleted());
        Assert.IsTrue(inspection.IsTerminal);

        OpenVinoConversationValidator session = new();
        session.Accept(Hello());
        session.Accept(StartSession());
        session.Accept(SessionStarted());
        session.Accept(new CloseSessionCommand(SessionId));
        session.Accept(new SessionCompletedEvent(SessionId));
        Assert.IsTrue(session.IsTerminal);
    }

    [TestMethod]
    public void InspectionRejectsSkippedOrRepeatedProgressStagesAndMismatchedIdentity()
    {
        OpenVinoConversationValidator validator = new();
        validator.Accept(Hello());
        validator.Accept(StartInspection());
        validator.Accept(new InspectionStartedEvent(RunId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new InspectionProgressEvent(
                RunId,
                OpenVinoInspectionStage.TokenizerParsed)));

        OpenVinoConversationValidator identity = new();
        identity.Accept(Hello());
        identity.Accept(StartInspection());
        identity.Accept(new InspectionStartedEvent(RunId));
        identity.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.ManifestVerified));
        identity.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.MainModelParsed));
        identity.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.TokenizerParsed));
        identity.Accept(new InspectionProgressEvent(
            RunId,
            OpenVinoInspectionStage.DetokenizerParsed));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            identity.Accept(InspectionCompleted() with
            {
                ModelSha256 = new string('f', 64)
            }));
    }

    private static StartInspectionCommand StartInspection() => new(
        RunId,
        PackagePath,
        PackageDigest,
        ModelDigest,
        88);

    private static StartSessionCommand StartSession() => new(
        SessionId,
        RunId,
        PackagePath,
        PackageDigest,
        ModelDigest,
        88,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(64, 2));

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        WorkerDigest);

    private static HelloEvent Hello() => new(
        OpenVinoProtocol.OfficialProtocolId,
        BuildEvidence());

    private static InspectionCompletedEvent InspectionCompleted() => new(
        RunId,
        PackageDigest,
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
