using GraniteEdgeAI.GgufRuntime.Contracts;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.Contracts.Session;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Transport.Tests;

[TestClass]
public sealed class GgufFramedChannelTests
{
    private static readonly string[] ExpectedPromptContents = ["first", "second"];

    private static readonly GgufSessionId SessionId = new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [TestMethod]
    public async Task ConcurrentCommandWritesProduceTwoCompleteNonInterleavedFrames()
    {
        await using var output = new SlowWriteStream();
        var channel = new GgufFramedChannel(Stream.Null, output, maxPendingWrites: 4);
        var first = CreatePrompt("first", "11111111-1111-1111-1111-111111111111");
        var second = CreatePrompt("second", "33333333-3333-3333-3333-333333333333");

        await Task.WhenAll(
            channel.WriteCommandAsync(first, CancellationToken.None).AsTask(),
            channel.WriteCommandAsync(second, CancellationToken.None).AsTask());

        Assert.AreEqual(1, output.MaximumConcurrentWrites);
        output.Position = 0;
        byte[] firstFrame = await GgufFrameReader.ReadAsync(output, CancellationToken.None);
        byte[] secondFrame = await GgufFrameReader.ReadAsync(output, CancellationToken.None);
        var contents = new[]
        {
            ((SubmitPromptCommand)GgufProtocolSerializer.DeserializeCommand(firstFrame)).Content,
            ((SubmitPromptCommand)GgufProtocolSerializer.DeserializeCommand(secondFrame)).Content,
        };
        CollectionAssert.AreEquivalent(ExpectedPromptContents, contents);
    }

    [TestMethod]
    public async Task WriteRejectsWorkAboveConfiguredPendingBound()
    {
        await using var output = new BlockingWriteStream();
        var channel = new GgufFramedChannel(Stream.Null, output, maxPendingWrites: 1);
        ValueTask firstWrite = channel.WriteCommandAsync(
            CreatePrompt("first", "11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);
        await output.WriteStarted.Task;

        await Assert.ThrowsExactlyAsync<GgufTransportException>(() =>
            channel.WriteCommandAsync(
                CreatePrompt("second", "33333333-3333-3333-3333-333333333333"),
                CancellationToken.None).AsTask());

        output.ReleaseWrites();
        await firstWrite;
    }

    private static SubmitPromptCommand CreatePrompt(string content, string requestId)
    {
        return new SubmitPromptCommand(
            GgufProtocolVersion.Current,
            Guid.Parse(requestId),
            SessionId,
            content);
    }

    private sealed class SlowWriteStream : MemoryStream
    {
        private int _activeWrites;

        public int MaximumConcurrentWrites { get; private set; }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int active = Interlocked.Increment(ref _activeWrites);
            MaximumConcurrentWrites = Math.Max(MaximumConcurrentWrites, active);
            try
            {
                await Task.Delay(10, cancellationToken);
                await base.WriteAsync(buffer, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeWrites);
            }
        }
    }

    private sealed class BlockingWriteStream : MemoryStream
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, cancellationToken);
        }

        public void ReleaseWrites()
        {
            _release.TrySetResult();
        }
    }
}
