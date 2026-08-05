namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Holds path-minimised file and integrity evidence from one completed
/// inspection operation.
/// </summary>
internal sealed record ModelInspectionFileEvidence
{
    /// <summary>
    /// Creates immutable file evidence without retaining the full model path.
    /// </summary>
    internal ModelInspectionFileEvidence(
        string fileName,
        string canonicalPathSha256,
        long lengthBytes,
        DateTimeOffset lastWriteTimeUtc,
        string modelSha256,
        bool integrityPreserved)
    {
        FileName = ModelInspectionContractValidation.RequireFinalFileName(
            fileName,
            nameof(fileName));
        CanonicalPathSha256 =
            ModelInspectionContractValidation.RequireHexDigest(
                canonicalPathSha256,
                expectedLength: 64,
                nameof(canonicalPathSha256));

        // Reliable completion evidence must describe one non-empty file.
        if (lengthBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthBytes),
                lengthBytes,
                "Model file length must be positive.");
        }

        LengthBytes = lengthBytes;
        LastWriteTimeUtc = ModelInspectionContractValidation.RequireUtc(
            lastWriteTimeUtc,
            nameof(lastWriteTimeUtc));
        ModelSha256 = ModelInspectionContractValidation.RequireHexDigest(
            modelSha256,
            expectedLength: 64,
            nameof(modelSha256));

        // A modified model must never enter the completed evidence graph.
        if (!integrityPreserved)
        {
            throw new ArgumentException(
                "Completed inspection evidence must preserve model integrity.",
                nameof(integrityPreserved));
        }

        IntegrityPreserved = true;
    }

    internal string FileName { get; }

    internal string CanonicalPathSha256 { get; }

    internal long LengthBytes { get; }

    internal DateTimeOffset LastWriteTimeUtc { get; }

    internal string ModelSha256 { get; }

    internal bool IntegrityPreserved { get; }
}
