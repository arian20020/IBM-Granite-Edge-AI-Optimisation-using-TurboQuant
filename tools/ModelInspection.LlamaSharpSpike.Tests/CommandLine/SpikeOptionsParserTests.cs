using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies the command-line contract shared by the native smoke and the
/// VocabOnly model-probe modes.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class SpikeOptionsParserTests
{
    [TestMethod]
    public void Parse_WithoutArguments_UsesNativeSmokeDefaults()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(Array.Empty<string>());

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsFalse(result.Options.ShowHelp);
        Assert.IsFalse(result.Options.RunsModelProbe);
        Assert.IsNull(result.Options.ModelPath);
        Assert.IsNull(result.Options.CancelAfterMilliseconds);
        Assert.AreEqual(
            SpikeOptions.NativeSmokeDefaultOutputPath,
            result.Options.OutputPath);
    }

    [TestMethod]
    public void Parse_WithModel_UsesVocabOnlyProbeDefaults()
    {
        const string modelPath = "models/granite.gguf";

        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--model", modelPath });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.RunsModelProbe);
        Assert.AreEqual(modelPath, result.Options.ModelPath);
        Assert.AreEqual(
            SpikeOptions.VocabOnlyModelProbeDefaultOutputPath,
            result.Options.OutputPath);
    }

    [TestMethod]
    public void Parse_WithModelOutputAndCancellation_AllowsAnyOptionOrder()
    {
        const string modelPath = "models/granite.gguf";
        const string outputPath = "output/model-probe.json";

        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[]
                {
                    "--cancel-after-ms", "250",
                    "--output", outputPath,
                    "--model", modelPath
                });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(modelPath, result.Options.ModelPath);
        Assert.AreEqual(outputPath, result.Options.OutputPath);
        Assert.AreEqual(250, result.Options.CancelAfterMilliseconds);
    }

    [TestMethod]
    public void Parse_WithOutputOption_PreservesSuppliedNativeSmokePath()
    {
        const string outputPath = "output/runtime-smoke.json";

        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--output", outputPath });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsFalse(result.Options.RunsModelProbe);
        Assert.AreEqual(outputPath, result.Options.OutputPath);
    }

    [TestMethod]
    public void Parse_WithHelpOption_ReturnsHelpRequest()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--help" });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.ShowHelp);
    }

    [TestMethod]
    public void Parse_WithMissingModelPath_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--model" });

        AssertFailedWith(result, "--model option requires a path");
    }

    [TestMethod]
    public void Parse_WithMissingOutputPath_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--output" });

        AssertFailedWith(result, "--output option requires a path");
    }

    [TestMethod]
    public void Parse_WithCancellationButNoModel_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--cancel-after-ms", "250" });

        AssertFailedWith(result, "--cancel-after-ms");
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "--model");
    }

    [TestMethod]
    public void Parse_WithInvalidCancellationDelay_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[]
                {
                    "--model", "models/granite.gguf",
                    "--cancel-after-ms", "0"
                });

        AssertFailedWith(result, "positive whole number");
    }

    [TestMethod]
    public void Parse_WithDuplicateModelOption_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[]
                {
                    "--model", "models/one.gguf",
                    "--model", "models/two.gguf"
                });

        AssertFailedWith(result, "specified more than once");
    }

    [TestMethod]
    public void Parse_WithUnknownArgument_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--unexpected" });

        AssertFailedWith(result, "Unknown argument");
    }

    private static void AssertFailedWith(
        SpikeOptionsParseResult result,
        string expectedMessagePart)
    {
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Options);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, expectedMessagePart);
    }
}
