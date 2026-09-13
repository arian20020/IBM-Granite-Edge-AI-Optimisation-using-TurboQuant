using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Specifies the only accepted request-scoped worker conversation after an
/// exact hello: Started, monotonic Progress, and one Completed terminal.
/// </summary>
[TestClass]
public sealed class WorkerConversationTests
{
    [TestMethod]
    public void StartedProgressAndTerminalAreAcceptedInOrder()
    {
        Guid requestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            requestId);
        WorkerProgressMessage progress = WorkerClientTestData.Progress(requestId);
        WorkerCompletedMessage terminal =
            WorkerClientTestData.OperationalFailure(requestId);

        conversation.Accept(WorkerClientTestData.Started(requestId));
        conversation.Accept(progress);
        conversation.Accept(terminal);
        conversation.CompleteOutput();

        Assert.IsTrue(conversation.HasStarted);
        Assert.AreSame(progress, conversation.LastProgress);
        Assert.AreSame(terminal, conversation.TerminalMessage);
    }

    [TestMethod]
    public void ProgressBeforeStartedIsRejected()
    {
        Guid requestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            requestId);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                conversation.Accept(
                    WorkerClientTestData.Progress(requestId)));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            error.Failure.Code);
    }

    [TestMethod]
    public void WrongRequestIdentityIsRejected()
    {
        Guid expectedRequestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            expectedRequestId);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                conversation.Accept(
                    WorkerClientTestData.Started(Guid.NewGuid())));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            error.Failure.Code);
        Assert.IsFalse(
            error.ToString().Contains(
                expectedRequestId.ToString(),
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void BackwardProgressIsRejected()
    {
        Guid requestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            requestId);
        conversation.Accept(WorkerClientTestData.Started(requestId));
        conversation.Accept(
            WorkerClientTestData.Progress(
                requestId,
                WorkerStage.ValidateModelStructure,
                completedStageCount: 3));

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                conversation.Accept(
                    WorkerClientTestData.Progress(
                        requestId,
                        WorkerStage.ReadModelConfiguration,
                        completedStageCount: 1)));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            error.Failure.Code);
    }

    [TestMethod]
    public void DuplicateTerminalIsRejected()
    {
        Guid requestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            requestId);
        conversation.Accept(WorkerClientTestData.Started(requestId));
        conversation.Accept(
            WorkerClientTestData.OperationalFailure(requestId));

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                conversation.Accept(
                    WorkerClientTestData.OperationalFailure(requestId)));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            error.Failure.Code);
    }

    [TestMethod]
    public void EofBeforeTerminalIsRejected()
    {
        Guid requestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            requestId);
        conversation.Accept(WorkerClientTestData.Started(requestId));

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(
                conversation.CompleteOutput);

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            error.Failure.Code);
    }

    [TestMethod]
    public void UnknownMessageTypeIsRejected()
    {
        Guid requestId = Guid.NewGuid();
        WorkerConversation conversation = new(
            WorkerClientTestData.Hello(),
            requestId);

        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                conversation.Accept(new object()));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            error.Failure.Code);
    }
}
