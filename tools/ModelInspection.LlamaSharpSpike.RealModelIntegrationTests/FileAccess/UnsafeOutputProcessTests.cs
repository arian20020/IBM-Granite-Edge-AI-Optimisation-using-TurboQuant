using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.FileAccess;

/// <summary>
/// Verifies invalid evidence destinations fail without replacing the model.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class UnsafeOutputProcessTests
{
    [TestMethod]
    public async Task Run_WithFileAsOutputParent_ReturnsEvidenceWriteFailure()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        string parentFile = context.CreateEvidencePath(
            "file-as-parent",
            "parent-file");
        await File.WriteAllTextAsync(parentFile, "not-a-directory");
        string impossibleOutput = Path.Combine(
            parentFile,
            "result.json");

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", context.Model.ModelPath,
                    "--output", impossibleOutput
                },
                TimeSpan.FromMinutes(2)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(1, process.ExitCode);
        StringAssert.Contains(
            process.StandardError,
            "MI-OP-EVIDENCE-WRITE-FAILED");
        Assert.AreEqual(
            expectedHash,
            await ModelFileHash.ComputeSha256Async(
                context.Model.ModelPath,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task Run_WithModelAsEvidenceOutput_ReturnsArgumentErrorAndPreservesModel()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", context.Model.ModelPath,
                    "--output", context.Model.ModelPath
                },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(2, process.ExitCode);
        StringAssert.Contains(
            process.StandardError,
            "must not overwrite the selected model");
        Assert.AreEqual(
            expectedHash,
            await ModelFileHash.ComputeSha256Async(
                context.Model.ModelPath,
                CancellationToken.None));
    }
}
