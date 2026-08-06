using System.Runtime.InteropServices;
using GraniteEdgeAI.ModelInspection.WorkerClient.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Locks the x64 Windows ABI assumptions before process creation code depends
/// on them. A wrong field offset can redirect handles or corrupt Job Object
/// configuration even when managed code compiles successfully.
/// </summary>
[TestClass]
public sealed class NativeLayoutTests
{
    [TestMethod]
    public void NativeStructureSizesMatchWindowsX64Abi()
    {
        Assert.AreEqual(24, Marshal.SizeOf<SecurityAttributes>());
        Assert.AreEqual(104, Marshal.SizeOf<StartupInfo>());
        Assert.AreEqual(112, Marshal.SizeOf<StartupInfoEx>());
        Assert.AreEqual(24, Marshal.SizeOf<ProcessInformation>());
        Assert.AreEqual(64, Marshal.SizeOf<JobObjectBasicLimitInformation>());
        Assert.AreEqual(48, Marshal.SizeOf<IoCounters>());
        Assert.AreEqual(144, Marshal.SizeOf<JobObjectExtendedLimitInformation>());
        Assert.AreEqual(48, Marshal.SizeOf<JobObjectBasicAccountingInformation>());
    }

    [TestMethod]
    public void StartupAndJobStructureOffsetsMatchWindowsX64Abi()
    {
        Assert.AreEqual(0, OffsetOf<StartupInfo>(nameof(StartupInfo.Size)));
        Assert.AreEqual(8, OffsetOf<StartupInfo>(nameof(StartupInfo.Reserved)));
        Assert.AreEqual(64, OffsetOf<StartupInfo>(nameof(StartupInfo.ShowWindow)));
        Assert.AreEqual(72, OffsetOf<StartupInfo>(nameof(StartupInfo.ReservedBytes)));
        Assert.AreEqual(80, OffsetOf<StartupInfo>(nameof(StartupInfo.StandardInput)));
        Assert.AreEqual(88, OffsetOf<StartupInfo>(nameof(StartupInfo.StandardOutput)));
        Assert.AreEqual(96, OffsetOf<StartupInfo>(nameof(StartupInfo.StandardError)));
        Assert.AreEqual(
            104,
            OffsetOf<StartupInfoEx>(nameof(StartupInfoEx.AttributeList)));

        Assert.AreEqual(
            16,
            OffsetOf<JobObjectBasicLimitInformation>(
                nameof(JobObjectBasicLimitInformation.LimitFlags)));
        Assert.AreEqual(
            64,
            OffsetOf<JobObjectExtendedLimitInformation>(
                nameof(JobObjectExtendedLimitInformation.IoInformation)));
        Assert.AreEqual(
            112,
            OffsetOf<JobObjectExtendedLimitInformation>(
                nameof(JobObjectExtendedLimitInformation.ProcessMemoryLimit)));
        Assert.AreEqual(
            40,
            OffsetOf<JobObjectBasicAccountingInformation>(
                nameof(JobObjectBasicAccountingInformation.ActiveProcesses)));
    }

    [TestMethod]
    public void LockedNativeConstantsContainNoBreakawayFlag()
    {
        uint expectedCreationFlags =
            NativeConstants.ExtendedStartupInfoPresent |
            NativeConstants.CreateNoWindow |
            NativeConstants.CreateUnicodeEnvironment;

        Assert.AreEqual(expectedCreationFlags, NativeConstants.RequiredCreationFlags);
        Assert.AreEqual(
            0u,
            NativeConstants.RequiredCreationFlags &
                NativeConstants.CreateBreakawayFromJob);
        Assert.AreEqual(
            (nuint)0x00020002,
            NativeConstants.ProcThreadAttributeHandleList);
        Assert.AreEqual(
            (nuint)0x0002000D,
            NativeConstants.ProcThreadAttributeJobList);
        Assert.AreEqual(0x00002000u, NativeConstants.JobObjectLimitKillOnJobClose);
        Assert.AreEqual(0x00000100u, NativeConstants.StartUseStandardHandles);
        Assert.AreEqual(0x00000001u, NativeConstants.HandleFlagInherit);
        Assert.AreEqual(259u, NativeConstants.StillActive);
    }

    private static int OffsetOf<T>(string fieldName)
        where T : struct => checked((int)Marshal.OffsetOf<T>(fieldName));
}
