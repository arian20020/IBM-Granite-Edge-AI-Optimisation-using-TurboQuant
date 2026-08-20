using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ProtocolSequenceTests
{
    private static readonly Guid SessionId = Guid.Parse("e39d252d-2144-4624-a055-0350c93f6728");
    private static readonly Guid RunId = Guid.Parse("3d2d12c1-b7e2-430d-a550-a5b839011ce2");
    private static readonly Guid TurnId = Guid.Parse("f77fb13c-263d-49a1-8d93-d908968c5832");
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void ConversationAcceptsExactlyOneStartSessionThenOrderedPromptGenerationAndTerminalEvents()
    {
        OpenVinoConversationValidator validator = new();

        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(StartSession());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(StartSession()));
        validator.Accept(new SessionStartedEvent(SessionId));
        validator.Accept(new PromptCommand(SessionId, TurnId, "first user prompt", 128));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(new TokenEvent(SessionId, TurnId, 0, "first"));
        validator.Accept(new TokenEvent(SessionId, TurnId, 1, "second"));
        validator.Accept(new TurnCompletedEvent(SessionId, TurnId));
        validator.Accept(new SessionCompletedEvent(SessionId));

        Assert.IsTrue(validator.IsTerminal);
    }

    [TestMethod]
    public void ConversationRejectsSessionStartWithoutExactlyOnePrecedingStartCommand()
    {
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new SessionStartedEvent(SessionId)));
    }

    [TestMethod]
    public void ConversationRejectsPromptAndGenerationWithStaleSessionOrTurnIdentity()
    {
        OpenVinoConversationValidator validator = StartedSession();

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new PromptCommand(Guid.NewGuid(), TurnId, "stale session", 1)));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new GenerationStartedEvent(SessionId, Guid.NewGuid())));
    }

    [TestMethod]
    public void ConversationRejectsConcurrentOrUnpairedPromptTurnsInsteadOfAllowingMultipleActiveTurns()
    {
        OpenVinoConversationValidator validator = StartedSession();
        validator.Accept(new PromptCommand(SessionId, TurnId, "active", 1));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new PromptCommand(SessionId, Guid.NewGuid(), "second", 1)));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new GenerationStartedEvent(SessionId, Guid.NewGuid())));
    }

    [TestMethod]
    public void ConversationAllowsTheSeparateInspectionStartAndTerminalPath()
    {
        OpenVinoConversationValidator validator = new();

        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(new StartInspectionCommand(RunId));
        validator.Accept(new InspectionCompletedEvent(RunId));

        Assert.IsTrue(validator.IsTerminal);
    }

    [TestMethod]
    public void ConversationRejectsStopAndCancelOutsideTheirActiveStatesAndRejectsTerminalReuse()
    {
        OpenVinoConversationValidator validator = StartedSession();
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new StopTurnCommand(SessionId, TurnId)));

        validator.Accept(new PromptCommand(SessionId, TurnId, "stop me", 1));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(new StopTurnCommand(SessionId, TurnId));
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new TokenEvent(SessionId, TurnId, 0, "late token")));
        validator.Accept(new TurnCompletedEvent(SessionId, TurnId));
        validator.Accept(new CancelSessionCommand(SessionId));
        validator.Accept(new SessionCancelledEvent(SessionId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new PromptCommand(SessionId, Guid.NewGuid(), "terminal reuse", 1)));
    }

    [TestMethod]
    public void ConversationRejectsThirtyThirdPromptAndCumulativeOperationTextAcrossTurns()
    {
        OpenVinoConversationValidator validator = StartedSession();

        for (int turn = 0; turn < OpenVinoProtocol.MaximumTurns; turn++)
        {
            Guid id = Guid.NewGuid();
            validator.Accept(new PromptCommand(SessionId, id, "turn", 1));
            validator.Accept(new GenerationStartedEvent(SessionId, id));
            validator.Accept(new TurnCompletedEvent(SessionId, id));
        }

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new PromptCommand(SessionId, Guid.NewGuid(), "turn thirty three", 1)));

        OpenVinoConversationValidator textValidator = StartedSession();
        textValidator.Accept(new PromptCommand(SessionId, TurnId, "first", 1));
        textValidator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        textValidator.Accept(new TokenEvent(SessionId, TurnId, 0, new string('a', 3 * 1024 * 1024)));
        textValidator.Accept(new TurnCompletedEvent(SessionId, TurnId));
        Guid nextTurnId = Guid.NewGuid();
        textValidator.Accept(new PromptCommand(SessionId, nextTurnId, "second", 1));
        textValidator.Accept(new GenerationStartedEvent(SessionId, nextTurnId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            textValidator.Accept(new TokenEvent(SessionId, nextTurnId, 0, new string('b', 2 * 1024 * 1024))));
    }

    private static OpenVinoConversationValidator StartedSession()
    {
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(StartSession());
        validator.Accept(new SessionStartedEvent(SessionId));
        return validator;
    }

    private static StartSessionCommand StartSession() => new(
        SessionId,
        RunId,
        Digest,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(1024, 128));
}
