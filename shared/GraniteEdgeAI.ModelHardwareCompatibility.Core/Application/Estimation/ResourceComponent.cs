using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// One estimated memory requirement, charged to exactly one pool and present
/// during one or more lifecycle phases.
/// </summary>
internal sealed record ResourceComponent
{
    private ResourceComponent(
        ResourceComponentKind kind,
        ResourceTarget target,
        ByteCount bytes,
        IReadOnlySet<LifecyclePhase> phases)
    {
        Kind = kind;
        Target = target;
        Bytes = bytes;
        Phases = phases;
    }

    internal ResourceComponentKind Kind { get; }

    internal ResourceTarget Target { get; }

    internal ByteCount Bytes { get; }

    internal IReadOnlySet<LifecyclePhase> Phases { get; }

    internal static ResourceComponent Create(
        ResourceComponentKind kind,
        ResourceTarget target,
        ByteCount bytes,
        IReadOnlySet<LifecyclePhase> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);

        if (kind == ResourceComponentKind.Unspecified)
        {
            throw new ArgumentException(
                "A component must declare its kind so ownership stays unambiguous.",
                nameof(kind));
        }

        if (target == ResourceTarget.Unspecified)
        {
            throw new ArgumentException(
                "A component must declare the pool it is charged against.",
                nameof(target));
        }

        if (phases.Count == 0)
        {
            throw new ArgumentException(
                "A component with no phase could never be composed into a peak, "
                + "so it would silently vanish from the estimate.",
                nameof(phases));
        }

        if (phases.Contains(LifecyclePhase.Unspecified))
        {
            throw new ArgumentException(
                "A component must not declare an unspecified phase.",
                nameof(phases));
        }

        // Copy so a later caller mutation cannot change an already composed estimate.
        return new ResourceComponent(
            kind, target, bytes, new HashSet<LifecyclePhase>(phases));
    }
}
