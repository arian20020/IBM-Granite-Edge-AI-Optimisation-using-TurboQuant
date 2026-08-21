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
    public void SelectedConversationUsesOneReadableBlueSurface()
    {
        var item = new ChatHistoryItem();
        PropertyInfo? selectedProperty = typeof(ChatHistoryItem).GetProperty("IsSelected");
        Assert.IsNotNull(selectedProperty, "History items require an explicit selected state.");
        Button button = Assert.IsInstanceOfType<Button>(item.FindName("HistoryButton"));

        Assert.IsNull(
            item.FindName("SelectionIndicator"),
            "A filled selected row must not also render a redundant accent bar.");

        selectedProperty.SetValue(item, true);

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

        Assert.AreSame(
            Application.Current.Resources["GgufChatNavigationRestBrush"],
            button.Background);
        Assert.AreSame(
            Application.Current.Resources["GgufChatTextBrush"],
            button.Foreground);
    }
}
