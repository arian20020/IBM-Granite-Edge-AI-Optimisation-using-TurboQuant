namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Composes the complete path-minimised technical evidence returned by one
/// successfully completed worker operation.
/// </summary>
public sealed record WorkerInspectionEvidence
{
    public WorkerRuntimeIdentity Runtime { get; init; } = new();

    public WorkerModelFileEvidence ModelFile { get; init; } = new();

    public WorkerModelConfigurationEvidence Configuration { get; init; } = new();

    public WorkerTokenizerEvidence Tokenizer { get; init; } = new();

    public WorkerChatTemplateEvidence ChatTemplate { get; init; } = new();

    public IReadOnlyList<WorkerObservation> Observations { get; init; } =
        Array.Empty<WorkerObservation>();

    /// <summary>
    /// Verifies mandatory evidence sections and every stable observation.
    /// </summary>
    public void Validate()
    {
        Runtime.Validate();
        ModelFile.Validate();

        WorkerProtocolValidation.Require(
            Configuration is not null,
            nameof(Configuration),
            "must be present");
        WorkerProtocolValidation.Require(
            Tokenizer is not null,
            nameof(Tokenizer),
            "must be present");
        WorkerProtocolValidation.Require(
            ChatTemplate is not null,
            nameof(ChatTemplate),
            "must be present");
        WorkerProtocolValidation.Require(
            Observations is not null,
            nameof(Observations),
            "must be present");

        foreach (WorkerObservation observation in Observations)
        {
            WorkerProtocolValidation.Require(
                observation is not null,
                nameof(Observations),
                "must not contain null entries");
            observation.Validate();
        }
    }
}
