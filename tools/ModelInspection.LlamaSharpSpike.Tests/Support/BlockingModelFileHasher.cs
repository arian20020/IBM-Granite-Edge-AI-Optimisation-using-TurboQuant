using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

/// <summary>
/// Holds hashing at a deterministic boundary until cancellation is requested.
/// </summary>
internal sealed class BlockingModelFileHasher : IModelFileHasher
{
    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Started => _started.Task;

    public async Task<byte[]> ComputeHashAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _started.TrySetResult();

        await Task.Delay(
            Timeout.InfiniteTimeSpan,
            cancellationToken);

        throw new InvalidOperationException(
            "The blocking test hasher should only finish through cancellation.");
    }
}
