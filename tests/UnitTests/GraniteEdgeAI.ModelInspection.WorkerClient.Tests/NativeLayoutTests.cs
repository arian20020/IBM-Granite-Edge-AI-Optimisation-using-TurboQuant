using System.Reflection;
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
            ReadUInt32Constant(nameof(NativeConstants.ExtendedStartupInfoPresent)) |
            ReadUInt32Constant(nameof(NativeConstants.CreateNoWindow)) |
            ReadUInt32Constant(nameof(NativeConstants.CreateUnicodeEnvironment));
        uint actualCreationFlags =
            ReadUInt32Constant(nameof(NativeConstants.RequiredCreationFlags));
        uint breakawayFlag =
            ReadUInt32Constant(nameof(NativeConstants.CreateBreakawayFromJob));

        Assert.AreEqual(expectedCreationFlags, actualCreationFlags);
        Assert.AreEqual(0u, actualCreationFlags & breakawayFlag);
        Assert.AreEqual(
            (nuint)0x00020002,
            ReadNativeUIntField(
                nameof(NativeConstants.ProcThreadAttributeHandleList)));
        Assert.AreEqual(
            (nuint)0x0002000D,
            ReadNativeUIntField(
                nameof(NativeConstants.ProcThreadAttributeJobList)));
        Assert.AreEqual(
            0x00002000u,
            ReadUInt32Constant(
                nameof(NativeConstants.JobObjectLimitKillOnJobClose)));
        Assert.AreEqual(
            0x00000100u,
            ReadUInt32Constant(nameof(NativeConstants.StartUseStandardHandles)));
        Assert.AreEqual(
            0x00000001u,
            ReadUInt32Constant(nameof(NativeConstants.HandleFlagInherit)));
        Assert.AreEqual(
            259u,
            ReadUInt32Constant(nameof(NativeConstants.StillActive)));
    }

    private static int OffsetOf<T>(string fieldName)
        where T : struct => checked((int)Marshal.OffsetOf<T>(fieldName));

    /// <summary>
    /// Reads a constant through reflection so the assertion observes the built
    /// assembly at runtime instead of being folded into an always-true compile-
    /// time comparison.
    /// </summary>
    private static uint ReadUInt32Constant(string fieldName)
    {
        FieldInfo field = typeof(NativeConstants).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"The native constant '{fieldName}' was not found.");
        return (uint)(field.GetRawConstantValue()
            ?? throw new InvalidOperationException(
                $"The native constant '{fieldName}' has no value."));
    }

    /// <summary>
    /// Reads a pointer-sized static field through reflection for the same
    /// runtime-observation reason as <see cref="ReadUInt32Constant"/>.
    /// </summary>
    private static nuint ReadNativeUIntField(string fieldName)
    {
        FieldInfo field = typeof(NativeConstants).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"The native field '{fieldName}' was not found.");
        return (nuint)(field.GetValue(null)
            ?? throw new InvalidOperationException(
                $"The native field '{fieldName}' has no value."));
    }
}
