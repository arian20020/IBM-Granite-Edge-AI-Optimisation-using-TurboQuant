using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.Foundation;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionRenderedStateTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(1, InspectionOutcomePresentationKind.Hidden, InspectionOutcomeTone.Neutral, InspectionModelBadgeState.ModelSelected, InspectionContentCardMode.Progress, "", "")]
    [DataRow(2, InspectionOutcomePresentationKind.Ready, InspectionOutcomeTone.Success, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Hidden, "InspectionSuccessSurfaceBrush", "InspectionSuccessBorderBrush")]
    [DataRow(3, InspectionOutcomePresentationKind.Ready, InspectionOutcomeTone.Success, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Hidden, "InspectionSuccessSurfaceBrush", "InspectionSuccessBorderBrush")]
    [DataRow(4, InspectionOutcomePresentationKind.ReadyWithWarnings, InspectionOutcomeTone.Warning, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Warnings, "InspectionWarningSurfaceBrush", "InspectionWarningBorderBrush")]
    [DataRow(5, InspectionOutcomePresentationKind.ReadyWithWarnings, InspectionOutcomeTone.Warning, InspectionModelBadgeState.Inspected, InspectionContentCardMode.Warnings, "InspectionWarningSurfaceBrush", "InspectionWarningBorderBrush")]
    [DataRow(6, InspectionOutcomePresentationKind.ConversionRequired, InspectionOutcomeTone.Information, InspectionModelBadgeState.SourceModel, InspectionContentCardMode.ConversionRequired, "InspectionBlueSurfaceBrush", "InspectionBlueBorderStrongBrush")]
    [DataRow(7, InspectionOutcomePresentationKind.ConversionRequired, InspectionOutcomeTone.Information, InspectionModelBadgeState.SourceModel, InspectionContentCardMode.ConversionRequired, "InspectionBlueSurfaceBrush", "InspectionBlueBorderStrongBrush")]
    [DataRow(8, InspectionOutcomePresentationKind.IncompletePackage, InspectionOutcomeTone.Warning, InspectionModelBadgeState.Incomplete, InspectionContentCardMode.IncompletePackage, "InspectionWarningSurfaceBrush", "InspectionWarningBorderBrush")]
    [DataRow(9, InspectionOutcomePresentationKind.Unsupported, InspectionOutcomeTone.Error, InspectionModelBadgeState.Unsupported, InspectionContentCardMode.Unsupported, "InspectionErrorSurfaceBrush", "InspectionErrorBorderBrush")]
    [DataRow(10, InspectionOutcomePresentationKind.Invalid, InspectionOutcomeTone.Error, InspectionModelBadgeState.Invalid, InspectionContentCardMode.Invalid, "InspectionErrorSurfaceBrush", "InspectionErrorBorderBrush")]
    [DataRow(11, InspectionOutcomePresentationKind.Invalid, InspectionOutcomeTone.Error, InspectionModelBadgeState.Invalid, InspectionContentCardMode.Invalid, "InspectionErrorSurfaceBrush", "InspectionErrorBorderBrush")]
    [DataRow(12, InspectionOutcomePresentationKind.Cancelled, InspectionOutcomeTone.Neutral, InspectionModelBadgeState.NotInspected, InspectionContentCardMode.Cancelled, "InspectionSurfaceMutedBrush", "InspectionBorderMutedBrush")]
    [DataRow(13, InspectionOutcomePresentationKind.OperationalFailure, InspectionOutcomeTone.Error, InspectionModelBadgeState.ResultUnknown, InspectionContentCardMode.OperationalFailure, "InspectionErrorSurfaceBrush", "InspectionErrorBorderBrush")]
    public async Task AllThirteenStates_RenderExactSemanticsTokensAndGeometry(
        int stateValue,
        InspectionOutcomePresentationKind expectedOutcome,
        InspectionOutcomeTone expectedTone,
        InspectionModelBadgeState expectedBadge,
        InspectionContentCardMode expectedContentMode,
        string outcomeSurfaceResource,
        string outcomeBorderResource)
    {
        ModelInspectionFigmaState state = (ModelInspectionFigmaState)stateValue;
        ModelInspectionPagePresentation presentation =
            ModelInspectionVisualTestScenario.CreatePresentation(state);
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(page, presentation);

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 1440,
            height: 1024);
        RenderedFrame frame = await host.CaptureAsync();
        page.UpdateLayout();

        FrameworkElement contentHost = Element<FrameworkElement>(
            page,
            "InspectionContentHost");
        InspectionOutcomeCard outcome = Element<InspectionOutcomeCard>(
            page,
            "InspectionOutcomeCardControl");
        InspectionModelCard model = Element<InspectionModelCard>(
            page,
            "InspectionModelCardControl");
        InspectionContentCard content = Element<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        InspectionActionCard actions = Element<InspectionActionCard>(
            page,
            "InspectionActionCardControl");
        Point contentOrigin = contentHost.TransformToVisual(page)
            .TransformPoint(new Point());
        FrameworkElement header = Element<FrameworkElement>(page, "Header");

        Assert.AreEqual(state, presentation.State);
        Assert.AreEqual(expectedOutcome, outcome.Presentation.Kind);
        Assert.AreEqual(expectedTone, outcome.Presentation.Tone);
        Assert.AreEqual(expectedBadge, model.Presentation.BadgeState);
        Assert.AreEqual(expectedContentMode, content.Presentation.Mode);
        Assert.AreEqual(presentation.ActionCard, actions.Presentation);
        Assert.AreEqual(1440, frame.Width);
        Assert.AreEqual(1024, frame.Height);
        Assert.AreEqual(1440d, page.ActualWidth, 1d);
        Assert.AreEqual(1024d, page.ActualHeight, 1d);
        Assert.AreEqual(1440d, page.XamlRoot.Size.Width, 1d);
        Assert.AreEqual(1024d, page.XamlRoot.Size.Height, 1d);
        Assert.AreEqual(840d, contentHost.ActualWidth, 1d);
        Assert.AreEqual(300d, contentOrigin.X, 1d);
        AssertBrushColor(
            "InspectionSurfaceBrush",
            Element<Grid>(page, "LayoutRoot").Background);

        Assert.AreEqual(840d, model.ActualWidth, 1d, $"{state} model width");
        if (expectedContentMode != InspectionContentCardMode.Hidden)
        {
            Assert.AreEqual(840d, content.ActualWidth, 1d, $"{state} content width");
            Assert.IsGreaterThan(0d, content.ActualHeight, $"{state} content height");
        }
        else
        {
            Assert.AreEqual(0d, VisibleLayoutHeight(content), 0.01d);
        }

        if (expectedContentMode == InspectionContentCardMode.Progress)
        {
            Grid[] progressRows = Descendants(content)
                .OfType<Grid>()
                .Where(row => string.Equals(
                    row.Tag as string,
                    "InspectionProgressRow",
                    StringComparison.Ordinal))
                .ToArray();
            Assert.HasCount(5, progressRows);
            Assert.IsTrue(progressRows.All(row =>
                row.ActualHeight >= 48d));
            Assert.IsTrue(progressRows.All(row =>
                Math.Abs(row.ActualHeight - 48d) <= 1d));
        }

        FrameworkElement[] visibleCards =
        [
            outcome,
            model,
            content,
            actions
        ];
        visibleCards = visibleCards
            .Where(card => VisibleLayoutHeight(card) > 0d)
            .ToArray();
        Assert.IsNotEmpty(visibleCards);
        Assert.AreEqual(
            24d,
            VerticalGap(page, header, visibleCards[0]),
            1d,
            $"{state} header-to-card gap");
        for (int index = 1; index < visibleCards.Length; index++)
        {
            Assert.AreEqual(
                16d,
                VerticalGap(page, visibleCards[index - 1], visibleCards[index]),
                1d,
                $"{state} card gap {index}");
        }

        if (expectedOutcome == InspectionOutcomePresentationKind.Hidden)
        {
            Assert.AreEqual(0d, VisibleLayoutHeight(outcome), 0.01d);

            Button cancel = Element<Button>(actions, "CancelActionButton");
            Assert.IsGreaterThanOrEqualTo(44d, cancel.ActualHeight);
            Assert.AreEqual(184d, cancel.ActualWidth, 1d);
        }
        else
        {
            Border banner = Element<Border>(outcome, "OutcomeCardBorder");
            Point bannerOrigin = banner.TransformToVisual(page)
                .TransformPoint(new Point());
            Assert.AreEqual(300d, bannerOrigin.X, 1d, $"{state} banner x");
            Assert.AreEqual(840d, banner.ActualWidth, 1d, $"{state} banner width");
            Assert.AreEqual(82d, banner.ActualHeight, 1d, $"{state} banner height");
            AssertBrushColor(
                outcomeSurfaceResource,
                banner.Background,
                state.ToString());
            AssertBrushColor(
                outcomeBorderResource,
                banner.BorderBrush,
                state.ToString());
            Assert.AreEqual(
                presentation.OutcomeCard.AutomationName,
                AutomationProperties.GetName(outcome));

            Border resultView = Element<Border>(actions, "ResultView");
            Point resultOrigin = resultView.TransformToVisual(page)
                .TransformPoint(new Point());
            Assert.AreEqual(300d, resultOrigin.X, 1d, $"{state} action x");
            Assert.AreEqual(840d, resultView.ActualWidth, 1d, $"{state} action width");
            Assert.AreEqual(140d, resultView.ActualHeight, 1d, $"{state} action height");
        }

        AssertTypographyAndWrapping(page, model, content);
        AssertRenderedPalette(page, model, content, actions, presentation);
        AssertNestedGeometry(model, content, presentation);
        AssertBoundedScrollContract(model, content, presentation);
        AssertVisibleControlsHaveApprovedSize(actions, presentation);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(2, 3, "Model", 304d, 470d)]
    [DataRow(4, 5, "Content", 232d, 380d)]
    [DataRow(6, 7, "Content", 232d, 365d)]
    [DataRow(10, 11, "Content", 248d, 380d)]
    public async Task FourDisclosurePairs_RenderExactCollapsedAndExpandedEndpoints(
        int collapsedValue,
        int expandedValue,
        string disclosureOwner,
        double collapsedHeight,
        double expandedHeight)
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionPagePresentation collapsed =
            ModelInspectionVisualTestScenario.CreatePresentation(
                (ModelInspectionFigmaState)collapsedValue);
        ModelInspectionPagePresentation expanded =
            ModelInspectionVisualTestScenario.CreatePresentation(
                (ModelInspectionFigmaState)expandedValue);
        ModelInspectionVisualTestScenario.Apply(page, collapsed);

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 1440,
            height: 1024);
        await host.CaptureAsync();
        page.UpdateLayout();

        FrameworkElement owner = disclosureOwner == "Model"
            ? Element<InspectionModelCard>(page, "InspectionModelCardControl")
            : Element<InspectionContentCard>(page, "InspectionContentCardControl");
        Border banner = Element<Border>(
            Element<InspectionOutcomeCard>(page, "InspectionOutcomeCardControl"),
            "OutcomeCardBorder");
        FrameworkElement detailsSurface = disclosureOwner == "Model"
            ? Element<Border>(owner, "DetailedView")
            : Element<Border>(owner, "ContentCardShell");
        Border actionSurface = Element<Border>(
            Element<InspectionActionCard>(page, "InspectionActionCardControl"),
            "ResultView");
        InspectionDisclosure disclosure = disclosureOwner == "Model"
            ? ((InspectionModelCard)owner).ActiveDisclosure!
            : ((InspectionContentCard)owner).ActiveDisclosure!;
        Assert.IsNotNull(disclosure);
        Assert.IsFalse(disclosure.IsExpanded);
        Assert.AreEqual(Visibility.Collapsed, disclosure.ViewportTarget.Visibility);
        Assert.AreEqual(collapsedHeight, VisibleLayoutHeight(owner), 1d);
        Assert.AreEqual(840d, banner.ActualWidth, 1d, "collapsed banner width");
        Assert.AreEqual(840d, detailsSurface.ActualWidth, 1d, "collapsed details width");
        Assert.AreEqual(840d, actionSurface.ActualWidth, 1d, "collapsed action width");
        Assert.AreEqual(140d, actionSurface.ActualHeight, 1d, "collapsed action height");

        ModelInspectionVisualTestScenario.Apply(page, expanded);
        await host.CaptureAsync();
        page.UpdateLayout();

        InspectionDisclosure retainedDisclosure = disclosureOwner == "Model"
            ? ((InspectionModelCard)owner).ActiveDisclosure!
            : ((InspectionContentCard)owner).ActiveDisclosure!;
        Assert.AreSame(disclosure, retainedDisclosure);
        Assert.IsTrue(retainedDisclosure.IsExpanded);
        Assert.AreEqual(Visibility.Visible, retainedDisclosure.ViewportTarget.Visibility);
        Assert.AreEqual(1d, retainedDisclosure.ViewportTarget.Opacity, 0.001d);
        Assert.AreEqual(expandedHeight, VisibleLayoutHeight(owner), 1d);
        Assert.AreEqual(840d, banner.ActualWidth, 1d, "expanded banner width");
        Assert.AreEqual(840d, detailsSurface.ActualWidth, 1d, "expanded details width");
        Assert.AreEqual(840d, actionSurface.ActualWidth, 1d, "expanded action width");
        Assert.AreEqual(140d, actionSurface.ActualHeight, 1d, "expanded action height");

        ScrollViewer bounded = Element<ScrollViewer>(
            owner,
            disclosureOwner == "Model"
                ? "InspectionChecksScrollViewer"
                : "ExpandedReportScrollViewer");
        Assert.AreEqual(172d, bounded.MaxHeight, 0.01d);
        Assert.AreEqual(ScrollMode.Enabled, bounded.VerticalScrollMode);
        Assert.AreEqual(ScrollBarVisibility.Auto, bounded.VerticalScrollBarVisibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(1440, 300d, 840d, 0, 0, 0)]
    [DataRow(888, 24d, 840d, 0, 0, 0)]
    [DataRow(600, 24d, 552d, 0, 0, 1)]
    [DataRow(360, 16d, 328d, 0, 1, 2)]
    public async Task ResponsiveWidths_RenderApprovedHierarchy(
        int width,
        double expectedOrigin,
        double expectedContentWidth,
        int secondaryOneRow,
        int secondaryTwoRow,
        int primaryRow)
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(
            page,
            ModelInspectionVisualTestScenario.CreatePresentation(
                ModelInspectionFigmaState.ReadyWithWarningsCollapsed));

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width,
            height: 900);
        RenderedFrame frame = await host.CaptureAsync();
        page.UpdateLayout();

        FrameworkElement contentHost = Element<FrameworkElement>(
            page,
            "InspectionContentHost");
        InspectionActionCard actions = Element<InspectionActionCard>(
            page,
            "InspectionActionCardControl");
        Point origin = contentHost.TransformToVisual(page)
            .TransformPoint(new Point());
        FrameworkElement secondaryOne = Element<FrameworkElement>(
            actions,
            "SecondaryActionOneHost");
        FrameworkElement secondaryTwo = Element<FrameworkElement>(
            actions,
            "SecondaryActionTwoHost");
        FrameworkElement primary = Element<FrameworkElement>(
            actions,
            "PrimaryActionHost");

        Assert.AreEqual(width, frame.Width);
        Assert.AreEqual(900, frame.Height);
        Assert.AreEqual(width, page.ActualWidth, 1d);
        Assert.AreEqual(width, page.XamlRoot.Size.Width, 1d);
        Assert.AreEqual(expectedContentWidth, contentHost.ActualWidth, 1d);
        Assert.AreEqual(expectedOrigin, origin.X, 1d);
        Assert.AreEqual(secondaryOneRow, Grid.GetRow(secondaryOne));
        Assert.AreEqual(secondaryTwoRow, Grid.GetRow(secondaryTwo));
        Assert.AreEqual(primaryRow, Grid.GetRow(primary));
        Assert.AreSame(
            Element<InspectionOutcomeCard>(page, "InspectionOutcomeCardControl"),
            page.FindName("InspectionOutcomeCardControl"));
        Assert.AreSame(
            Element<ScrollViewer>(page, "InspectionPageScrollViewer"),
            page.FindName("InspectionPageScrollViewer"));
    }

    private static void AssertTypographyAndWrapping(
        FrameworkElement page,
        InspectionModelCard model,
        InspectionContentCard content)
    {
        TextBlock title = Element<TextBlock>(page, "PageTitle");
        TextBlock explanation = Element<TextBlock>(
            page,
            "ModelInspectionExplanation");
        Assert.AreSame(
            Application.Current.Resources["InspectionPageTitleFontFamily"],
            title.FontFamily);
        Assert.AreEqual(
            Application.Current.Resources["InspectionPageTitleFontWeight"],
            title.FontWeight);
        Assert.AreEqual(32d, title.FontSize, 0.01d);
        Assert.AreSame(
            Application.Current.Resources["InspectionBodyFontFamily"],
            explanation.FontFamily);
        Assert.AreEqual(
            Application.Current.Resources["InspectionBodyFontWeight"],
            explanation.FontWeight);
        Assert.AreEqual(14d, explanation.FontSize, 0.01d);

        TextBlock modelSectionTitle = model.Presentation.DisplayMode ==
            InspectionModelCardMode.Detailed
                ? Element<TextBlock>(model, "ModelOverviewTitle")
                : Descendants(Element<StackPanel>(
                        model,
                        "CompactModelSummaryPanel"))
                    .OfType<TextBlock>()
                    .First();
        Assert.AreEqual(18d, modelSectionTitle.FontSize, 0.01d);
        Assert.AreEqual(700, modelSectionTitle.FontWeight.Weight);

        TextBlock modelLabel = model.Presentation.DisplayMode ==
            InspectionModelCardMode.Detailed
                ? Descendants(Element<Border>(model, "OverviewFormatChip"))
                    .OfType<TextBlock>()
                    .Single()
                : Element<TextBlock>(model, "CompactStatusText");
        Assert.AreEqual(10d, modelLabel.FontSize, 0.01d);
        Assert.AreEqual(700, modelLabel.FontWeight.Weight);

        if (content.Presentation.Mode != InspectionContentCardMode.Hidden)
        {
            TextBlock contentSectionTitle = content.Presentation.Mode ==
                InspectionContentCardMode.Progress
                    ? Descendants(Element<Grid>(content, "ProgressView"))
                        .OfType<TextBlock>()
                        .Single(text => text.Text ==
                            content.Presentation.SectionTitle)
                    : Element<TextBlock>(content, "FindingsSectionTitle");
            Assert.AreEqual(18d, contentSectionTitle.FontSize, 0.01d);
            Assert.AreEqual(700, contentSectionTitle.FontWeight.Weight);
        }

        TextBlock[] visibleText = Descendants(page)
            .OfType<TextBlock>()
            .Where(text =>
                text.ActualWidth > 0d &&
                text.ActualHeight > 0d &&
                !string.IsNullOrWhiteSpace(text.Text))
            .ToArray();
        Assert.IsNotEmpty(visibleText);
        foreach (TextBlock text in visibleText)
        {
            string source = text.FontFamily?.Source ?? string.Empty;
            if (source.StartsWith(
                    "Segoe Fluent Icons",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (HasAncestor<InspectionStatusGlyph>(text))
            {
                continue;
            }

            if (string.Equals(
                    source,
                    "Cascadia Mono",
                    StringComparison.Ordinal))
            {
                Assert.AreEqual(12d, text.FontSize, 0.01d, text.Text);
                Assert.AreEqual(600, text.FontWeight.Weight, text.Text);
                Assert.AreEqual(TextWrapping.Wrap, text.TextWrapping, text.Text);
                continue;
            }

            StringAssert.Contains(source, "/Assets/Fonts/Inter-");
            Assert.IsTrue(
                text.FontSize is 10d or 12d or 14d or 18d or 32d,
                $"Unexpected Inter role size {text.FontSize}: {text.Text}");
            if (source.Contains("Inter-Bold.ttf", StringComparison.Ordinal))
            {
                Assert.AreEqual(700, text.FontWeight.Weight, text.Text);
            }
            else
            {
                StringAssert.Contains(source, "Inter-Regular.ttf");
                Assert.AreEqual(400, text.FontWeight.Weight, text.Text);
            }

            if (text.Text.Length >= 40)
            {
                Assert.AreNotEqual(
                    TextWrapping.NoWrap,
                    text.TextWrapping,
                    text.Text);
                Assert.AreEqual(0, text.MaxLines, text.Text);
            }
        }
    }

    private static void AssertNestedGeometry(
        InspectionModelCard model,
        InspectionContentCard content,
        ModelInspectionPagePresentation presentation)
    {
        FrameworkElement modelSurface = presentation.ModelCard.DisplayMode ==
            InspectionModelCardMode.Detailed
                ? Element<Border>(model, "DetailedView")
                : Element<Border>(model, "CompactView");
        Assert.AreEqual(840d, modelSurface.ActualWidth, 1d);

        if (presentation.ModelCard.DisplayMode == InspectionModelCardMode.Compact)
        {
            Border formatTile = Element<Border>(model, "CompactFormatTile");
            Border statusChip = Element<Border>(model, "CompactStatusChip");
            Point formatOrigin = formatTile.TransformToVisual(modelSurface)
                .TransformPoint(new Point());
            Point statusOrigin = statusChip.TransformToVisual(modelSurface)
                .TransformPoint(new Point());
            Assert.AreEqual(24d, formatOrigin.X, 1d);
            Assert.AreEqual(
                24d,
                modelSurface.ActualWidth - statusOrigin.X - statusChip.ActualWidth,
                1d);
        }

        if (presentation.ModelCard.IsInspectionDetailsExpanded)
        {
            FrameworkElement viewport = Element<FrameworkElement>(
                model,
                "InspectionDetailsViewport");
            Point viewportOrigin = viewport.TransformToVisual(modelSurface)
                .TransformPoint(new Point());
            Assert.AreEqual(24d, viewportOrigin.X, 1d);
            Assert.AreEqual(792d, viewport.ActualWidth, 1d);
        }

        if (presentation.ContentCard.Mode == InspectionContentCardMode.Hidden)
        {
            return;
        }

        Border contentSurface = Element<Border>(content, "ContentCardShell");
        Assert.AreEqual(840d, contentSurface.ActualWidth, 1d);
        FrameworkElement primaryRows = presentation.ContentCard.Mode ==
            InspectionContentCardMode.Progress
                ? Element<FrameworkElement>(content, "ProgressItemsRepeater")
                : Element<FrameworkElement>(content, "FindingsItemsRepeater");
        if (presentation.ContentCard.Mode == InspectionContentCardMode.Progress)
        {
            Grid progressView = Element<Grid>(content, "ProgressView");
            double expectedWidth = contentSurface.ActualWidth -
                contentSurface.BorderThickness.Left -
                contentSurface.BorderThickness.Right -
                progressView.Padding.Left -
                progressView.Padding.Right;
            Assert.AreEqual(expectedWidth, primaryRows.ActualWidth, 1d);
        }
        else
        {
            Assert.AreEqual(792d, primaryRows.ActualWidth, 1d);
        }

        if (presentation.ContentCard.Mode != InspectionContentCardMode.Progress)
        {
            Border[] collapsedRows = Descendants(primaryRows)
                .OfType<Border>()
                .Where(row => Math.Abs(row.MinHeight - 72d) < 0.01d)
                .ToArray();
            Assert.HasCount(presentation.ContentCard.Items.Count, collapsedRows);
            Assert.IsTrue(collapsedRows.All(row =>
                Math.Abs(row.ActualHeight - 72d) <= 1d));
        }

        if (presentation.ContentCard.IsExpanded)
        {
            FrameworkElement viewport = Element<FrameworkElement>(
                content,
                "ExpandedReportViewport");
            Point viewportOrigin = viewport.TransformToVisual(contentSurface)
                .TransformPoint(new Point());
            Assert.AreEqual(24d, viewportOrigin.X, 1d);
            Assert.AreEqual(792d, viewport.ActualWidth, 1d);
            Border[] expandedRows = Descendants(viewport)
                .OfType<Border>()
                .Where(row => Math.Abs(row.MinHeight - 58d) < 0.01d)
                .ToArray();
            Assert.HasCount(
                presentation.ContentCard.ExpandedItems.Count,
                expandedRows);
            Assert.IsTrue(expandedRows.All(row =>
                Math.Abs(row.ActualHeight - 58d) <= 1d));
        }
    }

    private static void AssertRenderedPalette(
        FrameworkElement page,
        InspectionModelCard model,
        InspectionContentCard content,
        InspectionActionCard actions,
        ModelInspectionPagePresentation presentation)
    {
        AssertBrushColor(
            "InspectionTextPrimaryBrush",
            Element<TextBlock>(page, "PageTitle").Foreground);
        AssertBrushColor(
            "InspectionTextSecondaryMutedBrush",
            Element<TextBlock>(page, "ModelInspectionExplanation").Foreground);

        Border modelSurface = presentation.ModelCard.DisplayMode ==
            InspectionModelCardMode.Detailed
                ? Element<Border>(model, "DetailedView")
                : Element<Border>(model, "CompactView");
        AssertBrushColor("InspectionSurfaceBrush", modelSurface.Background);
        AssertBrushColor("InspectionBorderMutedBrush", modelSurface.BorderBrush);

        if (presentation.ContentCard.Mode != InspectionContentCardMode.Hidden)
        {
            Border contentSurface = Element<Border>(content, "ContentCardShell");
            AssertBrushColor("InspectionSurfaceBrush", contentSurface.Background);
            AssertBrushColor(
                "InspectionBorderMutedBrush",
                contentSurface.BorderBrush);
        }

        if (presentation.ActionCard.Mode == InspectionActionCardMode.Result)
        {
            Border resultSurface = Element<Border>(actions, "ResultView");
            Assert.AreEqual(840d, resultSurface.ActualWidth, 1d);
            Assert.AreEqual(140d, resultSurface.ActualHeight, 1d);
            AssertBrushColor(
                "InspectionSurfaceMutedBrush",
                resultSurface.Background);
            AssertBrushColor(
                "InspectionBorderLightBrush",
                resultSurface.BorderBrush);
            Button primary = Element<Button>(actions, "PrimaryActionButton");
            AssertBrushColor("InspectionPrimaryBlueBrush", primary.Background);
            AssertBrushColor("InspectionPrimaryBlueBrush", primary.BorderBrush);
            AssertBrushColor("InspectionSurfaceBrush", primary.Foreground);
        }
        else
        {
            Button cancel = Element<Button>(actions, "CancelActionButton");
            AssertBrushColor("InspectionSurfaceBrush", cancel.Background);
            AssertBrushColor("InspectionBorderControlBrush", cancel.BorderBrush);
        }
    }

    private static void AssertBoundedScrollContract(
        InspectionModelCard model,
        InspectionContentCard content,
        ModelInspectionPagePresentation presentation)
    {
        if (presentation.ModelCard.IsInspectionDetailsExpanded)
        {
            ScrollViewer scroll = Element<ScrollViewer>(
                model,
                "InspectionChecksScrollViewer");
            Assert.AreEqual(172d, scroll.MaxHeight, 0.01d);
            Assert.IsTrue(scroll.IsTabStop);
        }

        if (presentation.ContentCard.IsExpanded)
        {
            ScrollViewer scroll = Element<ScrollViewer>(
                content,
                "ExpandedReportScrollViewer");
            Assert.AreEqual(172d, scroll.MaxHeight, 0.01d);
            Assert.IsTrue(scroll.IsTabStop);
        }
    }

    private static void AssertVisibleControlsHaveApprovedSize(
        InspectionActionCard actions,
        ModelInspectionPagePresentation presentation)
    {
        Button[] visibleButtons = Descendants(actions)
            .OfType<Button>()
            .Where(button =>
                button.Visibility == Visibility.Visible &&
                button.ActualWidth > 0d &&
                button.ActualHeight > 0d)
            .ToArray();
        Assert.IsNotEmpty(visibleButtons);
        Assert.IsTrue(visibleButtons.All(button => button.MinHeight >= 44d));
        Assert.IsTrue(visibleButtons.All(button =>
            Math.Abs(button.ActualHeight - 46d) <= 1d));
        if (presentation.ActionCard.Mode == InspectionActionCardMode.Result)
        {
            Assert.AreEqual(
                140d,
                Element<Border>(actions, "ResultView").ActualHeight,
                1d);
        }
    }

    private static double VerticalGap(
        FrameworkElement root,
        FrameworkElement upper,
        FrameworkElement lower)
    {
        Point upperOrigin = upper.TransformToVisual(root).TransformPoint(new Point());
        Point lowerOrigin = lower.TransformToVisual(root).TransformPoint(new Point());
        return lowerOrigin.Y - (upperOrigin.Y + upper.ActualHeight);
    }

    private static bool HasAncestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static void AssertBrushColor(
        string resourceKey,
        Brush? actual,
        string? context = null)
    {
        SolidColorBrush expected = Assert.IsInstanceOfType<SolidColorBrush>(
            LightThemeResource(resourceKey));
        SolidColorBrush observed = Assert.IsInstanceOfType<SolidColorBrush>(actual);
        Assert.AreEqual(expected.Color, observed.Color, context ?? resourceKey);
    }

    private static object LightThemeResource(string resourceKey)
    {
        ResourceDictionary modelInspectionTheme = Application.Current.Resources
            .MergedDictionaries
            .Single(dictionary => dictionary.Source?.OriginalString.EndsWith(
                "/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml",
                StringComparison.OrdinalIgnoreCase) == true);
        ResourceDictionary lightTheme =
            Assert.IsInstanceOfType<ResourceDictionary>(
                modelInspectionTheme.ThemeDictionaries["Light"]);
        return lightTheme[resourceKey];
    }

    private static double VisibleLayoutHeight(FrameworkElement element)
    {
        FrameworkElement layout = Element<FrameworkElement>(
            element,
            "LayoutRoot");
        return layout.Visibility == Visibility.Visible
            ? layout.ActualHeight
            : 0d;
    }

    internal static T Element<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        Assert.IsInstanceOfType<T>(root.FindName(name));

    internal static IEnumerable<DependencyObject> Descendants(
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

internal static class ModelInspectionVisualTestScenario
{
    internal static ModelInspectionPage CreatePage() =>
        new(new UnexpectedInspectionService())
        {
            RequestedTheme = ElementTheme.Light
        };

    internal static ModelInspectionPagePresentation CreatePresentation(
        ModelInspectionFigmaState state)
    {
        ModelInspectionViewSnapshot snapshot = state switch
        {
            ModelInspectionFigmaState.InspectionProgress =>
                ModelInspectionViewSnapshot.Initial,
            ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded => Terminal(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.Ready))),
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded => Terminal(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.ReadyWithWarnings))),
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded => Terminal(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.ConversionRequired))),
            ModelInspectionFigmaState.IncompletePackage => Terminal(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.IncompletePackage))),
            ModelInspectionFigmaState.Unsupported => Terminal(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.Unsupported))),
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded => Terminal(
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.Invalid))),
            ModelInspectionFigmaState.Cancelled => Terminal(
                ModelInspectionExecutionResult.Cancelled(cooperative: true)),
            ModelInspectionFigmaState.OperationalFailure => Terminal(
                ModelInspectionExecutionResult.OperationalFailure(
                    PresentationTestData.CreateFailure())),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        var rows = new InspectionProgressRows();
        if (snapshot.RenderKey.AttemptGeneration > 0)
        {
            rows.Reset(new ModelInspectionRenderKey(
                snapshot.RenderKey.AttemptGeneration,
                presentationRevision: 0));
        }

        return ModelInspectionPresentationFactory.Create(
            PresentationTestData.CreateRequest(),
            snapshot,
            new ModelInspectionPresentationCommands(
                PresentationTestData.CreateCommand(canExecute: false),
                PresentationTestData.CreateCommand(),
                PresentationTestData.CreateCommand()),
            IsExpanded(state),
            rows);
    }

    internal static void Apply(
        ModelInspectionPage page,
        ModelInspectionPagePresentation presentation)
    {
        InspectionOutcomeCard outcome =
            ModelInspectionRenderedStateTests.Element<InspectionOutcomeCard>(
                page,
                "InspectionOutcomeCardControl");
        InspectionModelCard model =
            ModelInspectionRenderedStateTests.Element<InspectionModelCard>(
                page,
                "InspectionModelCardControl");
        InspectionContentCard content =
            ModelInspectionRenderedStateTests.Element<InspectionContentCard>(
                page,
                "InspectionContentCardControl");
        InspectionActionCard actions =
            ModelInspectionRenderedStateTests.Element<InspectionActionCard>(
                page,
                "InspectionActionCardControl");

        outcome.Presentation = presentation.OutcomeCard;
        model.Presentation = presentation.ModelCard;
        content.Presentation = presentation.ContentCard;
        actions.Presentation = presentation.ActionCard;
        CompleteDisclosure(model, presentation.ModelCard.IsInspectionDetailsExpanded);
        CompleteDisclosure(content, presentation.ContentCard.IsExpanded);
        page.UpdateLayout();
    }

    private static void CompleteDisclosure(
        InspectionModelCard control,
        bool expanded)
    {
        if (control.ActiveDisclosure is null)
        {
            return;
        }

        control.PrepareDisclosureTarget(expanded);
        control.CompleteDisclosureTarget(expanded);
    }

    private static void CompleteDisclosure(
        InspectionContentCard control,
        bool expanded)
    {
        if (control.ActiveDisclosure is null)
        {
            return;
        }

        control.PrepareDisclosureTarget(expanded);
        control.CompleteDisclosureTarget(expanded);
    }

    private static ModelInspectionViewSnapshot Terminal(
        ModelInspectionExecutionResult execution) =>
        new(
            new ModelInspectionRenderKey(
                attemptGeneration: 1,
                presentationRevision: 1),
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: execution);

    private static bool IsExpanded(ModelInspectionFigmaState state) => state is
        ModelInspectionFigmaState.ReadyExpanded or
        ModelInspectionFigmaState.ReadyWithWarningsExpanded or
        ModelInspectionFigmaState.ConversionRequiredExpanded or
        ModelInspectionFigmaState.InvalidExpanded;

    private sealed class UnexpectedInspectionService : IModelInspectionService
    {
        public Task<ModelInspectionExecutionResult> InspectAsync(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The rendered-state fixture must never start model inspection.");
    }
}
