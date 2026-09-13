using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
[DoNotParallelize]
public sealed class HardwareInspectionPageTests
{
    private readonly HardwareInspectionPresentationFactory _factory = new();
    private ResourceDictionary? _journeyResources;

    [TestInitialize]
    public void InstallJourneyResources()
    {
        _journeyResources = new ResourceDictionary
        {
            Source = new Uri(
                "ms-appx:///Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml")
        };
        Application.Current.Resources.MergedDictionaries.Add(_journeyResources);
    }

    [TestCleanup]
    public void RemoveJourneyResources()
    {
        if (_journeyResources is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_journeyResources);
            _journeyResources = null;
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_DefaultsToApprovedLightPresentationAndProvidesShellFooterSlot()
    {
        HardwareInspectionPage page = new();
        TextBlock footer = new() { Text = "MODEL SETUP · STEP 3 OF 5" };
        page.FooterContent = footer;

        Assert.AreEqual(ElementTheme.Default, page.RequestedTheme,
            "The feature page must inherit the onboarding shell theme.");
        Assert.IsNotNull(page.FindName("FooterPresenter"));
        Assert.AreSame(footer, ((ContentPresenter)page.FindName("FooterPresenter")).Content);
        TextBlock title = Text(page, "PageTitleTextBlock");
        TextBlock subtitle = Text(page, "PageSubtitleTextBlock");
        Assert.AreEqual(TextAlignment.Center, title.TextAlignment);
        Assert.AreEqual(TextAlignment.Center, subtitle.TextAlignment);
        Assert.AreEqual(HorizontalAlignment.Stretch, title.HorizontalAlignment);
        Assert.AreEqual(HorizontalAlignment.Stretch, subtitle.HorizontalAlignment);
        Border status = (Border)Element(page, "InspectionIdentityStatus");
        TextBlock statusText = Text(page, "InspectionIdentityStatusText");
        Assert.IsTrue(double.IsNaN(status.Height));
        Assert.AreEqual(32d, status.MinHeight);
        Assert.AreEqual(TextWrapping.WrapWholeWords, statusText.TextWrapping,
            "The identity badge must grow and wrap at elevated text scales.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_ActiveShowsOnlyExactProgressSurface()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState state = _factory.CreateActive(
            HardwareInspectionStage.DetectingGraphicsHardware);
        page.Apply(state);

        Assert.AreEqual("Hardware inspection", Text(page, "PageTitleTextBlock").Text);
        Assert.AreEqual(state.Subtitle, Text(page, "PageSubtitleTextBlock").Text);
        Assert.AreEqual(Visibility.Visible, Element(page, "ProgressCard").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "TerminalPanel").Visibility);
        Assert.AreEqual(state.Title, ((HardwareInspectionProgressCard)Element(page, "ProgressCard")).CurrentState?.Title);
        HardwareInspectionActionCard activeActions =
            (HardwareInspectionActionCard)Element(page, "ActiveActionCard");
        Assert.AreEqual(Visibility.Visible, activeActions.Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "ProgressPanel").Visibility);
        CollectionAssert.AreEqual(
            new[] { "Cancel inspection" },
            ((StackPanel)activeActions.FindName("ActionsPanel")).Children
                .Cast<Button>()
                .Select(button => button.Content?.ToString())
                .ToArray());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_WarningKeepsTypedOutcomeWithoutRetiredCompletedReportSubtrees()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState state = _factory.CreateTerminal(
            HardwareInspectionOutcome.CompletedWithWarnings);
        HardwareSummaryPresentation summary = HardwareSummaryPresentationFactory.Create(
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        page.Apply(state, summary, HardwareInspectionDetailsSummaryTests.CreateDetails());

        Assert.IsNull(page.FindName("SummaryGrid"));
        Assert.IsNull(page.FindName("ReviewPanel"));
        Assert.IsNull(page.FindName("LimitationPanel"));
        HardwareInspectionOutcomeCard outcome =
            (HardwareInspectionOutcomeCard)Element(page, "OutcomeCard");
        Assert.AreEqual(state.Title,
            ((TextBlock)outcome.FindName("TitleTextBlock")).Text);
        Assert.AreEqual(state.Body,
            ((TextBlock)outcome.FindName("BodyTextBlock")).Text);
        Assert.AreEqual(Visibility.Visible, ((FrameworkElement)outcome.FindName("ReviewCountPanel")).Visibility);
        Assert.AreEqual("1", ((TextBlock)outcome.FindName("ReviewCountTextBlock")).Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_CompletedKeepsOutcomeDetailsAndActionsWithoutStandaloneSummary()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState state = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: false);
        HardwareSummaryPresentation summary = HardwareSummaryPresentationFactory.Create(
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());
        page.Apply(state, summary, HardwareInspectionDetailsSummaryTests.CreateDetails());

        Assert.AreEqual(Visibility.Collapsed, Element(page, "ProgressPanel").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "TerminalPanel").Visibility);
        Assert.IsNull(page.FindName("SummaryGrid"));
        Assert.IsNull(page.FindName("ComputerSummaryCard"));
        Assert.IsNull(page.FindName("RuntimeSummaryCard"));
        Assert.IsNull(page.FindName("SourcesSummaryCard"));
        Assert.AreEqual(Visibility.Visible, Element(page, "DetailsCard").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "ActionCard").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_InvalidShowsOnlyBoundedOutcomeAndAction()
    {
        HardwareInspectionPage page = new();
        page.Apply(_factory.CreateInvalidHandoff());

        Assert.AreEqual(Visibility.Visible, Element(page, "TerminalPanel").Visibility);
        Assert.IsNull(page.FindName("SummaryGrid"));
        Assert.AreEqual(Visibility.Collapsed, Element(page, "DetailsCard").Visibility);
        Button[] actions = ((StackPanel)((HardwareInspectionActionCard)Element(page, "ActionCard"))
            .FindName("ActionsPanel")).Children.Cast<Button>().ToArray();
        CollectionAssert.AreEqual(new[] { "Back to model inspection" }, actions.Select(b => b.Content?.ToString()).ToArray());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RecoveryStatesUseApprovedGuidanceAndStoppingStaysBounded()
    {
        HardwareInspectionPage page = new();
        page.Apply(
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.TransientOperation),
            details: HardwareInspectionDetailsSummaryTests.CreateDetails());

        Assert.AreEqual(Visibility.Collapsed, Element(page, "RecoveryPanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "LocalProcessingPanel").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "TerminalPanel").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "DetailsCard").Visibility);
        HardwareInspectionOutcomeCard outcome =
            (HardwareInspectionOutcomeCard)Element(page, "OutcomeCard");
        Assert.AreEqual("The inspection could not start",
            ((TextBlock)outcome.FindName("TitleTextBlock")).Text);
        CollectionAssert.AreEqual(
            new[] { "Back", "Try again" },
            ((StackPanel)((HardwareInspectionActionCard)Element(page, "ActionCard"))
                .FindName("ActionsPanel")).Children.Cast<Button>()
                .Select(button => button.Content?.ToString()).ToArray());

        page.Apply(
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.ApplicationRepairRequired),
            details: HardwareInspectionDetailsSummaryTests.CreateDetails());
        Assert.AreEqual(Visibility.Visible, Element(page, "LocalProcessingPanel").Visibility);
        Assert.AreEqual(
            "Repair or reinstall the application before trying hardware inspection again.",
            Text(page, "LocalProcessingTextBlock").Text);

        page.Apply(
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.CriticalEvidence),
            details: HardwareInspectionDetailsSummaryTests.CreateDetails());
        Assert.AreEqual(Visibility.Visible, Element(page, "LocalProcessingPanel").Visibility);
        Assert.AreEqual(
            "No hardware conclusion was made. Return to model inspection and try again.",
            Text(page, "LocalProcessingTextBlock").Text);

        page.Apply(_factory.CreateStopping());
        Assert.AreEqual(Visibility.Visible, Element(page, "ProgressPanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "TerminalPanel").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "ActiveActionCard").Visibility);
        CollectionAssert.AreEqual(
            new[] { "Stopping..." },
            ((StackPanel)((HardwareInspectionActionCard)Element(
                page, "ActiveActionCard")).FindName("ActionsPanel"))
                .Children.Cast<Button>()
                .Select(button => button.Content?.ToString()).ToArray());
        Assert.IsFalse(((StackPanel)((HardwareInspectionActionCard)Element(
                page, "ActiveActionCard")).FindName("ActionsPanel"))
            .Children.Cast<Button>().Single().IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RejectsIncompleteStableTerminalBundleBeforeChangingPage()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState completed = _factory.CreateTerminal(HardwareInspectionOutcome.Completed);
        Assert.Throws<ArgumentException>(() => page.Apply(completed));

        HardwareInspectionPresentationState failed = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.TransientOperation);
        Assert.Throws<ArgumentException>(() => page.Apply(failed));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_BubblesTypedActionWithoutOwningNavigation()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionActionKind? requested = null;
        page.ActionRequested += (_, args) => requested = args.Kind;
        page.Apply(_factory.CreateInvalidHandoff());

        Button button = ((StackPanel)((HardwareInspectionActionCard)Element(page, "ActionCard"))
            .FindName("ActionsPanel")).Children.Cast<Button>().Single();
        IInvokeProvider invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!;
        invoke.Invoke();
        Assert.AreEqual(HardwareInspectionActionKind.BackToModelInspection, requested);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LoadedViewModel_StartsOnceAndAppliesAcceptedProgress()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = new(
            service,
            new ImmediateHardwareInspectionStagePacer());
        HardwareInspectionPage page = new(viewModel);
        Window window = await ShowAsync(page);
        try
        {
            await WaitForAsync(() => service.Calls.Count == 1);
            ManualRun run = service.Calls[0];
            run.Report(1, HardwareInspectionRunStage.StartingHardwareInspection);
            run.Report(2, HardwareInspectionRunStage.ReadingProcessorInformation);
            await WaitForAsync(() =>
                ((HardwareInspectionProgressCard)Element(page, "ProgressCard"))
                    .CurrentState?.Title == "Reading processor information");

            Assert.AreEqual(1, service.Calls.Count);
            Assert.AreEqual(Visibility.Visible, Element(page, "ProgressCard").Visibility);
            run.Complete(HardwareInspectionRunResult.CreateCancelled(run.InspectionId));
            await WaitForAsync(() =>
                page.CurrentState?.Kind == HardwareInspectionPresentationKind.Cancelled);
            Assert.AreEqual(Visibility.Visible, Element(page, "ProgressPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Element(page, "TerminalPanel").Visibility);
            CollectionAssert.AreEqual(
                new[] { "Back", "Run inspection again" },
                ((StackPanel)((HardwareInspectionActionCard)Element(
                    page, "ActiveActionCard")).FindName("ActionsPanel"))
                    .Children.Cast<Button>()
                    .Select(button => button.Content?.ToString()).ToArray());
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task TypedRunActions_CancelThenRetryWithANewIdentity()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = new(
            service,
            new ImmediateHardwareInspectionStagePacer());
        HardwareInspectionPage page = new(viewModel);
        Window window = await ShowAsync(page);
        try
        {
            await WaitForAsync(() => service.Calls.Count == 1);
            ManualRun first = service.Calls[0];
            Button cancel = ((StackPanel)((HardwareInspectionActionCard)Element(
                page, "ActiveActionCard")).FindName("ActionsPanel"))
                .Children.Cast<Button>().Single();
            ((IInvokeProvider)new ButtonAutomationPeer(cancel)
                .GetPattern(PatternInterface.Invoke)!).Invoke();
            await WaitForAsync(() => viewModel.Snapshot.Presentation.Kind
                == HardwareInspectionPresentationKind.Stopping);
            await WaitForAsync(() => first.CancellationToken.IsCancellationRequested);

            first.Complete(HardwareInspectionRunResult.CreateCompleted(
                first.InspectionId,
                HardwareInspectionOutcome.Completed,
                HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                    first.InspectionId)));
            await WaitForAsync(() => viewModel.Snapshot.Presentation.Kind
                == HardwareInspectionPresentationKind.Cancelled);
            await WaitForAsync(() =>
                page.CurrentState?.Kind == HardwareInspectionPresentationKind.Cancelled);
            Button retry = ((StackPanel)((HardwareInspectionActionCard)Element(
                page, "ActiveActionCard")).FindName("ActionsPanel"))
                .Children.Cast<Button>()
                .Single(button => button.Content?.ToString() == "Run inspection again");
            ((IInvokeProvider)new ButtonAutomationPeer(retry)
                .GetPattern(PatternInterface.Invoke)!).Invoke();

            await WaitForAsync(() => service.Calls.Count == 2);
            Assert.AreNotEqual(first.InspectionId, service.Calls[1].InspectionId);
            service.Calls[1].Complete(HardwareInspectionRunResult.CreateCancelled(
                service.Calls[1].InspectionId));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task VisibleActiveAttempt_CancelWinsOverPendingSuccessfulHandoff()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = new(
            service,
            new ImmediateHardwareInspectionStagePacer());
        HardwareInspectionPage page = new(viewModel);
        int completions = 0;
        page.InspectionCompleted += (_sender, _args) => completions++;
        Window window = await ShowAsync(page);
        try
        {
            await WaitForAsync(() => service.Calls.Count == 1);
            ManualRun run = service.Calls[0];
            long sequence = 0;
            foreach (HardwareInspectionRunStage stage in
                Enum.GetValues<HardwareInspectionRunStage>())
            {
                run.Report(++sequence, stage);
            }
            run.Complete(HardwareInspectionRunResult.CreateCompleted(
                run.InspectionId,
                HardwareInspectionOutcome.Completed,
                HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                    run.InspectionId)));

            await WaitForAsync(() =>
                viewModel.Snapshot.Presentation.Kind ==
                    HardwareInspectionPresentationKind.Completed
                && page.CurrentState?.Kind ==
                    HardwareInspectionPresentationKind.Active);
            Assert.IsNotNull(viewModel.Snapshot.Handoff,
                "The service success must be pending behind the visible progress handoff.");

            HardwareInspectionProgressCard progressCard =
                (HardwareInspectionProgressCard)Element(page, "ProgressCard");
            HardwareInspectionActionCard activeActionCard =
                (HardwareInspectionActionCard)Element(page, "ActiveActionCard");
            Button cancel = ((StackPanel)activeActionCard.FindName("ActionsPanel"))
                .Children.Cast<Button>().Single();
            Assert.IsTrue(cancel.Focus(FocusState.Programmatic));
            ((IInvokeProvider)new ButtonAutomationPeer(cancel)
                .GetPattern(PatternInterface.Invoke)!).Invoke();

            long acceptedRevision = viewModel.Snapshot.Revision;
            ((IInvokeProvider)new ButtonAutomationPeer(cancel)
                .GetPattern(PatternInterface.Invoke)!).Invoke();
            Assert.AreEqual(
                acceptedRevision,
                viewModel.Snapshot.Revision,
                "A duplicate action for the accepted attempt must be ignored without mutating the Button.");
            Assert.AreEqual(
                HardwareInspectionPresentationKind.Active,
                page.CurrentState?.Kind,
                "The Button invocation must return before Stopping mutates the visual tree.");
            Assert.AreEqual(
                FocusState.Unfocused,
                progressCard.FocusState,
                "Accepted cancellation must not synchronously transfer focus to the progress card.");

            await WaitForAsync(() =>
                page.CurrentState?.Kind ==
                HardwareInspectionPresentationKind.Stopping);
            Assert.AreEqual(
                HardwareInspectionPresentationKind.Stopping,
                progressCard.CurrentState?.Kind,
                "The first Stopping frame must own the frozen progress presentation.");
            Button stopping = ((StackPanel)activeActionCard.FindName("ActionsPanel"))
                .Children.Cast<Button>().Single();
            Assert.AreSame(cancel, stopping,
                "Stopping must reuse the invoked Cancel button instance.");
            Assert.AreEqual(FocusState.Unfocused, stopping.FocusState,
                "Focus must leave the action before that action is disabled.");
            Assert.AreNotEqual(FocusState.Unfocused, progressCard.FocusState,
                "Stopping must transfer focus to the stable progress surface.");
            Assert.AreEqual("Stopping...", stopping.Content?.ToString());
            Assert.IsFalse(stopping.IsEnabled);
            Assert.AreEqual("Stopping hardware inspection",
                AutomationProperties.GetName(stopping));
            ProgressBar progressBar = (ProgressBar)progressCard.FindName(
                "OverallProgressBar");
            TextBlock progressPercentage = (TextBlock)progressCard.FindName(
                "OverallPercentage");
            double frozenValue = progressBar.Value;
            string frozenPercentage = progressPercentage.Text;
            typeof(HardwareInspectionProgressCard).GetMethod(
                "RefreshEstimatedValues",
                System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!.Invoke(
                        progressCard,
                        null);
            await Task.Delay(40);
            Assert.AreEqual(frozenValue, progressBar.Value);
            Assert.AreEqual(frozenPercentage, progressPercentage.Text);
            await WaitForAsync(() =>
                viewModel.Snapshot.Presentation.Kind ==
                HardwareInspectionPresentationKind.Cancelled);
            await Task.Delay(100);
            Assert.AreEqual(
                HardwareInspectionPresentationKind.Stopping,
                page.CurrentState?.Kind,
                "An authoritative Cancelled snapshot must remain behind the minimum presentation delay.");
            await WaitForAsync(() =>
                page.CurrentState?.Kind ==
                HardwareInspectionPresentationKind.Cancelled);
            FrameworkElement progressPanel = Element(page, "ProgressPanel");
            FrameworkElement terminalPanel = Element(page, "TerminalPanel");
            Assert.AreEqual(Visibility.Visible, progressPanel.Visibility);
            Assert.AreEqual(Visibility.Collapsed, terminalPanel.Visibility);
            Assert.AreEqual(
                HardwareInspectionPresentationKind.Cancelled,
                progressCard.CurrentState?.Kind,
                "Cancelled with null details must use the dedicated progress recovery surface.");
            Button[] recoveryActions =
                ((StackPanel)activeActionCard.FindName("ActionsPanel"))
                    .Children.Cast<Button>().ToArray();
            await WaitForAsync(() => recoveryActions.Any(button =>
                button.FocusState != FocusState.Unfocused));
            Assert.AreNotEqual(FocusState.Unfocused,
                recoveryActions.First(button => button.IsEnabled).FocusState,
                "Cancelled recovery must focus the first enabled action after realization.");
            Assert.IsTrue(recoveryActions.All(button => button.IsTabStop),
                "Cancelled recovery actions must remain keyboard reachable through normal tab order.");
            Assert.AreEqual(
                "Hardware inspection cancelled. No hardware report was created.",
                AutomationProperties.GetName(progressCard));
            Assert.AreEqual(0, completions);
            Assert.IsNull(viewModel.Snapshot.Handoff);
            Assert.IsNull(viewModel.Snapshot.Summary);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DelayedCancelledPresentation_CannotCrossIntoANewAttempt()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = new(
            service,
            new ImmediateHardwareInspectionStagePacer());
        HardwareInspectionPage page = new(viewModel);
        Window window = await ShowAsync(page);
        try
        {
            await WaitForAsync(() => service.Calls.Count == 1);
            ManualRun first = service.Calls[0];
            long firstGeneration = viewModel.Snapshot.AttemptGeneration;
            Button cancel = ((StackPanel)((HardwareInspectionActionCard)Element(
                page, "ActiveActionCard")).FindName("ActionsPanel"))
                .Children.Cast<Button>().Single();
            ((IInvokeProvider)new ButtonAutomationPeer(cancel)
                .GetPattern(PatternInterface.Invoke)!).Invoke();
            await WaitForAsync(() =>
                page.CurrentState?.Kind ==
                HardwareInspectionPresentationKind.Stopping);

            first.Complete(HardwareInspectionRunResult.CreateCancelled(
                first.InspectionId));
            await WaitForAsync(() =>
                viewModel.Snapshot.Presentation.Kind ==
                HardwareInspectionPresentationKind.Cancelled);

            Task retry = viewModel.RetryAsync();
            await WaitForAsync(() => service.Calls.Count == 2);
            long secondGeneration = viewModel.Snapshot.AttemptGeneration;
            Assert.AreNotEqual(firstGeneration, secondGeneration);
            await WaitForAsync(() =>
                page.CurrentState?.Kind == HardwareInspectionPresentationKind.Active);
            await Task.Delay(320);

            Assert.AreEqual(
                secondGeneration,
                viewModel.Snapshot.AttemptGeneration);
            Assert.AreEqual(
                HardwareInspectionPresentationKind.Active,
                page.CurrentState?.Kind,
                "The delayed Cancelled callback from the retired attempt must be rejected.");
            Assert.AreEqual(
                Visibility.Visible,
                Element(page, "ProgressPanel").Visibility,
                "A retired delayed cancellation must not replace the new attempt with a terminal surface.");

            ManualRun second = service.Calls[1];
            second.Complete(HardwareInspectionRunResult.CreateCancelled(
                second.InspectionId));
            await retry;
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task AcceptedCancelDeferredPresentation_IsDiscardedAfterPageRetires()
    {
        ManualHardwareInspectionService service = new();
        HardwareInspectionViewModel viewModel = new(
            service,
            new ImmediateHardwareInspectionStagePacer());
        HardwareInspectionPage page = new(viewModel);
        Window window = await ShowAsync(page);
        bool detached = false;
        try
        {
            await WaitForAsync(() => service.Calls.Count == 1);
            ManualRun run = service.Calls[0];
            Button cancel = ((StackPanel)((HardwareInspectionActionCard)Element(
                page, "ActiveActionCard")).FindName("ActionsPanel"))
                .Children.Cast<Button>().Single();

            ((IInvokeProvider)new ButtonAutomationPeer(cancel)
                .GetPattern(PatternInterface.Invoke)!).Invoke();
            Assert.AreEqual(
                HardwareInspectionPresentationKind.Active,
                page.CurrentState?.Kind);

            window.Content = null;
            detached = true;
            await WaitForAsync(() => page.XamlRoot is null);
            run.Complete(HardwareInspectionRunResult.CreateCancelled(run.InspectionId));
            await Task.Delay(180);

            Assert.AreEqual(
                HardwareInspectionPresentationKind.Active,
                page.CurrentState?.Kind,
                "A deferred Stopping callback must not mutate a retired page.");
            Assert.AreEqual(
                FocusState.Unfocused,
                ((HardwareInspectionProgressCard)Element(page, "ProgressCard")).FocusState,
                "A stale deferred callback must not move focus after ownership is lost.");
        }
        finally
        {
            if (!detached)
            {
                window.Content = null;
            }
            window.Close();
        }
    }

    private static async Task<Window> ShowAsync(HardwareInspectionPage page)
    {
        Window window = new() { Content = page };
        window.Activate();
        await WaitForAsync(() => page.XamlRoot is not null);
        return window;
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static FrameworkElement Element(HardwareInspectionPage page, string name) =>
        (FrameworkElement)page.FindName(name);

    private static TextBlock Text(HardwareInspectionPage page, string name) =>
        (TextBlock)page.FindName(name);
}
