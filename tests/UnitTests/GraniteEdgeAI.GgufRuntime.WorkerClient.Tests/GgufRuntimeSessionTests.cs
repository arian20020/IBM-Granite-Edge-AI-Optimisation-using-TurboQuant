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

    [TestMethod]
    public void ExactCloseAcknowledgementIsAccepted()
    {
        var closed = new SessionClosedEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            0,
            cleanupSucceeded: true);

        GgufRuntimeSession.ValidateCloseEvent(closed, RequestId, SessionId);
    }

    [TestMethod]
    public void WrongCloseEventTypeIsAProtocolFailure()
    {
        var ready = new SessionReadyEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            0);

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufRuntimeSession.ValidateCloseEvent(ready, RequestId, SessionId));
    }

    [TestMethod]
    public void WrongCloseProtocolOrCorrelationIsAProtocolFailure()
    {
        static SessionClosedEvent Closed(
            int protocolVersion,
            Guid requestId,
            GgufSessionId sessionId) => new(
                protocolVersion,
                requestId,
                sessionId,
                0,
                cleanupSucceeded: true);

        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufRuntimeSession.ValidateCloseEvent(
                Closed(GgufProtocolVersion.Current + 1, RequestId, SessionId),
                RequestId,
                SessionId));
        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufRuntimeSession.ValidateCloseEvent(
                Closed(GgufProtocolVersion.Current, Guid.NewGuid(), SessionId),
                RequestId,
                SessionId));
        Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufRuntimeSession.ValidateCloseEvent(
                Closed(
                    GgufProtocolVersion.Current,
                    RequestId,
                    new GgufSessionId(Guid.NewGuid())),
                RequestId,
                SessionId));
    }

    [TestMethod]
    public void FailedWorkerCleanupIsAStablePolicyFailure()
    {
        var closed = new SessionClosedEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            0,
            cleanupSucceeded: false);

        GgufWorkerPolicyException exception =
            Assert.ThrowsExactly<GgufWorkerPolicyException>(() =>
                GgufRuntimeSession.ValidateCloseEvent(
                    closed,
                    RequestId,
                    SessionId));

        Assert.AreEqual("worker-session-close-cleanup-failed", exception.Code);
    }
}
