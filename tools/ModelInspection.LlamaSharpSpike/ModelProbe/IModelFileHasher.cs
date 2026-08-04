namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Calculates a model-file hash from an already opened read-only stream.
/// </summary>
public interface IModelFileHasher
{
    Task<byte[]> ComputeHashAsync(
        Stream stream,
        CancellationToken cancellationToken);
}
