using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Windows;

[TestClass]
public sealed class WindowsProcessorIntegrationTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RealProviderReturnsStructurallyValidPrivateSafeEvidence()
    {
        WindowsProcessorEvidence evidence =
            await new WindowsProcessorEvidenceProvider().CaptureAsync(CancellationToken.None);

        Assert.AreEqual(WindowsProcessorEvidenceState.Available, evidence.State);
        Assert.IsFalse(string.IsNullOrWhiteSpace(evidence.Name));
        Assert.IsTrue(Enum.IsDefined(evidence.Architecture!.Value));
        Assert.IsGreaterThanOrEqualTo(1, evidence.PhysicalCoreCount!.Value);
        Assert.IsLessThanOrEqualTo(4096, evidence.PhysicalCoreCount.Value);
        Assert.IsGreaterThanOrEqualTo(evidence.PhysicalCoreCount.Value, evidence.LogicalProcessorCount!.Value);
        Assert.IsLessThanOrEqualTo(4096, evidence.LogicalProcessorCount.Value);
        Assert.AreEqual(TimeSpan.Zero, evidence.CapturedAtUtc.Offset);
        Assert.HasCount(0, evidence.Diagnostics);
    }
}
