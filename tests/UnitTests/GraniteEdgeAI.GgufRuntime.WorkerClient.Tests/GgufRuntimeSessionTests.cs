using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Tests;

[TestClass]
public sealed class GgufRuntimeSessionTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly GgufSessionId SessionId = new(Guid.NewGuid());

    [TestMethod]
    public void StartupFailureIsPreservedInsteadOfReportedAsHandshakeFailure()
    {
        var failure = new GgufRuntimeFailure(
            GgufRuntimeFailureCategory.UnsupportedConfiguration,
            "chat-template-unsupported");
        var loading = new SessionLoadingEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            0,
            "load-model");
        var terminal = new RuntimeFailureEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            1,
            failure);

        GgufRuntimeStartupException exception =
            Assert.ThrowsExactly<GgufRuntimeStartupException>(() =>
                GgufRuntimeSession.ValidateStartupEvents(loading, terminal));

        Assert.AreSame(failure, exception.Failure);
    }

    [TestMethod]
    public void MalformedStartupSequenceRemainsAProtocolFailure()
    {
        var ready = new SessionReadyEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            0);

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufRuntimeSession.ValidateStartupEvents(ready, ready));
    }
}
