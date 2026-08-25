using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Windows;

[TestClass]
public sealed class WindowsStorageIntegrationTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RealProviderReturnsStructurallyValidPrivateSafeEvidence()
    {
        WindowsStorageEvidence evidence =
            await new WindowsStorageEvidenceProvider().CaptureAsync(CancellationToken.None);

        Assert.AreEqual(WindowsStorageEvidenceState.Available, evidence.State);
        Assert.IsGreaterThan(0UL, evidence.CapacityBytes!.Value);
        Assert.IsLessThanOrEqualTo(evidence.CapacityBytes.Value, evidence.AvailableToCallerBytes!.Value);
        Assert.AreEqual(TimeSpan.Zero, evidence.CapturedAtUtc.Offset);
        Assert.HasCount(0, evidence.Diagnostics);
    }
}
