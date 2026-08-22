using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.NativeIntegrationTests;

/// <summary>
/// Verifies a copied publish reports missing native infrastructure without
/// terminating the parent test host or inventing a model outcome.
/// </summary>
[TestClass]
[TestCategory("NativeIntegration")]
public sealed class MissingNativeBackendProcessTests
{
    [TestMethod]
    public async Task Run_WithNativeDllsRemoved_ReturnsControlledRuntimeFailure()
    {
        using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
            PublishedProbeLocation.RequireFromEnvironment());
        IReadOnlyList<string> nativeDlls = sandbox.FindNativeDlls();

        Assert.IsTrue(
            nativeDlls.Count > 0,
            "Published sandbox did not contain any llama.cpp/ggml native DLLs.");

        foreach (string nativeDll in nativeDlls)
        {
            File.Delete(nativeDll);
        }

        string evidencePath = Path.Combine(
            sandbox.DirectoryPath,
            "evidence",
            "missing-native-runtime.json");

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[] { "--output", evidencePath },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(1, result.ExitCode);

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        JsonElement root = document.RootElement;
        string? failureCode = root.GetProperty("failureCode").GetString();
        string[] allowedCodes =
        {
            "MI-OP-RUNTIME-UNAVAILABLE",
            "MI-OP-RUNTIME-INITIALISATION-FAILED"
        };

        Assert.IsTrue(
            allowedCodes.Contains(failureCode, StringComparer.Ordinal),
            $"Unexpected missing-backend failure code: {failureCode}");
        Assert.IsFalse(root.GetProperty("succeeded").GetBoolean());
        Assert.IsFalse(root.TryGetProperty("modelOutcome", out _));
    }
}
