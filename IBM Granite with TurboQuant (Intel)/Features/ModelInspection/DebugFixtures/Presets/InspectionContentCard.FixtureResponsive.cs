#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

public sealed partial class InspectionContentCard
{
    private double? _fixtureResponsiveWidthOverride;

    internal string? FixtureResponsiveStateName =>
        CurrentFixtureResponsiveStateName();

    internal void ApplyFixtureResponsiveState(
        ModelInspectionFixtureWidthProfile width)
    {
        _fixtureResponsiveWidthOverride = FixtureWidth(width);
        string stateName = width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => "WideContentState",
            ModelInspectionFixtureWidthProfile.Medium600 => "MediumContentState",
            ModelInspectionFixtureWidthProfile.Narrow360 => "NarrowContentState",
            _ => throw new ArgumentOutOfRangeException(
                nameof(width), width, "Unknown fixture width profile.")
        };

        if (!string.Equals(
                CurrentFixtureResponsiveStateName(),
                stateName,
                StringComparison.Ordinal) ||
            !string.Equals(
                _responsiveStateName,
                stateName,
                StringComparison.Ordinal))
        {
            ApplyVisualState(stateName);
            _responsiveStateName = stateName;
        }
    }

    partial void OverrideResponsiveWidthForFixture(ref double width)
    {
        if (_fixtureResponsiveWidthOverride is double fixtureWidth)
        {
            width = fixtureWidth;
        }
    }

    private static double FixtureWidth(
        ModelInspectionFixtureWidthProfile width) => width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => 1440d,
            ModelInspectionFixtureWidthProfile.Medium600 => 600d,
            ModelInspectionFixtureWidthProfile.Narrow360 => 360d,
            _ => throw new ArgumentOutOfRangeException(
                nameof(width), width, "Unknown fixture width profile.")
        };

    private string? CurrentFixtureResponsiveStateName()
    {
        foreach (VisualStateGroup group in
                 VisualStateManager.GetVisualStateGroups(LayoutRoot))
        {
            if (string.Equals(
                    group.Name,
                    "ResponsiveContentStates",
                    StringComparison.Ordinal))
            {
                return group.CurrentState?.Name;
            }
        }

        throw new InvalidOperationException(
            "The content-card responsive visual-state group was not found.");
    }
}
#endif
