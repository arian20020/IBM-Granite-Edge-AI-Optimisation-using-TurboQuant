using System.Text.RegularExpressions;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

public sealed class ModelDownloadCatalogEntry
{
    private static readonly Regex Sha256Pattern = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal ModelDownloadCatalogEntry(
        OptimizationPreferenceBand band,
        string id,
        string preferenceLabel,
        string quantisation,
        double minimumSliderValue,
        double maximumSliderValue,
        bool includesMaximum,
        string repositoryId,
        string revision,
        string fileName,
        long expectedByteLength,
        string expectedSha256)
    {
        Band = band;
        Id = RequireText(id, nameof(id));
        PreferenceLabel = RequireText(preferenceLabel, nameof(preferenceLabel));
        Quantisation = RequireText(quantisation, nameof(quantisation));
        RepositoryId = RequireText(repositoryId, nameof(repositoryId));
        Revision = RequireText(revision, nameof(revision));
        FileName = RequireSafeFileName(fileName);
        if (!double.IsFinite(minimumSliderValue)
            || !double.IsFinite(maximumSliderValue)
            || minimumSliderValue < 0
            || maximumSliderValue > 100
            || minimumSliderValue >= maximumSliderValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSliderValue));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedByteLength);
        if (expectedSha256 is null || !Sha256Pattern.IsMatch(expectedSha256))
            throw new ArgumentException("A lowercase SHA-256 is required.", nameof(expectedSha256));

        MinimumSliderValue = minimumSliderValue;
        MaximumSliderValue = maximumSliderValue;
        IncludesMaximum = includesMaximum;
        ExpectedByteLength = expectedByteLength;
        ExpectedSha256 = expectedSha256;
    }

    internal ModelDownloadCatalogEntry(
        string id,
        string preferenceLabel,
        string quantisation,
        double minimumSliderValue,
        double maximumSliderValue,
        bool includesMaximum,
        string repositoryId,
        string revision,
        string fileName,
        long expectedByteLength,
        string expectedSha256)
        : this(
            BandForMinimumSliderValue(minimumSliderValue),
            id,
            preferenceLabel,
            quantisation,
            minimumSliderValue,
            maximumSliderValue,
            includesMaximum,
            repositoryId,
            revision,
            fileName,
            expectedByteLength,
            expectedSha256)
    {
    }

    public OptimizationPreferenceBand Band { get; }
    public string Id { get; }
    public string PreferenceLabel { get; }
    public string Quantisation { get; }
    public double MinimumSliderValue { get; }
    public double MaximumSliderValue { get; }
    public bool IncludesMaximum { get; }
    public string RepositoryId { get; }
    public string Revision { get; }
    public string FileName { get; }
    public long ExpectedByteLength { get; }
    public string ExpectedSha256 { get; }
    public Uri ResolveUri => new(
        $"https://huggingface.co/{RepositoryId}/resolve/{Revision}/{FileName}?download=true",
        UriKind.Absolute);
    public string DownloadSizeText => $"{ExpectedByteLength / 1_000_000_000d:0.00} GB";
    public bool ContainsSliderValue(double value) =>
        value >= MinimumSliderValue
        && (value < MaximumSliderValue || (IncludesMaximum && value == MaximumSliderValue));

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw new ArgumentException("A canonical value is required.", parameterName);
        return value;
    }

    private static OptimizationPreferenceBand BandForMinimumSliderValue(double value) => value switch
    {
        < 20 => OptimizationPreferenceBand.MaximumEfficiency,
        < 40 => OptimizationPreferenceBand.Efficient,
        < 60 => OptimizationPreferenceBand.Balanced,
        < 80 => OptimizationPreferenceBand.HighCapability,
        _ => OptimizationPreferenceBand.MaximumCapability
    };

    private static string RequireSafeFileName(string value)
    {
        string fileName = RequireText(value, nameof(value));
        if (!fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("A safe GGUF file name is required.", nameof(value));
        }
        return fileName;
    }
}
