using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;

/// <summary>
/// Turns the setup the engine chose into the figures on the page.
///
/// The verdict alone is not enough. A user told "this fits" with nothing under
/// it is being asked to take a number on trust, and a user told "this does not
/// fit" with nothing under it cannot tell which part to change. So every screen
/// that has a setup shows what would run, what it needs, and where the need
/// comes from.
///
/// Names live here rather than in the engine, because a label is wording. The
/// engine says IntelIntegratedGpu; deciding a person should read "Built-in
/// graphics" is a presentation choice, and one that changes with language.
/// </summary>
internal static class CompatibilitySetupNarrative
{
    /// <summary>
    /// The four tiles across the top: what it needs, what it is allowed, how
    /// much text it can hold, and what it would run on.
    /// </summary>
    internal static IReadOnlyList<CompatibilityFact> Facts(CompatibilitySetupView setup) =>
    [
        new CompatibilityFact(
            "Memory needed",
            CompatibilityBudget.Describe(setup.RequiredBytes),
            "At its busiest moment"),
        new CompatibilityFact(
            "Memory allowed",
            CompatibilityBudget.Describe(setup.SafeBudgetBytes),
            Spare(setup)),
        new CompatibilityFact(
            "Context",
            Tokens(setup.ContextTokens),
            "How much it can read at once"),
        new CompatibilityFact(
            "Runs on",
            Device(setup.Device),
            Backend(setup.Backend))
    ];

    /// <summary>What would actually run, named plainly.</summary>
    internal static IReadOnlyList<CompatibilityRow> RuntimeRows(CompatibilitySetupView setup) =>
    [
        new CompatibilityRow(
            "Engine",
            Route(setup.Route),
            string.Empty,
            CompatibilityOutcomeTone.Neutral,
            false),
        new CompatibilityRow(
            "Processor",
            Device(setup.Device),
            string.Empty,
            CompatibilityOutcomeTone.Neutral,
            false),
        new CompatibilityRow(
            "Driver",
            Backend(setup.Backend),
            string.Empty,
            CompatibilityOutcomeTone.Neutral,
            false),
        new CompatibilityRow(
            "Model file",
            Weights(setup.Weights),
            setup.RequiresConversion ? "Needs converting" : string.Empty,
            setup.RequiresConversion
                ? CompatibilityOutcomeTone.Caution
                : CompatibilityOutcomeTone.Neutral,
            setup.RequiresConversion)
    ];

    /// <summary>
    /// What was checked and how it came out.
    ///
    /// The evidence line is not decoration. Every figure here is worked out
    /// from documented defaults rather than measured, and a page that showed
    /// the numbers without saying so would be presenting arithmetic as
    /// observation.
    /// </summary>
    internal static IReadOnlyList<CompatibilityRow> CheckRows(CompatibilitySetupView setup)
    {
        (string Value, CompatibilityOutcomeTone Tone) memory = setup.Fit switch
        {
            CompatibilityFitState.Safe => ("Fits", CompatibilityOutcomeTone.Positive),
            CompatibilityFitState.Narrow => ("Only just", CompatibilityOutcomeTone.Caution),
            _ => ("Too big", CompatibilityOutcomeTone.Blocking)
        };

        List<CompatibilityRow> rows =
        [
            new CompatibilityRow(
                "Memory",
                CompatibilityBudget.Describe(setup.RequiredBytes)
                    + " of "
                    + CompatibilityBudget.Describe(setup.SafeBudgetBytes),
                memory.Value,
                memory.Tone,
                true),
            new CompatibilityRow(
                "Where it runs",
                Device(setup.Device) + ", using " + Backend(setup.Backend),
                "Supported",
                CompatibilityOutcomeTone.Positive,
                true),
            new CompatibilityRow(
                "How we know",
                "Worked out from what this model needs, not measured",
                "Estimate",
                CompatibilityOutcomeTone.Neutral,
                true)
        ];

        if (setup.IsExperimental)
        {
            rows.Add(new CompatibilityRow(
                "Experimental",
                "This way of running is not switched on by default",
                "Opt-in",
                CompatibilityOutcomeTone.Caution,
                true));
        }

        return rows;
    }

