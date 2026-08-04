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
        Assert.IsNull(result.Options.CancelNativeAfterMilliseconds);
        Assert.AreEqual(
            SpikeOptions.NativeSmokeDefaultOutputPath,
            result.Options.OutputPath);
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    public void Parse_WithHelpOption_ReturnsHelpRequest(string helpOption)
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { helpOption });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.ShowHelp);
    }

    [TestMethod]
    public void Parse_WithHelpAndAnotherOption_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--help", "--output", "evidence.json" });

        AssertFailedWith(result, "help option");
    }

    [TestMethod]
    public void Parse_WithModel_UsesVocabOnlyProbeDefaults()
    {
        const string modelPath = "models/granite.gguf";

        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--model", modelPath });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.RunsModelProbe);
        Assert.AreEqual(modelPath, result.Options.ModelPath);
        Assert.AreEqual(
            SpikeOptions.VocabOnlyModelProbeDefaultOutputPath,
            result.Options.OutputPath);
    }

    [TestMethod]
    [DataRow("models/model with spaces.gguf")]
    [DataRow("models/グラナイト.gguf")]
    public void Parse_WithSpaceOrUnicodeModelPath_PreservesValue(
        string modelPath)
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--model", modelPath });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(modelPath, result.Options.ModelPath);
    }

    [TestMethod]
    public void Parse_WithModelOutputAndOperationCancellation_AllowsAnyOptionOrder()
    {
        const string modelPath = "models/granite.gguf";
        const string outputPath = "output/model-probe.json";

        SpikeOptionsParseResult result = SpikeOptionsParser.Parse(
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
        Assert.IsNull(result.Options.CancelNativeAfterMilliseconds);
    }

    [TestMethod]
    public void Parse_WithModelOutputAndNativeCancellation_AllowsAnyOptionOrder()
    {
        const string modelPath = "models/granite.gguf";
        const string outputPath = "output/model-probe.json";

        SpikeOptionsParseResult result = SpikeOptionsParser.Parse(
            new[]
            {
                "--output", outputPath,
                "--cancel-native-after-ms", "300",
                "--model", modelPath
            });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(modelPath, result.Options.ModelPath);
        Assert.AreEqual(outputPath, result.Options.OutputPath);
        Assert.IsNull(result.Options.CancelAfterMilliseconds);
        Assert.AreEqual(300, result.Options.CancelNativeAfterMilliseconds);
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
    [DataRow("--model")]
    [DataRow("--output")]
    [DataRow("--cancel-after-ms")]
    [DataRow("--cancel-native-after-ms")]
    public void Parse_WithMissingOptionValue_ReturnsControlledError(
        string option)
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { option });

        AssertFailedWith(result, option);
        AssertFailedWith(result, "requires");
    }

    [TestMethod]
    [DataRow("--model")]
    [DataRow("--output")]
    public void Parse_WithWhitespacePath_ReturnsControlledError(
        string option)
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { option, "   " });

        AssertFailedWith(result, option);
    }

    [TestMethod]
    public void Parse_WithOperationCancellationButNoModel_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--cancel-after-ms", "250" });

        AssertFailedWith(result, "--cancel-after-ms");
        AssertFailedWith(result, "--model");
    }

    [TestMethod]
    public void Parse_WithNativeCancellationButNoModel_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(
                new[] { "--cancel-native-after-ms", "250" });

        AssertFailedWith(result, "--cancel-native-after-ms");
        AssertFailedWith(result, "--model");
    }

    [TestMethod]
    public void Parse_WithBothCancellationModes_ReturnsControlledError()
    {
        SpikeOptionsParseResult result = SpikeOptionsParser.Parse(
            new[]
            {
                "--model", "granite.gguf",
                "--cancel-after-ms", "10",
                "--cancel-native-after-ms", "10"
            });

        AssertFailedWith(result, "--cancel-after-ms");
        AssertFailedWith(result, "--cancel-native-after-ms");
        AssertFailedWith(result, "mutually exclusive");
    }

    [TestMethod]
    [DataRow("--cancel-after-ms", "0")]
    [DataRow("--cancel-after-ms", "-1")]
    [DataRow("--cancel-after-ms", "1.5")]
    [DataRow("--cancel-after-ms", "not-a-number")]
    [DataRow("--cancel-after-ms", "2147483648")]
    [DataRow("--cancel-native-after-ms", "0")]
    [DataRow("--cancel-native-after-ms", "-1")]
    [DataRow("--cancel-native-after-ms", "1.5")]
    [DataRow("--cancel-native-after-ms", "not-a-number")]
    [DataRow("--cancel-native-after-ms", "2147483648")]
    public void Parse_WithInvalidCancellationDelay_ReturnsControlledError(
        string option,
        string value)
    {
        SpikeOptionsParseResult result = SpikeOptionsParser.Parse(
            new[]
            {
                "--model", "models/granite.gguf",
                option, value
            });

        AssertFailedWith(result, option);
        AssertFailedWith(result, "positive whole number");
    }

    [TestMethod]
    [DataRow("--model", "models/one.gguf", "models/two.gguf")]
    [DataRow("--output", "one.json", "two.json")]
    [DataRow("--cancel-after-ms", "10", "20")]
    [DataRow("--cancel-native-after-ms", "10", "20")]
    public void Parse_WithDuplicateOption_ReturnsControlledError(
        string option,
        string firstValue,
        string secondValue)
    {
        var arguments = new List<string>();

        if (option is not "--model")
        {
            arguments.AddRange(new[] { "--model", "model.gguf" });
        }

        arguments.AddRange(
            new[] { option, firstValue, option, secondValue });

        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(arguments);

        AssertFailedWith(result, option);
        AssertFailedWith(result, "specified more than once");
    }

    [TestMethod]
    public void Parse_WithUnknownArgument_ReturnsControlledError()
    {
        SpikeOptionsParseResult result =
            SpikeOptionsParser.Parse(new[] { "--unexpected" });

        AssertFailedWith(result, "Unknown argument");
    }

    [TestMethod]
    public void Parse_WithNullArguments_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => SpikeOptionsParser.Parse(null!));
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
