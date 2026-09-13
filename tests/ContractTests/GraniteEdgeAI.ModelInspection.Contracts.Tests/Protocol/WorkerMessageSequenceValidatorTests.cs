using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol;

/// <summary>
/// Defines the legal message order accepted from one short-lived worker.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class WorkerMessageSequenceValidatorTests
{
    private static readonly Guid RequestId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [TestMethod]
    public void NewValidator_ExposesAwaitingHelloState()
    {
        WorkerMessageSequenceValidator validator = new();

        Assert.IsFalse(validator.HasHello);
        Assert.IsFalse(validator.HasExpectedRequest);
        Assert.IsFalse(validator.HasStarted);
        Assert.IsFalse(validator.IsTerminal);
        Assert.IsNull(validator.ExpectedRequestId);
        Assert.IsNull(validator.LastStage);
        Assert.AreEqual(0, validator.CompletedStageCount);
    }

    [TestMethod]
    public void StartedBeforeHello_Throws()
    {
        WorkerMessageSequenceValidator validator = new();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptStarted(CreateStarted(RequestId)));
    }

    [TestMethod]
    public void ProgressBeforeHello_Throws()
    {
        WorkerMessageSequenceValidator validator = new();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptProgress(CreateProgress(
                RequestId,
                WorkerStage.CheckModelPackage,
                completed: 0)));
    }

    [TestMethod]
    public void CompletedBeforeHello_Throws()
    {
        WorkerMessageSequenceValidator validator = new();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptCompleted(CreateCancelled(RequestId)));
    }

    [TestMethod]
    public void SecondHello_Throws()
    {
        WorkerMessageSequenceValidator validator = new();
        WorkerHelloMessage hello = TestJson.CreateValidHello();
        validator.AcceptHello(hello);

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptHello(hello));
    }

    [TestMethod]
    public void SetExpectedRequestBeforeHello_Throws()
    {
        WorkerMessageSequenceValidator validator = new();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.SetExpectedRequest(RequestId));
    }

    [TestMethod]
    public void RequestScopedOutputBeforeExpectedRequest_Throws()
    {
        WorkerMessageSequenceValidator validator = new();
        validator.AcceptHello(TestJson.CreateValidHello());

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptStarted(CreateStarted(RequestId)));
    }

    [TestMethod]
    public void SecondExpectedRequest_Throws()
    {
        WorkerMessageSequenceValidator validator = new();
        validator.AcceptHello(TestJson.CreateValidHello());
        validator.SetExpectedRequest(RequestId);

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.SetExpectedRequest(RequestId));
    }

    [TestMethod]
    public void WrongRequestId_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateAwaitingStartedValidator();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptStarted(CreateStarted(
                Guid.Parse("22222222-2222-2222-2222-222222222222"))));
    }

    [TestMethod]
    public void ProgressBeforeStarted_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateAwaitingStartedValidator();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptProgress(CreateProgress(
                RequestId,
                WorkerStage.CheckModelPackage,
                completed: 0)));
    }

    [TestMethod]
    public void SecondStarted_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptStarted(CreateStarted(RequestId)));
    }

    [TestMethod]
    public void BackwardStage_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();
        validator.AcceptProgress(CreateProgress(
            RequestId,
            WorkerStage.ValidateModelStructure,
            completed: 3));

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptProgress(CreateProgress(
                RequestId,
                WorkerStage.ReadModelConfiguration,
                completed: 1)));
    }

    [TestMethod]
    public void DecreasingCompletedCount_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();
        validator.AcceptProgress(CreateProgress(
            RequestId,
            WorkerStage.ValidateTokenizerAndChatSetup,
            completed: 3,
            status: WorkerStageStatus.Completed));

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptProgress(CreateProgress(
                RequestId,
                WorkerStage.ValidateTokenizerAndChatSetup,
                completed: 2,
                status: WorkerStageStatus.Active)));
    }

    [TestMethod]
    public void DuplicateTerminal_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();
        validator.AcceptCompleted(CreateCancelled(RequestId));

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptCompleted(CreateCancelled(RequestId)));
    }

    [TestMethod]
    public void OutputAfterTerminal_Throws()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();
        validator.AcceptCompleted(CreateCancelled(RequestId));

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptProgress(CreateProgress(
                RequestId,
                WorkerStage.CheckModelPackage,
                completed: 0)));
    }

    [TestMethod]
    public void CompletedAfterStartedWithoutProgress_IsAccepted()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();

        validator.AcceptCompleted(CreateCancelled(RequestId));

        Assert.IsTrue(validator.IsTerminal);
    }

    [TestMethod]
    public void ValidFlow_AcceptsMonotonicProgressAndCompletion()
    {
        WorkerMessageSequenceValidator validator = CreateRunningValidator();

        validator.AcceptProgress(CreateProgress(
            RequestId,
            WorkerStage.CheckModelPackage,
            completed: 0));
        validator.AcceptProgress(CreateProgress(
            RequestId,
            WorkerStage.ReadModelConfiguration,
            completed: 1));
        validator.AcceptProgress(CreateProgress(
            RequestId,
            WorkerStage.ReadModelConfiguration,
            completed: 1));
        validator.AcceptProgress(CreateProgress(
            RequestId,
            WorkerStage.ConfirmCoreRuntimeCompatibility,
            completed: 4));
        validator.AcceptCompleted(CreateCancelled(RequestId));

        Assert.IsTrue(validator.HasHello);
        Assert.IsTrue(validator.HasExpectedRequest);
        Assert.IsTrue(validator.HasStarted);
        Assert.IsTrue(validator.IsTerminal);
        Assert.IsTrue(validator.ExpectedRequestId.HasValue);
        Assert.IsTrue(validator.ExpectedRequestId.Value == RequestId);
        Assert.AreEqual(
            WorkerStage.ConfirmCoreRuntimeCompatibility,
            validator.LastStage);
        Assert.AreEqual(4, validator.CompletedStageCount);
    }

    private static WorkerMessageSequenceValidator CreateAwaitingStartedValidator()
    {
        WorkerMessageSequenceValidator validator = new();
        validator.AcceptHello(TestJson.CreateValidHello());
        validator.SetExpectedRequest(RequestId);
        return validator;
    }

    private static WorkerMessageSequenceValidator CreateRunningValidator()
    {
        WorkerMessageSequenceValidator validator =
            CreateAwaitingStartedValidator();
        validator.AcceptStarted(CreateStarted(RequestId));
        return validator;
    }

    private static WorkerStartedMessage CreateStarted(Guid requestId)
    {
        return new WorkerStartedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Started,
            RequestId = requestId
        };
    }

    private static WorkerProgressMessage CreateProgress(
        Guid requestId,
        WorkerStage stage,
        int completed,
        WorkerStageStatus status = WorkerStageStatus.Active)
    {
        return new WorkerProgressMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Progress,
            RequestId = requestId,
            Stage = stage,
            StageStatus = status,
            CompletedStageCount = completed,
            TotalStageCount = 5,
            StageFraction = null
        };
    }

    private static WorkerCompletedMessage CreateCancelled(Guid requestId)
    {
        return new WorkerCompletedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = requestId,
            CompletionStatus = WorkerCompletionStatus.Cancelled,
            Evidence = null,
            OperationalFailure = null
        };
    }
}
