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
