using System.Text.Json;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;
using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.FileAccess;

/// <summary>
/// Verifies a sharing violation is controlled and cannot alter the model.
/// </summary>
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class LockedModelProcessTests
{
    [TestMethod]
    public async Task Run_WithExclusivelyLockedModel_ReturnsFileIoAndPreservesHash()
    {
        using RealModelTestContext context =
            await RealModelTestContext.CreateAsync();
        using TemporaryProbeSandbox sandbox = context.CreateProbeSandbox();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        string evidencePath = context.CreateEvidencePath("locked-model");

        ProbeExecutionResult process;
        await using (
            var exclusiveLock = new FileStream(
                context.Model.ModelPath,
                FileMode.Open,
                System.IO.FileAccess.Read,
                FileShare.None))
        {
            process = await new ProbeProcessRunner().RunAsync(
                sandbox.CreateRequest(
                    new[]
                    {
                        "--model", context.Model.ModelPath,
                        "--output", evidencePath
                    },
                    TimeSpan.FromSeconds(30)),
                CancellationToken.None);
        }

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(1, process.ExitCode);

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        JsonElement root = document.RootElement;
        Assert.AreEqual("Failed", root.GetProperty("completionStatus").GetString());
        Assert.AreEqual(
            "MI-OP-MODEL-FILE-IO",
            root.GetProperty("failureCode").GetString());
        Assert.AreEqual(
            expectedHash,
            await ModelFileHash.ComputeSha256Async(
                context.Model.ModelPath,
                CancellationToken.None));
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            await File.ReadAllTextAsync(evidencePath),
            context.Model.ModelPath);
    }
}
