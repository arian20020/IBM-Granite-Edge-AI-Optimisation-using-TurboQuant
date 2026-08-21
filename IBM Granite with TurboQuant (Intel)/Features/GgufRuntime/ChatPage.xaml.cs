using System;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.GgufRuntime;

public sealed partial class ChatPage : Page
{
    public ChatPage()
    {
        InitializeComponent();
    }

    public event EventHandler? NewChatRequested;
    public event EventHandler? ImportModelRequested;
    public event EventHandler<string>? SendRequested;
    public event EventHandler? StopRequested;
    public event EventHandler<Guid>? ConversationSelected;

    public void SetModelHeader(string displayName, string runtimeDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDescription);
        ModelNameText.Text = $"{displayName} · {runtimeDescription}";
    }

    public void AddHistoryGroup(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ChatHistoryList.Items.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 14, 0, 6),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["GgufChatDateHeadingBrush"],
        });
    }

    public void ClearHistory() => ChatHistoryList.Items.Clear();

    public void AddHistoryConversation(Guid id, string title, bool isSelected)
    {
        var item = new ChatHistoryItem
        {
            Title = title,
            IsSelected = isSelected,
        };
        item.Selected += (_, _) => ConversationSelected?.Invoke(this, id);
        ChatHistoryList.Items.Add(item);
    }

    public void ShowConversation()
    {
        EmptyConversationState.Visibility = Visibility.Collapsed;
        TranscriptList.Visibility = Visibility.Visible;
    }

    public void ClearTranscript()
    {
        TranscriptList.Items.Clear();
        EmptyConversationState.Visibility = Visibility.Visible;
        TranscriptList.Visibility = Visibility.Collapsed;
    }

    public void AddMessage(string content, bool isUser, string statusText = "")
    {
        TranscriptList.Items.Add(new ChatMessageBubble
        {
            MessageContent = content,
            IsUser = isUser,
            StatusText = statusText,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        ShowConversation();
        TranscriptList.ScrollIntoView(TranscriptList.Items[^1]);
    }

    public void SetGenerating(bool isGenerating) => Composer.IsGenerating = isGenerating;

    private void NewChatButton_Click(object sender, RoutedEventArgs eventArguments) =>
        NewChatRequested?.Invoke(this, EventArgs.Empty);

    private void ImportModelButton_Click(object sender, RoutedEventArgs eventArguments) =>
        ImportModelRequested?.Invoke(this, EventArgs.Empty);

    private void Composer_SendRequested(object? sender, string prompt) =>
        SendRequested?.Invoke(this, prompt);

    private void Composer_StopRequested(object? sender, EventArgs eventArguments) =>
        StopRequested?.Invoke(this, EventArgs.Empty);
}
