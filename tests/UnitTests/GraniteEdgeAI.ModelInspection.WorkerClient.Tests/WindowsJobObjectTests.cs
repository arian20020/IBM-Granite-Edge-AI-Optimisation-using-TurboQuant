using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Exercises a real empty Windows Job Object. These tests prove the primitive
/// is created before any process and is configured to terminate every contained
/// process when its owning handle closes.
/// </summary>
[TestClass]
public sealed class WindowsJobObjectTests
{
    [TestMethod]
    public void CreateKillOnCloseSetsOnlyTheRequiredContainmentFlag()
    {
        using WindowsJobObject job = WindowsJobObject.CreateKillOnClose();

        JobObjectExtendedLimitInformation limits = job.QueryExtendedLimits();

        Assert.AreNotEqual(
            0u,
            limits.BasicLimitInformation.LimitFlags &
                NativeConstants.JobObjectLimitKillOnJobClose);
        Assert.AreEqual(
            0u,
            limits.BasicLimitInformation.LimitFlags &
                NativeConstants.JobObjectLimitBreakawayOk);
        Assert.AreEqual(
            0u,
            limits.BasicLimitInformation.LimitFlags &
                NativeConstants.JobObjectLimitSilentBreakawayOk);
    }

    [TestMethod]
    public void NewJobReportsZeroActiveProcesses()
    {
        using WindowsJobObject job = WindowsJobObject.CreateKillOnClose();

        JobObjectBasicAccountingInformation accounting =
            job.QueryBasicAccounting();

        Assert.AreEqual(0u, accounting.ActiveProcesses);
        Assert.AreEqual(0u, accounting.TotalProcesses);
    }

    [TestMethod]
    public void TerminatingAnEmptyJobRemainsBoundedAndEmpty()
    {
        using WindowsJobObject job = WindowsJobObject.CreateKillOnClose();

        job.Terminate(exitCode: 73);
        JobObjectBasicAccountingInformation accounting =
            job.QueryBasicAccounting();

        Assert.AreEqual(0u, accounting.ActiveProcesses);
    }
}
