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
        WorkerRuntimeIdentity runtime =
            WorkerProtocolValidation.RequireNotNull(Runtime, nameof(Runtime));
        WorkerModelFileEvidence modelFile =
            WorkerProtocolValidation.RequireNotNull(ModelFile, nameof(ModelFile));
        _ = WorkerProtocolValidation.RequireNotNull(
            Configuration,
            nameof(Configuration));
        _ = WorkerProtocolValidation.RequireNotNull(
            Tokenizer,
            nameof(Tokenizer));
        _ = WorkerProtocolValidation.RequireNotNull(
            ChatTemplate,
            nameof(ChatTemplate));
        IReadOnlyList<WorkerObservation> observations =
            WorkerProtocolValidation.RequireNotNull(
                Observations,
                nameof(Observations));

        runtime.Validate();
        modelFile.Validate();

        foreach (WorkerObservation? candidate in observations)
        {
            WorkerObservation observation =
                WorkerProtocolValidation.RequireNotNull(
                    candidate,
                    nameof(Observations));
            observation.Validate();
        }
    }
}
