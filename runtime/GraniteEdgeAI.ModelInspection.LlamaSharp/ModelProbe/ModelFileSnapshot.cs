namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Records one read-only identity snapshot of the selected model file without
/// exposing its full local path.
/// </summary>
public sealed record ModelFileSnapshot
{
    public string FileName { get; init; } = string.Empty;

    public string CanonicalPathSha256 { get; init; } = string.Empty;

    public long LengthBytes { get; init; }

    public DateTimeOffset LastWriteTimeUtc { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

/// <summary>
/// Compares the selected model before and after native probing.
/// </summary>
public sealed record ModelFileIntegrityComparison
{
    public bool PathUnchanged { get; init; }

    public bool LengthUnchanged { get; init; }

    public bool LastWriteTimeUnchanged { get; init; }

    public bool Sha256Unchanged { get; init; }

    /// <summary>
    /// Gets whether every observed identity field remained unchanged.
    /// </summary>
    public bool IsPreserved =>
        PathUnchanged &&
        LengthUnchanged &&
        LastWriteTimeUnchanged &&
        Sha256Unchanged;

    /// <summary>
    /// Compares two snapshots captured around the probe.
    /// </summary>
    public static ModelFileIntegrityComparison Compare(
        ModelFileSnapshot before,
        ModelFileSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return new ModelFileIntegrityComparison
        {
            PathUnchanged = string.Equals(
                before.CanonicalPathSha256,
                after.CanonicalPathSha256,
                StringComparison.Ordinal),
            LengthUnchanged =
                before.LengthBytes == after.LengthBytes,
            LastWriteTimeUnchanged =
                before.LastWriteTimeUtc == after.LastWriteTimeUtc,
            Sha256Unchanged = string.Equals(
                before.Sha256,
                after.Sha256,
                StringComparison.Ordinal)
        };
    }
}
