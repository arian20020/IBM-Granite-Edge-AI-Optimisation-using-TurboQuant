using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
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
}
