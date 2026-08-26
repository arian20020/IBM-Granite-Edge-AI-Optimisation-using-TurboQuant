using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal sealed record StagedSourceSnapshot(
    string SourceSha256,
    ulong SourceLengthBytes,
    string SealedSnapshotIdentity)
{
    internal StagedSourceSnapshot Validate()
    {
        if (SourceLengthBytes < 1
            || SourceSha256.Length != 64
            || !SourceSha256.All(character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f')
            || string.IsNullOrWhiteSpace(SealedSnapshotIdentity))
        {
            throw new ArgumentException("The staged source snapshot is not sealed.");
        }
        return this;
    }
}

internal sealed record OptimizationAttemptContext(
    long Generation,
    StagedSourceSnapshot Source,
    string OperationStagingRootIdentity)
{
    internal OptimizationAttemptContext Validate()
    {
        if (Generation < 1 || string.IsNullOrWhiteSpace(OperationStagingRootIdentity))
        {
            throw new ArgumentException("The optimisation attempt identity is invalid.");
        }
        Source.Validate();
        return this;
    }
}

internal interface IOptimizationAttemptContextFactory
{
    Task<OptimizationAttemptContext> CreateAsync(
        long generation,
        CancellationToken cancellationToken);
}
