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
    private const string ExactOpenVinoSourceCommit =
        "f5f594dc0c9e5961785f0d17743486d52eac87e7";

    [TestMethod]
    public void RuntimeEvidencePublishesThePinnedTurboQuantSourceCommit()
    {
        string evidenceSource = File.ReadAllText(Path.Combine(
            Root, "workers/OpenVinoOfficial.Worker/src/runtime_evidence.cpp"));
        string lockSource = File.ReadAllText(Path.Combine(
            Root, "third-party/openvino-turboquant/upstream.lock.json"));

        StringAssert.Contains(evidenceSource, ExactOpenVinoSourceCommit);
        StringAssert.Contains(lockSource, ExactOpenVinoSourceCommit);
    }

    [TestMethod]
    public void SharedWorkerSerializesMeasuredTurboQuantActivationOnlyInTurboBuild()
    {
        string source = File.ReadAllText(Path.Combine(
            Root, "workers/OpenVinoOfficial.Worker/src/main.cpp"));

        StringAssert.Contains(source, "const turboquant_activation_evidence activation =");
        StringAssert.Contains(source, "write_event(activation.to_json(session_id, turn_id));");
        StringAssert.Contains(source, "#if defined(GRANITE_TURBOQUANT_WORKER)");
        Assert.IsFalse(source.Contains(
            "actualKvCachePrecision\", kv_cache_precision", StringComparison.Ordinal));
    }

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
        Assert.AreEqual("main-f5f594dc0c9", root.GetProperty("releaseTag").GetString());
        Assert.AreEqual(
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
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
        CollectionAssert.AreEqual(
            new[] { "TBQ3" },
            root.GetProperty("additionalAcceptedCodecs").EnumerateArray()
                .Select(value => value.GetString()).ToArray());

        JsonElement exclusions = root.GetProperty("excludedCapabilities");
        CollectionAssert.AreEquivalent(
            new[] { "QJL", "PolarQuant", "GPU", "PagedAttention", "prefillCompression" },
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
    public void PatchSeriesIsExplicitlyEmptyAndExactClosureIsApproved()
    {
        string seriesPath = Path.Combine(
            Root,
            "third-party/openvino-turboquant/patches/series.json");
        Assert.IsTrue(File.Exists(seriesPath), "Missing patch-series ledger.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(seriesPath));
        JsonElement root = document.RootElement;
        Assert.AreEqual("recovered-upstream", root.GetProperty("route").GetString());
        Assert.AreEqual(0, root.GetProperty("patches").GetArrayLength());
        Assert.AreEqual("local-source-and-runtime-closure-reviewed", root.GetProperty("securityReview").GetString());
        Assert.AreEqual("apache-2.0-and-third-party-notices-staged", root.GetProperty("licenseReview").GetString());
        Assert.IsTrue(root.GetProperty("packageRegistrationAllowed").GetBoolean());
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
        StringAssert.Contains(buildScript, "f5f594dc0c9e5961785f0d17743486d52eac87e7");
        StringAssert.Contains(buildScript, "6fbc103538d30d42da4b0b5130a4792a20f728ba");
        StringAssert.Contains(buildScript, "VerifiedRuntimeDirectory");
        StringAssert.Contains(buildScript, "Release");
        StringAssert.Contains(buildScript, "windows-x86_64");
        StringAssert.Contains(buildScript, "TBQ4");
        StringAssert.Contains(buildScript, "TBQ3");
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
                    sourceCommit = "f5f594dc0c9e5961785f0d17743486d52eac87e7",
                    implementationCommit = "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
                    genAiCommit = "6fbc103538d30d42da4b0b5130a4792a20f728ba",
                    acceptedTuple = new
                    {
                        codec = "TBQ4/TBQ3",
                        device = "CPU",
                        attention = "SDPA",
                        headDimension = 64
                    },
                    files = new[] { ManifestEntry(stage, "runtime.bin", "runtime-binary") }
                }));

            // preserve the runtime file length, then deliberately make the outer
            // worker manifest agree with the changed bytes. the inner runtime
            // manifest must still make the complete closure fail closed
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
