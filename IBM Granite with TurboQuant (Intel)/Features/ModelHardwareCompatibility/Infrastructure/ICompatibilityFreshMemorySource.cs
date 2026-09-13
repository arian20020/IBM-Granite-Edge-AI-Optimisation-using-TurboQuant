using System;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal enum CompatibilityFreshResourcesUnavailableReason
{
    StorageUnavailable,
    StorageInaccessible,
    ResourceEvidenceStale,
    ResourceEvidenceInconsistent,
}

internal sealed class CompatibilityFreshResourcesUnavailableException(
    CompatibilityFreshResourcesUnavailableReason reason)
    : Exception("Fresh compatibility resources are unavailable.")
{
    internal CompatibilityFreshResourcesUnavailableReason Reason { get; } = reason;
}

internal interface ICompatibilityFreshResourcesSource
{
    ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
        CancellationToken cancellationToken);
}
