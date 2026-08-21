using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatHistoryItem : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ChatHistoryItem), new PropertyMetadata("New chat"));
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

        HistoryButton.Background = (Microsoft.UI.Xaml.Media.Brush)
            Application.Current.Resources[IsSelected
                ? "GgufChatNavigationSelectedBrush"
                : "GgufChatNavigationRestBrush"];
        SelectionIndicator.Visibility = IsSelected
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs eventArguments) =>
        Selected?.Invoke(this, EventArgs.Empty);
}
