namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Carries one validated model selection from Model Import into the complete
/// Model Inspection use case.
/// </summary>
internal sealed record ModelInspectionRequest
{
    /// <summary>
    /// Creates an immutable request without opening or modifying the model.
    /// </summary>
    internal ModelInspectionRequest(
        string modelPath,
        string fileName,
        ExpectedModelFileIdentity expectedFileIdentity,
        ValidatedQuickScanSnapshot quickScan)
    {
        // Preserve one fully qualified authoritative local model path.
        string validatedPath = ModelInspectionContractValidation.RequireText(
            modelPath,
            nameof(modelPath));
        if (!Path.IsPathFullyQualified(validatedPath))
        {
            throw new ArgumentException(
                "Model path must be fully qualified.",
                nameof(modelPath));
        }

        // Keep a path-minimised display name and prove it agrees with the path.
        string validatedFileName =
            ModelInspectionContractValidation.RequireFinalFileName(
                fileName,
                nameof(fileName));
        string pathFileName = Path.GetFileName(validatedPath);
        if (!string.Equals(
                pathFileName,
                validatedFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "File name must match the final segment of the model path.",
                nameof(fileName));
        }

        ArgumentNullException.ThrowIfNull(expectedFileIdentity);
        ArgumentNullException.ThrowIfNull(quickScan);

        // The import identity and quick-scan snapshot must describe one file.
        if (expectedFileIdentity.LengthBytes != quickScan.FileSizeBytes)
        {
            throw new ArgumentException(
                "Expected file length must match the validated quick-scan size.",
                nameof(expectedFileIdentity));
        }

        ModelPath = validatedPath;
        FileName = validatedFileName;
        ExpectedFileIdentity = expectedFileIdentity;
        QuickScan = quickScan;
    }

    internal string ModelPath { get; }

    internal string FileName { get; }

    internal ExpectedModelFileIdentity ExpectedFileIdentity { get; }

    internal ValidatedQuickScanSnapshot QuickScan { get; }
}
