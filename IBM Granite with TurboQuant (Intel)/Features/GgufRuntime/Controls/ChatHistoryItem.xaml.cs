using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatHistoryItem : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ChatHistoryItem), new PropertyMetadata("New chat"));
    public static readonly DependencyProperty DateLabelProperty = DependencyProperty.Register(
        nameof(DateLabel), typeof(string), typeof(ChatHistoryItem), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(ChatHistoryItem),
        new PropertyMetadata(false, OnIsSelectedChanged));

    public ChatHistoryItem()
    {
        InitializeComponent();
        ApplySelection();
    }

    public event EventHandler? Selected;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value ?? "New chat");
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string DateLabel
    {
        get => (string)GetValue(DateLabelProperty);
        set => SetValue(DateLabelProperty, value ?? string.Empty);
    }

    private static void OnIsSelectedChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatHistoryItem)sender).ApplySelection();

    private void ApplySelection()
    {
        if (HistoryButton is null)
        {
            return;
        }

        HistoryButton.Style = (Style)Application.Current.Resources[IsSelected
            ? "GgufChatSelectedNavigationButtonStyle"
            : "GgufChatNavigationButtonStyle"];
        DateText.Style = (Style)Application.Current.Resources[IsSelected
            ? "GgufChatSelectedHistoryDateStyle"
            : "GgufChatHistoryDateStyle"];
    }

    internal void ApplyAutomationProjection(
        ListViewItem container,
        string dateGroup,
        int position,
        int setSize)
    {
        string selection = IsSelected ? "Selected" : "Not selected";
        container.IsSelected = IsSelected;
        AutomationProperties.SetName(
            container,
            $"{Title}. {dateGroup}. {selection}.");
        AutomationProperties.SetItemStatus(container, selection);
        AutomationProperties.SetPositionInSet(container, position);
        AutomationProperties.SetSizeOfSet(container, setSize);
    }

    internal void RaiseSelected() =>
        Selected?.Invoke(this, EventArgs.Empty);
}
