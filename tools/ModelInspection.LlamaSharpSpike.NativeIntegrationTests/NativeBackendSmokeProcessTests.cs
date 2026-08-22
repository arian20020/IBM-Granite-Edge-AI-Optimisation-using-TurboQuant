using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.NativeIntegrationTests;

/// <summary>
/// Verifies the published CPU native backend through a contained child process.
/// </summary>
[TestClass]
[TestCategory("NativeIntegration")]
public sealed class NativeBackendSmokeProcessTests
{
    [TestMethod]
    public async Task Run_WithPublishedCpuBackend_SucceedsAndWritesEvidence()
    {
        using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
            PublishedProbeLocation.RequireFromEnvironment());
        string evidencePath = Path.Combine(
            sandbox.DirectoryPath,
            "evidence",
            "runtime-smoke.json");

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[] { "--output", evidencePath },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(
            result.StandardOutput,
            "CPU backend dry run succeeded");

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        JsonElement root = document.RootElement;
        JsonElement backend = root.GetProperty("selectedBackend");

        Assert.IsTrue(root.GetProperty("succeeded").GetBoolean());
        Assert.AreEqual(
            "0.27.0",
            root.GetProperty("managedPackageVersion").GetString());
        Assert.AreEqual(
            "0.27.0",
            root.GetProperty("backendPackageVersion").GetString());
        Assert.AreEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            root.GetProperty("expectedLlamaCppCommit").GetString());
        Assert.IsFalse(backend.GetProperty("usesCuda").GetBoolean());
        Assert.IsFalse(backend.GetProperty("usesVulkan").GetBoolean());
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            backend.GetProperty("nativeLibraryName").GetString()));
    }
}
