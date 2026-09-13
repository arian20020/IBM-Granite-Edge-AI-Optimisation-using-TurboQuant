using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Dxgi;

[TestClass]
public sealed class DxgiGraphicsIntegrationTests
{
    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RealProviderReturnsStructurallyValidPrivateSafeEvidence()
    {
        DxgiGraphicsEvidence evidence =
            await new DxgiGraphicsEvidenceProvider().CaptureAsync(CancellationToken.None);

        Assert.AreEqual(DxgiGraphicsEvidenceState.Available, evidence.State);
        Assert.IsLessThanOrEqualTo(64, evidence.Adapters.Count);
        Assert.IsTrue(evidence.Adapters.All(static adapter =>
            !string.IsNullOrWhiteSpace(adapter.Name) &&
            Enum.IsDefined(adapter.Kind) &&
            adapter.Ordinal is >= 0 and < 64));
        Assert.AreEqual(TimeSpan.Zero, evidence.CapturedAtUtc.Offset);
        Assert.HasCount(0, evidence.Diagnostics);
    }
}
