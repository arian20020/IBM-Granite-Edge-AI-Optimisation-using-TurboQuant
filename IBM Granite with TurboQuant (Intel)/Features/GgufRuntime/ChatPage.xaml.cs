using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.Features.GgufRuntime.History;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.GgufRuntime;

public sealed partial class ChatPage : Page
{
    private readonly Dictionary<Guid, ChatMessageBubble> transcriptBubbles = [];
    private readonly List<Guid> renderedMessageIds = [];
    private Guid? renderedConversationId;
    private bool transcriptFollowRequested;
    private bool transcriptFollowAwaitingLayout;
    private double transcriptFollowOriginOffset;

    public ChatPage()
    {
        InitializeComponent();
        Unloaded += ChatPage_Unloaded;
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

    public void ClearTranscript() => ResetTranscript(null);

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
        RequestTranscriptFollow(
            FindDescendant<ScrollViewer>(TranscriptList)?.VerticalOffset ?? 0);
    }

    internal void SynchronizeTranscript(
        Guid conversationId,
        IReadOnlyList<ChatMessage> messages,
        bool forceFollowLatest)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException(
                "A conversation identifier is required.",
                nameof(conversationId));
        }

        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(TranscriptList);
        bool shouldFollowLatest = forceFollowLatest ||
            scrollViewer is null ||
            ShouldFollowOutput(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
        bool requiresTranscriptReset = RequiresTranscriptReset(conversationId, messages);
        if (requiresTranscriptReset)
        {
            ResetTranscript(conversationId);
        }
        else if (!shouldFollowLatest)
        {
            CancelTranscriptFollow();
        }

        for (int index = 0; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];
            if (!transcriptBubbles.TryGetValue(message.Id, out ChatMessageBubble? bubble))
            {
                bubble = new ChatMessageBubble
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                transcriptBubbles.Add(message.Id, bubble);
                renderedMessageIds.Add(message.Id);
                TranscriptList.Items.Add(bubble);
            }

            bubble.MessageContent = message.Content;
            bubble.IsUser = message.Role == ChatMessageRole.User;
            bubble.StatusText = FormatStatus(message.Status);
        }

        if (messages.Count == 0)
        {
            CancelTranscriptFollow();
            EmptyConversationState.Visibility = Visibility.Visible;
            TranscriptList.Visibility = Visibility.Collapsed;
            return;
        }

        ShowConversation();
        if (shouldFollowLatest)
        {
            RequestTranscriptFollow(
                requiresTranscriptReset ? 0 : scrollViewer?.VerticalOffset ?? 0);
        }
    }

    internal static bool ShouldFollowOutput(
        double verticalOffset,
        double scrollableHeight) =>
        scrollableHeight <= 0 || scrollableHeight - verticalOffset <= 48;

    internal static string FormatStatus(ChatCompletionStatus status) => status switch
    {
        ChatCompletionStatus.Pending => "Thinking…",
        ChatCompletionStatus.Streaming => "Generating…",
        ChatCompletionStatus.Stopped => "Stopped",
        ChatCompletionStatus.Incomplete => "Incomplete",
        ChatCompletionStatus.Failed => "Failed",
        _ => string.Empty,
    };

    private bool RequiresTranscriptReset(
        Guid conversationId,
        IReadOnlyList<ChatMessage> messages)
    {
        if (renderedConversationId != conversationId ||
            renderedMessageIds.Count > messages.Count)
        {
            return true;
        }

        for (int index = 0; index < renderedMessageIds.Count; index++)
        {
            if (renderedMessageIds[index] != messages[index].Id)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetTranscript(Guid? conversationId)
    {
        CancelTranscriptFollow();
        TranscriptList.Items.Clear();
        transcriptBubbles.Clear();
        renderedMessageIds.Clear();
        renderedConversationId = conversationId;
        EmptyConversationState.Visibility = Visibility.Visible;
        TranscriptList.Visibility = Visibility.Collapsed;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void ScrollTranscriptToEnd()
    {
        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(TranscriptList);
        bool shouldApplyFollow = transcriptFollowRequested &&
            scrollViewer is not null &&
            scrollViewer.VerticalOffset + 0.5 >= transcriptFollowOriginOffset;
        transcriptFollowRequested = false;
        if (shouldApplyFollow && scrollViewer!.ScrollableHeight > 0)
        {
            scrollViewer.ChangeView(
                horizontalOffset: null,
                verticalOffset: scrollViewer.ScrollableHeight,
                zoomFactor: null,
                disableAnimation: true);
        }
    }

    private void RequestTranscriptFollow(double verticalOffset)
    {
        transcriptFollowRequested = true;
        transcriptFollowOriginOffset = verticalOffset;
        if (!transcriptFollowAwaitingLayout)
        {
            transcriptFollowAwaitingLayout = true;
            TranscriptList.LayoutUpdated += TranscriptList_LayoutUpdated;
        }
    }

    private void CancelTranscriptFollow()
    {
        transcriptFollowRequested = false;
        if (transcriptFollowAwaitingLayout)
        {
            TranscriptList.LayoutUpdated -= TranscriptList_LayoutUpdated;
            transcriptFollowAwaitingLayout = false;
        }
    }

    private void TranscriptList_LayoutUpdated(object? sender, object eventArguments)
    {
        TranscriptList.LayoutUpdated -= TranscriptList_LayoutUpdated;
        transcriptFollowAwaitingLayout = false;
        ScrollTranscriptToEnd();
    }

    private void ChatPage_Unloaded(object sender, RoutedEventArgs eventArguments)
    {
        Unloaded -= ChatPage_Unloaded;
        CancelTranscriptFollow();
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
