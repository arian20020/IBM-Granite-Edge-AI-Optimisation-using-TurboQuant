namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// Identifies one compatibility run.
///
/// Stale-run rejection depends on being able to tell one run from another, so an
/// empty value is refused rather than quietly matching everything.
/// </summary>
internal readonly record struct CompatibilityRunId
{
    private CompatibilityRunId(Guid value) => Value = value;

    internal Guid Value { get; }

    /// <summary>
    /// True for a value that never went through a factory.
    ///
    /// A record struct cannot forbid <c>default</c>, and every defaulted instance
    /// compares equal to every other under value equality — so a defaulted id
    /// would match any other defaulted id, which is exactly the confusion
    /// stale-run rejection exists to prevent. Consumers that decide anything on
    /// identity check this rather than trusting the factory alone.
    /// </summary>
    internal bool IsEmpty => Value == Guid.Empty;

    internal static CompatibilityRunId New() => new(Guid.NewGuid());

    internal static CompatibilityRunId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An empty run id cannot distinguish one run from another.", nameof(value));
        }

        return new CompatibilityRunId(value);
    }

    public override string ToString() => Value.ToString("D");
}
