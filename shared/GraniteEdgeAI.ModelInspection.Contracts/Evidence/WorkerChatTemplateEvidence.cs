namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Summarises embedded chat-template presence without crossing the complete
/// template text over the process boundary.
/// </summary>
public sealed record WorkerChatTemplateEvidence
{
    private const string EmptySha256 =
        "e3b0c44298fc1c149afbf4c8996fb924" +
        "27ae41e4649b934ca495991b7852b855";

    public bool Present { get; init; }

    public int? LengthCharacters { get; init; }

    public string? Sha256 { get; init; }

    /// <summary>
    /// Verifies the data-minimised presence, length, and digest relationship.
    /// </summary>
    public void Validate()
    {
        if (!Present)
        {
            WorkerProtocolValidation.Require(
                !LengthCharacters.HasValue && Sha256 is null,
                nameof(Present),
                "cannot carry length or digest evidence when false");
            return;
        }

        // Protocol v1 historically allowed a present key with empty content.
        // Current producers classify that content as unusable before mapping,
        // while consumers retain the ability to parse the legacy wire shape.
        WorkerProtocolValidation.RequireOptionalNonNegative(
            LengthCharacters,
            nameof(LengthCharacters));
        WorkerProtocolValidation.Require(
            LengthCharacters.HasValue,
            nameof(LengthCharacters),
            "must be present when the chat template is present");
        WorkerProtocolValidation.RequireHexDigest(Sha256, nameof(Sha256));
        WorkerProtocolValidation.Require(
            LengthCharacters != 0 || string.Equals(
                Sha256,
                EmptySha256,
                StringComparison.OrdinalIgnoreCase),
            nameof(Sha256),
            "must identify empty input when the reported length is zero");
    }
}
