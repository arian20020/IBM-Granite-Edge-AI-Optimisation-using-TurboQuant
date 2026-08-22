using System;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Captures the lightweight file identity observed when Model Import validated
/// the selected model.
/// </summary>
internal sealed record ExpectedModelFileIdentity
{
    /// <summary>
    /// Creates an immutable identity without opening or hashing the model.
    /// </summary>
    internal ExpectedModelFileIdentity(
        long lengthBytes,
        DateTimeOffset lastWriteTimeUtc)
    {
        // A validated local model must be a non-empty file.
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
    }

    internal long LengthBytes { get; }

    internal DateTimeOffset LastWriteTimeUtc { get; }
}
