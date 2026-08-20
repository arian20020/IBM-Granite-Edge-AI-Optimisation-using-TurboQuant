using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.Application;

public sealed record AvailableMemorySnapshot
{
    public AvailableMemorySnapshot(ulong availablePhysicalBytes, DateTimeOffset capturedAtUtc)
    {
        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", nameof(capturedAtUtc));
        }

        AvailablePhysicalBytes = availablePhysicalBytes;
        CapturedAtUtc = capturedAtUtc;
    }

    public ulong AvailablePhysicalBytes { get; }
    public DateTimeOffset CapturedAtUtc { get; }
}

public interface IAvailableMemoryProvider
{
    ValueTask<AvailableMemorySnapshot> CaptureAsync(CancellationToken cancellationToken);
}
