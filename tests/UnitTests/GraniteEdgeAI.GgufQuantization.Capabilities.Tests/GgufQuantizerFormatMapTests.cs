using GraniteEdgeAI.GgufQuantization.Capabilities;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.GgufQuantization.Capabilities.Tests;

[TestClass]
public sealed class GgufQuantizerFormatMapTests
{
    [TestMethod]
    [DataRow(GgufWeightFormat.Q2K, "Q2_K")]
    [DataRow(GgufWeightFormat.Q3KM, "Q3_K_M")]
    [DataRow(GgufWeightFormat.Q4KM, "Q4_K_M")]
    [DataRow(GgufWeightFormat.Q5KM, "Q5_K_M")]
    [DataRow(GgufWeightFormat.Q6K, "Q6_K")]
    [DataRow(GgufWeightFormat.Q8_0, "Q8_0")]
    public void ExactPersistentFormatsMapToClosedToolTokens(
        GgufWeightFormat format,
        string expected)
    {
        Assert.AreEqual(expected, GgufQuantizerFormatMap.ToToolToken(format));
    }

    [TestMethod]
    [DataRow(GgufWeightFormat.Unspecified)]
    [DataRow(GgufWeightFormat.Imported)]
    [DataRow(GgufWeightFormat.BF16)]
    [DataRow(GgufWeightFormat.F16)]
    public void NonTargetFormatsCannotBecomeToolTokens(GgufWeightFormat format)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufQuantizerFormatMap.ToToolToken(format));
    }

    [TestMethod]
    [DataRow(GgufCacheType.Turbo3)]
    [DataRow(GgufCacheType.Turbo4)]
    public void TurboQuantCacheFormatsCannotBeMisusedAsPersistentWeightFormats(
        GgufCacheType format)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufQuantizerFormatMap.ToToolToken(format));
    }

    [TestMethod]
    public void ContractAndCoreMappingsAgreeExactly()
    {
        Assert.AreEqual(
            GgufQuantizationFormat.Q4KM,
            GgufQuantizerFormatMap.ToContract(GgufWeightFormat.Q4KM));
        Assert.AreEqual(
            GgufWeightFormat.Q4KM,
            GgufQuantizerFormatMap.ToCore(GgufQuantizationFormat.Q4KM));
    }
}
