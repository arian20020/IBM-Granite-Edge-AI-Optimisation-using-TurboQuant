using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufRouteConfigurationTests
{
    private static GgufRouteConfiguration Valid() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM,
            GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.IntelSycl,
            DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full);

    [TestMethod]
    public void Create_ExposesTheLlamaCppRoute()
    {
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, Valid().RouteId);
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedWeightFormat()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Unspecified, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedKvCacheFormat()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Unspecified,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedBackend()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Unspecified, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedDevice()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Unspecified, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedOffload()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.Unspecified));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void Create_RejectsUndefinedConfigurationEnums(int raw)
    {
        Action[] invalid =
        [
            () => GgufRouteConfiguration.Create(
                (GgufWeightFormat)raw, GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None),
            () => GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, (GgufKvCacheFormat)raw,
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None),
            () => GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
                (CompatibilityBackend)raw, DeviceRouteId.Cpu, GpuOffloadLevel.None),
            () => GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.Cpu, (DeviceRouteId)raw, GpuOffloadLevel.None),
            () => GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, (GpuOffloadLevel)raw)
        ];

        foreach (Action create in invalid)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(create);
        }
    }

    [TestMethod]
    // An OpenVINO backend on a llama.cpp configuration is route mixing.
    [DataRow(nameof(CompatibilityBackend.OpenVinoCpu))]
    [DataRow(nameof(CompatibilityBackend.OpenVinoGpu))]
    [DataRow(nameof(CompatibilityBackend.OpenVinoNpu))]
    public void Create_RejectsOpenVinoBackends(string backendName)
    {
        CompatibilityBackend backend = Enum.Parse<CompatibilityBackend>(backendName);

        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            backend, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsNpuDevice_BecauseNoLlamaCppNpuRouteIsAdmitted()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.IntelNpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsGpuOffloadOnACpuDevice()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.Full));
    }

    [TestMethod]
    public void Create_AllowsCpuDeviceWithNoOffload()
    {
        GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None);

        Assert.AreEqual(DeviceRouteId.Cpu, configuration.Device);
        Assert.AreEqual(GpuOffloadLevel.None, configuration.Offload);
    }

    [TestMethod]
    public void CanonicalDescriptor_IsStableAndCultureInvariant()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            string turkish = Valid().CanonicalDescriptor;

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string invariant = Valid().CanonicalDescriptor;

            Assert.AreEqual(invariant, turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void CanonicalDescriptor_ChangesWithTheWeightFormat()
    {
        Assert.AreNotEqual(
            Valid().CanonicalDescriptor,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q6K, GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full).CanonicalDescriptor);
    }

    [TestMethod]
    public void CanonicalDescriptor_ChangesWithTheKvCacheFormat()
    {
        Assert.AreNotEqual(
            Valid().CanonicalDescriptor,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full).CanonicalDescriptor);
    }

    [TestMethod]
    public void CanonicalDescriptor_ChangesWithTheBackend()
    {
        Assert.AreNotEqual(
            Valid().CanonicalDescriptor,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.IntelVulkan, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full).CanonicalDescriptor);
    }

    [TestMethod]
    public void CanonicalDescriptor_ChangesWithTheOffloadLevel()
    {
        Assert.AreNotEqual(
            Valid().CanonicalDescriptor,
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Partial).CanonicalDescriptor);
    }
}
