using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

internal enum HardwareInspectionOutcomeTone
{
    Neutral,
    Success,
    Warning,
    Error,
}

public sealed partial class HardwareInspectionOutcomeCard : UserControl
{
    public HardwareInspectionOutcomeCard()
    {
        InitializeComponent();
    }

    internal HardwareInspectionOutcomeTone CurrentTone { get; private set; }
    internal string AccessibleAnnouncement { get; private set; } = string.Empty;

    internal void Apply(HardwareInspectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Kind == HardwareInspectionPresentationKind.Active)
        {
            throw new ArgumentException("Outcome card does not render Active state.", nameof(state));
        }

        CurrentTone = ToneFor(state.Kind);
        KickerTextBlock.Text = state.Kicker;
        TitleTextBlock.Text = state.Title;
        BodyTextBlock.Text = state.Body;
        ReviewCountTextBlock.Text = state.UnresolvedReviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ReviewCountPanel.Visibility = state.UnresolvedReviewCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        AccessibleAnnouncement = state.Announcement;
        AutomationProperties.SetName(OutcomeBorder, state.Announcement);
        bool isStopping = state.Kind == HardwareInspectionPresentationKind.Stopping;
        OutcomeProgressRing.IsActive = isStopping;
        OutcomeProgressRing.Visibility = isStopping ? Visibility.Visible : Visibility.Collapsed;
        GlyphFontIcon.Visibility = isStopping ? Visibility.Collapsed : Visibility.Visible;
        GlyphFontIcon.Glyph = state.Kind switch
        {
            HardwareInspectionPresentationKind.Completed => "\uE73E",
            HardwareInspectionPresentationKind.CompletedWithWarnings => "\uE7BA",
            HardwareInspectionPresentationKind.FailedCriticalEvidence
                or HardwareInspectionPresentationKind.FailedTransientOperation
                or HardwareInspectionPresentationKind.FailedApplicationRepairRequired
                or HardwareInspectionPresentationKind.InvalidHandoff => "\uE711",
            HardwareInspectionPresentationKind.Cancelled => "\uE738",
            HardwareInspectionPresentationKind.Stopping => string.Empty,
            _ => throw new ArgumentOutOfRangeException(),
        };
        VisualStateManager.GoToState(this, CurrentTone.ToString(), false);
    }

    private static HardwareInspectionOutcomeTone ToneFor(HardwareInspectionPresentationKind kind) => kind switch
    {
        HardwareInspectionPresentationKind.Completed => HardwareInspectionOutcomeTone.Success,
        HardwareInspectionPresentationKind.CompletedWithWarnings => HardwareInspectionOutcomeTone.Warning,
        HardwareInspectionPresentationKind.FailedCriticalEvidence
            or HardwareInspectionPresentationKind.FailedTransientOperation
            or HardwareInspectionPresentationKind.FailedApplicationRepairRequired => HardwareInspectionOutcomeTone.Error,
        HardwareInspectionPresentationKind.InvalidHandoff
            or HardwareInspectionPresentationKind.Stopping
            or HardwareInspectionPresentationKind.Cancelled => HardwareInspectionOutcomeTone.Neutral,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
