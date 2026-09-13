using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.UnitTests.Features.OpenVinoRoute;

[TestClass]
public sealed class OpenVinoChatControllerTests
{
    [TestMethod]
    public void CompletedReplySurvivesIdleSessionShutdownAndLateSnapshots()
    {
        var bridge = new OpenVinoChatController();
        bridge.BeginTurn("Question");
        bridge.Complete(new(PromptTurnStatus.Completed, "Answer", 1, 1, null));
        foreach (var kind in new[] { PromptEventKind.CancellingSession, PromptEventKind.Cancelled,
                     PromptEventKind.Failed, PromptEventKind.TextDelta })
            bridge.Apply(State("Session status", 10) with { LastEventKind = kind });
        Assert.AreEqual("Answer", bridge.Messages[1].Content);
        Assert.AreEqual(GraniteEdgeAI.Features.GgufRuntime.History.ChatCompletionStatus.Completed,
            bridge.Messages[1].Status);
    }

    [TestMethod]
    public void StopRequestDoesNotClaimCompletionBeforeActualResult()
    {
        var bridge = new OpenVinoChatController();
        bridge.BeginTurn("Question");
        bridge.Apply(State("Partial", 1));
        bridge.Apply(State("Partial", 2) with { LastEventKind = PromptEventKind.StoppingTurn });
        Assert.AreEqual(GraniteEdgeAI.Features.GgufRuntime.History.ChatCompletionStatus.Streaming,
            bridge.Messages[1].Status);
        bridge.Complete(new(PromptTurnStatus.Stopped, "Partial", 1, 1, null));
        bridge.Apply(State("Partial", 3) with { LastEventKind = PromptEventKind.SessionReady });
        Assert.AreEqual(GraniteEdgeAI.Features.GgufRuntime.History.ChatCompletionStatus.Stopped,
            bridge.Messages[1].Status);
    }

    private static PromptSurfaceState State(string response, long revision) => new(
        "OpenVINO", "CPU", "Verified", response, "Generating locally.",
        false, true, true, Guid.NewGuid(), PromptEventKind.TextDelta, revision, 0);

    [TestMethod]
    public void CumulativeResponseReplacesOneStableAssistantMessage()
    {
        var bridge = new OpenVinoChatController();
        bridge.BeginTurn("  Exact input  ");
        bridge.Apply(State("Hello", 1));
        var assistantId = bridge.Messages[1].Id;
        bridge.Apply(State("Hello world", 2));
        Assert.AreEqual(2, bridge.Messages.Count);
        Assert.AreEqual("  Exact input  ", bridge.Messages[0].Content);
        Assert.AreEqual(assistantId, bridge.Messages[1].Id);
        Assert.AreEqual("Hello world", bridge.Messages[1].Content);
    }

    [TestMethod]
    public void RetiredBridgeRejectsLateSnapshots()
    {
        var bridge = new OpenVinoChatController();
        bridge.BeginTurn("Question");
        bridge.Apply(State("Current", 2));
        bridge.Apply(State("Older", 1));
        Assert.AreEqual("Current", bridge.Messages[1].Content);
        bridge.Retire();
        bridge.Apply(State("Late", 3));
        Assert.AreEqual("Current", bridge.Messages[1].Content);
    }

    [TestMethod]
    public void ConsecutiveTurnsKeepEarlierResponse()
    {
        var bridge = new OpenVinoChatController();
        bridge.BeginTurn("One");
        bridge.Apply(State("First", 1));
        bridge.BeginTurn("Two");
        bridge.Apply(State("Second", 2));
        Assert.AreEqual(4, bridge.Messages.Count);
        Assert.AreEqual("First", bridge.Messages[1].Content);
        Assert.AreEqual("Second", bridge.Messages[3].Content);
    }
}
