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

    private static readonly string[] ExpectedBalancedFieldOrder =
    [
        "MODEL NAME",
        "PUBLISHER",
        "PARAMETERS",
        "MODEL TYPE",
        "DECLARED MAX CONTEXT",
        "FILE SIZE",
        "FORMAT",
        "QUANTISATION"
    ];

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ReadyCollapsed_UsesBalancedNaturalGeometryAndFieldOrder()
    {
        InspectionModelCard control = CreateDetailedControl(
            width: 840,
            isExpanded: false);
        Border detailed = Find<Border>(control, "DetailedView");
        Border compact = Find<Border>(control, "CompactView");
        FrameworkElement detailedHeader = Find<FrameworkElement>(control, "DetailedHeader");
        Grid metadataGrid = Find<Grid>(control, "MetadataGrid");
        Grid balancedOverviewGrid = Find<Grid>(control, "BalancedOverviewGrid");
        Grid modelFactGrid = Find<Grid>(control, "ModelFactGrid");
        TextBlock[] labels = MetadataLabels(control);
        TextBlock sectionTitle = Descendants(control)
            .OfType<TextBlock>()
            .Single(text => text.Text == "Model overview");
        (string Label, string Value)[] expectedFields =
        [
            ("MODEL NAME", "Safe model name"),
            ("PUBLISHER", "Not reported"),
            ("FORMAT", "GGUF"),
            ("QUANTISATION", "Q4_K_M"),
            ("PARAMETERS", "3.2B"),
            ("MODEL TYPE", "Not reported"),
            ("DECLARED MAX CONTEXT", "131,072 tokens"),
            ("FILE SIZE", "2.08 GB")
        ];
        FrameworkElement[] fields = MetadataFields(control);
        Grid overviewPanel = Find<Grid>(control, "ModelOverviewPanel");
        TextBlock overviewCopy = Find<TextBlock>(control, "ModelOverviewCopy");
        Border formatChip = Find<Border>(control, "OverviewFormatChip");
        Point titleOrigin = sectionTitle.TransformToVisual(detailed)
            .TransformPoint(new Point());
        Point overviewOrigin = overviewPanel.TransformToVisual(detailed)
            .TransformPoint(new Point());
        InspectionDisclosure disclosure = control.ActiveDisclosure!;
        Border disclosureSurface = Find<Border>(
            disclosure,
            "DisclosureCardSurface");
        FrameworkElement disclosureHeader = Find<FrameworkElement>(
            disclosure,
            "DisclosureToggleButton");

        Assert.AreEqual(840d, detailed.ActualWidth, 0.01, "model card width");
        Assert.AreEqual(0d, detailed.MinHeight, 0.01, "model card must size naturally");
        Assert.AreEqual(
            disclosureHeader.ActualHeight +
                disclosureSurface.BorderThickness.Top +
                disclosureSurface.BorderThickness.Bottom,
            disclosure.ActualHeight,
            1d,
            "collapsed disclosure must not retain a trailing minimum-height tail");
        Assert.AreEqual(840d, disclosureSurface.ActualWidth, 0.01d);
        Assert.AreEqual(new Thickness(1d), disclosureSurface.BorderThickness);
        Assert.AreEqual(12d, disclosureSurface.CornerRadius.TopLeft, 0.01d);
        Assert.AreEqual(Visibility.Collapsed, formatChip.Visibility,
            "the duplicated header format badge is retired from Option B");
        Assert.AreEqual(TextAlignment.Left, sectionTitle.TextAlignment);
        Assert.AreEqual(HorizontalAlignment.Left, sectionTitle.HorizontalAlignment);
        Assert.AreEqual(overviewOrigin.X + 18d, titleOrigin.X, 1d,
            "overview heading uses the approved left card inset");
        Assert.AreEqual(
            "The selected model information used by this inspection.",
            overviewCopy.Text);
        Assert.AreEqual(TextAlignment.Left, overviewCopy.TextAlignment);
        Assert.AreEqual(12d, detailed.CornerRadius.TopLeft, 0.01, "shared card radius");
        Assert.AreEqual(new Thickness(24d), compact.Padding, "compact view padding");
        Assert.AreEqual(new Thickness(18d, 14d, 18d, 12d),
            detailedHeader.Margin, "approved overview header inset");
        Assert.AreEqual(new Thickness(0d), metadataGrid.Margin,
            "macro layout owns its full 840px width");
        Assert.HasCount(2, balancedOverviewGrid.ColumnDefinitions);
        Assert.AreEqual(GridUnitType.Star,
            balancedOverviewGrid.ColumnDefinitions[0].Width.GridUnitType);
        Assert.AreEqual(GridUnitType.Star,
            balancedOverviewGrid.ColumnDefinitions[1].Width.GridUnitType);
        Assert.AreEqual(
            1.6d,
            balancedOverviewGrid.ColumnDefinitions[0].Width.Value /
                balancedOverviewGrid.ColumnDefinitions[1].Width.Value,
            0.01d,
            "the named wide overview grid owns the approved 1.6fr / 1fr split");
        Assert.HasCount(2, modelFactGrid.ColumnDefinitions);
        Assert.IsTrue(modelFactGrid.ColumnDefinitions.All(column =>
            column.Width.GridUnitType == GridUnitType.Star &&
            Math.Abs(column.Width.Value - 1d) <= 0.01d));
        CollectionAssert.AreEqual(
            ExpectedBalancedFieldOrder,
            labels.Select(label => label.Text).ToArray());
        CollectionAssert.AreEqual(
            ExpectedBalancedFieldOrder[..6],
            modelFactGrid.Children
                .OfType<Border>()
                .Select(field => Descendants(field)
                    .OfType<TextBlock>()
                    .First(text => ExpectedFieldOrder.Contains(
                        text.Text,
                        StringComparer.Ordinal)).Text)
                .ToArray(),
            "the primary facts keep their accessible source order in the dominant card");
        Assert.AreEqual(3, fields.Select(Grid.GetRow).Distinct().Count(),
            "wide Ready metadata uses three visual data rows");
        Assert.AreEqual(2, fields.Select(Grid.GetColumn).Distinct().Count(),
            "wide Ready facts use two columns while support values use their card column");
        FrameworkElement configurationPanel = Find<FrameworkElement>(
            control,
            "ModelConfigurationPanel");
        FrameworkElement resultSummaryPanel = Find<FrameworkElement>(
            control,
            "ModelResultSummaryPanel");
        Assert.AreEqual(0, Grid.GetRow(configurationPanel));
        Assert.AreEqual(0, Grid.GetColumn(configurationPanel));
        Assert.AreEqual(1, Grid.GetRow(resultSummaryPanel));
        Assert.AreEqual(0, Grid.GetColumn(resultSummaryPanel));
        TextBlock configurationHeading = Find<TextBlock>(
            control,
            "ModelConfigurationHeading");
        Assert.AreEqual("Model configuration", configurationHeading.Text);
        Assert.IsTrue(configurationHeading.ActualHeight > 0d);
        TextBlock formatLabel = Descendants(fields[2])
            .OfType<TextBlock>()
            .First(text => text.Text == "FORMAT");
        TextBlock resultSummary = Find<TextBlock>(control, "ReadyResultSummary");
        TextBlock resultBadge = Find<TextBlock>(control, "ReadyResultBadgeText");
        Point headingOrigin = configurationHeading
            .TransformToVisual(detailed)
            .TransformPoint(new Point());
        Point formatLabelOrigin = formatLabel
            .TransformToVisual(detailed)
            .TransformPoint(new Point());
        Assert.IsGreaterThanOrEqualTo(
            headingOrigin.Y + configurationHeading.ActualHeight,
            formatLabelOrigin.Y,
            "configuration heading does not obscure the format field");
        Assert.AreEqual(0, Grid.GetRow(fields[0]));
        Assert.AreEqual(0, Grid.GetColumn(fields[0]));
        Assert.AreEqual(0, Grid.GetRow(fields[1]));
        Assert.AreEqual(1, Grid.GetColumn(fields[1]));
        Assert.AreEqual(1, Grid.GetRow(fields[4]));
        Assert.AreEqual(0, Grid.GetColumn(fields[4]));
        Assert.AreEqual(1, Grid.GetRow(fields[5]));
        Assert.AreEqual(1, Grid.GetColumn(fields[5]));
        Assert.AreEqual(2, Grid.GetRow(fields[6]));
        Assert.AreEqual(0, Grid.GetColumn(fields[6]));
        Assert.AreEqual(2, Grid.GetRow(fields[7]));
        Assert.AreEqual(1, Grid.GetColumn(fields[7]));
        Assert.AreEqual(1, Grid.GetRow(fields[2]));
        Assert.AreEqual(0, Grid.GetColumn(fields[2]));
        Assert.AreEqual(2, Grid.GetRow(fields[3]));
        Assert.AreEqual(0, Grid.GetColumn(fields[3]));
        FrameworkElement[] factTiles =
        [
            fields[0],
            fields[1],
            fields[4],
            fields[5],
            fields[6],
            fields[7]
        ];
        Assert.IsTrue(factTiles.Cast<Border>().All(field =>
            field.BorderThickness == new Thickness(1d) &&
            Math.Abs(field.CornerRadius.TopLeft - 9d) <= 0.01d),
            "the six overview facts use independent soft rounded tiles");
        Point configurationOrigin = configurationPanel
            .TransformToVisual(detailed)
            .TransformPoint(new Point());
        double macroGap = configurationOrigin.X -
            (overviewOrigin.X + overviewPanel.ActualWidth);
        Assert.AreEqual(16d, macroGap, 1d, "approved 16px macro rhythm");
        Assert.AreEqual(
            1.6d,
            overviewPanel.ActualWidth / configurationPanel.ActualWidth,
            0.04d,
            "approved 1.6fr / 1fr overview composition");
        Point metadataOrigin = metadataGrid.TransformToVisual(detailed)
            .TransformPoint(new Point());
        Point disclosureOrigin = disclosure.TransformToVisual(detailed)
            .TransformPoint(new Point());
        Assert.AreEqual(
            12d,
            disclosureOrigin.Y - detailed.ActualHeight,
            1d,
            "inspection details are a separate sibling card");
        Assert.IsGreaterThanOrEqualTo(
            metadataOrigin.Y + metadataGrid.ActualHeight - 1d,
            disclosureOrigin.Y,
            "inspection disclosure remains below metadata surface");
        Assert.AreEqual("All 5 inspection checks passed", resultSummary.Text);
        Assert.AreEqual("INSPECTED", resultBadge.Text);
        Assert.IsTrue(resultSummary.ActualHeight > 0d);
        Assert.IsTrue(resultBadge.ActualHeight > 0d);
        Assert.IsTrue(labels.All(label => label.FontSize == 10d));
        Assert.AreEqual(18d, sectionTitle.FontSize, 0.01, "section title size");
        double expectedFactWidth = fields[0].ActualWidth;
        Thickness expectedFactPadding = ((Border)fields[0]).Padding;
        for (int index = 0; index < expectedFields.Length; index++)
        {
            TextBlock[] fieldText = Descendants(fields[index])
                .OfType<TextBlock>()
                .ToArray();
            Assert.HasCount(2, fieldText, expectedFields[index].Label);
            Assert.AreEqual(expectedFields[index].Label, fieldText[0].Text);
            Assert.AreEqual(expectedFields[index].Value, fieldText[1].Text);
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(fieldText[1].Text),
                expectedFields[index].Label);
            Assert.AreEqual(14d, fieldText[1].FontSize, 0.01);
            if (index is not (2 or 3))
            {
                Assert.AreEqual(expectedFactWidth, fields[index].ActualWidth, 1d,
                    $"{expectedFields[index].Label} width");
                Assert.AreEqual(expectedFactPadding, ((Border)fields[index]).Padding,
                    $"{expectedFields[index].Label} tile padding");
            }
            Assert.AreEqual(TextAlignment.Left, fieldText[0].TextAlignment);
            Assert.AreEqual(
                index is 2 or 3 ? TextAlignment.Right : TextAlignment.Left,
                fieldText[1].TextAlignment);
            Assert.AreEqual(0, fieldText[1].MaxLines);
            Assert.AreNotEqual(TextWrapping.NoWrap, fieldText[1].TextWrapping);
            Assert.IsTrue(
                fieldText[1].ActualHeight +
                    fieldText[1].Margin.Top +
                    fieldText[1].Margin.Bottom +
                    1d >= fieldText[1].DesiredSize.Height,
                $"{expectedFields[index].Label} value must not be clipped; " +
                $"actual={fieldText[1].ActualWidth}x{fieldText[1].ActualHeight}, " +
                $"desired={fieldText[1].DesiredSize.Width}x{fieldText[1].DesiredSize.Height}");
        }

        control.Presentation = CreatePresentation(
            isExpanded: false,
            badgeState: InspectionModelBadgeState.Incomplete);
        ArrangeUntilStable(control, 840d);
        Assert.AreEqual(
            "WarningBadgeState",
            CurrentVisualState(
                Find<FrameworkElement>(control, "LayoutRoot"),
                "BadgeToneStates"),
            "the existing warning presentation uses the same balanced anatomy");
        Assert.AreSame(balancedOverviewGrid, control.FindName("BalancedOverviewGrid"));
        Assert.AreSame(modelFactGrid, control.FindName("ModelFactGrid"));
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
            Assert.AreEqual(24d, viewportLeftInset, 0.01, "report inner left inset");
            Assert.AreEqual(24d, viewportRightInset, 0.01, "report inner right inset");
            Assert.AreEqual(expectedViewportWidth, viewport.ActualWidth, 0.01, "nested report inner width");
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

            FrameworkElement configurationPanel = Find<FrameworkElement>(
                control,
                "ModelConfigurationPanel");
            FrameworkElement resultSummaryPanel = Find<FrameworkElement>(
                control,
                "ModelResultSummaryPanel");
            TextBlock configurationHeading = Find<TextBlock>(
                control,
                "ModelConfigurationHeading");
            TextBlock formatLabel = Descendants(
                    Find<FrameworkElement>(control, "FormatField"))
                .OfType<TextBlock>()
                .Single(text => text.Text == "FORMAT");
            TextBlock resultSummary = Find<TextBlock>(
                control,
                "ReadyResultSummary");
            TextBlock resultBadge = Find<TextBlock>(
                control,
                "ReadyResultBadgeText");
            configurationHeading.FontSize *= 2d;
            formatLabel.FontSize *= 2d;
            resultSummary.FontSize *= 2d;
            resultBadge.FontSize *= 2d;
            control.UpdateLayout();

            Point headingOrigin = configurationHeading
                .TransformToVisual(detailed)
                .TransformPoint(new Point());
            Point formatLabelOrigin = formatLabel
                .TransformToVisual(detailed)
                .TransformPoint(new Point());
            Assert.IsGreaterThanOrEqualTo(
                headingOrigin.Y + configurationHeading.ActualHeight,
                formatLabelOrigin.Y,
                "maximum-safe configuration heading remains above the format label at 200% text");
            Point headingPanelOrigin = configurationHeading
                .TransformToVisual(configurationPanel)
                .TransformPoint(new Point());
            Assert.IsGreaterThanOrEqualTo(
                -1d,
                headingPanelOrigin.X,
                "maximum-safe configuration heading remains within its panel at 200% text");
            Assert.IsGreaterThanOrEqualTo(
                -1d,
                headingPanelOrigin.Y,
                "maximum-safe configuration heading remains within its panel vertically at 200% text");
            Assert.IsTrue(
                configurationHeading.DesiredSize.Height <=
                    configurationHeading.ActualHeight +
                    configurationHeading.Margin.Top +
                    configurationHeading.Margin.Bottom +
                    1d,
                "maximum-safe configuration heading desired height fits its arranged bounds at 200% text");
            Assert.IsTrue(
                headingPanelOrigin.X + configurationHeading.ActualWidth <=
                    configurationPanel.ActualWidth + 1d &&
                headingPanelOrigin.Y + configurationHeading.ActualHeight <=
                    configurationPanel.ActualHeight + 1d,
                "maximum-safe configuration heading remains within its panel bounds at 200% text");
            AssertTextFits(
                resultSummary,
                resultSummaryPanel,
                "maximum-safe result summary at 200% text",
                requiresEmergencyWrap: false);
            AssertTextFits(
                resultBadge,
                resultSummaryPanel,
                "maximum-safe result badge at 200% text",
                requiresEmergencyWrap: false);
            Rect summaryBounds = resultSummary
                .TransformToVisual(resultSummaryPanel)
                .TransformBounds(new Rect(
                    0d,
                    0d,
                    resultSummary.ActualWidth,
                    resultSummary.ActualHeight));
            Rect badgeBounds = resultBadge
                .TransformToVisual(resultSummaryPanel)
                .TransformBounds(new Rect(
                    0d,
                    0d,
                    resultBadge.ActualWidth,
                    resultBadge.ActualHeight));
            Rect summaryBadgeIntersection = RectHelper.Intersect(
                summaryBounds,
                badgeBounds);
            Assert.IsTrue(
                summaryBadgeIntersection.IsEmpty ||
                summaryBadgeIntersection.Width <= 1d ||
                summaryBadgeIntersection.Height <= 1d,
                "maximum-safe result summary and badge do not overlap at 200% text");

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
    [DataRow(888d, 3)]
    [DataRow(600d, 4)]
    [DataRow(599d, 8)]
    public async Task Metadata_ReflowsAtApprovedResponsiveBreakpoints(
        double clientWidth,
        int expectedRowCount)
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
            int approvedAttachedRowCount = clientWidth >= 600d ? 3 : 6;
            int approvedVisualRowCount = clientWidth >= 600d ? 5 : 8;
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ResizeClientAndWaitAsync(
                window,
                control,
                effectiveWidth: 1000,
                expectedRowCount: 3);
            await ResizeClientAndWaitAsync(
                window,
                control,
                clientWidth,
                approvedAttachedRowCount);

            Border detailed = Find<Border>(control, "DetailedView");
            double[] rows = MetadataFields(control)
                .Select(field => field
                    .TransformToVisual(detailed)
                    .TransformPoint(new Point()).Y)
                .Select(value => Math.Round(value, 1))
                .Distinct()
                .ToArray();

            Assert.HasCount(
                approvedVisualRowCount,
                rows,
                $"metadata rows at {clientWidth}px: {string.Join(", ", rows)}");
            Assert.IsTrue(expectedRowCount is 3 or 4 or 8,
                "the existing responsive discovery inputs remain unchanged");

            FrameworkElement[] metadataFields = MetadataFields(control);
            FrameworkElement configurationPanel = Find<FrameworkElement>(
                control,
                "ModelConfigurationPanel");
            FrameworkElement resultSummaryPanel = Find<FrameworkElement>(
                control,
                "ModelResultSummaryPanel");
            Grid overviewPanel = Find<Grid>(control, "ModelOverviewPanel");
            Grid supportStack = Find<Grid>(control, "ModelSupportStack");
            Grid factGrid = Find<Grid>(control, "ModelFactGrid");
            if (clientWidth >= 888d)
            {
                Assert.AreEqual(0, Grid.GetRow(metadataFields[0]));
                Assert.AreEqual(0, Grid.GetColumn(metadataFields[0]));
                Assert.AreEqual(1, Grid.GetRow(metadataFields[4]));
                Assert.AreEqual(2, Grid.GetRow(metadataFields[6]));
                Assert.AreEqual(0, Grid.GetRow(configurationPanel));
                Assert.AreEqual(0, Grid.GetRow(supportStack));
                Assert.AreEqual(1, Grid.GetColumn(supportStack));
                Assert.AreEqual(1, Grid.GetRow(resultSummaryPanel));
            }
            else
            {
                Assert.AreEqual(1, Grid.GetRow(supportStack));
                Assert.AreEqual(0, Grid.GetColumn(supportStack));
                Point overviewOrigin = overviewPanel
                    .TransformToVisual(detailed)
                    .TransformPoint(new Point());
                Point supportOrigin = supportStack
                    .TransformToVisual(detailed)
                    .TransformPoint(new Point());
                Assert.IsGreaterThan(
                    overviewOrigin.Y + overviewPanel.ActualHeight,
                    supportOrigin.Y,
                    $"the support cards stack after the dominant overview card; " +
                    $"overviewOrigin={overviewOrigin.Y}, " +
                    $"overviewHeight={overviewPanel.ActualHeight}, " +
                    $"supportOrigin={supportOrigin.Y}, " +
                    $"supportMarginTop={supportStack.Margin.Top}");
                Point configurationOrigin = configurationPanel
                    .TransformToVisual(detailed)
                    .TransformPoint(new Point());
                Point resultOrigin = resultSummaryPanel
                    .TransformToVisual(detailed)
                    .TransformPoint(new Point());
                Assert.IsGreaterThan(
                    configurationOrigin.Y + configurationPanel.ActualHeight,
                    resultOrigin.Y,
                    "result summary follows configuration");
                if (clientWidth >= 600d)
                {
                    Assert.AreEqual(Grid.GetRow(metadataFields[0]),
                        Grid.GetRow(metadataFields[1]));
                    Assert.AreEqual(0, Grid.GetColumn(metadataFields[0]));
                    Assert.AreEqual(1, Grid.GetColumn(metadataFields[1]));
                    Assert.AreEqual(GridUnitType.Star,
                        factGrid.ColumnDefinitions[1].Width.GridUnitType);
                }
                else
                {
                    CollectionAssert.AreEqual(
                        Enumerable.Range(0, 6).ToArray(),
                        new[] { 0, 1, 4, 5, 6, 7 }
                            .Select(index => Grid.GetRow(metadataFields[index]))
                            .ToArray());
                    Assert.IsTrue(new[] { 0, 1, 4, 5, 6, 7 }.All(index =>
                        Grid.GetColumn(metadataFields[index]) == 0));
                    Assert.AreEqual(0d,
                        factGrid.ColumnDefinitions[1].ActualWidth,
                        0.01d);
                }
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
                    expectedRowCount: 6);
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
                0.01,
                "compact card uses its realized content, shared padding, and border thickness");
            Assert.AreEqual(Visibility.Collapsed, formatTile.Visibility,
                "the approved compact card has no visible format tile");
            Assert.AreEqual(0d, compactGrid.ColumnDefinitions[0].ActualWidth, 0.01d,
                "the retired format-tile column leaves no blank leading space");
            Assert.AreEqual(0, Grid.GetRow(summaryPanel));
            Assert.AreEqual(0, Grid.GetRow(badge));
            Assert.IsLessThan(
                badge.TransformToVisual(compact).TransformPoint(new Point()).X,
                summaryPanel.TransformToVisual(compact).TransformPoint(new Point()).X,
                "model summary precedes the status pill in one horizontal row");
            Assert.AreEqual(34d, badge.ActualHeight, 0.01);
            Assert.AreEqual(
                "InspectedBadgeState",
                CurrentVisualState(
                    Find<FrameworkElement>(control, "LayoutRoot"),
                    "BadgeToneStates"));
            Assert.AreSame(
                ThemeResource("Light", "InspectionSuccessSurfaceBrush"),
                badge.Background);

            await ChangeThemeAsync(control, ElementTheme.Dark);

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
                expectedRowCount: 6);
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
                (InspectionModelBadgeState.ModelSelected, "MODEL SELECTED"),
                (InspectionModelBadgeState.Inspected, "INSPECTED"),
                (InspectionModelBadgeState.SourceModel, "SOURCE MODEL"),
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
                    badge,
                    () => badgeText.Text == badgeCase.Text &&
                        badgeText.ActualWidth > 0d &&
                        badgeText.ActualHeight > 0d &&
                        badgeText.DesiredSize.Width <=
                            badgeText.ActualWidth + 1d &&
                        badgeText.DesiredSize.Height <=
                            badgeText.ActualHeight + 1d);

                Assert.AreEqual(badgeCase.Text, badgeText.Text);
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
        var window = new Window { Content = control };
        window.Activate();

        try
        {
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ResizeClientAndWaitAsync(
                window,
                control,
                effectiveWidth: 888,
                expectedRowCount: 3);
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
        string expectedState = effectiveWidth >= 888d
            ? "WideModelState"
            : effectiveWidth >= 600d
                ? "MediumModelState"
                : "NarrowModelState";
        var layoutReached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteWhenReady(object? sender, object eventArguments)
        {
            int actualRowCount = MetadataFields(element)
                .Select(Grid.GetRow)
                .Distinct()
                .Count();
            if (Math.Abs(element.XamlRoot.Size.Width - effectiveWidth) <= 1d &&
                actualRowCount == expectedRowCount &&
                CurrentVisualState(
                    Find<FrameworkElement>(element, "LayoutRoot"),
                    "ResponsiveModelStates") == expectedState)
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
            for (int pass = 0; pass < 3; pass++)
            {
                element.UpdateLayout();
                await Task.Yield();
            }
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
        Func<bool> condition)
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
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
