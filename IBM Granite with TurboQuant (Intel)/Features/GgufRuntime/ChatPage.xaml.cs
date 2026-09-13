using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using GraniteEdgeAI.Features.ChatModels;
using GraniteEdgeAI.Features.GgufRuntime.Clipboard;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;

namespace GraniteEdgeAI.Features.GgufRuntime;

public sealed partial class ChatPage : Page
{
    internal void FocusMessageInput()
    {
        if (Composer.FindName("PromptTextBox") is Control input)
            input.Focus(FocusState.Programmatic);
    }
    private Storyboard? navigationEntrance;
    private const string CopyChatActionLabel = "Copy chat";
    private readonly IChatClipboard clipboard;
    private readonly Dictionary<Guid, ChatMessageBubble> transcriptBubbles = [];
    private readonly Dictionary<Guid, ChatCompletionStatus> transcriptStatuses = [];
    private readonly List<Guid> renderedMessageIds = [];
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer copyChatFeedbackTimer;
    private IReadOnlyList<ChatMessage> currentMessages = [];
    private Guid? renderedConversationId;
    private bool transcriptFollowRequested;
    private bool transcriptFollowAwaitingLayout;
    private double transcriptFollowOriginOffset;
    private bool compactNavigationIsOpen;
    private bool restoreCompactNavigationFocusAfterSettings;
    private string currentHistoryGroup = "Chat history";
    private bool suppressHistorySelectionChanged;
    private bool historyEditing;
    private bool sessionPreparing;
    private bool historyCommandsEnabled = true;
    private long pendingHistoryStatusRevision;
    private string? pendingHistoryStatus;
    private readonly Dictionary<ChatHistoryItem, Guid> historyIds = [];
    private readonly Dictionary<FrameworkElement, (AccessibilityView View, bool IsTabStop)>
        settingsBackgroundAccessibility = [];

    public ChatPage() : this(new WindowsChatClipboard())
    {
    }

    internal ChatPage(IChatClipboard clipboard)
    {
        this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        InitializeComponent();
        copyChatFeedbackTimer = DispatcherQueue.CreateTimer();
        copyChatFeedbackTimer.Interval = TimeSpan.FromSeconds(1.5);
        copyChatFeedbackTimer.Tick += CopyChatFeedbackTimer_Tick;
        Composer.ModelSelectionRequested += Composer_ModelSelectionRequested;
        Composer.ModelSelectionCancellationRequested +=
            Composer_ModelSelectionCancellationRequested;
        Composer.ImportModelRequested += Composer_ImportModelRequested;
        Composer.GetMoreLocalModelsRequested +=
            Composer_GetMoreLocalModelsRequested;
        ChatLayoutRoot.SizeChanged += ChatLayoutRoot_SizeChanged;
        Unloaded += ChatPage_Unloaded;
    }

    public event EventHandler? NewChatRequested;
    public event EventHandler? ImportModelRequested;
    public event EventHandler? GetMoreLocalModelsRequested;
    public event EventHandler<ElementTheme>? ThemeRequested;
    public event EventHandler<string>? SendRequested;
    public event EventHandler? StopRequested;
    internal event EventHandler? CloseSessionRequested;
    public event EventHandler<Guid>? ConversationSelected;
    internal event EventHandler<Guid>? ConversationRenameRequested;
    internal event EventHandler<Guid>? ConversationDeleteRequested;
    public event EventHandler<Guid>? ContinuationRequested;
    internal event EventHandler<string>? ModelSelectionRequested;
    internal Func<Task<bool>>? ModelSelectionPreparation { get; set; }
    private bool preparingModelSelection;
    internal event EventHandler? ModelSelectionCancellationRequested;

