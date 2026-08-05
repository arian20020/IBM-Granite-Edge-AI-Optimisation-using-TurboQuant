namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Captures the file identity observed when Model Import validated the model.
/// </summary>
public sealed record WorkerExpectedFileIdentity
{
    public long LengthBytes { get; init; }

    public DateTimeOffset LastWriteTimeUtc { get; init; }

    /// <summary>
    /// Verifies that the expected identity can represent one existing file.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.Require(
            LengthBytes > 0,
            nameof(LengthBytes),
            "must be positive");
        WorkerProtocolValidation.RequireUtcTimestamp(
            LastWriteTimeUtc,
            nameof(LastWriteTimeUtc));
    }
}
