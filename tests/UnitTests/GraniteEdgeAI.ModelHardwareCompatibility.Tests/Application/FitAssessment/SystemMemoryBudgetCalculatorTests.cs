using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class SystemMemoryBudgetCalculatorTests
{
    private const ulong MiB = 1024UL * 1024;
    private const ulong GiB = 1024UL * MiB;

    [TestMethod]
    [DataRow(0UL, 0UL, 0UL)]
    [DataRow(1UL, 1UL, 0UL)]
    [DataRow(512UL * MiB, 512UL * MiB, 0UL)]
    [DataRow(4UL * GiB, 512UL * MiB, 3584UL * MiB)]
    [DataRow(20UL * GiB, 2UL * GiB, 18UL * GiB)]
    public void Calculate_ProducesOneBoundedCoherentBudget(
        ulong availableBytes,
        ulong expectedReserveBytes,
        ulong expectedExecutableBytes)
    {
        AvailableMemorySafetyBudget result = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes(availableBytes));

        Assert.AreEqual(availableBytes, result.Available.Bytes);
        Assert.AreEqual(expectedReserveBytes, result.Reserve.Bytes);
        Assert.AreEqual(expectedExecutableBytes, result.Executable.Bytes);
        Assert.AreEqual(availableBytes, checked(result.Reserve.Bytes + result.Executable.Bytes));
    }

    [TestMethod]
    public void Calculate_RoundsTheTenPercentReserveUpward()
    {
        AvailableMemorySafetyBudget result = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes((5 * GiB) + 1));

        Assert.AreEqual((GiB / 2) + 1, result.Reserve.Bytes);
    }

    [TestMethod]
    public void Calculate_UlongMaximum_IsOverflowSafeAndBounded()
    {
        AvailableMemorySafetyBudget result = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes(ulong.MaxValue));

        Assert.AreEqual(1_844_674_407_370_955_162UL, result.Reserve.Bytes);
        Assert.AreEqual(ulong.MaxValue - result.Reserve.Bytes, result.Executable.Bytes);
    }

    [TestMethod]
    public void PublicFactories_RequireRoleSpecificMemoryTypes()
    {
        Type[] hardwareParameters = typeof(CompatibilityHardwareInput)
            .GetMethods()
            .Single(method => method.Name == nameof(CompatibilityHardwareInput.Create))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Type[] freshParameters = typeof(CompatibilityFreshResourcesInput)
            .GetMethods()
            .Single(method => method.Name == nameof(CompatibilityFreshResourcesInput.Create))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.AreEqual(typeof(TotalPhysicalMemory), hardwareParameters[0]);
        Assert.AreEqual(typeof(CurrentlyAvailableMemory), freshParameters[0]);
        Assert.AreNotEqual(hardwareParameters[0], freshParameters[0]);
    }

    [TestMethod]
    public void HardwareInput_RejectsTheDefaultTotalMemoryValue()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CompatibilityHardwareInput.Create(
                default,
                0,
                1,
                [GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain.DeviceRouteId.Cpu],
                [GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain.CompatibilityBackend.Cpu]));
    }
}
