using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Infrastructure;

internal sealed class WindowsAvailableMemoryProvider : IAvailableMemoryProvider
{
    private readonly Func<
        CancellationToken,
        ValueTask<WindowsSystemSnapshot>> _captureSnapshot;

    internal WindowsAvailableMemoryProvider()
        : this(new WindowsSystemSnapshotProvider().CaptureAsync)
    {
    }

    internal WindowsAvailableMemoryProvider(
        Func<CancellationToken, ValueTask<WindowsSystemSnapshot>> captureSnapshot)
    {
        _captureSnapshot = captureSnapshot ??
            throw new ArgumentNullException(nameof(captureSnapshot));
    }

    public async ValueTask<AvailableMemorySnapshot> CaptureAsync(
        CancellationToken cancellationToken)
    {
        WindowsSystemSnapshot snapshot =
            await _captureSnapshot(cancellationToken).ConfigureAwait(false);
        return new AvailableMemorySnapshot(
            snapshot.AvailablePhysicalBytes,
            snapshot.CapturedAtUtc);
    }
}
