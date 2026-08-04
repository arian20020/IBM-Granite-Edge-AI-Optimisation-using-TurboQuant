using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.RealModel;

/// <summary>
/// Verifies the exact trusted Granite model through the published CPU runtime.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class GraniteVocabOnlySuccessTests
{
    [TestMethod]
    public async Task Run_WithControlledGranite_ReturnsVerifiedEvidenceAndPreservesModel()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        string modelHashBefore = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        Assert.AreEqual(context.Model.Manifest.Sha256, modelHashBefore);

        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string evidencePath = context.CreateEvidencePath("success");

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", context.Model.ModelPath,
                    "--output", evidencePath
                },
                TimeSpan.FromMinutes(2)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(0, process.ExitCode);

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        EvidenceAssertions.AssertSuccessfulGraniteVocabOnly(
            document.RootElement,
            context.Model.Manifest);

        string json = await File.ReadAllTextAsync(evidencePath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            json,
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardOutput,
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardError,
            context.Model.ModelPath);

        string modelHashAfter = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        Assert.AreEqual(modelHashBefore, modelHashAfter);
        Assert.AreEqual(
            0,
            Directory.GetFiles(
                context.EvidenceRoot,
                "*.tmp-*",
                SearchOption.AllDirectories).Length);
        EvidenceAssertions.AssertNoGgufFiles(context.EvidenceRoot);
    }
}
