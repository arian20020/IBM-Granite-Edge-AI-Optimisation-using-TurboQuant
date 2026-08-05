namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Requests one lightweight inspection from one short-lived worker process.
/// </summary>
public sealed record WorkerStartInspectionCommand
{
    public int ProtocolVersion { get; init; }

    public WorkerCommandKind CommandType { get; init; }

    public Guid RequestId { get; init; }

    public int ParentProcessId { get; init; }

    public DateTimeOffset ParentProcessStartTimeUtc { get; init; }

    public string ModelPath { get; init; } = string.Empty;

    public WorkerExpectedFileIdentity ExpectedFileIdentity { get; init; } = new();

    public WorkerQuickScanSnapshot QuickScan { get; init; } = new();

    /// <summary>
    /// Verifies the complete start command before any model file is opened.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            CommandType == WorkerCommandKind.StartInspection,
            nameof(CommandType),
            "must equal StartInspection");
        WorkerProtocolValidation.RequireRequestId(RequestId);
        WorkerProtocolValidation.Require(
            ParentProcessId > 0,
            nameof(ParentProcessId),
            "must be positive");
        WorkerProtocolValidation.RequireUtcTimestamp(
            ParentProcessStartTimeUtc,
            nameof(ParentProcessStartTimeUtc));
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(ModelPath) &&
            Path.IsPathFullyQualified(ModelPath),
            nameof(ModelPath),
            "must be a non-empty fully qualified path");
        WorkerProtocolValidation.Require(
            ExpectedFileIdentity is not null,
            nameof(ExpectedFileIdentity),
            "must be present");
        WorkerProtocolValidation.Require(
            QuickScan is not null,
            nameof(QuickScan),
            "must be present");

        ExpectedFileIdentity.Validate();
        QuickScan.Validate();

        WorkerProtocolValidation.Require(
            ExpectedFileIdentity.LengthBytes == QuickScan.FileSizeBytes,
            nameof(ExpectedFileIdentity),
            "length must match QuickScan.FileSizeBytes");
    }
}
