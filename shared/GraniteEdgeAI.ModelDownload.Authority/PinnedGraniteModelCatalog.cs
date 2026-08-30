using System.Collections.ObjectModel;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

public static class PinnedGraniteModelCatalog
{
    private const string RepositoryId = "ibm-granite/granite-4.0-h-micro-GGUF";
    private const string Revision = "51ce07a9c9cfa971ca359d9625836bf8a4a1b61f";

    private static readonly ReadOnlyCollection<ModelDownloadCatalogEntry> CatalogEntries =
        Array.AsReadOnly<ModelDownloadCatalogEntry>(
        [
            Create(OptimizationPreferenceBand.MaximumEfficiency,
                "granite-4.0-h-micro-q2-k", "Maximum efficiency", "Q2_K", 0, 20, false,
                "granite-4.0-h-micro-Q2_K.gguf", 1_226_247_840,
                "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead"),
            Create(OptimizationPreferenceBand.Efficient,
                "granite-4.0-h-micro-q3-k-m", "Efficient", "Q3_K_M", 20, 40, false,
                "granite-4.0-h-micro-Q3_K_M.gguf", 1_555_472_032,
                "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29"),
            Create(OptimizationPreferenceBand.Balanced,
                "granite-4.0-h-micro-q4-k-m", "Balanced", "Q4_K_M", 40, 60, false,
                "granite-4.0-h-micro-Q4_K_M.gguf", 1_942_564_512,
                "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e"),
            Create(OptimizationPreferenceBand.HighCapability,
                "granite-4.0-h-micro-q5-k-m", "High capability", "Q5_K_M", 60, 80, false,
                "granite-4.0-h-micro-Q5_K_M.gguf", 2_273_455_776,
                "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c"),
            Create(OptimizationPreferenceBand.MaximumCapability,
                "granite-4.0-h-micro-q8-0", "Maximum capability", "Q8_0", 80, 100, true,
                "granite-4.0-h-micro-Q8_0.gguf", 3_397_676_704,
                "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59")
        ]);

    public static IReadOnlyList<ModelDownloadCatalogEntry> Entries => CatalogEntries;

    public static ModelDownloadCatalogEntry ForPreferenceBand(OptimizationPreferenceBand band) =>
        CatalogEntries.SingleOrDefault(entry => entry.Band == band)
        ?? throw new ArgumentOutOfRangeException(nameof(band));

    public static ModelDownloadCatalogEntry ForSliderValue(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value));
        return CatalogEntries.First(entry => entry.ContainsSliderValue(value));
    }

    private static ModelDownloadCatalogEntry Create(
        OptimizationPreferenceBand band,
        string id,
        string preferenceLabel,
        string quantisation,
        double minimumSliderValue,
        double maximumSliderValue,
        bool includesMaximum,
        string fileName,
        long expectedByteLength,
        string expectedSha256) =>
        new(band, id, preferenceLabel, quantisation, minimumSliderValue,
            maximumSliderValue, includesMaximum, RepositoryId, Revision,
            fileName, expectedByteLength, expectedSha256);
}
