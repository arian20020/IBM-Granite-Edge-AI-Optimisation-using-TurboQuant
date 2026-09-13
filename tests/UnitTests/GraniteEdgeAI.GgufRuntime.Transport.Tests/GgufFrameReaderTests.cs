using System.Buffers.Binary;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Transport.Tests;

[TestClass]
public sealed class GgufFrameReaderTests
{
    [TestMethod]
    public async Task WriterAndReaderRoundTripPayloadWithLittleEndianLength()
    {
        byte[] payload = [0x41, 0x42, 0x43];
        await using var stream = new MemoryStream();

        await GgufFrameWriter.WriteAsync(stream, payload, CancellationToken.None);

        CollectionAssert.AreEqual(
            new byte[] { 0x03, 0x00, 0x00, 0x00, 0x41, 0x42, 0x43 },
            stream.ToArray());
        stream.Position = 0;
        byte[] result = await GgufFrameReader.ReadAsync(stream, CancellationToken.None);
        CollectionAssert.AreEqual(payload, result);
    }

    [TestMethod]
    public async Task ReaderCompletesFrameWhenStreamReturnsOneByteAtATime()
    {
        byte[] bytes = new byte[7];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 3);
        bytes[4] = 0x61;
        bytes[5] = 0x62;
        bytes[6] = 0x63;
        await using var stream = new OneByteReadStream(bytes);

        byte[] result = await GgufFrameReader.ReadAsync(stream, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, result);
    }

    [TestMethod]
    public async Task ReaderRejectsPayloadThatEndsBeforeDeclaredLength()
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 3);
        bytes[4] = 0x61;
        bytes[5] = 0x62;
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() =>
            GgufFrameReader.ReadAsync(stream, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ReaderRejectsDeclaredLengthAboveBoundBeforeAllocatingPayload()
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(
            header,
            GgufFrameReader.MaxFrameBytes + 1);
        await using var stream = new MemoryStream(header);

        await Assert.ThrowsExactlyAsync<GgufTransportException>(() =>
            GgufFrameReader.ReadAsync(stream, CancellationToken.None).AsTask());
    }

    private sealed class OneByteReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
        }
    }
}
