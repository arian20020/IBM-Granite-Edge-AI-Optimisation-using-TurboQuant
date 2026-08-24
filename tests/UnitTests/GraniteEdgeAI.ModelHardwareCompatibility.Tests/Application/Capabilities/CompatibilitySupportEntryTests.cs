using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Capabilities;

[TestClass]
public sealed class CompatibilitySupportEntryTests
{
    private static CompatibilitySupportEntry Create(
        string entryId = "gguf-cpu-imported-f16",
        int minimumContext = 1024,
        int maximumContext = 32768,
        SupportLevel level = SupportLevel.DeclaredSupported) =>
        CompatibilitySupportEntry.Create(
            entryId,
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            minimumContext,
            maximumContext,
            level,
            requiresEvidence: false);

    [TestMethod]
    public void Create_PreservesEveryField()
    {
        CompatibilitySupportEntry entry = Create();

        Assert.AreEqual("gguf-cpu-imported-f16", entry.EntryId);
        Assert.AreEqual(nameof(RuntimeRouteId.LlamaCpp), entry.Route.ToString());
        Assert.AreEqual(nameof(CompatibilityBackend.Cpu), entry.Backend.ToString());
        Assert.AreEqual(nameof(DeviceRouteId.Cpu), entry.Device.ToString());
        Assert.AreEqual(nameof(GpuOffloadLevel.None), entry.Offload.ToString());
        Assert.AreEqual(nameof(GgufWeightFormat.Imported), entry.Weights.ToString());
        Assert.AreEqual(nameof(GgufKvCacheFormat.F16), entry.KvCache.ToString());
        Assert.AreEqual(1024, entry.MinimumContextTokens);
        Assert.AreEqual(32768, entry.MaximumContextTokens);
        Assert.AreEqual(nameof(SupportLevel.DeclaredSupported), entry.Level.ToString());
        Assert.IsFalse(entry.RequiresEvidence);
    }

    [TestMethod]
    public void Create_RejectsABlankEntryId()
    {
        // A candidate names the entry it came from, so an unnamed entry would
        // produce a candidate whose provenance cannot be audited.
        Assert.ThrowsExactly<ArgumentException>(() => Create(entryId: "   "));
    }

    [TestMethod]
    public void Create_RejectsAnUnknownSupportLevel()
    {
        // The matrix must state a claim. Absence of a claim is handled by the
        // entry not existing, not by an entry that declares nothing.
        Assert.ThrowsExactly<ArgumentException>(() => Create(level: SupportLevel.Unknown));
    }

    [TestMethod]
    [DataRow(0, 32768)]
    [DataRow(-1, 32768)]
    [DataRow(1024, 0)]
    [DataRow(1024, -1)]
    public void Create_RejectsNonPositiveContextBounds(int minimum, int maximum)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Create(minimumContext: minimum, maximumContext: maximum));
    }

    [TestMethod]
    public void Create_RejectsAnInvertedContextRange()
    {
        // A maximum below the minimum admits nothing, so the entry could never
        // produce a candidate and would silently vanish from the matrix.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Create(minimumContext: 8192, maximumContext: 4096));
    }

    [TestMethod]
    public void Create_AllowsAMinimumEqualToTheMaximum()
    {
        CompatibilitySupportEntry entry = Create(minimumContext: 4096, maximumContext: 4096);

        Assert.AreEqual(4096, entry.MinimumContextTokens);
        Assert.AreEqual(4096, entry.MaximumContextTokens);
    }

    [TestMethod]
    public void Create_RejectsARouteAndBackendMismatch()
    {
        // An OpenVINO backend on a llama.cpp entry would let a candidate be
        // generated that no evaluator can estimate.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilitySupportEntry.Create(
                "mismatched",
                RuntimeRouteId.LlamaCpp,
                CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                1024,
                32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false));
    }

    [TestMethod]
    public void Create_RejectsAConfigurationTheRouteWouldNotAccept()
    {
        // A CPU device cannot offload to a GPU. Rejecting here means an
        // unbuildable entry cannot sit in the matrix waiting to fail later.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilitySupportEntry.Create(
                "cpu-with-offload",
                RuntimeRouteId.LlamaCpp,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.Full,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                1024,
                32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false));
    }

}
