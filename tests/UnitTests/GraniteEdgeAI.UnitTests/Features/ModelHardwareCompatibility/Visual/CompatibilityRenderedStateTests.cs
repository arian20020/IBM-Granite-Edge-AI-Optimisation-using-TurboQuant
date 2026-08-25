// The fixture catalogue these render is compiled only in Debug, so the suite
// that renders it is too. A Release build has no ten screens to check because
// nine of them cannot be reached without the adapters that do not exist yet.
#if DEBUG
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.DebugFixtures;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Windows.Foundation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

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
    public async Task MissingDestination_LeavesViewModelCommandAndPageDisabled()
    {
        CompatibilityScreenModel result = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true);
        var blockedPage = new CompatibilityPage(
            _ => Task.FromResult(result),
            continueDestinationAvailable: false)
        {
            StartAutomatically = false
        };
        await blockedPage.ViewModel.StartAsync();

        Assert.IsFalse(blockedPage.ViewModel.Presentation.PrimaryActionEnabled);
        Assert.IsFalse(Element<Button>(blockedPage, "PrimaryAction").IsEnabled);
        Assert.IsFalse(blockedPage.ViewModel.ContinueCommand.CanExecute(null));
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

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SecondaryAction_InvokesCancelRetryAndBackForTheRenderedState()
    {
        TaskCompletionSource<CompatibilityScreenModel> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var page = new CompatibilityPage(_ => ++calls == 1
            ? first.Task
            : Task.FromResult(CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.EstimatedCompatible,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: true)))
        {
            StartAutomatically = false
        };
        Button secondary = Element<Button>(page, "SecondaryAction");

        Task analysing = page.ViewModel.StartAsync();
        Assert.AreEqual("Cancel", secondary.Content);
        Invoke(secondary);
        first.SetResult(CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true));
        await analysing;
        Assert.AreEqual("Check stopped", page.ViewModel.Presentation.OutcomeTitle);

        Assert.AreEqual("Check again", secondary.Content);
        Invoke(secondary);
        await WaitUntilAsync(() =>
            calls == 2
            && page.ViewModel.Presentation.SecondaryActionKind
                == CompatibilitySecondaryActionKind.Back
            && Equals(secondary.Content, "Back"));
        Assert.IsTrue(page.ViewModel.Presentation.OutcomeTitle.StartsWith(
            "Yes",
            StringComparison.Ordinal));

        bool backed = false;
        page.BackRequested += (_, _) => backed = true;
        Assert.AreEqual("Back", secondary.Content);
        Invoke(secondary);
        Assert.IsTrue(backed);

        page.Apply(CompatibilityPresentation.Empty with
        {
            SecondaryActionText = "Unavailable",
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.None
        });
        Assert.IsFalse(secondary.IsEnabled);
        Assert.IsNull(secondary.Command);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OptimizationRowsRemainStableAndAccessibilityTracksTheEffectiveMode()
    {
        CompatibilityPage page = CreatePage();
        CompatibilityPresentation automatic =
            CompatibilityFixtureCatalogue.ById("CMP-021")!.Presentation;
        page.Apply(automatic);
        Border firstRow = Assert.IsInstanceOfType<Border>(
            Element<Panel>(page, "OptimizationModeRows").Children[0]);
        Slider slider = Element<Slider>(page, "OptimizationSlider");

        CompatibilityOptimizationPresentation optimization = automatic.Optimization!;
        CompatibilityOptimizationModePresentation manualMode = optimization.Modes[1];
        page.Apply(automatic with
        {
            Optimization = optimization with
            {
                IsAutomatic = false,
                SliderValue = manualMode.SliderValue!.Value,
                SelectedMode = manualMode
            }
        });

        Assert.AreSame(firstRow, Element<Panel>(page, "OptimizationModeRows").Children[0]);
        Assert.AreEqual(manualMode.SliderValue.Value, slider.Value);
        StringAssert.Contains(AutomationProperties.GetName(slider), manualMode.Label);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider), manualMode.ExpectedQualityText);
        StringAssert.Contains(AutomationProperties.GetHelpText(slider), "Strong quality warning");
        StringAssert.Contains(AutomationProperties.GetItemStatus(slider), "Released");

        page.Apply(automatic);
        Assert.AreEqual(automatic.Optimization!.SliderValue, slider.Value);
        StringAssert.Contains(AutomationProperties.GetName(slider), "Automatic");
        StringAssert.Contains(
            AutomationProperties.GetName(slider),
            automatic.Optimization.SelectedMode.Label);
        StringAssert.Contains(
            AutomationProperties.GetHelpText(slider),
            automatic.Optimization.SelectedMode.Label);
        StringAssert.Contains(AutomationProperties.GetItemStatus(slider), "Recommended");
        StringAssert.Contains(
            AutomationProperties.GetItemStatus(slider),
            automatic.Optimization.SelectedMode.Label);

        CompatibilityPresentation experimental =
            CompatibilityFixtureCatalogue.ById("CMP-026")!.Presentation;
        page.Apply(experimental);
        StringAssert.Contains(
            AutomationProperties.GetHelpText(slider),
            "Experimental opt-in");
        StringAssert.Contains(
            AutomationProperties.GetHelpText(slider),
            "Strong quality warning");
        StringAssert.Contains(
            AutomationProperties.GetHelpText(slider),
            experimental.Optimization!.SelectedMode.WarningText);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SharedPreferenceSliderThemeDefinesSystemResolvedHighContrastResources()
    {
        ResourceDictionary theme = new()
        {
            Source = new Uri(
                "ms-appx:///Features/ModelImport/ModelDownload/ModelPreferenceSliderTheme.xaml")
        };
        ResourceDictionary highContrast = Assert.IsInstanceOfType<ResourceDictionary>(
            theme.ThemeDictionaries["HighContrast"]);

        foreach (string key in new[]
        {
            "SliderOuterThumbBackground",
            "SliderOuterThumbBackgroundPointerOver",
            "SliderOuterThumbBackgroundPressed",
            "SliderThumbBackground",
            "SliderThumbBackgroundPointerOver",
            "SliderThumbBackgroundPressed",
            "SliderThumbBorderBrush",
            "SliderThumbBorderBrushPointerOver",
            "SliderThumbBorderBrushPressed",
            "SliderTrackFill",
            "SliderTrackFillPointerOver",
            "SliderTrackFillPressed",
            "SliderTrackValueFill",
            "SliderTrackValueFillPointerOver",
            "SliderTrackValueFillPressed"
        })
        {
            Assert.IsInstanceOfType<SolidColorBrush>(highContrast[key], key);
        }

        CompatibilityPage compatibilityPage = CreatePage();
        Assert.IsTrue(compatibilityPage.Resources.MergedDictionaries.Any(
            dictionary => dictionary.Source?.OriginalString.EndsWith(
                "/ModelPreferenceSliderTheme.xaml",
                StringComparison.Ordinal) == true));

        var downloadCard = new ModelDownloadCard();
        Slider downloadSlider = Assert.IsInstanceOfType<Slider>(
            downloadCard.FindName("ModelScaleSlider"));
        Assert.IsTrue(downloadSlider.Resources.MergedDictionaries.Any(
            dictionary => dictionary.Source?.OriginalString.EndsWith(
                "/ModelPreferenceSliderTheme.xaml",
                StringComparison.Ordinal) == true));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HostLifetime_UnloadRetiresLateWorkAndReloadStartsExactlyOnce()
    {
        TaskCompletionSource<CompatibilityScreenModel> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var page = new CompatibilityPage(_ => ++calls == 1
            ? first.Task
            : Task.FromResult(CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false)));

        Task firstLifetime = InvokeLifecycleAsync(page, "ActivateAsync");
        await WaitUntilAsync(() => calls == 1);
        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.AreEqual(1, calls, "Repeated Loaded must be idempotent in one lifetime.");

        InvokeLifecycle(page, "Deactivate");
        first.SetResult(CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true));
        await firstLifetime;
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Cancel,
            page.ViewModel.Presentation.SecondaryActionKind);
        Assert.IsFalse(page.ViewModel.Presentation.PrimaryActionEnabled);

        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.AreEqual(2, calls, "Reload starts one intentional fresh attempt.");
        await InvokeLifecycleAsync(page, "ActivateAsync");
        Assert.AreEqual(2, calls);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HostLoadedBoundary_ReportsAnEvaluatorFaultWithoutThrowing()
    {
        var page = new CompatibilityPage(_ =>
            Task.FromException<CompatibilityScreenModel>(
                new InvalidOperationException("adapter failed")));

        await InvokeLifecycleAsync(page, "ActivateAsync");

        Assert.AreEqual(
            "The compatibility check could not finish",
            page.ViewModel.Presentation.OutcomeTitle);
        Assert.AreEqual("Check again", Element<Button>(page, "SecondaryAction").Content);
    }

    [TestMethod]
    public void RequiredOptimizationFixtures_CarryExactRouteFormatsAndWarnings()
    {
        var expected = new[]
        {
            new { Id = "CMP-020", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Good", Experimental = false, Strong = false },
            new { Id = "CMP-021", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q2_K", RecommendedCache = "Q8_0", Quality = "Expected quality: Low", Experimental = false, Strong = true },
            new { Id = "CMP-022", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Acceptable", Experimental = false, Strong = false },
            new { Id = "CMP-023", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT4", RecommendedCache = "Runtime default", Quality = "Expected quality: Good", Experimental = false, Strong = false },
            new { Id = "CMP-024", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT8", RecommendedCache = "U4", Quality = "Expected quality: Excellent", Experimental = false, Strong = false },
            new { Id = "CMP-025", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT8", RecommendedCache = "TurboQuant TBQ4", Quality = "Expected quality: Good", Experimental = true, Strong = false },
            new { Id = "CMP-026", Route = OptimizationRoute.OpenVino, CurrentWeight = "INT8", CurrentCache = "U8", RecommendedWeight = "INT8", RecommendedCache = "TurboQuant TBQ3", Quality = "Expected quality: Acceptable", Experimental = true, Strong = true },
            new { Id = "CMP-027", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q3_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Acceptable", Experimental = false, Strong = false },
            new { Id = "CMP-028", Route = OptimizationRoute.Gguf, CurrentWeight = "Q4_K_M", CurrentCache = "F16", RecommendedWeight = "Q4_K_M", RecommendedCache = "Q8_0", Quality = "Expected quality: Good", Experimental = false, Strong = false }
        };

        foreach (var item in expected)
        {
            CompatibilityOptimizationPresentation optimization =
                CompatibilityFixtureCatalogue.ById(item.Id)!.Presentation.Optimization!;
            CompatibilityOptimizationModePresentation selected = optimization.SelectedMode;
            Assert.AreEqual(item.Route, optimization.Route, item.Id);
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

    private static void Invoke(Button button)
    {
        ButtonAutomationPeer peer = new(button);
        IInvokeProvider provider = Assert.IsInstanceOfType<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke));
        provider.Invoke();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.IsTrue(DateTime.UtcNow < deadline, "Timed out waiting for the UI action.");
            await Task.Delay(10);
        }
    }

    private static Task InvokeLifecycleAsync(CompatibilityPage page, string methodName)
    {
        MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return Assert.IsInstanceOfType<Task>(method.Invoke(page, null));
    }

    private static void InvokeLifecycle(CompatibilityPage page, string methodName)
    {
        MethodInfo? method = typeof(CompatibilityPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(page, null);
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
