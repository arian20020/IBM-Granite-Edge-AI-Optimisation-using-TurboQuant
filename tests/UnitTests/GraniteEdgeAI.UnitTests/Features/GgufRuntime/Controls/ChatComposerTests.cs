using GraniteEdgeAI.Features.GgufRuntime.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls;

[TestClass]
public sealed class ChatComposerTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void StopSquareHasItsOwnCenteredLayoutCell()
    {
        var composer = new ChatComposer { IsGenerating = true };
        Rectangle icon = Assert.IsInstanceOfType<Rectangle>(
            composer.FindName("StopSquare"));
        TextBlock label = Assert.IsInstanceOfType<TextBlock>(
            composer.FindName("StopLabel"));

        Assert.AreEqual(HorizontalAlignment.Center, icon.HorizontalAlignment);
        Assert.AreEqual(VerticalAlignment.Center, icon.VerticalAlignment);
        Assert.AreNotEqual(Grid.GetColumn(icon), Grid.GetColumn(label));
        Assert.AreEqual(Visibility.Visible,
            Assert.IsInstanceOfType<Button>(composer.FindName("StopButton")).Visibility);
    }
}
