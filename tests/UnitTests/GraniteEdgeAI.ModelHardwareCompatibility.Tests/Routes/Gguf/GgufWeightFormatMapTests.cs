using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufWeightFormatMapTests
{
    [TestMethod]
    public void ToCanonical_ImportedHasNoCanonicalQuantisation()
    {
        // "Imported" means "whatever the file already is", which is a property of
        // the file and not of the requested format
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            GgufWeightFormatMap.ToCanonical(GgufWeightFormat.Imported).ToString());
    }

    [TestMethod]
    [DataRow(nameof(GgufWeightFormat.BF16), nameof(WeightQuantisation.BF16))]
    [DataRow(nameof(GgufWeightFormat.F16), nameof(WeightQuantisation.F16))]
    [DataRow(nameof(GgufWeightFormat.Q8_0), nameof(WeightQuantisation.Q8_0))]
    [DataRow(nameof(GgufWeightFormat.Q6K), nameof(WeightQuantisation.Q6_K))]
    [DataRow(nameof(GgufWeightFormat.Q5KM), nameof(WeightQuantisation.Q5_K_M))]
    [DataRow(nameof(GgufWeightFormat.Q4KM), nameof(WeightQuantisation.Q4_K_M))]
    [DataRow(nameof(GgufWeightFormat.Q3KM), nameof(WeightQuantisation.Q3_K_M))]
    public void ToCanonical_MapsEveryGeneratableFormat(string format, string expected)
    {
        GgufWeightFormat parsed = Enum.Parse<GgufWeightFormat>(format);

        Assert.AreEqual(expected, GgufWeightFormatMap.ToCanonical(parsed).ToString());
    }

    [TestMethod]
    public void ToCanonical_UnspecifiedHasNoCanonicalQuantisation()
    {
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            GgufWeightFormatMap.ToCanonical(GgufWeightFormat.Unspecified).ToString());
    }
}
