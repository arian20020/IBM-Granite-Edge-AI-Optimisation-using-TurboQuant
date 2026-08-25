using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using System.Reflection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityProductionInputTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    [TestMethod]
    public void ValidProductionInput_ReachesARealDecision()
    {
        CompatibilityScreenModel result = CompatibilityEngine.Run(ValidInput());

        Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, result.State);
        Assert.IsNotNull(result.Setup);
        Assert.AreEqual(4, result.Modes.Count);
    }

    [TestMethod]
    public void ProductionInput_RejectsEmptyOrDuplicateRunIdentities()
    {
        CompatibilityProductionInput valid = ValidInput();

        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityProductionInput.Create(
            Guid.Empty,
            valid.ProductHardwareRunId,
            valid.Model,
            valid.Hardware,
            valid.FreshResources));

        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityProductionInput.Create(
            valid.ModelInspectionRunId,
            valid.ModelInspectionRunId,
            valid.Model,
            valid.Hardware,
            valid.FreshResources));
    }

    [TestMethod]
    public void ModelInput_RejectsZeroLengthAndNonPositiveOptionalFacts()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufCompatibilityModelInput.Create(
                0, 32, 4096, 32, 8, 8192, 15, 2));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufCompatibilityModelInput.Create(
                3 * GiB, 0, 4096, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    public void HardwareInput_RejectsMissingCapacityAndUnknownEnums()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CompatibilityHardwareInput.Create(
                0,
                0,
                500 * GiB,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]));

        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityHardwareInput.Create(
                64 * GiB,
                0,
                500 * GiB,
                [DeviceRouteId.Unspecified],
                [CompatibilityBackend.Cpu]));
    }

    [TestMethod]
    public void InputCollections_AreDefensivelyCopied()
    {
        HashSet<DeviceRouteId> devices = [DeviceRouteId.Cpu];
        HashSet<CompatibilityBackend> backends = [CompatibilityBackend.Cpu];
        CompatibilityHardwareInput input = CompatibilityHardwareInput.Create(
            64 * GiB, 0, 500 * GiB, devices, backends);

        devices.Clear();
        backends.Clear();

        CollectionAssert.Contains(input.PresentDevices.ToArray(), DeviceRouteId.Cpu);
        CollectionAssert.Contains(input.VerifiedBackends.ToArray(), CompatibilityBackend.Cpu);
    }

    [TestMethod]
    public void FreshResources_RequireUtcObservation()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CompatibilityFreshResourcesInput.Create(
                48 * GiB,
                0,
                500 * GiB,
                new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void ProductionBoundary_CarriesNoFreeFormOrPathLikeStrings()
    {
        Type[] boundaryTypes =
        [
            typeof(GgufCompatibilityModelInput),
            typeof(CompatibilityHardwareInput),
            typeof(CompatibilityFreshResourcesInput),
            typeof(CompatibilityProductionInput),
        ];

        foreach (Type type in boundaryTypes)
        {
            PropertyInfo[] strings = type.GetProperties()
                .Where(property => property.PropertyType == typeof(string))
                .ToArray();

            Assert.AreEqual(0, strings.Length, $"{type.Name} carries free-form text.");
        }
    }

    private static CompatibilityProductionInput ValidInput()
    {
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        return CompatibilityProductionInput.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            GgufCompatibilityModelInput.Create(
                3 * GiB,
                32,
                4096,
                32,
                8,
                8192,
                15,
                2),
            CompatibilityHardwareInput.Create(
                64 * GiB,
                0,
                500 * GiB,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]),
            CompatibilityFreshResourcesInput.Create(
                48 * GiB,
                0,
                500 * GiB,
                observedAt));
    }
}
