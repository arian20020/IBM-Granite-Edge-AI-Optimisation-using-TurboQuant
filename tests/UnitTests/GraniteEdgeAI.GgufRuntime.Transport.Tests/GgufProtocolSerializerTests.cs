using System.Text;
using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Transport.Tests;

[TestClass]
public sealed class GgufProtocolSerializerTests
{
    private static readonly Guid RequestId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly GgufSessionId SessionId = new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [TestMethod]
    public void StartSessionRoundTripPreservesConfigurationAndInitialTurns()
    {
        var command = new StartSessionCommand(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            CreateConfiguration(),
            [new GgufConversationTurn(GgufConversationRole.User, "hello")]);

        byte[] payload = GgufProtocolSerializer.SerializeCommand(command);
        GgufRuntimeCommand result = GgufProtocolSerializer.DeserializeCommand(payload);

        var start = (StartSessionCommand)result;
        Assert.AreEqual("granite-3b", start.Configuration.ModelId);
        Assert.AreEqual(SessionId, start.SessionId);
        Assert.HasCount(1, start.InitialTurns);
        Assert.AreEqual("hello", start.InitialTurns[0].Content);
    }

    [TestMethod]
    public void TextDeltaRoundTripPreservesCorrelationSequenceAndText()
    {
        var runtimeEvent = new TextDeltaEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            7,
            "delta");

        byte[] payload = GgufProtocolSerializer.SerializeEvent(runtimeEvent);
        GgufRuntimeEvent result = GgufProtocolSerializer.DeserializeEvent(payload);

        var delta = (TextDeltaEvent)result;
        Assert.AreEqual(RequestId, delta.RequestId);
        Assert.AreEqual(7L, delta.Sequence);
        Assert.AreEqual("delta", delta.Text);
    }

    [TestMethod]
    public void DeserializeCommandRejectsUnknownKind()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            "{\"kind\":\"run-anything\",\"payload\":{}}");

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufProtocolSerializer.DeserializeCommand(payload));
    }

    [TestMethod]
    public void DeserializeCommandRejectsDuplicateMembers()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            "{\"kind\":\"close-session\",\"kind\":\"close-session\",\"payload\":{}}");

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufProtocolSerializer.DeserializeCommand(payload));
    }

    [TestMethod]
    public void DeserializeCommandRejectsInvalidUtf8()
    {
        byte[] payload = [0x7B, 0x22, 0xFF, 0x22, 0x7D];

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufProtocolSerializer.DeserializeCommand(payload));
    }

    [TestMethod]
    public void DeserializeCommandRejectsMismatchedProtocolVersion()
    {
        var command = new SubmitPromptCommand(
            GgufProtocolVersion.Current + 1,
            RequestId,
            SessionId,
            "hello");
        byte[] payload = GgufProtocolSerializer.SerializeCommand(command);

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufProtocolSerializer.DeserializeCommand(payload));
    }

    private static GgufRuntimeConfiguration CreateConfiguration()
    {
        return new GgufRuntimeConfiguration(
            "granite-3b",
            new string('a', 64),
            "cpu-build",
            new string('b', 40),
            GgufRuntimeBackend.Cpu,
            "cpu",
            4096,
            GgufCacheType.F16,
            GgufCacheType.F16,
            0,
            false,
            8,
            512,
            "controlled",
            "cpu-safe");
    }
}
