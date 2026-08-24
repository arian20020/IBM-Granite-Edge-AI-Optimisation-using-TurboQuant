using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Ports;

[TestClass]
public sealed class UnavailablePortTests
{
    [TestMethod]
    public void ModelFactsSource_ReturnsUnavailableWithAStableReason()
    {
        ModelFactsResolution resolution = UnavailablePorts.ModelFacts().Resolve("run-1");

        Assert.IsFalse(resolution.IsEstablished);
        Assert.IsNull(resolution.Facts);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), resolution.Reason.ToString());
    }

    [TestMethod]
    public void HardwareFactsSource_ReturnsUnavailableWithAStableReason()
    {
        HardwareFactsResolution resolution = UnavailablePorts.HardwareFacts().Resolve("run-1");

        Assert.IsFalse(resolution.IsEstablished);
        Assert.IsNull(resolution.Facts);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), resolution.Reason.ToString());
    }

    [TestMethod]
    public void MemoryProbe_ReturnsUnavailableRatherThanAZeroReading()
    {
        // A zero reading would be indistinguishable from a machine with no free
        // memory, and the gate would refuse for the wrong reason.
        FreshMemoryReading reading = UnavailablePorts.MemoryProbe().Probe();

        Assert.IsFalse(reading.IsEstablished);
        Assert.IsNull(reading.Resources);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), reading.Reason.ToString());
    }

    [TestMethod]
    public void Gateway_RefusesToClaim()
    {
        HandoffClaim claim = UnavailablePorts.Gateway().Claim();

        Assert.IsFalse(claim.IsClaimed);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), claim.Reason.ToString());
    }

    [TestMethod]
    public void Gateway_RefusesToCommit()
    {
        Assert.IsFalse(UnavailablePorts.Gateway().Commit(CompatibilityRunId.New()));
    }

    [TestMethod]
    public void Gateway_RollbackIsSafeToCallWhenNothingWasClaimed()
    {
        // Rollback must be callable on the failure path without needing to know
        // whether a claim succeeded, or every caller grows the same conditional.
        UnavailablePorts.Gateway().Rollback();
    }

    [TestMethod]
    public void VerificationRunner_ReportsNotRegistered()
    {
        // Runtime verification is implemented by another team, never by C1.
        VerificationOutcome outcome = UnavailablePorts.VerificationRunner().Verify(
            CompatibilityRunId.New());

        Assert.IsFalse(outcome.IsEstablished);
        Assert.AreEqual(
            nameof(PortUnavailableReason.RunnerNotRegistered), outcome.Reason.ToString());
    }

    [TestMethod]
    public void HardwareFacts_ProjectPresenceAndVerificationSeparately()
    {
        // A device being present is not the same as its backend being verified.
        HardwareFacts facts = HardwareFacts.Create(
            installedSystemMemory: ByteCount.FromBytes(32UL * 1024 * 1024 * 1024),
            installedDedicatedDeviceMemory: ByteCount.FromBytes(8UL * 1024 * 1024 * 1024),
            freeStorage: ByteCount.FromBytes(500UL * 1024 * 1024 * 1024),
            presentDevices: new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            verifiedBackends: new HashSet<CompatibilityBackend>());

        Assert.IsTrue(facts.PresentDevices.Contains(DeviceRouteId.Cpu));
        Assert.AreEqual(0, facts.VerifiedBackends.Count);
    }

    [TestMethod]
    public void HardwareFacts_CopyItsSetsSoLaterMutationCannotChangeThem()
    {
        HashSet<DeviceRouteId> devices = [DeviceRouteId.Cpu];

        HardwareFacts facts = HardwareFacts.Create(
            ByteCount.FromBytes(1024),
            ByteCount.Zero,
            ByteCount.FromBytes(1024),
            devices,
            new HashSet<CompatibilityBackend>());

        devices.Add(DeviceRouteId.IntelDiscreteGpu);

        Assert.AreEqual(1, facts.PresentDevices.Count);
    }

    [TestMethod]
    public void HardwareFacts_RejectsZeroInstalledSystemMemory()
    {
        // Zero installed RAM is not a machine; it is a failed read.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => HardwareFacts.Create(
                ByteCount.Zero,
                ByteCount.Zero,
                ByteCount.FromBytes(1024),
                new HashSet<DeviceRouteId>(),
                new HashSet<CompatibilityBackend>()));
    }

    [TestMethod]
    public void EveryUnavailableReason_IsDistinct()
    {
        PortUnavailableReason[] values = Enum.GetValues<PortUnavailableReason>();

        Assert.AreEqual(values.Length, values.Distinct().Count());
    }
}
