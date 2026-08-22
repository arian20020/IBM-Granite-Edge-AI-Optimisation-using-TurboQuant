using System;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Preserves the bounded facts produced by one successful GGUF quick scan
/// without retaining the Model Import scanner result object.
/// </summary>
internal sealed record ValidatedQuickScanSnapshot
{
    private ValidatedQuickScanSnapshot(
        string modelName,
        string architecture,
        string? parameterSizeLabel,
        string? quantisation,
        long fileSizeBytes,
        ulong? declaredContextLength,
        uint ggufVersion)
    {
        // This first production route accepts only validated GGUF snapshots.
        Format = "GGUF";
        ModelName = ModelInspectionContractValidation.RequireText(
            modelName,
            nameof(modelName));
        Architecture = ModelInspectionContractValidation.RequireText(
            architecture,
            nameof(architecture));
        ParameterSizeLabel =
            ModelInspectionContractValidation.RequireOptionalText(
                parameterSizeLabel,
                nameof(parameterSizeLabel));
        Quantisation = ModelInspectionContractValidation.RequireOptionalText(
            quantisation,
            nameof(quantisation));

        // The snapshot must still describe a non-empty model file.
        if (fileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSizeBytes),
                fileSizeBytes,
                "Model file size must be positive.");
        }

        if (declaredContextLength.HasValue &&
            declaredContextLength.Value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(declaredContextLength),
                declaredContextLength,
                "An available context length must be positive.");
        }

        if (ggufVersion == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ggufVersion),
                ggufVersion,
                "GGUF version must be positive.");
        }

        FileSizeBytes = fileSizeBytes;
        DeclaredContextLength = declaredContextLength;
        GgufVersion = ggufVersion;
    }

    /// <summary>
    /// Creates a snapshot from a successful validated GGUF quick scan.
    /// </summary>
    internal static ValidatedQuickScanSnapshot CreateGguf(
        string modelName,
        string architecture,
        string? parameterSizeLabel,
        string? quantisation,
        long fileSizeBytes,
        ulong? declaredContextLength,
        uint ggufVersion)
    {
        return new ValidatedQuickScanSnapshot(
            modelName,
            architecture,
            parameterSizeLabel,
            quantisation,
            fileSizeBytes,
            declaredContextLength,
            ggufVersion);
    }

    internal string Format { get; }

    internal string ModelName { get; }

    internal string Architecture { get; }

    internal string? ParameterSizeLabel { get; }

    internal string? Quantisation { get; }

    internal long FileSizeBytes { get; }

    internal ulong? DeclaredContextLength { get; }

    internal uint GgufVersion { get; }
}
