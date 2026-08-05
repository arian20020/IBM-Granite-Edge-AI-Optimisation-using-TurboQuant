using System.Text;
using GraniteEdgeAI.ModelInspection.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Transport.Tests;

/// <summary>
/// Defines the strict, byte-bounded framing behaviour shared by the protected
/// worker and its application-side client.
/// </summary>
[TestClass]
public sealed class BoundedUtf8LineTests
{
    private const int OneMiB = 1024 * 1024;

    /// <summary>
    /// A non-positive line limit cannot provide a meaningful memory boundary.
    /// </summary>
    [TestMethod]
    public void ReaderConstructor_NonPositiveLimit_ThrowsInvalidConfiguration()
    {
        using MemoryStream stream = new();

        ProtocolStreamException error = Assert.ThrowsExactly<ProtocolStreamException>(
            () => new BoundedUtf8LineReader(stream, 0));

        Assert.AreEqual(ProtocolStreamErrorKind.InvalidConfiguration, error.ErrorKind);
    }

    /// <summary>
    /// Clean EOF before any bytes is the normal end of a protocol stream.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_EmptyStream_ReturnsNull()
    {
        await using MemoryStream stream = new();
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

        byte[]? line = await reader.ReadLineAsync(CancellationToken.None);

        Assert.IsNull(line);
    }

    /// <summary>
    /// The configured maximum excludes the terminating line-feed byte.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_ExactOneMiBLimit_ReturnsPayload()
    {
        byte[] payload = Enumerable.Repeat((byte)'a', OneMiB).ToArray();
        byte[] frame = [.. payload, (byte)'\n'];
        await using MemoryStream stream = new(frame, writable: false);
        BoundedUtf8LineReader reader = new(stream, OneMiB);

        byte[]? line = await reader.ReadLineAsync(CancellationToken.None);

        Assert.IsNotNull(line);
        Assert.AreEqual(OneMiB, line.Length);
        Assert.AreEqual((byte)'a', line[0]);
        Assert.AreEqual((byte)'a', line[^1]);
    }

