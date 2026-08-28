using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.TrustedTools;

[TestClass]
public sealed class TrustedManifestFileTests
{
    [TestMethod]
    public void ReadBoundedReturnsExactRegularFileBytes()
    {
        using var fixture = new TemporaryManifest();
        byte[] expected = [1, 2, 3, 4];
        File.WriteAllBytes(fixture.Path, expected);

        byte[] actual = TrustedManifestFile.ReadBounded(fixture.Path, 16);

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReadBoundedRejectsOversizedFileBeforeReadingItsBytes()
    {
        using var fixture = new TemporaryManifest();
        using (FileStream stream = new(
                   fixture.Path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        {
            stream.SetLength((long)int.MaxValue + 1);
        }

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            TrustedManifestFile.ReadBounded(fixture.Path, 64 * 1024));
    }

    [TestMethod]
    public void ReadBoundedRejectsReparsePointFile()
    {
        using var fixture = new TemporaryManifest();
        string target = fixture.Path + ".target";
        File.WriteAllBytes(target, [1, 2, 3, 4]);
        try
        {
            File.CreateSymbolicLink(fixture.Path, target);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Assert.Inconclusive(
                $"File symbolic links are unavailable: {exception.GetType().Name}.");
        }

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            TrustedManifestFile.ReadBounded(fixture.Path, 16));
    }

    private sealed class TemporaryManifest : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "geai-trusted-manifest-tests-" + Guid.NewGuid().ToString("N"));

        internal TemporaryManifest()
        {
            Directory.CreateDirectory(_root);
            Path = System.IO.Path.Combine(_root, "manifest.json");
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
