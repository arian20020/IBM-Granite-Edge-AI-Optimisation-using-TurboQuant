using System.Text.Json;

namespace GraniteEdgeAI.GgufQuantization.Capabilities.Tests;

[TestClass]
public sealed class QuantizerSourceLockTests
{
    private static readonly string[] ExpectedCmakeFlags =
    {
        "GGML_NATIVE=OFF",
        "GGML_OPENMP=OFF",
        "LLAMA_CURL=OFF",
        "BUILD_SHARED_LIBS=OFF",
    };

    [TestMethod]
    public void SourceLockPinsTheApprovedReproducibleQuantizerBuild()
    {
        string repositoryRoot = FindRepositoryRoot();
        string lockPath = Path.Combine(repositoryRoot, "third-party", "llama-quantize", "source.lock.json");

        Assert.IsTrue(File.Exists(lockPath), $"Required source lock is missing: {lockPath}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(lockPath));
        JsonElement root = document.RootElement;

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("https://github.com/ggml-org/llama.cpp.git", root.GetProperty("sourceUrl").GetString());
        Assert.AreEqual("3f7c29d318e317b63f54c558bc69803963d7d88c", root.GetProperty("sourceCommit").GetString());
        Assert.AreEqual("llama-quantize", root.GetProperty("target").GetString());
        Assert.AreEqual("x64", root.GetProperty("architecture").GetString());
        Assert.AreEqual("Release", root.GetProperty("configuration").GetString());
        Assert.AreEqual("static", root.GetProperty("libraryLinkage").GetString());
        Assert.AreEqual("third-party/licenses/LICENSE.llama.cpp.txt", root.GetProperty("licensePath").GetString());

        string[] flags = root.GetProperty("cmakeFlags").EnumerateArray().Select(value => value.GetString()!).ToArray();
        CollectionAssert.AreEqual(ExpectedCmakeFlags, flags);
        Assert.AreEqual(0, root.GetProperty("networkFetches").GetArrayLength());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