    /// <summary>
    /// Bytes read beyond one line must remain available for the next call.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_TwoBufferedLines_ReturnsBothInOrder()
    {
        byte[] input = Encoding.UTF8.GetBytes("first\nsecond\n");
        await using MemoryStream stream = new(input, writable: false);
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

        byte[]? first = await reader.ReadLineAsync(CancellationToken.None);
        byte[]? second = await reader.ReadLineAsync(CancellationToken.None);
        byte[]? end = await reader.ReadLineAsync(CancellationToken.None);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("first"), first);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("second"), second);
        Assert.IsNull(end);
    }

    /// <summary>
    /// A blank frame cannot be a valid command or worker message.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_EmptyLine_ThrowsEmptyLine()
    {
        await using MemoryStream stream = new([(byte)'\n'], writable: false);
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.EmptyLine, error.ErrorKind);
    }

    /// <summary>
    /// UTF-8 BOM bytes are valid Unicode but forbidden protocol framing.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_BomPrefixedLine_ThrowsBomNotAllowed()
    {
        byte[] input = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}', (byte)'\n'];
        await using MemoryStream stream = new(input, writable: false);
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.BomNotAllowed, error.ErrorKind);
    }

    /// <summary>
    /// Production framing is LF-only, so both CR and CRLF are rejected.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_CarriageReturnFraming_ThrowsCarriageReturnNotAllowed()
    {
        byte[][] inputs =
        [
            [(byte)'{', (byte)'}', (byte)'\r'],
            [(byte)'{', (byte)'}', (byte)'\r', (byte)'\n']
        ];

        foreach (byte[] input in inputs)
        {
            await using MemoryStream stream = new(input, writable: false);
            BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

            ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
                () => reader.ReadLineAsync(CancellationToken.None).AsTask());

            Assert.AreEqual(
                ProtocolStreamErrorKind.CarriageReturnNotAllowed,
                error.ErrorKind);
        }
    }

    /// <summary>
    /// Invalid byte sequences must fail rather than being replacement-decoded.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_InvalidUtf8_ThrowsInvalidUtf8()
    {
        byte[] input = [0xC3, 0x28, (byte)'\n'];
        await using MemoryStream stream = new(input, writable: false);
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.InvalidUtf8, error.ErrorKind);
    }

    /// <summary>
    /// The reader fails on the first byte beyond the limit instead of consuming
    /// the complete untrusted frame and measuring it afterward.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_StopsAtFirstByteBeyondLimit()
    {
        byte[] input = [1, 2, 3, 4, 5, (byte)'\n'];
        await using OneByteAtATimeReadStream stream = new(input);
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 4);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.LineTooLong, error.ErrorKind);
        Assert.AreEqual(
            5,
            stream.BytesServed,
            "The first over-limit byte must end the read immediately.");
    }

    /// <summary>
    /// EOF after payload bytes is not a complete protocol frame.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_PartialEof_ThrowsUnexpectedEndOfStream()
    {
        await using MemoryStream stream = new(
            Encoding.UTF8.GetBytes("unterminated"),
            writable: false);
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => reader.ReadLineAsync(CancellationToken.None).AsTask());

        Assert.AreEqual(
            ProtocolStreamErrorKind.UnexpectedEndOfStream,
            error.ErrorKind);
    }

    /// <summary>
    /// A blocked pipe read must observe caller cancellation.
    /// </summary>
    [TestMethod]
    public async Task ReadLineAsync_BlockedRead_ObservesCancellation()
    {
        await using BlockingReadStream stream = new();
        BoundedUtf8LineReader reader = new(stream, maximumLineBytes: 32);
        using CancellationTokenSource cancellation = new();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => reader.ReadLineAsync(cancellation.Token).AsTask());
    }

    /// <summary>
    /// The writer appends exactly one LF, adds no BOM or CR, and flushes once.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsync_ValidPayload_WritesOneLfAndFlushes()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"protocolVersion\":1}");
        await using RecordingWriteStream stream = new();
        BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 128);

        await writer.WriteLineAsync(payload, CancellationToken.None);

        byte[] expected = [.. payload, (byte)'\n'];
        CollectionAssert.AreEqual(expected, stream.ToArray());
        Assert.AreEqual(1, stream.FlushCalls);
        Assert.AreNotEqual(0xEF, stream.ToArray()[0]);
        Assert.IsFalse(stream.ToArray().Contains((byte)'\r'));
    }

    /// <summary>
    /// Emitting an empty payload would create the forbidden blank frame.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsync_EmptyPayload_RejectsBeforeWriting()
    {
        await using RecordingWriteStream stream = new();
        BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => writer.WriteLineAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.EmptyLine, error.ErrorKind);
        Assert.AreEqual(0, stream.WriteCalls);
        Assert.AreEqual(0, stream.FlushCalls);
    }

    /// <summary>
    /// The writer must not pass a caller-supplied BOM into the protocol stream.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsync_BomPrefixedPayload_RejectsBeforeWriting()
    {
        byte[] payload = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'];
        await using RecordingWriteStream stream = new();
        BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => writer.WriteLineAsync(payload, CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.BomNotAllowed, error.ErrorKind);
        Assert.AreEqual(0, stream.WriteCalls);
    }

    /// <summary>
    /// Raw CR and LF bytes would allow one payload to alter frame boundaries.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsync_EmbeddedCrOrLf_RejectsBeforeWriting()
    {
        (byte[] Payload, ProtocolStreamErrorKind ExpectedKind)[] cases =
        [
            ([1, (byte)'\r', 2], ProtocolStreamErrorKind.CarriageReturnNotAllowed),
            ([1, (byte)'\n', 2], ProtocolStreamErrorKind.LineFeedNotAllowed)
        ];

        foreach ((byte[] payload, ProtocolStreamErrorKind expectedKind) in cases)
        {
            await using RecordingWriteStream stream = new();
            BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

            ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
                () => writer.WriteLineAsync(payload, CancellationToken.None).AsTask());

            Assert.AreEqual(expectedKind, error.ErrorKind);
            Assert.AreEqual(0, stream.WriteCalls);
            Assert.AreEqual(0, stream.FlushCalls);
        }
    }

    /// <summary>
    /// The writer applies the same strict UTF-8 rule as the reader.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsync_InvalidUtf8_RejectsBeforeWriting()
    {
        byte[] payload = [0xC3, 0x28];
        await using RecordingWriteStream stream = new();
        BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => writer.WriteLineAsync(payload, CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.InvalidUtf8, error.ErrorKind);
        Assert.AreEqual(0, stream.WriteCalls);
    }

    /// <summary>
    /// Oversized output is rejected before any bytes reach the pipe.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsync_OversizedPayload_RejectsBeforeWriting()
    {
        byte[] payload = new byte[33];
        Array.Fill(payload, (byte)'a');
        await using RecordingWriteStream stream = new();
        BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

        ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
            () => writer.WriteLineAsync(payload, CancellationToken.None).AsTask());

        Assert.AreEqual(ProtocolStreamErrorKind.LineTooLong, error.ErrorKind);
        Assert.AreEqual(0, stream.WriteCalls);
        Assert.AreEqual(0, stream.FlushCalls);
    }

    /// <summary>
    /// Returns at most one byte per read so a limit test can observe exactly how
    /// many untrusted bytes the production reader requested.
    /// </summary>
    private sealed class OneByteAtATimeReadStream(byte[] source) : Stream
    {
        private int _offset;

        public int BytesServed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => source.Length;

        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ValidateBufferArguments(buffer.Length, offset, count);

            if (_offset >= source.Length || count == 0)
            {
                return 0;
            }

            buffer[offset] = source[_offset++];
            BytesServed++;
            return 1;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_offset >= source.Length || buffer.Length == 0)
            {
                return ValueTask.FromResult(0);
            }

            buffer.Span[0] = source[_offset++];
            BytesServed++;
            return ValueTask.FromResult(1);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private static void ValidateBufferArguments(
            int bufferLength,
            int offset,
            int count)
        {
            if (offset < 0 || count < 0 || offset > bufferLength - count)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
        }
    }

    /// <summary>
    /// Simulates a pipe with no data and no EOF until the operation is cancelled.
    /// </summary>
    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Records whether output was written or flushed so validation can be proven
    /// to occur before any untrusted frame reaches the stream.
    /// </summary>
    private sealed class RecordingWriteStream : MemoryStream
    {
        public int WriteCalls { get; private set; }

        public int FlushCalls { get; private set; }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCalls++;
            return base.FlushAsync(cancellationToken);
        }
    }
}
