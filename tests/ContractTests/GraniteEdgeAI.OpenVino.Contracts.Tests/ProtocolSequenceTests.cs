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
    private const string ModelDigest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string WorkerDigest = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string PackagePath = @"C:\operation\package";

    [TestMethod]
    public void ConversationAcceptsExactlyOneStartSessionThenOrderedPromptGenerationAndTerminalEvents()
    {
        OpenVinoConversationValidator validator = new();

        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));
        validator.Accept(StartSession());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() => validator.Accept(StartSession()));
        validator.Accept(SessionStarted());
        validator.Accept(new PromptCommand(SessionId, TurnId, "first user prompt", 128));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(new TokenEvent(SessionId, TurnId, 0, "first"));
        validator.Accept(new TokenEvent(SessionId, TurnId, 1, "second"));
        validator.Accept(TurnCompleted(TurnId, generatedTokenCount: 2));
        validator.Accept(new CloseSessionCommand(SessionId));
        validator.Accept(new SessionCompletedEvent(SessionId));

        Assert.IsTrue(validator.IsTerminal);
    }

    [TestMethod]
    public void ConversationAcceptsOneTextFragmentForFixtureTokenAndAuthoritativeEosCount()
    {
        OpenVinoConversationValidator validator = StartedSession();
        validator.Accept(new PromptCommand(SessionId, TurnId, "hello", 2));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        validator.Accept(new TokenEvent(SessionId, TurnId, 0, "fixture"));
        validator.Accept(TurnCompleted(TurnId, generatedTokenCount: 2));
    }

    [TestMethod]
    public void ConversationRejectsAuthoritativeGeneratedCountAbovePromptRequest()
    {
        OpenVinoConversationValidator validator = StartedSession();
        validator.Accept(new PromptCommand(SessionId, TurnId, "hello", 1));
        validator.Accept(new GenerationStartedEvent(SessionId, TurnId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(TurnCompleted(TurnId, generatedTokenCount: 2)));
    }

    [TestMethod]
    public void TokenEventRejectsEmptyTextSoEosNeverCreatesAFrame()
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            new TokenEvent(SessionId, TurnId, 0, string.Empty).Validate());
    }

    [TestMethod]
    public void ConversationRejectsSessionStartWithoutExactlyOnePrecedingStartCommand()
    {
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(OpenVinoProtocol.OfficialProtocolId));

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(SessionStarted()));
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
        validator.Accept(StartInspection());
        validator.Accept(new InspectionStartedEvent(RunId));
        validator.Accept(new InspectionProgressEvent(RunId, OpenVinoInspectionStage.ManifestVerified));
        validator.Accept(new InspectionProgressEvent(RunId, OpenVinoInspectionStage.MainModelParsed));
        validator.Accept(new InspectionProgressEvent(RunId, OpenVinoInspectionStage.TokenizerParsed));
        validator.Accept(new InspectionProgressEvent(RunId, OpenVinoInspectionStage.DetokenizerParsed));
        validator.Accept(InspectionCompleted());

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
        validator.Accept(TurnCompleted(
            TurnId,
            OpenVinoTurnDisposition.Stopped,
            generatedTokenCount: 0));
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
            validator.Accept(TurnCompleted(id, generatedTokenCount: 0));
        }

        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            validator.Accept(new PromptCommand(SessionId, Guid.NewGuid(), "turn thirty three", 1)));

        OpenVinoConversationValidator textValidator = StartedSession();
        textValidator.Accept(new PromptCommand(SessionId, TurnId, "first", 1));
        textValidator.Accept(new GenerationStartedEvent(SessionId, TurnId));
        textValidator.Accept(new TokenEvent(SessionId, TurnId, 0, new string('a', 3 * 1024 * 1024)));
        textValidator.Accept(TurnCompleted(TurnId));
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
        validator.Accept(SessionStarted());
        return validator;
    }

    private static StartSessionCommand StartSession() => new(
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

    private static SessionStartedEvent SessionStarted() => new(
        SessionId,
        "CPU",
        ["CPU"],
        OpenVinoProtocol.OfficialProtocolId,
        BuildEvidence());

    private static TurnCompletedEvent TurnCompleted(
        Guid turnId,
        OpenVinoTurnDisposition disposition = OpenVinoTurnDisposition.Completed,
        long generatedTokenCount = 1) =>
        new(SessionId, turnId, 1, generatedTokenCount, disposition);

    private static InspectionCompletedEvent InspectionCompleted() => new(
        RunId,
        Digest,
        ModelDigest,
        88,
        true,
        true,
        true,
        BuildEvidence());
}
