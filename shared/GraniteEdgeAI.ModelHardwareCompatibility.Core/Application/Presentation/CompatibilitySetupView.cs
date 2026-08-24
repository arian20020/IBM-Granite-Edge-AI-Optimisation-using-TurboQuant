using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// One line of the memory breakdown.
///
/// The kind is named rather than described, because a component's label is
/// wording and wording belongs to presentation. What crosses the boundary is
/// which cost this is and how large it came out.
/// </summary>
public sealed record CompatibilityComponentView(ResourceComponentKind Kind, ulong Bytes);

/// <summary>
/// The setup a screen is describing, flattened for a caller outside this
/// assembly.
///
/// A screen that says a model fits has to show the figures behind that claim,
/// or the user is being asked to trust a verdict with nothing under it. This is
/// those figures: what would run, what it needs, and what it was allowed.
///
/// Absent when no candidate was evaluated. A screen with no setup must say it
/// reached no answer rather than drawing an empty breakdown, which would read
/// as a configuration that costs nothing.
/// </summary>
public sealed record CompatibilitySetupView
{
    internal CompatibilitySetupView(
        RuntimeRouteId route,
        CompatibilityBackend backend,
        DeviceRouteId device,
        WeightQuantisation weights,
        int contextTokens,
        CompatibilityFitState fit,
        ulong requiredBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        ulong uncertaintyAllowanceBytes,
        bool isExperimental,
        bool requiresConversion,
        IReadOnlyList<CompatibilityComponentView> components)
    {
        Route = route;
        Backend = backend;
        Device = device;
        Weights = weights;
        ContextTokens = contextTokens;
        Fit = fit;
        RequiredBytes = requiredBytes;
        SafeBudgetBytes = safeBudgetBytes;
        HeadroomBytes = headroomBytes;
        UncertaintyAllowanceBytes = uncertaintyAllowanceBytes;
        IsExperimental = isExperimental;
        RequiresConversion = requiresConversion;
        Components = components;
    }

    public RuntimeRouteId Route { get; }

    public CompatibilityBackend Backend { get; }

    public DeviceRouteId Device { get; }

    /// <summary>
    /// The format the weights would be in when this runs, which is not always
    /// the format they are in now: a setup requiring conversion is described by
    /// what it would produce, not by what it started from.
    /// </summary>
    public WeightQuantisation Weights { get; }

    public int ContextTokens { get; }

    public CompatibilityFitState Fit { get; }

    /// <summary>
    /// Peak memory this setup would need. Excludes the safety reserve, which is
    /// not a cost of running the model but a margin held back from it.
    /// </summary>
    public ulong RequiredBytes { get; }

    /// <summary>The most this setup was allowed to use, after the reserve was withheld.</summary>
    public ulong SafeBudgetBytes { get; }

    /// <summary>Budget left over. Zero when the setup does not fit.</summary>
    public ulong HeadroomBytes { get; }

    /// <summary>
    /// Extra memory demanded on top of the components, because the figures are
    /// worked out rather than measured.
    ///
    /// Part of the requirement, not a deduction from the budget: it is memory
    /// this setup is expected to want, so a breakdown that omitted it would not
    /// add up to the total shown beside it.
    /// </summary>
    public ulong UncertaintyAllowanceBytes { get; }

    public bool IsExperimental { get; }

    /// <summary>
    /// Whether running this means writing a new model file first. That is a
    /// different proposition from changing a setting, and a screen that blurred
    /// the two would have a user agree to disk writes they never saw offered.
    /// </summary>
    public bool RequiresConversion { get; }

    /// <summary>
    /// What the requirement is made of, largest first. Present so a user facing
    /// "it doesn't fit" can see which part is responsible.
    /// </summary>
    public IReadOnlyList<CompatibilityComponentView> Components { get; }

    /// <summary>
    /// Builds a setup view directly, for Debug fixtures and tests that render a
    /// screen without an engine run behind it. It produces presentation input,
    /// never a run result, so nothing downstream can mistake one for the other.
    /// </summary>
    public static CompatibilitySetupView ForPresentation(
        RuntimeRouteId route,
        CompatibilityBackend backend,
        DeviceRouteId device,
        WeightQuantisation weights,
        int contextTokens,
        CompatibilityFitState fit,
        ulong requiredBytes,
        ulong safeBudgetBytes,
        ulong headroomBytes,
        ulong uncertaintyAllowanceBytes,
        bool isExperimental,
        bool requiresConversion,
        IReadOnlyList<CompatibilityComponentView> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        return new CompatibilitySetupView(
            route,
            backend,
            device,
            weights,
            contextTokens,
            fit,
            requiredBytes,
            safeBudgetBytes,
            headroomBytes,
            uncertaintyAllowanceBytes,
            isExperimental,
            requiresConversion,
            [.. components]);
    }
}
