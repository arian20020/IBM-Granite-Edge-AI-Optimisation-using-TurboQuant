namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Describes a controlled worker or infrastructure failure that prevents a
/// reliable model classification.
/// </summary>
public sealed record WorkerOperationalFailure
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Verifies that the operational failure is stable and user-safe.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(Code),
            nameof(Code),
            "must not be empty");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(Message),
            nameof(Message),
            "must not be empty");
    }
}
