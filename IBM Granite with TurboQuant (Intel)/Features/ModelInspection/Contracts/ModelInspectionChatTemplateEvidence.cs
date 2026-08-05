namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Records only the presence, bounded length, and digest of a chat template.
/// Complete template text is deliberately excluded from application evidence.
/// </summary>
internal sealed record ModelInspectionChatTemplateEvidence
{
    /// <summary>
    /// Creates immutable, data-minimised chat-template evidence.
    /// </summary>
    internal ModelInspectionChatTemplateEvidence(
        bool? present,
        int? lengthCharacters,
        string? sha256)
    {
        // Unknown or absent templates cannot truthfully carry length or digest.
        if (present is not true)
        {
            if (lengthCharacters.HasValue || sha256 is not null)
            {
                throw new ArgumentException(
                    "An unavailable chat template cannot carry length or digest evidence.",
                    nameof(present));
            }

            Present = present;
            LengthCharacters = null;
            Sha256 = null;
            return;
        }

        // A present template must have positive length and a valid digest.
        if (lengthCharacters is not > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthCharacters),
                lengthCharacters,
                "A present chat template requires a positive length.");
        }

        Present = true;
        LengthCharacters = lengthCharacters;
        Sha256 = ModelInspectionContractValidation.RequireHexDigest(
            sha256,
            expectedLength: 64,
            nameof(sha256));
    }

    internal bool? Present { get; }

    internal int? LengthCharacters { get; }

    internal string? Sha256 { get; }
}
