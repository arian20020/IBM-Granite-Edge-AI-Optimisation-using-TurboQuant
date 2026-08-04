using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies deterministic failures that occur before LLamaSharp touches the
/// native runtime.
/// </summary>
[TestClass]
public sealed class VocabOnlyModelProbeFailureTests
{
    [TestMethod]
    public async Task RunAsync_WithMissingModel_ReturnsControlledFileFailure()
    {
        string modelPath = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-MissingModels",
            Guid.NewGuid().ToString("N"),
            "missing-granite.gguf");

        VocabOnlyModelProbeResult result =
            await new VocabOnlyModelProbe().RunAsync(
                modelPath,
                CancellationToken.None);

        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Failed,
            result.CompletionStatus);
        Assert.AreEqual(
            "MI-OP-MODEL-FILE-NOT-FOUND",
            result.FailureCode);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.BeforeSnapshot);
        Assert.IsNull(result.ModelEvidence);
        Assert.IsNotNull(result.FailureMessage);
        Assert.IsFalse(
            result.FailureMessage.Contains(
                Path.GetFullPath(modelPath),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal));
    }
}
