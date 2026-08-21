using System.Reflection;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls;

[TestClass]
public sealed class ChatHistoryItemTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectedConversationShowsIndicatorAndSelectedSurface()
    {
        var item = new ChatHistoryItem();
        PropertyInfo? selectedProperty = typeof(ChatHistoryItem).GetProperty("IsSelected");
        Assert.IsNotNull(selectedProperty, "History items require an explicit selected state.");
        Border indicator = Assert.IsInstanceOfType<Border>(
            item.FindName("SelectionIndicator"));
        Button button = Assert.IsInstanceOfType<Button>(item.FindName("HistoryButton"));

        selectedProperty.SetValue(item, true);

        Assert.AreEqual(Visibility.Visible, indicator.Visibility);
        Assert.AreEqual(3, indicator.Width);
        Assert.AreSame(
            Application.Current.Resources["GgufChatNavigationSelectedBrush"],
            button.Background);
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatPrimaryBrush"]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(button.Background).Color);
        Assert.AreEqual(
            Assert.IsInstanceOfType<SolidColorBrush>(
                Application.Current.Resources["GgufChatPrimaryForegroundBrush"]).Color,
            Assert.IsInstanceOfType<SolidColorBrush>(button.Foreground).Color);

        selectedProperty.SetValue(item, false);

        Assert.AreEqual(Visibility.Collapsed, indicator.Visibility);
        Assert.AreSame(
            Application.Current.Resources["GgufChatNavigationRestBrush"],
            button.Background);
        Assert.AreSame(
            Application.Current.Resources["GgufChatTextBrush"],
            button.Foreground);
    }
}
