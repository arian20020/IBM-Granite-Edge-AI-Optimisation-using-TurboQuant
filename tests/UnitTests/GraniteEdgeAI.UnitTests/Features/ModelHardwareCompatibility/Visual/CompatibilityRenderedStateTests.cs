// The fixture catalogue these render is compiled only in Debug, so the suite
// that renders it is too. A Release build has no ten screens to check because
// nine of them cannot be reached without the adapters that do not exist yet.
#if DEBUG
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.DebugFixtures;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility.Visual;

/// <summary>
/// Renders every compatibility screen and checks what a person would actually
/// see.
///
/// The presentation tests already prove the wording is chosen correctly. They
/// cannot prove it arrives: a resource that resolves to nothing, a card left
/// hidden, a diagram drawn at zero — each of those passes every test that never
/// builds the tree. So these construct the real page, apply a real snapshot,
/// and read the controls back.
///
/// The claim they defend is narrow and important. The verdict a user reads must
/// never appear without the figures behind it, and the figures must never
/// appear on a screen that reached no verdict.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CompatibilityRenderedStateTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_UsesApprovedLightTheme()
    {
        CompatibilityPage page = new() { StartAutomatically = false };

        Assert.AreEqual(ElementTheme.Light, page.RequestedTheme);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_CentresAReadableDesktopContentColumn()
    {
        CompatibilityPage page = new() { StartAutomatically = false };
        ScrollViewer contentHost = Element<ScrollViewer>(page, "ContentHost");
        FrameworkElement pageStack = Element<FrameworkElement>(page, "PageStack");
        TextBlock title = Element<TextBlock>(page, "PageTitleText");

        Assert.AreEqual(HorizontalAlignment.Stretch, contentHost.HorizontalContentAlignment);
        Assert.AreEqual(HorizontalAlignment.Center, pageStack.HorizontalAlignment);
        Assert.IsGreaterThanOrEqualTo(1180d, pageStack.MaxWidth);
        Assert.IsLessThanOrEqualTo(1280d, pageStack.MaxWidth);
        Assert.IsGreaterThanOrEqualTo(26d, title.FontSize);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_HasResponsiveAssessmentLayoutForNarrowWindows()
    {
        CompatibilityPage page = new() { StartAutomatically = false };
        Grid assessment = Element<Grid>(page, "AssessmentGrid");
        IList<VisualStateGroup> groups = VisualStateManager.GetVisualStateGroups(assessment);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(2, groups[0].States.Count);
        Assert.AreEqual(2, assessment.RowDefinitions.Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_DoesNotDuplicateTheOnboardingShellStageIndicator()
    {
        CompatibilityPage page = new() { StartAutomatically = false };

        Assert.IsNull(page.FindName("StepperSteps"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OptimisationRequired_UsesStableResponsiveAccessibleChoiceTree()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-020");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        FrameworkElement card = Element<FrameworkElement>(page, "OptimizationCard");
        Button automatic = Element<Button>(page, "AutomaticChoice");
        Slider slider = Element<Slider>(page, "OptimizationSlider");
        Button primary = Element<Button>(page, "PrimaryAction");
        Button secondary = Element<Button>(page, "SecondaryAction");

        Assert.AreEqual(Visibility.Visible, card.Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "AssessmentGrid").Visibility,
            "The optimisation state must not leave the legacy empty assessment cards above it.");
        Assert.IsNotNull(page.FindName("OptimizationModeRows"));
        Assert.IsNull(page.FindName("OptimizationOnboardingShell"));
        Assert.IsGreaterThanOrEqualTo(44d, automatic.MinHeight);
        Assert.IsGreaterThanOrEqualTo(44d, slider.MinHeight);
        Assert.IsGreaterThanOrEqualTo(44d, primary.MinHeight);
        Assert.IsGreaterThanOrEqualTo(44d, secondary.MinHeight);
        Assert.AreEqual(ScrollBarVisibility.Auto,
            Element<ScrollViewer>(page, "ContentHost").VerticalScrollBarVisibility);
    }

    [TestMethod]
    public void RequiredOptimizationFixtures_CarryExactRouteFormatsAndWarnings()
    {
        var expected = new[]
        {
            new { Id = "CMP-020", CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Good", Experimental = false, Strong = false },
            new { Id = "CMP-021", CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q2_K", RecommendedCache = "Q8_0", Quality = "Expected quality: Low", Experimental = false, Strong = true },
            new { Id = "CMP-022", CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Acceptable", Experimental = false, Strong = false },
            new { Id = "CMP-023", CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT4", RecommendedCache = "Runtime default", Quality = "Expected quality: Good", Experimental = false, Strong = false },
            new { Id = "CMP-024", CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "Original", RecommendedCache = "U4", Quality = "Expected quality: Excellent", Experimental = false, Strong = false },
            new { Id = "CMP-025", CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "Original", RecommendedCache = "TurboQuant TBQ4", Quality = "Expected quality: Good", Experimental = true, Strong = false },
            new { Id = "CMP-026", CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "Original", RecommendedCache = "TurboQuant TBQ3", Quality = "Expected quality: Acceptable", Experimental = true, Strong = true },
            new { Id = "CMP-027", CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Acceptable", Experimental = false, Strong = false },
            new { Id = "CMP-028", CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q4_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Good", Experimental = false, Strong = false }
        };

        foreach (var item in expected)
        {
            CompatibilityOptimizationPresentation optimization =
                CompatibilityFixtureCatalogue.ById(item.Id)!.Presentation.Optimization!;
            CompatibilityOptimizationModePresentation selected = optimization.SelectedMode;
            Assert.AreEqual(item.CurrentWeight, optimization.CurrentWeightFormat, item.Id);
            Assert.AreEqual(item.CurrentCache, optimization.CurrentCacheFormat, item.Id);
            Assert.AreEqual(item.RecommendedWeight, selected.WeightFormat, item.Id);
            Assert.AreEqual(item.RecommendedCache, selected.CacheFormat, item.Id);
            Assert.IsTrue(
                string.Equals(item.Quality, selected.ExpectedQualityText, StringComparison.Ordinal),
                item.Id);
            Assert.AreEqual(item.Experimental, selected.IsExperimental, item.Id);
            Assert.AreEqual(item.Strong, selected.HasStrongQualityWarning, item.Id);
        }

        Assert.IsNull(CompatibilityFixtureCatalogue.ById("CMP-030")!.Presentation.Optimization);
        Assert.IsNull(CompatibilityFixtureCatalogue.ById("CMP-040")!.Presentation.Optimization);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OptimizationLayout_RespondsToItsOwnNarrowHost()
    {
        CompatibilityPage page = CreatePage();
        Arrange(page, 680, 720);
        page.Apply(CompatibilityFixtureCatalogue.ById("CMP-020")!.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(1, Grid.GetRow(Element<FrameworkElement>(page,
            "RecommendedSetupCard")));
        Border outcomeBadge = Element<Border>(page, "OutcomeBadgeHost");
        Grid outcomeLayout = Element<Grid>(page, "OutcomeLayoutGrid");
        Assert.AreEqual(1, Grid.GetRow(outcomeBadge));
        Assert.AreEqual(2, outcomeLayout.RowDefinitions.Count,
            "The compact badge needs its own measured row rather than overlaying the message.");
        Grid actions = Element<Grid>(page, "ActionGrid");
        Assert.AreEqual(0d, actions.MinWidth);
        Assert.AreEqual(1, Grid.GetRow(Element<FrameworkElement>(page,
            "PrimaryAction")));
        Assert.AreEqual(ScrollBarVisibility.Disabled,
            Element<ScrollViewer>(page, "ContentHost").HorizontalScrollBarVisibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OptimizationLayout_GrowsAndScrollsAtRepresentative200PercentText()
    {
        CompatibilityPage page = CreatePage();
        page.Apply(CompatibilityFixtureCatalogue.ById("CMP-021")!.Presentation);
        Arrange(page, 560, 520);
        TextBlock title = Element<TextBlock>(page, "PageTitleText");
        double naturalTitleHeight = title.ActualHeight;

        foreach (TextBlock text in Descendants(page).OfType<TextBlock>())
        {
            text.FontSize *= 2d;
        }
        Arrange(page, 560, 520);

        ScrollViewer scroll = Element<ScrollViewer>(page, "ContentHost");
        Assert.AreEqual(TextWrapping.Wrap, title.TextWrapping);
        Assert.IsTrue(title.IsTextScaleFactorEnabled);
        Assert.IsGreaterThan(naturalTitleHeight * 1.5d, title.ActualHeight,
            "the centered title must grow naturally at the 200% preview scale");
        Assert.IsGreaterThan(0d, scroll.ScrollableHeight,
            "the reduced viewport must expose vertical scrolling");
        Assert.AreNotEqual(ScrollMode.Disabled, scroll.VerticalScrollMode);
        string[] clipped = Descendants(page).OfType<TextBlock>()
            .Where(text => text.ActualWidth > 0d && text.ActualHeight > 0d)
            .Where(text => text.ActualHeight + 4d < text.DesiredSize.Height)
            .Select(text => $"'{text.Text}' actual={text.ActualHeight:F1} desired={text.DesiredSize.Height:F1}")
            .ToArray();
        Assert.IsEmpty(clipped,
            "realized text must receive its desired height instead of clipping: "
            + string.Join("; ", clipped));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConcludedPresentation_ExposesSemanticMemoryEstimate()
    {
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-010");
        Assert.IsNotNull(fixture);
        CompatibilityEstimateSummary? summary = fixture.Presentation.EstimateSummary;
        Assert.IsNotNull(summary);

        Assert.AreEqual(4_697_620_480UL, summary.ModelWeightsBytes);
        Assert.AreEqual(536_870_912UL, summary.KvCacheBytes);
        Assert.AreEqual(369_098_752UL, summary.RuntimeAndBufferBytes);
        Assert.AreEqual(560_359_014UL, summary.MarginForErrorBytes);
        Assert.AreEqual(6_163_949_158UL, summary.EstimatedPeakBytes);
        Assert.AreEqual(9_663_676_416UL, summary.SafeMemoryBytes);
        Assert.IsNull(CompatibilityPresentation.Empty.EstimateSummary);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OnlyMemoryBlockedOutcome_TellsUserToCloseApplicationsAndTabs()
    {
        CompatibilityFixture? blockedFixture =
            CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(blockedFixture);
        CompatibilityPresentation blocked = blockedFixture.Presentation;

        StringAssert.Contains(
            blocked.OutcomeDetail,
            "Close unused applications and browser tabs");
        Assert.IsTrue(blocked.Recoveries.Any(recovery =>
            recovery.Detail.Contains("browser tabs", StringComparison.Ordinal)));

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All
            .Where(fixture => fixture.Id != "CMP-030"))
        {
            Assert.IsFalse(fixture.Presentation.OutcomeDetail.Contains(
                "Close unused applications and browser tabs",
                StringComparison.Ordinal));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PresentationWithoutSafeModelDisplayText_HidesModelSummaryCard()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "ModelSummaryCard").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MemoryBlockedOutcome_IsAnExplicitActionableWarning()
    {
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        StringAssert.Contains(fixture.Presentation.OutcomeTitle, "Memory warning");
        StringAssert.Contains(fixture.Presentation.OutcomeDetail, "check again");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void GeneratedRuntimeRows_UseReadableLightThemeTextBeforeFirstLoad()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-030");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        Grid row = Assert.IsInstanceOfType<Grid>(Panel(page, "RuntimeRows").Children[0]);
        StackPanel text = Assert.IsInstanceOfType<StackPanel>(row.Children[0]);
        TextBlock title = Assert.IsInstanceOfType<TextBlock>(text.Children[0]);
        SolidColorBrush foreground = Assert.IsInstanceOfType<SolidColorBrush>(title.Foreground);

        Assert.AreEqual("#FF101828", foreground.Color.ToString());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_RendersWithoutThrowing()
    {
        // The page resolves theme-scoped brushes from code. A key that is
        // missing under one theme throws inside the constructor, which is a
        // crash on navigation rather than a visual defect, so it has to be
        // caught by construction rather than by inspection.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConcludedScreens_ShowTheFiguresBehindTheVerdict()
    {
        // A verdict with empty cards under it is the failure this whole design
        // exists to prevent: a confident sentence with nothing supporting it.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in Concluded())
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreNotEqual(
                0,
                Panel(page, "FactsGrid").Children.Count,
                $"{fixture.Id} stated a verdict with no facts under it.");

            Assert.AreNotEqual(
                0,
                Panel(page, "RuntimeRows").Children.Count,
                $"{fixture.Id} did not say what would run.");

            Assert.AreNotEqual(
                0,
                Panel(page, "CheckRows").Children.Count,
                $"{fixture.Id} did not say what was checked.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConcludedScreens_DrawTheMemoryBar()
    {
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in Concluded())
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreEqual(
                Visibility.Visible,
                Element<FrameworkElement>(page, "BudgetDiagram").Visibility,
                $"{fixture.Id} reached a verdict without showing the memory it rests on.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EvaluatedScreen_ShowsVisibleEstimatedMemoryBreakdown()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityFixture? fixture = CompatibilityFixtureCatalogue.ById("CMP-010");
        Assert.IsNotNull(fixture);

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Visible,
            Element<FrameworkElement>(page, "EstimateBreakdownCard").Visibility);
        Assert.AreEqual("4.4 GB", Element<TextBlock>(page, "EstimateWeightsValue").Text);
        Assert.AreEqual("512 MB", Element<TextBlock>(page, "EstimateKvCacheValue").Text);
        Assert.AreEqual("352 MB", Element<TextBlock>(page, "EstimateRuntimeValue").Text);
        Assert.AreEqual("534 MB", Element<TextBlock>(page, "EstimateMarginValue").Text);
        Assert.AreEqual("5.7 GB", Element<TextBlock>(page, "EstimatePeakValue").Text);
        Assert.AreEqual("9 GB", Element<TextBlock>(page, "EstimateSafeValue").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void UnevaluatedScreen_HidesEstimatedMemoryBreakdown()
    {
        CompatibilityPage page = CreatePage();

        page.Apply(CompatibilityPresentation.Empty);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "EstimateBreakdownCard").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ScreenThatReachedNoAnswer_DrawsNoMemoryBar()
    {
        // An empty bar beside "we could not work this out" reads as a model
        // that costs nothing, which is the opposite of what it means.
        CompatibilityPage page = CreatePage();

        page.Apply(CompatibilityPresentation.Empty);
        page.UpdateLayout();

        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "BudgetDiagram").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            Element<FrameworkElement>(page, "BudgetLegend").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_LeavesTheContinueButtonVisible()
    {
        // Disabled, never removed. A button that disappears reads as an option
        // that never existed, and the user cannot tell they are blocked.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreEqual(
                Visibility.Visible,
                Element<FrameworkElement>(page, "PrimaryAction").Visibility,
                $"{fixture.Id} hid Continue instead of disabling it.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryBlockingScreen_OffersSomethingToDo()
    {
        // A problem stated without a remedy is half an answer.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All)
        {
            if (fixture.Presentation.Tone != CompatibilityOutcomeTone.Blocking)
            {
                continue;
            }

            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreNotEqual(
                0,
                Panel(page, "RecoveryRows").Children.Count,
                $"{fixture.Id} blocked the user and suggested nothing.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryFixture_SaysWhereItsFiguresCameFrom()
    {
        // The estimated-versus-tested distinction is the one claim this feature
        // must never blur, and the badge is where it is carried.
        CompatibilityPage page = CreatePage();

        foreach (CompatibilityFixture fixture in Concluded())
        {
            page.Apply(fixture.Presentation);
            page.UpdateLayout();

            Assert.AreNotEqual(
                string.Empty,
                Element<TextBlock>(page, "OutcomeBadgeText").Text,
                $"{fixture.Id} showed figures without saying how they were arrived at.");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void AppliedTwice_LeavesNoRowsBehindFromTheFirstSnapshot()
    {
        // The page applies deltas to a tree that already exists. A card that
        // appended rather than replaced would show one screen's checks stacked
        // under another's.
        CompatibilityPage page = CreatePage();
        CompatibilityFixture fixture = Concluded().First();

        page.Apply(fixture.Presentation);
        page.UpdateLayout();
        int first = Panel(page, "CheckRows").Children.Count;

        page.Apply(fixture.Presentation);
        page.UpdateLayout();

        Assert.AreEqual(first, Panel(page, "CheckRows").Children.Count);
    }

    /// <summary>
    /// Reaches a named control the way the other rendered-state suites do,
    /// through the name scope rather than a field, so the page needs no test
    /// affordance widening its own surface.
    /// </summary>
    private static T Element<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        Assert.IsInstanceOfType<T>(root.FindName(name));

    private static Panel Panel(FrameworkElement root, string name) =>
        Element<Panel>(root, name);

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
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

    private static IEnumerable<CompatibilityFixture> Concluded() =>
        CompatibilityFixtureCatalogue.All.Where(fixture =>
            fixture.Presentation.Facts.Count > 0);

    private static CompatibilityPage CreatePage()
    {
        return new CompatibilityPage { StartAutomatically = false };
    }
}
#endif
