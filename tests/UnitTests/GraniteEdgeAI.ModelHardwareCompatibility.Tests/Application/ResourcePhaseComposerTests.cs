using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class ResourcePhaseComposerTests
{
    private static ResourceComponent Component(
        ResourceComponentKind kind,
        ResourceTarget target,
        ulong bytes,
        params LifecyclePhase[] phases) =>
        ResourceComponent.Create(
            kind,
            target,
            ByteCount.FromBytes(bytes),
            new HashSet<LifecyclePhase>(phases));

    [TestMethod]
    public void Peak_IsMaximumPhaseSum_NotTotalOfAllComponents()
    {
        // Staging exists only during Load; the KV cache only during generation.
        // Summing everything would overstate the requirement by 300 bytes.
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.Load, LifecyclePhase.SteadyStateGeneration),
            Component(ResourceComponentKind.StagingBuffer, ResourceTarget.SystemMemory, 300,
                LifecyclePhase.Load),
            Component(ResourceComponentKind.KvCache, ResourceTarget.SystemMemory, 500,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.AreEqual(1500UL, profile.PeakFor(ResourceTarget.SystemMemory).Bytes);
    }

    [TestMethod]
    public void Targets_AreNeverSummedTogether()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.SteadyStateGeneration),
            Component(ResourceComponentKind.Weights, ResourceTarget.DedicatedDeviceMemory, 700,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.AreEqual(1000UL, profile.PeakFor(ResourceTarget.SystemMemory).Bytes);
        Assert.AreEqual(700UL, profile.PeakFor(ResourceTarget.DedicatedDeviceMemory).Bytes);
    }

    [TestMethod]
    public void SharedDeviceMemory_CountsAgainstSystemPressureExactlyOnce()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.SteadyStateGeneration),
            Component(ResourceComponentKind.KvCache, ResourceTarget.SharedDeviceMemory, 400,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.AreEqual(1400UL, profile.SystemMemoryPressure.Bytes);
        Assert.AreEqual(1000UL, profile.PeakFor(ResourceTarget.SystemMemory).Bytes);
    }

    [TestMethod]
    public void SharedDeviceMemory_NeverIncreasesSystemCapacity()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.KvCache, ResourceTarget.SharedDeviceMemory, 400,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.IsTrue(profile.SystemMemoryPressure >=
            profile.PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void Component_WithNoPhase_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 10));
    }

    [TestMethod]
    public void Component_WithUnspecifiedKind_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Unspecified, ResourceTarget.SystemMemory, 10,
                LifecyclePhase.Load));
    }

    [TestMethod]
    public void Component_WithUnspecifiedTarget_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Weights, ResourceTarget.Unspecified, 10,
                LifecyclePhase.Load));
    }

    [TestMethod]
    public void Component_WithUnspecifiedPhase_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 10,
                LifecyclePhase.Unspecified));
    }

    [TestMethod]
    public void Component_CopiesPhases_SoLaterMutationCannotAlterIt()
    {
        HashSet<LifecyclePhase> phases = [LifecyclePhase.Load];
        ResourceComponent component = ResourceComponent.Create(
            ResourceComponentKind.Weights,
            ResourceTarget.SystemMemory,
            ByteCount.FromBytes(10),
            phases);

        phases.Add(LifecyclePhase.SteadyStateGeneration);

        Assert.AreEqual(1, component.Phases.Count);
    }

    [TestMethod]
    public void AddingALiveComponent_CanNeverReduceItsPhaseRequirement()
    {
        ResourceComponent[] baseline =
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.SteadyStateGeneration),
        ];

        ResourcePeakProfile before = ResourcePhaseComposer.Compose(baseline);
        ResourcePeakProfile after = ResourcePhaseComposer.Compose(
        [
            .. baseline,
            Component(ResourceComponentKind.ComputeBuffer, ResourceTarget.SystemMemory, 1,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.IsTrue(after.PeakFor(ResourceTarget.SystemMemory) >
            before.PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void Compose_IsOrderIndependent()
    {
        ResourceComponent a = Component(ResourceComponentKind.Weights,
            ResourceTarget.SystemMemory, 1000, LifecyclePhase.Load);
        ResourceComponent b = Component(ResourceComponentKind.KvCache,
            ResourceTarget.SystemMemory, 500, LifecyclePhase.Load);

        Assert.AreEqual(
            ResourcePhaseComposer.Compose([a, b]).PeakFor(ResourceTarget.SystemMemory),
            ResourcePhaseComposer.Compose([b, a]).PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void EmptyComponentSet_ProducesZeroPeaks()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose([]);
        Assert.AreEqual(ByteCount.Zero, profile.PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void Compose_RejectsNullComponentList()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ResourcePhaseComposer.Compose(null!));
    }
}
