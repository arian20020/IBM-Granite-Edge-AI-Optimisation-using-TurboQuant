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
    public async Task WriteLineAsyncSerializesFlushWithinEachFrameTransaction()
    {
        await using FirstFlushBlockingStream stream = new();
        using BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);

        Task first = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("first"), CancellationToken.None)
            .AsTask();
        await stream.FirstFlushStarted.WaitAsync(TimeSpan.FromSeconds(1));
        Task second = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("second"), CancellationToken.None)
            .AsTask();

        Assert.IsFalse(
            second.IsCompleted,
            "The second frame must remain queued until the first flush ends.");
        string[] expectedBeforeRelease =
        [
            "Write:first",
            "Write:LF",
            "Flush:start"
        ];
        CollectionAssert.AreEqual(expectedBeforeRelease, stream.Events);
        stream.ReleaseFirstFlush();
        await Task.WhenAll(first, second);

        string[] expectedEvents =
        [
            "Write:first",
            "Write:LF",
            "Flush:start",
            "Flush:end",
            "Write:second",
            "Write:LF",
            "Flush:start",
            "Flush:end"
        ];
        CollectionAssert.AreEqual(
            expectedEvents,
            stream.Events);
        Assert.AreEqual("first\nsecond\n", stream.GetOutput());
    }

    /// <summary>
    /// A queued frame must write the exact bytes it validated even when the
    /// caller later changes the array wrapped by the supplied memory.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsyncPreservesValidatedQueuedPayload()
    {
        await using FirstWriteBlockingStream stream = new();
        using BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);
        byte[] mutableSecondPayload = Encoding.UTF8.GetBytes("second");

        Task first = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("first"), CancellationToken.None)
            .AsTask();
        await stream.FirstWriteStarted.WaitAsync(TimeSpan.FromSeconds(1));

        Task second = writer
            .WriteLineAsync(mutableSecondPayload, CancellationToken.None)
            .AsTask();

        // Change the caller-owned array after the second call has validated and
        // queued it. The writer must retain its own bounded validated snapshot.
        mutableSecondPayload[0] = (byte)'\n';
        stream.ReleaseFirstWrite();

        await Task.WhenAll(first, second);

        Assert.AreEqual(
            "first\nsecond\n",
            Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>
    /// Cancelling one caller while it waits for the serialization gate must not
    /// write any part of that frame or prevent the next caller from proceeding.
    /// </summary>
    [TestMethod]
    public async Task WriteLineAsyncCancellationWhileQueuedDoesNotWriteAndAllowsFollowingFrame()
    {
        await using FirstFlushBlockingStream stream = new();
        using BoundedUtf8LineWriter writer = new(stream, maximumLineBytes: 32);
        using CancellationTokenSource cancellation = new();

        Task first = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("first"), CancellationToken.None)
            .AsTask();
        await stream.FirstFlushStarted.WaitAsync(TimeSpan.FromSeconds(1));

        Task cancelled = writer
            .WriteLineAsync(Encoding.UTF8.GetBytes("cancelled"), cancellation.Token)
            .AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        Assert.AreEqual(
            "first\n",
            stream.GetOutput(),
            "The cancelled queued frame must contribute zero bytes.");

        stream.ReleaseFirstFlush();
        await first;
        await writer.WriteLineAsync(
            Encoding.UTF8.GetBytes("following"),
            CancellationToken.None);

        Assert.AreEqual("first\nfollowing\n", stream.GetOutput());
        CollectionAssert.DoesNotContain(stream.Events, "Write:cancelled");
    }

    /// <summary>
    /// Records payload, LF, and flush boundaries while holding the first flush
    /// so a following writer can be proven to remain behind the same gate.
    /// </summary>
    private sealed class FirstFlushBlockingStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly object _sync = new();
        private readonly List<string> _events = [];
        private readonly TaskCompletionSource<bool> _firstFlushStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstFlush = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _flushCalls;

        public Task FirstFlushStarted => _firstFlushStarted.Task;

        public string[] Events
        {
            get
            {
                lock (_sync)
                {
                    return [.. _events];
                }
            }
        }

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

        public string GetOutput()
        {
            lock (_sync)
            {
                return Encoding.UTF8.GetString(_inner.ToArray());
            }
        }

        public void ReleaseFirstFlush()
        {
            _releaseFirstFlush.TrySetResult(true);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_sync)
            {
                _events.Add(
                    buffer.Span.SequenceEqual([(byte)'\n'])
                        ? "Write:LF"
                        : $"Write:{Encoding.UTF8.GetString(buffer.Span)}");
                _inner.Write(buffer.Span);
            }

            return ValueTask.CompletedTask;
        }

        public override async Task FlushAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int flushCall = Interlocked.Increment(ref _flushCalls);

            lock (_sync)
            {
                _events.Add("Flush:start");
            }

            if (flushCall == 1)
            {
                _firstFlushStarted.TrySetResult(true);
                await _releaseFirstFlush.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            lock (_sync)
            {
                _events.Add("Flush:end");
            }
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
                _releaseFirstFlush.TrySetCanceled();
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Holds the first payload write while allowing a second writer call to
    /// reach its serialization gate.
    /// </summary>
    private sealed class FirstWriteBlockingStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly object _sync = new();
        private readonly TaskCompletionSource<bool> _firstWriteStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstWrite = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCalls;

        public Task FirstWriteStarted => _firstWriteStarted.Task;

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

        public void ReleaseFirstWrite()
        {
            _releaseFirstWrite.TrySetResult(true);
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int writeCall = Interlocked.Increment(ref _writeCalls);

            if (writeCall == 1)
            {
                _firstWriteStarted.TrySetResult(true);
                await _releaseFirstWrite.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            lock (_sync)
            {
                _inner.Write(buffer.Span);
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
                _releaseFirstWrite.TrySetCanceled();
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
