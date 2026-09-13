namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Whether the user let the planner choose or placed the slider themselves.
///
/// Automatic is a separate choice, not a position on the slider. The two are
/// kept apart because the explanation a user is owed differs: one says what the
/// planner decided and why, the other says what their own position resolved to.
/// </summary>
public enum OptimizationPreferenceKind
{
    Automatic = 1,
    Manual = 2,
    Exact = 3
}

/// <summary>
/// The five named positions on the preference slider.
///
/// Ordered from efficiency to capability, and that order is load-bearing: the
/// safe frontier is ordered the same way, and the monotonic properties compare
/// the two orderings against each other. Renumbering these would silently
/// invert what a slider position means.
/// </summary>
public enum OptimizationPreferenceBand
{
    MaximumEfficiency = 1,
    Efficient = 2,
    Balanced = 3,
    HighCapability = 4,
    MaximumCapability = 5
}

/// <summary>
/// What the user asked for.
///
/// A band is derived from the slider value rather than stored in its place,
/// because the plan binds the exact position the user chose. A selection that
/// remembered only its band could not be reproduced, and two different
/// positions inside one band would become indistinguishable.
///
/// This is not a precision. Nothing here names a quantisation, a device or a
/// cache format: it is a preference, and what it resolves to depends on the
/// model, the computer, the workload and the route.
/// </summary>
public sealed record OptimizationPreferenceSelection
{
    private OptimizationPreferenceSelection()
    {
    }

    public OptimizationPreferenceKind Kind { get; private init; }

    /// <summary>
    /// The exact slider position, 0 through 100. Null for Automatic, which was
    /// never a position.
    /// </summary>
    public int? PreferenceValue { get; private init; }

    /// <summary>The band that position falls in. Null for Automatic.</summary>
    public OptimizationPreferenceBand? Band { get; private init; }

    /// <summary>Opaque digest of a retained candidate; never a path or display label.</summary>
    public string? ExactCandidateIdentity { get; private init; }

    public static OptimizationPreferenceSelection Exact(string candidateIdentity)
    {
        ArgumentNullException.ThrowIfNull(candidateIdentity);
        if (candidateIdentity.Length != 64 || !candidateIdentity.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("An exact selection requires a SHA256 identity.",
                nameof(candidateIdentity));
        }
        return new OptimizationPreferenceSelection
        {
            Kind = OptimizationPreferenceKind.Exact,
            ExactCandidateIdentity = candidateIdentity.ToLowerInvariant()
        };
    }

    public static OptimizationPreferenceSelection Automatic() =>
        new() { Kind = OptimizationPreferenceKind.Automatic };

    /// <summary>
    /// A position the user placed.
    ///
    /// Out-of-range values throw rather than clamp. A number outside 0 to 100
    /// did not come from the slider, and clamping it would record a preference
    /// the user never expressed while looking exactly like one they did.
    /// </summary>
    public static OptimizationPreferenceSelection Manual(int value)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The preference slider runs from 0 to 100; a value outside it did "
                + "not come from the control and must not be clamped into one.");
        }

        return new OptimizationPreferenceSelection
        {
            Kind = OptimizationPreferenceKind.Manual,
            PreferenceValue = value,
            Band = value switch
            {
                < 20 => OptimizationPreferenceBand.MaximumEfficiency,
                < 40 => OptimizationPreferenceBand.Efficient,
                < 60 => OptimizationPreferenceBand.Balanced,
                < 80 => OptimizationPreferenceBand.HighCapability,
                _ => OptimizationPreferenceBand.MaximumCapability
            }
        };
    }
}
