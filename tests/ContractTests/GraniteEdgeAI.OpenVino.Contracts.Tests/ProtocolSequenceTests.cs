using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ProtocolSequenceTests
{
    private static readonly Guid SessionId = Guid.Parse("e39d252d-2144-4624-a055-0350c93f6728");
    private static readonly Guid TurnId = Guid.Parse("f77fb13c-263d-49a1-8d93-d908968c5832");

    [TestMethod]
    public void SequenceAcceptsHelloStartGenerationTokensTurnCompletionAndSessionCompletionInThatOrder()
    {
        OpenVinoSessionSequenceValidator validator = new(SessionId);

        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(new SessionStartedEvent(SessionId));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(new TokenEvent(SessionId, TurnId, 0, "first"));
        validator.Accept(new TokenEvent(SessionId, TurnId, 1, "second"));
        validator.Accept(new TurnCompletedEvent(SessionId, TurnId));
        validator.Accept(new SessionCompletedEvent(SessionId));

        Assert.IsTrue(validator.IsTerminal);
    }

    [TestMethod]
    public void SequenceRejectsTokenBeforeGenerationInsteadOfPermittingAnOutOfOrderStream()
    {
        OpenVinoSessionSequenceValidator validator = new(SessionId);
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(new SessionStartedEvent(SessionId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new TokenEvent(SessionId, TurnId, 0, "late")));
    }

    [TestMethod]
    public void SequenceRejectsThirtyThirdTurnInsteadOfRetainingUnboundedConversationState()
    {
        OpenVinoSessionSequenceValidator validator = new(SessionId);
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(new SessionStartedEvent(SessionId));

        for (int turn = 0; turn < OpenVinoProtocol.MaximumTurns; turn++)
        {
            Guid id = Guid.NewGuid();
            validator.Accept(new GenerationStartedEvent(SessionId, id));
            validator.Accept(new TurnCompletedEvent(SessionId, id));
        }

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new GenerationStartedEvent(SessionId, Guid.NewGuid())));
    }

    [TestMethod]
    public void SequenceRejectsStaleSessionUuidInsteadOfApplyingAPreviousOperationEvent()
    {
        OpenVinoSessionSequenceValidator validator = new(SessionId);
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new SessionStartedEvent(Guid.NewGuid())));
    }

    [TestMethod]
    public void SequenceRejectsOperationTextSplitAcrossTurnsInsteadOfResettingTheFourMiBOperationBudget()
    {
        OpenVinoSessionSequenceValidator validator = new(SessionId);
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(new SessionStartedEvent(SessionId));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(new TokenEvent(SessionId, TurnId, 0, new string('a', 3 * 1024 * 1024)));
        validator.Accept(new TurnCompletedEvent(SessionId, TurnId));
        Guid nextTurnId = Guid.NewGuid();
        validator.Accept(new GenerationStartedEvent(SessionId, nextTurnId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new TokenEvent(SessionId, nextTurnId, 0, new string('b', 2 * 1024 * 1024))));
    }
}
