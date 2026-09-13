using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Capabilities;

[TestClass]
public sealed class CapabilityProjectionTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static HardwareFacts Facts(
        IReadOnlySet<DeviceRouteId>? devices = null,
        IReadOnlySet<CompatibilityBackend>? backends = null) =>
        HardwareFacts.Create(
            ByteCount.FromBytes(32 * Gibibyte),
            ByteCount.FromBytes(8 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            devices ?? new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            backends ?? new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu });

    [TestMethod]
    public void Project_ReportsInstalledAndVerifiedWhenDeviceAndBackendBothCheckOut()
    {
        Assert.AreEqual(
            nameof(InstallationState.InstalledAndVerified),
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), Facts(), new HashSet<string>())
                ["gguf-cpu-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_ReportsNotInstalledWhenTheDeviceIsAbsent()
    {
        // No discrete GPU present, so nothing targeting one can be offered.
        Assert.AreEqual(
            nameof(InstallationState.NotInstalled),
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), Facts(), new HashSet<string>())
                ["gguf-dgpu-sycl-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_ReportsNotInstalledWhenTheDeviceIsPresentButTheBackendIsUnverified()
    {
        // Presence is not verification. Treating it as such is how a run fails at
        // launch rather than at planning
        Assert.AreEqual(
            nameof(InstallationState.NotInstalled),
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(),
                Facts(devices: new HashSet<DeviceRouteId>
                {
                    DeviceRouteId.Cpu, DeviceRouteId.IntelDiscreteGpu
                }),
                new HashSet<string>())
                ["gguf-dgpu-sycl-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_ReportsVerifiedAndOptedInOnlyForAnOptedInExperimentalEntry()
    {
        HardwareFacts facts = Facts(
            devices: new HashSet<DeviceRouteId>
            {
                DeviceRouteId.Cpu, DeviceRouteId.IntelDiscreteGpu
            },
            backends: new HashSet<CompatibilityBackend>
            {
                CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl
            });

        Assert.AreEqual(
            nameof(InstallationState.VerifiedAndOptedIn),
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(),
                facts,
                new HashSet<string> { "gguf-dgpu-sycl-imported-tq3" })
                ["gguf-dgpu-sycl-imported-tq3"].ToString());

        Assert.AreEqual(
            nameof(InstallationState.InstalledAndVerified),
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), facts, new HashSet<string>())
                ["gguf-dgpu-sycl-imported-tq3"].ToString());
    }

    [TestMethod]
    public void Project_IgnoresAnOptInForANonExperimentalEntry()
    {
        // Opting in to something that needs no opt-in must not upgrade it past
        // the check its own support level demands
        Assert.AreEqual(
            nameof(InstallationState.InstalledAndVerified),
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(),
                Facts(),
                new HashSet<string> { "gguf-cpu-imported-f16" })
                ["gguf-cpu-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_CoversEveryEntryInTheMatrix()
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        Assert.AreEqual(
            matrix.Entries.Count,
            CapabilityProjection.Project(matrix, Facts(), new HashSet<string>()).Count);
    }

    [TestMethod]
    public void Project_OverAnAbsentMatrixIsEmptyRatherThanThrowing()
    {
        Assert.AreEqual(
            0,
            CapabilityProjection.Project(
                SupportMatrix.Absent(), Facts(), new HashSet<string>()).Count);
    }

    [TestMethod]
    public void Project_NeverReportsAnUnknownState()
    {
        // Unknown resolves to Unsupported downstream, which would silently drop an
        // entry the machine can in fact run. the projection must always decide
        Assert.IsFalse(
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), Facts(), new HashSet<string>())
                .Values.Any(state => state == InstallationState.Unknown));
    }
}
