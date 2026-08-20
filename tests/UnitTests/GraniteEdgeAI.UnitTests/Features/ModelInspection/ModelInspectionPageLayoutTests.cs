using GraniteEdgeAI.Features.ModelInspection;
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
    public async Task Desktop1440_UsesApprovedCenteredGeometry()
    {
        var page = new ModelInspectionPage();
        ScrollViewer scrollViewer = Assert.IsInstanceOfType<ScrollViewer>(
            page.FindName("InspectionPageScrollViewer"));
        Grid scrollContent =
            Assert.IsInstanceOfType<Grid>(scrollViewer.Content);
        FrameworkElement contentHost = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("InspectionContentHost"));
        Assert.AreSame(contentHost, scrollContent.Children.Single());
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ResizeClientAndWaitAsync(
                window,
                page,
                contentHost,
                effectiveWidth: 1000,
                effectiveHeight: 720,
                expectedInset: 24);
            await ResizeClientAndWaitAsync(
                window,
                page,
                contentHost,
                effectiveWidth: 1440,
                effectiveHeight: 1024,
                expectedInset: 24);
            TextBlock title = Descendants(page)
                .OfType<TextBlock>()
                .Single(text => text.Text == "Model inspection");
            TextBlock subtitle = Descendants(page)
                .OfType<TextBlock>()
                .Single(text => text.Text.StartsWith(
                    "We are checking that the model package",
                    StringComparison.Ordinal));

            Point contentOrigin = contentHost
                .TransformToVisual(page)
                .TransformPoint(new Point());
            Assert.AreEqual(840d, contentHost.ActualWidth, 0.01, "desktop host width");
            Assert.AreEqual(300d, contentOrigin.X, 0.01, "desktop host left edge");
            Assert.AreEqual(32d, title.FontSize, 0.01, "page title size");
            Assert.AreEqual(14d, subtitle.FontSize, 0.01, "page subtitle size");
            Assert.AreEqual(TextAlignment.Center, title.TextAlignment);
            Assert.AreEqual(TextAlignment.Center, subtitle.TextAlignment);
            Assert.AreSame(
                Application.Current.Resources["InspectionPageTitleFontFamily"],
                title.FontFamily);
            Assert.AreSame(
                Application.Current.Resources["InspectionBodyFontFamily"],
                subtitle.FontFamily);
            Assert.AreEqual(ElementTheme.Light, page.RequestedTheme);
            Assert.AreEqual(ElementTheme.Light, page.ActualTheme);
            Assert.AreEqual(ElementTheme.Light, title.ActualTheme);
            Assert.AreEqual(ElementTheme.Light, subtitle.ActualTheme);
            Assert.AreSame(
                ThemeResource("Light", "InspectionTextPrimaryBrush"),
                title.Foreground);
            Assert.AreSame(
                ThemeResource("Light", "InspectionTextSecondaryMutedBrush"),
                subtitle.Foreground);
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
        ScrollViewer scrollViewer = Assert.IsInstanceOfType<ScrollViewer>(
            page.FindName("InspectionPageScrollViewer"));
        Grid scrollContent =
            Assert.IsInstanceOfType<Grid>(scrollViewer.Content);
        FrameworkElement contentHost = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("InspectionContentHost"));
        Assert.AreSame(contentHost, scrollContent.Children.Single());
        FrameworkElement outcome =
            (FrameworkElement)page.FindName("InspectionOutcomeCardControl");
        FrameworkElement model =
            (FrameworkElement)page.FindName("InspectionModelCardControl");
        FrameworkElement content =
            (FrameworkElement)page.FindName("InspectionContentCardControl");
        FrameworkElement actions =
            (FrameworkElement)page.FindName("InspectionActionCardControl");
        FrameworkElement layoutRoot = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("LayoutRoot"));
        (double Width, double Inset, double HostWidth)[] endpoints =
        [
            (888d, 24d, 840d),
            (887d, 24d, 839d),
            (600d, 24d, 552d),
            (599d, 16d, 567d)
        ];
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

            foreach (var endpoint in endpoints)
            {
                await ResizeClientAndWaitAsync(
                    window,
                    page,
                    contentHost,
                    endpoint.Width,
                    effectiveHeight: 720,
                    expectedInset: endpoint.Inset);

                Point origin = contentHost
                    .TransformToVisual(page)
                    .TransformPoint(new Point());
                Assert.AreEqual(endpoint.Inset, origin.X, 1d);
                Assert.AreEqual(
                    endpoint.HostWidth,
                    contentHost.ActualWidth,
                    1d);
                Assert.AreSame(
                    outcome,
                    page.FindName("InspectionOutcomeCardControl"));
                Assert.AreSame(
                    model,
                    page.FindName("InspectionModelCardControl"));
                Assert.AreSame(
                    content,
                    page.FindName("InspectionContentCardControl"));
                Assert.AreSame(
                    actions,
                    page.FindName("InspectionActionCardControl"));
                Assert.AreSame(
                    scrollViewer,
                    page.FindName("InspectionPageScrollViewer"));
                Assert.AreEqual(0d, outcome.ActualHeight, 0.01d);
                Assert.IsGreaterThan(0d, model.ActualHeight);
                Assert.AreEqual(0d, content.ActualHeight, 0.01d);
                Assert.AreEqual(0d, actions.ActualHeight, 0.01d);
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
        Grid layoutRoot = Assert.IsInstanceOfType<Grid>(
            page.FindName("LayoutRoot"));
        ScrollViewer pageScrollViewer = Assert.IsInstanceOfType<ScrollViewer>(
            page.FindName("InspectionPageScrollViewer"));
        Grid scrollContent = Assert.IsInstanceOfType<Grid>(pageScrollViewer.Content);
        Grid contentHost = Assert.IsInstanceOfType<Grid>(
            page.FindName("InspectionContentHost"));
        Grid reflowHost = Assert.IsInstanceOfType<Grid>(
            page.FindName("InspectionReflowHost"));

        Assert.AreSame(contentHost, scrollContent.Children.Single());
        Assert.AreSame(reflowHost, contentHost.Children.Single());
        Assert.AreEqual(ScrollMode.Enabled, pageScrollViewer.VerticalScrollMode);
        Assert.AreEqual(
            ScrollBarVisibility.Auto,
            pageScrollViewer.VerticalScrollBarVisibility);
        Assert.AreEqual(ScrollMode.Disabled, pageScrollViewer.HorizontalScrollMode);
        Assert.AreEqual(
            ScrollBarVisibility.Disabled,
            pageScrollViewer.HorizontalScrollBarVisibility);
        Assert.AreEqual(ZoomMode.Disabled, pageScrollViewer.ZoomMode);
        Assert.IsFalse(reflowHost.Children.OfType<ScrollViewer>().Any());

        FrameworkElement outcome = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("InspectionOutcomeCardControl"));
        FrameworkElement model = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("InspectionModelCardControl"));
        FrameworkElement content = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("InspectionContentCardControl"));
        FrameworkElement outgoing = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("OutgoingProgressContentCard"));
        FrameworkElement actions = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("InspectionActionCardControl"));
        Assert.AreEqual(2, Grid.GetRow(outcome));
        Assert.AreEqual(4, Grid.GetRow(model));
        Assert.AreEqual(6, Grid.GetRow(content));
        Assert.AreEqual(6, Grid.GetRow(outgoing));
        Assert.AreEqual(8, Grid.GetRow(actions));
        Assert.IsTrue(
            reflowHost.Children.IndexOf(outcome) <
            reflowHost.Children.IndexOf(model));
        Assert.IsTrue(
            reflowHost.Children.IndexOf(model) <
            reflowHost.Children.IndexOf(content));
        Assert.IsTrue(
            reflowHost.Children.IndexOf(content) <
            reflowHost.Children.IndexOf(actions));

        AssertResponsiveStateContract(
            layoutRoot,
            "DesktopPageState",
            minimumWidth: 888,
            inset: 24);
        AssertResponsiveStateContract(
            layoutRoot,
            "MediumPageState",
            minimumWidth: 600,
            inset: 24);
        AssertResponsiveStateContract(
            layoutRoot,
            "NarrowPageState",
            minimumWidth: 0,
            inset: 16);

        ResourceDictionary resources = ModelInspectionResources();
        Assert.AreEqual(840d, resources["InspectionContentColumnWidth"]);
        Assert.AreEqual(840d, contentHost.MaxWidth);
        Assert.AreEqual(888d, resources["InspectionDesktopBreakpoint"]);
        Assert.AreEqual(600d, resources["InspectionCompactBreakpoint"]);
        Assert.AreEqual(
            new CornerRadius(12d),
            Assert.IsInstanceOfType<CornerRadius>(
                resources["InspectionCardCornerRadius"]));
        Assert.AreEqual(
            new CornerRadius(10d),
            Assert.IsInstanceOfType<CornerRadius>(
                resources["InspectionActionCornerRadius"]));
        Assert.AreEqual(
            new Thickness(18d, 10d, 18d, 10d),
            Assert.IsInstanceOfType<Thickness>(
                resources["InspectionActionPadding"]));
        Thickness cardPadding = Assert.IsInstanceOfType<Thickness>(
            resources["InspectionCardPadding"]);
        Assert.AreEqual(24d, cardPadding.Left, 0.01d);
        Assert.AreEqual(24d, cardPadding.Top, 0.01d);
        Assert.AreEqual(24d, cardPadding.Right, 0.01d);
        Assert.AreEqual(24d, cardPadding.Bottom, 0.01d);
        foreach (string themeName in new[] { "Light", "Dark", "HighContrast" })
        {
            ResourceDictionary theme = Assert.IsInstanceOfType<ResourceDictionary>(
                resources.ThemeDictionaries[themeName]);
            Assert.IsTrue(
                theme.ContainsKey("InspectionCanvasBrush"),
                $"{themeName} must define InspectionCanvasBrush");
        }
        Assert.AreEqual(ElementTheme.Light, page.RequestedTheme);
        Assert.AreEqual(ElementTheme.Light, page.ActualTheme);
        Assert.AreEqual(ElementTheme.Light, layoutRoot.ActualTheme);
        Assert.AreSame(
            ThemeResource("Light", "InspectionCanvasBrush"),
            layoutRoot.Background);
        Assert.AreEqual(16d, resources["InspectionCardGap"]);
        Assert.AreEqual(24d, resources["InspectionHeaderToCardGap"]);
        Assert.AreEqual(16d, resources["InspectionProgressHeadingGap"]);
        Assert.AreEqual(48d, resources["InspectionProgressRowHeight"]);

        const double alternateCardGap = 19d;
        const double alternateHeaderGap = 29d;
        ResourceDictionary applicationResources = Application.Current.Resources;
        applicationResources["InspectionCardGap"] = alternateCardGap;
        applicationResources["InspectionHeaderToCardGap"] = alternateHeaderGap;
        try
        {
            var tokenPage = new ModelInspectionPage();
            Assert.AreEqual(
                alternateHeaderGap,
                Assert.IsInstanceOfType<FrameworkElement>(
                    tokenPage.FindName("HeaderToFirstCardGap")).Height);
            foreach (string name in new[]
            {
                "OutcomeToModelCardGap",
                "ModelToContentLiveGap",
                "ModelToContentOutgoingGap",
                "ContentToActionCardGap"
            })
            {
                Assert.AreEqual(
                    alternateCardGap,
                    Assert.IsInstanceOfType<FrameworkElement>(
                        tokenPage.FindName(name)).Height,
                    $"{name} must consume InspectionCardGap directly");
            }
        }
        finally
        {
            applicationResources.Remove("InspectionCardGap");
            applicationResources.Remove("InspectionHeaderToCardGap");
        }
    }

    private static void AssertResponsiveStateContract(
        FrameworkElement root,
        string stateName,
        double minimumWidth,
        double inset)
    {
        VisualStateGroup group = VisualStateManager
            .GetVisualStateGroups(root)
            .Single(candidate => candidate.Name == "ResponsivePageStates");
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
            double expectedHostWidth = Math.Min(
                840d,
                effectiveWidth - (2d * expectedInset));
            double expectedOrigin =
                (effectiveWidth - expectedHostWidth) / 2d;
            Point contentOrigin = contentHost
                .TransformToVisual(page)
                .TransformPoint(new Point());
            if (Math.Abs(page.ActualWidth - effectiveWidth) <= 1d &&
                Math.Abs(page.ActualHeight - effectiveHeight) <= 1d &&
                Math.Abs(contentHost.Margin.Left - expectedInset) <= 0.01 &&
                Math.Abs(contentHost.ActualWidth - expectedHostWidth) <= 1d &&
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
            page.FindName("InspectionPageScrollViewer"));

        Assert.HasCount(2, headerText);
        Assert.IsTrue(headerText.All(text => text.MaxLines == 0));
        Assert.IsTrue(headerText.All(text => text.IsTextScaleFactorEnabled));
        Assert.AreEqual(ScrollMode.Enabled, scrollViewer.VerticalScrollMode);
        Assert.AreEqual(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);

        double naturalHeaderHeight = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("Header")).ActualHeight;
        foreach (TextBlock text in headerText)
        {
            text.FontSize *= 2d;
        }

        Arrange(page, 360, 400);

        FrameworkElement scaledHeader = Assert.IsInstanceOfType<FrameworkElement>(
            page.FindName("Header"));
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

    private static ResourceDictionary ModelInspectionResources() =>
        Application.Current.Resources.MergedDictionaries.Single(dictionary =>
            dictionary.Source?.OriginalString.EndsWith(
                "/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml",
                StringComparison.OrdinalIgnoreCase) == true);

    private static object ThemeResource(string themeName, string key)
    {
        ResourceDictionary modelInspectionTheme = ModelInspectionResources();
        ResourceDictionary theme = Assert.IsInstanceOfType<ResourceDictionary>(
            modelInspectionTheme.ThemeDictionaries[themeName]);
        return theme[key];
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
