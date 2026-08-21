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
            HorizontalAlignment.Center,
            Assert.IsInstanceOfType<Button>(page.FindName("NewChatButton")).HorizontalContentAlignment);
        Assert.AreEqual(
            HorizontalAlignment.Center,
            Assert.IsInstanceOfType<Button>(page.FindName("ImportModelButton")).HorizontalContentAlignment);
        Assert.AreEqual(
            HorizontalAlignment.Left,
            Assert.IsInstanceOfType<Button>(page.FindName("SettingsButton")).HorizontalContentAlignment);

        var historyItem = new ChatHistoryItem();
        Button historyButton = Assert.IsInstanceOfType<Button>(
            historyItem.FindName("HistoryButton"));
        Assert.AreSame(expected, historyButton.Style);
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
