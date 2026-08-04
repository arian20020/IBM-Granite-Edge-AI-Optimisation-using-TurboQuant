using System.Security.Cryptography;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Calculates SHA-256 for a read-only model stream.
/// </summary>
public sealed class Sha256ModelFileHasher : IModelFileHasher
{
    public async Task<byte[]> ComputeHashAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using SHA256 sha256 = SHA256.Create();
        return await sha256.ComputeHashAsync(
            stream,
            cancellationToken);
    }
}
