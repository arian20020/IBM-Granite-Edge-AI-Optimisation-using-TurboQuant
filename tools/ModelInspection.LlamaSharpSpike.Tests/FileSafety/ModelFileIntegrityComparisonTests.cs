using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies each independent before/after model identity comparison field.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ModelFileIntegrityComparisonTests
{
    [TestMethod]
    public void Compare_WithIdenticalSnapshots_ReportsPreserved()
    {
        ModelFileSnapshot before = Snapshot();

        ModelFileIntegrityComparison result =
            ModelFileIntegrityComparison.Compare(before, before with { });

        Assert.IsTrue(result.PathUnchanged);
        Assert.IsTrue(result.LengthUnchanged);
        Assert.IsTrue(result.LastWriteTimeUnchanged);
        Assert.IsTrue(result.Sha256Unchanged);
        Assert.IsTrue(result.IsPreserved);
    }

    [TestMethod]
    public void Compare_WhenOnlyPathFingerprintChanges_ReportsOnlyPathDifference()
    {
        ModelFileSnapshot before = Snapshot();
        ModelFileSnapshot after = before with
        {
            CanonicalPathSha256 = "different-path"
        };

        ModelFileIntegrityComparison result =
            ModelFileIntegrityComparison.Compare(before, after);

        Assert.IsFalse(result.PathUnchanged);
        Assert.IsTrue(result.LengthUnchanged);
        Assert.IsTrue(result.LastWriteTimeUnchanged);
        Assert.IsTrue(result.Sha256Unchanged);
        Assert.IsFalse(result.IsPreserved);
    }

    [TestMethod]
    public void Compare_WhenOnlyLengthChanges_ReportsOnlyLengthDifference()
    {
        ModelFileSnapshot before = Snapshot();
        ModelFileSnapshot after = before with
        {
            LengthBytes = before.LengthBytes + 1
        };

        ModelFileIntegrityComparison result =
            ModelFileIntegrityComparison.Compare(before, after);

        Assert.IsTrue(result.PathUnchanged);
        Assert.IsFalse(result.LengthUnchanged);
        Assert.IsTrue(result.LastWriteTimeUnchanged);
        Assert.IsTrue(result.Sha256Unchanged);
        Assert.IsFalse(result.IsPreserved);
    }

    [TestMethod]
    public void Compare_WhenOnlyTimestampChanges_ReportsOnlyTimestampDifference()
    {
        ModelFileSnapshot before = Snapshot();
        ModelFileSnapshot after = before with
        {
            LastWriteTimeUtc = before.LastWriteTimeUtc.AddSeconds(1)
        };

        ModelFileIntegrityComparison result =
            ModelFileIntegrityComparison.Compare(before, after);

        Assert.IsTrue(result.PathUnchanged);
        Assert.IsTrue(result.LengthUnchanged);
        Assert.IsFalse(result.LastWriteTimeUnchanged);
        Assert.IsTrue(result.Sha256Unchanged);
        Assert.IsFalse(result.IsPreserved);
    }

    [TestMethod]
    public void Compare_WhenOnlySha256Changes_ReportsOnlyHashDifference()
    {
        ModelFileSnapshot before = Snapshot();
        ModelFileSnapshot after = before with
        {
            Sha256 = "different-hash"
        };

        ModelFileIntegrityComparison result =
            ModelFileIntegrityComparison.Compare(before, after);

        Assert.IsTrue(result.PathUnchanged);
        Assert.IsTrue(result.LengthUnchanged);
        Assert.IsTrue(result.LastWriteTimeUnchanged);
        Assert.IsFalse(result.Sha256Unchanged);
        Assert.IsFalse(result.IsPreserved);
    }

    [TestMethod]
    public void Compare_WithNullBefore_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ModelFileIntegrityComparison.Compare(
                before: null!,
                after: Snapshot()));
    }

    [TestMethod]
    public void Compare_WithNullAfter_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ModelFileIntegrityComparison.Compare(
                before: Snapshot(),
                after: null!));
    }

    private static ModelFileSnapshot Snapshot()
    {
        return new ModelFileSnapshot
        {
            FileName = "granite.gguf",
            CanonicalPathSha256 = "path-hash",
            LengthBytes = 10,
            LastWriteTimeUtc = new DateTimeOffset(
                2026,
                8,
                4,
                10,
                0,
                0,
                TimeSpan.Zero),
            Sha256 = "content-hash"
        };
    }
}
