using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using Windows.Foundation;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class InspectionModelCardTests
{
    private static readonly string[] ExpectedFieldOrder =
    [
        "MODEL NAME",
        "PUBLISHER",
        "FORMAT",
        "QUANTISATION",
        "PARAMETERS",
        "MODEL TYPE",
        "DECLARED MAX CONTEXT",
        "FILE SIZE"
    ];

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ReadyCollapsed_UsesBalancedNaturalGeometryAndFieldOrder()
    {
        InspectionModelCard control = CreateDetailedControl(width: 840, isExpanded: false);
        Border detailed = Find<Border>(control, "DetailedView");
        Grid metadata = Find<Grid>(control, "MetadataGrid");
        FrameworkElement[] fields = MetadataFields(control);
        InspectionDisclosure disclosure = control.ActiveDisclosure!;
        Border disclosureSurface = Find<Border>(disclosure, "DisclosureCardSurface");
        FrameworkElement disclosureHeader = Find<FrameworkElement>(disclosure, "DisclosureToggleButton");

        Assert.AreEqual(840d, detailed.ActualWidth, 0.01d);
        Assert.AreEqual(0d, detailed.MinHeight, 0.01d, "metadata sizes to its content");
        Assert.AreEqual(new Thickness(24d, 0d, 24d, 12d), detailed.Padding);
        Assert.AreEqual(new Thickness(0d), detailed.BorderThickness);
        Assert.AreEqual(792d, metadata.ActualWidth, 0.01d, "metadata shares the inset content edge");
        Assert.AreEqual(792d, disclosureSurface.ActualWidth, 0.01d);
        Assert.AreEqual(new Thickness(24d, 8d, 24d, 16d), disclosure.Margin);
        Assert.AreEqual(
            disclosureHeader.ActualHeight + disclosureSurface.BorderThickness.Top +
                disclosureSurface.BorderThickness.Bottom,
            disclosure.ActualHeight, 1d, "collapsed disclosure has no trailing minimum-height tail");
        Assert.AreEqual(new Thickness(1d), disclosureSurface.BorderThickness);
        Assert.AreEqual(12d, disclosureSurface.CornerRadius.TopLeft, 0.01d);
        Assert.AreEqual(Visibility.Collapsed,
            Find<Border>(control, "OverviewFormatChip").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            Find<FrameworkElement>(control, "ModelOverviewPanel").Visibility);
        CollectionAssert.AreEqual(ExpectedFieldOrder,
            MetadataLabels(control).Select(label => label.Text).ToArray());

        (int Row, int Column, string Value)[] expected =
        [
            (1, 0, "Safe model name"),
            (1, 1, "Not reported"),
            (2, 2, "GGUF"),
            (2, 3, "Q4_K_M"),
            (1, 2, "3.2B"),
            (1, 3, "Not reported"),
            (2, 0, "131,072 tokens"),
            (2, 1, "2.08 GB")
        ];
        Assert.AreEqual(2, fields.Select(Grid.GetRow).Distinct().Count());
        Assert.AreEqual(4, fields.Select(Grid.GetColumn).Distinct().Count());
        for (int index = 0; index < fields.Length; index++)
        {
            FrameworkElement field = fields[index];
            Assert.AreEqual(expected[index].Row, Grid.GetRow(field), ExpectedFieldOrder[index]);
            Assert.AreEqual(expected[index].Column, Grid.GetColumn(field), ExpectedFieldOrder[index]);
            Assert.AreEqual(1, Grid.GetColumnSpan(field), ExpectedFieldOrder[index]);
            TextBlock[] text = Descendants(field).OfType<TextBlock>()
                .Where(item => item.Visibility == Visibility.Visible).ToArray();
            Assert.HasCount(2, text, ExpectedFieldOrder[index]);
            Assert.AreEqual(ExpectedFieldOrder[index], text[0].Text);
            Assert.AreEqual(expected[index].Value, text[1].Text);
            Assert.AreEqual(0, text[1].MaxLines);
            Assert.AreNotEqual(TextWrapping.NoWrap, text[1].TextWrapping);
            Assert.IsTrue(text[1].ActualHeight + text[1].Margin.Top +
                text[1].Margin.Bottom + 1d >= text[1].DesiredSize.Height,
                $"{ExpectedFieldOrder[index]} value must remain readable");
        }
        Assert.AreEqual("All 5 inspection checks passed",
            Find<TextBlock>(control, "ReadyResultSummary").Text);
        Assert.AreEqual("INSPECTED", Find<TextBlock>(control, "ReadyResultBadgeText").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ReadyExpanded_RendersBoundedFiveCheckGeometry()
    {
        (InspectionModelCard control, Window window) =
            await CreateLoadedDetailedControlAsync(
            width: 840,
            isExpanded: true);

        try
        {
            Border detailed = Find<Border>(control, "DetailedView");
            FrameworkElement viewport = Find<FrameworkElement>(
                control,
                "InspectionDetailsViewport");
            ScrollViewer checksScrollViewer = Find<ScrollViewer>(
                control,
                "InspectionChecksScrollViewer");
            InspectionDisclosure disclosure = control.ActiveDisclosure!;
            Assert.IsNotNull(disclosure);
            Border disclosureSurface = Find<Border>(
                disclosure,
                "DisclosureCardSurface");
            Point viewportOrigin = viewport
                .TransformToVisual(disclosureSurface)
                .TransformPoint(new Point());
            Assert.IsTrue(control.Presentation.IsInspectionDetailsExpanded);
            Assert.IsTrue(disclosure.IsExpanded);
            Assert.AreEqual(0d, detailed.MinHeight, 0.01d);
            Assert.IsTrue(
                control.ActualHeight >= detailed.ActualHeight + 12d +
                    disclosure.ActualHeight - 1d,
                "the model control grows for the separate disclosure card");
            Assert.AreEqual(
                172d,
                checksScrollViewer.MaxHeight,
                0.01,
                "bounded check viewport");
            double viewportLeftInset = viewportOrigin.X -
                disclosureSurface.BorderThickness.Left;
            double viewportRightInset = disclosureSurface.ActualWidth -
                disclosureSurface.BorderThickness.Right - viewportOrigin.X -
                viewport.ActualWidth;
            double expectedViewportWidth = disclosureSurface.ActualWidth -
                disclosureSurface.BorderThickness.Left -
                disclosureSurface.BorderThickness.Right - 48d;
            double physicalPixel = 1d / control.XamlRoot.RasterizationScale;
            Assert.AreEqual(24d, viewportLeftInset, physicalPixel, "report inner left inset");
            Assert.AreEqual(24d, viewportRightInset, physicalPixel, "report inner right inset");
            Assert.AreEqual(expectedViewportWidth, viewport.ActualWidth, 2d * physicalPixel,
                "nested report inner width rounds both edges to physical pixels");
            Assert.IsGreaterThanOrEqualTo(
                0d,
                viewportOrigin.Y,
                "report top remains within the disclosure card");
            Assert.IsGreaterThanOrEqualTo(
                0d,
                disclosureSurface.ActualHeight - viewportOrigin.Y -
                    viewport.ActualHeight,
                "report bottom remains within the disclosure card");
            Assert.AreEqual(Visibility.Visible, viewport.Visibility);
            FrameworkElement fade = Find<FrameworkElement>(
                control,
                "InspectionChecksBottomFade");
            Assert.AreEqual(24d, fade.Height, 0.01);
            Assert.IsFalse(fade.IsHitTestVisible);
            ItemsControl checks = Find<ItemsControl>(
                control,
                "InspectionChecksItemsControl");
            FrameworkElement[] rows = Descendants(checks)
                .OfType<FrameworkElement>()
                .Where(element => string.Equals(
                    element.Tag as string,
                    "InspectionCheckRow",
                    StringComparison.Ordinal))
                .ToArray();
            Assert.HasCount(5, rows, "all five rows must exist in the automation tree");
            for (int index = 0; index < rows.Length; index++)
            {
                string expectedTitle = $"Inspection check {index + 1}";
                string expectedDetail = $"Safe evidence for check {index + 1}.";
                string expectedAutomationName = $"{expectedTitle}. Passed.";
                TextBlock title = Descendants(rows[index])
                    .OfType<TextBlock>()
                    .Single(text => text.Text == expectedTitle);
                TextBlock detail = Descendants(rows[index])
                    .OfType<TextBlock>()
                    .Single(text => text.Text == expectedDetail);
                TextBlock status = Descendants(rows[index])
                    .OfType<TextBlock>()
                    .Single(text =>
                        text.Visibility == Visibility.Visible &&
                        text.Text == "Passed");
                InspectionStatusGlyph statusGlyph = Descendants(rows[index])
                    .OfType<InspectionStatusGlyph>()
                    .Single(element => element.Visibility == Visibility.Visible);
                AutomationPeer titlePeer =
                    FrameworkElementAutomationPeer.CreatePeerForElement(title)
                    ?? new TextBlockAutomationPeer(title);

                Assert.AreEqual(expectedAutomationName, titlePeer.GetName());
                Assert.AreEqual(12d, detail.FontSize, 0.01);
                Assert.AreEqual(0, detail.MaxLines);
                Assert.AreEqual("Passed", status.Text);
                Assert.AreEqual(InspectionStatusGlyphKind.Success, statusGlyph.Kind);
                Assert.AreEqual(22d, statusGlyph.SurfaceSize, 0.01d);
                Viewbox glyphHost = Descendants(rows[index])
                    .OfType<Viewbox>()
                    .Single(viewbox =>
                        Math.Abs(viewbox.Width - 20d) < 0.01d &&
                        Math.Abs(viewbox.Height - 20d) < 0.01d);
                Assert.AreEqual(20d, glyphHost.ActualWidth, 0.01d);
                Assert.AreEqual(20d, glyphHost.ActualHeight, 0.01d);
                Assert.AreEqual(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(statusGlyph));
                object successBrush = ThemeResource(
                    control.ActualTheme.ToString(),
                    "InspectionSuccessTextBrush");
                Assert.IsTrue(Descendants(statusGlyph)
                    .OfType<Shape>()
                    .Any(shape =>
                        ReferenceEquals(shape.Stroke, successBrush) ||
                        ReferenceEquals(shape.Fill, successBrush)));
            }

            Assert.IsTrue(checksScrollViewer.IsTabStop);
            Assert.IsTrue(checksScrollViewer.ScrollableHeight > 0d);
            Assert.IsTrue(checksScrollViewer.Focus(FocusState.Keyboard));
            Assert.AreSame(
                checksScrollViewer,
                FocusManager.GetFocusedElement(control.XamlRoot));
            var scrollPeer = new ScrollViewerAutomationPeer(checksScrollViewer);
            IScrollProvider scrollProvider = Assert.IsInstanceOfType<IScrollProvider>(
                scrollPeer.GetPattern(PatternInterface.Scroll));
            double initialOffset = checksScrollViewer.VerticalOffset;
            scrollProvider.Scroll(
                ScrollAmount.NoAmount,
                ScrollAmount.SmallIncrement);
            control.UpdateLayout();
            Assert.IsTrue(
                checksScrollViewer.VerticalOffset > initialOffset,
                "the focused bounded viewport must expose vertical scrolling");

            control.Presentation = CreatePresentation(
                isExpanded: true,
                useMaximumSafeEvidence: true);
            await WaitForLayoutConditionAsync(
                control,
                () => Descendants(checks)
                    .OfType<TextBlock>()
                    .Count(text => text.Text == new string('D', 512)) == 5);
            TextBlock[] maximumDetails = Descendants(checks)
                .OfType<TextBlock>()
                .Where(text => text.Text == new string('D', 512))
                .ToArray();
            Assert.HasCount(5, maximumDetails);
            foreach (TextBlock detail in maximumDetails)
            {
                detail.FontSize *= 2d;
            }

            Assert.AreEqual(Visibility.Collapsed,
                Find<FrameworkElement>(control, "ModelConfigurationPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed,
                Find<FrameworkElement>(control, "ModelResultSummaryPanel").Visibility);
            FrameworkElement outcomeBand = Find<FrameworkElement>(control, "TerminalOutcomeBand");
            TextBlock outcomeTitle = Find<TextBlock>(control, "TerminalOutcomeTitle");
            TextBlock outcomeMessage = Find<TextBlock>(control, "TerminalOutcomeMessage");
            outcomeTitle.FontSize *= 2d;
            outcomeMessage.FontSize *= 2d;
            control.UpdateLayout();

            AssertTextFits(outcomeTitle, outcomeBand,
                "terminal outcome title at 200% text", requiresEmergencyWrap: false);
            AssertTextFits(outcomeMessage, outcomeBand,
                "terminal outcome message at 200% text", requiresEmergencyWrap: false);
            Point messageOrigin = outcomeMessage.TransformToVisual(outcomeBand)
                .TransformPoint(new Point());
            Point titleOrigin = outcomeTitle.TransformToVisual(outcomeBand)
                .TransformPoint(new Point());
            Assert.IsTrue(messageOrigin.Y >= titleOrigin.Y + outcomeTitle.ActualHeight - 1d,
                "terminal outcome message follows its title without overlap at 200% text");

            foreach (TextBlock detail in maximumDetails)
            {
                FrameworkElement row = Assert.IsInstanceOfType<FrameworkElement>(
                    AncestorWithTag(detail, "InspectionCheckRow"));
                AssertTextFits(
                    detail,
                    row,
                    "maximum safe inspection detail at 200% equivalent text size");
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
    [DataRow(888d, 2)]
    [DataRow(600d, 5)]
    [DataRow(599d, 8)]
    public async Task Metadata_ReflowsAtApprovedResponsiveBreakpoints(
        double clientWidth,
        int expectedReadingRowCount)
    {
        var control = new InspectionModelCard
        {
            Presentation = CreatePresentation(
                isExpanded: false,
                useMaximumSafeEvidence: true)
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var host = new ScrollViewer
        {
            Content = control,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled
        };
        var window = new Window { Content = host };

        try
        {
            int expectedAttachedRowCount = clientWidth >= 888d
                ? 2
                : clientWidth >= 600d ? 5 : 8;
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ResizeClientAndWaitAsync(
                window,
                control,
                effectiveWidth: 1000,
                expectedRowCount: 2);
            await ResizeClientAndWaitAsync(
                window,
                control,
                clientWidth,
                expectedAttachedRowCount);

            Border detailed = Find<Border>(control, "DetailedView");
            FrameworkElement[] metadataFields = MetadataFields(control);
            int overviewReadingRows = new[] { 0, 1, 4, 5, 6, 7 }
                .Select(index => Grid.GetRow(metadataFields[index]))
                .Distinct()
                .Count();
            int configurationReadingRows = new[] { 2, 3 }
                .Select(index => Grid.GetRow(metadataFields[index]))
                .Distinct()
                .Count();
            int readingRows = clientWidth >= 888d
                ? Math.Max(overviewReadingRows, configurationReadingRows)
                : overviewReadingRows + configurationReadingRows;
            Assert.AreEqual(
                expectedReadingRowCount,
                readingRows,
                $"grouped metadata reading rows at {clientWidth}px");

            FrameworkElement configurationPanel = Find<FrameworkElement>(
                control,
                "ModelConfigurationPanel");
            FrameworkElement resultSummaryPanel = Find<FrameworkElement>(
                control,
                "ModelResultSummaryPanel");
            if (clientWidth >= 888d)
            {
                Assert.AreEqual(1, Grid.GetRow(metadataFields[0]));
                Assert.AreEqual(0, Grid.GetColumn(metadataFields[0]));
                Assert.AreEqual(1, Grid.GetRow(metadataFields[4]));
                Assert.AreEqual(2, Grid.GetRow(metadataFields[6]));
                Assert.AreEqual(0, Grid.GetRow(configurationPanel));
                Assert.AreEqual(2, Grid.GetColumn(configurationPanel));
                Assert.AreEqual(2, Grid.GetColumnSpan(configurationPanel));
                Assert.AreEqual(3, Grid.GetRow(resultSummaryPanel));
            }
            else if (clientWidth >= 600d)
            {
                Assert.AreEqual(Grid.GetRow(metadataFields[0]),
                    Grid.GetRow(metadataFields[1]));
                Assert.AreEqual(Grid.GetRow(metadataFields[4]),
                    Grid.GetRow(metadataFields[5]));
                Assert.AreEqual(Grid.GetRow(metadataFields[6]),
                    Grid.GetRow(metadataFields[7]));
                Assert.AreEqual(0, Grid.GetColumn(metadataFields[0]));
                Assert.AreEqual(1, Grid.GetColumn(metadataFields[1]));
                Assert.AreEqual(4, Grid.GetRow(configurationPanel));
                Assert.AreEqual(7, Grid.GetRow(resultSummaryPanel));
                Assert.IsGreaterThan(
                    Grid.GetRow(metadataFields[7]),
                    Grid.GetRow(configurationPanel),
                    "configuration follows the two-column overview facts");
                Assert.IsGreaterThan(
                    Grid.GetRow(configurationPanel),
                    Grid.GetRow(resultSummaryPanel),
                    "result summary follows configuration");
            }
            else
            {
                int[] overviewIndexes = [0, 1, 4, 5, 6, 7];
                for (int rowIndex = 0; rowIndex < overviewIndexes.Length; rowIndex++)
                {
                    FrameworkElement field = metadataFields[overviewIndexes[rowIndex]];
                    Assert.AreEqual(rowIndex + 1, Grid.GetRow(field));
                    Assert.AreEqual(0, Grid.GetColumn(field));
                    Assert.AreEqual(2, Grid.GetColumnSpan(field));
                }
                Assert.AreEqual(8, Grid.GetRow(metadataFields[2]));
                Assert.AreEqual(9, Grid.GetRow(metadataFields[3]));
                Assert.AreEqual(7, Grid.GetRow(configurationPanel));
                Assert.AreEqual(10, Grid.GetRow(resultSummaryPanel));
            }
            Thickness expectedFactPadding = ((Border)metadataFields[0]).Padding;
            Assert.IsTrue(new[] { 1, 4, 5, 6, 7 }.All(index =>
                    ((Border)metadataFields[index]).Padding == expectedFactPadding),
                $"all overview fact tiles use equal padding at {clientWidth}px");

            foreach (FrameworkElement field in metadataFields)
            {
                TextBlock value = Descendants(field)
                    .OfType<TextBlock>()
                    .Last();
                AssertTextFits(
                    value,
                    field,
                    $"maximum safe metadata at {clientWidth}px");
            }

            Border formatChip = Find<Border>(control, "OverviewFormatChip");
            FrameworkElement header = Find<FrameworkElement>(
                control,
                "DetailedHeader");
            FrameworkElement titleHost = Find<FrameworkElement>(
                control,
                "ModelOverviewTitleHost");
            TextBlock sectionTitle = Descendants(header)
                .OfType<TextBlock>()
                .Single(text => text.Text == "Model overview");
            Assert.AreEqual(Visibility.Collapsed, formatChip.Visibility);
            Assert.AreEqual(HorizontalAlignment.Left, sectionTitle.HorizontalAlignment);
            Assert.AreEqual(TextAlignment.Left, sectionTitle.TextAlignment);
            AssertTextFits(
                sectionTitle,
                header,
                "responsive ready title",
                requiresEmergencyWrap: false);

            if (clientWidth < 600d)
            {
                await ResizeClientAndWaitAsync(
                    window,
                    control,
                    effectiveWidth: 360d,
                    expectedRowCount: 8);
                Assert.AreEqual(1, Grid.GetColumn(titleHost));
                Assert.AreEqual(1, Grid.GetColumnSpan(titleHost));
                Assert.IsGreaterThan(
                    120d,
                    sectionTitle.ActualWidth,
                    "the 360px title must not be squeezed between chip-sized columns");
                Assert.IsLessThanOrEqualTo(
                    30d,
                    sectionTitle.ActualHeight,
                    "the 360px title must remain on a usable single line");
                AssertTextFits(
                    sectionTitle,
                    header,
                    "360px ready title",
                    requiresEmergencyWrap: false);
                Assert.AreEqual(
                    HorizontalAlignment.Left,
                    sectionTitle.HorizontalAlignment,
                    "360px ready title keeps the approved left hierarchy");
                Assert.AreEqual(
                    TextAlignment.Left,
                    sectionTitle.TextAlignment,
                    "360px ready title preserves left alignment");

                foreach (TextBlock value in MetadataFields(control)
                    .Select(field => Descendants(field)
                        .OfType<TextBlock>()
                        .Last()))
                {
                    value.FontSize *= 2d;
                }

                control.UpdateLayout();
                await Task.Yield();

                Assert.AreEqual(
                    HorizontalAlignment.Left,
                    sectionTitle.HorizontalAlignment,
                    "scaled narrow ready title remains left aligned");
                Assert.AreEqual(
                    TextAlignment.Left,
                    sectionTitle.TextAlignment,
                    "scaled narrow ready title preserves left hierarchy");
                foreach (FrameworkElement field in MetadataFields(control))
                {
                    AssertTextFits(
                        Descendants(field).OfType<TextBlock>().Last(),
                        field,
                        "maximum safe narrow metadata at 200% equivalent text size");
                }
            }
        }
        finally
        {
            host.Content = null;
            window.Content = null;
            window.Close();
        }
    }

    private static void AssertElementCentre(
        FrameworkElement element,
        FrameworkElement container,
        string message)
    {
        Point origin = element.TransformToVisual(container)
            .TransformPoint(new Point());
        Assert.AreEqual(
            container.ActualWidth / 2d,
            origin.X + (element.ActualWidth / 2d),
            1d,
            message);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PresentationAssignment_UsesPageTargetWithoutResettingExpansion()
    {
        (InspectionModelCard control, Window window) =
            await CreateLoadedDetailedControlAsync(
                width: 840,
                isExpanded: true);

        try
        {
            int userEventCount = 0;
            control.DisclosureToggleRequested +=
                (_, _) => userEventCount++;
            control.Presentation = CreatePresentation(isExpanded: true);
            control.UpdateLayout();
            await Task.Yield();

            InspectionDisclosure disclosure = control.ActiveDisclosure!;
            Assert.IsNotNull(disclosure);
            Assert.IsTrue(control.Presentation.IsInspectionDetailsExpanded);
            Assert.IsTrue(disclosure.IsExpanded);

            control.Presentation = CreatePresentation(isExpanded: false);
            control.UpdateLayout();
            await Task.Yield();

            Assert.IsFalse(control.Presentation.IsInspectionDetailsExpanded);
            Assert.IsFalse(disclosure.IsExpanded);
            Assert.AreEqual(
                0,
                userEventCount,
                "page-applied targets must not feed back as user requests");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Disclosure_ExposesTypedChangeEventAndNamedTargets()
    {
        var control = new InspectionModelCard
        {
            Presentation = CreatePresentation(isExpanded: false)
        };
        EventInfo? changeEvent = typeof(InspectionModelCard).GetEvent(
            "DisclosureToggleRequested",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsNotNull(changeEvent, "the page needs a typed disclosure event seam");
        Type handlerType = changeEvent.EventHandlerType!;
        Assert.IsTrue(handlerType.IsGenericType);
        Type eventArgsType = handlerType.GenericTypeArguments.Single();
        Assert.IsTrue(typeof(EventArgs).IsAssignableFrom(eventArgsType));
        PropertyInfo? targetProperty = eventArgsType.GetProperty(
            "IsExpanded",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(targetProperty);
        Assert.AreEqual(typeof(bool), targetProperty.PropertyType);
        Assert.IsFalse(targetProperty.CanWrite);

        var capture = new ExpansionEventCapture();
        Delegate handler = Delegate.CreateDelegate(
            handlerType,
            capture,
            typeof(ExpansionEventCapture).GetMethod(
                nameof(ExpansionEventCapture.Capture))!);
        changeEvent.GetAddMethod(nonPublic: true)!.Invoke(control, [handler]);
        InspectionDisclosure disclosure = control.ActiveDisclosure!;
        Assert.IsNotNull(disclosure);

        Assert.IsNull(capture.IsExpanded);
        Assert.AreSame(disclosure, control.ActiveDisclosure);
        Assert.AreSame(
            disclosure.FindName("DisclosureChevron"),
            disclosure.ChevronTarget);
        Assert.AreSame(
            disclosure.FindName("DisclosureViewport"),
            disclosure.ViewportTarget);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DisclosureToggle_PreservesCurrentFocus()
    {
        InspectionModelCard control = CreateDetailedControl(
            width: 840,
            isExpanded: false);
        InspectionDisclosure disclosure = control.ActiveDisclosure!;
        Assert.IsNotNull(disclosure);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window
        {
            Content = control
        };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
            DependencyObject? focusedBefore =
                FocusManager.GetFocusedElement(control.XamlRoot) as DependencyObject;
            AutomationPeer peer = FrameworkElementAutomationPeer
                .CreatePeerForElement(disclosure);
            Assert.IsNotNull(peer);
            IExpandCollapseProvider expandProvider =
                Assert.IsInstanceOfType<IExpandCollapseProvider>(
                    peer.GetPattern(PatternInterface.ExpandCollapse));
            int eventCount = 0;
            bool? requestedTarget = null;
            control.DisclosureToggleRequested += (_, eventArguments) =>
            {
                eventCount++;
                requestedTarget = eventArguments.IsExpanded;
            };

            expandProvider.Expand();
            control.UpdateLayout();

            Assert.IsFalse(
                disclosure.IsExpanded,
                "The request must not mutate page-owned presentation state.");
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(true, requestedTarget);
            Assert.IsNotNull(focusedBefore);
            Assert.AreSame(
                focusedBefore,
                FocusManager.GetFocusedElement(control.XamlRoot));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SharedThemeResources_AndCompactAccessibleScaleRemainNatural()
    {
        var control = new InspectionModelCard
        {
            Width = 840,
            RequestedTheme = ElementTheme.Light,
            Presentation = CreatePresentation(
                isExpanded: false,
                displayMode: InspectionModelCardMode.Compact)
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var host = new ScrollViewer
        {
            Content = control,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled
        };
        var window = new Window { Content = host };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ArrangeUntilStable(control, 840);
            Border compact = Find<Border>(control, "CompactView");
            Border badge = Find<Border>(control, "CompactStatusChip");
            Border formatTile = Find<Border>(control, "CompactFormatTile");
            Grid compactGrid = Find<Grid>(control, "CompactGrid");
            FrameworkElement summaryPanel = Find<FrameworkElement>(
                control,
                "CompactModelSummaryPanel");
            double expectedCompactHeight = compact.BorderThickness.Top +
                compact.Padding.Top + compactGrid.ActualHeight +
                compact.Padding.Bottom + compact.BorderThickness.Bottom;

            Assert.AreEqual(840d, compact.ActualWidth, 0.01);
            Assert.AreEqual(
                expectedCompactHeight,
                compact.ActualHeight,
                1d,
                "compact card uses its realized content, shared padding, and border thickness");
            Assert.AreEqual(Visibility.Visible, formatTile.Visibility,
                "the compact card identifies the selected model file");
            Assert.AreEqual(48d, compactGrid.ColumnDefinitions[0].ActualWidth, 0.01d,
                "the file icon owns the leading compact column");
            Assert.AreEqual(0, Grid.GetRow(summaryPanel));
            Assert.AreEqual(0, Grid.GetRow(badge));
            Assert.IsLessThan(
                badge.TransformToVisual(compact).TransformPoint(new Point()).X,
                summaryPanel.TransformToVisual(compact).TransformPoint(new Point()).X,
                "model summary precedes the status pill in one horizontal row");
            Assert.AreEqual(34d, badge.ActualHeight, 1d);
            Assert.AreEqual(
                "InspectedBadgeState",
                CurrentVisualState(
                    Find<FrameworkElement>(control, "LayoutRoot"),
                    "BadgeToneStates"));
            Assert.AreSame(
                ThemeResource("Light", "InspectionSuccessSurfaceBrush"),
                badge.Background);

            await ChangeThemeAsync(control, ElementTheme.Dark);
            await WaitForLayoutConditionAsync(
                control,
                () => ReferenceEquals(
                    ThemeResource("Dark", "InspectionSuccessSurfaceBrush"),
                    badge.Background));

            Assert.AreSame(
                ThemeResource("Dark", "InspectionSuccessSurfaceBrush"),
                badge.Background,
                "ThemeResource must follow the control's actual theme");
            Assert.IsNotNull(
                ThemeResource("HighContrast", "InspectionSuccessSurfaceBrush"));
            Assert.IsNotNull(
                ThemeResource("HighContrast", "InspectionSuccessTextBrush"));

            control.Width = double.NaN;
            control.Presentation = CreatePresentation(
                isExpanded: false,
                displayMode: InspectionModelCardMode.Compact,
                useMaximumSafeEvidence: true,
                badgeState: InspectionModelBadgeState.NotInspected);
            await ResizeClientAndWaitAsync(
                window,
                control,
                effectiveWidth: 599,
                expectedRowCount: 8);
            TextBlock[] dynamicText =
            [
                Descendants(compact).OfType<TextBlock>()
                    .Single(text => text.Text == new string('M', 160)),
                Descendants(compact).OfType<TextBlock>()
                    .Single(text => text.Text ==
                        $"{new string('F', 96)} | " +
                        $"{new string('Q', 96)} | 2.08 GB"),
                Find<TextBlock>(control, "CompactStatusText")
            ];
            Point summaryOrigin = summaryPanel
                .TransformToVisual(compact)
                .TransformPoint(new Point());
            Point badgeOrigin = badge
                .TransformToVisual(compact)
                .TransformPoint(new Point());
            Assert.IsTrue(
                badgeOrigin.Y > summaryOrigin.Y,
                "the compact status chip must reflow below model evidence on narrow clients");

            foreach (TextBlock text in dynamicText)
            {
                text.FontSize *= 2d;
            }

            control.UpdateLayout();
            await Task.Yield();

            Assert.IsTrue(
                badge.ActualHeight > 34d,
                "the compact status chip must grow at 200% equivalent text size");
            Assert.IsTrue(
                compact.ActualHeight > expectedCompactHeight,
                "the compact card must grow instead of clipping scaled content");
            host.Height = expectedCompactHeight;
            await WaitForLayoutConditionAsync(
                host,
                () => host.ScrollableHeight > 0d);
            Assert.IsTrue(
                host.ScrollableHeight > 0d,
                "the page-like host must scroll rather than cap the natural card height");
            foreach (TextBlock text in dynamicText)
            {
                FrameworkElement container = ReferenceEquals(text, dynamicText[^1])
                    ? badge
                    : compact;
                AssertTextFits(
                    text,
                    container,
                    "maximum safe compact evidence at 200% equivalent text size",
                    requiresEmergencyWrap: !ReferenceEquals(text, dynamicText[^1]));
            }

            (InspectionModelBadgeState State, string Text)[] badgeCases =
            [
                (InspectionModelBadgeState.ModelSelected, "Inspection in progress"),
                (InspectionModelBadgeState.Inspected, "Ready"),
                (InspectionModelBadgeState.SourceModel, "Inspection in progress"),
                (InspectionModelBadgeState.Incomplete, "INCOMPLETE"),
                (InspectionModelBadgeState.Unsupported, "UNSUPPORTED"),
                (InspectionModelBadgeState.Invalid, "INVALID"),
                (InspectionModelBadgeState.NotInspected, "NOT INSPECTED"),
                (InspectionModelBadgeState.ResultUnknown, "RESULT UNKNOWN")
            ];
            foreach (var badgeCase in badgeCases)
            {
                control.Presentation = CreatePresentation(
                    isExpanded: false,
                    displayMode: InspectionModelCardMode.Compact,
                    useMaximumSafeEvidence: true,
                    badgeState: badgeCase.State);
                TextBlock badgeText = Find<TextBlock>(
                    control,
                    "CompactStatusText");
                badgeText.FontSize = 20d;
                control.UpdateLayout();
                await WaitForLayoutConditionAsync(
                    badgeText,
                    () => badgeText.Text == badgeCase.Text &&
                        badgeText.ActualWidth > 0d &&
                        badgeText.ActualHeight > 0d &&
                        badgeText.DesiredSize.Width <=
                            badgeText.ActualWidth + 1d &&
                        badgeText.DesiredSize.Height <=
                            badgeText.ActualHeight + 1d);

                Assert.AreEqual(badgeCase.Text, badgeText.Text);
                Assert.AreEqual($"Model status: {control.GetBadgeText(badgeCase.State)}",
                    AutomationProperties.GetName(badge),
                    "the status badge retains its explicit accessible model state");
                Assert.IsTrue(badge.ActualHeight >= 34d);
                if (badgeText.ActualHeight > badgeText.FontSize * 1.5d)
                {
                    Assert.IsTrue(
                        badge.ActualHeight > 34d,
                        $"{badgeCase.Text} must grow its chip when it wraps");
                }

                AssertTextFits(
                    badgeText,
                    badge,
                    $"{badgeCase.Text} at 200% equivalent text size",
                    requiresEmergencyWrap: false);
            }
        }
        finally
        {
            host.Content = null;
            window.Content = null;
            window.Close();
        }
    }

    private static InspectionModelCard CreateDetailedControl(
        double width,
        bool isExpanded)
    {
        var control = new InspectionModelCard
        {
            Width = width,
            Presentation = CreatePresentation(isExpanded: false)
        };

        ArrangeUntilStable(control, width);
        Assert.IsTrue(VisualStateManager.GoToState(
            control,
            "WideModelState",
            false));
        ArrangeUntilStable(control, width);
        if (isExpanded)
        {
            control.Presentation = CreatePresentation(isExpanded: true);
            ArrangeUntilStable(control, width);
        }

        return control;
    }

    private static void ArrangeUntilStable(
        InspectionModelCard control,
        double width)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            control.Measure(new Size(width, double.PositiveInfinity));
            control.Arrange(new Rect(0, 0, width, control.DesiredSize.Height));
            control.UpdateLayout();
        }
    }

    private static async Task<(InspectionModelCard Control, Window Window)>
        CreateLoadedDetailedControlAsync(
            double width,
            bool isExpanded)
    {
        InspectionModelCard control = CreateDetailedControl(
            width,
            isExpanded: false);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window
        {
            Content = new ScrollViewer
            {
                Content = control,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Enabled
            }
        };
        window.Activate();

        try
        {
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ResizeClientAndWaitAsync(
                window,
                control,
                effectiveWidth: 888,
                expectedRowCount: 2);
            control.Presentation = CreatePresentation(isExpanded);
            control.UpdateLayout();
            return (control, window);
        }
        catch
        {
            window.Content = null;
            window.Close();
            throw;
        }
    }

    private static async Task ResizeClientAndWaitAsync(
        Window window,
        InspectionModelCard element,
        double effectiveWidth,
        int expectedRowCount)
    {
        var layoutReached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteWhenReady(object? sender, object eventArguments)
        {
            int actualRowCount = MetadataFields(element)
                .Select(Grid.GetRow)
                .Distinct()
                .Count();
            if (Math.Abs(element.XamlRoot.Size.Width - effectiveWidth) <= 1d &&
                actualRowCount == expectedRowCount)
            {
                layoutReached.TrySetResult(true);
            }
        }

        void CompleteWhenRootChanges(
            XamlRoot sender,
            XamlRootChangedEventArgs eventArguments) =>
            CompleteWhenReady(sender, eventArguments);

        element.LayoutUpdated += CompleteWhenReady;
        element.XamlRoot.Changed += CompleteWhenRootChanges;
        try
        {
            double scale = element.XamlRoot.RasterizationScale;
            window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                (int)Math.Round(effectiveWidth * scale),
                (int)Math.Round(720d * scale)));
            CompleteWhenReady(null, EventArgs.Empty);
            await layoutReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            element.UpdateLayout();
        }
        finally
        {
            element.LayoutUpdated -= CompleteWhenReady;
            element.XamlRoot.Changed -= CompleteWhenRootChanges;
        }
    }

    private static InspectionModelCardPresentation CreatePresentation(
        bool isExpanded,
        InspectionModelCardMode displayMode = InspectionModelCardMode.Detailed,
        bool useMaximumSafeEvidence = false,
        InspectionModelBadgeState badgeState =
            InspectionModelBadgeState.Inspected)
    {
        string modelName = useMaximumSafeEvidence
            ? new string('M', 160)
            : "Safe model name";
        string format = useMaximumSafeEvidence
            ? new string('F', 96)
            : "GGUF";
        string quantisation = useMaximumSafeEvidence
            ? new string('Q', 96)
            : "Q4_K_M";
        string parameters = useMaximumSafeEvidence
            ? new string('P', 96)
            : "3.2B";
        string checkDetail = useMaximumSafeEvidence
            ? new string('D', 512)
            : string.Empty;

        return new InspectionModelCardPresentation
        {
            DisplayMode = displayMode,
            BadgeState = badgeState,
            ModelName = modelName,
            CompactSummary = $"{format} | {quantisation} | 2.08 GB",
            FormatShortName = format,
            OverviewFormatBadgeText = $"{format} MODEL",
            Publisher = "Not reported",
            FormatName = format,
            Quantisation = quantisation,
            ParameterCount = parameters,
            ModelType = "Not reported",
            DeclaredContext = "131,072 tokens",
            FileSize = "2.08 GB",
            InspectionChecksSummary = "All 5 inspection checks passed",
            InspectionChecks = Enumerable.Range(1, 5)
                .Select(index => new InspectionCheckPresentation
                {
                    Title = $"Inspection check {index}",
                    Detail = useMaximumSafeEvidence
                        ? checkDetail
                        : $"Safe evidence for check {index}.",
                    Status = InspectionCheckStatus.Passed,
                    StatusText = "Passed",
                    AutomationName = $"Inspection check {index}. Passed."
                })
                .ToArray(),
            InspectionDetailsVisibility = Visibility.Visible,
            IsInspectionDetailsExpanded = isExpanded
        };
    }

    private static TextBlock[] MetadataLabels(DependencyObject root) =>
        Descendants(root)
            .OfType<TextBlock>()
            .Where(text => ExpectedFieldOrder.Contains(text.Text, StringComparer.Ordinal))
            .ToArray();

    private static FrameworkElement[] MetadataFields(FrameworkElement root) =>
    [
        Find<FrameworkElement>(root, "ModelNameField"),
        Find<FrameworkElement>(root, "PublisherField"),
        Find<FrameworkElement>(root, "FormatField"),
        Find<FrameworkElement>(root, "QuantisationField"),
        Find<FrameworkElement>(root, "ParametersField"),
        Find<FrameworkElement>(root, "ModelTypeField"),
        Find<FrameworkElement>(root, "DeclaredContextField"),
        Find<FrameworkElement>(root, "FileSizeField")
    ];

    private static DependencyObject? AncestorWithTag(
        DependencyObject element,
        string tag)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is FrameworkElement frameworkElement &&
                string.Equals(
                    frameworkElement.Tag as string,
                    tag,
                    StringComparison.Ordinal))
            {
                return current;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static async Task WaitForLayoutConditionAsync(
        FrameworkElement element,
        Func<bool> condition,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))]
        string conditionDescription = "")
    {
        var reached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteWhenReady(object? sender, object eventArguments)
        {
            if (condition())
            {
                reached.TrySetResult(true);
            }
        }

        element.LayoutUpdated += CompleteWhenReady;
        try
        {
            element.InvalidateMeasure();
            element.UpdateLayout();
            CompleteWhenReady(null, EventArgs.Empty);
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException exception)
            {
                throw new TimeoutException(
                    $"Layout condition timed out for {element.Name}: {conditionDescription}. " +
                    $"Theme={element.ActualTheme}, actual={element.ActualWidth}x{element.ActualHeight}, " +
                    $"desired={element.DesiredSize.Width}x{element.DesiredSize.Height}, " +
                    $"text='{(element as TextBlock)?.Text}', margin={element.Margin}.", exception);
            }
        }
        finally
        {
            element.LayoutUpdated -= CompleteWhenReady;
        }
    }

    private static void AssertTextFits(
        TextBlock text,
        FrameworkElement container,
        string context,
        bool requiresEmergencyWrap = true)
    {
        Point origin = text
            .TransformToVisual(container)
            .TransformPoint(new Point());
        double arrangedWidth = text.ActualWidth +
            text.Margin.Left +
            text.Margin.Right;
        double arrangedHeight = text.ActualHeight +
            text.Margin.Top +
            text.Margin.Bottom;

        if (requiresEmergencyWrap)
        {
            Assert.AreEqual(TextWrapping.Wrap, text.TextWrapping, context);
        }
        else
        {
            Assert.AreNotEqual(TextWrapping.NoWrap, text.TextWrapping, context);
        }
        Assert.IsTrue(
            text.DesiredSize.Width <= arrangedWidth + 1d,
            $"{context}: desired width {text.DesiredSize.Width} exceeds " +
            $"arranged width {arrangedWidth}");
        Assert.IsTrue(
            text.DesiredSize.Height <= arrangedHeight + 1d,
            $"{context}: desired height {text.DesiredSize.Height} exceeds " +
            $"arranged height {arrangedHeight}");
        Assert.IsTrue(
            origin.X >= -1d &&
            origin.Y >= -1d &&
            origin.X + text.ActualWidth <= container.ActualWidth + 1d &&
            origin.Y + text.ActualHeight <= container.ActualHeight + 1d,
            $"{context}: text '{text.Text[..Math.Min(text.Text.Length, 24)]}' " +
            $"at ({origin.X:F1},{origin.Y:F1}) size " +
            $"({text.ActualWidth:F1},{text.ActualHeight:F1}) escapes container " +
            $"({container.ActualWidth:F1},{container.ActualHeight:F1})");
    }

    private static string CurrentVisualState(
        FrameworkElement root,
        string groupName) =>
        VisualStateManager.GetVisualStateGroups(root)
            .Single(group => group.Name == groupName)
            .CurrentState
            .Name;

    private static async Task ChangeThemeAsync(
        FrameworkElement element,
        ElementTheme requestedTheme)
    {
        var themeChanged = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnActualThemeChanged(FrameworkElement sender, object eventArguments) =>
            themeChanged.TrySetResult(true);

        element.ActualThemeChanged += OnActualThemeChanged;
        try
        {
            element.RequestedTheme = requestedTheme;
            await themeChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            element.UpdateLayout();
        }
        finally
        {
            element.ActualThemeChanged -= OnActualThemeChanged;
        }
    }

    private static object ThemeResource(string themeName, string key)
    {
        ResourceDictionary modelInspectionTheme = Application.Current.Resources
            .MergedDictionaries
            .Single(dictionary => dictionary.Source?.OriginalString.EndsWith(
                "/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml",
                StringComparison.OrdinalIgnoreCase) == true);
        ResourceDictionary theme = Assert.IsInstanceOfType<ResourceDictionary>(
            modelInspectionTheme.ThemeDictionaries[themeName]);
        return theme[key];
    }

    private static T Find<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        Assert.IsInstanceOfType<T>(root.FindName(name));

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

    private sealed class ExpansionEventCapture
    {
        public bool? IsExpanded { get; private set; }

        public void Capture(object? sender, EventArgs eventArguments)
        {
            IsExpanded = (bool?)eventArguments
                .GetType()
                .GetProperty(
                    "IsExpanded",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)?
                .GetValue(eventArguments);
        }
    }
}
