using GraniteEdgeAI.Features.HardwareInspection.Infrastructure;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class WindowsAvailableMemoryProviderTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 22, 18, 45, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task CaptureMapsOnlyAvailableBytesAndUtcTimestamp()
    {
        WindowsSystemSnapshot source = new(
            physicallyInstalledBytes: 32UL * 1024 * 1024 * 1024,
            osUsablePhysicalBytes: 31UL * 1024 * 1024 * 1024,
            availablePhysicalBytes: 17UL * 1024 * 1024 * 1024,
            CapturedAtUtc,
            "Windows 11",
            "10.0.26200",
            "x64");
        WindowsAvailableMemoryProvider provider = new(
            _ => ValueTask.FromResult(source));

        GraniteEdgeAI.Features.HardwareInspection.Application.AvailableMemorySnapshot result =
            await provider.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(17UL * 1024 * 1024 * 1024, result.AvailablePhysicalBytes);
        Assert.AreEqual(CapturedAtUtc, result.CapturedAtUtc);
    }

    [TestMethod]
    public async Task CaptureForwardsCallerCancellationWithoutReplacingIt()
    {
        CancellationToken observed = default;
        WindowsAvailableMemoryProvider provider = new(token =>
        {
            observed = token;
            token.ThrowIfCancellationRequested();
            throw new AssertFailedException("Cancelled capture should not continue.");
        });
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await provider.CaptureAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, observed);
    }
}
