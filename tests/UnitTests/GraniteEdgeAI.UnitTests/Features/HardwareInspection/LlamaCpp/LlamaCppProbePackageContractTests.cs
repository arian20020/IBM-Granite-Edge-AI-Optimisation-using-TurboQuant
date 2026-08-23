using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.LlamaCpp;

[TestClass]
[TestCategory("HardwareInspection")]
public sealed class LlamaCppProbePackageContractTests
{
    [TestMethod]
    public void PackagedProbeHasExactFlatTrustedManifestAndAmd64Executable()
    {
        string packageBase = Path.GetFullPath(AppContext.BaseDirectory);
        string hardwareRoot = Path.GetFullPath(Path.Combine(packageBase, "HardwareInspection"));
        string probeRoot = Path.GetFullPath(Path.Combine(hardwareRoot, "LlamaCppProbe"));
        Assert.IsTrue(IsStrictDescendant(probeRoot, packageBase));
        Assert.IsTrue(Directory.Exists(probeRoot));
        Assert.IsFalse(Directory.EnumerateDirectories(probeRoot).Any());

        string[] actualMembers = Directory.GetFiles(probeRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(1, actualMembers.Length);
        Assert.IsLessThanOrEqualTo(64, actualMembers.Length);

        string manifestPath = Path.Combine(hardwareRoot, "llamacpp-probe-manifest.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        Assert.IsGreaterThan(0, manifestBytes.Length);
        Assert.IsLessThanOrEqualTo(64 * 1024, manifestBytes.Length);
        Assert.AreEqual((byte)'\n', manifestBytes[^1]);
        Assert.AreEqual(1, manifestBytes.Count(static value => value == (byte)'\n'));
        Assert.AreEqual(0, manifestBytes.Count(static value => value == (byte)'\r'));

        using JsonDocument document = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        });
        JsonElement root = document.RootElement;
        AssertExactProperties(root,
            "schemaVersion", "toolId", "version", "executable", "executableSha256",
            "members", "machine", "disposition", "commands");
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("granite-edge-hardware-llamacpp-probe", root.GetProperty("toolId").GetString());
        Assert.AreEqual("0.27.0-cpu-win-x64", root.GetProperty("version").GetString());
        const string ExecutableName = "GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe";
        Assert.AreEqual(ExecutableName, root.GetProperty("executable").GetString());
        Assert.AreEqual("Amd64", root.GetProperty("machine").GetString());
        Assert.AreEqual("AcceptedForFunctionalEvaluation", root.GetProperty("disposition").GetString());

        string[] manifestMembers = root.GetProperty("members").EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();
        CollectionAssert.AreEqual(actualMembers, manifestMembers);
        CollectionAssert.AreEqual(actualMembers.Order(StringComparer.Ordinal).ToArray(), manifestMembers);

        string executablePath = Path.Combine(probeRoot, ExecutableName);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath))).ToLowerInvariant();
        Assert.AreEqual(hash, root.GetProperty("executableSha256").GetString());
        Assert.IsTrue(hash.All(static value => value is >= '0' and <= '9' or >= 'a' and <= 'f'));
        AssertAmd64(executablePath);

        JsonElement[] commands = root.GetProperty("commands").EnumerateArray().ToArray();
        Assert.HasCount(2, commands);
        AssertCommand(commands[0], "identity", "identity", "--format", "json-v1");
        AssertCommand(commands[1], "capabilities", "capabilities", "--format", "json-v1");

        string[] probeDirectories = Directory.GetDirectories(
            packageBase,
            "LlamaCppProbe",
            SearchOption.AllDirectories);
        CollectionAssert.AreEqual(new[] { probeRoot }, probeDirectories);
    }

    private static bool IsStrictDescendant(string candidate, string parent) =>
        candidate.StartsWith(
            parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static void AssertExactProperties(JsonElement element, params string[] names)
    {
        JsonProperty[] properties = element.EnumerateObject().ToArray();
        Assert.HasCount(names.Length, properties);
        CollectionAssert.AreEquivalent(names, properties.Select(static property => property.Name).ToArray());
        Assert.AreEqual(names.Length, properties.Select(static property => property.Name).Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertCommand(JsonElement command, string identity, params string[] arguments)
    {
        AssertExactProperties(command, "identity", "arguments");
        Assert.AreEqual(identity, command.GetProperty("identity").GetString());
        CollectionAssert.AreEqual(
            arguments,
            command.GetProperty("arguments").EnumerateArray().Select(static item => item.GetString()!).ToArray());
    }

    private static void AssertAmd64(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[64];
        stream.ReadExactly(header);
        Assert.AreEqual((byte)'M', header[0]);
        Assert.AreEqual((byte)'Z', header[1]);
        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(header[0x3c..]);
        Assert.IsGreaterThanOrEqualTo(64, peOffset);
        Assert.IsLessThanOrEqualTo(stream.Length - 6, peOffset);
        stream.Position = peOffset;
        Span<byte> pe = stackalloc byte[6];
        stream.ReadExactly(pe);
        Assert.AreEqual(0x00004550u, BinaryPrimitives.ReadUInt32LittleEndian(pe));
        Assert.AreEqual((ushort)0x8664, BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]));
    }
}
