namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Summarises embedded chat-template presence without crossing the complete
/// template text over the process boundary.
/// </summary>
public sealed record WorkerChatTemplateEvidence
{
    public bool Present { get; init; }

    public int? LengthCharacters { get; init; }

    public string? Sha256 { get; init; }
}
