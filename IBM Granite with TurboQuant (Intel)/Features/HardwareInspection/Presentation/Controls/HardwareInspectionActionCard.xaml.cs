using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
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
    private readonly Dictionary<HardwareInspectionActionKind, Button> _buttonsByKind = [];
    private HardwareInspectionPresentationKind? _appliedKind;
    private HardwareInspectionPresentationState? _appliedState;
    private long _announcementGeneration;

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
        if (_appliedKind == state.Kind && ActionItems.SequenceEqual(visible))
        {
            return;
        }

        bool announceStopping = state.Kind == HardwareInspectionPresentationKind.Stopping
            && _appliedKind != HardwareInspectionPresentationKind.Stopping;
        ActionItems = Array.AsReadOnly(visible);
        ActionBandBorder.BorderThickness = state.Kind == HardwareInspectionPresentationKind.Active
            ? new Thickness(0)
            : new Thickness(0, 1, 0, 0);
        ActionBandBorder.Padding = state.Kind == HardwareInspectionPresentationKind.Active
            ? new Thickness(0)
            : new Thickness(14, 12, 14, 12);
        for (int index = 0; index < visible.Length; index++)
        {
            HardwareInspectionAction action = visible[index];
            Button button = GetOrCreateButton(action.Kind);
            int existingIndex = ActionsPanel.Children.IndexOf(button);
            if (existingIndex != index)
            {
                if (existingIndex >= 0)
                {
                    ActionsPanel.Children.RemoveAt(existingIndex);
                }

                if (index < ActionsPanel.Children.Count)
                {
                    ActionsPanel.Children.Insert(index, button);
                }
                else
                {
                    ActionsPanel.Children.Add(button);
                }
            }

            button.Content = action.Label;
            button.IsEnabled = action.IsEnabled;
            button.Style = (Style)Resources["HardwareInspectionSecondaryActionStyle"];
            button.Tag = action.Kind;
            ApplyNativeStatePalette(button, primary: false);
            AutomationProperties.SetName(
                button,
                action.Kind == HardwareInspectionActionKind.Stopping
                    ? "Stopping hardware inspection"
                    : action.Label);
            AutomationProperties.SetHelpText(button, action.AccessibleHelp ?? string.Empty);
            AutomationProperties.SetLiveSetting(
                button,
                action.Kind == HardwareInspectionActionKind.Stopping
                    ? AutomationLiveSetting.Polite
                    : AutomationLiveSetting.Off);
            AutomationProperties.SetItemStatus(
                button,
                action.Kind == HardwareInspectionActionKind.Stopping
                    ? "Stopping hardware inspection"
                    : string.Empty);
        }

        while (ActionsPanel.Children.Count > visible.Length)
        {
            int finalIndex = ActionsPanel.Children.Count - 1;
            ActionsPanel.Children.RemoveAt(finalIndex);
        }

        if (state.Kind is not HardwareInspectionPresentationKind.Active
            and not HardwareInspectionPresentationKind.Stopping
            && visible.LastOrDefault()?.Kind is not HardwareInspectionActionKind.Back
            and not HardwareInspectionActionKind.BackToModelInspection
            && ActionsPanel.Children.LastOrDefault() is Button primary)
        {
            primary.Style = (Style)Resources["HardwareInspectionPrimaryActionStyle"];
            ApplyNativeStatePalette(primary, primary: true);
        }

        ApplyAvailableWidth(ActualWidth);
        _appliedKind = state.Kind;
        _appliedState = state;
        long announcementGeneration = checked(++_announcementGeneration);
        if (announceStopping
            && visible.FirstOrDefault(action =>
                action.Kind == HardwareInspectionActionKind.Stopping) is not null
            && _buttonsByKind.TryGetValue(
                HardwareInspectionActionKind.Stopping,
                out Button? stoppingButton))
        {
            const string announcement = "Stopping hardware inspection";
            AutomationProperties.SetName(stoppingButton, announcement);
            AutomationProperties.SetLiveSetting(
                stoppingButton,
                AutomationLiveSetting.Polite);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (announcementGeneration != _announcementGeneration
                    || !ReferenceEquals(state, _appliedState)
                    || _appliedKind != HardwareInspectionPresentationKind.Stopping
                    || !_buttonsByKind.TryGetValue(
                        HardwareInspectionActionKind.Stopping,
                        out Button? currentStoppingButton)
                    || !ReferenceEquals(stoppingButton, currentStoppingButton))
                {
                    return;
                }

                AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(stoppingButton)
                    ?? FrameworkElementAutomationPeer.CreatePeerForElement(stoppingButton);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            });
        }
    }

    private Button GetOrCreateButton(HardwareInspectionActionKind kind)
    {
        if (_buttonsByKind.TryGetValue(kind, out Button? button))
        {
            return button;
        }

        if (kind == HardwareInspectionActionKind.Stopping
            && _buttonsByKind.TryGetValue(
                HardwareInspectionActionKind.CancelInspection,
                out Button? cancelButton))
        {
            _buttonsByKind.Add(kind, cancelButton);
            return cancelButton;
        }

        button = new Button
        {
            MinWidth = 176,
            MaxWidth = 240,
            MinHeight = 44,
            Tag = kind,
        };
        button.Click += ActionButton_Click;
        _buttonsByKind.Add(kind, button);
        return button;
    }

    internal bool FocusFirstEnabledAction()
    {
        Button? target = ActionsPanel.Children.OfType<Button>()
            .FirstOrDefault(button => button.Visibility == Visibility.Visible && button.IsEnabled);
        return target?.Focus(FocusState.Programmatic) == true;
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
        if (sender is not Button button)
        {
            return;
        }

        HardwareInspectionAction? action = ActionItems.FirstOrDefault(candidate =>
            candidate.IsVisible
            && candidate.IsEnabled
            && _buttonsByKind.TryGetValue(candidate.Kind, out Button? candidateButton)
            && ReferenceEquals(button, candidateButton));
        if (action is not null)
        {
            ActionRequested?.Invoke(
                this,
                new HardwareInspectionActionRequestedEventArgs(action.Kind));
        }
    }

    private void ApplyNativeStatePalette(Button button, bool primary)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.Resources["ButtonBackgroundPointerOver"] = Resources[
            primary
                ? "GraniteJourneyPrimaryPointerOverBrush"
                : "GraniteJourneySecondaryPointerOverBrush"];
        button.Resources["ButtonBorderBrushPointerOver"] = Resources[
            primary
                ? "GraniteJourneyPrimaryPointerOverBrush"
                : "GraniteJourneySecondaryPointerOverBorderBrush"];
        button.Resources["ButtonBackgroundPressed"] = Resources[
            primary
                ? "GraniteJourneyPrimaryPressedBrush"
                : "GraniteJourneySecondaryPressedBrush"];
        button.Resources["ButtonBorderBrushPressed"] = Resources[
            primary
                ? "GraniteJourneyPrimaryPressedBrush"
                : "GraniteJourneySecondaryPressedBorderBrush"];
        button.Resources["ButtonBackgroundDisabled"] =
            Resources["GraniteJourneyDisabledBackgroundBrush"];
        button.Resources["ButtonForegroundDisabled"] =
            Resources["GraniteJourneyDisabledForegroundBrush"];
        button.Resources["ButtonBorderBrushDisabled"] =
            Resources["GraniteJourneyDisabledBorderBrush"];
    }
}
