using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.FileAccess;

/// <summary>
/// Verifies invalid filesystem objects fail before native model loading.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class MissingAndDirectoryModelProcessTests
{
    [TestMethod]
    public async Task Run_WithMissingModel_ReturnsFileNotFoundEvidence()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            Guid.NewGuid().ToString("N"),
            "missing.gguf");

        await AssertControlledFailureAsync(
            context,
            sandbox,
            missingPath,
            "missing-model",
            "MI-OP-MODEL-FILE-NOT-FOUND");
    }

    [TestMethod]
    public async Task Run_WithDirectoryAsModel_ReturnsFileNotFoundEvidence()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            "directory-input",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        try
        {
            await AssertControlledFailureAsync(
                context,
                sandbox,
                directoryPath,
                "directory-model",
                "MI-OP-MODEL-FILE-NOT-FOUND");
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static async Task AssertControlledFailureAsync(
        RealModelTestContext context,
        TemporaryProbeSandbox sandbox,
        string modelPath,
        string scenario,
        string expectedCode)
    {
        string evidencePath = context.CreateEvidencePath(scenario);

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", modelPath,
                    "--output", evidencePath
                },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(1, process.ExitCode);

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        JsonElement root = document.RootElement;
        Assert.AreEqual("Failed", root.GetProperty("completionStatus").GetString());
        Assert.AreEqual(expectedCode, root.GetProperty("failureCode").GetString());
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            await File.ReadAllTextAsync(evidencePath),
            Path.GetFullPath(modelPath));
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardOutput,
            Path.GetFullPath(modelPath));
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardError,
            Path.GetFullPath(modelPath));
    }
}
