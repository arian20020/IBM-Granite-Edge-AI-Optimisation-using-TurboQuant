#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class ModelInspectionFixturePageLifecycleTests
{
    [UITestMethod]
    public void FixtureActivation_UsesSharedProductionPath()
    {
        using ModelInspectionFixtureSession fixtureSession = Session("MI-001");
        using ModelInspectionFixtureSession productionSession = Session("MI-001");
        ModelInspectionPage fixturePage = ModelInspectionPage.CreateForFixture(
            fixtureSession,
            startInspectionOnLoaded: false);
        var productionPage = new ModelInspectionPage(productionSession.Service);
        InvokeNavigation(productionPage, "OnNavigatedTo", productionSession.Request);

        Assert.AreSame(fixtureSession.Request, fixturePage.Request);
        Assert.AreSame(productionSession.Request, productionPage.Request);
        Assert.IsNotNull(fixturePage.ViewModel);
        Assert.IsNotNull(productionPage.ViewModel);
        Assert.AreEqual(
            productionPage.ViewModel.Snapshot,
            fixturePage.ViewModel.Snapshot);
        Assert.AreEqual(
            productionPage.CurrentPresentation?.State,
            fixturePage.CurrentPresentation?.State);
        Assert.AreEqual(
            productionPage.CurrentPresentation?.RenderKey,
            fixturePage.CurrentPresentation?.RenderKey);
        Assert.AreEqual(
            productionPage.CurrentFooterStatus,
            fixturePage.CurrentFooterStatus);
        AssertCoordinatorOwnsCurrentPresentation(productionPage);
        AssertCoordinatorOwnsCurrentPresentation(fixturePage);
        Assert.AreEqual(0, fixtureSession.Evidence.ServiceCallCount);

        Assert.IsTrue(fixturePage.RetireForFixture());
        Assert.IsFalse(fixturePage.RetireForFixture());
        InvokeNavigation(productionPage, "OnNavigatedFrom", parameter: null);
        Assert.AreEqual(1, fixtureSession.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, fixtureSession.Evidence.MotionSettingsDisposalCount);
    }

    [UITestMethod]
    public async Task LoadedStartOption_ControlsOnlyAutomaticInspectionStart()
    {
        using ModelInspectionFixtureSession stoppedSession = Session("MI-001");
        ModelInspectionPage stoppedPage = ModelInspectionPage.CreateForFixture(
            stoppedSession,
            startInspectionOnLoaded: false);
        await LoadPageAsync(stoppedPage);
        Assert.AreEqual(0, stoppedSession.Evidence.ServiceCallCount);
        Assert.IsNull(stoppedPage.CurrentInspectionTask);
        Assert.IsTrue(stoppedPage.RetireForFixture());

        using ModelInspectionFixtureSession startedSession = Session("MI-002");
        ModelInspectionPage startedPage = ModelInspectionPage.CreateForFixture(
            startedSession,
            startInspectionOnLoaded: true);
        await LoadPageAsync(startedPage);
        Assert.AreEqual(1, startedSession.Evidence.ServiceCallCount);
        Assert.IsNotNull(startedPage.CurrentInspectionTask);
        Assert.IsTrue(startedPage.RetireForFixture());
    }

    [UITestMethod]
    public void FixtureResources_AreConfiguredExactlyOnceBeforeInitializeComponent()
    {
        using ModelInspectionFixtureSession configuredSession = Session("MI-001");
        int configurationCount = 0;
        ModelInspectionPage configured = ModelInspectionPage.CreateForFixture(
            configuredSession,
            startInspectionOnLoaded: false,
            resources =>
            {
                configurationCount++;
                resources["InspectionContentColumnWidth"] = 777d;
            });

        Assert.AreEqual(1, configurationCount);
        var configuredHost = configured.FindName("InspectionContentHost") as Grid;
        Assert.IsNotNull(configuredHost);
        Assert.AreEqual(777d, configuredHost.MaxWidth);
        Assert.IsTrue(configured.RetireForFixture());

        using ModelInspectionFixtureSession defaultSession = Session("MI-001");
        ModelInspectionPage defaults = ModelInspectionPage.CreateForFixture(
            defaultSession,
            startInspectionOnLoaded: false,
            configureResourcesBeforeInitialize: null);
        var defaultHost = defaults.FindName("InspectionContentHost") as Grid;
        Assert.IsNotNull(defaultHost);
        Assert.AreEqual(840d, defaultHost.MaxWidth);
        Assert.IsTrue(defaults.RetireForFixture());
    }

    [UITestMethod]
    public async Task StaleResultSnapshot_CannotCaptureNonterminalAttempt()
    {
        using ModelInspectionFixtureSession session = Session(
            "MI-034",
            animationsEnabled: false);
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);
        Task first = page.StartInspectionIfReadyAsync()!;

        try
        {
            session.Service.ReleaseServiceCheckpoint(
                attempt: 1,
                "capture-old-result");
            ModelInspectionViewSnapshot snapshot = page.ViewModel!.Snapshot;
            Assert.AreEqual(1L, snapshot.RenderKey.AttemptGeneration);
            Assert.IsNull(snapshot.TerminalResult);

            InvalidOperationException error = Assert.ThrowsExactly<
                InvalidOperationException>(() =>
                    page.CaptureStaleResultSnapshotForFixture(
                        ownerAttempt: 1,
                        "capture-old-result"));
            StringAssert.Contains(error.Message, "terminal");
        }
        finally
        {
            session.Service.ReleaseServiceCheckpoint(attempt: 1, "failure");
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            await DrainDispatcherAsync(page);
            _ = page.RetireForFixture();
        }
    }

    [UITestMethod]
    public async Task StaleResultSnapshot_UsesCapturedCoordinatorSnapshotAndLifetime()
    {
        var driver = new CapturingAnimationDriver();
        using ModelInspectionFixtureSession session = Session(
            "MI-034",
            animationsEnabled: true,
            () => driver);
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        bool retired = false;
        ModelInspectionRenderKey capturedKey = default;
        ModelInspectionViewSnapshot? capturedSnapshot = null;
        ModelInspectionPagePresentation? capturedPresentation = null;

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ReachSecondTerminalAsync(
                page,
                session,
                "capture-old-result",
                () =>
                {
                    capturedSnapshot = page.ViewModel!.Snapshot;
                    ModelInspectionExecutionResult terminal =
                        capturedSnapshot.TerminalResult!;
                    Assert.IsNotNull(terminal);
                    Assert.AreEqual(
                        ModelInspectionExecutionStatus.OperationalFailure,
                        terminal.Status);
                    Assert.IsNotNull(terminal.Failure);
                    Assert.AreEqual(
                        "MI-OP-WORKER-START",
                        terminal.Failure.Code);
                    Assert.AreEqual(
                        1L,
                        capturedSnapshot.RenderKey.AttemptGeneration);

                    capturedPresentation = page.CurrentPresentation;
                    Assert.IsNotNull(capturedPresentation);
                    Assert.AreEqual(
                        ModelInspectionFigmaState.OperationalFailure,
                        capturedPresentation.State);
                    Assert.AreEqual(
                        capturedSnapshot.RenderKey,
                        capturedPresentation.RenderKey);
                    capturedKey =
                        page.CaptureStaleResultSnapshotForFixture(
                            ownerAttempt: 1,
                            "capture-old-result");
                    Assert.AreEqual(
                        capturedSnapshot.RenderKey,
                        capturedKey);
                });

            Assert.IsNotNull(capturedSnapshot);
            Assert.IsNotNull(capturedPresentation);
            Assert.AreEqual(1L, capturedKey.AttemptGeneration);
            ModelInspectionPagePresentation retained =
                page.CurrentPresentation!;
            Assert.AreEqual(
                ModelInspectionFigmaState.ReadyCollapsed,
                retained.State);
            Assert.AreEqual(2L, retained.RenderKey.AttemptGeneration);
            InspectionFooterStatus retainedFooter = page.CurrentFooterStatus;
            var content = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            var outcome = (InspectionOutcomeCard)page.FindName(
                "InspectionOutcomeCardControl");
            int retainedProgressAnnouncements =
                content.LiveRegionChangeNotificationCount;
            int retainedOutcomeAnnouncements =
                outcome.LiveRegionChangeNotificationCount;
            int retainedMotionStarts = driver.TotalStartCount;
            InspectionProgressRows retainedRows =
                retained.ContentCard.ProgressRows;
            var retainedRowItems = retainedRows.Items.ToArray();
            InspectionProgressRows retainedControlRows =
                content.Presentation.ProgressRows;
            var pageScroll = (ScrollViewer)page.FindName(
                "InspectionPageScrollViewer");
            pageScroll.IsTabStop = true;
            Assert.IsTrue(pageScroll.Focus(FocusState.Programmatic));
            object? retainedFocus = FocusManager.GetFocusedElement(
                page.XamlRoot);
            Assert.IsNotNull(retainedFocus);

            Assert.IsTrue(page.SubmitStaleResultSnapshotForFixture(
                ownerAttempt: 1,
                "old-result"));
            await DrainDispatcherAsync(page);

            Assert.AreSame(retained, page.CurrentPresentation);
            Assert.AreEqual(retainedFooter, page.CurrentFooterStatus);
            Assert.AreSame(
                retainedFocus,
                FocusManager.GetFocusedElement(page.XamlRoot));
            Assert.AreEqual(
                retainedProgressAnnouncements,
                content.LiveRegionChangeNotificationCount);
            Assert.AreEqual(
                retainedOutcomeAnnouncements,
                outcome.LiveRegionChangeNotificationCount);
            Assert.AreEqual(retainedMotionStarts, driver.TotalStartCount);
            Assert.AreSame(
                retainedRows,
                page.CurrentPresentation!.ContentCard.ProgressRows);
            CollectionAssert.AreEqual(
                retainedRowItems,
                page.CurrentPresentation.ContentCard.ProgressRows.Items
                    .ToArray());
            Assert.AreSame(
                retainedControlRows,
                content.Presentation.ProgressRows);

            retired = page.RetireForFixture();
            Assert.IsTrue(retired);
            Assert.IsFalse(page.SubmitStaleResultSnapshotForFixture(
                ownerAttempt: 1,
                "old-result"));
        }
        finally
        {
            if (!retired)
            {
                _ = page.RetireForFixture();
            }

            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    public async Task StaleMotion_UsesCapturedCompletionDelegateAndOperationKey()
    {
        var driver = new CapturingAnimationDriver();
        using ModelInspectionFixtureSession session = Session(
            "MI-035",
            animationsEnabled: true,
            () => driver);
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);

        await ReachSecondTerminalAsync(
            page,
            session,
            "capture-old-motion");
        Assert.AreEqual(2, driver.TerminalKeys.Count);
        Assert.AreEqual(
            1L,
            driver.TerminalKeys[0].RenderKey.AttemptGeneration);
        Assert.AreEqual(
            2L,
            driver.TerminalKeys[1].RenderKey.AttemptGeneration);
        var outgoing = (InspectionContentCard)page.FindName(
            "OutgoingProgressContentCard");
        Visibility retainedVisibility = outgoing.Visibility;
        object retainedPresentation = outgoing.Presentation;
        Assert.AreEqual(Visibility.Visible, retainedVisibility);

        Assert.IsTrue(page.ReleaseStaleMotionForFixture(
            ownerAttempt: 1,
            "old-motion"));

        Assert.AreEqual(retainedVisibility, outgoing.Visibility);
        Assert.AreSame(retainedPresentation, outgoing.Presentation);
        Assert.IsTrue(page.RetireForFixture());
        Assert.IsFalse(page.ReleaseStaleMotionForFixture(
            ownerAttempt: 1,
            "old-motion"));
    }

    [UITestMethod]
    public async Task StaleAnnouncement_UsesCapturedTextKeyAndCoordinator()
    {
        using ModelInspectionFixtureSession session = Session(
            "MI-036",
            animationsEnabled: false);
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);

        await ReachSecondTerminalAsync(
            page,
            session,
            "capture-old-announcement");
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");
        int retainedAnnouncements =
            outcome.LiveRegionChangeNotificationCount;

        Assert.IsTrue(page.ReleaseStaleAnnouncementForFixture(
            ownerAttempt: 1,
            "old-announcement"));

        Assert.AreEqual(
            retainedAnnouncements,
            outcome.LiveRegionChangeNotificationCount);
        Assert.IsTrue(page.RetireForFixture());
        Assert.IsFalse(page.ReleaseStaleAnnouncementForFixture(
            ownerAttempt: 1,
            "old-announcement"));
    }

    private static ModelInspectionFixtureSession Session(
        string id,
        bool animationsEnabled = true,
        Func<IModelInspectionAnimationDriver>? animationDriverFactory = null) =>
        new(
            ModelInspectionFixtureTestCatalogue.Get(id).Input,
            animationsEnabled,
            animationDriverFactory);

    private static async Task ReachSecondTerminalAsync(
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        string captureCheckpoint,
        Action? afterFirstTerminal = null)
    {
        Task first = page.StartInspectionIfReadyAsync()!;
        session.Service.ReleaseServiceCheckpoint(
            attempt: 1,
            captureCheckpoint);
        session.Service.ReleaseServiceCheckpoint(attempt: 1, "failure");
        await first.WaitAsync(TimeSpan.FromSeconds(10));
        await DrainDispatcherAsync(page);
        afterFirstTerminal?.Invoke();

        ModelInspectionViewModel viewModel = page.ViewModel!;
        var terminal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler changed = (_, eventArguments) =>
        {
            if (eventArguments.PropertyName == nameof(
                    ModelInspectionViewModel.Snapshot) &&
                viewModel.Snapshot.RenderKey.AttemptGeneration == 2 &&
                viewModel.Snapshot.TerminalResult is not null)
            {
                terminal.TrySetResult(true);
            }
        };
        viewModel.PropertyChanged += changed;
        try
        {
            viewModel.RetryCommand.Execute(null);
            session.Service.ReleaseServiceCheckpoint(attempt: 2, "ready");
            await terminal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            viewModel.PropertyChanged -= changed;
        }

        await DrainDispatcherAsync(page);
        Assert.AreEqual(2, session.Evidence.ServiceCallCount);
    }

    private static async Task DrainDispatcherAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
            () => drained.TrySetResult(true)));
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task LoadPageAsync(ModelInspectionPage page)
    {
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        page.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = page };
        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static void AssertCoordinatorOwnsCurrentPresentation(
        ModelInspectionPage page)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            "_coordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var coordinator = field.GetValue(page) as ModelInspectionRenderCoordinator;
        Assert.IsNotNull(coordinator);
        Assert.AreEqual(
            page.CurrentPresentation?.RenderKey,
            coordinator.LatestAcceptedKey);
        Assert.AreSame(page.CurrentPresentation, coordinator.CurrentPresentation);
    }

    private static void InvokeNavigation(
        ModelInspectionPage page,
        string methodName,
        object? parameter)
    {
        NavigationEventArgs navigation = CreateNavigationEventArgs(parameter);
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        try
        {
            method.Invoke(page, [navigation]);
        }
        catch (TargetInvocationException error)
            when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private static NavigationEventArgs CreateNavigationEventArgs(object? parameter)
    {
        NavigationEventArgs? captured = null;
        var frame = new Frame();
        frame.Navigated += (_, eventArguments) => captured = eventArguments;
        Assert.IsTrue(frame.Navigate(typeof(Page), parameter));
        Assert.IsNotNull(captured);
        return captured;
    }

    private sealed class CapturingAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly List<ModelInspectionVisualOperationKey>
            terminalKeys = [];

        internal IReadOnlyList<ModelInspectionVisualOperationKey>
            TerminalKeys => terminalKeys;

        internal int TotalStartCount { get; private set; }

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            TotalStartCount++;
        }

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            TotalStartCount++;
        }

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            TotalStartCount++;
        }

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed)
        {
            TotalStartCount++;
            terminalKeys.Add(key);
        }

        public void CancelAll()
        {
        }

        public void Dispose()
        {
        }
    }
}
#endif
