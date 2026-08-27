using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class FixtureContractTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string FixtureRoot = Path.Combine(
        Root,
        "tests",
        "TestFixtures",
        "OpenVINO",
        "GenAI",
        "TinySyntheticV1");

    private static readonly string[] RequiredPackageFiles =
    [
        "config.json",
        "generation_config.json",
        "openvino_detokenizer.bin",
        "openvino_detokenizer.xml",
        "openvino_model.bin",
        "openvino_model.xml",
        "openvino_tokenizer.bin",
        "openvino_tokenizer.xml",
        "tokenizer_config.json"
    ];

    [TestMethod]
    public void CommittedFixtureHasExactClosedPackageManifestAndConcreteLicense()
    {
        string packageDirectory = Path.Combine(FixtureRoot, "package");
        Assert.IsTrue(Directory.Exists(packageDirectory), "Missing generated fixture package.");

        string[] actualPackageFiles = Directory.GetFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(packageDirectory, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(RequiredPackageFiles, actualPackageFiles);

        string manifestPath = Path.Combine(FixtureRoot, "manifest.json");
        Assert.IsTrue(File.Exists(manifestPath), "Missing fixture manifest.");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        Assert.AreEqual(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("TinySyntheticV1", manifest.RootElement.GetProperty("fixtureId").GetString());
        Assert.AreEqual("MIT", manifest.RootElement.GetProperty("license").GetString());

        JsonElement[] entries = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        string[] paths = entries.Select(entry => entry.GetProperty("path").GetString()!).ToArray();
        string[] expectedPaths = RequiredPackageFiles.Select(path => $"package/{path}").ToArray();
        CollectionAssert.AreEqual(expectedPaths, paths, "Manifest paths must be closed and ordinally sorted.");

        foreach (JsonElement entry in entries)
        {
            string relativePath = entry.GetProperty("path").GetString()!;
            string sha256 = entry.GetProperty("sha256").GetString()!;
            long length = entry.GetProperty("length").GetInt64();
            Assert.IsTrue(Regex.IsMatch(sha256, "^[0-9a-f]{64}$"), relativePath);
            Assert.IsTrue(length > 0, relativePath);

            string fullPath = Path.Combine(FixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            FileInfo file = new(fullPath);
            Assert.AreEqual(length, file.Length, relativePath);
            Assert.AreEqual(sha256, HashFile(fullPath), relativePath);
        }

        string licensePath = Path.Combine(FixtureRoot, "LICENSE.txt");
        Assert.IsTrue(File.Exists(licensePath), "Missing redistribution license.");
        string[] licenseLines = File.ReadAllLines(licensePath);
        Assert.IsTrue(licenseLines.Length >= 5, "Redistribution license is incomplete.");
        Assert.AreEqual("SPDX-License-Identifier: MIT", licenseLines[0]);
        Assert.IsTrue(licenseLines.Any(line => line.StartsWith("Copyright (c) ", StringComparison.Ordinal)));
        Assert.IsFalse(licenseLines.Any(line => IsSentinel(line.Trim())));
    }

    [TestMethod]
    public void GeneratorProducesByteIdenticalFixturesInTwoCleanOperationOwnedRoots()
    {
        using TemporaryDirectory first = TemporaryDirectory.Create();
        using TemporaryDirectory second = TemporaryDirectory.Create();

        AssertScriptSuccess(
            "scripts/openvino/New-OpenVinoGenAiFixture.ps1",
            "fixture_generated",
            "-DestinationRoot", first.Path);
        AssertScriptSuccess(
            "scripts/openvino/New-OpenVinoGenAiFixture.ps1",
            "fixture_generated",
            "-DestinationRoot", second.Path);

        Dictionary<string, string> firstFiles = HashTree(first.Path);
        Dictionary<string, string> secondFiles = HashTree(second.Path);
        CollectionAssert.AreEqual(firstFiles.Keys.ToArray(), secondFiles.Keys.ToArray());
        foreach ((string path, string sha256) in firstFiles)
        {
            Assert.AreEqual(sha256, secondFiles[path], path);
        }

        AssertScriptSuccess(
            "scripts/openvino/Test-OpenVinoGenAiFixture.ps1",
            "fixture_valid",
            "-FixtureRoot", first.Path);
        AssertScriptSuccess(
            "scripts/openvino/Test-OpenVinoGenAiFixture.ps1",
            "fixture_valid",
            "-FixtureRoot", second.Path);
    }

    [TestMethod]
    public void VerifierRejectsPackageAdditionOmissionAndOneByteTamper()
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        AssertScriptSuccess(
            "scripts/openvino/Test-OpenVinoGenAiFixture.ps1",
            "fixture_valid",
            "-FixtureRoot", temporary.Path);

        File.WriteAllText(Path.Combine(temporary.Path, "package", "addition.txt"), "unexpected");
        AssertScriptFailure(temporary.Path);
        File.Delete(Path.Combine(temporary.Path, "package", "addition.txt"));

        string omitted = Path.Combine(temporary.Path, "package", "config.json");
        byte[] config = File.ReadAllBytes(omitted);
        File.Delete(omitted);
        AssertScriptFailure(temporary.Path);
        File.WriteAllBytes(omitted, config);

        string tampered = Path.Combine(temporary.Path, "package", "openvino_model.bin");
        FlipOneByte(tampered);
        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    public void VerifierRejectsReparsePointsAnywhereInFixtureTopology()
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        using TemporaryDirectory outside = TemporaryDirectory.Create();
        string target = Path.Combine(outside.Path, "outside.txt");
        string link = Path.Combine(temporary.Path, "package", "config.json");
        File.WriteAllText(target, "outside");
        File.Delete(link);

        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (UnauthorizedAccessException exception)
        {
            Assert.Inconclusive($"Symbolic-link creation is unavailable: {exception.Message}");
        }

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    public void VerifierRejectsSourceSpecTamper()
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        FlipOneByte(Path.Combine(temporary.Path, "source", "fixture-spec.json"));
        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    public void VerifierRejectsAlternateDataStreamsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Alternate data streams are a Windows-only topology check.");
        }

        using TemporaryDirectory temporary = CopyCommittedFixture();
        string streamPath = Path.Combine(temporary.Path, "package", "config.json") + ":unexpected";
        try
        {
            File.WriteAllText(streamPath, "unsafe");
        }
        catch (IOException exception)
        {
            Assert.Inconclusive($"Alternate data streams are unavailable: {exception.Message}");
        }

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    public void VerifierRejectsSemanticSourceTensorMutation()
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        string sourcePath = Path.Combine(temporary.Path, "source", "fixture-spec.json");
        JsonObject source = ReadJsonObject(sourcePath);
        JsonArray tensors = source["model"]!.AsObject()["sourceTensors"]!.AsArray();
        JsonArray values = tensors[3]!.AsObject()["values"]!.AsArray();
        values[2] = 9;
        WriteJsonObject(sourcePath, source);

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    public void VerifierRejectsForbiddenDecodedSourceValue()
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        string sourcePath = Path.Combine(temporary.Path, "source", "fixture-spec.json");
        JsonObject source = ReadJsonObject(sourcePath);
        source["generation"]!.AsObject()["expectedText"] =
            "https://fixture.invalid/api_key=credential";
        WriteJsonObject(sourcePath, source);

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    [DataRow("source/fixture-spec.json", false)]
    [DataRow("source/fixture-spec.json", true)]
    [DataRow("manifest.json", false)]
    [DataRow("manifest.json", true)]
    public void VerifierRejectsUnknownOrDuplicatePropertiesInClosedJsonSchemas(
        string relativePath,
        bool duplicate)
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        string path = Path.Combine(
            temporary.Path,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (duplicate)
        {
            string json = File.ReadAllText(path);
            int rootStart = json.IndexOf('{');
            Assert.IsTrue(rootStart >= 0);
            File.WriteAllText(path, json.Insert(rootStart + 1, "\n  \"schemaVersion\": 1,"));
        }
        else
        {
            JsonObject root = ReadJsonObject(path);
            root["unexpectedProperty"] = true;
            WriteJsonObject(path, root);
        }

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    [DataRow("387.4")]
    [DataRow("9223372036854775808")]
    [DataRow("-387")]
    [DataRow("3.87e2")]
    [DataRow("387.0")]
    public void VerifierRejectsManifestLengthThatIsNotCanonicalNonNegativeInt64(
        string replacementLength)
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        string manifestPath = Path.Combine(temporary.Path, "manifest.json");
        string manifest = File.ReadAllText(manifestPath);
        const string original = "\"length\": 387,";
        int lengthStart = manifest.IndexOf(original, StringComparison.Ordinal);
        Assert.IsTrue(lengthStart >= 0, "The controlled manifest length was not found.");
        string replacement = $"\"length\": {replacementLength},";
        File.WriteAllText(
            manifestPath,
            manifest.Remove(lengthStart, original.Length).Insert(lengthStart, replacement));

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    public void VerifierRejectsCoordinatedWeakLicenseReplacement()
    {
        using TemporaryDirectory temporary = CopyCommittedFixture();
        const string weakLicense =
            "SPDX-License-Identifier: MIT\n\n" +
            "Copyright (c) 2026 GraniteEdgeAI contributors\n" +
            "Permission is granted.\n" +
            "Redistribution is permitted.\n" +
            "Warranty is disclaimed.\n";
        string sourcePath = Path.Combine(temporary.Path, "source", "fixture-spec.json");
        JsonObject source = ReadJsonObject(sourcePath);
        source["licenseText"] = weakLicense;
        WriteJsonObject(sourcePath, source);
        File.WriteAllText(Path.Combine(temporary.Path, "LICENSE.txt"), weakLicense);

        AssertScriptFailure(temporary.Path);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("package")]
    [DataRow("source")]
    public void VerifierRejectsAlternateDataStreamsOnFixtureDirectories(string relativeDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Alternate data streams are a Windows-only topology check.");
        }

        using TemporaryDirectory temporary = CopyCommittedFixture();
        string directory = relativeDirectory.Length == 0
            ? temporary.Path
            : Path.Combine(temporary.Path, relativeDirectory);
        try
        {
            File.WriteAllText(directory + ":unexpected", "unsafe");
        }
        catch (IOException exception)
        {
            Assert.Inconclusive($"Directory alternate data streams are unavailable: {exception.Message}");
        }

        AssertScriptFailure(temporary.Path);
    }

    private static TemporaryDirectory CopyCommittedFixture()
    {
        TemporaryDirectory temporary = TemporaryDirectory.Create();
        CopyDirectory(FixtureRoot, temporary.Path);
        return temporary;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static Dictionary<string, string> HashTree(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Sha256 = HashFile(path)
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToDictionary(file => file.RelativePath, file => file.Sha256, StringComparer.Ordinal);

    private static void AssertScriptFailure(string fixtureRoot)
    {
        ScriptResult result = RunScript(
            "scripts/openvino/Test-OpenVinoGenAiFixture.ps1",
            "-FixtureRoot", fixtureRoot);
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual("fixture_invalid", result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
        Assert.IsFalse(result.StandardOutput.Contains(fixtureRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertScriptSuccess(string relativeScriptPath, string expectedOutput, params string[] arguments)
    {
        ScriptResult result = RunScript(relativeScriptPath, arguments);
        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        Assert.AreEqual(expectedOutput, result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    private static ScriptResult RunScript(string relativeScriptPath, params string[] arguments)
    {
        string scriptPath = Path.Combine(Root, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar));
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
        startInfo.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start fixture script.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ScriptResult(process.ExitCode, standardOutput.Trim(), standardError.Trim());
    }

    private static void FlipOneByte(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        int original = stream.ReadByte();
        Assert.IsTrue(original >= 0);
        stream.Position = 0;
        stream.WriteByte((byte)(original ^ 1));
    }

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static JsonObject ReadJsonObject(string path) =>
        JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static void WriteJsonObject(string path, JsonObject value) =>
        File.WriteAllText(
            path,
            value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    private static bool IsSentinel(string value) =>
        new[] { "TBD", "N/A", "unverified", "unknown", "pending" }
            .Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        foreach (string startingPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? directory = new(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    File.Exists(Path.Combine(directory.FullName, "IBM Granite with TurboQuant (Intel).slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GraniteEdgeAI.OpenVino.GenAiFixture",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
