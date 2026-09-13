namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Carries the successful, bounded GGUF quick-scan facts into the worker.
/// </summary>
public sealed record WorkerQuickScanSnapshot
{
    public string Format { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public string Architecture { get; init; } = string.Empty;

    public string? ParameterSizeLabel { get; init; }

    public string? Quantisation { get; init; }

    public long FileSizeBytes { get; init; }

    public ulong? DeclaredContextLength { get; init; }

    public uint GgufVersion { get; init; }

    /// <summary>
    /// Verifies that the snapshot represents one successful GGUF quick scan.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.Require(
            string.Equals(Format, "GGUF", StringComparison.Ordinal),
            nameof(Format),
            "must equal GGUF");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(ModelName),
            nameof(ModelName),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(Architecture),
            nameof(Architecture),
            "must not be empty");
        WorkerProtocolValidation.Require(
            FileSizeBytes > 0,
            nameof(FileSizeBytes),
            "must be positive");
        WorkerProtocolValidation.Require(
            GgufVersion > 0,
            nameof(GgufVersion),
            "must be positive");
    }
}
