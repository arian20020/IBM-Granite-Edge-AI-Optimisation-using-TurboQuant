using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.FileAccess;

/// <summary>
/// Verifies evidence-write failure cannot replace a locked existing file or
/// modify the controlled model.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class LockedOutputProcessTests
{
    [TestMethod]
    public async Task Run_WithLockedEvidenceFile_ReturnsWriteFailureAndPreservesInputs()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        string evidencePath = context.CreateEvidencePath("locked-output");
        const string originalEvidence = "existing-evidence";
        await File.WriteAllTextAsync(evidencePath, originalEvidence);

        ProbeExecutionResult process;
        await using (
            var exclusiveLock = new FileStream(
                evidencePath,
                FileMode.Open,
                System.IO.FileAccess.ReadWrite,
                FileShare.None))
        {
            process = await new ProbeProcessRunner().RunAsync(
                sandbox.CreateRequest(
                    new[]
                    {
                        "--model", context.Model.ModelPath,
                        "--output", evidencePath
                    },
                    TimeSpan.FromMinutes(2)),
                CancellationToken.None);
        }

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(1, process.ExitCode);
        StringAssert.Contains(
            process.StandardError,
            "MI-OP-EVIDENCE-WRITE-FAILED");
        Assert.AreEqual(
            originalEvidence,
            await File.ReadAllTextAsync(evidencePath));
        Assert.AreEqual(
            0,
            Directory.GetFiles(
                Path.GetDirectoryName(evidencePath)!,
                "*.tmp-*",
                SearchOption.AllDirectories).Length);
        Assert.AreEqual(
            expectedHash,
            await ModelFileHash.ComputeSha256Async(
                context.Model.ModelPath,
                CancellationToken.None));
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardOutput,
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardError,
            context.Model.ModelPath);
    }
}
