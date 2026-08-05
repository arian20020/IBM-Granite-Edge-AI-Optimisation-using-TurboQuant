using System.Text;
using GraniteEdgeAI.ModelInspection.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Transport.Tests;

/// <summary>
/// Protects the one-writer rule so concurrent callers cannot interleave bytes
/// from separate protocol frames on the shared standard-input stream.
/// </summary>
[TestClass]
public sealed class BoundedUtf8LineConcurrencyTests
{
    /// <summary>
    /// Two concurrent calls may complete in either order, but each payload, LF,
    /// and flush operation must remain inside one serialized write transaction.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsyncSerializesConcurrentFrames()
    {
        await using ConcurrentWriteProbeStream stream = new();
        BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

        Task first = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("first"), CancellationToken.None)
            .AsTask();
        Task second = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("second"), CancellationToken.None)
            .AsTask();

        await Task.WhenAll(first, second);

        Assert.AreEqual(
            1,
            stream.MaximumConcurrentWrites,
            "Only one underlying write may be active at a time.");

        string output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.IsTrue(
            string.Equals(output, "first\nsecond\n", StringComparison.Ordinal) ||
            string.Equals(output, "second\nfirst\n", StringComparison.Ordinal),
            $"Expected two complete LF-delimited frames but received '{output}'.");
    }

    /// <summary>
    /// Delays every underlying write while recording concurrent entry. The
    /// stream is deliberately safe enough to report interleaving rather than
    /// failing for an unrelated MemoryStream thread-safety reason.
    /// </summary>
    private sealed class ConcurrentWriteProbeStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly object _sync = new();
        private int _activeWrites;
        private int _maximumConcurrentWrites;

        public int MaximumConcurrentWrites =>
            Volatile.Read(ref _maximumConcurrentWrites);

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                lock (_sync)
                {
                    return _inner.Length;
                }
            }
        }

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] ToArray()
        {
            lock (_sync)
            {
                return _inner.ToArray();
            }
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int activeWrites = Interlocked.Increment(ref _activeWrites);
            RecordMaximum(activeWrites);

            try
            {
                await Task.Delay(
                        TimeSpan.FromMilliseconds(20),
                        cancellationToken)
                    .ConfigureAwait(false);

                lock (_sync)
                {
                    _inner.Write(buffer.Span);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeWrites);
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void RecordMaximum(int candidate)
        {
            int observed = Volatile.Read(ref _maximumConcurrentWrites);

            while (
                candidate > observed &&
                Interlocked.CompareExchange(
                    ref _maximumConcurrentWrites,
                    candidate,
                    observed) != observed)
            {
                observed = Volatile.Read(ref _maximumConcurrentWrites);
            }
        }
    }
}