    /// <summary>
    /// The memory bar.
    ///
    /// Segments arrive already restricted to the moment memory peaks, so they
    /// sum to the requirement rather than to a larger total that never existed
    /// at any one time. The safety reserve is not drawn as a segment because it
    /// has already been taken out of the limit the bar is measured against;
    /// drawing it again would subtract it twice.
    /// </summary>
    internal static CompatibilityBudget Budget(CompatibilitySetupView setup)
    {
        List<CompatibilityBudgetSegment> segments =
        [
            .. setup.Components.Select(component => new CompatibilityBudgetSegment(
                Component(component.Kind), component.Bytes, false))
        ];

        // Counted as a cost rather than drawn as a reserve, because that is
        // what it is: memory this setup is expected to want on top of the
        // parts we can name. Leaving it off would make the segments stop short
        // of the total printed beside them
        if (setup.UncertaintyAllowanceBytes > 0)
        {
            segments.Add(new CompatibilityBudgetSegment(
                "Margin for error", setup.UncertaintyAllowanceBytes, false));
        }

        return CompatibilityBudget.Create(segments, setup.SafeBudgetBytes);
    }

    /// <summary>
    /// Groups the detailed peak-phase components into the compact categories a
    /// person can scan. This does not estimate anything: it only preserves and
    /// groups values already produced by the core estimator.
    /// </summary>
    internal static CompatibilityEstimateSummary EstimateSummary(
        CompatibilitySetupView setup)
    {
        ulong weights = BytesFor(setup, ResourceComponentKind.Weights);
        ulong kvCache = BytesFor(setup, ResourceComponentKind.KvCache);
        ulong runtime = setup.Components
            .Where(component => component.Kind is not ResourceComponentKind.Weights
                and not ResourceComponentKind.KvCache)
            .Aggregate(0UL, (sum, component) => checked(sum + component.Bytes));

        return new CompatibilityEstimateSummary(
            weights,
            kvCache,
            runtime,
            setup.UncertaintyAllowanceBytes,
            setup.RequiredBytes,
            setup.SafeBudgetBytes);
    }

    private static ulong BytesFor(
        CompatibilitySetupView setup,
        ResourceComponentKind kind) =>
        setup.Components
            .Where(component => component.Kind == kind)
            .Aggregate(0UL, (sum, component) => checked(sum + component.Bytes));

    private static string Spare(CompatibilitySetupView setup) => setup.Fit switch
    {
        CompatibilityFitState.Safe =>
            CompatibilityBudget.Describe(setup.HeadroomBytes) + " spare",
        CompatibilityFitState.Narrow => "Almost none spare",
        _ => "Not enough"
    };

    private static string Tokens(int tokens) =>
        tokens >= 1024 && tokens % 1024 == 0
            ? string.Create(CultureInfo.CurrentCulture, $"{tokens / 1024}K tokens")
            : string.Create(CultureInfo.CurrentCulture, $"{tokens:N0} tokens");

    private static string Route(RuntimeRouteId route) => route switch
    {
        RuntimeRouteId.LlamaCpp => "llama.cpp",
        RuntimeRouteId.OpenVinoGenAi => "OpenVINO",
        _ => "Not known"
    };

    private static string Device(DeviceRouteId device) => device switch
    {
        DeviceRouteId.Cpu => "Processor",
        DeviceRouteId.IntelIntegratedGpu => "Built-in graphics",
        DeviceRouteId.IntelDiscreteGpu => "Graphics card",
        DeviceRouteId.IntelNpu => "AI accelerator",
        _ => "Not known"
    };

    private static string Backend(CompatibilityBackend backend) => backend switch
    {
        CompatibilityBackend.Cpu => "Processor",
        CompatibilityBackend.IntelSycl => "Intel SYCL",
        CompatibilityBackend.IntelVulkan => "Vulkan",
        CompatibilityBackend.OpenVinoCpu => "OpenVINO on the processor",
        CompatibilityBackend.OpenVinoGpu => "OpenVINO on graphics",
        CompatibilityBackend.OpenVinoNpu => "OpenVINO on the AI accelerator",
        _ => "Not known"
    };

    /// <summary>
    /// Quantisation names are kept as they are. They are the names on the files
    /// a user downloads, so translating them would break the one link between
    /// what this page says and what they can go and find.
    /// </summary>
    private static string Weights(WeightQuantisation weights) =>
        weights == WeightQuantisation.Unknown ? "Not known" : weights.ToString();

    private static string Component(ResourceComponentKind kind) => kind switch
    {
        ResourceComponentKind.Weights => "The model itself",
        ResourceComponentKind.KvCache => "Context memory",
        ResourceComponentKind.ComputeBuffer => "Working space",
        ResourceComponentKind.BackendAllocation => "Driver",
        ResourceComponentKind.StagingBuffer => "Loading buffer",
        ResourceComponentKind.ModelState => "Model state",
        ResourceComponentKind.ApplicationOverhead => "This app",
        ResourceComponentKind.PersistentArtifact => "Converted file",
        _ => "Other"
    };
}
