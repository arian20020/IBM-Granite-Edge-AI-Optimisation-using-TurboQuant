using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

public sealed class HardwareInspectionActionRequestedEventArgs : EventArgs
{
    internal HardwareInspectionActionRequestedEventArgs(HardwareInspectionActionKind kind)
    {
        Kind = kind;
    }

    public HardwareInspectionActionKind Kind { get; }
}

public sealed partial class HardwareInspectionActionCard : UserControl
{
    public HardwareInspectionActionCard()
    {
        InitializeComponent();
        SizeChanged += (_, args) => ApplyAvailableWidth(args.NewSize.Width);
    }

    public event EventHandler<HardwareInspectionActionRequestedEventArgs>? ActionRequested;

    internal IReadOnlyList<HardwareInspectionAction> ActionItems { get; private set; } =
        Array.Empty<HardwareInspectionAction>();

    internal void Apply(HardwareInspectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        HardwareInspectionAction[] visible = state.Actions
            .Where(action => action.IsVisible)
            .ToArray();
        ActionItems = Array.AsReadOnly(visible);
        ActionsPanel.Children.Clear();

        foreach (HardwareInspectionAction action in visible)
        {
            Button button = new()
            {
                Content = action.Label,
                IsEnabled = action.IsEnabled,
                MinHeight = 44,
                MinWidth = 44,
                Padding = new Thickness(18, 10, 18, 10),
                Tag = action.Kind,
                UseSystemFocusVisuals = true,
            };
            AutomationProperties.SetName(button, action.Label);
            if (!string.IsNullOrWhiteSpace(action.AccessibleHelp))
            {
                AutomationProperties.SetHelpText(button, action.AccessibleHelp);
            }

            button.Click += ActionButton_Click;
            ActionsPanel.Children.Add(button);
        }

        if (state.Kind is not HardwareInspectionPresentationKind.Active
            and not HardwareInspectionPresentationKind.Stopping
            && ActionsPanel.Children.LastOrDefault() is Button primary)
        {
            primary.Style = (Style)Resources["HardwareInspectionPrimaryActionStyle"];
        }

        ApplyAvailableWidth(ActualWidth);
    }

    internal void ApplyAvailableWidth(double width)
    {
        bool compact = width < 600;
        ActionsPanel.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
        ActionsPanel.HorizontalAlignment = compact
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        foreach (Button button in ActionsPanel.Children.OfType<Button>())
        {
            button.HorizontalAlignment = compact
                ? HorizontalAlignment.Stretch
                : HorizontalAlignment.Center;
        }
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: HardwareInspectionActionKind kind })
        {
            ActionRequested?.Invoke(this, new HardwareInspectionActionRequestedEventArgs(kind));
        }
    }
}
