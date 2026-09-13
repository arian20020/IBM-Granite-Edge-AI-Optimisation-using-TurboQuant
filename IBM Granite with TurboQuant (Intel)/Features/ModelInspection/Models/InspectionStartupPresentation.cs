using Microsoft.UI.Xaml;

namespace GraniteEdgeAI.Features.ModelInspection.Models;

/// <summary>
/// Describes truthful feedback while secure inspection startup is pending.
/// </summary>
public sealed class InspectionStartupPresentation
{
    /// <summary>
    /// Gets the safe presentation used outside secure startup.
    /// </summary>
    public static InspectionStartupPresentation Hidden { get; } = new();

    /// <summary>
    /// Gets whether the startup status is displayed.
    /// </summary>
    public Visibility Visibility { get; init; } = Visibility.Collapsed;

    /// <summary>
    /// Gets the visible startup summary.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the accessible startup announcement.
    /// </summary>
    public string AutomationName { get; init; } = string.Empty;
}
