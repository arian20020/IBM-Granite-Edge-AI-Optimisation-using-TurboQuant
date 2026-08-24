using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Route-specific configuration. Concrete types are closed and sealed so a
/// candidate can never degrade into a bag of optional cross-route properties.
/// </summary>
public abstract record RouteConfiguration
{
    public abstract RuntimeRouteId RouteId { get; }

    /// <summary>
    /// A stable, culture-invariant description of every memory-relevant
    /// setting. It feeds the candidate fingerprint, so it must change whenever
    /// any such setting changes and must never vary by machine or locale.
    /// </summary>
    public abstract string CanonicalDescriptor { get; }
}
