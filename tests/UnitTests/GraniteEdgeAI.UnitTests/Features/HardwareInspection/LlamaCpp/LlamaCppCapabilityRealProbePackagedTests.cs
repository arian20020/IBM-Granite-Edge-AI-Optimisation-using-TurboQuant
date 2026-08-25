using System.Diagnostics;
using System.Text.Json;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.LlamaCpp;

[TestClass]
[DoNotParallelize]
public sealed class LlamaCppCapabilityRealProbePackagedTests
{
    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public async Task SignedPackageCapturesPinnedCpuCapabilitiesOnlyInChildProcess()
    {
        string packageBase = Path.GetFullPath(AppContext.BaseDirectory);
        string hardwareRoot = Path.GetFullPath(Path.Combine(packageBase, "HardwareInspection"));
        string probeRoot = Path.GetFullPath(Path.Combine(hardwareRoot, "LlamaCppProbe"));
        AssertStrictDescendant(probeRoot, hardwareRoot);
        Assert.IsTrue(Directory.Exists(probeRoot));
        Assert.IsFalse(Directory.EnumerateDirectories(probeRoot).Any());

        string[] inventoryBefore = Directory.GetFiles(probeRoot, "*", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.IsFalse(inventoryBefore.Any(static path =>
            Path.GetExtension(path).Equals(".gguf", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(path).Contains("model", StringComparison.OrdinalIgnoreCase)));
        CollectionAssert.AreEqual(Array.Empty<string>(), LoadedLlamaCppModules());

        TrustedToolPackageManifest manifest = ParseExactManifest(
            Path.Combine(hardwareRoot, "llamacpp-probe-manifest.json"));
        TrustedToolVerificationResult verification = new TrustedToolPackageVerifier().Verify(
            hardwareRoot,
            probeRoot,
            manifest);
        Assert.IsTrue(verification.IsVerified);
        using VerifiedTrustedTool tool = verification.Tool!;
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        LlamaCppCapabilityEvidence evidence = await new LlamaCppCapabilityEvidenceProvider(
            new ExternalProcessRunner()).CaptureAsync(tool, CancellationToken.None);

        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        Assert.AreEqual(
            LlamaCppCapabilityEvidenceState.Available,
            evidence.State,
            $"Probe diagnostic: {evidence.Diagnostic?.ToString() ?? "none"}");
        Assert.AreSame(LlamaCppRuntimeIdentity.PinnedCpu, evidence.RuntimeIdentity);
        Assert.AreEqual("3f7c29d318e317b63f54c558bc69803963d7d88c", evidence.RuntimeIdentity!.MappedLlamaCppCommit);
        CollectionAssert.AreEqual(new[] { LlamaCppBackend.Cpu }, evidence.Backends.ToArray());
        Assert.IsGreaterThanOrEqualTo(1, evidence.VisibleDevices.Count);
        Assert.IsLessThanOrEqualTo(16, evidence.VisibleDevices.Count);
        for (int index = 0; index < evidence.VisibleDevices.Count; index++)
        {
            LlamaCppVisibleDevice device = evidence.VisibleDevices[index];
            Assert.AreEqual(index, device.Ordinal);
            Assert.IsFalse(string.IsNullOrWhiteSpace(device.BufferType));
            Assert.IsLessThanOrEqualTo(128, device.BufferType.EnumerateRunes().Count());
            Assert.IsFalse(device.BufferType.Any(char.IsControl));
        }

        Assert.AreEqual(TimeSpan.Zero, evidence.CapturedAtUtc.Offset);
        Assert.IsGreaterThanOrEqualTo(startedAtUtc, evidence.CapturedAtUtc);
        Assert.IsLessThanOrEqualTo(completedAtUtc, evidence.CapturedAtUtc);
        Assert.IsNull(evidence.Diagnostic);
        CollectionAssert.AreEqual(Array.Empty<string>(), LoadedLlamaCppModules());
        CollectionAssert.AreEqual(
            inventoryBefore,
            Directory.GetFiles(probeRoot, "*", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static TrustedToolPackageManifest ParseExactManifest(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        });
        JsonElement root = document.RootElement;
        string[] properties = root.EnumerateObject().Select(static value => value.Name).ToArray();
        CollectionAssert.AreEquivalent(
            new[]
            {
                "schemaVersion", "toolId", "version", "executable", "executableSha256",
                "members", "machine", "disposition", "commands",
            },
            properties);
        Assert.HasCount(9, properties);
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("Amd64", root.GetProperty("machine").GetString());
        Assert.AreEqual("AcceptedForFunctionalEvaluation", root.GetProperty("disposition").GetString());

        TrustedToolCommand[] commands = root.GetProperty("commands").EnumerateArray()
            .Select(static command => new TrustedToolCommand(
                command.GetProperty("identity").GetString()!,
                command.GetProperty("arguments").EnumerateArray()
                    .Select(static argument => argument.GetString()!)
                    .ToArray()))
            .ToArray();
        return new TrustedToolPackageManifest(
            root.GetProperty("toolId").GetString()!,
            root.GetProperty("version").GetString()!,
            root.GetProperty("executable").GetString()!,
            root.GetProperty("executableSha256").GetString()!,
            root.GetProperty("members").EnumerateArray()
                .Select(static member => member.GetString()!)
                .ToArray(),
            PeMachine.Amd64,
            TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
            commands);
    }

    private static string[] LoadedLlamaCppModules()
    {
        using Process current = Process.GetCurrentProcess();
        return current.Modules.Cast<ProcessModule>()
            .Select(static module => Path.GetFileName(module.FileName))
            .Where(static name =>
                name.StartsWith("llama", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("ggml", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AssertStrictDescendant(string candidate, string parent)
    {
        string relative = Path.GetRelativePath(parent, candidate);
        Assert.IsFalse(Path.IsPathRooted(relative));
        Assert.AreNotEqual("..", relative);
        Assert.IsFalse(relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
