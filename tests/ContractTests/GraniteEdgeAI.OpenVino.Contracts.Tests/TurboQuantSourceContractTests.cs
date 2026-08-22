using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class TurboQuantSourceContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void RecoveredUpstreamIsExactReleasedCpuSdpaImplementation()
    {
        string lockPath = Path.Combine(
            Root,
            "third-party/openvino-turboquant/upstream.lock.json");
        Assert.IsTrue(File.Exists(lockPath), "Missing TurboQuant upstream lock.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(lockPath));
        JsonElement root = document.RootElement;
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("recovered-upstream", root.GetProperty("implementationRoute").GetString());
        Assert.AreEqual("https://github.com/openvinotoolkit/openvino.git", root.GetProperty("repository").GetString());
        Assert.AreEqual("2026.3.0", root.GetProperty("releaseTag").GetString());
        Assert.AreEqual(
            "8a17657b995fd3b4a52f8484acfcf2bb61214623",
            root.GetProperty("releaseCommit").GetString());
        Assert.AreEqual(
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            root.GetProperty("implementationCommit").GetString());
        Assert.AreEqual(35853, root.GetProperty("pullRequest").GetInt32());
        Assert.AreEqual(64, root.GetProperty("acceptedHeadDimension").GetInt32());
        Assert.AreEqual("CPU", root.GetProperty("acceptedDevice").GetString());
        Assert.AreEqual("SDPA", root.GetProperty("acceptedAttentionPath").GetString());
        Assert.AreEqual("TBQ4", root.GetProperty("acceptedKeyCodec").GetString());
        Assert.AreEqual("TBQ4", root.GetProperty("acceptedValueCodec").GetString());

        JsonElement exclusions = root.GetProperty("excludedCapabilities");
        CollectionAssert.AreEquivalent(
            new[] { "TBQ3", "QJL", "PolarQuant", "GPU", "PagedAttention", "prefillCompression" },
            exclusions.EnumerateArray().Select(value => value.GetString()).ToArray());

        JsonElement files = root.GetProperty("sourceFiles");
        Assert.IsTrue(files.GetArrayLength() >= 8);
        foreach (JsonElement file in files.EnumerateArray())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(file.GetProperty("path").GetString()));
            AssertLowercaseSha256(file.GetProperty("sha256").GetString());
        }
    }

    [TestMethod]
    public void PatchSeriesIsExplicitlyEmptyAndExperimentalExposureIsClosed()
    {
        string seriesPath = Path.Combine(
            Root,
            "third-party/openvino-turboquant/patches/series.json");
        Assert.IsTrue(File.Exists(seriesPath), "Missing patch-series ledger.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(seriesPath));
        JsonElement root = document.RootElement;
        Assert.AreEqual("recovered-upstream", root.GetProperty("route").GetString());
        Assert.AreEqual(0, root.GetProperty("patches").GetArrayLength());
        Assert.AreEqual("pending-external-review", root.GetProperty("securityReview").GetString());
        Assert.AreEqual("pending-external-review", root.GetProperty("licenseReview").GetString());
        Assert.IsFalse(root.GetProperty("packageRegistrationAllowed").GetBoolean());
    }

    [TestMethod]
    public void BuildAndVerificationClosureExistsWithoutExcludedImplementations()
    {
        string[] required =
        {
            "third-party/openvino-turboquant/LICENSES.md",
            "third-party/openvino-turboquant/README.md",
            "scripts/openvino/Build-OpenVinoTurboQuantRuntime.ps1",
            "scripts/openvino/Test-OpenVinoTurboQuantPatchClosure.ps1",
            "workers/OpenVinoTurboQuant.Worker/native/tbq4_codec_tests.cpp",
            "workers/OpenVinoTurboQuant.Worker/native/tbq4_dispatch_tests.cpp",
        };
        foreach (string relativePath in required)
        {
            Assert.IsTrue(File.Exists(Path.Combine(Root, relativePath)), relativePath);
        }

        string buildScript = File.ReadAllText(Path.Combine(
            Root,
            "scripts/openvino/Build-OpenVinoTurboQuantRuntime.ps1"));
        StringAssert.Contains(buildScript, "Test-OpenVinoTurboQuantPatchClosure.ps1");
        StringAssert.Contains(buildScript, "Visual Studio 17 2022");
        StringAssert.Contains(buildScript, "Release");
        StringAssert.Contains(buildScript, "x64");
        StringAssert.Contains(buildScript, "TBQ4");
        Assert.IsFalse(buildScript.Contains("TBQ3", StringComparison.Ordinal));
        Assert.IsFalse(buildScript.Contains("QJL", StringComparison.Ordinal));
        Assert.IsFalse(buildScript.Contains("PolarQuant", StringComparison.Ordinal));
    }

    private static void AssertLowercaseSha256(string? value)
    {
        Assert.IsNotNull(value);
        Assert.IsTrue(
            value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            value);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
