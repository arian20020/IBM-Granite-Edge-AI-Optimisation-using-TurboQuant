using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatPageTests
{
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
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatPrimaryBrush"]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatUserBubbleBrush"]).Color);
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatPrimaryForegroundBrush"]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatUserBubbleTextBrush"]).Color);
        Assert.AreEqual(
            Windows.UI.Color.FromArgb(255, 234, 242, 255),
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatAssistantBubbleBrush"]).Color);
        Assert.AreEqual(
            Windows.UI.Color.FromArgb(255, 16, 46, 107),
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatAssistantBubbleTextBrush"]).Color);
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
        Assert.AreEqual(new Thickness(4, 8, 4, 8), historyButton.Padding);
        Grid historyContent = Assert.IsInstanceOfType<Grid>(
            historyItem.FindName("HistoryContentGrid"));
        TextBlock historyTitle = Assert.IsInstanceOfType<TextBlock>(
            historyItem.FindName("TitleText"));
        Assert.AreEqual(12, historyContent.ColumnSpacing);
        Assert.AreEqual(new GridLength(20), historyContent.ColumnDefinitions[0].Width);
        Assert.AreEqual(new GridLength(1, GridUnitType.Star), historyContent.ColumnDefinitions[1].Width);
        Assert.AreEqual(1, Grid.GetColumn(historyTitle));
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

        Assert.AreEqual(new GridLength(256), historyColumn.Width);
        Assert.AreEqual(new CornerRadius(14), panel.CornerRadius);
        Assert.AreEqual(new Thickness(24), panel.Padding);
        Assert.AreEqual(new Thickness(1), panel.BorderThickness);
        Assert.AreEqual(2, panel.Translation.Z);
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
    public async Task DeferredFollowScrollsOverflowAfterLayout()
    {
        var page = new ChatPage();
        Guid conversationId = Guid.NewGuid();
        ChatMessage[] messages = CreateLongMessages();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 520);

        page.SynchronizeTranscript(conversationId, messages, forceFollowLatest: true);
        await host.CaptureAsync();
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ScrollViewer? scrollViewerCandidate = FindDescendant<ScrollViewer>(transcript);
        Assert.IsNotNull(scrollViewerCandidate);
        ScrollViewer scrollViewer = scrollViewerCandidate;
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
    public async Task SwitchingOverflowingConversationFollowsTheNewTranscript()
    {
        var page = new ChatPage();
        ChatMessage[] firstMessages = CreateLongMessages();
        ChatMessage[] secondMessages = CreateLongMessages();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 520);

        page.SynchronizeTranscript(Guid.NewGuid(), firstMessages, forceFollowLatest: true);
        await host.CaptureAsync();
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        ScrollViewer? scrollViewerCandidate = FindDescendant<ScrollViewer>(transcript);
        Assert.IsNotNull(scrollViewerCandidate);
        ScrollViewer scrollViewer = scrollViewerCandidate;
        Assert.IsLessThanOrEqualTo(
            1,
            scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset);

        page.SynchronizeTranscript(Guid.NewGuid(), secondMessages, forceFollowLatest: true);
        await host.CaptureAsync();

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
        TextBlock text = Assert.IsInstanceOfType<TextBlock>(bubble.FindName("MessageText"));
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources[expectedSurfaceKey]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(border.Background).Color);
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources[expectedTextKey]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(text.Foreground).Color);
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
}
