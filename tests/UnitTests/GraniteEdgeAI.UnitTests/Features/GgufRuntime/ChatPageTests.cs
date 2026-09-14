using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.Clipboard;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatPageTests
{
    [UITestMethod]
    [TestCategory("ChatTextInteractions")]
    public void EmptyMessageStatusDoesNotReserveSpaceAboveCopy()
    {
        var bubble = new ChatMessageBubble { MessageContent = "An answer." };
        var status = (TextBlock)bubble.FindName("StatusTextBlock");
        Assert.AreEqual(Visibility.Collapsed, status.Visibility);
        bubble.StatusText = "Stopped";
        Assert.AreEqual(Visibility.Visible, status.Visibility);
        bubble.StatusText = string.Empty;
        Assert.AreEqual(Visibility.Collapsed, status.Visibility);
        Assert.AreEqual(44d, ((Button)bubble.FindName("CopyMessageButton")).Height);
    }

    [UITestMethod]
    [TestCategory("ChatTextInteractions")]
    public void MarkdownHeadingsHaveHierarchyAndInlineStrongRemainsBodySized()
    {
        var text = new RichTextBlock();
        GraniteEdgeAI.Features.GgufRuntime.Presentation.ChatMarkdownRenderer.Render(text, "# First\n## Second\n### Third\n**Body emphasis**");
        Assert.AreEqual(24d, text.Blocks[0].FontSize);
        Assert.AreEqual(22d, text.Blocks[1].FontSize);
        Assert.AreEqual(20d, text.Blocks[2].FontSize);
        var body = (Microsoft.UI.Xaml.Documents.Paragraph)text.Blocks[3];
        Assert.AreEqual(Microsoft.UI.Text.FontWeights.SemiBold, body.Inlines[0].FontWeight);
        Assert.AreEqual(text.FontSize, body.FontSize);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PageContainsRailDatedHistoryTranscriptHeaderAndComposer()
    {
        var page = new ChatPage();

        Assert.IsInstanceOfType<Grid>(page.FindName("ChatLayoutRoot"));
        Assert.IsInstanceOfType<Button>(page.FindName("NewChatButton"));
        Assert.IsInstanceOfType<ListView>(page.FindName("ChatHistoryList"));
        Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList"));
        Assert.IsInstanceOfType<TextBlock>(page.FindName("ModelNameText"));
        Assert.IsNotNull(page.FindName("Composer"));
        Assert.IsNotNull(page.FindName("EmptyConversationState"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MessageBubblesUseReadableLightRoleSurfaces()
    {
        var assistant = new ChatMessageBubble { MessageContent = "Assistant", IsUser = false };
        var user = new ChatMessageBubble { MessageContent = "User", IsUser = true };
        TextBlock assistantIdentity = Assert.IsInstanceOfType<TextBlock>(
            assistant.FindName("AssistantIdentityText"));
        TextBlock userIdentity = Assert.IsInstanceOfType<TextBlock>(
            user.FindName("AssistantIdentityText"));

        Assert.AreEqual("Granite Edge AI", assistantIdentity.Text);
        Assert.AreEqual(Visibility.Visible, assistantIdentity.Visibility);
        Assert.AreEqual(Visibility.Collapsed, userIdentity.Visibility);

        AssertRoleColors(
            assistant,
            "GgufChatAssistantBubbleBrush",
            "GgufChatAssistantBubbleTextBrush");
        AssertRoleColors(
            user,
            "GgufChatUserBubbleBrush",
            "GgufChatUserBubbleTextBrush");

        Assert.AreEqual(
            Windows.UI.Color.FromArgb(255, 238, 242, 247),
            Assert.IsInstanceOfType<SolidColorBrush>(
                LightChatResources()["GgufChatUserBubbleBrush"]).Color);
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                LightChatResources()["GgufChatTextBrush"]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(
                LightChatResources()["GgufChatUserBubbleTextBrush"]).Color);
        Assert.AreEqual(
            Microsoft.UI.Colors.Transparent,
            Assert.IsInstanceOfType<SolidColorBrush>(
                LightChatResources()["GgufChatAssistantBubbleBrush"]).Color);
        Assert.AreEqual(
            Windows.UI.Color.FromArgb(255, 17, 24, 39),
            Assert.IsInstanceOfType<SolidColorBrush>(
                LightChatResources()["GgufChatAssistantBubbleTextBrush"]).Color);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MessageTextIsSelectableAndCopyRaisesExactVisibleContent()
    {
        var bubble = new ChatMessageBubble
        {
            MessageContent = "Line one\r\nLine two",
            IsUser = false,
        };
        RichTextBlock text = Assert.IsInstanceOfType<RichTextBlock>(
            bubble.FindName("MessageText"));
        Button copy = Assert.IsInstanceOfType<Button>(
            bubble.FindName("CopyMessageButton"));
        string? requested = null;
        bubble.CopyRequested += (_, content) => requested = content;

        Assert.IsTrue(text.IsTextSelectionEnabled);
        Assert.AreEqual("Copy message", AutomationProperties.GetName(copy));
        Invoke(copy);
        Assert.AreEqual("Line one\r\nLine two", requested);

        bubble.ShowCopyResult(succeeded: true);
        Assert.AreEqual("Copied", AutomationProperties.GetName(copy));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MessageCopyFeedbackResetsAcrossUnloadAndReload()
    {
        var bubble = new ChatMessageBubble { MessageContent = "Copy me" };
        var root = new Grid();
        root.Children.Add(bubble);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(root, 480, 240);
        Button copy = Assert.IsInstanceOfType<Button>(
            bubble.FindName("CopyMessageButton"));
        bubble.ShowCopyResult(succeeded: true);
        Assert.AreEqual("Copied", AutomationProperties.GetName(copy));

        var unloaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bubble.Unloaded += (_, _) => unloaded.TrySetResult(true);
        root.Children.Remove(bubble);
        await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        root.Children.Add(bubble);
        await host.CaptureAsync();

        Assert.AreEqual("Copy message", AutomationProperties.GetName(copy));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SidebarActionsAndHistoryUseGhostNavigationRows()
    {
        var page = new ChatPage();
        Style expected = Assert.IsInstanceOfType<Style>(
            Application.Current.Resources["GgufChatNavigationButtonStyle"]);
        foreach (string name in new[] { "NewChatButton", "ImportModelButton", "SettingsButton" })
        {
            Button action = Assert.IsInstanceOfType<Button>(page.FindName(name));
            Assert.AreSame(expected, action.Style, name);
            Assert.AreEqual(0, action.BorderThickness.Left, name);
        }

        Assert.AreEqual(
            HorizontalAlignment.Left,
            Assert.IsInstanceOfType<Button>(page.FindName("NewChatButton")).HorizontalContentAlignment);
        Assert.AreEqual(
            HorizontalAlignment.Left,
            Assert.IsInstanceOfType<Button>(page.FindName("ImportModelButton")).HorizontalContentAlignment);
        Assert.AreEqual(
            HorizontalAlignment.Left,
            Assert.IsInstanceOfType<Button>(page.FindName("SettingsButton")).HorizontalContentAlignment);
        foreach (string name in new[] { "NewChatButton", "ImportModelButton", "SettingsButton" })
        {
            Button action = Assert.IsInstanceOfType<Button>(page.FindName(name));
            Assert.AreEqual(new Thickness(4, 8, 4, 8), action.Padding, name);
        }

        var historyItem = new ChatHistoryItem();
        Button historyButton = Assert.IsInstanceOfType<Button>(
            historyItem.FindName("HistoryButton"));
        Assert.AreSame(expected, historyButton.Style);
        Assert.AreEqual(
            HorizontalAlignment.Left,
            historyButton.HorizontalContentAlignment);
        Assert.AreEqual(new Thickness(12, 8, 12, 8), historyButton.Padding);
        StackPanel historyContent = Assert.IsInstanceOfType<StackPanel>(
            historyItem.FindName("HistoryContentGrid"));
        TextBlock historyTitle = Assert.IsInstanceOfType<TextBlock>(
            historyItem.FindName("TitleText"));
        Assert.AreEqual(3d, historyContent.Spacing);
        Assert.AreEqual(Orientation.Vertical, historyContent.Orientation);
        Assert.AreSame(historyItem.FindName("DateText"), historyContent.Children[0]);
        Assert.AreSame(historyTitle, historyContent.Children[1]);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RestrainedDesktopShellUsesCompactRailAndPanelGeometry()
    {
        var page = new ChatPage();
        ColumnDefinition historyColumn = Assert.IsInstanceOfType<ColumnDefinition>(
            page.FindName("HistoryColumn"));
        Border panel = Assert.IsInstanceOfType<Border>(
            page.FindName("ConversationPanel"));

        Assert.AreEqual(new GridLength(0), historyColumn.Width);
        Assert.AreEqual(new CornerRadius(0), panel.CornerRadius);
        Assert.AreEqual(new Thickness(0), panel.Padding);
        Assert.AreEqual(new Thickness(0), panel.BorderThickness);
        Assert.AreEqual(0, panel.Translation.Z);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void HistoryScrollsInsideItsRegionAndSettingsHasASeparatedFooter()
    {
        var page = new ChatPage();
        Grid historyRegion = Assert.IsInstanceOfType<Grid>(
            page.FindName("HistoryRegion"));
        Border settingsFooter = Assert.IsInstanceOfType<Border>(
            page.FindName("SettingsFooter"));
        ListView history = Assert.IsInstanceOfType<ListView>(
            page.FindName("ChatHistoryList"));

        Assert.AreEqual(2, historyRegion.RowDefinitions.Count);
        Assert.AreEqual(
            GridUnitType.Auto,
            historyRegion.RowDefinitions[0].Height.GridUnitType);
        Assert.AreEqual(
            GridUnitType.Star,
            historyRegion.RowDefinitions[1].Height.GridUnitType);
        Assert.AreEqual(1, Grid.GetRow(history));
        Assert.AreEqual(new Thickness(0, 12, 0, 0), settingsFooter.Margin);
        Assert.AreEqual(new Thickness(0, 1, 0, 0), settingsFooter.BorderThickness);
        Assert.AreEqual(new Thickness(0, 12, 0, 0), settingsFooter.Padding);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void TranscriptTurnsKeepResponsiveVerticalSeparation()
    {
        var page = new ChatPage();
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        Assert.IsNotNull(transcript.ItemContainerStyle);
        Style containerStyle = transcript.ItemContainerStyle;
        Setter marginSetter = containerStyle.Setters
            .OfType<Setter>()
            .Single(setter => setter.Property == FrameworkElement.MarginProperty);

        Assert.AreEqual(new Thickness(0, 0, 0, 12), marginSetter.Value);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void StreamingContentUpdatesTheExistingAssistantBubbleInPlace()
    {
        var page = new ChatPage();
        Guid conversationId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatMessage[] initialMessages =
        [
            new(userId, ChatMessageRole.User, "Hello", ChatCompletionStatus.Completed, now),
            new(assistantId, ChatMessageRole.Assistant, "Hello ", ChatCompletionStatus.Streaming, now)
        ];
        ChatMessage[] updatedMessages =
        [
            initialMessages[0],
            new(assistantId, ChatMessageRole.Assistant, "Hello there", ChatCompletionStatus.Streaming, now)
        ];

        page.SynchronizeTranscript(conversationId, initialMessages, forceFollowLatest: false);
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ChatMessageBubble firstAssistant = Assert.IsInstanceOfType<ChatMessageBubble>(
            transcript.Items[1]);

        page.SynchronizeTranscript(conversationId, updatedMessages, forceFollowLatest: false);

        Assert.AreSame(firstAssistant, transcript.Items[1]);
        Assert.AreEqual("Hello there", firstAssistant.MessageContent);
        Assert.AreEqual("Generating…", firstAssistant.StatusText);
        Assert.AreEqual(2, transcript.Items.Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RepeatedStreamingSynchronizationKeepsTranscriptVisible()
    {
        var page = new ChatPage();
        Guid conversationId = Guid.NewGuid();
        Guid assistantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var first = new ChatMessage(
            assistantId,
            ChatMessageRole.Assistant,
            "First chunk",
            ChatCompletionStatus.Streaming,
            now);
        var second = new ChatMessage(
            assistantId,
            ChatMessageRole.Assistant,
            "First chunk and second chunk",
            ChatCompletionStatus.Streaming,
            now);

        page.SynchronizeTranscript(conversationId, [first], forceFollowLatest: true);
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        object bubble = transcript.Items[0];
        page.SynchronizeTranscript(conversationId, [second], forceFollowLatest: true);

        Assert.AreEqual(Visibility.Visible, transcript.Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            Assert.IsInstanceOfType<FrameworkElement>(
                page.FindName("EmptyConversationState")).Visibility);
        Assert.AreSame(bubble, transcript.Items[0]);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CopyChatWritesOnlyTheOpenConversationInApprovedFormat()
    {
        var clipboard = new RecordingClipboard();
        var page = new ChatPage(clipboard);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [
                ChatMessage.User("Question", now),
                ChatMessage.Assistant(
                    "Partial",
                    ChatCompletionStatus.Streaming,
                    now),
            ],
            forceFollowLatest: false);
        Button copy = Assert.IsInstanceOfType<Button>(
            page.FindName("CopyChatButton"));

        Invoke(copy);

        Assert.AreEqual(
            $"You:{Environment.NewLine}Question{Environment.NewLine}" +
            $"{Environment.NewLine}Granite Edge AI:{Environment.NewLine}Partial",
            clipboard.Text);
        Assert.AreEqual("Copied", AutomationProperties.GetName(copy));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CopyChatFeedbackResetsAcrossUnloadAndReload()
    {
        var page = new ChatPage(new RecordingClipboard());
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [ChatMessage.User("Question", DateTimeOffset.UtcNow)],
            forceFollowLatest: false);
        var root = new Grid();
        root.Children.Add(page);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(root, 900, 520);
        Button copy = Assert.IsInstanceOfType<Button>(
            page.FindName("CopyChatButton"));
        Invoke(copy);
        Assert.AreEqual("Copied", AutomationProperties.GetName(copy));

        var unloaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Unloaded += (_, _) => unloaded.TrySetResult(true);
        root.Children.Remove(page);
        await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        root.Children.Add(page);
        await host.CaptureAsync();

        Assert.AreEqual("Copy chat", AutomationProperties.GetName(copy));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CopyMessageWritesOnlyThatMessagesVisibleContent()
    {
        var clipboard = new RecordingClipboard();
        var page = new ChatPage(clipboard);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [ChatMessage.User("Only this message", now)],
            forceFollowLatest: false);
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ChatMessageBubble bubble = Assert.IsInstanceOfType<ChatMessageBubble>(
            transcript.Items[0]);
        Button copy = Assert.IsInstanceOfType<Button>(
            bubble.FindName("CopyMessageButton"));

        Invoke(copy);

        Assert.AreEqual("Only this message", clipboard.Text);
        Assert.AreEqual("Copied", AutomationProperties.GetName(copy));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CompletedAssistantDoesNotOfferContinuation()
    {
        var page = new ChatPage();
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [ChatMessage.Assistant(
                "Complete",
                ChatCompletionStatus.Completed,
                DateTimeOffset.UtcNow)],
            forceFollowLatest: false);
        ChatMessageBubble bubble = Assert.IsInstanceOfType<ChatMessageBubble>(
            Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList")).Items[0]);

        Button continuation = Assert.IsInstanceOfType<Button>(
            bubble.FindName("ContinueButton"));

        Assert.AreEqual(Visibility.Collapsed, continuation.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ContinuationRaisesTheEligibleAssistantIdOnce()
    {
        var page = new ChatPage();
        ChatMessage assistant = ChatMessage.Assistant(
            "Partial",
            ChatCompletionStatus.LimitReached,
            DateTimeOffset.UtcNow);
        Guid? requested = null;
        int requests = 0;
        page.ContinuationRequested += (_, id) =>
        {
            requested = id;
            requests++;
        };
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [assistant],
            forceFollowLatest: false);
        ChatMessageBubble bubble = Assert.IsInstanceOfType<ChatMessageBubble>(
            Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList")).Items[0]);

        Invoke(Assert.IsInstanceOfType<Button>(bubble.FindName("ContinueButton")));

        Assert.AreEqual(1, requests);
        Assert.AreEqual(assistant.Id, requested);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CopyingStreamingPartialDoesNotRaiseStopOrContinuation()
    {
        var clipboard = new RecordingClipboard();
        var page = new ChatPage(clipboard);
        int stops = 0;
        int continuations = 0;
        page.StopRequested += (_, _) => stops++;
        page.ContinuationRequested += (_, _) => continuations++;
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [ChatMessage.Assistant(
                "Partial answer",
                ChatCompletionStatus.Streaming,
                DateTimeOffset.UtcNow)],
            forceFollowLatest: true);
        ChatMessageBubble bubble = Assert.IsInstanceOfType<ChatMessageBubble>(
            Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList")).Items[0]);

        Invoke(Assert.IsInstanceOfType<Button>(bubble.FindName("CopyMessageButton")));

        Assert.AreEqual(0, stops);
        Assert.AreEqual(0, continuations);
        Assert.AreEqual("Partial answer", clipboard.Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ClipboardFailureProvidesFeedbackWithoutChangingTranscript()
    {
        var clipboard = new RecordingClipboard(succeeds: false);
        var page = new ChatPage(clipboard);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [ChatMessage.User("Still visible", now)],
            forceFollowLatest: false);
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ChatMessageBubble bubble = Assert.IsInstanceOfType<ChatMessageBubble>(
            transcript.Items[0]);
        Button copyMessage = Assert.IsInstanceOfType<Button>(
            bubble.FindName("CopyMessageButton"));
        Button copyChat = Assert.IsInstanceOfType<Button>(
            page.FindName("CopyChatButton"));

        Invoke(copyMessage);
        Invoke(copyChat);

        Assert.AreEqual("Couldn't copy", AutomationProperties.GetName(copyMessage));
        Assert.AreEqual("Couldn't copy", AutomationProperties.GetName(copyChat));
        Assert.AreEqual("Still visible", bubble.MessageContent);
        Assert.AreSame(bubble, transcript.Items[0]);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CopyChatIsDisabledWhenTheOpenConversationHasNoVisibleContent()
    {
        var page = new ChatPage(new RecordingClipboard());
        Button copy = Assert.IsInstanceOfType<Button>(
            page.FindName("CopyChatButton"));

        Assert.IsFalse(copy.IsEnabled);
        page.SynchronizeTranscript(Guid.NewGuid(), [], forceFollowLatest: false);
        Assert.IsFalse(copy.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("DeferredChatAutoScroll")]
    public async Task DeferredFollowScrollsOverflowAfterLayout()
    {
        var page = new ChatPage();
        Guid conversationId = Guid.NewGuid();
        ChatMessage[] messages = CreateLongMessages();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 520);

        page.SynchronizeTranscript(conversationId, messages, forceFollowLatest: true);
        await host.CaptureAsync();
        await host.CaptureAsync();
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ScrollViewer? scrollViewerCandidate = FindDescendant<ScrollViewer>(transcript);
        Assert.IsNotNull(scrollViewerCandidate);
        ScrollViewer scrollViewer = scrollViewerCandidate;
        await WaitForFollowAsync(scrollViewer);
        Assert.IsGreaterThan(0, scrollViewer.ScrollableHeight);
        Assert.IsLessThanOrEqualTo(
            1,
            scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PendingFollowDoesNotOverrideManualScrollAway()
    {
        var page = new ChatPage();
        Guid conversationId = Guid.NewGuid();
        ChatMessage[] messages = CreateLongMessages();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 520);

        page.SynchronizeTranscript(conversationId, messages, forceFollowLatest: false);
        await host.CaptureAsync();
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ScrollViewer? scrollViewerCandidate = FindDescendant<ScrollViewer>(transcript);
        Assert.IsNotNull(scrollViewerCandidate);
        ScrollViewer scrollViewer = scrollViewerCandidate;
        Assert.IsGreaterThan(0, scrollViewer.ScrollableHeight);
        scrollViewer.ChangeView(
            horizontalOffset: null,
            verticalOffset: scrollViewer.ScrollableHeight,
            zoomFactor: null,
            disableAnimation: true);
        await host.CaptureAsync();
        Assert.IsLessThanOrEqualTo(
            1,
            scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset);

        page.SynchronizeTranscript(conversationId, messages, forceFollowLatest: true);
        Assert.IsTrue(scrollViewer.ChangeView(
            horizontalOffset: null,
            verticalOffset: 0,
            zoomFactor: null,
            disableAnimation: true));
        await host.CaptureAsync();

        Assert.IsLessThanOrEqualTo(1, scrollViewer.VerticalOffset);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("DeferredChatAutoScroll")]
    public async Task SwitchingOverflowingConversationFollowsTheNewTranscript()
    {
        var page = new ChatPage();
        ChatMessage[] firstMessages = CreateLongMessages();
        ChatMessage[] secondMessages = CreateLongMessages();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 520);

        page.SynchronizeTranscript(Guid.NewGuid(), firstMessages, forceFollowLatest: true);
        await host.CaptureAsync();
        await host.CaptureAsync();
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ScrollViewer? scrollViewerCandidate = FindDescendant<ScrollViewer>(transcript);
        Assert.IsNotNull(scrollViewerCandidate);
        ScrollViewer scrollViewer = scrollViewerCandidate;
        await WaitForFollowAsync(scrollViewer);
        Assert.IsLessThanOrEqualTo(
            1,
            scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset);

        page.SynchronizeTranscript(Guid.NewGuid(), secondMessages, forceFollowLatest: true);
        await host.CaptureAsync();
        await host.CaptureAsync();
        await WaitForFollowAsync(scrollViewer);

        Assert.IsGreaterThan(0, scrollViewer.ScrollableHeight);
        Assert.IsLessThanOrEqualTo(
            1,
            scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectingAnotherConversationResetsTranscriptAndEmptyState()
    {
        var page = new ChatPage();
        var first = new ChatMessage(
            Guid.NewGuid(),
            ChatMessageRole.User,
            "First",
            ChatCompletionStatus.Completed,
            DateTimeOffset.UtcNow);
        var second = new ChatMessage(
            Guid.NewGuid(),
            ChatMessageRole.User,
            "Second",
            ChatCompletionStatus.Completed,
            DateTimeOffset.UtcNow);
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));

        page.SynchronizeTranscript(Guid.NewGuid(), [first], forceFollowLatest: false);
        object firstBubble = transcript.Items[0];
        page.SynchronizeTranscript(Guid.NewGuid(), [second], forceFollowLatest: false);

        Assert.AreEqual(1, transcript.Items.Count);
        Assert.AreNotSame(firstBubble, transcript.Items[0]);
        Assert.AreEqual(
            "Second",
            Assert.IsInstanceOfType<ChatMessageBubble>(transcript.Items[0]).MessageContent);

        page.SynchronizeTranscript(Guid.NewGuid(), [], forceFollowLatest: false);

        Assert.AreEqual(0, transcript.Items.Count);
        Assert.AreEqual(Visibility.Visible,
            Assert.IsInstanceOfType<FrameworkElement>(
                page.FindName("EmptyConversationState")).Visibility);
        Assert.AreEqual(Visibility.Collapsed, transcript.Visibility);
    }

    [TestMethod]
    public void OutputFollowsOnlyWhenViewportIsNearBottom()
    {
        Assert.IsTrue(ChatPage.ShouldFollowOutput(
            verticalOffset: 500,
            scrollableHeight: 520));
        Assert.IsTrue(ChatPage.ShouldFollowOutput(
            verticalOffset: 520,
            scrollableHeight: 520));
        Assert.IsFalse(ChatPage.ShouldFollowOutput(
            verticalOffset: 300,
            scrollableHeight: 520));
        Assert.IsTrue(ChatPage.ShouldFollowOutput(
            verticalOffset: 0,
            scrollableHeight: 0));
    }

    private static void AssertRoleColors(
        ChatMessageBubble bubble,
        string expectedSurfaceKey,
        string expectedTextKey)
    {
        Border border = Assert.IsInstanceOfType<Border>(bubble.FindName("BubbleBorder"));
        RichTextBlock text = Assert.IsInstanceOfType<RichTextBlock>(bubble.FindName("MessageText"));
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources[expectedSurfaceKey]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(border.Background).Color);
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources[expectedTextKey]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(text.Foreground).Color);
    }

    private static ResourceDictionary LightChatResources()
    {
        ResourceDictionary chatTheme = Application.Current.Resources.MergedDictionaries
            .Single(dictionary =>
                dictionary.ThemeDictionaries.ContainsKey("Light") &&
                ((ResourceDictionary)dictionary.ThemeDictionaries["Light"])
                    .ContainsKey("GgufChatUserBubbleBrush"));
        return (ResourceDictionary)chatTheme.ThemeDictionaries["Light"];
    }

    private static ChatMessage[] CreateLongMessages()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return Enumerable.Range(0, 30)
            .Select(index => new ChatMessage(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                $"Message {index}: {new string('x', 180)}",
                ChatCompletionStatus.Completed,
                now.AddSeconds(index)))
            .ToArray();
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

    private static async Task WaitForFollowAsync(ScrollViewer scrollViewer)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset > 1
            && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    private static void Invoke(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        var provider = Assert.IsInstanceOfType<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke));
        provider.Invoke();
    }

    private sealed class RecordingClipboard(bool succeeds = true) : IChatClipboard
    {
        internal string? Text { get; private set; }

        public bool TrySetText(string text)
        {
            Text = text;
            return succeeds;
        }
    }
}
