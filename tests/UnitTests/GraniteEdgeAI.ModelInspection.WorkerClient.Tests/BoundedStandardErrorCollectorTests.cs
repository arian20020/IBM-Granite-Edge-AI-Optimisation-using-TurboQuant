using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Specifies continuous stderr drainage, bounded retention, strict decoding,
/// and privacy-preserving diagnostic redaction.
/// </summary>
[TestClass]
public sealed class BoundedStandardErrorCollectorTests
{
    [TestMethod]
    public async Task DrainAsyncContinuesToEofAfterRetentionLimit()
    {
        byte[] input = Encoding.UTF8.GetBytes(new string('x', 4096));
        await using MemoryStream stream = new(input);
        BoundedStandardErrorCollector collector = new(
            maximumRetainedBytes: 128);

        StandardErrorSnapshot snapshot = await collector.DrainAsync(
            stream,
            CancellationToken.None);

        Assert.AreEqual(stream.Length, stream.Position);
        Assert.IsTrue(snapshot.IsTruncated);
        Assert.IsFalse(snapshot.InvalidUtf8Detected);
        Assert.IsTrue(
            Encoding.UTF8.GetByteCount(snapshot.RetainedText) <= 128);
    }

    [TestMethod]
    public async Task InvalidUtf8IsFlaggedWithoutRetainingUndecodableText()
    {
        byte[] input = [0x43, 0x33, 0xA3, 0x28];
        await using MemoryStream stream = new(input);
        BoundedStandardErrorCollector collector = new(
            maximumRetainedBytes: 128);

        StandardErrorSnapshot snapshot = await collector.DrainAsync(
            stream,
            CancellationToken.None);

        Assert.AreEqual(stream.Length, stream.Position);
        Assert.IsTrue(snapshot.InvalidUtf8Detected);
        Assert.AreEqual(string.Empty, snapshot.RetainedText);
    }

    [TestMethod]
    public async Task RetainedTextRedactsPathsIdentifiersAndSecretAssignments()
    {
        const string RequestId = "7b82a9b0-77c0-40d1-bf62-7ba0146591a8";
        const string SensitiveText =
            "C:\\Users\\Arian\\private-model.gguf " +
            RequestId +
            " token=supersecret password: hunter2";
        await using MemoryStream stream = new(
            Encoding.UTF8.GetBytes(SensitiveText));
        BoundedStandardErrorCollector collector = new(
            maximumRetainedBytes: 1024);

        StandardErrorSnapshot snapshot = await collector.DrainAsync(
            stream,
            CancellationToken.None);

        Assert.IsFalse(snapshot.IsTruncated);
        Assert.IsFalse(snapshot.InvalidUtf8Detected);
        StringAssert.Contains(snapshot.RetainedText, "[REDACTED]");
        AssertDoesNotContain(snapshot.RetainedText, "Arian");
        AssertDoesNotContain(snapshot.RetainedText, "private-model.gguf");
        AssertDoesNotContain(snapshot.RetainedText, RequestId);
        AssertDoesNotContain(snapshot.RetainedText, "supersecret");
        AssertDoesNotContain(snapshot.RetainedText, "hunter2");
    }

    [TestMethod]
    public void RetentionLimitMustBePositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new BoundedStandardErrorCollector(maximumRetainedBytes: 0));
    }

    private static void AssertDoesNotContain(
        string actual,
        string unexpected)
    {
        Assert.IsFalse(
            actual.Contains(unexpected, StringComparison.Ordinal),
            $"The diagnostic unexpectedly contained '{unexpected}'.");
    }
}
