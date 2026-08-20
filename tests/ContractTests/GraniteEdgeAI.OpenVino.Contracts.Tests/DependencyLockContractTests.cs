using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class DependencyLockContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void OfficialLocksArePinnedAndClosed()
    {
        AssertOfficialLock(
            "third-party/openvino-official/openvino-runtime.lock.json",
            "openvino-runtime",
            "2026.3.0",
            "8a17657b995fd3b4a52f8484acfcf2bb61214623");
        AssertOfficialLock(
            "third-party/openvino-official/openvino-genai.lock.json",
            "openvino-genai",
            "2026.3.0.0",
            "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0");
        AssertOfficialLock(
            "third-party/openvino-official/openvino-tokenizers.lock.json",
            "openvino-tokenizers",
            "2026.3.0.0",
            "183c6f25cda2a469cba5eff8b72022d2d51ba0ca");
    }

    [TestMethod]
    public void ConverterClosureFailsClosedWhenTheExactTrainCannotResolve()
    {
        string runtimePath = Path.Combine(
            Root,
            "third-party/openvino-converter/python-runtime.lock.json");
        string requirementsPath = Path.Combine(
            Root,
            "third-party/openvino-converter/requirements.lock");
        string manifestPath = Path.Combine(
            Root,
            "third-party/openvino-converter/wheel-manifest.json");

        Assert.IsTrue(File.Exists(runtimePath), "Missing CPython runtime lock.");
        Assert.IsTrue(File.Exists(requirementsPath), "Missing wheel requirements lock.");
        Assert.IsTrue(File.Exists(manifestPath), "Missing wheel manifest.");

        using JsonDocument runtime = JsonDocument.Parse(File.ReadAllBytes(runtimePath));
        Assert.AreEqual("3.13.15", runtime.RootElement.GetProperty("version").GetString());
        AssertLowercaseSha256(runtime.RootElement.GetProperty("sha256").GetString());
        Assert.IsTrue(runtime.RootElement.GetProperty("length").GetInt64() > 0);
        AssertReviewed(runtime.RootElement);

        string requirements = File.ReadAllText(requirementsPath);
        StringAssert.Contains(requirements, "optimum-intel==2.1.0");
        StringAssert.Contains(requirements, "optimum==2.1.0");
        StringAssert.Contains(requirements, "transformers==5.5.4");
        StringAssert.Contains(requirements, "openvino==2026.3.0");
        StringAssert.Contains(requirements, "openvino-genai==2026.3.0.0");
        StringAssert.Contains(requirements, "nncf==3.3.0");
        StringAssert.Contains(requirements, "converter_closure_unresolved");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        Assert.AreEqual(
            "unresolved",
            manifest.RootElement.GetProperty("closureStatus").GetString());
        Assert.AreEqual(
            "converter_closure_unresolved",
            manifest.RootElement.GetProperty("supportCode").GetString());
        JsonElement wheels = manifest.RootElement.GetProperty("wheels");
        Assert.AreEqual(0, wheels.GetArrayLength());
        AssertReviewed(manifest.RootElement);

        JsonElement requestedPackages = manifest.RootElement.GetProperty("requestedPackages");
        Assert.AreEqual(6, requestedPackages.GetArrayLength());
    }

    private static void AssertOfficialLock(
        string relativePath,
        string component,
        string tag,
        string commit)
    {
        string path = Path.Combine(Root, relativePath);
        Assert.IsTrue(File.Exists(path), $"Missing official lock: {relativePath}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        Assert.AreEqual(component, root.GetProperty("component").GetString());
        Assert.AreEqual(tag, root.GetProperty("release").GetProperty("tag").GetString());
        Assert.AreEqual(commit, root.GetProperty("release").GetProperty("commit").GetString());
        AssertNonFloatingUrl(root.GetProperty("release").GetProperty("sourceUrl").GetString());
        AssertNonFloatingUrl(root.GetProperty("archive").GetProperty("sourceUrl").GetString());
        Assert.IsTrue(root.GetProperty("archive").GetProperty("length").GetInt64() > 0);
        AssertLowercaseSha256(root.GetProperty("archive").GetProperty("sha256").GetString());
        AssertReviewed(root);

        JsonElement files = root.GetProperty("files");
        Assert.IsTrue(files.GetArrayLength() > 0, $"{component} inventory is empty.");
        string[] paths = files.EnumerateArray()
            .Select(file => file.GetProperty("path").GetString()!)
            .ToArray();
        Assert.AreEqual(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
        foreach (JsonElement file in files.EnumerateArray())
        {
            Assert.IsTrue(file.GetProperty("length").GetInt64() >= 0);
        }

        JsonElement licenses = root.GetProperty("licenses");
        Assert.IsTrue(licenses.GetArrayLength() > 0, $"{component} has no license entries.");
        foreach (JsonElement license in licenses.EnumerateArray())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(license.GetProperty("name").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(license.GetProperty("disposition").GetString()));
        }
    }

    private static void AssertReviewed(JsonElement root)
    {
        JsonElement reviews = root.GetProperty("reviews");
        string[] requiredGates = ["DEP-01", "DEP-02", "LIC-01"];
        foreach (string gate in requiredGates)
        {
            JsonElement review = reviews.EnumerateArray().Single(item =>
                item.GetProperty("gate").GetString() == gate);
            string reviewer = review.GetProperty("reviewer").GetString()!;
            string reviewedAtUtc = review.GetProperty("reviewedAtUtc").GetString()!;
            string disposition = review.GetProperty("disposition").GetString()!;
            Assert.IsFalse(string.IsNullOrWhiteSpace(reviewer) || reviewer == "TBD");
            Assert.IsFalse(string.IsNullOrWhiteSpace(reviewedAtUtc) || reviewedAtUtc == "TBD");
            Assert.IsFalse(string.IsNullOrWhiteSpace(disposition) || disposition == "TBD");
            Assert.IsTrue(DateTimeOffset.TryParse(reviewedAtUtc, out _));
        }
    }

    private static void AssertNonFloatingUrl(string? value)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(value));
        Assert.IsFalse(value!.Contains("latest", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(value.Contains("main", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(value.Contains("master", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertLowercaseSha256(string? value) =>
        Assert.IsTrue(value is not null && System.Text.RegularExpressions.Regex.IsMatch(
            value,
            "^[0-9a-f]{64}$"));

    private static string FindRepositoryRoot()
    {
        foreach (string startingPath in new[]
                 {
                     AppContext.BaseDirectory,
                     Environment.CurrentDirectory,
                     Path.GetDirectoryName(typeof(DependencyLockContractTests).Assembly.Location)
                     ?? AppContext.BaseDirectory
                 })
        {
            DirectoryInfo? directory = new(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(
                        directory.FullName,
                        "IBM Granite with TurboQuant (Intel).slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
