using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatHistoryItem : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ChatHistoryItem), new PropertyMetadata("New chat"));

    public ChatHistoryItem() => InitializeComponent();

    public event EventHandler? Selected;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value ?? "New chat");
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs eventArguments) =>
        Selected?.Invoke(this, EventArgs.Empty);
}
