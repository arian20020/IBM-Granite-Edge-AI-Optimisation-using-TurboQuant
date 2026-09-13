namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Summarises the model file before and after native inspection without
/// returning its canonical local path.
/// </summary>
public sealed record WorkerModelFileEvidence
{
    public string FileName { get; init; } = string.Empty;

    public string CanonicalPathSha256 { get; init; } = string.Empty;

    public long LengthBefore { get; init; }

    public long LengthAfter { get; init; }

    public DateTimeOffset LastWriteTimeBeforeUtc { get; init; }

    public DateTimeOffset LastWriteTimeAfterUtc { get; init; }

    public string Sha256Before { get; init; } = string.Empty;

    public string Sha256After { get; init; } = string.Empty;

    public bool IntegrityPreserved { get; init; }

    /// <summary>
    /// Verifies that the integrity evidence is complete and path-minimised.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(FileName) &&
            string.Equals(
                Path.GetFileName(FileName),
                FileName,
                StringComparison.Ordinal),
            nameof(FileName),
            "must contain only the final file name");
        WorkerProtocolValidation.RequireHexDigest(
            CanonicalPathSha256,
            nameof(CanonicalPathSha256));
        WorkerProtocolValidation.Require(
            LengthBefore > 0 && LengthAfter > 0,
            nameof(LengthBefore),
            "and LengthAfter must be positive");
        WorkerProtocolValidation.RequireUtcTimestamp(
            LastWriteTimeBeforeUtc,
            nameof(LastWriteTimeBeforeUtc));
        WorkerProtocolValidation.RequireUtcTimestamp(
            LastWriteTimeAfterUtc,
            nameof(LastWriteTimeAfterUtc));
        WorkerProtocolValidation.RequireHexDigest(
            Sha256Before,
            nameof(Sha256Before));
        WorkerProtocolValidation.RequireHexDigest(
            Sha256After,
            nameof(Sha256After));

        if (IntegrityPreserved)
        {
            WorkerProtocolValidation.Require(
                LengthBefore == LengthAfter,
                nameof(IntegrityPreserved),
                "requires equal before/after lengths when true");
            WorkerProtocolValidation.Require(
                LastWriteTimeBeforeUtc == LastWriteTimeAfterUtc,
                nameof(IntegrityPreserved),
                "requires equal before/after timestamps when true");
            WorkerProtocolValidation.Require(
                string.Equals(
                    Sha256Before,
                    Sha256After,
                    StringComparison.OrdinalIgnoreCase),
                nameof(IntegrityPreserved),
                "requires equal before/after SHA-256 values when true");
        }
    }
}
