using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class InspectionContentCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InitialFactoryState_RendersFiveWaitingRowsAndDisabledCancel()
    {
        RecordingCommand command = PresentationTestData.CreateCommand();
        ModelInspectionPagePresentation page =
            ModelInspectionPresentationFactory.Create(
                PresentationTestData.CreateRequest(),
                ModelInspectionViewSnapshot.Initial,
                new ModelInspectionPresentationCommands(
                    command,
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                new InspectionProgressRows());
        var content = new InspectionContentCard
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            Presentation = page.ContentCard
        };
        var actions = new InspectionActionCard
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            Presentation = page.ActionCard
        };
        var host = new StackPanel { Width = 840 };
        host.Children.Add(content);
        host.Children.Add(actions);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        host.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window
        {
            Content = new ScrollViewer { Content = host }
        };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.UpdateLayout();

            DependencyObject[] descendants =
                EnumerateDescendants(content).ToArray();
            Grid[] rows = descendants
                .OfType<Grid>()
                .Where(row =>
                    Math.Abs(row.MinHeight - 60d) < 0.01 &&
                    !string.IsNullOrWhiteSpace(
                        AutomationProperties.GetName(row)))
                .ToArray();
            TextBlock[] waitingLabels = descendants
                .OfType<TextBlock>()
                .Where(text =>
                    text.Text == "Waiting" &&
                    text.ActualHeight > 0d)
                .ToArray();
            ProgressRing[] visibleSpinners = descendants
                .OfType<ProgressRing>()
                .Where(ring =>
                    ring.Visibility == Visibility.Visible &&
                    ring.ActualHeight > 0d)
                .ToArray();
            Button cancel = (Button)actions.FindName("CancelActionButton");

            Assert.AreEqual(ModelInspectionFigmaState.InspectionProgress, page.State);
            Assert.AreEqual("Inspection progress", page.ContentCard.SectionTitle);
            Assert.AreEqual("0 of 5 checks complete", page.ContentCard.ProgressSummary);
            Assert.HasCount(5, rows);
            Assert.IsTrue(rows.All(row => Math.Abs(row.ActualHeight - 60d) < 0.01));
            Assert.HasCount(5, waitingLabels);
            Assert.HasCount(0, visibleSpinners);
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsFalse(cancel.IsEnabled);
            Assert.AreEqual("Cancel inspection", page.ActionCard.CancelAction.Text);
            Assert.AreEqual(
                page.ActionCard.CancelAction.AutomationName,
                AutomationProperties.GetName(cancel));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Disclosure_IsStableAcrossPresentationRevisionsAndForwardsCurrentRequests()
    {
        var control = new InspectionContentCard
        {
            Presentation = CreateDisclosurePresentation(isExpanded: false)
        };
        InspectionDisclosure first = control.ActiveDisclosure!;
        Assert.IsNotNull(first);
        List<bool> requests = [];
        control.DisclosureToggleRequested += (_, args) => requests.Add(args.IsExpanded);

        Button toggle = (Button)first.FindName("DisclosureToggleButton");
        var peer = new ButtonAutomationPeer(toggle);
        Assert.IsInstanceOfType<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke)).Invoke();

        control.Presentation = CreateDisclosurePresentation(isExpanded: true);
        InspectionDisclosure second = control.ActiveDisclosure!;
        Assert.IsNotNull(second);

        Assert.AreSame(first, second);
        CollectionAssert.AreEqual(new[] { true }, requests);
        Assert.AreEqual(Visibility.Visible, second.ViewportTarget.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DisclosurePairs_UseApprovedCollapsedAndExpandedMinimumGeometry()
    {
        var control = new InspectionContentCard
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Presentation = CreateDisclosurePresentation(isExpanded: false)
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();
            Assert.AreEqual(
                232d,
                control.ActualHeight,
                1d,
                "warning collapsed height");

            control.Presentation = CreateDisclosurePresentation(isExpanded: true);
            control.UpdateLayout();

            Assert.AreEqual(
                380d,
                control.ActualHeight,
                1d,
                "warning expanded height");
            InspectionDisclosure disclosure = control.ActiveDisclosure!;
            Assert.IsNotNull(disclosure);
            ScrollViewer report =
                (ScrollViewer)control.FindName("ExpandedReportScrollViewer");
            FrameworkElement fade =
                (FrameworkElement)control.FindName("ExpandedReportBottomFade");
            TextBlock affordance =
                (TextBlock)control.FindName("ExpandedReportScrollAffordance");
            Assert.AreEqual(172d, report.MaxHeight, 0.01);
            Assert.AreEqual(
                Visibility.Visible,
                disclosure.ViewportTarget.Visibility);
            Assert.IsGreaterThan(0d, fade.Height);
            Assert.AreEqual("Scroll for more", affordance.Text);

            Border[] reportRows = EnumerateDescendants(report)
                .OfType<Border>()
                .Where(row => Math.Abs(row.MinHeight - 58d) < 0.01)
                .ToArray();
            Assert.HasCount(3, reportRows);
            Assert.IsTrue(reportRows.All(row =>
                Math.Abs(row.ActualHeight - 58d) < 0.01));
            string[] expectedTitles =
            [
                "Chat template warning",
                "Tokenizer check",
                "Runtime check"
            ];
            for (int index = 0; index < reportRows.Length; index++)
            {
                TextBlock title = EnumerateDescendants(reportRows[index])
                    .OfType<TextBlock>()
                    .Single(text => text.Text == expectedTitles[index]);
                AutomationPeer titlePeer =
                    FrameworkElementAutomationPeer.CreatePeerForElement(title)
                    ?? new TextBlockAutomationPeer(title);

                Assert.AreEqual(
                    $"{expectedTitles[index]}. Warning.",
                    titlePeer.GetName());
                Assert.AreEqual(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(reportRows[index]));
            }

            Assert.IsTrue(report.IsTabStop);
            Assert.IsGreaterThan(0d, report.ScrollableHeight);
            Assert.IsTrue(report.Focus(FocusState.Keyboard));
            Assert.AreSame(report, FocusManager.GetFocusedElement(control.XamlRoot));
            var scrollPeer = new ScrollViewerAutomationPeer(report);
            IScrollProvider scrollProvider = Assert.IsInstanceOfType<IScrollProvider>(
                scrollPeer.GetPattern(PatternInterface.Scroll));
            Assert.IsTrue(scrollProvider.VerticallyScrollable);
            double initialOffset = report.VerticalOffset;
            scrollProvider.Scroll(
                ScrollAmount.NoAmount,
                ScrollAmount.SmallIncrement);
            control.UpdateLayout();
            Assert.IsGreaterThan(initialOffset, report.VerticalOffset);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(4, 232d)]
    [DataRow(5, 380d)]
    [DataRow(6, 232d)]
    [DataRow(7, 365d)]
    [DataRow(8, 232d)]
    [DataRow(9, 232d)]
    [DataRow(10, 248d)]
    [DataRow(11, 380d)]
    [DataRow(12, 202d)]
    [DataRow(13, 248d)]
    public async Task FactoryTerminalStates_UseApprovedStandardGeometry(
        int stateValue,
        double expectedHeight)
    {
        ModelInspectionFigmaState expectedState =
            (ModelInspectionFigmaState)stateValue;
        ModelInspectionPagePresentation page =
            CreateFactoryTerminalPresentation(expectedState);
        var control = new InspectionContentCard
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Presentation = page.ContentCard
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();

            Assert.AreEqual(expectedState, page.State);
            Assert.AreEqual(expectedHeight, control.ActualHeight, 1d);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void TechnicalDetailsFuture_ExposesDisabledHelpTooltipAndAdjacentText()
    {
        var control = new InspectionContentCard
        {
            Presentation = new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Unsupported,
                SectionTitle = "Why it cannot continue",
                TechnicalDetailsVisibility = Visibility.Visible,
                TechnicalDetailsActionText = "View technical details",
                TechnicalDetailsAutomationName = "View technical details",
                TechnicalDetailsAutomationHelpText = "Coming later",
                IsTechnicalDetailsEnabled = false
            }
        };
        Button button = (Button)control.FindName("TechnicalDetailsButton");
        TextBlock help = (TextBlock)control.FindName("TechnicalDetailsFutureHelpText");

        Assert.IsFalse(button.IsEnabled);
        Assert.AreEqual("Coming later", AutomationProperties.GetHelpText(button));
        Assert.AreEqual("Coming later", ToolTipService.GetToolTip(button));
        Assert.AreEqual(Visibility.Visible, help.Visibility);
        Assert.AreEqual("Coming later", help.Text);
        Assert.AreEqual(
            "View technical details. Coming later",
            AutomationProperties.GetName(help));
    }

    [TestMethod]
    public void HiddenPresentations_DoNotShareProgressFallbackOrItems()
    {
        InspectionContentCardPresentation first =
            InspectionContentCardPresentation.Hidden;
        InspectionContentCardPresentation second =
            InspectionContentCardPresentation.Hidden;

        Assert.IsNull(GetStoredProgressRows(first));
        Assert.IsNull(GetStoredProgressRows(second));

        InspectionProgressRows firstOwner = first.ProgressRows;
        InspectionProgressRows secondOwner = second.ProgressRows;

        Assert.AreSame(firstOwner, first.ProgressRows);
        Assert.AreSame(secondOwner, second.ProgressRows);
        Assert.AreNotSame(firstOwner, secondOwner);
        Assert.AreNotSame(firstOwner.Items, secondOwner.Items);
        Assert.AreNotSame(first.Items, second.Items);
        Assert.HasCount(0, first.Items);
        Assert.HasCount(0, second.Items);
    }

    [TestMethod]
    public void NonProgressGetters_DoNotAllocateProgressOwner()
    {
        InspectionContentCardPresentation hidden =
            InspectionContentCardPresentation.Hidden;
        var terminalItems = Array.AsReadOnly(
            new[]
            {
                new InspectionContentItemPresentation
                {
                    Title = "Safe terminal finding"
                }
            });
        var terminal = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            ProgressSummary = "Complete",
            Items = terminalItems
        };

        Assert.IsNull(GetStoredProgressRows(hidden));
        Assert.IsNull(GetStoredProgressRows(terminal));

        Assert.AreEqual(string.Empty, hidden.ProgressSummary);
        Assert.HasCount(0, hidden.Items);
        Assert.AreEqual("Complete", terminal.ProgressSummary);
        Assert.AreSame(terminalItems, terminal.Items);

        Assert.IsNull(GetStoredProgressRows(hidden));
        Assert.IsNull(GetStoredProgressRows(terminal));
    }

    [TestMethod]
    public void ExplicitProgressOwner_IsStoredDirectlyAndNullIsRejected()
    {
        InspectionProgressRows owner = new();
        var presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            ProgressRows = owner
        };

        Assert.AreSame(owner, GetStoredProgressRows(presentation));
        Assert.AreSame(owner, presentation.ProgressRows);
        Assert.AreSame(owner.Items, presentation.Items);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _ = new InspectionContentCardPresentation
            {
                ProgressRows = null!
            });
    }

    [TestMethod]
    [DataRow((int)InspectionContentStatus.Passed, true, false, false, (int)Symbol.Accept)]
    [DataRow((int)InspectionContentStatus.Warning, true, false, false, (int)Symbol.Important)]
    [DataRow((int)InspectionContentStatus.Error, true, false, false, (int)Symbol.Cancel)]
    [DataRow((int)InspectionContentStatus.Information, true, false, false, (int)Symbol.Help)]
    [DataRow((int)InspectionContentStatus.Active, false, true, false, (int)Symbol.Clock)]
    [DataRow((int)InspectionContentStatus.Waiting, false, false, true, (int)Symbol.Clock)]
    [DataRow((int)InspectionContentStatus.Neutral, false, false, false, (int)Symbol.Help)]
    public void StatusMarkers_AreMutuallyExclusiveForEveryProgressStatus(
        int statusValue,
        bool terminalVisible,
        bool activeVisible,
        bool waitingVisible,
        int symbolValue)
    {
        InspectionContentStatus status = (InspectionContentStatus)statusValue;

        Assert.AreEqual(
            terminalVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetTerminalMarkerVisibility(status));
        Assert.AreEqual(
            activeVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetActiveVisibility(status));
        Assert.AreEqual(
            waitingVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetWaitingVisibility(status));
        Assert.AreEqual((Symbol)symbolValue, InspectionContentCard.GetStatusSymbol(status));
        Assert.AreEqual(
            status == InspectionContentStatus.Neutral ? 0 : 1,
            new[] { terminalVisible, activeVisible, waitingVisible }.Count(value => value),
            status is InspectionContentStatus.Neutral
                ? "Neutral intentionally renders no progress marker."
                : $"{status} must render exactly one marker.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ProgressBindings_ObserveOwnerWithoutReplacingPresentation()
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create(rows);
        var driver = new RecordingProgressAnimationDriver();
        var control = new InspectionContentCard
        {
            Presentation = presentation
        };
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
            InspectionContentItemPresentation rowBefore = rows.Items[1];
            InspectionProgressRowsApplyResult active = rows.Apply(
                new InspectionProgressRowsUpdate(
                    new ModelInspectionProgressRegionKey(
                        ModelInspectionStage.ReadModelConfiguration,
                        ModelInspectionStageStatus.Active,
                        completedStageCount: 1,
                        stageCount: 5,
                        stageFraction: null,
                        detail: "Reading validated configuration."),
                    new ModelInspectionRenderKey(1, 1),
                    "1 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();
            control.AnimateProgressChanges(
                active,
                driver,
                new ModelInspectionVisualOperationKey(
                    new ModelInspectionRenderKey(1, 1),
                    interactionRevision: 0),
                _ => true);
            ProgressRing activeGlyph = EnumerateDescendants(control)
                .OfType<ProgressRing>()
                .Single(ring =>
                    ring.Visibility == Visibility.Visible &&
                    ring.ActualHeight > 0d);
            Assert.IsTrue(activeGlyph.IsIndeterminate);
            Assert.AreSame(rowBefore, rows.Items[1]);
            Assert.IsFalse(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text.EndsWith('%')));

            control.AnnounceProgress(
                "Inspection progress. 1 of 5 checks complete. " +
                "Read model configuration. Checking.");
            int announcementCount =
                control.LiveRegionChangeNotificationCount;
            int statusAnimationCount = driver.StageStatusStartCount;
            int detailAnimationCount = driver.ActiveDetailStartCount;

            InspectionProgressRowsApplyResult quarter = rows.Apply(
                new InspectionProgressRowsUpdate(
                    new ModelInspectionProgressRegionKey(
                        ModelInspectionStage.ReadModelConfiguration,
                        ModelInspectionStageStatus.Active,
                        completedStageCount: 1,
                        stageCount: 5,
                        stageFraction: 0.25,
                        detail: "Reading validated configuration."),
                    new ModelInspectionRenderKey(1, 2),
                    "1 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();
            control.AnimateProgressChanges(
                quarter,
                driver,
                new ModelInspectionVisualOperationKey(
                    new ModelInspectionRenderKey(1, 2),
                    interactionRevision: 0),
                _ => true);
            ProgressRing quarterGlyph = EnumerateDescendants(control)
                .OfType<ProgressRing>()
                .Single(ring =>
                    ring.Visibility == Visibility.Visible &&
                    ring.ActualHeight > 0d);
            Assert.AreEqual(statusAnimationCount, driver.StageStatusStartCount);
            Assert.AreEqual(detailAnimationCount, driver.ActiveDetailStartCount);
            Assert.AreEqual(
                announcementCount,
                control.LiveRegionChangeNotificationCount);
            Assert.AreSame(activeGlyph, quarterGlyph);
            Assert.IsTrue(quarterGlyph.IsIndeterminate);
            Assert.IsTrue(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text == "25%"));

            Grid activeRow = EnumerateDescendants(control)
                .OfType<Grid>()
                .Single(grid =>
                    string.Equals(
                        grid.Tag as string,
                        "InspectionProgressRow",
                        StringComparison.Ordinal) &&
                    EnumerateDescendants(grid)
                        .OfType<TextBlock>()
                        .Any(text => text.Text ==
                            "Read model configuration"));
            TextBlock[] directStatusText = EnumerateDescendants(activeRow)
                .OfType<TextBlock>()
                .Where(text =>
                    Grid.GetColumn(text) == 2 &&
                    ReferenceEquals(
                        VisualTreeHelper.GetParent(text),
                        activeRow))
                .ToArray();
            Assert.AreEqual(
                1,
                directStatusText.Length,
                "The fixture observer requires one direct status TextBlock " +
                "at progress-row column 2.");
            Assert.AreEqual("Checking", directStatusText[0].Text);
            TextBlock[] directFractionText = EnumerateDescendants(activeRow)
                .OfType<TextBlock>()
                .Where(text =>
                    Grid.GetColumn(text) == 3 &&
                    ReferenceEquals(
                        VisualTreeHelper.GetParent(text),
                        activeRow))
                .ToArray();
            Assert.AreEqual(
                1,
                directFractionText.Length,
                "Native fraction text must occupy its own trailing column.");
            Assert.AreEqual("25%", directFractionText[0].Text);

            InspectionProgressRowsApplyResult threeQuarters = rows.Apply(
                new InspectionProgressRowsUpdate(
                    new ModelInspectionProgressRegionKey(
                        ModelInspectionStage.ReadModelConfiguration,
                        ModelInspectionStageStatus.Active,
                        completedStageCount: 1,
                        stageCount: 5,
                        stageFraction: 0.75,
                        detail: "Reading validated configuration."),
                    new ModelInspectionRenderKey(1, 3),
                    "1 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();
            control.AnimateProgressChanges(
                threeQuarters,
                driver,
                new ModelInspectionVisualOperationKey(
                    new ModelInspectionRenderKey(1, 3),
                    interactionRevision: 0),
                _ => true);
            ProgressRing threeQuarterGlyph = EnumerateDescendants(control)
                .OfType<ProgressRing>()
                .Single(ring =>
                    ring.Visibility == Visibility.Visible &&
                    ring.ActualHeight > 0d);
            Assert.AreEqual(statusAnimationCount, driver.StageStatusStartCount);
            Assert.AreEqual(detailAnimationCount, driver.ActiveDetailStartCount);
            Assert.AreEqual(
                announcementCount,
                control.LiveRegionChangeNotificationCount);
            Assert.AreSame(activeGlyph, threeQuarterGlyph);
            Assert.AreSame(rowBefore, rows.Items[1]);
            Assert.IsTrue(threeQuarterGlyph.IsIndeterminate);
            Assert.IsTrue(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text == "75%"));

            InspectionProgressRowsApplyResult completeFraction = rows.Apply(
                new InspectionProgressRowsUpdate(
                    new ModelInspectionProgressRegionKey(
                        ModelInspectionStage.ReadModelConfiguration,
                        ModelInspectionStageStatus.Active,
                        completedStageCount: 1,
                        stageCount: 5,
                        stageFraction: 1d,
                        detail: "Reading validated configuration."),
                    new ModelInspectionRenderKey(1, 4),
                    "1 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();
            control.AnimateProgressChanges(
                completeFraction,
                driver,
                new ModelInspectionVisualOperationKey(
                    new ModelInspectionRenderKey(1, 4),
                    interactionRevision: 0),
                _ => true);
            ProgressRing completeFractionGlyph = EnumerateDescendants(control)
                .OfType<ProgressRing>()
                .Single(ring =>
                    ring.Visibility == Visibility.Visible &&
                    ring.ActualHeight > 0d);
            Assert.AreEqual(statusAnimationCount, driver.StageStatusStartCount);
            Assert.AreEqual(detailAnimationCount, driver.ActiveDetailStartCount);
            Assert.AreEqual(
                announcementCount,
                control.LiveRegionChangeNotificationCount);
            Assert.AreSame(activeGlyph, completeFractionGlyph);
            Assert.IsTrue(completeFractionGlyph.IsIndeterminate);
            Assert.IsTrue(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text == "100%"));

            rows.Apply(new InspectionProgressRowsUpdate(
                new ModelInspectionProgressRegionKey(
                    ModelInspectionStage.ReadModelConfiguration,
                    ModelInspectionStageStatus.Warning,
                    completedStageCount: 2,
                    stageCount: 5,
                    stageFraction: null,
                    detail: "Configuration completed with a warning."),
                new ModelInspectionRenderKey(1, 5),
                "2 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();

            IReadOnlyList<DependencyObject> descendants =
                EnumerateDescendants(control).ToArray();
            string[] visibleText = descendants
                .OfType<TextBlock>()
                .Where(text => text.Visibility == Visibility.Visible)
                .Select(text => text.Text)
                .ToArray();
            SymbolIcon[] visibleSymbols = descendants
                .OfType<SymbolIcon>()
                .Where(icon => icon.Visibility == Visibility.Visible)
                .ToArray();

            Assert.AreSame(presentation, control.Presentation);
            CollectionAssert.Contains(visibleText, "2 of 5 checks complete");
            CollectionAssert.Contains(visibleText, "Warning");
            Assert.IsTrue(visibleSymbols.Any(icon => icon.Symbol == Symbol.Important));
            Assert.IsFalse(descendants
                .OfType<ProgressRing>()
                .Any(ring =>
                    ring.Visibility == Visibility.Visible &&
                    ring.ActualHeight > 0d));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> EnumerateDescendants(
        DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static InspectionProgressRows? GetStoredProgressRows(
        InspectionContentCardPresentation presentation)
    {
        FieldInfo[] ownerFields = typeof(InspectionContentCardPresentation)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(InspectionProgressRows))
            .ToArray();
        Assert.HasCount(
            1,
            ownerFields,
            "The presentation must have exactly one progress-owner storage field.");
        return (InspectionProgressRows?)ownerFields[0].GetValue(presentation);
    }

    private static InspectionContentCardPresentation CreateDisclosurePresentation(
        bool isExpanded)
    {
        InspectionContentItemPresentation finding = new()
        {
            Title = "Chat template warning",
            Detail = "A compatible template was not reported.",
            DetailVisibility = Visibility.Visible,
            Status = InspectionContentStatus.Warning,
            StatusText = "Warning",
            AutomationName = "Chat template warning. Warning."
        };

        return new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Warnings,
            SectionTitle = "Inspection warnings",
            Items = Array.AsReadOnly(new[] { finding }),
            DisclosureVisibility = Visibility.Visible,
            DisclosureStatus = InspectionContentStatus.Warning,
            DisclosureSummary = "4 checks passed, 1 warning",
            CollapsedDisclosureText = "View full details",
            ExpandedDisclosureText = "Hide full details",
            DisclosureAutomationName = "Inspection warning details",
            ExpandedItems = Array.AsReadOnly(
                new[]
                {
                    CreateFinding("Chat template warning"),
                    CreateFinding("Tokenizer check"),
                    CreateFinding("Runtime check")
                }),
            IsExpanded = isExpanded
        };
    }

    private static ModelInspectionPagePresentation CreateFactoryTerminalPresentation(
        ModelInspectionFigmaState state)
    {
        ModelInspectionExecutionResult execution = state switch
        {
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded =>
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.ReadyWithWarnings)),
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded =>
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.ConversionRequired)),
            ModelInspectionFigmaState.IncompletePackage =>
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.IncompletePackage)),
            ModelInspectionFigmaState.Unsupported =>
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.Unsupported)),
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded =>
                ModelInspectionExecutionResult.Completed(
                    PresentationTestData.CreateResult(
                        ModelInspectionOutcome.Invalid)),
            ModelInspectionFigmaState.Cancelled =>
                ModelInspectionExecutionResult.Cancelled(cooperative: true),
            ModelInspectionFigmaState.OperationalFailure =>
                ModelInspectionExecutionResult.OperationalFailure(
                    PresentationTestData.CreateFailure()),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        var renderKey = new ModelInspectionRenderKey(1, 1);
        var snapshot = new ModelInspectionViewSnapshot(
            renderKey,
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: execution);
        var rows = new InspectionProgressRows();
        rows.Reset(new ModelInspectionRenderKey(1, 0));

        return ModelInspectionPresentationFactory.Create(
            PresentationTestData.CreateRequest(),
            snapshot,
            new ModelInspectionPresentationCommands(
                PresentationTestData.CreateCommand(),
                PresentationTestData.CreateCommand(),
                PresentationTestData.CreateCommand()),
            isDisclosureExpanded: state is
                ModelInspectionFigmaState.ReadyWithWarningsExpanded or
                ModelInspectionFigmaState.ConversionRequiredExpanded or
                ModelInspectionFigmaState.InvalidExpanded,
            rows);
    }

    private sealed class RecordingProgressAnimationDriver :
        IModelInspectionAnimationDriver
    {
        internal int StageStatusStartCount { get; private set; }

        internal int ActiveDetailStartCount { get; private set; }

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            StageStatusStartCount++;

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            ActiveDetailStartCount++;

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
        }

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
        }

        public void CancelAll()
        {
        }

        public void Dispose()
        {
        }
    }

    private static InspectionContentItemPresentation CreateFinding(string title) =>
        new()
        {
            Title = title,
            Detail = "A compatible value was not reported.",
            DetailVisibility = Visibility.Visible,
            Status = InspectionContentStatus.Warning,
            StatusText = "Warning",
            AutomationName = $"{title}. Warning."
        };
}
