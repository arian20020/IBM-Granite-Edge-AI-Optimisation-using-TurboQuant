using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.NativeIntegrationTests;

/// <summary>
/// Verifies the model-free runtime-smoke evidence contract and privacy boundary.
/// </summary>
[TestClass]
[TestCategory("NativeIntegration")]
public sealed class RuntimeEvidenceProcessTests
{
    [TestMethod]
    public async Task Run_WithPublishedBackend_WritesVersionedModelFreeEvidence()
    {
        using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
            PublishedProbeLocation.RequireFromEnvironment());
        string evidencePath = Path.Combine(
            sandbox.DirectoryPath,
            "evidence",
            "runtime-contract.json");

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[] { "--output", evidencePath },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(0, result.ExitCode);

        string json = await File.ReadAllTextAsync(evidencePath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement backend = root.GetProperty("selectedBackend");
        JsonElement logs = root.GetProperty("logs");

        Assert.AreEqual("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            root.GetProperty("expectedLlamaCppCommit").GetString());
        Assert.AreEqual(
            "X64",
            root.GetProperty("processArchitecture").GetString(),
            ignoreCase: true,
            culture: null);
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            backend.GetProperty("nativeLibraryName").GetString()));
        Assert.IsFalse(backend.GetProperty("usesCuda").GetBoolean());
        Assert.IsFalse(backend.GetProperty("usesVulkan").GetBoolean());
        Assert.AreEqual(JsonValueKind.Array, logs.ValueKind);
        Assert.IsTrue(logs.GetArrayLength() <= 1000);
        Assert.IsFalse(
            json.Contains("modelPath", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(
            result.StandardOutput.Contains(
                "modelPath",
                StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(
            result.StandardError.Contains(
                "modelPath",
                StringComparison.OrdinalIgnoreCase));
        EvidenceAssertions.AssertNoGgufFiles(sandbox.DirectoryPath);
    }
}
