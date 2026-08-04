using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that local evidence can never overwrite the selected model file.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ModelProbeSafetyValidatorTests
{
    [TestMethod]
    public void ValidateOutputPath_WithDistinctPaths_Succeeds()
    {
        ModelProbeSafetyValidationResult result =
            ModelProbeSafetyValidator.ValidateOutputPath(
                "models/granite.gguf",
                "artifacts/model-probe.json");

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateOutputPath_WithEquivalentCanonicalPaths_Fails()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ModelProbeSafety");
        string modelPath = Path.Combine(root, "granite.gguf");
        string equivalentOutputPath = Path.Combine(
            root,
            ".",
            "granite.gguf");

        ModelProbeSafetyValidationResult result =
            ModelProbeSafetyValidator.ValidateOutputPath(
                modelPath,
                equivalentOutputPath);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(
            result.ErrorMessage,
            "must not overwrite the selected model");
    }
}
