using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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

    [TestMethod]
    public void VerifierAcceptsControlledOfficialClosureAndRejectsOneByteArchiveTamper()
    {
        using VerifierFixture fixture = VerifierFixture.Create();

        VerifierResult valid = RunVerifier(fixture, fixture.OfficialClosure, "Official");
        Assert.AreEqual(0, valid.ExitCode);
        Assert.AreEqual("dependency_lock_valid", valid.StandardOutput);
        Assert.AreEqual(string.Empty, valid.StandardError);

        FlipOneByte(fixture.RuntimeArchivePath);
        VerifierResult tampered = RunVerifier(fixture, fixture.OfficialClosure, "Official");
        Assert.AreNotEqual(0, tampered.ExitCode);
        Assert.AreEqual("runtime_integrity_failed", tampered.StandardOutput);
        Assert.AreEqual(string.Empty, tampered.StandardError);
        Assert.IsFalse(tampered.StandardOutput.Contains(fixture.Root, StringComparison.Ordinal));
    }

    [TestMethod]
    public void VerifierRejectsOneByteTamperOfThePinnedPythonRuntime()
    {
        using VerifierFixture fixture = VerifierFixture.Create();

        VerifierResult valid = RunVerifier(fixture, fixture.ConverterClosure, "Converter");
        Assert.AreEqual(0, valid.ExitCode);
        Assert.AreEqual("dependency_lock_valid", valid.StandardOutput);

        FlipOneByte(fixture.PythonRuntimePath);
        VerifierResult tampered = RunVerifier(fixture, fixture.ConverterClosure, "Converter");
        Assert.AreNotEqual(0, tampered.ExitCode);
        Assert.AreEqual("runtime_integrity_failed", tampered.StandardOutput);
        Assert.AreEqual(string.Empty, tampered.StandardError);
        Assert.IsFalse(tampered.StandardOutput.Contains(fixture.Root, StringComparison.Ordinal));
    }

    [TestMethod]
    public void VerifierRejectsSentinelReviewerValues()
    {
        foreach (string sentinel in new[] { "N/A", "unverified", "unknown" })
        {
            using VerifierFixture fixture = VerifierFixture.Create(reviewer: sentinel);
            VerifierResult result = RunVerifier(fixture, fixture.OfficialClosure, "Official");
            Assert.AreNotEqual(0, result.ExitCode, sentinel);
            Assert.AreEqual("runtime_integrity_failed", result.StandardOutput, sentinel);
            Assert.AreEqual(string.Empty, result.StandardError, sentinel);
            Assert.IsFalse(result.StandardOutput.Contains(fixture.Root, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void ContractReviewerValidationRejectsSentinelValues()
    {
        foreach (string sentinel in new[] { "TBD", "N/A", "unverified", "unknown" })
        {
            using JsonDocument document = JsonDocument.Parse(
                $$"""
                {
                  "reviews": [
                    { "gate": "DEP-01", "reviewer": "{{sentinel}}", "reviewedAtUtc": "2026-08-20T15:00:00Z", "disposition": "reviewed" },
                    { "gate": "DEP-02", "reviewer": "reviewer@example.test", "reviewedAtUtc": "2026-08-20T15:00:00Z", "disposition": "reviewed" },
                    { "gate": "LIC-01", "reviewer": "reviewer@example.test", "reviewedAtUtc": "2026-08-20T15:00:00Z", "disposition": "reviewed" }
                  ]
                }
                """);

            Assert.ThrowsExactly<AssertFailedException>(() => AssertReviewed(document.RootElement));
        }
    }

    private static VerifierResult RunVerifier(
        VerifierFixture fixture,
        string closureDirectory,
        string scope)
    {
        ProcessStartInfo startInfo = new("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(fixture.ScriptPath);
        startInfo.ArgumentList.Add("-ClosureDirectory");
        startInfo.ArgumentList.Add(closureDirectory);
        startInfo.ArgumentList.Add("-Scope");
        startInfo.ArgumentList.Add(scope);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start PowerShell verifier.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new VerifierResult(
            process.ExitCode,
            standardOutput.Trim(),
            standardError.Trim());
    }

    private static void FlipOneByte(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        stream.Position = 0;
        int original = stream.ReadByte();
        Assert.IsTrue(original >= 0);
        stream.Position = 0;
        stream.WriteByte((byte)(original ^ 1));
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
            AssertConcreteReviewValue(reviewer);
            AssertConcreteReviewValue(reviewedAtUtc);
            AssertConcreteReviewValue(disposition);
            Assert.IsTrue(DateTimeOffset.TryParse(reviewedAtUtc, out _));
        }
    }

    private static void AssertConcreteReviewValue(string value)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(value));
        Assert.IsFalse(
            new[] { "tbd", "n/a", "na", "unverified", "unknown", "none", "null", "pending", "unset", "-" }
                .Contains(value.Trim(), StringComparer.OrdinalIgnoreCase));
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

    private sealed record VerifierResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class VerifierFixture : IDisposable
    {
        private const string ReviewedAtUtc = "2026-08-20T15:00:00Z";

        private VerifierFixture(
            string root,
            string scriptPath,
            string officialClosure,
            string converterClosure,
            string runtimeArchivePath,
            string pythonRuntimePath)
        {
            Root = root;
            ScriptPath = scriptPath;
            OfficialClosure = officialClosure;
            ConverterClosure = converterClosure;
            RuntimeArchivePath = runtimeArchivePath;
            PythonRuntimePath = pythonRuntimePath;
        }

        public string Root { get; }

        public string ScriptPath { get; }

        public string OfficialClosure { get; }

        public string ConverterClosure { get; }

        public string RuntimeArchivePath { get; }

        public string PythonRuntimePath { get; }

        public static VerifierFixture Create(string reviewer = "reviewer@example.test")
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI.OpenVino.DependencyLocks",
                Guid.NewGuid().ToString("N"));
            string scriptDirectory = Path.Combine(root, "scripts", "openvino");
            string officialLocks = Path.Combine(root, "third-party", "openvino-official");
            string converterLocks = Path.Combine(root, "third-party", "openvino-converter");
            string officialClosure = Path.Combine(root, "official-closure");
            string converterClosure = Path.Combine(root, "converter-closure");
            Directory.CreateDirectory(scriptDirectory);
            Directory.CreateDirectory(officialLocks);
            Directory.CreateDirectory(converterLocks);
            Directory.CreateDirectory(officialClosure);
            Directory.CreateDirectory(converterClosure);
            File.WriteAllText(Path.Combine(root, "global.json"), "{}");

            string sourceScript = Path.Combine(
                DependencyLockContractTests.Root,
                "scripts",
                "openvino",
                "Test-OpenVinoDependencyLocks.ps1");
            string scriptPath = Path.Combine(scriptDirectory, "Test-OpenVinoDependencyLocks.ps1");
            File.Copy(sourceScript, scriptPath);

            object[] reviews =
            [
                new { gate = "DEP-01", reviewer, reviewedAtUtc = ReviewedAtUtc, disposition = "reviewed" },
                new { gate = "DEP-02", reviewer, reviewedAtUtc = ReviewedAtUtc, disposition = "reviewed" },
                new { gate = "LIC-01", reviewer, reviewedAtUtc = ReviewedAtUtc, disposition = "reviewed" }
            ];

            ArchiveEvidence runtime = CreateArchive(
                officialClosure,
                "runtime.zip",
                "runtime-payload");
            ArchiveEvidence genAi = CreateArchive(
                officialClosure,
                "genai.zip",
                "genai-payload");
            ArchiveEvidence tokenizers = CreateArchive(
                officialClosure,
                "tokenizers.zip",
                "tokenizer-payload");
            WriteOfficialLock(officialLocks, "openvino-runtime.lock.json", "runtime", runtime, reviews);
            WriteOfficialLock(officialLocks, "openvino-genai.lock.json", "genai", genAi, reviews);
            WriteOfficialLock(officialLocks, "openvino-tokenizers.lock.json", "tokenizers", tokenizers, reviews);

            string pythonRuntimePath = Path.Combine(converterClosure, "python-embed.zip");
            File.WriteAllBytes(pythonRuntimePath, Encoding.UTF8.GetBytes("python-runtime"));
            File.WriteAllBytes(
                Path.Combine(converterClosure, "converter-wheel.whl"),
                Encoding.UTF8.GetBytes("converter-wheel"));
            FileInfo pythonRuntime = new(pythonRuntimePath);
            FileInfo wheel = new(Path.Combine(converterClosure, "converter-wheel.whl"));
            WriteJson(
                Path.Combine(converterLocks, "python-runtime.lock.json"),
                new
                {
                    version = "3.13.15",
                    filename = pythonRuntime.Name,
                    length = pythonRuntime.Length,
                    sha256 = HashFile(pythonRuntime.FullName),
                    reviews
                });
            WriteJson(
                Path.Combine(converterLocks, "wheel-manifest.json"),
                new
                {
                    closureStatus = "resolved",
                    reviews,
                    wheels = new[]
                    {
                        new
                        {
                            filename = wheel.Name,
                            length = wheel.Length,
                            sha256 = HashFile(wheel.FullName)
                        }
                    }
                });

            return new VerifierFixture(
                root,
                scriptPath,
                officialClosure,
                converterClosure,
                Path.Combine(officialClosure, runtime.FileName),
                pythonRuntimePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static ArchiveEvidence CreateArchive(
            string directory,
            string fileName,
            string payload)
        {
            string path = Path.Combine(directory, fileName);
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("payload.txt");
                using (StreamWriter writer = new(entry.Open(), Encoding.UTF8, 1024, leaveOpen: false))
                {
                    writer.Write(payload);
                }
            }

            long payloadLength;
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                payloadLength = archive.GetEntry("payload.txt")!.Length;
            }

            FileInfo file = new(path);
            return new ArchiveEvidence(
                file.Name,
                file.Length,
                HashFile(file.FullName),
                payloadLength);
        }

        private static void WriteOfficialLock(
            string directory,
            string lockFileName,
            string component,
            ArchiveEvidence archive,
            object[] reviews) =>
            WriteJson(
                Path.Combine(directory, lockFileName),
                new
                {
                    component,
                    release = new { tag = "2026.3.0", commit = "0123456789abcdef0123456789abcdef01234567" },
                    archive = new { fileName = archive.FileName, length = archive.Length, sha256 = archive.Sha256 },
                    files = new[] { new { path = "payload.txt", length = archive.PayloadLength } },
                    licenses = new[] { new { name = "Apache-2.0", disposition = "include" } },
                    reviews
                });

        private static void WriteJson(string path, object value) =>
            File.WriteAllText(path, JsonSerializer.Serialize(value));

        private static string HashFile(string path) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        private sealed record ArchiveEvidence(
            string FileName,
            long Length,
            string Sha256,
            long PayloadLength);
    }

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
