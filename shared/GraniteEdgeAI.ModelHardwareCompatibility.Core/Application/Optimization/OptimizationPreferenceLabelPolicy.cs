namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// The exact words a preference is shown as.
///
/// These strings already exist in Model Download. They live here, in the shared
/// contract, rather than being retyped in the optimisation UI, because two
/// copies of a vocabulary drift: one screen would say "Max efficiency" and
/// another "Maximum efficiency" for the same slider position, and nothing would
/// fail to make that visible.
///
/// This is the one place in the engine that holds user-facing words, and it
/// holds them deliberately. They are not descriptions the planner composes -
/// they are a fixed vocabulary the product already committed to, and the
/// planner's job is to match it rather than to invent alongside it.
/// </summary>
public static class OptimizationPreferenceLabelPolicy
{
    /// <summary>
    /// What Automatic is called. Distinct from every band, because it is a
    /// separate choice rather than a position on the slider.
    /// </summary>
    public const string AutomaticLabel = "Automatic";

    public static string GetLabel(OptimizationPreferenceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return selection.Band is { } band ? GetLabel(band) : AutomaticLabel;
    }

    /// <summary>
    /// Fails closed on an undefined band. Returning a blank, or falling back to
    /// a neighbour's wording, would put a name on a preference that has none.
    /// </summary>
    public static string GetLabel(OptimizationPreferenceBand band) => band switch
    {
        OptimizationPreferenceBand.MaximumEfficiency => "Maximum efficiency",
        OptimizationPreferenceBand.Efficient => "Efficient",
        OptimizationPreferenceBand.Balanced => "Balanced",
        OptimizationPreferenceBand.HighCapability => "High capability",
        OptimizationPreferenceBand.MaximumCapability => "Maximum capability",
        _ => throw new ArgumentOutOfRangeException(
            nameof(band),
            band,
            "An unnamed preference band must not reach a user as a blank or as "
            + "some neighbouring band's wording.")
    };
}
