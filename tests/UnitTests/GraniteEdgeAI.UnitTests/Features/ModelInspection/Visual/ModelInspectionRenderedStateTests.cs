using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Views;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI;
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
    [TestMethod]
    [DataRow(0, "Checking the model package.")]
    [DataRow(1, "Reading model configuration.")]
    [DataRow(2, "Validating tokenizer and chat setup.")]
    [DataRow(3, "Validating model structure.")]
    [DataRow(4, "Confirming core runtime compatibility.")]
    public void FastCompletedRow_DisplaysActiveCopyUntilVisibleCompletion(
        int stageIndex, string expectedActiveDetail)
    {
        var row = new InspectionProgressRows().Items[stageIndex];
        const string completedDetail = "Completed inspection check.";
        row.Status = InspectionContentStatus.Passed;
        row.IsActive = false;
        row.Detail = completedDetail;

        Assert.AreEqual(string.Empty,
            ModelInspectionPreviewProjection.ProjectDisplayedStageDetail(row, current: false, settled: false));
        Assert.AreEqual(expectedActiveDetail,
            ModelInspectionPreviewProjection.ProjectDisplayedStageDetail(row, current: true, settled: false));
        Assert.AreEqual(completedDetail,
            ModelInspectionPreviewProjection.ProjectDisplayedStageDetail(row, current: false, settled: true));
        Assert.AreEqual(completedDetail, row.Detail,
            "Visible pacing must not overwrite authoritative completion evidence.");

        row.Status = InspectionContentStatus.Active;
        row.IsActive = true;
        row.Detail = "Reading supported model metadata.";
        Assert.AreEqual(row.Detail,
            ModelInspectionPreviewProjection.ProjectDisplayedStageDetail(row, current: true, settled: false),
            "A current active worker detail must remain intact.");
    }

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
            width: 1000,
            height: 700);
        RenderedFrame frame = await host.CaptureAsync();
        page.UpdateLayout();
        double rasterizationScale = page.XamlRoot.RasterizationScale;

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
        Assert.AreEqual(
            (int)Math.Round(1000d * rasterizationScale),
            frame.Width);
        Assert.AreEqual(
            (int)Math.Round(700d * rasterizationScale),
            frame.Height);
        Assert.AreEqual(1000d, page.ActualWidth, 1d);
        Assert.AreEqual(700d, page.ActualHeight, 1d);
        Assert.AreEqual(1000d, page.XamlRoot.Size.Width, 1d);
        Assert.AreEqual(700d, page.XamlRoot.Size.Height, 1d);
        Assert.AreEqual(840d, contentHost.ActualWidth, 1d);
        Assert.AreEqual(80d, contentOrigin.X, 1d);
        AssertBrushColor(
            "InspectionCanvasBrush",
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
            Border progressRowsSurface = Element<Border>(
                content,
                "ProgressRowsSurface");
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
            Assert.AreEqual(new Thickness(1d), progressRowsSurface.BorderThickness);
            Assert.AreEqual(10d, progressRowsSurface.CornerRadius.TopLeft, 0.01d);
            Assert.IsTrue(Descendants(progressRowsSurface)
                .OfType<InspectionStatusGlyph>()
                .Where(IsRendered)
                .All(glyph => Math.Abs(glyph.SurfaceSize - 22d) <= 0.01d));
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
            Border inspectingView = Element<Border>(actions, "InspectingCardSurface");
            Grid inspectingLayout = Element<Grid>(actions, "InspectingLayout");
            TextBlock reassurance = Element<TextBlock>(actions, "InspectingMessage");
            Assert.IsGreaterThanOrEqualTo(44d, cancel.ActualHeight);
            Assert.AreEqual(184d, cancel.ActualWidth, 1d);
            Assert.AreEqual(new Thickness(24d), inspectingView.Padding);
            Assert.AreEqual(new Thickness(1d), inspectingView.BorderThickness);
            Assert.AreEqual(12d, inspectingView.CornerRadius.TopLeft, 0.01d);
            Assert.IsTrue(string.IsNullOrEmpty(actions.Presentation.Title));
            Assert.AreEqual(2, inspectingLayout.RowDefinitions.Count);
            Assert.AreEqual(0, Grid.GetRow(reassurance));
            Assert.AreEqual(1, Grid.GetRow(cancel));
            Point reassuranceOrigin = reassurance.TransformToVisual(inspectingView)
                .TransformPoint(default);
            Point cancelOrigin = cancel.TransformToVisual(inspectingView)
                .TransformPoint(default);
            Assert.IsTrue(reassuranceOrigin.Y < cancelOrigin.Y,
                "the real progress reassurance appears before Cancel");
        }
        else
        {
            Border banner = Element<Border>(outcome, "OutcomeCardBorder");
            Point bannerOrigin = banner.TransformToVisual(page)
                .TransformPoint(new Point());
            Assert.AreEqual(80d, bannerOrigin.X, 1d, $"{state} banner x");
            Assert.AreEqual(840d, banner.ActualWidth, 1d, $"{state} banner width");
            Assert.AreEqual(0d, banner.MinHeight, 0.01d, $"{state} banner minimum");
            AssertCompactOutcome(outcome, banner, state.ToString());
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
            Assert.AreEqual(80d, resultOrigin.X, 1d, $"{state} action x");
            Assert.AreEqual(840d, resultView.ActualWidth, 1d, $"{state} action width");
            Assert.AreEqual(0d, resultView.MinHeight, 0.01d, $"{state} action minimum");
        }

        AssertTypographyAndWrapping(page, model, content);
        AssertRenderedPalette(page, model, content, actions, presentation);
        AssertNestedGeometry(model, content, presentation);
        AssertBoundedScrollContract(model, content, presentation);
        AssertVisibleControlsHaveApprovedSize(actions, presentation);
        await AssertResponsiveAccessibilityMatrixAsync(state);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(2, 3, "Model")]
    [DataRow(4, 5, "Content")]
    [DataRow(6, 7, "Content")]
    [DataRow(10, 11, "Content")]
    public async Task FourDisclosurePairs_RenderNaturalCollapsedAndBoundedExpandedEndpoints(
        int collapsedValue,
        int expandedValue,
        string disclosureOwner)
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
            width: 1000,
            height: 700);
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
        Border disclosureSurface = Element<Border>(
            disclosure,
            "DisclosureCardSurface");
        Assert.IsFalse(disclosure.IsExpanded);
        Assert.AreEqual(Visibility.Collapsed, disclosure.ViewportTarget.Visibility);
        FrameworkElement disclosureHeader = Element<FrameworkElement>(
            disclosure,
            "DisclosureToggleButton");
        double collapsedHeight = VisibleLayoutHeight(owner);
        Assert.AreEqual(
            disclosureHeader.ActualHeight +
                disclosureSurface.BorderThickness.Top +
                disclosureSurface.BorderThickness.Bottom,
            disclosure.ActualHeight,
            1d,
            "collapsed disclosure equals its realized header");
        Assert.AreEqual(840d, banner.ActualWidth, 1d, "collapsed banner width");
        Assert.AreEqual(840d, detailsSurface.ActualWidth, 1d, "collapsed details width");
        Assert.AreEqual(840d, disclosureSurface.ActualWidth, 1d,
            "collapsed disclosure card width");
        Assert.AreEqual(new Thickness(1d), disclosureSurface.BorderThickness);
        Assert.AreEqual(12d, disclosureSurface.CornerRadius.TopLeft, 0.01d);
        Assert.AreEqual(840d, actionSurface.ActualWidth, 1d, "collapsed action width");
        Assert.AreEqual(0d, actionSurface.MinHeight, 0.01d, "collapsed action minimum");

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
        Assert.IsGreaterThan(collapsedHeight, VisibleLayoutHeight(owner));
        Assert.AreEqual(840d, banner.ActualWidth, 1d, "expanded banner width");
        Assert.AreEqual(840d, detailsSurface.ActualWidth, 1d, "expanded details width");
        Assert.AreEqual(840d, disclosureSurface.ActualWidth, 1d,
            "expanded disclosure card width");
        Assert.AreEqual(840d, actionSurface.ActualWidth, 1d, "expanded action width");
        Assert.AreEqual(0d, actionSurface.MinHeight, 0.01d, "expanded action minimum");

        ScrollViewer bounded = Element<ScrollViewer>(
            owner,
            disclosureOwner == "Model"
                ? "InspectionChecksScrollViewer"
                : "ExpandedReportScrollViewer");
        Assert.AreEqual(172d, bounded.MaxHeight, 0.01d);
        Assert.AreEqual(ScrollMode.Enabled, bounded.VerticalScrollMode);
        Assert.AreEqual(ScrollBarVisibility.Auto, bounded.VerticalScrollBarVisibility);

        await AssertResponsiveAccessibilityMatrixAsync(
            (ModelInspectionFigmaState)collapsedValue);
        await AssertResponsiveAccessibilityMatrixAsync(
            (ModelInspectionFigmaState)expandedValue);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(1000, 80d, 840d, 0, 0, 0)]
    [DataRow(888, 24d, 840d, 0, 0, 0)]
    [DataRow(600, 24d, 552d, 0, 0, 0)]
    [DataRow(360, 16d, 328d, 0, 0, 1)]
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
            height: 700);
        RenderedFrame frame = await host.CaptureAsync();
        page.UpdateLayout();
        double rasterizationScale = page.XamlRoot.RasterizationScale;

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

        Assert.AreEqual(
            (int)Math.Round(width * rasterizationScale),
            frame.Width);
        Assert.AreEqual(
            (int)Math.Round(700d * rasterizationScale),
            frame.Height);
        Assert.AreEqual(width, page.ActualWidth, 1d);
        Assert.AreEqual(width, page.XamlRoot.Size.Width, 1d);
        Assert.AreEqual(expectedContentWidth, contentHost.ActualWidth, 1d);
        Assert.AreEqual(expectedOrigin, origin.X, 1d);
        Assert.AreEqual(secondaryOneRow, Grid.GetRow(secondaryOne));
        Assert.AreEqual(Visibility.Collapsed, secondaryTwo.Visibility);
        Assert.AreEqual(primaryRow, Grid.GetRow(primary));
        Assert.AreSame(
            Element<InspectionOutcomeCard>(page, "InspectionOutcomeCardControl"),
            page.FindName("InspectionOutcomeCardControl"));
        Assert.AreSame(
            Element<ScrollViewer>(page, "InspectionPageScrollViewer"),
            page.FindName("InspectionPageScrollViewer"));
    }

    private static readonly double[] ResponsiveWidths =
        [1000d, 900d, 888d, 887d, 600d, 599d, 480d, 360d];

    private static readonly string DominatingModelName = new('M', 160);

    private static readonly string DominatingStageDetail = string.Concat(
        Enumerable.Repeat(
            "Inspection progress detail remains bounded for deterministic maximum-copy validation. ",
            7))[..512];

    private static readonly string DominatingOperationalFailureDetail =
        string.Concat(
            Enumerable.Repeat(
                "Operational failure detail remains bounded for deterministic maximum-copy validation. ",
                7))[..512];

    private static async Task AssertResponsiveAccessibilityMatrixAsync(
        ModelInspectionFigmaState state)
    {
        foreach (bool preview200 in new[] { false, true })
        {
            Action<ResourceDictionary>? configureResources = preview200
                ? ConfigureTwoHundredPercentPreviewResources
                : null;
            ModelInspectionPage page =
                ModelInspectionVisualTestScenario.CreatePage(configureResources);
            if (preview200)
            {
                page.RequestedTheme = ElementTheme.Dark;
            }

            ModelInspectionPagePresentation presentation =
                ModelInspectionVisualTestScenario.CreatePresentation(state);
            ModelInspectionVisualTestScenario.Apply(page, presentation);
            var loaded = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            page.Loaded += (_, _) => loaded.TrySetResult(true);
            var window = new Window { Content = page };
            try
            {
                window.Activate();
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                ApplyDominatingCopy(page, state);

                string[]? semanticActionOrder = null;
                string[]? semanticRegionOrder = null;
                string[]? logicalTabOrder = null;
                foreach (double width in ResponsiveWidths)
                {
                    await ResizeClientAndWaitAsync(window, page, width, 700d);
                    if (preview200 && width == ResponsiveWidths[0])
                    {
                        AssertTwoHundredPercentRoleSizes(page);
                    }
                    double layoutWidth = page.XamlRoot.Size.Width;
                    Assert.AreEqual(
                        width,
                        layoutWidth,
                        1d,
                        $"{state}/{preview200}/{width}: client width");
                    AssertResponsiveEndpoint(
                        page,
                        presentation,
                        state,
                        layoutWidth,
                        preview200,
                        ref semanticActionOrder,
                        ref semanticRegionOrder,
                        ref logicalTabOrder);
                    await AssertCompleteScrollableSurfacesAsync(
                        page,
                        state,
                        layoutWidth,
                        preview200);
                }
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }
    }

    private static void ConfigureTwoHundredPercentPreviewResources(
        ResourceDictionary resources)
    {
        resources["InspectionPageTitleFontSize"] = 64d;
        resources["InspectionSectionTitleFontSize"] = 36d;
        resources["InspectionBodyFontSize"] = 28d;
        resources["InspectionHelperFontSize"] = 24d;
        resources["InspectionLabelFontSize"] = 20d;
    }

    private static void AssertTwoHundredPercentRoleSizes(
        ModelInspectionPage page)
    {
        Assert.AreEqual(
            64d,
            Element<TextBlock>(page, "PageTitle").FontSize,
            0.01d,
            "200% page-title role");
        Assert.AreEqual(
            28d,
            Element<TextBlock>(page, "ModelInspectionExplanation").FontSize,
            0.01d,
            "200% body role");
        TextBlock[] realizedText = Descendants(page)
            .OfType<TextBlock>()
            .Where(text => text.ActualWidth > 0d && text.ActualHeight > 0d)
            .ToArray();
        foreach (double expected in new[] { 64d, 36d, 28d, 24d, 20d })
        {
            Assert.IsTrue(
                realizedText.Any(text => Math.Abs(text.FontSize - expected) <= 0.01d),
                $"The 200% preview did not realize the {expected} px role.");
        }
    }

    private static void ApplyDominatingCopy(
        ModelInspectionPage page,
        ModelInspectionFigmaState state)
    {
        var modelCard = Element<InspectionModelCard>(
            page,
            "InspectionModelCardControl");
        if (state is ModelInspectionFigmaState.InspectionProgress or
            ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded)
        {
            Assert.IsGreaterThanOrEqualTo(160, DominatingModelName.Length);
            SetLongestRenderedText(
                modelCard,
                DominatingModelName,
                modelCard.Presentation.ModelName);
        }

        if (state is ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded)
        {
            SetNamedFieldValue(
                modelCard,
                "PublisherField",
                "Not reported");
        }

        var contentCard = Element<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        if (state == ModelInspectionFigmaState.InspectionProgress)
        {
            string? currentStageDetail = contentCard.Presentation.Items
                .FirstOrDefault(item => item.IsActive)?.Detail;
            Assert.IsGreaterThanOrEqualTo(
                512,
                DominatingStageDetail.Length);
            SetLongestRenderedText(
                contentCard,
                DominatingStageDetail,
                currentStageDetail);
        }
        else if (state is ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded or
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded or
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded)
        {
            SetRoleCopy(
                contentCard,
                DominatingPrimaryCopy(state),
                Longest(contentCard.Presentation.Items.Select(item => item.Title)));
            SetRoleCopy(
                contentCard,
                DominatingSecondaryCopy(state),
                Longest(contentCard.Presentation.Items.Select(item => item.Detail)));
        }
        else if (state is ModelInspectionFigmaState.IncompletePackage or
            ModelInspectionFigmaState.Unsupported or
            ModelInspectionFigmaState.Cancelled or
            ModelInspectionFigmaState.OperationalFailure)
        {
            var outcomeCard = Element<InspectionOutcomeCard>(
                page,
                "InspectionOutcomeCardControl");
            SetRoleCopy(
                outcomeCard,
                DominatingOutcomeCopy(state),
                outcomeCard.Presentation.Message);
            SetRoleCopy(
                contentCard,
                DominatingSecondaryCopy(state),
                Longest(contentCard.Presentation.Items.Select(item => item.Detail)));
        }

        var actionCard = Element<InspectionActionCard>(
            page,
            "InspectionActionCardControl");
        SetRoleCopy(
            actionCard,
            DominatingActionCopy(state),
            Longest(CurrentActionCopy(actionCard.Presentation)));
    }

    private static string DominatingPrimaryCopy(
        ModelInspectionFigmaState state) => state switch
        {
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded =>
                "Chat template not reported",
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded =>
                "Conversion required",
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded =>
                "Structural validation did not produce a usable model result.",
            _ => "Model result unavailable"
        };

    private static string DominatingSecondaryCopy(
        ModelInspectionFigmaState state) => state switch
        {
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded =>
                "The model does not report a chat template. Chat formatting may require manual configuration.",
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded =>
                "Choose another model or review the expected converted output.",
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded =>
                "Choose another model.",
            ModelInspectionFigmaState.IncompletePackage =>
                "Locate the missing file or choose another model.",
            ModelInspectionFigmaState.Unsupported => "Choose another model.",
            ModelInspectionFigmaState.Cancelled =>
                "No model result was produced because inspection was cancelled.",
            ModelInspectionFigmaState.OperationalFailure =>
                DominatingOperationalFailureDetail,
            _ => string.Empty
        };

    private static string DominatingOutcomeCopy(
        ModelInspectionFigmaState state) => state switch
        {
            ModelInspectionFigmaState.IncompletePackage =>
                "The model package is missing required content.",
            ModelInspectionFigmaState.Unsupported =>
                "The selected model is not supported by this inspection route.",
            ModelInspectionFigmaState.Cancelled =>
                "No model result was produced because inspection was cancelled.",
            ModelInspectionFigmaState.OperationalFailure =>
                DominatingOperationalFailureDetail,
            _ => string.Empty
        };

    private static string DominatingActionCopy(
        ModelInspectionFigmaState state) => state switch
        {
            ModelInspectionFigmaState.InspectionProgress =>
                "Cancel inspection",
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded =>
                "Continue to hardware check",
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded =>
                "Choose conversion format",
            ModelInspectionFigmaState.ReadyCollapsed or ModelInspectionFigmaState.ReadyExpanded => "Choose another model",
            _ => "Choose another model"
        };

    private static IEnumerable<string?> CurrentActionCopy(
        InspectionActionCardPresentation presentation) =>
        new[]
        {
            presentation.CancelAction,
            presentation.SecondaryActionOne,
            presentation.SecondaryActionTwo,
            presentation.PrimaryAction
        }
        .Where(action => action.Visibility == Visibility.Visible)
        .Select(action => action.Text);

    private static void SetRoleCopy(
        FrameworkElement region,
        string? value,
        string? currentValue)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(currentValue))
        {
            SetLongestRenderedText(region, value, currentValue);
        }
    }

    private static void SetNamedFieldValue(
        FrameworkElement region,
        string fieldName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        FrameworkElement field = Element<FrameworkElement>(region, fieldName);
        TextBlock? target = Descendants(field)
            .OfType<TextBlock>()
            .Where(text => text.TextWrapping != TextWrapping.NoWrap)
            .LastOrDefault();
        Assert.IsNotNull(target, fieldName);
        target.Text = value;
    }

    private static string? Longest(IEnumerable<string?> copy) => copy
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .OrderByDescending(text => text!.Length)
        .FirstOrDefault();

    private static void SetLongestRenderedText(
        FrameworkElement region,
        string? value,
        string? currentValue = null)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsRendered(region))
        {
            return;
        }

        TextBlock? target = Descendants(region)
            .OfType<TextBlock>()
            .Where(text => IsRendered(text) &&
                text.TextWrapping != TextWrapping.NoWrap &&
                (currentValue is null || string.Equals(
                    text.Text,
                    currentValue,
                    StringComparison.Ordinal)))
            .OrderByDescending(text => text.Text.Length)
            .FirstOrDefault();
        if (target is not null)
        {
            target.Text = value;
        }
    }

    private static void AssertResponsiveEndpoint(
        ModelInspectionPage page,
        ModelInspectionPagePresentation presentation,
        ModelInspectionFigmaState state,
        double width,
        bool preview200,
        ref string[]? semanticActionOrder,
        ref string[]? semanticRegionOrder,
        ref string[]? logicalTabOrder)
    {
        FrameworkElement contentHost = Element<FrameworkElement>(
            page,
            "InspectionContentHost");
        FrameworkElement scrollContent = Element<FrameworkElement>(
            page,
            "InspectionScrollContent");
        Point origin = contentHost.TransformToVisual(page)
            .TransformPoint(default);
        double inset = width < 600d ? 16d : 24d;
        double availableWidth = scrollContent.ActualWidth;
        Assert.IsGreaterThan(0d, availableWidth);
        Assert.IsLessThanOrEqualTo(width + 1d, availableWidth);
        double expectedWidth = Math.Min(
            840d,
            availableWidth - (2d * inset));
        Assert.AreEqual(expectedWidth, contentHost.ActualWidth, 1d,
            $"{state}/{preview200}/{width}: host width");
        Assert.AreEqual((availableWidth - expectedWidth) / 2d, origin.X, 1d,
            $"{state}/{preview200}/{width}: host origin");

        var outcome = Element<InspectionOutcomeCard>(
            page,
            "InspectionOutcomeCardControl");
        var model = Element<InspectionModelCard>(
            page,
            "InspectionModelCardControl");
        var content = Element<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        var actions = Element<InspectionActionCard>(
            page,
            "InspectionActionCardControl");
        AssertHiddenCardMeasuresZero(
            outcome,
            presentation.OutcomeCard.Kind == InspectionOutcomePresentationKind.Hidden);
        AssertHiddenCardMeasuresZero(
            content,
            presentation.ContentCard.Mode == InspectionContentCardMode.Hidden);
        AssertHiddenCardMeasuresZero(
            actions,
            presentation.ActionCard.Mode == InspectionActionCardMode.Hidden);
        AssertFindingsSurfacesRespectCardInsets(
            content,
            presentation.ContentCard.Mode,
            state,
            preview200,
            width);
        AssertNarrowCardInsets(
            model,
            content,
            presentation,
            state,
            preview200,
            width);
        if (presentation.ContentCard.Mode == InspectionContentCardMode.Progress)
        {
            Assert.HasCount(5, presentation.ContentCard.ProgressRows.Items);
            Grid[] progressRows = Descendants(content)
                .OfType<Grid>()
                .Where(row => string.Equals(
                    row.Tag as string,
                    "InspectionProgressRow",
                    StringComparison.Ordinal))
                .ToArray();
            Assert.IsNotEmpty(
                progressRows,
                $"{state}/{preview200}/{width}: at least one virtualized progress row is realized");
            foreach (Grid row in progressRows)
            {
                Grid statusOwner = Descendants(row)
                    .OfType<Grid>()
                    .Single(element =>
                        Grid.GetColumn(element) == 2 &&
                        ReferenceEquals(VisualTreeHelper.GetParent(element), row));
                TextBlock fraction = Descendants(row)
                    .OfType<TextBlock>()
                    .Single(element =>
                        Grid.GetColumn(element) == 3 &&
                        ReferenceEquals(VisualTreeHelper.GetParent(element), row));
                StackPanel copy = Descendants(row)
                    .OfType<StackPanel>()
                    .Single(element =>
                        Grid.GetColumn(element) == 1 &&
                        ReferenceEquals(VisualTreeHelper.GetParent(element), row));
                Viewbox glyphHost = Descendants(row)
                    .OfType<Viewbox>()
                    .Single(element =>
                        Grid.GetColumn(element) == 0 &&
                        ReferenceEquals(VisualTreeHelper.GetParent(element), row));
                Assert.AreEqual(20d, glyphHost.ActualWidth, 0.01d);
                Assert.AreEqual(20d, glyphHost.ActualHeight, 0.01d);
                if (width >= 888d)
                {
                    Assert.AreEqual(0, Grid.GetRow(statusOwner));
                    Assert.AreEqual(0, Grid.GetRow(fraction));
                    Assert.AreEqual(1, Grid.GetColumnSpan(copy));
                    Assert.AreEqual(1, Grid.GetRowSpan(glyphHost));
                }
                else
                {
                    Grid rowStateHost = row.FindName("ProgressRowResponsiveHost") as Grid ??
                        throw new AssertFailedException(
                            "The progress-row responsive state host was not realized.");
                    string rowWidthState = VisualStateManager
                        .GetVisualStateGroups(rowStateHost)
                        .First(group => group.Name == "ProgressRowWidthStates")
                        .CurrentState?.Name ?? "<none>";
                    Assert.AreEqual(0, Grid.GetRow(statusOwner),
                        $"{state}/{preview200}/{width}: {rowWidthState}");
                    Assert.AreEqual(0, Grid.GetRow(fraction));
                    Assert.AreEqual(1, Grid.GetColumnSpan(copy));
                    Assert.AreEqual(1, Grid.GetRowSpan(glyphHost));
                    Rect copyBounds = Bounds(page, copy);
                    Rect statusBounds = Bounds(page, statusOwner);
                    Assert.AreEqual(
                        copyBounds.Y + (copyBounds.Height / 2d),
                        statusBounds.Y + (statusBounds.Height / 2d),
                        1d,
                        $"{state}/{preview200}/{width}: status and copy share a vertical centre");
                }

                Rect rowBounds = Bounds(page, row);
                foreach (FrameworkElement trailing in new FrameworkElement[]
                         {
                             statusOwner,
                             fraction
                         }.Where(IsRendered))
                {
                    Rect bounds = Bounds(page, trailing);
                    Assert.IsTrue(
                        bounds.X >= rowBounds.X - 1d &&
                        bounds.Right <= rowBounds.Right + 1d &&
                        bounds.Y >= rowBounds.Y - 1d &&
                        bounds.Bottom <= rowBounds.Bottom + 1d,
                        $"{state}/{preview200}/{width}: trailing progress evidence fits");
                }
            }
        }

        FrameworkElement pageTitle = Element<FrameworkElement>(page, "PageTitle");
        FrameworkElement explanation = Element<FrameworkElement>(
            page,
            "ModelInspectionExplanation");
        FrameworkElement[] semanticRegions =
        [
            pageTitle,
            explanation,
            outcome,
            model,
            content,
            actions
        ];
        DependencyObject[] visualOrder = Descendants(page).ToArray();
        string[] currentRegionOrder = semanticRegions
            .Where(IsRendered)
            .OrderBy(region => Array.IndexOf(visualOrder, region))
            .Select(region => region.Name)
            .ToArray();
        semanticRegionOrder ??= currentRegionOrder;
        CollectionAssert.AreEqual(
            semanticRegionOrder,
            currentRegionOrder,
            $"{state}/{preview200}/{width}: complete semantic region order");
        CollectionAssert.AreEqual(
            new[] { pageTitle.Name, explanation.Name },
            currentRegionOrder.Take(2).ToArray(),
            $"{state}/{preview200}/{width}: heading reading order");
        AssertNoPeerOverlap(
            page,
            [pageTitle, explanation],
            $"{state}/{preview200}/{width}: heading geometry");

        string[] currentTabOrder = Descendants(page)
            .OfType<Control>()
            .Where(control =>
                control.IsTabStop &&
                control.IsEnabled &&
                IsLogicallyVisible(control))
            .OrderBy(control => control.TabIndex)
            .Select(SemanticIdentity)
            .ToArray();
        logicalTabOrder ??= currentTabOrder;
        CollectionAssert.AreEqual(
            logicalTabOrder,
            currentTabOrder,
            $"{state}/{preview200}/{width}: complete logical tab order");

        if (presentation.ModelCard.DisplayMode == InspectionModelCardMode.Detailed)
        {
            FrameworkElement[] metadata =
            [
                Element<FrameworkElement>(model, "ModelNameField"),
                Element<FrameworkElement>(model, "PublisherField"),
                Element<FrameworkElement>(model, "FormatField"),
                Element<FrameworkElement>(model, "QuantisationField"),
                Element<FrameworkElement>(model, "ParametersField"),
                Element<FrameworkElement>(model, "ModelTypeField"),
                Element<FrameworkElement>(model, "DeclaredContextField"),
                Element<FrameworkElement>(model, "FileSizeField")
            ];
            int expectedColumns = width >= 888d
                ? 3
                : width >= 600d ? 2 : 1;
            Assert.AreEqual(
                expectedColumns,
                metadata.Select(Grid.GetColumn).Distinct().Count(),
                $"{state}/{preview200}/{width}: metadata columns");
            AssertNoPeerOverlap(page, metadata, $"{state}/{preview200}/{width}: metadata");
            AssertDetailPanelGeometry(
                page,
                model,
                Element<Border>(model, "DetailedView"),
                $"{state}/{preview200}/{width}: Ready detail panels");
        }

        Button[] visibleButtons = Descendants(page)
            .OfType<Button>()
            .Where(button => IsRendered(button))
            .ToArray();
        foreach (Button button in visibleButtons)
        {
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualWidth, button.Name);
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualHeight, button.Name);
            Assert.IsTrue(button.UseSystemFocusVisuals, button.Name);
        }

        FrameworkElement[] visibleActionHosts =
        [
            Element<FrameworkElement>(actions, "SecondaryActionOneHost"),
            Element<FrameworkElement>(actions, "SecondaryActionTwoHost"),
            Element<FrameworkElement>(actions, "PrimaryActionHost")
        ];
        visibleActionHosts = visibleActionHosts.Where(IsRendered).ToArray();
        if (visibleActionHosts.Length > 0)
        {
            if (width < 600d)
            {
                CollectionAssert.AreEqual(
                    Enumerable.Range(0, visibleActionHosts.Length).ToArray(),
                    visibleActionHosts.Select(Grid.GetRow).ToArray(),
                    $"{state}/{preview200}/{width}: narrow action stack");
            }
            else
            {
                Assert.AreEqual(
                    1,
                    visibleActionHosts.Select(Grid.GetRow).Distinct().Count(),
                    $"{state}/{preview200}/{width}: horizontal action row");
                Assert.IsTrue(visibleActionHosts.Skip(1).All(host =>
                    Math.Abs(host.ActualWidth - visibleActionHosts[0].ActualWidth) <= 1d),
                    $"{state}/{preview200}/{width}: equal action columns");
            }

            AssertNoPeerOverlap(
                page,
                visibleActionHosts,
                $"{state}/{preview200}/{width}: actions");
            string[] currentOrder = visibleActionHosts
                .SelectMany(host => Descendants(host).OfType<Button>())
                .Where(IsRendered)
                .OrderBy(button => button.TabIndex)
                .Select(button => button.Tag as string ?? button.Name)
                .ToArray();
            semanticActionOrder ??= currentOrder;
            CollectionAssert.AreEqual(
                semanticActionOrder,
                currentOrder,
                $"{state}/{preview200}/{width}: action semantics");
        }

        InspectionDisclosure? disclosure = model.ActiveDisclosure ??
            content.ActiveDisclosure;
        if (disclosure is not null)
        {
            FrameworkElement toggle = Element<FrameworkElement>(
                disclosure,
                "DisclosureToggleButton");
            Rect bounds = Bounds(page, toggle);
            Assert.IsGreaterThanOrEqualTo(44d, toggle.ActualHeight);
            Assert.IsGreaterThanOrEqualTo(-1d, bounds.X);
            Assert.IsLessThanOrEqualTo(width + 1d, bounds.Right);
        }

    }

    private static void AssertHiddenCardMeasuresZero(
        FrameworkElement card,
        bool hidden)
    {
        FrameworkElement root = Element<FrameworkElement>(card, "LayoutRoot");
        if (hidden)
        {
            Assert.AreEqual(0d, card.ActualHeight, 0.01d, card.Name);
            Assert.AreEqual(0d, root.ActualHeight, 0.01d, card.Name);
        }
    }

    private static void AssertNarrowCardInsets(
        InspectionModelCard model,
        InspectionContentCard content,
        ModelInspectionPagePresentation presentation,
        ModelInspectionFigmaState state,
        bool preview200,
        double width)
    {
        if (width >= 600d)
        {
            return;
        }

        Border compact = Element<Border>(model, "CompactView");
        Assert.AreEqual(
            new Thickness(24d),
            compact.Padding,
            $"{state}/{preview200}/{width}: compact card inset");

        Grid detailedHeader = Element<Grid>(model, "DetailedHeader");
        Grid metadata = Element<Grid>(model, "MetadataGrid");
        Grid inspectionDetailsHeader = Element<Grid>(
            model,
            "InspectionDetailsHeader");
        FrameworkElement inspectionDetailsViewport = Element<FrameworkElement>(
            model,
            "InspectionDetailsViewport");
        Assert.AreEqual(
            new Thickness(16d, 14d, 16d, 12d),
            detailedHeader.Margin,
            $"{state}/{preview200}/{width}: detailed header inset");
        Assert.AreEqual(
            new Thickness(0d),
            metadata.Margin,
            $"{state}/{preview200}/{width}: metadata inset");
        Assert.AreEqual(
            new Thickness(16d, 0d, 16d, 0d),
            inspectionDetailsHeader.Padding,
            $"{state}/{preview200}/{width}: inspection details header inset");
        Assert.AreEqual(
            new Thickness(16d, 0d, 16d, 16d),
            inspectionDetailsViewport.Margin,
            $"{state}/{preview200}/{width}: inspection details viewport inset");

        TextBlock technicalDetailsFutureHelpText = Element<TextBlock>(
            content,
            "TechnicalDetailsFutureHelpText");
        Button technicalDetailsButton = Element<Button>(
            content,
            "TechnicalDetailsButton");
        Assert.AreEqual(
            new Thickness(24d, 12d, 24d, 0d),
            technicalDetailsFutureHelpText.Margin,
            $"{state}/{preview200}/{width}: technical details help inset");
        Assert.AreEqual(
            new Thickness(24d, 8d, 24d, 12d),
            technicalDetailsButton.Margin,
            $"{state}/{preview200}/{width}: technical details action inset");

        if (presentation.ModelCard.DisplayMode == InspectionModelCardMode.Detailed &&
            presentation.ModelCard.IsInspectionDetailsExpanded)
        {
            InspectionDisclosure disclosure = model.ActiveDisclosure!;
            Border disclosureSurface = Element<Border>(
                disclosure,
                "DisclosureCardSurface");
            AssertCardInnerHorizontalGeometry(
                disclosureSurface,
                inspectionDetailsViewport,
                $"{state}/{preview200}/{width}: inspection details viewport",
                expectedInset: 16d);
        }
    }

    private static void AssertNoPeerOverlap(
        FrameworkElement root,
        IReadOnlyList<FrameworkElement> elements,
        string context)
    {
        for (int left = 0; left < elements.Count; left++)
        {
            for (int right = left + 1; right < elements.Count; right++)
            {
                Rect intersection = RectHelper.Intersect(
                    Bounds(root, elements[left]),
                    Bounds(root, elements[right]));
                Assert.IsTrue(
                    intersection.IsEmpty ||
                    intersection.Width <= 1d ||
                    intersection.Height <= 1d,
                    $"{context}: {elements[left].Name}/{elements[right].Name}");
            }
        }
    }

    private static void AssertDetailPanelGeometry(
        FrameworkElement root,
        InspectionModelCard model,
        FrameworkElement modelSurface,
        string context)
    {
        FrameworkElement configurationPanel = Element<FrameworkElement>(
            model,
            "ModelConfigurationPanel");
        FrameworkElement resultSummaryPanel = Element<FrameworkElement>(
            model,
            "ModelResultSummaryPanel");
        FrameworkElement[] panels =
        [
            configurationPanel,
            resultSummaryPanel
        ];
        Rect surfaceBounds = Bounds(root, modelSurface);
        foreach (FrameworkElement panel in panels)
        {
            Rect panelBounds = Bounds(root, panel);
            Assert.IsGreaterThanOrEqualTo(
                surfaceBounds.Left - 1d,
                panelBounds.Left,
                $"{context}: {panel.Name} remains inside the model surface left edge");
            Assert.IsGreaterThanOrEqualTo(
                surfaceBounds.Top - 1d,
                panelBounds.Top,
                $"{context}: {panel.Name} remains inside the model surface top edge");
            Assert.IsLessThanOrEqualTo(
                surfaceBounds.Right + 1d,
                panelBounds.Right,
                $"{context}: {panel.Name} remains inside the model surface right edge");
            Assert.IsLessThanOrEqualTo(
                surfaceBounds.Bottom + 1d,
                panelBounds.Bottom,
                $"{context}: {panel.Name} remains inside the model surface bottom edge");
        }
        AssertNoPeerOverlap(root, panels, $"{context}: panel overlap");
        Rect configurationBounds = Bounds(root, configurationPanel);
        Rect resultSummaryBounds = Bounds(root, resultSummaryPanel);
        Assert.AreEqual(
            configurationBounds.Left,
            resultSummaryBounds.Left,
            1d,
            $"{context}: configuration/result left bounds align");
        Assert.AreEqual(
            configurationBounds.Right,
            resultSummaryBounds.Right,
            1d,
            $"{context}: configuration/result right bounds align");

        Border configurationBackground = Element<Border>(
            model,
            "ModelConfigurationSurface");
        Border resultSummaryBackground = Element<Border>(
            model,
            "ModelResultSummarySurface");
        AssertSameBounds(
            root,
            configurationPanel,
            configurationBackground,
            $"{context}: configuration background fills its panel");
        AssertSameBounds(
            root,
            resultSummaryPanel,
            resultSummaryBackground,
            $"{context}: result background fills its panel");

        TextBlock configurationHeading = Element<TextBlock>(
            model,
            "ModelConfigurationHeading");
        TextBlock formatLabel = Descendants(
                Element<FrameworkElement>(model, "FormatField"))
            .OfType<TextBlock>()
            .Single(text => text.Text == "FORMAT");
        TextBlock resultSummary = Element<TextBlock>(
            model,
            "ReadyResultSummary");
        TextBlock resultBadge = Element<TextBlock>(
            model,
            "ReadyResultBadgeText");
        AssertElementInside(
            root,
            configurationHeading,
            configurationPanel,
            $"{context}: configuration heading fit");
        AssertElementInside(
            root,
            resultSummary,
            resultSummaryPanel,
            $"{context}: result summary fit");
        AssertElementInside(
            root,
            resultBadge,
            resultSummaryPanel,
            $"{context}: result badge fit");
        AssertNoPeerOverlap(
            root,
            [configurationHeading, formatLabel],
            $"{context}: configuration heading/format overlap");
        AssertNoPeerOverlap(
            root,
            [resultSummary, resultBadge],
            $"{context}: result summary/badge overlap");
    }

    private static void AssertSameBounds(
        FrameworkElement root,
        FrameworkElement expected,
        FrameworkElement actual,
        string context)
    {
        Rect expectedBounds = Bounds(root, expected);
        Rect actualBounds = Bounds(root, actual);
        Assert.AreEqual(expectedBounds.Left, actualBounds.Left, 1d, context);
        Assert.AreEqual(expectedBounds.Top, actualBounds.Top, 1d, context);
        Assert.AreEqual(expectedBounds.Right, actualBounds.Right, 1d, context);
        Assert.AreEqual(expectedBounds.Bottom, actualBounds.Bottom, 1d, context);
    }

    private static void AssertElementInside(
        FrameworkElement root,
        FrameworkElement element,
        FrameworkElement container,
        string context)
    {
        Rect elementBounds = Bounds(root, element);
        Rect containerBounds = Bounds(root, container);
        Assert.IsGreaterThanOrEqualTo(
            containerBounds.Left - 1d,
            elementBounds.Left,
            $"{context}: left edge");
        Assert.IsGreaterThanOrEqualTo(
            containerBounds.Top - 1d,
            elementBounds.Top,
            $"{context}: top edge");
        Assert.IsLessThanOrEqualTo(
            containerBounds.Right + 1d,
            elementBounds.Right,
            $"{context}: right edge");
        Assert.IsLessThanOrEqualTo(
            containerBounds.Bottom + 1d,
            elementBounds.Bottom,
            $"{context}: bottom edge");
    }

    private static void AssertNoGeneralOverlap(
        FrameworkElement root,
        IReadOnlyList<FrameworkElement> elements,
        string context)
    {
        Dictionary<FrameworkElement, Rect> visibleBounds = elements
            .Distinct()
            .ToDictionary(
                element => element,
                element => VisibleBoundsInScrollViewports(root, element));
        for (int left = 0; left < elements.Count; left++)
        {
            for (int right = left + 1; right < elements.Count; right++)
            {
                FrameworkElement first = elements[left];
                FrameworkElement second = elements[right];
                if (IsAncestor(first, second) || IsAncestor(second, first))
                {
                    continue;
                }

                Rect firstBounds = visibleBounds[first];
                Rect secondBounds = visibleBounds[second];
                Rect intersection = RectHelper.Intersect(
                    firstBounds,
                    secondBounds);
                Assert.IsTrue(
                    intersection.IsEmpty ||
                    intersection.Width <= 1d ||
                    intersection.Height <= 1d,
                    $"{context}: {SemanticIdentity(first)}/" +
                    $"{SemanticIdentity(second)}; " +
                    $"first={firstBounds}; second={secondBounds}");
            }
        }
    }

    private static async Task AssertCompleteScrollableSurfacesAsync(
        ModelInspectionPage page,
        ModelInspectionFigmaState state,
        double width,
        bool preview200)
    {
        ScrollViewer pageScroll = Element<ScrollViewer>(
            page,
            "InspectionPageScrollViewer");
        ScrollViewer[] scrollOwners = Descendants(page)
            .OfType<ScrollViewer>()
            .Where(owner => IsLogicallyVisible(owner) &&
                owner.ActualWidth > 0d &&
                owner.ActualHeight > 0d)
            .OrderBy(owner => ReferenceEquals(owner, pageScroll) ? 0 : 1)
            .ToArray();
        Assert.AreEqual(
            1,
            scrollOwners.Count(owner => ReferenceEquals(owner, pageScroll)),
            $"{state}/{preview200}/{width}: page scroll owner count");

        foreach (ScrollViewer owner in scrollOwners)
        {
            Assert.AreEqual(
                ScrollMode.Enabled,
                owner.VerticalScrollMode,
                $"{state}/{preview200}/{width}: {owner.Name} scroll mode");
            Assert.IsLessThanOrEqualTo(
                owner.ViewportHeight + owner.ScrollableHeight + 1d,
                owner.ExtentHeight,
                $"{state}/{preview200}/{width}: {owner.Name} extent closure");

            if (!ReferenceEquals(owner, pageScroll))
            {
                Rect ownerBounds = Bounds(pageScroll, owner);
                double logicalTop = ownerBounds.Top + pageScroll.VerticalOffset;
                double pageTarget = Math.Clamp(
                    logicalTop - 12d,
                    0d,
                    pageScroll.ScrollableHeight);
                await ChangeScrollOffsetAsync(page, pageScroll, pageTarget);
            }

            await AssertScrollOwnerViewportSweepAsync(
                page,
                owner,
                state,
                width,
                preview200);
        }

        foreach (ScrollViewer owner in scrollOwners.Reverse())
        {
            await ChangeScrollOffsetAsync(page, owner, 0d);
        }
    }

    private static async Task AssertScrollOwnerViewportSweepAsync(
        ModelInspectionPage page,
        ScrollViewer owner,
        ModelInspectionFigmaState state,
        double width,
        bool preview200)
    {
        int guard = 0;
        double target = 0d;
        while (true)
        {
            await ChangeScrollOffsetAsync(page, owner, target);
            AssertCurrentScrollableViewport(page, state, width, preview200);

            double maximum = owner.ScrollableHeight;
            if (target >= maximum - 1d)
            {
                Assert.AreEqual(maximum, owner.VerticalOffset, 1d,
                    $"{state}/{preview200}/{width}: {owner.Name} bottom reach");
                return;
            }

            double step = Math.Max(44d, owner.ViewportHeight * 0.75d);
            target = Math.Min(maximum, target + step);
            guard++;
            Assert.IsLessThanOrEqualTo(
                64,
                guard,
                $"{state}/{preview200}/{width}: {owner.Name} sweep bound");
        }
    }

    private static void AssertCurrentScrollableViewport(
        ModelInspectionPage page,
        ModelInspectionFigmaState state,
        double width,
        bool preview200)
    {
        TextBlock[] visibleText = Descendants(page)
            .OfType<TextBlock>()
            .Where(text => IsActuallyVisibleInScrollViewports(page, text) &&
                !string.IsNullOrWhiteSpace(text.Text) &&
                !HasAncestor<InspectionStatusGlyph>(text) &&
                !(text.FontFamily?.Source ?? string.Empty).StartsWith(
                    "Segoe Fluent Icons",
                    StringComparison.Ordinal))
            .ToArray();
        foreach (TextBlock text in visibleText)
        {
            Rect bounds = Bounds(page, text);
            Assert.IsGreaterThanOrEqualTo(-1d, bounds.X,
                $"{state}/{preview200}/{width}: {text.Text}; " +
                DescribeVisualPath(text));
            Assert.IsLessThanOrEqualTo(width + 1d, bounds.Right,
                $"{state}/{preview200}/{width}: {text.Text}");
            if (text.TextWrapping != TextWrapping.NoWrap)
            {
                Assert.AreEqual(TextTrimming.None, text.TextTrimming,
                    $"{state}/{preview200}/{width}: {text.Text}");
                Assert.AreEqual(0, text.MaxLines,
                    $"{state}/{preview200}/{width}: {text.Text}");
                double allowance = text.Margin.Top + text.Margin.Bottom +
                    Math.Max(4d, text.FontSize * 0.25d);
                Assert.IsGreaterThanOrEqualTo(
                    text.DesiredSize.Height,
                    text.ActualHeight + allowance,
                    $"{state}/{preview200}/{width}: desired-size " +
                    $"clipping for {text.Text}; {DescribeVisualPath(text)}");
            }
        }

        FrameworkElement[] overlapCandidates = visibleText
            .Cast<FrameworkElement>()
            .Concat(Descendants(page).OfType<Button>().Where(button =>
                IsActuallyVisibleInScrollViewports(page, button)))
            .ToArray();
        AssertNoGeneralOverlap(
            page,
            overlapCandidates,
            $"{state}/{preview200}/{width}: scrolled text/control overlap");
    }

    private static bool IsActuallyVisibleInScrollViewports(
        FrameworkElement page,
        FrameworkElement element) =>
        !VisibleBoundsInScrollViewports(page, element).IsEmpty;

    private static Rect VisibleBoundsInScrollViewports(
        FrameworkElement page,
        FrameworkElement element)
    {
        if (!IsRendered(element))
        {
            return Rect.Empty;
        }

        Rect visibleBounds = RectHelper.Intersect(
            Bounds(page, element),
            new Rect(0d, 0d, page.ActualWidth, page.ActualHeight));

        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null && !visibleBounds.IsEmpty)
        {
            if (current is ScrollViewer owner)
            {
                Rect ownerBounds = Bounds(page, owner);
                double viewportWidth = owner.ViewportWidth > 0d
                    ? owner.ViewportWidth
                    : owner.ActualWidth;
                double viewportHeight = owner.ViewportHeight > 0d
                    ? owner.ViewportHeight
                    : owner.ActualHeight;
                visibleBounds = RectHelper.Intersect(
                    visibleBounds,
                    new Rect(
                        ownerBounds.X,
                        ownerBounds.Y,
                        viewportWidth,
                        viewportHeight));
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return visibleBounds;
    }

    private static async Task ChangeScrollOffsetAsync(
        FrameworkElement page,
        ScrollViewer owner,
        double offset)
    {
        owner.ChangeView(
            horizontalOffset: null,
            verticalOffset: offset,
            zoomFactor: null,
            disableAnimation: true);
        var dispatched = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!page.DispatcherQueue.TryEnqueue(() => dispatched.TrySetResult(true)))
        {
            throw new InvalidOperationException(
                "The responsive scroll sweep dispatcher rejected a boundary.");
        }

        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        page.UpdateLayout();
    }

    private static bool IsAncestor(
        DependencyObject possibleAncestor,
        DependencyObject element)
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (ReferenceEquals(current, possibleAncestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static string SemanticIdentity(FrameworkElement element)
    {
        string automationName = AutomationProperties.GetName(element);
        return !string.IsNullOrWhiteSpace(element.Name)
            ? element.Name
            : !string.IsNullOrWhiteSpace(automationName)
                ? $"{element.GetType().Name}:{automationName}"
                : DescribeVisualPath(element);
    }

    private static Rect Bounds(FrameworkElement root, FrameworkElement element) =>
        element.TransformToVisual(root).TransformBounds(
            new Rect(0d, 0d, element.ActualWidth, element.ActualHeight));

    private static string DescribeVisualPath(FrameworkElement element)
    {
        var parts = new List<string>();
        DependencyObject? current = element;
        while (current is FrameworkElement ancestor && parts.Count < 8)
        {
            parts.Add($"{ancestor.GetType().Name}#{ancestor.Name}" +
                $"[{ancestor.ActualWidth:0.#}x{ancestor.ActualHeight:0.#}]");
            current = VisualTreeHelper.GetParent(ancestor);
        }

        return string.Join("/", parts);
    }

    private static bool IsRendered(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is FrameworkElement ancestor)
        {
            if (ancestor.Visibility != Visibility.Visible ||
                ancestor.ActualWidth <= 0d ||
                ancestor.ActualHeight <= 0d)
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(ancestor);
        }

        return true;
    }

    private static bool IsLogicallyVisible(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is FrameworkElement ancestor)
        {
            if (ancestor.Visibility != Visibility.Visible)
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(ancestor);
        }

        return true;
    }

    private static async Task ResizeClientAndWaitAsync(
        Window window,
        FrameworkElement page,
        double width,
        double height)
    {
        FrameworkElement scrollContent = Element<FrameworkElement>(
            page,
            "InspectionScrollContent");
        FrameworkElement contentHost = Element<FrameworkElement>(
            page,
            "InspectionContentHost");
        var model = Element<InspectionModelCard>(
            page,
            "InspectionModelCardControl");
        FrameworkElement[] metadata =
        [
            Element<FrameworkElement>(model, "ModelNameField"),
            Element<FrameworkElement>(model, "PublisherField"),
            Element<FrameworkElement>(model, "FormatField"),
            Element<FrameworkElement>(model, "QuantisationField"),
            Element<FrameworkElement>(model, "ParametersField"),
            Element<FrameworkElement>(model, "ModelTypeField"),
            Element<FrameworkElement>(model, "DeclaredContextField"),
            Element<FrameworkElement>(model, "FileSizeField")
        ];
        var actions = Element<InspectionActionCard>(
            page,
            "InspectionActionCardControl");
        FrameworkElement[] actionHosts =
        [
            Element<FrameworkElement>(actions, "SecondaryActionOneHost"),
            Element<FrameworkElement>(actions, "SecondaryActionTwoHost"),
            Element<FrameworkElement>(actions, "PrimaryActionHost")
        ];
        FrameworkElement resultView = Element<FrameworkElement>(
            actions,
            "ResultView");
        var reached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(object? sender, object args)
        {
            bool widthReached = Math.Abs(page.XamlRoot.Size.Width - width) <= 1d;
            bool contentArranged = scrollContent.ActualWidth <= width + 1d;
            double expectedInset = width < 600d ? 16d : 24d;
            bool responsiveStateApplied = Math.Abs(
                contentHost.Margin.Left - expectedInset) <= 0.1d;
            int expectedMetadataColumns = width >= 888d
                ? 3
                : width >= 600d ? 2 : 1;
            bool modelStateApplied = metadata
                .Select(Grid.GetColumn)
                .Distinct()
                .Count() == expectedMetadataColumns;
            FrameworkElement[] visibleActionHosts = actionHosts
                .Where(host => host.Visibility == Visibility.Visible)
                .ToArray();
            bool actionStateApplied = resultView.Visibility != Visibility.Visible ||
                visibleActionHosts.Length == 0 ||
                (width < 600d
                    ? visibleActionHosts.Select(Grid.GetRow).SequenceEqual(
                        Enumerable.Range(0, visibleActionHosts.Length))
                    : visibleActionHosts.Select(Grid.GetRow).Distinct().Count() == 1);
            if (widthReached &&
                contentArranged &&
                responsiveStateApplied &&
                modelStateApplied &&
                actionStateApplied &&
                Math.Abs(page.XamlRoot.Size.Height - height) <= 1d)
            {
                reached.TrySetResult(true);
            }
        }

        page.LayoutUpdated += Observe;
        try
        {
            double scale = page.XamlRoot.RasterizationScale;
            window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale)));
            Observe(null, EventArgs.Empty);
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            page.UpdateLayout();
        }
        finally
        {
            page.LayoutUpdated -= Observe;
        }
    }

    private static void AssertFindingsSurfacesRespectCardInsets(
        InspectionContentCard content,
        InspectionContentCardMode mode,
        ModelInspectionFigmaState state,
        bool preview200,
        double width)
    {
        if (mode is InspectionContentCardMode.Hidden or InspectionContentCardMode.Progress)
        {
            return;
        }

        Border shell = Element<Border>(content, "ContentCardShell");
        InspectionDisclosure findingsDisclosure = Element<InspectionDisclosure>(
            content,
            "FindingsDisclosure");
        Border findingsDisclosureSurface = Assert.IsInstanceOfType<Border>(
            findingsDisclosure.FindName("DisclosureCardSurface"));
        Grid findingsHeaderContent = Assert.IsInstanceOfType<Grid>(
            findingsDisclosure.HeaderContent);
        FrameworkElement[] surfaces =
        [
            Element<FrameworkElement>(content, "FindingsSectionTitle"),
            Element<FrameworkElement>(content, "FindingsItemsRepeater"),
            Element<FrameworkElement>(content, "SupportingText"),
            Element<FrameworkElement>(content, "TertiaryText"),
            Element<FrameworkElement>(content, "DiagnosticCodeBorder"),
            Element<FrameworkElement>(content, "ExpandedReportViewport")
        ];
        foreach (FrameworkElement surface in surfaces.Where(IsRendered))
        {
            Point origin = surface.TransformToVisual(shell).TransformPoint(default);
            double leftInset = origin.X - shell.BorderThickness.Left;
            double rightInset = shell.ActualWidth - shell.BorderThickness.Right -
                origin.X - surface.ActualWidth;
            Assert.IsGreaterThanOrEqualTo(
                24d - 0.01d,
                leftInset,
                $"{state}/{preview200}/{width}: {surface.Name} left inset");
            Assert.IsGreaterThanOrEqualTo(
                24d - 0.01d,
                rightInset,
                $"{state}/{preview200}/{width}: {surface.Name} right inset");
        }

        if (IsRendered(findingsHeaderContent))
        {
            Point headerOrigin = findingsHeaderContent.TransformToVisual(
                    findingsDisclosureSurface)
                .TransformPoint(default);
            double headerContentLeftInset = headerOrigin.X +
                findingsHeaderContent.Padding.Left;
            Assert.IsGreaterThanOrEqualTo(
                24d - 0.01d,
                headerContentLeftInset,
                $"{state}/{preview200}/{width}: findings disclosure header content left inset");
            if (findingsHeaderContent.Padding.Right > 0d)
            {
                double headerContentRightInset = findingsDisclosureSurface.ActualWidth -
                    headerOrigin.X -
                    findingsHeaderContent.ActualWidth + findingsHeaderContent.Padding.Right;
                Assert.IsGreaterThanOrEqualTo(
                    24d - 0.01d,
                    headerContentRightInset,
                    $"{state}/{preview200}/{width}: findings disclosure header content right inset");
            }
        }

        if (preview200)
        {
            Assert.AreEqual(
                0d,
                shell.MinHeight,
                0.01d,
                $"{state}/{width}: findings card remains natural at 200%");
        }
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
            if (HasAncestor<InspectionActionCard>(text) &&
                HasAncestor<Button>(text))
            {
                Assert.AreEqual(600, text.FontWeight.Weight, text.Text);
                continue;
            }

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
        Assert.AreEqual(0d, modelSurface.MinHeight, 0.01d);

        if (presentation.ModelCard.DisplayMode == InspectionModelCardMode.Detailed)
        {
            TextBlock title = Element<TextBlock>(model, "ModelOverviewTitle");
            FrameworkElement overviewPanel = Element<FrameworkElement>(
                model,
                "ModelOverviewPanel");
            Point titleOrigin = title.TransformToVisual(overviewPanel)
                .TransformPoint(new Point());
            Assert.AreEqual(TextAlignment.Left, title.TextAlignment);
            Assert.AreEqual(HorizontalAlignment.Left, title.HorizontalAlignment);
            Assert.AreEqual(18d, titleOrigin.X, 1d,
                "ready model title left inset");
            FrameworkElement[] metadata =
            [
                Element<FrameworkElement>(model, "ModelNameField"),
                Element<FrameworkElement>(model, "PublisherField"),
                Element<FrameworkElement>(model, "FormatField"),
                Element<FrameworkElement>(model, "QuantisationField"),
                Element<FrameworkElement>(model, "ParametersField"),
                Element<FrameworkElement>(model, "ModelTypeField"),
                Element<FrameworkElement>(model, "DeclaredContextField"),
                Element<FrameworkElement>(model, "FileSizeField")
            ];
            AssertNoPeerOverlap(
                modelSurface,
                metadata,
                "ready metadata geometry");
            AssertDetailPanelGeometry(
                modelSurface,
                model,
                modelSurface,
                "ready wide detail panels");
        }

        if (presentation.ModelCard.DisplayMode == InspectionModelCardMode.Compact)
        {
            Border formatTile = Element<Border>(model, "CompactFormatTile");
            FrameworkElement summary = Element<FrameworkElement>(
                model,
                "CompactModelSummaryPanel");
            Border statusChip = Element<Border>(model, "CompactStatusChip");
            Point summaryOrigin = summary.TransformToVisual(modelSurface)
                .TransformPoint(new Point());
            Point statusOrigin = statusChip.TransformToVisual(modelSurface)
                .TransformPoint(new Point());
            Assert.AreEqual(Visibility.Collapsed, formatTile.Visibility,
                "compact format tile is retired visually");
            Assert.AreEqual(0, Grid.GetRow(summary));
            Assert.AreEqual(0, Grid.GetRow(statusChip));
            Assert.IsLessThan(statusOrigin.X, summaryOrigin.X,
                "compact summary precedes the status pill");
            Assert.AreEqual(
                24d,
                modelSurface.ActualWidth - ((Border)modelSurface).BorderThickness.Right -
                    statusOrigin.X - statusChip.ActualWidth,
                1d,
                "compact status chip inner right inset");
        }

        if (presentation.ModelCard.IsInspectionDetailsExpanded)
        {
            FrameworkElement viewport = Element<FrameworkElement>(
                model,
                "InspectionDetailsViewport");
            InspectionDisclosure disclosure = model.ActiveDisclosure!;
            Border disclosureSurface = Element<Border>(
                disclosure,
                "DisclosureCardSurface");
            AssertCardInnerHorizontalGeometry(
                disclosureSurface,
                viewport,
                "model inspection details viewport");
        }

        if (presentation.ContentCard.Mode == InspectionContentCardMode.Hidden)
        {
            return;
        }

        Border contentSurface = Element<Border>(content, "ContentCardShell");
        Assert.AreEqual(840d, contentSurface.ActualWidth, 1d);
        Assert.AreEqual(0d, contentSurface.MinHeight, 0.01d);
        Assert.IsTrue(Descendants(content)
            .OfType<TextBlock>()
            .Where(text => text.Text.Length >= 40)
            .All(text => text.TextAlignment == TextAlignment.Left));
        FrameworkElement primaryRows = presentation.ContentCard.Mode ==
            InspectionContentCardMode.Progress
                ? Element<FrameworkElement>(content, "ProgressRowsSurface")
                : Element<FrameworkElement>(content, "FindingsRowsSurface");
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
            AssertCardInnerHorizontalGeometry(
                contentSurface,
                primaryRows,
                "findings list");
        }

        if (presentation.ContentCard.Mode != InspectionContentCardMode.Progress)
        {
            Border[] collapsedRows = Descendants(primaryRows)
                .OfType<Border>()
                .Where(row => Math.Abs(row.MinHeight - 48d) < 0.01d)
                .ToArray();
            Assert.HasCount(presentation.ContentCard.Items.Count, collapsedRows);
            Assert.IsTrue(collapsedRows.All(row =>
                row.ActualHeight >= 48d));
        }

        if (presentation.ContentCard.IsExpanded)
        {
            FrameworkElement viewport = Element<FrameworkElement>(
                content,
                "ExpandedReportViewport");
            AssertCardInnerHorizontalGeometry(
                contentSurface,
                viewport,
                "expanded report viewport");
            Border[] expandedRows = Descendants(viewport)
                .OfType<Border>()
                .Where(row => Math.Abs(row.MinHeight - 48d) < 0.01d)
                .ToArray();
            Assert.HasCount(
                presentation.ContentCard.ExpandedItems.Count,
                expandedRows);
            Assert.IsTrue(expandedRows.All(row =>
                row.ActualHeight >= 48d));
        }
    }

    private static void AssertCardInnerHorizontalGeometry(
        Border card,
        FrameworkElement surface,
        string surfaceName,
        double expectedInset = 24d)
    {
        Point origin = surface.TransformToVisual(card).TransformPoint(default);
        double leftInset = origin.X - card.BorderThickness.Left;
        double rightInset = card.ActualWidth - card.BorderThickness.Right -
            origin.X - surface.ActualWidth;
        double expectedWidth = card.ActualWidth - card.BorderThickness.Left -
            card.BorderThickness.Right - (2d * expectedInset);
        Assert.AreEqual(expectedInset, leftInset, 1d, $"{surfaceName} inner left inset");
        Assert.AreEqual(expectedInset, rightInset, 1d, $"{surfaceName} inner right inset");
        Assert.AreEqual(expectedWidth, surface.ActualWidth, 1d, $"{surfaceName} inner width");
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

        if (presentation.ModelCard.DisplayMode == InspectionModelCardMode.Detailed)
        {
            foreach (string surfaceName in new[]
                     {
                         "ModelOverviewSurface",
                         "ModelConfigurationSurface",
                         "ModelResultSummarySurface"
                     })
            {
                Border modelSurface = Element<Border>(model, surfaceName);
                AssertBrushColor("InspectionSurfaceBrush", modelSurface.Background);
                AssertBrushColor("InspectionBorderMutedBrush", modelSurface.BorderBrush);
            }
        }
        else
        {
            Border modelSurface = Element<Border>(model, "CompactView");
            AssertBrushColor("InspectionSurfaceBrush", modelSurface.Background);
            AssertBrushColor("InspectionBorderMutedBrush", modelSurface.BorderBrush);
        }

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
            Assert.AreEqual(0d, resultSurface.MinHeight, 0.01d);
            AssertBrushColor(
                "InspectionSurfaceMutedBrush",
                resultSurface.Background);
            AssertBrushColor(
                "InspectionBorderLightBrush",
                resultSurface.BorderBrush);
            Button primary = Element<Button>(actions, "PrimaryActionButton");
            AssertBrushColor(
                ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB),
                primary.Background,
                "shared primary background");
            AssertBrushColor(
                ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB),
                primary.BorderBrush,
                "shared primary border");
            AssertBrushColor(
                Colors.White,
                primary.Foreground,
                "shared primary foreground");
        }
        else
        {
            Button cancel = Element<Button>(actions, "CancelActionButton");
            AssertBrushColor(
                Colors.White,
                cancel.Background,
                "shared secondary background");
            AssertBrushColor(
                ColorHelper.FromArgb(0xFF, 0xC9, 0xD7, 0xE8),
                cancel.BorderBrush,
                "shared secondary border");
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
            FrameworkElement panel = Element<FrameworkElement>(
                actions,
                "ResultButtonPanel");
            FrameworkElement[] visibleHosts =
            [
                Element<FrameworkElement>(actions, "SecondaryActionOneHost"),
                Element<FrameworkElement>(actions, "SecondaryActionTwoHost"),
                Element<FrameworkElement>(actions, "PrimaryActionHost")
            ];
            visibleHosts = visibleHosts.Where(host => host.ActualHeight > 0d).ToArray();
            Assert.IsNotEmpty(visibleHosts);
            Assert.IsTrue(visibleHosts.Skip(1).All(host =>
                Math.Abs(host.ActualWidth - visibleHosts[0].ActualWidth) <= 1d));
            if (visibleHosts.Length == 1)
            {
                Point origin = visibleHosts[0].TransformToVisual(panel)
                    .TransformPoint(new Point());
                Assert.AreEqual(
                    panel.ActualWidth / 2d,
                    origin.X + (visibleHosts[0].ActualWidth / 2d),
                    1d);
            }
        }
    }

    private static void AssertCompactOutcome(
        InspectionOutcomeCard outcome,
        Border banner,
        string state)
    {
        Grid layout = Element<Grid>(outcome, "OutcomeLayoutGrid");
        Assert.AreEqual(2, layout.ColumnDefinitions.Count,
            $"{state} inline outcome columns");
        Assert.AreEqual(20d, layout.ColumnDefinitions[0].ActualWidth, 0.01d,
            $"{state} inline outcome glyph column");
        InspectionStatusGlyph glyph = Element<InspectionStatusGlyph>(
            outcome,
            "OutcomeIcon");
        Assert.AreEqual(22d, glyph.SurfaceSize, 0.01d,
            $"{state} approved glyph dependency-property surface");
        Border iconContainer = Element<Border>(outcome, "OutcomeIconContainer");
        Viewbox glyphHost = Descendants(iconContainer)
            .OfType<Viewbox>()
            .Single(viewbox =>
                Math.Abs(viewbox.Width - 20d) < 0.01d &&
                Math.Abs(viewbox.Height - 20d) < 0.01d);
        Assert.AreEqual(20d, glyphHost.ActualWidth, 0.01d,
            $"{state} visible outcome glyph width");
        Assert.AreEqual(20d, glyphHost.ActualHeight, 0.01d,
            $"{state} visible outcome glyph height");
        double? leftEdge = null;
        foreach (string name in new[] { "OutcomeTitle", "OutcomeMessage" })
        {
            TextBlock text = Element<TextBlock>(outcome, name);
            Point origin = text.TransformToVisual(banner).TransformPoint(new Point());
            Assert.AreEqual(TextAlignment.Left, text.TextAlignment,
                $"{state} {name} alignment");
            leftEdge ??= origin.X;
            Assert.AreEqual(leftEdge.Value, origin.X, 1d,
                $"{state} {name} left edge");
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

    private static void AssertBrushColor(
        Windows.UI.Color expected,
        Brush? actual,
        string context)
    {
        SolidColorBrush observed = Assert.IsInstanceOfType<SolidColorBrush>(actual);
        Assert.AreEqual(expected, observed.Color, context);
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
    internal static ModelInspectionPage CreatePage(
        Action<ResourceDictionary>? configureResourcesBeforeInitialize = null)
    {
        var service = new UnexpectedInspectionService();
        ModelInspectionPage page = configureResourcesBeforeInitialize is null
            ? new ModelInspectionPage(service)
            : new ModelInspectionPage(
                service,
                configureResourcesBeforeInitialize);
        page.RequestedTheme = ElementTheme.Light;
        return page;
    }

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
