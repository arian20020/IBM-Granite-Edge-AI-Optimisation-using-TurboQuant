using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Processes;

[TestClass]
public sealed class BoundedProcessOutputTests
{
    [TestMethod]
    public async Task BoundedOutputRejectsInvalidUtf8WithoutReplacementText()
    {
        using var stream = new MemoryStream([0x7b, 0xff, 0x7d]);
        BoundedProcessOutput output = BoundedProcessOutput.Start(stream, 16);

        BoundedProcessOutputResult result = await output.Completion;

        Assert.IsTrue(result.Failed);
        Assert.AreEqual(string.Empty, result.Text);
        Assert.IsFalse(result.LimitExceeded);
    }

    [TestMethod]
    public async Task BoundedOutputDecodesMultibyteScalarSplitAcrossReadsExactly()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("Aé🙂Z");
        using var stream = new ChunkedReadStream(utf8, maximumReadSize: 1);
        BoundedProcessOutput output = BoundedProcessOutput.Start(stream, utf8.Length);

        BoundedProcessOutputResult result = await output.Completion;

        Assert.IsFalse(result.Failed);
        Assert.AreEqual("Aé🙂Z", result.Text);
        Assert.IsFalse(result.LimitExceeded);
    }

    private sealed class ChunkedReadStream : MemoryStream
    {
        private readonly int _maximumReadSize;

        internal ChunkedReadStream(byte[] buffer, int maximumReadSize)
            : base(buffer, writable: false)
        {
            _maximumReadSize = maximumReadSize;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(
                buffer[..Math.Min(buffer.Length, _maximumReadSize)],
                cancellationToken);
    }
}
