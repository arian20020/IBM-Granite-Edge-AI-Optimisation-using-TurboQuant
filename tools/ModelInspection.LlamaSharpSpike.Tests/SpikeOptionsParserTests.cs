using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies the deliberately small command-line contract of the native-backend
/// smoke tool.
/// </summary>
[TestClass]
public sealed class SpikeOptionsParserTests
{
    [TestMethod]
    public void Parse_WithoutArguments_UsesIgnoredDefaultOutputPath()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(Array.Empty<string>());

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsFalse(result.Options.ShowHelp);
        Assert.AreEqual(
            SpikeOptions.DefaultOutputPath,
            result.Options.OutputPath);
    }

    [TestMethod]
    public void Parse_WithOutputOption_PreservesSuppliedPath()
    {
        const string outputPath =
            "output/runtime-smoke.json";

        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--output", outputPath });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(
            outputPath,
            result.Options.OutputPath);
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
    public void Parse_WithMissingOutputPath_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--output" });

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Options);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(
            result.ErrorMessage,
            "requires a path");
    }

    [TestMethod]
    public void Parse_WithUnknownArgument_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--unexpected" });

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Options);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(
            result.ErrorMessage,
            "Unknown argument");
    }
}
