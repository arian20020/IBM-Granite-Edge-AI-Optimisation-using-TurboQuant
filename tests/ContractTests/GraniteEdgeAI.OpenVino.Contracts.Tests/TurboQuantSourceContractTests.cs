using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using YamlDotNet.RepresentationModel;

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

    [TestMethod]
    public void TrustedWorkflowIsValidManualOnlyYaml()
    {
        string path = Path.Combine(Root, ".github/workflows/openvino-turboquant-ucl.yml");
        using StreamReader reader = File.OpenText(path);
        YamlStream yaml = new();
        yaml.Load(reader);
        Assert.HasCount(1, yaml.Documents);
        YamlMappingNode root = Assert.IsInstanceOfType<YamlMappingNode>(
            yaml.Documents[0].RootNode);
        YamlMappingNode triggers = Assert.IsInstanceOfType<YamlMappingNode>(
            root.Children[new YamlScalarNode("on")]);
        Assert.IsTrue(triggers.Children.ContainsKey(new YamlScalarNode("workflow_dispatch")));
        Assert.IsFalse(triggers.Children.ContainsKey(new YamlScalarNode("push")));
        Assert.IsFalse(triggers.Children.ContainsKey(new YamlScalarNode("pull_request")));
    }

    [TestMethod]
    public void WorkerManifestCannotRebaseAChangedRuntimeClosure()
    {
        string stage = Path.Combine(
            Path.GetTempPath(),
            "granite-turboquant-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(stage, "licenses"));
        try
        {
            Write(stage, "runtime.bin", "original");
            Write(stage, "OpenVinoTurboQuant.Worker.exe", "worker");
            Write(stage, "OpenVinoTurboQuant.Probe.dll", "probe");
            Write(stage, "licenses/nlohmann-json-LICENSE.MIT.txt", "license");
            File.WriteAllBytes(
                Path.Combine(stage, "turboquant-runtime.manifest.json"),
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1,
                    component = "openvino-turboquant-runtime",
                    platform = "windows-x86_64",
                    configuration = "Release",
                    sourceCommit = "8a17657b995fd3b4a52f8484acfcf2bb61214623",
                    implementationCommit = "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                    genAiCommit = "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0",
                    acceptedTuple = new
                    {
                        codec = "TBQ4/TBQ4",
                        device = "CPU",
                        attention = "SDPA",
                        headDimension = 64
                    },
                    files = new[] { ManifestEntry(stage, "runtime.bin", "runtime-binary") }
                }));

            // Preserve the runtime file length, then deliberately make the outer
            // worker manifest agree with the changed bytes. The inner runtime
            // manifest must still make the complete closure fail closed.
            Write(stage, "runtime.bin", "tampered");
            string[] workerFiles =
            [
                "runtime.bin",
                "turboquant-runtime.manifest.json",
                "OpenVinoTurboQuant.Worker.exe",
                "OpenVinoTurboQuant.Probe.dll",
                "licenses/nlohmann-json-LICENSE.MIT.txt"
            ];
            File.WriteAllBytes(
                Path.Combine(stage, "worker-manifest.json"),
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1,
                    files = workerFiles.Select(path => ManifestEntry(stage, path)).ToArray()
                }));

            ProcessStartInfo start = new("powershell.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(Path.Combine(
                Root,
                "scripts/openvino/Test-OpenVinoTurboQuantWorkerManifest.ps1"));
            start.ArgumentList.Add("-StageDirectory");
            start.ArgumentList.Add(stage);
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("PowerShell did not start.");
            string output = process.StandardOutput.ReadToEnd().Trim();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(1, process.ExitCode);
            Assert.AreEqual("turboquant_worker_manifest_invalid", output);
        }
        finally
        {
            Directory.Delete(stage, recursive: true);
        }
    }

    private static object ManifestEntry(
        string root,
        string relativePath,
        string? kind = null)
    {
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (kind is not null)
        {
            return new
            {
                path = relativePath,
                length = new FileInfo(fullPath).Length,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant(),
                kind
            };
        }
        return new
        {
            path = relativePath,
            length = new FileInfo(fullPath).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant()
        };
    }

    private static void Write(string root, string relativePath, string value) =>
        File.WriteAllText(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            value);

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
