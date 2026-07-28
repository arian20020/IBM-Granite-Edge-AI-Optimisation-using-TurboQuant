using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using System.Globalization;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ImportedModelCardDataMapperTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Create_FormatsAllVisibleValuesUsingProvidedCulture()
    {
        ModelQuickScanResult result = CreateSuccessResult(
            parameterSizeLabel: " 8B ",
            quantization: " Q4_K_M ",
            fileSizeBytes: 5_100_000_000L,
            contextLength: 131_072UL);

        ImportedModelCardData cardData =
            ImportedModelCardDataMapper.Create(
                "granite.gguf",
                result,
                CultureInfo.GetCultureInfo("en-GB"));

        Assert.AreEqual("granite.gguf", cardData.FileName);
        Assert.AreEqual("IBM Granite", cardData.ModelName);
        Assert.AreEqual("8B", cardData.Parameters);
        Assert.AreEqual("Granite", cardData.Architecture);
        Assert.AreEqual("Q4_K_M", cardData.Quantization);
        Assert.AreEqual("5.1 GB", cardData.FileSize);
        Assert.AreEqual("128K tokens", cardData.DeclaredContext);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_MissingOptionalMetadata_UsesVisibleFallbackLabels()
    {
        ModelQuickScanResult result = CreateSuccessResult(
            parameterSizeLabel: null,
            quantization: null,
            fileSizeBytes: 320L,
            contextLength: null);

        ImportedModelCardData cardData =
            ImportedModelCardDataMapper.Create(
                "minimal.gguf",
                result,
                CultureInfo.GetCultureInfo("en-GB"));

        Assert.AreEqual("Unknown", cardData.Parameters);
        Assert.AreEqual("Unknown", cardData.Quantization);
        Assert.AreEqual("320 B", cardData.FileSize);
        Assert.AreEqual("Not declared", cardData.DeclaredContext);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_UsesTheSuppliedDecimalSeparator()
    {
        ModelQuickScanResult result = CreateSuccessResult(
            parameterSizeLabel: "3B",
            quantization: "Q4_K_M",
            fileSizeBytes: 5_100_000_000L,
            contextLength: 131_072UL);

        ImportedModelCardData cardData =
            ImportedModelCardDataMapper.Create(
                "granite.gguf",
                result,
                CultureInfo.GetCultureInfo("fr-FR"));

        Assert.AreEqual("5,1 GB", cardData.FileSize);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_ExactMillionTokenContext_UsesMTokenLabel()
    {
        ModelQuickScanResult result = CreateSuccessResult(
            parameterSizeLabel: "3B",
            quantization: "Q4_K_M",
            fileSizeBytes: 2_000_000_000L,
            contextLength: 1_048_576UL);

        ImportedModelCardData cardData =
            ImportedModelCardDataMapper.Create(
                "granite.gguf",
                result,
                CultureInfo.GetCultureInfo("en-GB"));

        Assert.AreEqual("1M tokens", cardData.DeclaredContext);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_NonPowerOfTwoContext_UsesFullTokenCount()
    {
        ModelQuickScanResult result = CreateSuccessResult(
            parameterSizeLabel: "3B",
            quantization: "Q4_K_M",
            fileSizeBytes: 2_000_000_000L,
            contextLength: 100_000UL);

        ImportedModelCardData cardData =
            ImportedModelCardDataMapper.Create(
                "granite.gguf",
                result,
                CultureInfo.GetCultureInfo("en-GB"));

        Assert.AreEqual("100,000 tokens", cardData.DeclaredContext);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Create_FailureResult_ThrowsArgumentException()
    {
        ModelQuickScanResult failure =
            ModelQuickScanResult.CreateFailure(
                "invalid-magic",
                "The file is not a valid GGUF model.",
                "Unexpected magic bytes at offset zero.");

        Assert.Throws<ArgumentException>(
            () => ImportedModelCardDataMapper.Create(
                "broken.gguf",
                failure,
                CultureInfo.GetCultureInfo("en-GB")));
    }

    private static ModelQuickScanResult CreateSuccessResult(
        string? parameterSizeLabel,
        string? quantization,
        long fileSizeBytes,
        ulong? contextLength)
    {
        return ModelQuickScanResult.CreateSuccess(
            modelName: "IBM Granite",
            architecture: "granite",
            parameterSizeLabel: parameterSizeLabel,
            quantization: quantization,
            fileSizeBytes: fileSizeBytes,
            contextLength: contextLength,
            ggufVersion: 3);
    }
}
