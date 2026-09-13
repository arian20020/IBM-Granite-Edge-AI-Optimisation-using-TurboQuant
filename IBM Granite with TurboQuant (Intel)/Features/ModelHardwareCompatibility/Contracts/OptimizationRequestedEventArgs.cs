using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

internal sealed class OptimizationRequestedEventArgs(
    OptimizationJourneyEntryContext context) : EventArgs
{
    internal OptimizationJourneyEntryContext Context { get; } = context
        ?? throw new ArgumentNullException(nameof(context));
}
