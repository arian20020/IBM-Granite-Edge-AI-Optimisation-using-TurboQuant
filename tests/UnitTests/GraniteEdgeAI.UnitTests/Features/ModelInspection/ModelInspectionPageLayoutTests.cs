using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.Foundation;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionPageLayoutTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExpandedReadyDisclosure_UsesFeatureOwnedVerticalScrollExtent(
        bool useOpenVinoPreview)
    {
        var page = new ModelInspectionPage();
        ScrollViewer scrollViewer = Assert.IsInstanceOfType<ScrollViewer>(
            page.FindName("InspectionScrollViewer"));
        ContentControl previewHost = Assert.IsInstanceOfType<ContentControl>(
            page.FindName("InspectionPreviewHost"));
        StackPanel contentStack = Assert.IsInstanceOfType<StackPanel>(
            page.FindName("InspectionContentStack"));
        UserControl preview = useOpenVinoPreview
            ? new ModelInspectionOpenVinoPreviewView()
            : Assert.IsInstanceOfType<ModelInspectionGgufPreviewView>(
                previewHost.Content);
        previewHost.Content = preview;
        FrameworkElement progressPanel = Assert.IsInstanceOfType<FrameworkElement>(
            preview.FindName("InspectionProgressPanel"));
        FrameworkElement readyPanel = Assert.IsInstanceOfType<FrameworkElement>(
            preview.FindName("InspectionReadyPanel"));
        Expander disclosure = Assert.IsInstanceOfType<Expander>(
            preview.FindName("ReadyDetailsExpander"));
        progressPanel.Visibility = Visibility.Collapsed;
        readyPanel.Visibility = Visibility.Visible;
        disclosure.IsExpanded = true;

        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            double scale = page.XamlRoot.RasterizationScale;
            window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                (int)Math.Round(760d * scale),
                (int)Math.Round(420d * scale)));

            await WaitForLayoutAsync(page, () =>
                disclosure.ActualHeight > 0d &&
                scrollViewer.ViewportHeight > 0d &&
                scrollViewer.ScrollableHeight > 0d);

            Assert.AreEqual(ScrollMode.Disabled, scrollViewer.HorizontalScrollMode);
            Assert.AreEqual(
                ScrollBarVisibility.Disabled,
                scrollViewer.HorizontalScrollBarVisibility);
            Assert.AreEqual(ScrollMode.Enabled, scrollViewer.VerticalScrollMode);
            Assert.AreEqual(
                ScrollBarVisibility.Auto,
                scrollViewer.VerticalScrollBarVisibility);
            Assert.AreEqual(ZoomMode.Disabled, scrollViewer.ZoomMode);
            Assert.AreEqual(
                VerticalAlignment.Top,
                scrollViewer.VerticalContentAlignment);
            SolidColorBrush background = Assert.IsInstanceOfType<SolidColorBrush>(
                scrollViewer.Background);
            Assert.AreEqual(0, background.Color.A);
            Assert.AreEqual(HorizontalAlignment.Center,
                contentStack.HorizontalAlignment);
            Point stackOrigin = contentStack
                .TransformToVisual(scrollViewer)
                .TransformPoint(new Point());
            Assert.AreEqual(
                scrollViewer.ViewportWidth / 2d,
                stackOrigin.X + contentStack.ActualWidth / 2d,
                0.5,
                "The inspection content must remain centred in the viewport.");
            Assert.IsGreaterThan(
                scrollViewer.ViewportHeight,
                scrollViewer.ExtentHeight,
                "The expanded disclosure must contribute to the page-owned extent.");
            Assert.IsGreaterThan(
                0d,
                scrollViewer.ScrollableHeight,
                $"A constrained loaded {(useOpenVinoPreview ? "OpenVINO" : "GGUF")} " +
                "page must be able to reach expanded disclosure content.");

            Assert.IsTrue(scrollViewer.ChangeView(
                horizontalOffset: null,
                verticalOffset: scrollViewer.ScrollableHeight,
                zoomFactor: null,
                disableAnimation: true));
            await WaitForLayoutAsync(page, () => scrollViewer.VerticalOffset > 0d);
            Assert.IsGreaterThan(0d, scrollViewer.VerticalOffset);
            Assert.AreEqual(
                scrollViewer.ScrollableHeight,
                scrollViewer.VerticalOffset,
                0.5,
                "The bottom of the page-owned extent must be reachable.");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Desktop1000_UsesApprovedCenteredGeometry()
    {
        var page = new ModelInspectionPage();
        StackPanel contentHost = Assert.IsInstanceOfType<StackPanel>(
            page.FindName("InspectionContentStack"));
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ResizeClientAndWaitAsync(window, page, contentHost, 1000, 700, 24);
            StackPanel header = Assert.IsInstanceOfType<StackPanel>(contentHost.Children[0]);
            TextBlock title = Assert.IsInstanceOfType<TextBlock>(header.Children[0]);
            TextBlock subtitle = Assert.IsInstanceOfType<TextBlock>(header.Children[1]);
            Point origin = contentHost.TransformToVisual(page).TransformPoint(new Point());
            Assert.IsGreaterThan(0d, contentHost.ActualWidth);
            Assert.IsTrue(contentHost.ActualWidth <= 952d + 1d);
            Assert.AreEqual(500d, origin.X + contentHost.ActualWidth / 2d, 1d);
            Assert.AreEqual(32d, title.FontSize, 0.01d);
            Assert.AreEqual(14d, subtitle.FontSize, 0.01d);
            Assert.AreEqual(TextAlignment.Center, title.TextAlignment);
            Assert.AreEqual(TextAlignment.Center, subtitle.TextAlignment);
            Assert.AreEqual("Model inspection", title.Text);
            Assert.AreEqual(960d, contentHost.MaxWidth);
            Assert.AreEqual(ElementTheme.Default, page.RequestedTheme,
                "The current shell inherits the application's theme.");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ResponsiveWidths_UseApprovedMarginsAndPreserveStableTree()
    {
        var page = new ModelInspectionPage();
        var scroll = Assert.IsInstanceOfType<ScrollViewer>(page.FindName("InspectionScrollViewer"));
        var stack = Assert.IsInstanceOfType<StackPanel>(page.FindName("InspectionContentStack"));
        var previewHost = Assert.IsInstanceOfType<ContentControl>(page.FindName("InspectionPreviewHost"));
        var preview = Assert.IsInstanceOfType<ModelInspectionGgufPreviewView>(previewHost.Content);
        var bay = Assert.IsInstanceOfType<FrameworkElement>(preview.FindName("InspectionBay"));
        var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var endpoint in new[] { (1100d, 24d), (1008d, 24d), (1007d, 24d),
                (640d, 24d), (639d, 16d), (480d, 16d) })
            {
                await ResizeClientAndWaitAsync(window, page, stack,
                    endpoint.Item1, 700, endpoint.Item2);
                Assert.AreSame(scroll, page.FindName("InspectionScrollViewer"));
                Assert.AreSame(preview, previewHost.Content);
                Assert.AreSame(bay, preview.FindName("InspectionBay"));
                Assert.IsGreaterThan(0d, bay.ActualHeight);
                Assert.AreEqual(stack.ActualWidth, previewHost.ActualWidth, 1d);
                Assert.AreEqual(stack.ActualWidth, preview.ActualWidth, 1d);
                Assert.AreEqual(0d, scroll.ScrollableWidth, 1d);
            }
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ResponsiveStates_DeclareExactClientBreakpointsAndInsets()
    {
        var page = new ModelInspectionPage();
        var root = Assert.IsInstanceOfType<Grid>(page.FindName("LayoutRoot"));
        var scroll = Assert.IsInstanceOfType<ScrollViewer>(page.FindName("InspectionScrollViewer"));
        var grid = Assert.IsInstanceOfType<Grid>(scroll.Content);
        var stack = Assert.IsInstanceOfType<StackPanel>(page.FindName("InspectionContentStack"));
        var host = Assert.IsInstanceOfType<ContentControl>(page.FindName("InspectionPreviewHost"));
        Assert.AreSame(stack, grid.Children.Single());
        Assert.AreSame(host, stack.Children[1]);
        Assert.AreEqual(2, stack.Children.Count);
        Assert.AreEqual(960d, stack.MaxWidth);
        Assert.AreEqual(16d, stack.Spacing);
        Assert.AreEqual(HorizontalAlignment.Center, stack.HorizontalAlignment);
        Assert.AreEqual(ScrollMode.Enabled, scroll.VerticalScrollMode);
        Assert.AreEqual(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.AreEqual(ScrollMode.Disabled, scroll.HorizontalScrollMode);
        Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.AreEqual(ZoomMode.Disabled, scroll.ZoomMode);
        Assert.IsFalse(Descendants(host).OfType<ScrollViewer>().Any());
        AssertResponsiveStateContract(root, "InspectionPageWide", 640, 24);
        AssertResponsiveStateContract(root, "InspectionPageCompact", 0, 16);
    }

    private static void AssertResponsiveStateContract(
        FrameworkElement root,
        string stateName,
        double minimumWidth,
        double inset)
    {
        VisualStateGroup group = VisualStateManager
            .GetVisualStateGroups(root)
            .Single(candidate => candidate.Name == "InspectionPageResponsiveStates");
        VisualState state = group.States
            .Single(candidate => candidate.Name == stateName);
        AdaptiveTrigger trigger = Assert.IsInstanceOfType<AdaptiveTrigger>(
            state.StateTriggers.Single());

        Assert.AreEqual(minimumWidth, trigger.MinWindowWidth, 0.01);
        Setter marginSetter = state.Setters
            .OfType<Setter>()
            .Single(setter => setter.Target?.Path.Path == "Margin");
        Thickness margin = Assert.IsInstanceOfType<Thickness>(marginSetter.Value);
        Assert.AreEqual(inset, margin.Left, 0.01);
        Assert.AreEqual(inset, margin.Right, 0.01);
    }

    private static async Task ResizeClientAndWaitAsync(
        Window window,
        FrameworkElement page,
        FrameworkElement contentHost,
        double effectiveWidth,
        double effectiveHeight,
        double expectedInset)
    {
        var layoutReached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteWhenReady(object? sender, object eventArguments)
        {
            double maximumHostWidth = Math.Min(
                960d,
                effectiveWidth - (2d * expectedInset));
            double expectedOrigin =
                (effectiveWidth - contentHost.ActualWidth) / 2d;
            Point contentOrigin = contentHost
                .TransformToVisual(page)
                .TransformPoint(new Point());
            if (Math.Abs(page.ActualWidth - effectiveWidth) <= 1d &&
                Math.Abs(page.ActualHeight - effectiveHeight) <= 1d &&
                Math.Abs(contentHost.Margin.Left - expectedInset) <= 0.01 &&
                contentHost.ActualWidth > 0d &&
                contentHost.ActualWidth <= maximumHostWidth + 1d &&
                Math.Abs(contentOrigin.X - expectedOrigin) <= 1d)
            {
                layoutReached.TrySetResult(true);
            }
        }

        page.LayoutUpdated += CompleteWhenReady;
        try
        {
            double scale = page.XamlRoot.RasterizationScale;
            window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                (int)Math.Round(effectiveWidth * scale),
                (int)Math.Round(effectiveHeight * scale)));
            CompleteWhenReady(null, EventArgs.Empty);
            await layoutReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            page.UpdateLayout();
        }
        finally
        {
            page.LayoutUpdated -= CompleteWhenReady;
        }
    }

    private static async Task WaitForLayoutAsync(
        FrameworkElement element,
        Func<bool> predicate)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            element.UpdateLayout();
            if (predicate())
            {
                return;
            }

            var drained = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
                () => drained.TrySetResult(true)));
            await drained.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Fail("The expanded disclosure did not create a scrollable extent.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void HeaderAndShell_AllowNaturalHeightAtSimulated200PercentTextScale()
    {
        var page = new ModelInspectionPage();
        Arrange(page, 520, 400);
        TextBlock[] headerText = Descendants(page)
            .OfType<TextBlock>()
            .Where(text =>
                text.Text == "Model inspection" ||
                text.Text.StartsWith(
                    "We are checking that the model package",
                    StringComparison.Ordinal))
            .ToArray();
        ScrollViewer scrollViewer = Assert.IsInstanceOfType<ScrollViewer>(
            page.FindName("InspectionScrollViewer"));

        Assert.HasCount(2, headerText);
        Assert.IsTrue(headerText.All(text => text.MaxLines == 0));
        Assert.IsTrue(headerText.All(text => text.IsTextScaleFactorEnabled));
        Assert.AreEqual(ScrollMode.Enabled, scrollViewer.VerticalScrollMode);
        Assert.AreEqual(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);

        double naturalHeaderHeight = Assert.IsInstanceOfType<FrameworkElement>(
            Assert.IsInstanceOfType<StackPanel>(page.FindName("InspectionContentStack")).Children[0]).ActualHeight;
        foreach (TextBlock text in headerText)
        {
            text.FontSize *= 2d;
        }

        Arrange(page, 360, 400);

        FrameworkElement scaledHeader = Assert.IsInstanceOfType<FrameworkElement>(
            Assert.IsInstanceOfType<StackPanel>(page.FindName("InspectionContentStack")).Children[0]);
        Assert.IsTrue(
            scaledHeader.ActualHeight > naturalHeaderHeight * 1.5d,
            "the header must grow naturally at an effective 200% text size");
        Assert.IsTrue(headerText.All(text =>
            text.ActualHeight + 1d >= text.DesiredSize.Height));
    }

    private static void Arrange(
        FrameworkElement element,
        double width,
        double height)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(
        DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
