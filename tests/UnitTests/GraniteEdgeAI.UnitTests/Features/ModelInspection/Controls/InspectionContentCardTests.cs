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
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using Windows.Foundation;

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
            Grid[] rows = ProgressRows(content);
            TextBlock[] waitingLabels = descendants
                .OfType<TextBlock>()
                .Where(text =>
                    text.Text == "Waiting" &&
                    text.ActualHeight > 0d)
                .ToArray();
            InspectionStatusGlyph[] waitingGlyphs = descendants
                .OfType<InspectionStatusGlyph>()
                .Where(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Waiting)
                .ToArray();
            Button cancel = (Button)actions.FindName("CancelActionButton");
            Grid heading = Assert.IsInstanceOfType<Grid>(
                content.FindName("ProgressHeadingRow"));
            Grid progressView = Assert.IsInstanceOfType<Grid>(
                content.FindName("ProgressView"));
            Border completedCountChip = Assert.IsInstanceOfType<Border>(
                content.FindName("ProgressCompletedCountChip"));
            Border progressRowsSurface = Assert.IsInstanceOfType<Border>(
                content.FindName("ProgressRowsSurface"));
            TextBlock completedCountText = EnumerateDescendants(completedCountChip)
                .OfType<TextBlock>()
                .Single();
            Rectangle[] connectors = ProgressConnectors(content);

            Assert.AreEqual(ModelInspectionFigmaState.InspectionProgress, page.State);
            Assert.AreEqual("Inspection progress", page.ContentCard.SectionTitle);
            Assert.AreEqual("0 of 5 checks complete", page.ContentCard.ProgressSummary);
            Assert.AreEqual(new Thickness(24d), progressView.Padding, "progress view padding");
            Assert.HasCount(5, rows);
            Assert.IsTrue(rows.All(row => row.ActualHeight >= 48d));
            Assert.IsTrue(rows.All(row => Math.Abs(row.ActualHeight - 48d) <= 1d));
            Assert.AreEqual(new Thickness(1d), progressRowsSurface.BorderThickness);
            Assert.AreEqual(10d, progressRowsSurface.CornerRadius.TopLeft, 0.01d);
            Assert.AreSame(
                Application.Current.Resources["InspectionSurfaceSubtleBrush"],
                progressRowsSurface.Background);
            Assert.AreSame(
                Application.Current.Resources["InspectionBorderLightBrush"],
                progressRowsSurface.BorderBrush);
            AssertInRange(
                VerticalGap(heading, rows[0], content),
                14d,
                16d,
                "heading to first row gap");
            Assert.HasCount(10, connectors, "connector identities remain in the row template");
            Assert.IsTrue(connectors.All(connector =>
                    connector.Visibility == Visibility.Collapsed ||
                    connector.ActualWidth <= 0.01d ||
                    connector.ActualHeight <= 0.01d),
                "the retired vertical connector rail must occupy no visible geometry");
            AssertInRange(
                ContentBottomWhitespace(content, rows[^1]),
                24d,
                25d,
                "progress card bottom inset plus the grouped-list border");
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(completedCountChip));
            Assert.AreEqual(
                AccessibilityView.Content,
                AutomationProperties.GetAccessibilityView(completedCountText));
            Assert.AreEqual(
                page.ContentCard.ProgressSummary,
                AutomationProperties.GetName(completedCountText));
            Assert.AreSame(
                Application.Current.Resources["InspectionBlueSurfaceBrush"],
                completedCountChip.Background);
            Assert.AreSame(
                Application.Current.Resources["InspectionBlueBorderBrush"],
                completedCountChip.BorderBrush);
            Assert.AreSame(
                Application.Current.Resources["InspectionPrimaryBlueBrush"],
                completedCountText.Foreground);
            Assert.HasCount(5, waitingLabels);
            Assert.IsTrue(waitingLabels.All(label => ReferenceEquals(
                    Application.Current.Resources["InspectionTextSecondaryMutedBrush"],
                    label.Foreground)),
                "waiting labels use the approved muted semantic colour");
            Assert.HasCount(5, waitingGlyphs);
            Assert.IsTrue(waitingGlyphs.All(glyph =>
                Math.Abs(glyph.SurfaceSize - 22d) < 0.01d));
            CollectionAssert.AreEquivalent(
                new[] { "1", "2", "3", "4", "5" },
                waitingGlyphs.Select(glyph => glyph.StageNumber).ToArray());
            Assert.IsFalse(descendants
                .OfType<InspectionStatusGlyph>()
                .Any(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Active));
            Assert.AreEqual(Visibility.Visible, cancel.Visibility);
            Assert.IsFalse(cancel.IsEnabled);
            Assert.IsGreaterThanOrEqualTo(44d, cancel.ActualHeight);
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
    public async Task DisclosurePairs_UseNaturalCollapsedAndBoundedExpandedGeometry()
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
            Border shell = (Border)control.FindName("ContentCardShell");
            InspectionDisclosure disclosure = control.ActiveDisclosure!;
            Assert.IsNotNull(disclosure);
            FrameworkElement header = (FrameworkElement)disclosure.FindName(
                "DisclosureToggleButton");
            Border disclosureSurface = (Border)disclosure.FindName(
                "DisclosureCardSurface");
            InspectionStatusGlyph disclosureGlyph = EnumerateDescendants(header)
                .OfType<InspectionStatusGlyph>()
                .Single(IsEffectivelyVisible);
            double collapsedHeight = control.ActualHeight;
            Assert.AreEqual(0d, shell.MinHeight, 0.01d);
            Assert.AreEqual(22d, disclosureGlyph.SurfaceSize, 0.01d,
                "the disclosure information glyph aligns with the row glyphs");
            Assert.AreEqual(
                header.ActualHeight + disclosureSurface.BorderThickness.Top +
                    disclosureSurface.BorderThickness.Bottom,
                disclosure.ActualHeight,
                1d,
                "collapsed disclosure must equal its realized header height");

            control.Presentation = CreateDisclosurePresentation(isExpanded: true);
            control.UpdateLayout();

            Assert.IsGreaterThan(collapsedHeight, control.ActualHeight);
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
                .Where(row => Math.Abs(row.MinHeight - 48d) < 0.01)
                .ToArray();
            Assert.HasCount(3, reportRows);
            Assert.IsTrue(reportRows.All(row =>
                    row.ActualHeight >= 48d),
                $"report rows keep a 48px minimum and grow naturally: " +
                string.Join(", ", reportRows.Select(row => row.ActualHeight)));
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
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(12)]
    [DataRow(13)]
    public async Task FactoryTerminalStates_UseNaturalGeometryAndLeftAlignedLongCopy(
        int stateValue)
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

            Border shell = (Border)control.FindName("ContentCardShell");
            Assert.AreEqual(expectedState, page.State);
            Assert.AreEqual(0d, shell.MinHeight, 0.01d);
            Assert.IsGreaterThan(0d, control.ActualHeight);
            foreach (TextBlock text in EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Where(text => text.Text.Length >= 40))
            {
                Assert.AreEqual(
                    TextAlignment.Left,
                    text.TextAlignment,
                    $"long findings and diagnostics remain left aligned: {text.Text}");
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
    [DataRow((int)InspectionContentStatus.Passed, (int)InspectionStatusGlyphKind.Success, true)]
    [DataRow((int)InspectionContentStatus.Warning, (int)InspectionStatusGlyphKind.Warning, true)]
    [DataRow((int)InspectionContentStatus.Error, (int)InspectionStatusGlyphKind.Error, true)]
    [DataRow((int)InspectionContentStatus.Information, (int)InspectionStatusGlyphKind.Information, true)]
    [DataRow((int)InspectionContentStatus.Active, (int)InspectionStatusGlyphKind.Active, true)]
    [DataRow((int)InspectionContentStatus.Waiting, (int)InspectionStatusGlyphKind.Waiting, true)]
    [DataRow((int)InspectionContentStatus.Neutral, (int)InspectionStatusGlyphKind.NotComplete, false)]
    public void StatusMarkers_AreMutuallyExclusiveForEveryProgressStatus(
        int statusValue,
        int glyphKindValue,
        bool markerVisible)
    {
        InspectionContentStatus status = (InspectionContentStatus)statusValue;

        Assert.AreEqual(
            (InspectionStatusGlyphKind)glyphKindValue,
            InspectionContentCard.GetStatusGlyphKind(status));
        Assert.AreEqual(
            markerVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetStatusGlyphVisibility(status));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ProgressBindings_ObserveOwnerWithoutReplacingPresentation()
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        InspectionContentCardPresentation startupPresentation =
            InitialInspectionProgressPresentationFactory.Create(
                rows,
                new InspectionStartupPresentation
                {
                    Visibility = Visibility.Visible,
                    Summary = "Starting model inspection",
                    AutomationName = "Model inspection is starting."
                });
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create(rows);
        var driver = new RecordingProgressAnimationDriver();
        var control = new InspectionContentCard
        {
            Presentation = startupPresentation
        };
        control.SetMotionEnabled(true);
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
            await ResizeClientAndWaitAsync(window, control, 1000d);
            control.UpdateLayout();
            InspectionStatusGlyph startupGlyph =
                Assert.IsInstanceOfType<InspectionStatusGlyph>(
                    control.FindName("StartupActiveIndicatorHost"));
            Grid heading = Assert.IsInstanceOfType<Grid>(
                control.FindName("ProgressHeadingRow"));
            Grid startupRow = Assert.IsInstanceOfType<Grid>(
                control.FindName("StartupStatusRow"));
            Grid[] startupRows = ProgressRows(control);
            AssertInRange(
                VerticalGap(heading, startupRow, control),
                14d,
                16d,
                "heading to startup gap");
            AssertInRange(
                VerticalGap(startupRow, startupRows[0], control),
                14d,
                16d,
                "startup to first row gap");
            Assert.AreEqual(22d, startupGlyph.SurfaceSize, 0.01d);
            Assert.IsTrue(startupGlyph.IsPrecisionOrbitRunning,
                "visible startup glyph starts its precision orbit");

            control.Presentation = presentation;
            await Task.Yield();
            control.UpdateLayout();
            Assert.IsFalse(
                startupGlyph.IsPrecisionOrbitRunning,
                "A hidden startup row must not retain its precision orbit.");
            Assert.IsNull(startupGlyph.PrecisionOrbitAnimation);
            Grid[] collapsedStartupRows = ProgressRows(control);
            AssertInRange(
                VerticalGap(heading, collapsedStartupRows[0], control),
                14d,
                16d,
                "heading to first row gap after startup collapses");

            InspectionContentItemPresentation rowBefore = rows.Items[1];
            InspectionContentItemPresentation[] rowIdentities =
                rows.Items.ToArray();
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
            InspectionStatusGlyph activeGlyph = EnumerateDescendants(control)
                .OfType<InspectionStatusGlyph>()
                .Single(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Active);
            Assert.AreEqual(22d, activeGlyph.SurfaceSize);
            Assert.IsTrue(activeGlyph.IsMotionEnabled,
                "active stage glyph retains the motion policy");
            Assert.IsTrue(activeGlyph.IsPrecisionOrbitRunning,
                "active stage glyph starts its precision orbit");
            int orbitStartCount = activeGlyph.PrecisionOrbitStartCount;
            Assert.AreSame(rowBefore, rows.Items[1]);
            CollectionAssert.AreEqual(rowIdentities, rows.Items.ToArray());
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
            InspectionStatusGlyph quarterGlyph = EnumerateDescendants(control)
                .OfType<InspectionStatusGlyph>()
                .Single(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Active);
            Assert.AreEqual(statusAnimationCount, driver.StageStatusStartCount);
            Assert.AreEqual(detailAnimationCount, driver.ActiveDetailStartCount);
            Assert.AreEqual(
                announcementCount,
                control.LiveRegionChangeNotificationCount);
            Assert.AreSame(activeGlyph, quarterGlyph);
            Assert.IsTrue(quarterGlyph.IsPrecisionOrbitRunning,
                "fraction-only updates retain the active precision orbit");
            Assert.AreEqual(orbitStartCount, quarterGlyph.PrecisionOrbitStartCount);
            Assert.IsTrue(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text == "25%"),
                "fraction-only updates remain visible text evidence");

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
            Grid statusOwner = EnumerateDescendants(activeRow)
                .OfType<Grid>()
                .Single(grid =>
                    Grid.GetColumn(grid) == 2 &&
                    ReferenceEquals(VisualTreeHelper.GetParent(grid), activeRow));
            TextBlock activeStatus = EnumerateDescendants(statusOwner)
                .OfType<TextBlock>()
                .Where(IsEffectivelyVisible)
                .Single();
            Assert.AreEqual("Checking", activeStatus.Text);
            Assert.AreSame(
                Application.Current.Resources["InspectionPrimaryBlueBrush"],
                activeStatus.Foreground);
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
            StackPanel copyPanel = EnumerateDescendants(activeRow)
                .OfType<StackPanel>()
                .Single(panel =>
                    Grid.GetColumn(panel) == 1 &&
                    ReferenceEquals(VisualTreeHelper.GetParent(panel), activeRow));
            Viewbox glyphHost = EnumerateDescendants(activeRow)
                .OfType<Viewbox>()
                .Single(viewbox =>
                    Grid.GetColumn(viewbox) == 0 &&
                    ReferenceEquals(VisualTreeHelper.GetParent(viewbox), activeRow));
            Grid rowStateHost = activeRow.FindName("ProgressRowResponsiveHost") as Grid ??
                throw new AssertFailedException(
                    "The progress-row responsive state host was not realized.");
            string rowWidthState = VisualStateManager
                .GetVisualStateGroups(rowStateHost)
                .First(group => group.Name == "ProgressRowWidthStates")
                .CurrentState?.Name ?? "<none>";
            Assert.AreEqual(0, Grid.GetRow(copyPanel));
            Assert.AreEqual(1, Grid.GetColumnSpan(copyPanel),
                $"state={rowWidthState}; xamlRoot={control.XamlRoot.Size.Width}; " +
                $"control={control.ActualWidth}");
            Assert.AreEqual(1, Grid.GetRowSpan(glyphHost));
            Assert.AreEqual(0, Grid.GetRow(statusOwner));
            Assert.AreEqual(0, Grid.GetRow(directFractionText[0]));
            Assert.AreEqual(20d, glyphHost.ActualWidth, 0.01d);
            Assert.AreEqual(20d, glyphHost.ActualHeight, 0.01d);
            Border activeSurface = EnumerateDescendants(activeRow)
                .OfType<Border>()
                .Single(border => string.Equals(
                    border.Tag as string,
                    "InspectionProgressActiveSurface",
                    StringComparison.Ordinal));
            Assert.IsTrue(
                activeSurface.Visibility == Visibility.Collapsed ||
                activeSurface.ActualWidth <= 0.01d ||
                activeSurface.ActualHeight <= 0.01d,
                "the retired full-row active surface must occupy no visible geometry");
            double standardActiveHeight = activeRow.ActualHeight;
            TextBlock[] scalableActiveText = EnumerateDescendants(activeRow)
                .OfType<TextBlock>()
                .Where(text =>
                    text.Text == "Read model configuration" ||
                    text.Text == "Reading validated configuration." ||
                    text.Text == "Checking" ||
                    text.Text == "25%")
                .ToArray();
            Assert.IsNotEmpty(scalableActiveText);
            foreach (TextBlock text in scalableActiveText)
            {
                text.FontSize *= 2d;
            }
            control.UpdateLayout();
            Assert.IsGreaterThan(standardActiveHeight, activeRow.ActualHeight);
            Assert.IsTrue(scalableActiveText.All(text =>
                    text.ActualHeight + 1d >= text.DesiredSize.Height),
                "scaled active copy must fit its natural text height");
            Point copyOrigin = copyPanel.TransformToVisual(activeRow)
                .TransformPoint(default);
            Point statusOrigin = statusOwner.TransformToVisual(activeRow)
                .TransformPoint(default);
            Point fractionOrigin = directFractionText[0]
                .TransformToVisual(activeRow)
                .TransformPoint(default);
            Assert.IsTrue(
                copyOrigin.X + copyPanel.ActualWidth <= statusOrigin.X + 1d,
                "wide scaled status evidence remains after the stage copy");
            Assert.IsTrue(
                statusOrigin.X + statusOwner.ActualWidth <= fractionOrigin.X + 1d,
                "scaled status and fraction evidence do not collide");
            Assert.IsTrue(ProgressConnectors(control).All(connector =>
                    connector.Visibility == Visibility.Collapsed ||
                    connector.ActualWidth <= 0.01d ||
                    connector.ActualHeight <= 0.01d),
                "scaled progress rows must not restore the retired connector rail");

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
            InspectionStatusGlyph threeQuarterGlyph = EnumerateDescendants(control)
                .OfType<InspectionStatusGlyph>()
                .Single(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Active);
            Assert.AreEqual(statusAnimationCount, driver.StageStatusStartCount);
            Assert.AreEqual(detailAnimationCount, driver.ActiveDetailStartCount);
            Assert.AreEqual(
                announcementCount,
                control.LiveRegionChangeNotificationCount);
            Assert.AreSame(activeGlyph, threeQuarterGlyph);
            Assert.AreSame(rowBefore, rows.Items[1]);
            Assert.IsTrue(threeQuarterGlyph.IsPrecisionOrbitRunning,
                "three-quarter fraction retains the active precision orbit");
            Assert.AreEqual(
                orbitStartCount,
                threeQuarterGlyph.PrecisionOrbitStartCount);
            Assert.IsTrue(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text == "75%"),
                "three-quarter fraction remains visible text evidence");

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
            InspectionStatusGlyph completeFractionGlyph =
                EnumerateDescendants(control)
                .OfType<InspectionStatusGlyph>()
                .Single(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Active);
            Assert.AreEqual(statusAnimationCount, driver.StageStatusStartCount);
            Assert.AreEqual(detailAnimationCount, driver.ActiveDetailStartCount);
            Assert.AreEqual(
                announcementCount,
                control.LiveRegionChangeNotificationCount);
            Assert.AreSame(activeGlyph, completeFractionGlyph);
            Assert.IsTrue(completeFractionGlyph.IsPrecisionOrbitRunning,
                "complete fraction retains the active precision orbit");
            Assert.AreEqual(
                orbitStartCount,
                completeFractionGlyph.PrecisionOrbitStartCount);
            Assert.IsTrue(EnumerateDescendants(control)
                .OfType<TextBlock>()
                .Any(text => text.Text == "100%"),
                "complete fraction remains visible text evidence");

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
            InspectionStatusGlyph[] visibleGlyphs = descendants
                .OfType<InspectionStatusGlyph>()
                .Where(IsEffectivelyVisible)
                .ToArray();

            Assert.AreSame(presentation, control.Presentation);
            CollectionAssert.Contains(visibleText, "2 of 5 checks complete");
            CollectionAssert.Contains(visibleText, "Warning");
            TextBlock warningStatus = descendants
                .OfType<TextBlock>()
                .Single(text =>
                    IsEffectivelyVisible(text) &&
                    text.Text == "Warning");
            Assert.AreSame(
                Application.Current.Resources["InspectionWarningTextBrush"],
                warningStatus.Foreground);
            Assert.IsTrue(visibleGlyphs.Any(glyph =>
                    glyph.Kind == InspectionStatusGlyphKind.Warning &&
                    Math.Abs(glyph.SurfaceSize - 22d) < 0.01d),
                "warning rows retain the approved compact glyph footprint");
            Assert.IsFalse(visibleGlyphs.Any(glyph =>
                glyph.Kind == InspectionStatusGlyphKind.Active));

            rows.Apply(new InspectionProgressRowsUpdate(
                new ModelInspectionProgressRegionKey(
                    ModelInspectionStage.ReadModelConfiguration,
                    ModelInspectionStageStatus.Active,
                    completedStageCount: 1,
                    stageCount: 5,
                    stageFraction: 0.75,
                    detail: "Reading validated configuration."),
                new ModelInspectionRenderKey(1, 6),
                "1 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();
            InspectionStatusGlyph retiringGlyph = EnumerateDescendants(control)
                .OfType<InspectionStatusGlyph>()
                .Single(glyph =>
                    IsEffectivelyVisible(glyph) &&
                    glyph.Kind == InspectionStatusGlyphKind.Active);
            Assert.IsTrue(retiringGlyph.IsPrecisionOrbitRunning,
                "the restored active glyph resumes its precision orbit");

            control.Presentation = CreateFactoryTerminalPresentation(
                ModelInspectionFigmaState.ReadyWithWarningsCollapsed).ContentCard;
            await Task.Yield();
            control.UpdateLayout();

            Assert.IsFalse(
                retiringGlyph.IsPrecisionOrbitRunning,
                "A collapsed retained progress template must not keep animating.");
            Assert.IsNull(retiringGlyph.PrecisionOrbitAnimation);
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

    private static async Task ResizeClientAndWaitAsync(
        Window window,
        FrameworkElement element,
        double effectiveWidth)
    {
        var layoutReached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void CompleteWhenReady(object? sender, object eventArguments)
        {
            if (Math.Abs(element.XamlRoot.Size.Width - effectiveWidth) <= 1d)
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

    private static Grid[] ProgressRows(InspectionContentCard content)
    {
        ItemsRepeater repeater = Assert.IsInstanceOfType<ItemsRepeater>(
            content.FindName("ProgressItemsRepeater"));
        var rows = new List<Grid>();
        for (int index = 0;
             index < content.Presentation.ProgressRows.Items.Count;
             index++)
        {
            FrameworkElement realized = Assert.IsInstanceOfType<FrameworkElement>(
                repeater.TryGetElement(index));
            Grid row = realized as Grid ?? EnumerateDescendants(realized)
                .OfType<Grid>()
                .Single(candidate => string.Equals(
                    candidate.Tag as string,
                    "InspectionProgressRow",
                    StringComparison.Ordinal));
            rows.Add(row);
        }

        return rows.ToArray();
    }

    private static Rectangle[] ProgressConnectors(InspectionContentCard content) =>
        ProgressRows(content)
            .SelectMany(row => EnumerateDescendants(row).OfType<Rectangle>())
            .Where(connector => string.Equals(
                    connector.Tag as string,
                    "InspectionProgressConnectorStart",
                    StringComparison.Ordinal) ||
                string.Equals(
                    connector.Tag as string,
                    "InspectionProgressConnectorEnd",
                    StringComparison.Ordinal))
            .ToArray();

    private static double VerticalGap(
        FrameworkElement upper,
        FrameworkElement lower,
        UIElement root)
    {
        Point upperOrigin = upper.TransformToVisual(root).TransformPoint(new Point());
        Point lowerOrigin = lower.TransformToVisual(root).TransformPoint(new Point());
        return lowerOrigin.Y - (upperOrigin.Y + upper.ActualHeight);
    }

    private static void AssertInRange(
        double actual,
        double minimum,
        double maximum,
        string context) =>
        Assert.IsTrue(
            actual >= minimum && actual <= maximum,
            $"{context}: {actual} is outside [{minimum}, {maximum}].");

    private static void AssertConnectorGeometry(
        IReadOnlyList<Grid> rows,
        IReadOnlyList<Rectangle> connectors,
        UIElement root)
    {
        for (int index = 0; index < connectors.Count; index++)
        {
            InspectionStatusGlyph currentGlyph = EnumerateDescendants(rows[index])
                .OfType<InspectionStatusGlyph>()
                .Single(IsEffectivelyVisible);
            InspectionStatusGlyph nextGlyph = EnumerateDescendants(rows[index + 1])
                .OfType<InspectionStatusGlyph>()
                .Single(IsEffectivelyVisible);
            Rectangle connectorEnd = EnumerateDescendants(rows[index + 1])
                .OfType<Rectangle>()
                .Single(connector =>
                    string.Equals(
                        connector.Tag as string,
                        "InspectionProgressConnectorEnd",
                        StringComparison.Ordinal) &&
                    IsEffectivelyVisible(connector));
            Point connectorOrigin = connectors[index]
                .TransformToVisual(root)
                .TransformPoint(new Point());
            Point connectorEndOrigin = connectorEnd
                .TransformToVisual(root)
                .TransformPoint(new Point());
            Point currentOrigin = currentGlyph
                .TransformToVisual(root)
                .TransformPoint(new Point());
            Point nextOrigin = nextGlyph
                .TransformToVisual(root)
                .TransformPoint(new Point());

            Assert.AreEqual(
                currentOrigin.Y + (currentGlyph.ActualHeight / 2d),
                connectorOrigin.Y,
                0.5d,
                $"connector {index + 1} start");
            Assert.AreEqual(
                connectorOrigin.Y + connectors[index].ActualHeight,
                connectorEndOrigin.Y,
                0.5d,
                $"connector {index + 1} segment join");
            Assert.AreEqual(
                nextOrigin.Y + (nextGlyph.ActualHeight / 2d),
                connectorEndOrigin.Y + connectorEnd.ActualHeight,
                0.5d,
                $"connector {index + 1} end");
        }
    }

    private static double FinalConnectorTailHeight(Grid finalRow) =>
        EnumerateDescendants(finalRow)
            .OfType<Rectangle>()
            .Where(connector =>
                string.Equals(
                    connector.Tag as string,
                    "InspectionProgressConnectorStart",
                    StringComparison.Ordinal) &&
                IsEffectivelyVisible(connector))
            .Sum(connector => connector.ActualHeight);

    private static double ContentBottomWhitespace(
        InspectionContentCard content,
        FrameworkElement finalRow)
    {
        Grid shell = Assert.IsInstanceOfType<Grid>(
            content.FindName("ProgressView"));
        Point finalOrigin = finalRow
            .TransformToVisual(shell)
            .TransformPoint(new Point());
        return shell.ActualHeight - finalOrigin.Y - finalRow.ActualHeight;
    }

    private static bool IsEffectivelyVisible(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is UIElement uiElement &&
                uiElement.Visibility != Visibility.Visible)
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return element.ActualWidth > 0d && element.ActualHeight > 0d;
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
