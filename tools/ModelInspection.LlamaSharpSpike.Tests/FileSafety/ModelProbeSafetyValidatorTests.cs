using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
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
    public void ValidateOutputPath_WithExactSamePath_Fails()
    {
        AssertCollision("models/granite.gguf", "models/granite.gguf");
    }

    [TestMethod]
    public void ValidateOutputPath_WithDotSegmentEquivalentPath_Fails()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ModelProbeSafety");
        string modelPath = Path.Combine(root, "granite.gguf");
        string outputPath = Path.Combine(root, ".", "granite.gguf");

        AssertCollision(modelPath, outputPath);
    }

    [TestMethod]
    public void ValidateOutputPath_WithParentSegmentEquivalentPath_Fails()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ModelProbeSafety");
        string modelPath = Path.Combine(root, "granite.gguf");
        string outputPath = Path.Combine(
            root,
            "child",
            "..",
            "granite.gguf");

        AssertCollision(modelPath, outputPath);
    }

    [TestMethod]
    public void ValidateOutputPath_WithRelativeAndAbsoluteEquivalentPaths_Fails()
    {
        string relativePath = Path.Combine(
            "relative-models",
            "granite.gguf");
        string absolutePath = Path.GetFullPath(relativePath);

        AssertCollision(relativePath, absolutePath);
    }

    [TestMethod]
    public void ValidateOutputPath_OnWindowsWithCaseOnlyDifference_Fails()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        AssertCollision(
            @"C:\Models\Granite.gguf",
            @"c:\models\GRANITE.GGUF");
    }

    [TestMethod]
    public void ValidateOutputPath_WithSpacesAndUnicodeDistinctPaths_Succeeds()
    {
        ModelProbeSafetyValidationResult result =
            ModelProbeSafetyValidator.ValidateOutputPath(
                "models/Granite model/グラナイト.gguf",
                "artifacts/inspection result/結果.json");

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    [DataRow("", "output.json", "model path")]
    [DataRow("   ", "output.json", "model path")]
    [DataRow("model.gguf", "", "output path")]
    [DataRow("model.gguf", "   ", "output path")]
    public void ValidateOutputPath_WithBlankInput_ReturnsControlledError(
        string modelPath,
        string outputPath,
        string expectedMessagePart)
    {
        ModelProbeSafetyValidationResult result =
            ModelProbeSafetyValidator.ValidateOutputPath(
                modelPath,
                outputPath);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, expectedMessagePart);
    }

    [TestMethod]
    public void ValidateOutputPath_WithNullCharacter_ReturnsInvalidPathError()
    {
        ModelProbeSafetyValidationResult result =
            ModelProbeSafetyValidator.ValidateOutputPath(
                "model\0.gguf",
                "evidence.json");

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "invalid");
    }

    private static void AssertCollision(
        string modelPath,
        string outputPath)
    {
        ModelProbeSafetyValidationResult result =
            ModelProbeSafetyValidator.ValidateOutputPath(
                modelPath,
                outputPath);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(
            result.ErrorMessage,
            "must not overwrite the selected model");
    }
}
