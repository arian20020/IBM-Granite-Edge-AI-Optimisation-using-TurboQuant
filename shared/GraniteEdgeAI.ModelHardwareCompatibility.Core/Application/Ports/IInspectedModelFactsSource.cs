using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// Model facts, or a named reason there are none.
///
/// Built only through the two factories, so "established with no facts" and
/// "unavailable for reason None" are unrepresentable rather than merely
/// discouraged. The first would be a null-reference waiting for the coordinator;
/// the second is an unknown reported as a zero, which this design forbids.
/// </summary>
internal sealed record ModelFactsResolution
{
    private ModelFactsResolution(
        bool isEstablished, InspectedModelFacts? facts, PortUnavailableReason reason)
    {
        IsEstablished = isEstablished;
        Facts = facts;
        Reason = reason;
    }

    internal bool IsEstablished { get; }

    internal InspectedModelFacts? Facts { get; }

    internal PortUnavailableReason Reason { get; }

    internal static ModelFactsResolution Established(InspectedModelFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return new ModelFactsResolution(true, facts, PortUnavailableReason.None);
    }

    internal static ModelFactsResolution Unavailable(PortUnavailableReason reason)
    {
        if (reason == PortUnavailableReason.None)
        {
            throw new ArgumentException(
                "An unavailable resolution must name why.", nameof(reason));
        }

        return new ModelFactsResolution(false, null, reason);
    }
}

/// <summary>
/// Resolves an inspection identity into C1's own model facts. It never returns an
/// owner type, so this compiles before the Model Inspection handoff exists.
/// </summary>
internal interface IInspectedModelFactsSource
{
    ModelFactsResolution Resolve(string modelInspectionRunId);
}