    public void SetModelHeader(string displayName, string runtimeDescription, string? conciseRuntime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDescription);
        ModelNameText.Text = conciseRuntime ?? (runtimeDescription.StartsWith("Cpu - ", StringComparison.Ordinal)
            ? "llama.cpp · CPU" : runtimeDescription.StartsWith("Vulkan - ", StringComparison.Ordinal)
                ? "llama.cpp · Vulkan" : runtimeDescription);
        FullModelNameText.Text = $"{displayName} · {runtimeDescription}";
        AutomationProperties.SetName(ModelNameText, FullModelNameText.Text);
        ToolTipService.SetToolTip(ModelNameText, $"{displayName} · {runtimeDescription}");
    }

    internal void ApplyModelLibrary(IReadOnlyList<ChatModelSnapshot> snapshots) =>
        Composer.ApplyModelLibrary(ChatModelSelectorPresentation.Create(snapshots));

    internal void ApplyModelSwitchPresentation(
        ChatModelSwitchPresentation presentation,
        string? requestedModelId)
    {
        Composer.ApplyModelSwitchPresentation(presentation, requestedModelId);
        if (presentation.RestoreFocus)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
                Composer.RestoreModelSelectionFocus(requestedModelId));
        }
    }

    public void AddHistoryGroup(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        currentHistoryGroup = label;
    }

    internal void ConfigureOpenVinoChat(string modelLabel)
    {
        HistoryRegion.Visibility = Visibility.Collapsed;
        RouteDetailsPanel.Visibility = Visibility.Visible;
        SettingsModelsTab.Visibility = Visibility.Collapsed;
        RouteStatusText.Visibility = Visibility.Visible;
        Composer.ConfigureExternalRoute(modelLabel);
    }

    internal void SetActiveModelLabel(string label) => Composer.SetActiveModelLabel(label);

    internal void ApplyOpenVinoChatState(GraniteEdgeAI.Features.Prompting.PromptSurfaceState state)
    {
        RouteCapabilityText.Text = state.CapabilitySummary;
        RouteExecutionText.Text = state.ExecutionEvidence;
        RouteBuildText.Text = state.BuildEvidence;
        RouteStatusText.Text = state.Announcement;
        RouteCloseButton.IsEnabled = state.CancelEnabled;
        Composer.ApplyExternalRouteState(state.SendEnabled, state.StopEnabled,
            state.LastEventKind is GraniteEdgeAI.Features.Prompting.PromptEventKind.GeneratingTurn
                or GraniteEdgeAI.Features.Prompting.PromptEventKind.GenerationConfirmed
                or GraniteEdgeAI.Features.Prompting.PromptEventKind.TextDelta
                or GraniteEdgeAI.Features.Prompting.PromptEventKind.StoppingTurn);
    }

    internal void SetNewConversationEnabled(bool enabled)
    {
        HeaderNewChatButton.IsEnabled = enabled;
        NewChatButton.IsEnabled = enabled;
    }

    private void RouteCloseButton_Click(object sender, RoutedEventArgs args) =>
        CloseSessionRequested?.Invoke(this, EventArgs.Empty);

    public void ClearHistory()
    {
        suppressHistorySelectionChanged = true;
        try
        {
            ChatHistoryList.Items.Clear();
            historyIds.Clear();
        }
        finally
        {
            suppressHistorySelectionChanged = false;
        }
    }

    public void AddHistoryConversation(Guid id, string title, bool isSelected)
    {
        var item = new ChatHistoryItem
        {
            Title = title,
            DateLabel = currentHistoryGroup,
            IsSelected = isSelected,
        };
        item.Tag = currentHistoryGroup;
        historyIds[item] = id;
        item.Selected += (_, _) =>
        {
            if (!historyEditing && !sessionPreparing) ConversationSelected?.Invoke(this, id);
        };
        ChatHistoryList.Items.Add(item);
        if (isSelected)
        {
            ConversationTitle.Text = title;
            suppressHistorySelectionChanged = true;
            try
            {
                ChatHistoryList.SelectedItem = item;
            }
            finally
            {
                suppressHistorySelectionChanged = false;
            }
        }
        item.Loaded += (_, _) => UpdateHistoryAutomation();
        UpdateHistoryAutomation();
    }

    private void ChatHistoryList_ItemClick(
        object sender,
        ItemClickEventArgs eventArguments)
    {
        if (!historyEditing && !sessionPreparing && !suppressHistorySelectionChanged &&
            eventArguments.ClickedItem is ChatHistoryItem item)
        {
            item.RaiseSelected();
        }
    }

    public void ShowConversation()
    {
        ApplyConversationLayout(hasMessages: true);
        EmptyConversationState.Visibility = Visibility.Collapsed;
        TranscriptList.Visibility = Visibility.Visible;
    }

    public void ClearTranscript() => ResetTranscript(null);

    internal void SetConversationTitle(string title) => ConversationTitle.Text = title;

    internal void PresentPendingConversation(
        Guid conversationId,
        string title,
        IReadOnlyList<ChatMessage> messages)
    {
        SynchronizeTranscript(conversationId, messages, forceFollowLatest: false);
        ConversationTitle.Text = title;
        SelectDisplayedHistoryItem(conversationId);
    }

    internal void PresentPendingConversationDeletion(
        Guid conversationId,
        ChatConversation? replacement,
        bool deletedConversationWasSelected)
    {
        ChatHistoryItem? removed = historyIds.FirstOrDefault(
            pair => pair.Value == conversationId).Key;
        if (removed is not null)
        {
            suppressHistorySelectionChanged = true;
            try
            {
                historyIds.Remove(removed);
                ChatHistoryList.Items.Remove(removed);
            }
            finally
            {
                suppressHistorySelectionChanged = false;
            }
            UpdateHistoryAutomation();
        }

        if (!deletedConversationWasSelected)
        {
            FocusPendingHistoryTarget(null);
            return;
        }

        if (replacement is null)
        {
            ClearTranscript();
            ConversationTitle.Text = "New chat";
            FocusPendingHistoryTarget(null);
            return;
        }

        PresentPendingConversation(
            replacement.Id,
            replacement.Title,
            replacement.Messages);
        FocusPendingHistoryTarget(replacement.Id);
    }

    private void FocusPendingHistoryTarget(Guid? preferredConversationId)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ChatHistoryItem? item = preferredConversationId is Guid preferred
                ? historyIds.FirstOrDefault(pair => pair.Value == preferred).Key
                : historyIds.Keys.FirstOrDefault(candidate => candidate.IsSelected);
            if (item is not null)
            {
                ChatHistoryList.ScrollIntoView(item);
                ChatHistoryList.UpdateLayout();
                if (ChatHistoryList.ContainerFromItem(item) is Control container)
                {
                    container.Focus(FocusState.Programmatic);
                    return;
                }
            }
            HeaderNewChatButton.Focus(FocusState.Programmatic);
        });
    }

    private void SelectDisplayedHistoryItem(Guid conversationId)
    {
        ChatHistoryItem? selected = null;
        suppressHistorySelectionChanged = true;
        try
        {
            foreach ((ChatHistoryItem item, Guid id) in historyIds)
            {
                bool isSelected = id == conversationId;
                item.IsSelected = isSelected;
                if (isSelected)
                {
                    selected = item;
                }
            }
            ChatHistoryList.SelectedItem = selected;
        }
        finally
        {
            suppressHistorySelectionChanged = false;
        }
        UpdateHistoryAutomation();
    }

    private void ApplyConversationLayout(bool hasMessages)
    {
        EmptyTopSpace.Height = hasMessages ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        EmptyBottomSpace.Height = EmptyTopSpace.Height;
        ConversationContentRow.Height = hasMessages ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
    }

    public void AddMessage(string content, bool isUser, string statusText = "")
    {
        ChatMessageBubble bubble = CreateMessageBubble();
        bubble.MessageContent = content;
        bubble.IsUser = isUser;
        bubble.StatusText = statusText;
        TranscriptList.Items.Add(bubble);
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

        ChatMessage[] visibleMessages = messages
            .Where(message => message.IsVisible)
            .ToArray();
        ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(TranscriptList);
        bool shouldFollowLatest = forceFollowLatest ||
            scrollViewer is null ||
            ShouldFollowOutput(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
        bool requiresTranscriptReset = RequiresTranscriptReset(
            conversationId,
            visibleMessages);
        if (requiresTranscriptReset)
        {
            ResetTranscript(conversationId);
        }
        else if (!shouldFollowLatest)
        {
            CancelTranscriptFollow();
        }

        for (int index = 0; index < visibleMessages.Length; index++)
        {
            ChatMessage message = visibleMessages[index];
            if (!transcriptBubbles.TryGetValue(message.Id, out ChatMessageBubble? bubble))
            {
                bubble = CreateMessageBubble();
                transcriptBubbles.Add(message.Id, bubble);
                renderedMessageIds.Add(message.Id);
                TranscriptList.Items.Add(bubble);
            }

            bubble.MessageContent = message.Content;
            bubble.IsUser = message.Role == ChatMessageRole.User;
            bubble.StatusText = FormatStatus(message.Status);
            bubble.MessageId = message.Id;
            bubble.CanContinue =
                index == visibleMessages.Length - 1 &&
                message.Role == ChatMessageRole.Assistant &&
                message.Status == ChatCompletionStatus.LimitReached;
            string role = message.Role == ChatMessageRole.User ? "User" : "Assistant";
            string status = string.IsNullOrEmpty(FormatStatus(message.Status))
                ? "Complete"
                : FormatStatus(message.Status);
            AutomationProperties.SetLiveSetting(bubble, AutomationLiveSetting.Off);
            AutomationProperties.SetName(bubble, $"{role}. {message.Content}");
            AutomationProperties.SetItemStatus(bubble, status);
            AutomationProperties.SetPositionInSet(bubble, index + 1);
            AutomationProperties.SetSizeOfSet(bubble, visibleMessages.Length);
            bool announceCompletion = transcriptStatuses.TryGetValue(message.Id, out ChatCompletionStatus previousStatus) &&
                message.Role == ChatMessageRole.Assistant &&
                previousStatus is ChatCompletionStatus.Pending or ChatCompletionStatus.Streaming &&
                message.Status is not ChatCompletionStatus.Pending and not ChatCompletionStatus.Streaming;
            transcriptStatuses[message.Id] = message.Status;
            if (announceCompletion)
            {
                AutomationProperties.SetLiveSetting(bubble, AutomationLiveSetting.Polite);
                AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(bubble) ??
                    FrameworkElementAutomationPeer.CreatePeerForElement(bubble);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        currentMessages = visibleMessages;
        CopyChatButton.IsEnabled = currentMessages.Any(
            message => !string.IsNullOrWhiteSpace(message.Content));
        CopyChatButton.Visibility = CopyChatButton.IsEnabled ? Visibility.Visible : Visibility.Collapsed;

        if (visibleMessages.Length == 0)
        {
            ApplyConversationLayout(hasMessages: false);
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
        ChatCompletionStatus.LimitReached => "Response limit reached",
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
        foreach (ChatMessageBubble bubble in TranscriptList.Items
            .OfType<ChatMessageBubble>())
        {
            bubble.CopyRequested -= MessageBubble_CopyRequested;
            bubble.ContinueRequested -= MessageBubble_ContinueRequested;
        }

        TranscriptList.Items.Clear();
        transcriptBubbles.Clear();
        transcriptStatuses.Clear();
        renderedMessageIds.Clear();
        renderedConversationId = conversationId;
        currentMessages = [];
        CopyChatButton.IsEnabled = false;
        ApplyConversationLayout(hasMessages: false);
        CopyChatButton.Visibility = Visibility.Collapsed;
        ConversationTitle.Text = "New conversation";
        ResetCopyChatFeedback();
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
        copyChatFeedbackTimer.Stop();
        ResetCopyChatFeedback();
        CancelTranscriptFollow();
        CloseCompactNavigation(restoreFocus: false);
        restoreCompactNavigationFocusAfterSettings = false;
        CompactNavigationScrim.Visibility = Visibility.Collapsed;
        SettingsOverlay.Visibility = Visibility.Collapsed;
        SetSettingsBackgroundIsolated(isolated: false);
    }

    public void SetGenerating(bool isGenerating) => Composer.IsGenerating = isGenerating;

    internal void SetStopping(bool stopping) => Composer.SetStopping(stopping);

    internal void ShowStopFailure()
    {
        Composer.SetStopping(false);
        RouteStatusText.Text = "The response could not be stopped. Try Stop again, or reload the model if it is no longer responding. Your visible response is retained.";
        RouteStatusText.Visibility = Visibility.Visible;
    }

    internal void SetSessionPreparing(bool preparing)
    {
        sessionPreparing = preparing;
        Composer.SetSessionPreparing(preparing);
        SetNewConversationEnabled(!preparing);
        RouteStatusText.Text = preparing ? "Switching model…" : string.Empty;
        RouteStatusText.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void SetConversationReady(bool ready) => Composer.SetConversationReady(ready);

    internal void ShowHistoryResumeNotice(string reason = "This conversation cannot be resumed.")
    {
        RouteStatusText.Text = $"{reason} Your previous conversation is saved. A new chat is ready.";
        RouteStatusText.Visibility = Visibility.Visible;
    }

    internal void CompleteConversationNavigation()
    {
        Composer.FocusPrompt();
    }

    internal void ShowHistorySelectionFailure(string reason = "The conversation could not be loaded. Try again.")
    {
        RouteStatusText.Text = $"{reason} Your current conversation is unchanged.";
        RouteStatusText.Visibility = Visibility.Visible;
        if (compactNavigationIsOpen)
        {
            ChatHistoryList.UpdateLayout();
            if (ChatHistoryList.ContainerFromItem(ChatHistoryList.SelectedItem) is Control selected)
                selected.Focus(FocusState.Programmatic);
            else ChatHistoryList.Focus(FocusState.Programmatic);
        }
    }

    private void NewChatButton_Click(object sender, RoutedEventArgs eventArguments)
    {
        NewChatRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void SetHistoryEditing(bool editing)
    {
        historyEditing = editing;
        Composer.SetSessionPreparing(editing);
        SetNewConversationEnabled(!editing);
    }

    internal void SetHistoryLoading(bool loading)
    {
        historyEditing = loading;
        Composer.SetSessionPreparing(loading, allowDraftEditing: true);
        SetNewConversationEnabled(!loading);
        if (loading)
        {
            RouteStatusText.Text = "Loading conversation…";
            RouteStatusText.Visibility = Visibility.Visible;
        }
        else if (RouteStatusText.Text == "Loading conversation…")
        {
            RouteStatusText.Text = string.Empty;
            RouteStatusText.Visibility = Visibility.Collapsed;
        }
    }

    internal long BeginConversationLoading() =>
        BeginPendingHistoryStatus("Loading conversation…", allowDraftEditing: true);

    internal long BeginConversationDeletion() =>
        BeginPendingHistoryStatus("Deleting chat…", allowDraftEditing: false);

    internal void CompletePendingHistoryStatus(long revision)
    {
        if (revision != pendingHistoryStatusRevision)
        {
            return;
        }
        string? status = pendingHistoryStatus;
        pendingHistoryStatus = null;
        checked { pendingHistoryStatusRevision++; }
        if (status is not null && string.Equals(RouteStatusText.Text, status, StringComparison.Ordinal))
        {
            RouteStatusText.Text = string.Empty;
            RouteStatusText.Visibility = Visibility.Collapsed;
        }
    }

    private long BeginPendingHistoryStatus(string status, bool allowDraftEditing)
    {
        if (string.Equals(pendingHistoryStatus, status, StringComparison.Ordinal))
        {
            return pendingHistoryStatusRevision;
        }
        historyEditing = true;
        Composer.SetSessionPreparing(true, allowDraftEditing);
        SetNewConversationEnabled(false);
        long revision = checked(++pendingHistoryStatusRevision);
        pendingHistoryStatus = status;
        RouteStatusText.Text = status;
        RouteStatusText.Visibility = Visibility.Visible;
        (FrameworkElementAutomationPeer.FromElement(RouteStatusText)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(RouteStatusText))?
            .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        return revision;
    }

    private string? displayedGenerationFailure;
    internal void ShowGenerationFailure(string? message)
    {
        if (message is not null)
        {
            RouteStatusText.Text = message;
            RouteStatusText.Visibility = Visibility.Visible;
        }
        else if (displayedGenerationFailure is not null && RouteStatusText.Text == displayedGenerationFailure)
        {
            RouteStatusText.Text = string.Empty;
            RouteStatusText.Visibility = Visibility.Collapsed;
        }
        displayedGenerationFailure = message;
    }

    internal void SetHistoryCommandsEnabled(bool enabled)
    {
        historyCommandsEnabled = enabled;
        foreach (ChatHistoryItem item in ChatHistoryList.Items.OfType<ChatHistoryItem>())
            if (ChatHistoryList.ContainerFromItem(item) is ListViewItem { ContextFlyout: MenuFlyout menu })
                foreach (MenuFlyoutItem command in menu.Items.OfType<MenuFlyoutItem>()) command.IsEnabled = enabled;
    }

    internal async Task<string?> RequestConversationTitleAsync(string title, CancellationToken cancellationToken)
    {
        var input = new TextBox { Header = "Chat name", Text = title, MaxLength = ChatTitlePolicy.MaximumTitleLength };
        AutomationProperties.SetAutomationId(input, "RenameChatTitle");
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = ActualTheme, Title = "Rename chat", Content = input,
            PrimaryButtonText = "Rename", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(title)
        };
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);
        dialog.Opened += (_, _) => { input.Focus(FocusState.Programmatic); input.SelectAll(); };
        using var registration = cancellationToken.Register(() => DispatcherQueue.TryEnqueue(() => dialog.Hide()));
        cancellationToken.ThrowIfCancellationRequested();
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }

    internal async Task<bool> ConfirmDeleteConversationAsync(string title, CancellationToken cancellationToken)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = ActualTheme, Title = "Delete chat?",
            Content = $"Delete ‘{title}’ and its messages? This cannot be undone.",
            PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
        };
        using var registration = cancellationToken.Register(() => DispatcherQueue.TryEnqueue(() => dialog.Hide()));
        cancellationToken.ThrowIfCancellationRequested();
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    internal async Task<bool> ConfirmStopForModelSwitchAsync(CancellationToken token)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = ActualTheme,
            Title = "Stop this response and switch models?",
            Content = "The response generated so far will stay in this conversation.",
            PrimaryButtonText = "Stop and switch", CloseButtonText = "Keep generating",
            DefaultButton = ContentDialogButton.Close
        };
        using var registration = token.Register(() => DispatcherQueue.TryEnqueue(() => dialog.Hide()));
        token.ThrowIfCancellationRequested();
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    internal void ShowHistoryEditFailure()
    {
        RouteStatusText.Text = "The chat could not be updated. Your conversation is still available. Please try again.";
        RouteStatusText.Visibility = Visibility.Visible;
        (FrameworkElementAutomationPeer.FromElement(RouteStatusText)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(RouteStatusText))?
            .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    internal void RestoreHistoryFocus(Guid id)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ChatHistoryItem? item = historyIds.FirstOrDefault(pair => pair.Value == id).Key
                ?? ChatHistoryList.SelectedItem as ChatHistoryItem;
            if (item is not null)
            {
                ChatHistoryList.ScrollIntoView(item);
                ChatHistoryList.UpdateLayout();
                if (ChatHistoryList.ContainerFromItem(item) is Control container)
                { container.Focus(FocusState.Programmatic); return; }
            }
            HeaderNewChatButton.Focus(FocusState.Programmatic);
        });
    }

    private void ImportModelButton_Click(object sender, RoutedEventArgs eventArguments)
    {
        CloseCompactNavigation(restoreFocus: false);
        ImportModelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CompactNavigationButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (compactNavigationIsOpen)
            CloseCompactNavigation(restoreFocus: true);
        else
            OpenCompactNavigation();
    }

    private void CompactPaneDismissButton_Click(
        object sender,
        RoutedEventArgs eventArguments) =>
        CloseCompactNavigation(restoreFocus: true);

    private void CompactNavigationScrim_Click(
        object sender,
        RoutedEventArgs eventArguments) =>
        CloseCompactNavigation(restoreFocus: true);

    private void SettingsButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        restoreCompactNavigationFocusAfterSettings = compactNavigationIsOpen;
        CloseCompactNavigation(restoreFocus: false);
        SetSettingsBackgroundIsolated(isolated: true);
        SettingsOverlay.Visibility = Visibility.Visible;
        SettingsCloseButton.Focus(FocusState.Programmatic);
    }

    private void SettingsCloseButton_Click(
        object sender,
        RoutedEventArgs eventArguments) =>
        CloseSettings(restoreFocus: true);

    private void SettingsImportModelButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        CloseSettings(restoreFocus: false);
        ImportModelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsGetMoreModelsButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        CloseSettings(restoreFocus: false);
        GetMoreLocalModelsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ThemeRadioButton_Checked(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (sender is not RadioButton { Tag: string requestedTheme })
        {
            return;
        }

        ElementTheme theme = requestedTheme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        RequestedTheme = theme;
        ThemeRequested?.Invoke(this, theme);
    }

    private void ChatPage_PreviewKeyDown(
        object sender,
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs eventArguments)
    {
        if (eventArguments.Key != VirtualKey.Escape ||
            SettingsOverlay.Visibility != Visibility.Visible)
        {
            if (eventArguments.Key == VirtualKey.Escape && compactNavigationIsOpen)
            {
                eventArguments.Handled = true;
                CloseCompactNavigation(restoreFocus: true);
            }

            return;
        }

        eventArguments.Handled = true;
        CloseSettings(restoreFocus: true);
    }

    private void CloseSettings(bool restoreFocus)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        SetSettingsBackgroundIsolated(isolated: false);
        if (restoreFocus)
        {
            if (restoreCompactNavigationFocusAfterSettings && IsCompactNavigationLayout)
            {
                CompactNavigationButton.Focus(FocusState.Programmatic);
            }
            else
            {
                SettingsButton.Focus(FocusState.Programmatic);
            }
        }

        restoreCompactNavigationFocusAfterSettings = false;
    }

    private void SetSettingsBackgroundIsolated(bool isolated)
    {
        if (!isolated)
        {
            foreach ((FrameworkElement element, (AccessibilityView view, bool isTabStop))
                     in settingsBackgroundAccessibility)
            {
                AutomationProperties.SetAccessibilityView(element, view);
                element.IsTabStop = isTabStop;
            }

            settingsBackgroundAccessibility.Clear();
            return;
        }

        settingsBackgroundAccessibility.Clear();
        IsolateSettingsBackground(HistoryRail);
        IsolateSettingsBackground(ConversationPanel);
    }

    private void IsolateSettingsBackground(DependencyObject root)
    {
        if (root is FrameworkElement element)
        {
            settingsBackgroundAccessibility[element] = (
                AutomationProperties.GetAccessibilityView(element),
                element.IsTabStop);
            AutomationProperties.SetAccessibilityView(element, AccessibilityView.Raw);
            element.IsTabStop = false;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            IsolateSettingsBackground(VisualTreeHelper.GetChild(root, index));
        }
    }

    private bool IsCompactNavigationLayout => true;

    private void OpenCompactNavigation()
    {
        if (!IsCompactNavigationLayout)
        {
            return;
        }

        compactNavigationIsOpen = true;
        UpdateHistoryColumn();
        // the history rail is nonmodal; conversation controls remain interactive
        CompactNavigationScrim.Visibility = Visibility.Collapsed;
        HistoryRail.Visibility = Visibility.Visible;
        navigationEntrance?.Stop();
        HistoryRail.Opacity = 1;
        HistoryRailTranslation.X = 0;
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            navigationEntrance = new Storyboard();
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(180) };
            Storyboard.SetTarget(fade, HistoryRail);
            Storyboard.SetTargetProperty(fade, "Opacity");
            navigationEntrance.Children.Add(fade);
            var slide = new DoubleAnimation { From = -18, To = 0, Duration = TimeSpan.FromMilliseconds(180) };
            Storyboard.SetTarget(slide, HistoryRailTranslation);
            Storyboard.SetTargetProperty(slide, "X");
            navigationEntrance.Children.Add(slide);
            navigationEntrance.Begin();
        }
        CompactPaneDismissButton.Visibility = ActualWidth < 840 ? Visibility.Visible : Visibility.Collapsed;
        NewChatButton.Focus(FocusState.Programmatic);
    }

    private void CloseCompactNavigation(bool restoreFocus)
    {
        bool wasOpen = compactNavigationIsOpen;
        navigationEntrance?.Stop();
        navigationEntrance = null;
        HistoryRail.Opacity = 1;
        HistoryRailTranslation.X = 0;
        compactNavigationIsOpen = false;
        UpdateHistoryColumn();
        CompactNavigationScrim.Visibility = Visibility.Collapsed;

        if (IsCompactNavigationLayout)
        {
            HistoryRail.Visibility = Visibility.Collapsed;
        }

        if (restoreFocus && wasOpen && IsCompactNavigationLayout)
        {
            CompactNavigationButton.Focus(FocusState.Programmatic);
        }
    }

    private void ChatLayoutRoot_SizeChanged(
        object sender,
        SizeChangedEventArgs eventArguments)
    {
        UpdateHistoryColumn();
        if (!compactNavigationIsOpen)
        {
            HistoryRail.Visibility = Visibility.Collapsed;
            CompactNavigationScrim.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateHistoryColumn()
    {
        HistoryColumn.Width = new GridLength(compactNavigationIsOpen
            ? Math.Clamp(ActualWidth * .4d, 160d, 256d) : 0d);
        // when the existing centered chat leaves room for the rail, reserve no
        // space from its layout. only compact windows reflow beside the rail
        bool hasSpareRailSpace = ActualWidth >= 1452d;
        Grid.SetColumn(ConversationInset, hasSpareRailSpace ? 0 : 1);
        Grid.SetColumnSpan(ConversationInset, hasSpareRailSpace ? 2 : 1);
        ConversationInset.MaxWidth = hasSpareRailSpace ? 940d : double.PositiveInfinity;
        ConversationInset.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private void Composer_SendRequested(object? sender, string prompt) =>
        SendRequested?.Invoke(this, prompt);

    private void Composer_StopRequested(object? sender, EventArgs eventArguments) =>
        StopRequested?.Invoke(this, EventArgs.Empty);

    private async void Composer_ModelSelectionRequested(object? sender, string modelId)
    {
        if (preparingModelSelection) return;
        preparingModelSelection = true;
        try
        {
            if (ModelSelectionPreparation is not null && !await ModelSelectionPreparation()) return;
            ModelSelectionRequested?.Invoke(this, modelId);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            ShowGenerationFailure("The model could not be switched. Your conversation is still available. Try again.");
        }
        finally { preparingModelSelection = false; }
    }

    private void Composer_ModelSelectionCancellationRequested(
        object? sender,
        EventArgs eventArguments) =>
        ModelSelectionCancellationRequested?.Invoke(this, EventArgs.Empty);

    private void Composer_ImportModelRequested(
        object? sender,
        EventArgs eventArguments) =>
        ImportModelRequested?.Invoke(this, EventArgs.Empty);

    private void Composer_GetMoreLocalModelsRequested(
        object? sender,
        EventArgs eventArguments) =>
        GetMoreLocalModelsRequested?.Invoke(this, EventArgs.Empty);

    private ChatMessageBubble CreateMessageBubble()
    {
        var bubble = new ChatMessageBubble
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        bubble.CopyRequested += MessageBubble_CopyRequested;
        bubble.ContinueRequested += MessageBubble_ContinueRequested;
        return bubble;
    }

    private void UpdateHistoryAutomation()
    {
        ChatHistoryItem[] conversations = ChatHistoryList.Items.OfType<ChatHistoryItem>().ToArray();
        foreach (IGrouping<string, ChatHistoryItem> groupItems in conversations.GroupBy(
                     item => item.Tag as string ?? "Chat history",
                     StringComparer.Ordinal))
        {
            ChatHistoryItem[] items = groupItems.ToArray();
            for (int index = 0; index < items.Length; index++)
            {
                ChatHistoryItem item = items[index];
                string selection = item.IsSelected ? "Selected" : "Not selected";
                AutomationProperties.SetName(
                    item,
                    $"{item.Title}. {groupItems.Key}. {selection}.");
                AutomationProperties.SetItemStatus(item, selection);
                AutomationProperties.SetPositionInSet(item, index + 1);
                AutomationProperties.SetSizeOfSet(item, items.Length);
                if (ChatHistoryList.ContainerFromItem(item) is ListViewItem container)
                {
                    if (historyIds.TryGetValue(item, out Guid id))
                    {
                        var menu = new MenuFlyout();
                        var rename = new MenuFlyoutItem { Text = "Rename", IsEnabled = historyCommandsEnabled };
                        var delete = new MenuFlyoutItem { Text = "Delete", IsEnabled = historyCommandsEnabled };
                        AutomationProperties.SetAutomationId(rename, "RenameChat");
                        AutomationProperties.SetAutomationId(delete, "DeleteChat");
                        rename.Click += (_, _) => ConversationRenameRequested?.Invoke(this, id);
                        delete.Click += (_, _) => ConversationDeleteRequested?.Invoke(this, id);
                        menu.Items.Add(rename);
                        menu.Items.Add(delete);
                        container.ContextFlyout = menu;
                    }
                    item.ApplyAutomationProjection(
                        container,
                        groupItems.Key,
                        index + 1,
                        items.Length);
                }
            }
        }
    }

    private void MessageBubble_ContinueRequested(object? sender, Guid messageId) =>
        ContinuationRequested?.Invoke(this, messageId);

    private void MessageBubble_CopyRequested(object? sender, string content)
    {
        if (sender is ChatMessageBubble bubble)
        {
            bubble.ShowCopyResult(clipboard.TrySetText(content));
        }
    }

    private void CopyChatButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        string text = ChatTranscriptFormatter.Format(currentMessages);
        if (!string.IsNullOrEmpty(text))
        {
            ShowCopyChatResult(clipboard.TrySetText(text));
        }
    }

    private void ShowCopyChatResult(bool succeeded)
    {
        copyChatFeedbackTimer.Stop();
        CopyChatGlyph.Glyph = succeeded ? "\uE73E" : "\uE783";
        string label = succeeded ? "Copied" : "Couldn't copy";
        CopyChatLabel.Text = label;
        AutomationProperties.SetName(CopyChatButton, label);
        ToolTipService.SetToolTip(CopyChatButton, label);
        copyChatFeedbackTimer.Start();
    }

    private void CopyChatFeedbackTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object eventArguments)
    {
        sender.Stop();
        ResetCopyChatFeedback();
    }

    private void ResetCopyChatFeedback()
    {
        if (CopyChatButton is null)
        {
            return;
        }

        CopyChatGlyph.Glyph = "\uE8C8";
        CopyChatLabel.Text = CopyChatActionLabel;
        AutomationProperties.SetName(CopyChatButton, CopyChatActionLabel);
        ToolTipService.SetToolTip(CopyChatButton, CopyChatActionLabel);
    }
}
