using GraniteEdgeAI.Features.ModelImport.ModelDownload;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class PinnedGraniteModelCatalogTests
{
    [TestMethod]
    public void Entries_PinExactOfficialArtifacts()
    {
        (string Label, string File, long Bytes, string Sha)[] expected =
        [
            ("Maximum efficiency", "granite-4.0-h-micro-Q2_K.gguf", 1_226_247_840, "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead"),
            ("Efficient", "granite-4.0-h-micro-Q3_K_M.gguf", 1_555_472_032, "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29"),
            ("Balanced", "granite-4.0-h-micro-Q4_K_M.gguf", 1_942_564_512, "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e"),
            ("High capability", "granite-4.0-h-micro-Q5_K_M.gguf", 2_273_455_776, "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c"),
            ("Maximum capability", "granite-4.0-h-micro-Q8_0.gguf", 3_397_676_704, "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59")
        ];

        CollectionAssert.AreEqual(
            expected,
            PinnedGraniteModelCatalog.Entries
                .Select(entry =>
                    (entry.PreferenceLabel, entry.FileName, entry.ExpectedByteLength, entry.ExpectedSha256))
                .ToArray());
    }

    [TestMethod]
    [DataRow(0d, "Q2_K")]
    [DataRow(19.999d, "Q2_K")]
    [DataRow(20d, "Q3_K_M")]
    [DataRow(39.999d, "Q3_K_M")]
    [DataRow(40d, "Q4_K_M")]
    [DataRow(59.999d, "Q4_K_M")]
    [DataRow(60d, "Q5_K_M")]
    [DataRow(79.999d, "Q5_K_M")]
    [DataRow(80d, "Q8_0")]
    [DataRow(100d, "Q8_0")]
    public void ForSliderValue_MapsEveryBoundary(double value, string quantisation)
    {
        Assert.AreEqual(
            quantisation,
            PinnedGraniteModelCatalog.ForSliderValue(value).Quantisation);
    }

    [TestMethod]
    [DataRow(-0.001d)]
    [DataRow(100.001d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void ForSliderValue_RejectsValuesOutsideFiniteRange(double value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => PinnedGraniteModelCatalog.ForSliderValue(value));
    }

    [TestMethod]
    public void Balanced_ResolvesPinnedHttpsUriAndHumanReadableSize()
    {
        ModelDownloadCatalogEntry balanced = PinnedGraniteModelCatalog.ForSliderValue(50);

        Assert.AreEqual("1.94 GB", balanced.DownloadSizeText);
        Assert.AreEqual(
            "https://huggingface.co/ibm-granite/granite-4.0-h-micro-GGUF/resolve/51ce07a9c9cfa971ca359d9625836bf8a4a1b61f/granite-4.0-h-micro-Q4_K_M.gguf?download=true",
            balanced.ResolveUri.AbsoluteUri);
    }
}
