namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Records one stable technical observation for later application mapping.
/// </summary>
public sealed record WorkerObservation
{
    public string Code { get; init; } = string.Empty;

    public string TechnicalCategory { get; init; } = string.Empty;

    public string TechnicalDetail { get; init; } = string.Empty;

    /// <summary>
    /// Verifies that the observation can be identified and interpreted.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(Code),
            nameof(Code),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(TechnicalCategory),
            nameof(TechnicalCategory),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(TechnicalDetail),
            nameof(TechnicalDetail),
            "must not be empty");
    }
}
