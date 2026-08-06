using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Specifies the exact agreement required between a terminal message, process
/// exit code, and whether the parent had to terminate the Job Object.
/// </summary>
[TestClass]
public sealed class WorkerExitConsistencyValidatorTests
{
    [TestMethod]
    [DataRow(WorkerCompletionStatus.Completed, 0, false, true)]
    [DataRow(WorkerCompletionStatus.Cancelled, 3, false, true)]
    [DataRow(WorkerCompletionStatus.OperationalFailure, 4, false, true)]
    [DataRow(WorkerCompletionStatus.Completed, 4, false, false)]
    [DataRow(WorkerCompletionStatus.Cancelled, 0, false, false)]
    [DataRow(WorkerCompletionStatus.OperationalFailure, 3, false, false)]
    [DataRow(WorkerCompletionStatus.Completed, 0, true, false)]
    [DataRow(WorkerCompletionStatus.Cancelled, 3, true, false)]
    [DataRow(WorkerCompletionStatus.OperationalFailure, 4, true, false)]
    public void TerminalStatusMustMatchExactExitAndNoForcedTermination(
        WorkerCompletionStatus status,
        int exitCode,
        bool forcedTermination,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            WorkerExitConsistencyValidator.IsConsistent(
                status,
                exitCode,
                forcedTermination));
    }

    [TestMethod]
    public void ProtocolMisuseExitIsNeverATrustedTerminalResult()
    {
        foreach (WorkerCompletionStatus status in
                 Enum.GetValues<WorkerCompletionStatus>())
        {
            Assert.IsFalse(
                WorkerExitConsistencyValidator.IsConsistent(
                    status,
                    exitCode: 2,
                    forcedTermination: false),
                status.ToString());
        }
    }
}
