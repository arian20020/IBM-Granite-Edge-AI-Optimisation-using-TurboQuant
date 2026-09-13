using System.Linq;
using System.Reflection;
using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationResponsiveLayoutTests
{
    [UITestMethod]
    public void PageDefinesCompactStandardAndWideLayouts()
    {
        OptimizationPage page = new();
        FrameworkElement root = (FrameworkElement)page.FindName("LayoutRoot");
        ScrollViewer scrollViewer =
            (ScrollViewer)page.FindName("OptimizationPageScrollViewer");
        FrameworkElement contentHost =
            (FrameworkElement)page.FindName("OptimizationContentHost");
        var stateNames = VisualStateManager.GetVisualStateGroups(root)
            .SelectMany(group => group.States)
            .Select(state => state.Name)
            .ToArray();

        CollectionAssert.IsSubsetOf(new[] { "CompactPageState", "StandardPageState", "WidePageState" }, stateNames);
        Assert.AreEqual(ScrollMode.Enabled, scrollViewer.VerticalScrollMode);
        Assert.AreEqual(HorizontalAlignment.Center, contentHost.HorizontalAlignment);
        Assert.AreEqual(HorizontalAlignment.Center, scrollViewer.HorizontalContentAlignment);
    }

    [UITestMethod]
    public void CompactLayoutGrowsNaturallyAtSimulatedTwoHundredPercentText()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(
            OptimizationFixtureCatalog.All.Single(item => item.Id == "confirmation").Presentation);
        Arrange(page, 360, 640);

        TextBlock[] visibleText = Descendants(page)
            .OfType<TextBlock>()
            .Where(text => text.Visibility == Visibility.Visible && text.ActualWidth > 0)
            .ToArray();
        TextBlock heading = (TextBlock)page.FindName("OptimizationConfirmingHeading");
        Assert.IsTrue(heading.IsTextScaleFactorEnabled);
        Assert.AreEqual(TextWrapping.WrapWholeWords, heading.TextWrapping);
        foreach (TextBlock text in visibleText)
        {
            text.FontSize *= 2;
        }

        Arrange(page, 360, 640);
        foreach (TextBlock text in visibleText)
        {
            Assert.IsTrue(
                text.ActualHeight + 1 >= text.DesiredSize.Height,
                $"'{text.Text}' was {text.ActualHeight} high but requested {text.DesiredSize.Height}.");
        }
        Assert.AreEqual(
            ScrollBarVisibility.Auto,
            ((ScrollViewer)page.FindName("OptimizationPageScrollViewer")).VerticalScrollBarVisibility);
    }

    [UITestMethod]
    public void SuccessStacksAllThreeActionsAtCompactWidth()
    {
        foreach (string id in new[]
                 {
                     "success-persistent", "success-runtime-profile"
                 })
        {
            OptimizationPage page = new();
            page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
                item => item.Id == id).Presentation);

            typeof(OptimizationPage).GetMethod(
                "ApplyResponsiveLayout",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, new object[] { 600d });

            Assert.AreEqual(Orientation.Vertical,
                ((StackPanel)page.FindName("OptimizationTerminalActions")).Orientation,
                id);
            Assert.AreEqual(Orientation.Vertical,
                ((StackPanel)page.FindName("OptimizationTerminalForwardActions")).Orientation,
                id);
            Assert.AreEqual(Visibility.Visible,
                ((Button)page.FindName("BtnOptimizationTerminalBack")).Visibility,
                id);
            Assert.AreEqual(Visibility.Visible,
                ((Button)page.FindName("BtnOptimizationAlternative")).Visibility,
                id);
            Assert.AreEqual(Visibility.Visible,
                ((Button)page.FindName("BtnOptimizationPrimary")).Visibility,
                id);
        }
    }

    [UITestMethod]
    public void GgufRuntimeSuccessGrowsNaturallyAtSimulatedTwoHundredPercentText()
    {
        OptimizationPage page = new();
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-runtime-profile").Presentation);
        Arrange(page, 360, 640);

        TextBlock[] visibleText = Descendants(page)
            .OfType<TextBlock>()
            .Where(text => text.Visibility == Visibility.Visible
                && text.ActualWidth > 0)
            .ToArray();
        foreach (TextBlock text in visibleText)
        {
            text.FontSize *= 2;
        }

        Arrange(page, 360, 640);
        Assert.IsTrue(visibleText.All(text =>
            text.ActualHeight + 1 >= text.DesiredSize.Height));
        Assert.AreEqual("Save model and settings",
            ((Button)page.FindName("BtnOptimizationAlternative")).Content);
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
