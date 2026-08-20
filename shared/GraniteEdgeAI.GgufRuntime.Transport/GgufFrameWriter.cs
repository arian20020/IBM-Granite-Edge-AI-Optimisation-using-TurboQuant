using System.Buffers.Binary;

namespace GraniteEdgeAI.GgufRuntime.Transport;

public static class GgufFrameWriter
{
    public static async ValueTask WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.IsEmpty || payload.Length > GgufFrameReader.MaxFrameBytes)
        {
            throw new GgufTransportException("The frame payload length is invalid.");
        }

        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
