namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Reports truthful progress through the five lightweight inspection stages.
/// </summary>
public sealed record WorkerProgressMessage
{
    private const int ExpectedStageCount = 5;

    public int ProtocolVersion { get; init; }

    public WorkerMessageKind MessageType { get; init; }

    public Guid RequestId { get; init; }

    public WorkerStage Stage { get; init; }

    public WorkerStageStatus StageStatus { get; init; }

    public int CompletedStageCount { get; init; }

    public int TotalStageCount { get; init; }

    public double? StageFraction { get; init; }

    /// <summary>
    /// Verifies one self-consistent progress message.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            MessageType == WorkerMessageKind.Progress,
            nameof(MessageType),
            "must equal Progress");
        WorkerProtocolValidation.RequireRequestId(RequestId);
        WorkerProtocolValidation.RequireDefinedEnum(Stage, nameof(Stage));
        WorkerProtocolValidation.RequireDefinedEnum(
            StageStatus,
            nameof(StageStatus));
        WorkerProtocolValidation.Require(
            TotalStageCount == ExpectedStageCount,
            nameof(TotalStageCount),
            $"must equal {ExpectedStageCount}");
        WorkerProtocolValidation.Require(
            CompletedStageCount >= 0 &&
            CompletedStageCount <= TotalStageCount,
            nameof(CompletedStageCount),
            "must be between zero and TotalStageCount");

        if (StageFraction is double fraction)
        {
            WorkerProtocolValidation.Require(
                double.IsFinite(fraction) && fraction >= 0 && fraction <= 1,
                nameof(StageFraction),
                "must be a finite value between zero and one");
        }
    }
}
