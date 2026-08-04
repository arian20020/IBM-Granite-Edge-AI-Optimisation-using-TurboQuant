using System.Text;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies read-only model identity capture and before/after comparison.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ModelFileSnapshotServiceTests
{
    [TestMethod]
    public async Task CaptureAsync_RecordsExpectedFileIdentity()
    {
        string testDirectory = CreateTestDirectory();

        try
        {
            string modelPath = Path.Combine(testDirectory, "granite.gguf");
            await File.WriteAllBytesAsync(
                modelPath,
                Encoding.UTF8.GetBytes("granite"));

            var service = new ModelFileSnapshotService();

            ModelFileSnapshot snapshot = await service.CaptureAsync(
                modelPath,
                CancellationToken.None);

            Assert.AreEqual("granite.gguf", snapshot.FileName);
            Assert.AreEqual(7L, snapshot.LengthBytes);
            Assert.AreEqual(
                "ac7daf28fd6bfc7a5c3e4b83c7fc9fd51f92ddff10bdc848f99417eca6fafc7c",
                snapshot.Sha256);
            Assert.AreEqual(64, snapshot.CanonicalPathSha256.Length);
        }
        finally
        {
            DeleteDirectory(testDirectory);
        }
    }

    [TestMethod]
    public async Task Compare_WithUnchangedFile_ReportsPreserved()
    {
        string testDirectory = CreateTestDirectory();

        try
        {
            string modelPath = Path.Combine(testDirectory, "granite.gguf");
            await File.WriteAllTextAsync(modelPath, "granite");
            var service = new ModelFileSnapshotService();

            ModelFileSnapshot before = await service.CaptureAsync(
                modelPath,
                CancellationToken.None);
            ModelFileSnapshot after = await service.CaptureAsync(
                modelPath,
                CancellationToken.None);

            ModelFileIntegrityComparison comparison =
                ModelFileIntegrityComparison.Compare(before, after);

            Assert.IsTrue(comparison.IsPreserved);
            Assert.IsTrue(comparison.LengthUnchanged);
            Assert.IsTrue(comparison.LastWriteTimeUnchanged);
            Assert.IsTrue(comparison.Sha256Unchanged);
        }
        finally
        {
            DeleteDirectory(testDirectory);
        }
    }

    [TestMethod]
    public async Task Compare_AfterContentChange_ReportsNotPreserved()
    {
        string testDirectory = CreateTestDirectory();

        try
        {
            string modelPath = Path.Combine(testDirectory, "granite.gguf");
            await File.WriteAllTextAsync(modelPath, "granite");
            var service = new ModelFileSnapshotService();

            ModelFileSnapshot before = await service.CaptureAsync(
                modelPath,
                CancellationToken.None);

            await Task.Delay(20);
            await File.WriteAllTextAsync(modelPath, "granite-changed");

            ModelFileSnapshot after = await service.CaptureAsync(
                modelPath,
                CancellationToken.None);

            ModelFileIntegrityComparison comparison =
                ModelFileIntegrityComparison.Compare(before, after);

            Assert.IsFalse(comparison.IsPreserved);
            Assert.IsFalse(comparison.LengthUnchanged);
            Assert.IsFalse(comparison.Sha256Unchanged);
        }
        finally
        {
            DeleteDirectory(testDirectory);
        }
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ModelSnapshotTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
