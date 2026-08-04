using System.Security.Cryptography;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Calculates an independent SHA-256 for integration-test input preservation.
/// </summary>
public static class ModelFileHash
{
    public static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(
            stream,
            cancellationToken);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }
}
