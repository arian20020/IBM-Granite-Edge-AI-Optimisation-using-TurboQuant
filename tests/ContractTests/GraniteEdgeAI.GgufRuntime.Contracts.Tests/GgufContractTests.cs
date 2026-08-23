using System.Reflection;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.GgufRuntime.Contracts.Tests;

[TestClass]
public sealed class GgufContractTests
{
    private static readonly Guid RequestId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly GgufSessionId SessionId = new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [TestMethod]
    public void StartSessionCarriesCompleteCpuConfigurationAndBoundedInitialTurns()
    {
        var configuration = new GgufRuntimeConfiguration(
            modelId: "granite-3b-q8",
            modelSha256: new string('a', 64),
            runtimeBuildId: "llama-cpp-cpu-test",
            runtimeSourceCommit: new string('b', 40),
            backend: GgufRuntimeBackend.Cpu,
            deviceId: "cpu",
            contextSize: 4096,
            keyCacheType: GgufCacheType.F16,
            valueCacheType: GgufCacheType.F16,
            gpuLayerCount: 0,
            flashAttention: false,
            threadCount: 8,
            batchSize: 512,
            evidenceGrade: "controlled",
            profileId: "cpu-safe");
        var turns = new[]
        {
            new GgufConversationTurn(GgufConversationRole.User, "First question"),
            new GgufConversationTurn(GgufConversationRole.Assistant, "First answer"),
        };

        var command = new StartSessionCommand(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            configuration,
            turns);

        Assert.AreEqual(GgufRuntimeBackend.Cpu, command.Configuration.Backend);
        Assert.AreEqual(0, command.Configuration.GpuLayerCount);
        Assert.HasCount(2, command.InitialTurns);
        Assert.AreEqual(RequestId, command.RequestId);
        Assert.AreEqual(SessionId, command.SessionId);
    }

    [TestMethod]
    public void SubmitPromptRejectsContentAboveProtocolLimit()
    {
        var oversized = new string('x', GgufProtocolLimits.MaxPromptCharacters + 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SubmitPromptCommand(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                oversized));
    }

    [TestMethod]
    public void TextDeltaEventPreservesCorrelationAndSequence()
    {
        var runtimeEvent = new TextDeltaEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            sequence: 4,
            text: "token");

        Assert.AreEqual(RequestId, runtimeEvent.RequestId);
        Assert.AreEqual(SessionId, runtimeEvent.SessionId);
        Assert.AreEqual(4L, runtimeEvent.Sequence);
        Assert.AreEqual("token", runtimeEvent.Text);
    }

    [TestMethod]
    public void LifecycleEventsRepresentEveryObservableSessionOutcome()
    {
        GgufRuntimeEvent[] events =
        [
            new SessionLoadingEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                0,
                "load-model"),
            new SessionReadyEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                1),
            new ResponseStartedEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                2),
            new UsageUpdatedEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                3,
                inputTokenCount: 12,
                outputTokenCount: 4,
                contextTokenCount: 16),
            new ResponseCompletedEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                4,
                GgufCompletionReason.Stop),
            new ResponseStoppedEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                5,
                GgufStopDisposition.StoppedNeedsReload),
            new RuntimeFailureEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                6,
                new GgufRuntimeFailure(
                    GgufRuntimeFailureCategory.GenerationFailed,
                    "generation-failed")),
            new SessionClosedEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                7,
                cleanupSucceeded: true),
        ];

        Assert.HasCount(8, events);
        CollectionAssert.AreEqual(
            new long[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            events.Select(runtimeEvent => runtimeEvent.Sequence).ToArray());
        Assert.AreEqual(
            GgufStopDisposition.StoppedNeedsReload,
            ((ResponseStoppedEvent)events[5]).Disposition);
        Assert.IsTrue(((SessionClosedEvent)events[7]).CleanupSucceeded);
    }

    [TestMethod]
    public void FailureContractHasNoSensitiveContentOrPathProperty()
    {
        string[] forbiddenFragments = ["Path", "Prompt", "Response", "Environment", "RawLog"];
        PropertyInfo[] properties = typeof(GgufRuntimeFailure).GetProperties();

        foreach (string fragment in forbiddenFragments)
        {
            Assert.IsFalse(
                properties.Any(property => property.Name.Contains(
                    fragment,
                    StringComparison.OrdinalIgnoreCase)),
                $"Failure property names must not contain '{fragment}'.");
        }
    }

    [TestMethod]
    public void FailureRejectsUndefinedCategory()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new GgufRuntimeFailure(
                (GgufRuntimeFailureCategory)int.MaxValue,
                "runtime-unavailable"));
    }

    [TestMethod]
    [DataRow(GgufCompletionReason.Stop)]
    [DataRow(GgufCompletionReason.Length)]
    public void ResponseCompletedRequiresAClosedCompletionReason(
        GgufCompletionReason reason)
    {
        var runtimeEvent = new ResponseCompletedEvent(
            GgufProtocolVersion.Current,
            RequestId,
            SessionId,
            4,
            reason);

        Assert.AreEqual(reason, runtimeEvent.Reason);
    }

    [TestMethod]
    public void ResponseCompletedRejectsUnknownCompletionReason()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ResponseCompletedEvent(
                GgufProtocolVersion.Current,
                RequestId,
                SessionId,
                4,
                (GgufCompletionReason)99));
    }
}
