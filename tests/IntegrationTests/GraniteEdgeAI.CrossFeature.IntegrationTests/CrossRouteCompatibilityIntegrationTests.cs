using System.Reflection;

using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class CrossRouteCompatibilityIntegrationTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void CurrentModelFitAllowsDirectChat(int route)
    {
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            CreateRouteInput(route, 48 * GiB, 64 * GiB),
            new FixedTimeProvider(Now));

        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible,
            evaluation.Screen.State);
        Assert.IsTrue(evaluation.Screen.UseCurrentModelAvailable);
        Assert.IsTrue(evaluation.Screen.ContinueEnabled);
        Assert.IsNotNull(evaluation.Screen.CurrentSetup);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void OnlyAdmittedAlternativeFitRequiresOptimization(int route)
    {
        OptimizationExecutionPlan plan = route == 0
            ? CrossFeaturePlanFixture.PersistentGgufPlan(
                CrossFeaturePlanFixture.ModelDigest, 4 * GiB)
            : CrossFeaturePlanFixture.Issue();

        Assert.AreEqual(route == 0 ? OptimizationRoute.Gguf : OptimizationRoute.OpenVino,
            plan.Route);
        Assert.IsTrue(plan.IsExecutableBy(OptimizationExecutionPlan.CurrentContractVersion));
        Assert.IsTrue(plan.MatchesCapability(plan.CapabilitySnapshot));
        Assert.IsTrue(plan.Candidate.Metrics.FitsSafely);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void NoSafeConfigurationDisablesExecution(int route)
    {
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            CreateRouteInput(route, GiB / 2, GiB),
            new FixedTimeProvider(Now));

        Assert.AreEqual(CompatibilityScreenState.NoEstimatedSafeConfiguration,
            evaluation.Screen.State);
        Assert.IsFalse(evaluation.Screen.UseCurrentModelAvailable);
        Assert.IsFalse(evaluation.Screen.ContinueEnabled);
    }

    [TestMethod]
    public void InstalledAvailableReserveAndExecutableBudgetRemainDistinct()
    {
        CompatibilityMachineMemory memory = CompatibilityMachineMemory.Create(
            16 * GiB, 10 * GiB, GiB, 9 * GiB);

        Assert.AreEqual(16 * GiB, memory.InstalledSystemMemoryBytes);
        Assert.AreEqual(10 * GiB, memory.AvailableSystemMemoryBytes);
        Assert.AreEqual(GiB, memory.SafetyReserveBytes);
        Assert.AreEqual(9 * GiB, memory.SafeModelBudgetBytes);
        Assert.AreEqual(
            memory.AvailableSystemMemoryBytes - memory.SafetyReserveBytes,
            memory.SafeModelBudgetBytes);
    }

    [TestMethod]
    [DataRow(1UL, 0UL)]
    [DataRow(1UL, 1UL)]
    [DataRow(1073741824UL, 268435456UL)]
    [DataRow(1073741824UL, 536870912UL)]
    [DataRow(1073741824UL, 536870913UL)]
    [DataRow(8589934592UL, 3221225472UL)]
    [DataRow(17179869184UL, 10737418240UL)]
    [DataRow(34359738368UL, 25769803776UL)]
    [DataRow(6442450944UL, 5368709120UL)]
    [DataRow(6442450944UL, 5368709121UL)]
    [DataRow(ulong.MaxValue, ulong.MaxValue)]
    public void ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative(
        ulong installedBytes,
        ulong availableBytes)
    {
        ulong tenPercentCeiling = availableBytes / 10
            + (availableBytes % 10 == 0 ? 0UL : 1UL);
        ulong expectedReserve = Math.Min(
            availableBytes, Math.Max(tenPercentCeiling, GiB / 2));
        ulong expectedBudget = availableBytes - expectedReserve;

        ulong actualReserve = InvokeReserve(availableBytes);

        Assert.AreEqual(expectedReserve, actualReserve);
        Assert.IsTrue(actualReserve <= availableBytes);
        ulong actualBudget = availableBytes - actualReserve;
        Assert.AreEqual(expectedBudget, actualBudget);
        CompatibilityMachineMemory memory = CompatibilityMachineMemory.Create(
            installedBytes, availableBytes, actualReserve, actualBudget);
        Assert.IsTrue(memory.SafetyReserveBytes <= memory.AvailableSystemMemoryBytes);
    }

    [TestMethod]
    public void InstalledMemoryAloneCannotChangeAnAvailableMemoryReserve()
    {
        ulong available = 10 * GiB;
        ulong reserve = InvokeReserve(available);
        ulong budget = available - reserve;
        CompatibilityMachineMemory first = CompatibilityMachineMemory.Create(
            16 * GiB, available, reserve, budget);
        CompatibilityMachineMemory second = CompatibilityMachineMemory.Create(
            32 * GiB, available, reserve, budget);

        Assert.AreEqual(first.SafetyReserveBytes, second.SafetyReserveBytes);
        Assert.AreEqual(first.SafeModelBudgetBytes, second.SafeModelBudgetBytes);
    }

    [TestMethod]
    public void MachineMemoryRejectsIncoherentBudgets()
    {
        TargetInvocationException absent = Assert.ThrowsExactly<TargetInvocationException>(
            () => InvokeReserve(GiB, absentPolicy: true));
        Assert.IsInstanceOfType<InvalidOperationException>(absent.InnerException);
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityMachineMemory.Create(GiB, GiB / 4, GiB / 2, 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityMachineMemory.Create(GiB, GiB, GiB / 2, GiB));
    }

    [TestMethod]
    public void ZeroAvailableMemoryProjectsAnEstablishedZeroBudgetWithoutThrowing()
    {
        CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(
            ProductionCompatibilityBindingIntegrationTests.CreateInput(
                Now,
                installedSystemMemoryBytes: GiB,
                availableSystemMemoryBytes: 0),
            new FixedTimeProvider(Now));

        Assert.AreEqual(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            evaluation.Screen.State);
        Assert.IsNotNull(evaluation.MachineMemory);
        Assert.AreEqual(0UL, evaluation.MachineMemory.SafetyReserveBytes);
        Assert.AreEqual(0UL, evaluation.MachineMemory.SafeModelBudgetBytes);
    }

    private static CompatibilityProductionInput CreateRouteInput(
        int route, ulong availableBytes, ulong installedBytes)
    {
        if (route == 1)
        {
            return ProductionCompatibilityBindingIntegrationTests.CreateInput(
                Now,
                installedSystemMemoryBytes: installedBytes,
                availableSystemMemoryBytes: availableBytes);
        }

        return CompatibilityProductionInput.Create(
            Guid.Parse("77777777-7777-4777-8777-777777777777"),
            Guid.Parse("88888888-8888-4888-8888-888888888888"),
            GgufCompatibilityModelInput.Create(
                3 * GiB, 32, 4096, 32, 8, 8192, 15, 2),
            CompatibilityHardwareInput.Create(
                installedBytes, 0, 500 * GiB,
                [DeviceRouteId.Cpu], [CompatibilityBackend.Cpu]),
            CompatibilityFreshResourcesInput.Create(
                availableBytes, 0, 500 * GiB, Now));
    }

    private static ulong InvokeReserve(
        ulong availableBytes,
        bool absentPolicy = false)
    {
        Assembly assembly = typeof(CompatibilityEngine).Assembly;
        Type safetyType = assembly.GetType(
            "GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment.SafetyPolicy",
            throwOnError: true)!;
        Type byteCountType = assembly.GetType(
            "GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain.ByteCount",
            throwOnError: true)!;
        const BindingFlags staticFlags = BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags instanceFlags = BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic;
        object policy = safetyType.GetMethod(
            absentPolicy ? "Absent" : "ProportionalV2", staticFlags)!
            .Invoke(null, null)!;
        object available = byteCountType.GetMethod("FromBytes", staticFlags)!
            .Invoke(null, [availableBytes])!;
        object reserve = safetyType.GetMethod(
            "AvailableMemoryReserveFor", instanceFlags)!
            .Invoke(policy, [available])!;
        return (ulong)byteCountType.GetProperty("Bytes", instanceFlags)!
            .GetValue(reserve)!;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
