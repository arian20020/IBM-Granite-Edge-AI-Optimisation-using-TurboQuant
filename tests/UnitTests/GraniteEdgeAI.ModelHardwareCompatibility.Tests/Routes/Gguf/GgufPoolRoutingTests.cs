using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufPoolRoutingTests
{
    [TestMethod]
    [DataRow(
        nameof(DeviceRouteId.Cpu),
        nameof(GpuOffloadLevel.None),
        nameof(ResourceTarget.SystemMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelIntegratedGpu),
        nameof(GpuOffloadLevel.None),
        nameof(ResourceTarget.SystemMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelIntegratedGpu),
        nameof(GpuOffloadLevel.Full),
        nameof(ResourceTarget.SharedDeviceMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelDiscreteGpu),
        nameof(GpuOffloadLevel.None),
        nameof(ResourceTarget.SystemMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelDiscreteGpu),
        nameof(GpuOffloadLevel.Full),
        nameof(ResourceTarget.DedicatedDeviceMemory),
        true)]
    public void AdmittedPairings_RouteToOnePoolAndDeclareStaging(
        string device,
        string offload,
        string expectedTarget,
        bool expectedStaging)
    {
        bool resolved = GgufPoolRouter.TryResolve(
            Enum.Parse<DeviceRouteId>(device),
            Enum.Parse<GpuOffloadLevel>(offload),
            out GgufPoolRouting routing,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(resolved, $"Expected a routing, got {reason}.");
        Assert.AreEqual(expectedTarget, routing.ModelTarget.ToString());
        Assert.AreEqual(expectedStaging, routing.RequiresHostStaging);
    }

    [TestMethod]
    [DataRow(nameof(DeviceRouteId.Cpu))]
    [DataRow(nameof(DeviceRouteId.IntelIntegratedGpu))]
    [DataRow(nameof(DeviceRouteId.IntelDiscreteGpu))]
    public void PartialOffload_RefusesBecauseNoLayerCountExists(string device)
    {
        // GpuOffloadLevel declares no number of offloaded layers, so there is no
        // split to compute. Charging everything to one pool would understate the
        // other, and the understated pool is where a crash comes from
        bool resolved = GgufPoolRouter.TryResolve(
            Enum.Parse<DeviceRouteId>(device),
            GpuOffloadLevel.Partial,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(resolved);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownOffloadSplit),
            reason.ToString());
    }

    [TestMethod]
    [DataRow(nameof(DeviceRouteId.IntelNpu), nameof(GpuOffloadLevel.Full))]
    [DataRow(nameof(DeviceRouteId.IntelNpu), nameof(GpuOffloadLevel.None))]
    [DataRow(nameof(DeviceRouteId.Unspecified), nameof(GpuOffloadLevel.None))]
    [DataRow(nameof(DeviceRouteId.Cpu), nameof(GpuOffloadLevel.Unspecified))]
    [DataRow(nameof(DeviceRouteId.Cpu), nameof(GpuOffloadLevel.Full))]
    public void UnadmittedPairings_RefuseWithAStableReason(string device, string offload)
    {
        bool resolved = GgufPoolRouter.TryResolve(
            Enum.Parse<DeviceRouteId>(device),
            Enum.Parse<GpuOffloadLevel>(offload),
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(resolved);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnsupportedDeviceRoute),
            reason.ToString());
    }

    [TestMethod]
    public void EveryDeviceAndOffloadPairing_IsDecidedNeverSilentlyDefaulted()
    {
        // Exhaustive sweep. A device or offload member added later without a rule
        // here fails this test rather than routing to an unspecified pool
        foreach (DeviceRouteId device in Enum.GetValues<DeviceRouteId>())
        {
            foreach (GpuOffloadLevel offload in Enum.GetValues<GpuOffloadLevel>())
            {
                bool resolved = GgufPoolRouter.TryResolve(
                    device, offload, out GgufPoolRouting routing, out EstimationUnavailableReason reason);

                if (resolved)
                {
                    Assert.AreNotEqual(
                        ResourceTarget.Unspecified,
                        routing.ModelTarget,
                        $"{device} with {offload} resolved to an unspecified pool.");
                }
                else
                {
                    Assert.AreNotEqual(
                        EstimationUnavailableReason.None,
                        reason,
                        $"{device} with {offload} refused without naming a reason.");
                }
            }
        }
    }

    [TestMethod]
    public void HostStaging_IsDeclaredOnlyWhenWeightsCrossToSeparateMemory()
    {
        // Integrated graphics read the same physical RAM, so there is no upload
        // and no transient host copy. Charging one would overstate the Load phase.
        GgufPoolRouter.TryResolve(
            DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.Full, out GgufPoolRouting shared, out _);
        GgufPoolRouter.TryResolve(
            DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.Full, out GgufPoolRouting dedicated, out _);

        Assert.IsFalse(shared.RequiresHostStaging);
        Assert.IsTrue(dedicated.RequiresHostStaging);
    }
}
