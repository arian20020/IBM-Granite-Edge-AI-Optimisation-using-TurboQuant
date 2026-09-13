using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;

/// <summary>
/// One band of the memory bar.
///
/// A reserve is drawn differently from a requirement: it is memory held back for
/// Windows and for whatever else the user has open, not memory this model wants.
/// Colouring it like a requirement would tell the user the model is bigger than
/// it is.
/// </summary>
internal sealed record CompatibilityBudgetSegment(
    string Label,
    ulong Bytes,
    bool IsReserve);

/// <summary>
/// The memory picture behind an outcome.
///
/// Two treatments come off the same data, as the design intends: brackets on the
/// main surface, which answer "does it fit" at a glance, and a full legend inside
/// the calculation disclosure for anyone who wants the breakdown.
/// </summary>
internal sealed record CompatibilityBudget
{
    private CompatibilityBudget(
        IReadOnlyList<CompatibilityBudgetSegment> segments,
        ulong safeLimitBytes,
        ulong requiredBytes,
        ulong scaleBytes,
        bool fits,
        string limitingComponent)
    {
        Segments = segments;
        SafeLimitBytes = safeLimitBytes;
        RequiredBytes = requiredBytes;
        ScaleBytes = scaleBytes;
        Fits = fits;
        LimitingComponent = limitingComponent;
    }

    internal IReadOnlyList<CompatibilityBudgetSegment> Segments { get; }

    /// <summary>Where the dashed rule sits: the most that can safely be used.</summary>
    internal ulong SafeLimitBytes { get; }

    internal ulong RequiredBytes { get; }

    /// <summary>
    /// the full width of the bar.
    ///
    /// when the model does not fit, this is the requirement rather than the safe
    /// limit, so the overflow is drawn instead of clipped. a bar that stopped at
    /// the limit would hide exactly how far over it is, which is the one number
    /// the user needs to judge whether closing an app would be enough
    /// </summary>
    internal ulong ScaleBytes { get; }

    internal bool Fits { get; }

    /// <summary>the largest band, named so the footer can say what is driving this</summary>
    internal string LimitingComponent { get; }

    internal static CompatibilityBudget Empty { get; } = new(
        [], 0, 0, 1, true, string.Empty);

    internal static CompatibilityBudget Create(
        IReadOnlyList<CompatibilityBudgetSegment> segments,
        ulong safeLimitBytes)
    {
        ArgumentNullException.ThrowIfNull(segments);

        ulong required = 0;

        foreach (CompatibilityBudgetSegment segment in segments)
        {
            if (!segment.IsReserve)
            {
                required += segment.Bytes;
            }
        }

        bool fits = required <= safeLimitBytes;

        // Rescale to whichever is larger so nothing is drawn off the end.
        ulong scale = Math.Max(Math.Max(required, safeLimitBytes), 1UL);

        string limiting = segments
            .Where(segment => !segment.IsReserve)
            .OrderByDescending(segment => segment.Bytes)
            .Select(segment => segment.Label)
            .FirstOrDefault() ?? string.Empty;

        return new CompatibilityBudget(
            [.. segments], safeLimitBytes, required, scale, fits, limiting);
    }

    /// <summary>
    /// Bytes as a person would say them. Deliberately coarse: a figure like
    /// "5.7 GB" is honest about an estimate in a way that "5,732,847,104 bytes"
    /// is not, because the precision would imply a measurement.
    /// </summary>
    internal static string Describe(ulong bytes)
    {
        const double Gibibyte = 1024d * 1024 * 1024;
        const double Mebibyte = 1024d * 1024;

        return bytes >= Gibibyte
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes / Gibibyte:0.#} GB")
            : string.Create(CultureInfo.CurrentCulture, $"{bytes / Mebibyte:0} MB");
    }
}
