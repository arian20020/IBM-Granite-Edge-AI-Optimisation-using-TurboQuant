using System.Text.RegularExpressions;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed class ModelDownloadCatalogEntry
{
    private static readonly Regex Sha256Pattern = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

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
    {
        Id = RequireText(id, nameof(id));
        PreferenceLabel = RequireText(preferenceLabel, nameof(preferenceLabel));
        Quantisation = RequireText(quantisation, nameof(quantisation));
        RepositoryId = RequireText(repositoryId, nameof(repositoryId));
        Revision = RequireText(revision, nameof(revision));
        FileName = RequireSafeFileName(fileName);

        if (!double.IsFinite(minimumSliderValue) ||
            !double.IsFinite(maximumSliderValue) ||
            minimumSliderValue < 0 ||
            maximumSliderValue > 100 ||
            minimumSliderValue >= maximumSliderValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSliderValue),
                "Slider bounds must describe a non-empty range inside 0 through 100.");
        }

        if (expectedByteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedByteLength));
        }

        if (expectedSha256 is null || !Sha256Pattern.IsMatch(expectedSha256))
        {
            throw new ArgumentException(
                "The expected SHA-256 must be exactly 64 lowercase hexadecimal characters.",
                nameof(expectedSha256));
        }

        MinimumSliderValue = minimumSliderValue;
        MaximumSliderValue = maximumSliderValue;
        IncludesMaximum = includesMaximum;
        ExpectedByteLength = expectedByteLength;
        ExpectedSha256 = expectedSha256;
    }

    internal string Id { get; }

    internal string PreferenceLabel { get; }

    internal string Quantisation { get; }

    internal double MinimumSliderValue { get; }

    internal double MaximumSliderValue { get; }

    internal bool IncludesMaximum { get; }

    internal string RepositoryId { get; }

    internal string Revision { get; }

    internal string FileName { get; }

    internal long ExpectedByteLength { get; }

    internal string ExpectedSha256 { get; }

    internal Uri ResolveUri => new(
        $"https://huggingface.co/{RepositoryId}/resolve/{Revision}/{FileName}?download=true",
        UriKind.Absolute);

    internal string DownloadSizeText =>
        $"{ExpectedByteLength / 1_000_000_000d:0.00} GB";

    internal bool ContainsSliderValue(double value) =>
        value >= MinimumSliderValue &&
        (value < MaximumSliderValue || (IncludesMaximum && value == MaximumSliderValue));

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("A non-empty canonical value is required.", parameterName);
        }

        return value;
    }

    private static string RequireSafeFileName(string value)
    {
        string fileName = RequireText(value, nameof(value));
        if (!fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("A safe GGUF file name is required.", nameof(value));
        }

        return fileName;
    }
}
