using GraniteEdgeAI.Features.ModelImport.QuickScan;
using System.Globalization;

namespace GraniteEdgeAI.Features.ModelImport.Controls
{
    /// <summary>
    /// Converts successful scanner output into the formatted values displayed
    /// by the imported-model card.
    /// </summary>
    internal static class ImportedModelCardDataMapper
    {
        internal static ImportedModelCardData Create(
            string selectedFileName,
            ModelQuickScanResult scanResult,
            CultureInfo culture)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedFileName);
            ArgumentNullException.ThrowIfNull(scanResult);
            ArgumentNullException.ThrowIfNull(culture);

            if (scanResult.Outcome != ModelQuickScanOutcome.Success)
            {
                throw new ArgumentException(
                    "Imported-card data can only be created from a successful scan.",
                    nameof(scanResult));
            }

            return new ImportedModelCardData(
                FileName: selectedFileName,
                ModelName: string.IsNullOrWhiteSpace(scanResult.ModelName)
                    ? selectedFileName
                    : scanResult.ModelName,
                Parameters: FormatOptionalValue(
                    scanResult.ParameterSizeLabel),
                Architecture: FormatArchitecture(
                    scanResult.Architecture),
                Quantization: FormatOptionalValue(
                    scanResult.Quantization),
                FileSize: FormatFileSize(
                    scanResult.FileSizeBytes,
                    culture),
                DeclaredContext: FormatContextLength(
                    scanResult.ContextLength,
                    culture));
        }

        private static string FormatOptionalValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Unknown"
                : value.Trim();
        }

        private static string FormatArchitecture(string? architecture)
        {
            if (string.IsNullOrWhiteSpace(architecture))
            {
                return "Unknown";
            }

            string value = architecture.Trim();
            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        private static string FormatFileSize(
            long? fileSizeBytes,
            CultureInfo culture)
        {
            if (fileSizeBytes is null || fileSizeBytes <= 0)
            {
                return "Unknown";
            }

            double value = fileSizeBytes.Value;
            string unit;

            if (value >= 1_000_000_000_000)
            {
                value /= 1_000_000_000_000;
                unit = "TB";
            }
            else if (value >= 1_000_000_000)
            {
                value /= 1_000_000_000;
                unit = "GB";
            }
            else if (value >= 1_000_000)
            {
                value /= 1_000_000;
                unit = "MB";
            }
            else if (value >= 1_000)
            {
                value /= 1_000;
                unit = "KB";
            }
            else
            {
                unit = "B";
            }

            string numberFormat = unit == "B" ? "N0" : "0.#";
            return $"{value.ToString(numberFormat, culture)} {unit}";
        }

        private static string FormatContextLength(
            ulong? contextLength,
            CultureInfo culture)
        {
            if (contextLength is null)
            {
                return "Not declared";
            }

            const ulong OneK = 1_024;
            const ulong OneM = OneK * OneK;
            ulong value = contextLength.Value;

            if (value >= OneM && value % OneM == 0)
            {
                return $"{(value / OneM).ToString("N0", culture)}M tokens";
            }

            if (value >= OneK && value % OneK == 0)
            {
                return $"{(value / OneK).ToString("N0", culture)}K tokens";
            }

            return $"{value.ToString("N0", culture)} tokens";
        }
    }
}
