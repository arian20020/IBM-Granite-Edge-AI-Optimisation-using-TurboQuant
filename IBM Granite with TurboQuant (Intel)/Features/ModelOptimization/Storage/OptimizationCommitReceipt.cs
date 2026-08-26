using System;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed record OptimizationOutputKey(
    OptimizationRoute Route,
    Guid OptimizationPlanId,
    string ConfigurationSha256,
    string OutputIdentity,
    string OutputManifestSha256)
{
    internal OptimizationOutputKey Validate()
    {
        if (!Enum.IsDefined(Route)
            || OptimizationPlanId == Guid.Empty
            || !StagedSourceSnapshot.IsCanonicalSha256(ConfigurationSha256)
            || !StagedSourceSnapshot.IsOpaqueIdentity(OutputIdentity)
            || !StagedSourceSnapshot.IsCanonicalSha256(OutputManifestSha256))
        {
            throw new ArgumentException("The optimization output key is invalid.");
        }
        return this;
    }
}

internal sealed record OptimizationCommitReceipt(
    OptimizationOutputKey Key,
    long AttemptGeneration,
    string SourceSha256,
    bool SourceUnchanged,
    ulong OutputSizeBytes,
    string SealedStagingIdentity,
    string PublicationIdentity)
{
    internal OptimizationCommitReceipt Validate()
    {
        Key.Validate();
        if (AttemptGeneration < 1
            || !StagedSourceSnapshot.IsCanonicalSha256(SourceSha256)
            || !SourceUnchanged
            || OutputSizeBytes == 0
            || !StagedSourceSnapshot.IsOpaqueIdentity(SealedStagingIdentity)
            || !StagedSourceSnapshot.IsOpaqueIdentity(PublicationIdentity))
        {
            throw new ArgumentException("The optimization commit receipt is invalid.");
        }
        return this;
    }
}
