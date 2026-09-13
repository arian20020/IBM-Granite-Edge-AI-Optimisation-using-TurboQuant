using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol;

/// <summary>
/// Defines the legal command order accepted by one short-lived worker.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class WorkerCommandSequenceValidatorTests
{
    [TestMethod]
    public void NewValidator_ExposesAwaitingStartState()
    {
        WorkerCommandSequenceValidator validator = new();

        Assert.IsFalse(validator.HasStarted);
        Assert.IsFalse(validator.CancellationRequested);
        Assert.IsFalse(validator.IsTerminal);
        Assert.IsNull(validator.RequestId);
    }

    [TestMethod]
    public void CancelBeforeStart_Throws()
    {
        WorkerCommandSequenceValidator validator = new();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptCancel(TestJson.CreateValidCancel()));
    }

    [TestMethod]
    public void SecondStart_Throws()
    {
        WorkerCommandSequenceValidator validator = new();
        WorkerStartInspectionCommand start = TestJson.CreateValidStart();
        validator.AcceptStart(start);

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptStart(start));
    }

    [TestMethod]
    public void MatchingRepeatedCancel_IsIdempotent()
    {
        WorkerCommandSequenceValidator validator = CreateRunningValidator();
        WorkerCancelInspectionCommand cancel = TestJson.CreateValidCancel();

        validator.AcceptCancel(cancel);
        validator.AcceptCancel(cancel);

        Assert.IsTrue(validator.CancellationRequested);
        Assert.IsFalse(validator.IsTerminal);
    }

    [TestMethod]
    public void CancelWithWrongRequestId_Throws()
    {
        WorkerCommandSequenceValidator validator = CreateRunningValidator();
        WorkerCancelInspectionCommand cancel = TestJson.CreateValidCancel() with
        {
            RequestId = Guid.Parse("22222222-2222-2222-2222-222222222222")
        };

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptCancel(cancel));
    }

    [TestMethod]
    public void MarkTerminalBeforeStart_Throws()
    {
        WorkerCommandSequenceValidator validator = new();

        Assert.ThrowsExactly<WorkerProtocolException>(validator.MarkTerminal);
    }

    [TestMethod]
    public void CommandAfterTerminal_Throws()
    {
        WorkerCommandSequenceValidator validator = CreateRunningValidator();
        validator.MarkTerminal();

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            validator.AcceptCancel(TestJson.CreateValidCancel()));
    }

    [TestMethod]
    public void SecondTerminalMark_Throws()
    {
        WorkerCommandSequenceValidator validator = CreateRunningValidator();
        validator.MarkTerminal();

        Assert.ThrowsExactly<WorkerProtocolException>(validator.MarkTerminal);
    }

    [TestMethod]
    public void ValidStart_ExposesRunningRequestIdentity()
    {
        WorkerCommandSequenceValidator validator = new();
        WorkerStartInspectionCommand start = TestJson.CreateValidStart();

        validator.AcceptStart(start);

        Assert.IsTrue(validator.HasStarted);
        Assert.AreEqual(start.RequestId, validator.RequestId);
        Assert.IsFalse(validator.CancellationRequested);
        Assert.IsFalse(validator.IsTerminal);
    }

    private static WorkerCommandSequenceValidator CreateRunningValidator()
    {
        WorkerCommandSequenceValidator validator = new();
        validator.AcceptStart(TestJson.CreateValidStart());
        return validator;
    }
}
