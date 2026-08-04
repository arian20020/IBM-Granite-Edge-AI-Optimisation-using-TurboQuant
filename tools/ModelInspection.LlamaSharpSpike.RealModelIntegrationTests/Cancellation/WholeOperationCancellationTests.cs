using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Cancellation;

/// <summary>
/// Verifies timed cancellation after the initial integrity snapshot produces a
/// controlled result with before/after model evidence.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class WholeOperationCancellationTests
{
    [TestMethod]
    public async Task Run_WithPostPreflightCancellation_ReturnsCancelledAndPreservesModel()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        string evidencePath = context.CreateEvidencePath(
            "post-preflight-cancellation");

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", context.Model.ModelPath,
                    "--cancel-after-ms", "1",
                    "--output", evidencePath
                },
                TimeSpan.FromMinutes(2)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(3, process.ExitCode);

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        EvidenceAssertions.AssertCancelled(
            document.RootElement,
            context.Model.Manifest,
            requireSelectedBackend: false);

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

        Assert.AreEqual(
            expectedHash,
            await ModelFileHash.ComputeSha256Async(
                context.Model.ModelPath,
                CancellationToken.None));
    }
}
