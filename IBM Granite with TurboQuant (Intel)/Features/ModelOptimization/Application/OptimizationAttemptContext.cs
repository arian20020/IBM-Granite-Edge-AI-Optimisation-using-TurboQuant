using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Storage;

namespace GraniteEdgeAI.Features.ModelOptimization.Journey;

internal sealed record OptimizationAttemptContext(
    long Generation,
    StagedSourceSnapshot Source,
    string OperationStagingRootIdentity)
    : IDisposable
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

    public void Dispose() => Source.Dispose();
}

internal interface IOptimizationAttemptContextFactory
{
    Task<OptimizationAttemptContext> CreateAsync(
        long generation,
        CancellationToken cancellationToken);
}
