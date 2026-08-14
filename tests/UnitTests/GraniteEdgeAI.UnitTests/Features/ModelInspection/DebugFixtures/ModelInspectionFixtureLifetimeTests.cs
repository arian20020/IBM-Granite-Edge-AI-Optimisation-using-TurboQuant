#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class ModelInspectionFixtureLifetimeTests
{
    [UITestMethod]
    public async Task EveryTypedOldEvent_BeforeAndAfterRetryLeavesWinningScreenUntouched()
    {
        foreach ((string Id, string Checkpoint, string Release) scenario in new[]
                 {
                     ("MI-033", "capture-old-progress", "old-progress"),
                     ("MI-034", "capture-old-result", "old-result"),
                     ("MI-035", "capture-old-motion", "old-motion"),
                     ("MI-036", "capture-old-announcement", "old-announcement")
                 })
        {
            ModelInspectionFixtureGalleryPage gallery =
                ModelInspectionFixtureGalleryTestHarness.CreateGallery();
            Microsoft.UI.Xaml.Window window =
                await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
            try
            {
                await gallery.SelectFixtureForStaleEventCheckpointThroughLoadedRunnerAsync(
                    scenario.Id, scenario.Checkpoint);
                ModelInspectionFixtureHostPage host = gallery.ActiveHost!;
                ModelInspectionPage page = host.ModelInspectionPage!;
                ModelInspectionFixtureSession session = host.Session;
                ModelInspectionFixtureSessionEvidence evidence =
                    session.Evidence;
                if (scenario.Id == "MI-034")
                {
                    _ = page.CaptureStaleResultSnapshotForFixture(1,
                        scenario.Checkpoint);
                }
                await gallery.InvokeRetryThroughRenderedControlAsync();
                ModelInspectionFixtureStaleInvariant before = await CaptureAsync(page,
                    session, gallery.ViewModel.SelectedItem!.Id);

                Assert.IsTrue(page.ReleaseTypedStaleEventForFixture(
                    scenario.Id, 1, scenario.Release), scenario.Id);
                await ModelInspectionFixtureLifetimeHarness.DrainDispatcherAsync(page);
                ModelInspectionFixtureStaleInvariant after = await CaptureAsync(page,
                    session, gallery.ViewModel.SelectedItem!.Id);
                before.AssertUnchanged(after, scenario.Id);
                Assert.AreSame(host, gallery.ActiveHost, scenario.Id);
                Assert.AreEqual(scenario.Id, gallery.ViewModel.SelectedItem!.Id,
                    scenario.Id);

                Assert.IsTrue(page.RetireForFixture(), scenario.Id);
                Assert.IsFalse(page.ReleaseTypedStaleEventForFixture(
                    scenario.Id, 1, scenario.Release), scenario.Id);
                await WaitForPendingZeroAsync(evidence);
                AssertAllPendingZero(evidence, session.Service);
            }
            finally
            {
                gallery.CloseForTesting();
                window.Content = null;
                window.Close();
            }
        }
    }

    [UITestMethod]
    public async Task StateRoutes_CancelRetryRestartKeepTheSameLifetime()
    {
        foreach ((string Fixture, string Action) route in new[]
                 { ("MI-014", "cancel"), ("MI-032", "retry-attempt"), ("MI-031", "restart-attempt") })
        {
            ModelInspectionFixtureGalleryPage gallery =
                ModelInspectionFixtureGalleryTestHarness.CreateGallery();
            Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
            try
            {
                await gallery.PositionAtDeclaredInteractionCheckpointThroughScenarioAsync(
                    route.Fixture,
                    ModelInspectionFixtureTestCatalogue.Get(route.Fixture).Interactions
                        .Single(item => item.Id == route.Action).SourceCheckpoint);
                ModelInspectionFixtureHostPage host = gallery.ActiveHost!;
                ModelInspectionPage page = host.ModelInspectionPage!;
                ModelInspectionFixtureSession session = host.Session;
                ModelInspectionFixtureSessionEvidence evidence =
                    session.Evidence;
                int serviceCallsBefore = evidence.ServiceCallCount;
                int cancellationsBefore = evidence.CancellationObservationCount;
                int releasedCheckpointsBefore =
                    evidence.ReleasedServiceCheckpoints.Count;
                int terminalCompletionsBefore = evidence.TerminalCompletionCount;
                long attemptGenerationBefore =
                    page.ViewModel!.Snapshot.RenderKey.AttemptGeneration;
                ModelInspectionFixtureActionDispatchResult result = await gallery
                    .DispatchDeclaredActionForTestingAsync(route.Action);
                await ModelInspectionFixtureGalleryTestHarness.DrainAsync(page);
                await ModelInspectionFixtureGalleryTestHarness
                    .WaitForUiPendingZeroAsync(page, session);
                page.UpdateLayout();
                Assert.IsTrue(result.IsDispatched, route.Action);
                Assert.AreSame(host, gallery.ActiveHost, route.Action);
                Assert.AreSame(page, host.ModelInspectionPage, route.Action);
                Assert.AreSame(session, gallery.ActiveHost!.Session, route.Action);
                Assert.AreEqual(
                    ModelInspectionFigmaState.InspectionProgress,
                    page.CurrentPresentation!.State,
                    route.Action);
                Assert.IsTrue(page.ViewModel.Snapshot.IsRunActive, route.Action);
                Assert.AreEqual(1, session.Service.PendingCallCount, route.Action);
                Assert.AreEqual(
                    1,
                    evidence.ActiveCancellationRegistrationCount,
                    route.Action);
                Assert.AreEqual(
                    releasedCheckpointsBefore,
                    evidence.ReleasedServiceCheckpoints.Count,
                    route.Action);
                Assert.AreEqual(
                    terminalCompletionsBefore,
                    evidence.TerminalCompletionCount,
                    route.Action);
                Assert.AreEqual(0, session.Evidence.PageRetirementCount, route.Action);
                Assert.AreEqual(0, session.Evidence.SessionRetirementCount, route.Action);
                Assert.AreEqual(0, session.Evidence.SessionDisposalCount, route.Action);
                ModelInspectionFixtureGalleryTestHarness.AssertUiPendingZero(
                    session,
                    route.Action);
                if (string.Equals(route.Action, "cancel", StringComparison.Ordinal))
                {
                    Assert.AreEqual(
                        serviceCallsBefore,
                        evidence.ServiceCallCount,
                        route.Action);
                    Assert.AreEqual(
                        cancellationsBefore + 1,
                        evidence.CancellationObservationCount,
                        route.Action);
                    Assert.AreEqual(
                        attemptGenerationBefore,
                        page.ViewModel.Snapshot.RenderKey.AttemptGeneration,
                        route.Action);
                    Assert.IsTrue(
                        page.ViewModel.Snapshot.IsCancellationRequested,
                        route.Action);
                }
                else
                {
                    Assert.AreEqual(
                        serviceCallsBefore + 1,
                        evidence.ServiceCallCount,
                        route.Action);
                    Assert.AreEqual(
                        cancellationsBefore,
                        evidence.CancellationObservationCount,
                        route.Action);
                    Assert.AreEqual(
                        attemptGenerationBefore + 1,
                        page.ViewModel.Snapshot.RenderKey.AttemptGeneration,
                        route.Action);
                    Assert.IsFalse(
                        page.ViewModel.Snapshot.IsCancellationRequested,
                        route.Action);
                }
            }
            finally
            {
                gallery.CloseForTesting();
                window.Content = null;
                window.Close();
            }
        }
    }

    [UITestMethod]
    public async Task ReplacementRoutes_ResetAndSwitchRetireOnlyTheOldLifetime()
    {
        foreach ((string Route, string Target) route in new[]
                 { ("reset", "MI-002"), ("fixture-switch", "MI-003") })
        {
            ModelInspectionFixtureGalleryPage gallery = ModelInspectionFixtureGalleryTestHarness.CreateGallery();
            Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
            try
            {
                await gallery.SelectFixtureThroughRealListForTestingAsync("MI-002");
                ModelInspectionFixtureHostPage old = gallery.ActiveHost!;
                ModelInspectionFixtureSession oldSession = old.Session;
                ModelInspectionFixtureSessionEvidence oldEvidence =
                    oldSession.Evidence;
                await ModelInspectionFixtureLifetimeTestHarness.DriveReplacementRouteAsync(
                    gallery, route.Route);
                await WaitForPendingZeroAsync(oldEvidence);
                AssertRetiredExactlyOnce(oldEvidence, oldSession, route.Route);
                Assert.AreNotSame(old, gallery.ActiveHost, route.Route);
                Assert.AreEqual(route.Target, gallery.ViewModel.SelectedItem?.Id,
                    route.Route);
                ModelInspectionFixtureSession replacementSession =
                    gallery.ActiveHost!.Session;
                await ModelInspectionFixtureGalleryTestHarness
                    .WaitForInteractionSettlementAsync(
                        gallery.ActiveHost.ModelInspectionPage!,
                        replacementSession,
                        ModelInspectionFixtureTestCatalogue.Get(route.Target)
                            .Expected.Figma.State);
                ModelInspectionObservedScreen observed =
                    await ObserveCumulativeAsync(gallery.ActiveHost!
                        .ModelInspectionPage!);
                ModelInspectionFixtureGalleryTestHarness.AssertExactScreen(
                    gallery,
                    ModelInspectionFixtureTestCatalogue.Get(route.Target),
                    observed,
                    route.Route);
                await WaitForPendingZeroAsync(replacementSession.Evidence);
                AssertAllPendingZero(
                    replacementSession.Evidence,
                    replacementSession.Service);
            }
            finally { gallery.CloseForTesting(); window.Content = null; window.Close(); }
        }
    }

    [UITestMethod]
    public async Task InvalidListSelection_RetiresOldLifetimeAndCreatesNoReplacement()
    {
        ModelInspectionFixtureGalleryPage gallery =
            ModelInspectionFixtureGalleryTestHarness.CreateGallery();
        Window window = await ModelInspectionFixtureGalleryTestHarness
            .ShowAndLoadWindowAsync(gallery);
        try
        {
            await gallery.SelectFixtureThroughRealListForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage old = gallery.ActiveHost!;
            ModelInspectionFixtureSession oldSession = old.Session;
            ModelInspectionFixtureSessionEvidence oldEvidence =
                oldSession.Evidence;

            await gallery.SelectInvalidRawFixtureThroughRealListForTestingAsync();

            await WaitForPendingZeroAsync(oldEvidence);
            AssertRetiredExactlyOnce(
                oldEvidence, oldSession, "invalid-list-selection");
            Assert.IsNull(gallery.ActiveHost);
            Assert.IsNull(((ListView)gallery.FindName("FixtureList")).SelectedItem);
            Assert.IsNull(gallery.ViewModel.SelectedItem);
            Assert.AreEqual(
                "Fixture unavailable: " +
                "MI-002-ready-clean-compatible-model-collapsed.fixture.json" +
                "|$|json.invalid",
                gallery.ViewModel.ValidationStatus);
        }
        finally
        {
            gallery.CloseForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    public async Task OuterFrame_AwayAndBackRetiresOldGalleryAndCreatesFreshDestination()
    {
        (Window window, Frame frame, ModelInspectionFixtureGalleryPage gallery) =
            await ModelInspectionFixtureLifetimeTestHarness
                .ShowDefaultGalleryInOuterFrameAsync();
        try
        {
            await gallery.SelectFixtureThroughRealListForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage oldHost = gallery.ActiveHost!;
            ModelInspectionFixtureSession oldSession = oldHost.Session;
            ModelInspectionFixtureSessionEvidence oldEvidence =
                oldSession.Evidence;

            await ModelInspectionFixtureLifetimeTestHarness.NavigateAsync(
                frame, typeof(Page));
            Assert.IsInstanceOfType<Page>(frame.Content);
            Assert.IsNotInstanceOfType<ModelInspectionFixtureGalleryPage>(
                frame.Content);
            Assert.IsTrue(frame.CanGoBack);
            await ModelInspectionFixtureLifetimeTestHarness.GoBackAsync(frame);

            var replacement = (ModelInspectionFixtureGalleryPage)frame.Content;
            await replacement.CatalogueLoaded;
            await ModelInspectionFixtureLifetimeHarness.DrainDispatcherAsync(
                replacement);
            await WaitForPendingZeroAsync(oldEvidence);
            AssertRetiredExactlyOnce(
                oldEvidence, oldSession, "frame-away-back");
            Assert.AreNotSame(gallery, replacement);
            Assert.IsNull(replacement.ActiveHost);
            Assert.IsNull(replacement.ViewModel.SelectedItem);
            Assert.AreEqual(
                "Catalogue validated: 49 fixtures.",
                replacement.ViewModel.ValidationStatus);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    public async Task TrueExitRoutes_RetireExactlyOnceAndRemoveTheActiveHost()
    {
        foreach (string route in new[]
                 { "choose-another", "gallery-close-control", "window-close" })
        {
            int closeRequests = 0;
            ModelInspectionFixtureGalleryPage gallery = route ==
                    "gallery-close-control"
                ? new ModelInspectionFixtureGalleryPage(
                    new ModelInspectionFixturePackageLoader(
                        ModelInspectionFixtureGalleryTestHarness
                            .CreateCountingReader()),
                    () => closeRequests++)
                : ModelInspectionFixtureGalleryTestHarness.CreateGallery();
            Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
            var unloaded = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler? unloadedHandler = null;
            if (route == "window-close")
            {
                unloadedHandler = (_, _) => unloaded.TrySetResult(true);
                gallery.Unloaded += unloadedHandler;
            }
            try
            {
                await gallery.SelectFixtureThroughRealListForTestingAsync("MI-002");
                ModelInspectionFixtureHostPage host = gallery.ActiveHost!;
                ModelInspectionFixtureSession session = host.Session;
                ModelInspectionFixtureSessionEvidence evidence =
                    session.Evidence;
                await ModelInspectionFixtureLifetimeTestHarness.DriveTrueExitAsync(
                    gallery, window, route);
                if (route == "window-close")
                {
                    await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
                Assert.IsNull(gallery.ActiveHost, route);
                await WaitForPendingZeroAsync(evidence);
                AssertRetiredExactlyOnce(evidence, session, route);
                if (route == "choose-another")
                {
                    Assert.IsNull(gallery.ViewModel.SelectedItem, route);
                    Assert.AreEqual(
                        "gallery:no-active-fixture",
                        gallery.ViewModel.ValidationStatus,
                        route);
                }
                if (route == "gallery-close-control")
                {
                    Assert.AreEqual(1, closeRequests, route);
                }
            }
            finally
            {
                if (unloadedHandler is not null)
                {
                    gallery.Unloaded -= unloadedHandler;
                }
                gallery.CloseForTesting();
                if (route != "window-close")
                {
                    window.Content = null;
                    window.Close();
                }
            }
        }
    }

    [UITestMethod]
    public async Task OuterFrameNavigationAndUnload_RetiresExactlyOnce()
    {
        (Window window, Frame frame, ModelInspectionFixtureGalleryPage gallery) =
            await ModelInspectionFixtureLifetimeTestHarness
                .ShowDefaultGalleryInOuterFrameAsync();
        try
        {
            await gallery.SelectFixtureThroughRealListForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage host = gallery.ActiveHost!;
            ModelInspectionFixtureSession session = host.Session;
            ModelInspectionFixtureSessionEvidence evidence = session.Evidence;

            await ModelInspectionFixtureLifetimeTestHarness.NavigateAsync(
                frame, typeof(Page));

            Assert.IsInstanceOfType<Page>(frame.Content);
            Assert.IsNotInstanceOfType<ModelInspectionFixtureGalleryPage>(
                frame.Content);
            Assert.IsNull(gallery.ActiveHost);
            await WaitForPendingZeroAsync(evidence);
            AssertRetiredExactlyOnce(
                evidence, session, "navigation-and-unloaded");
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    public async Task ProducerAudit_IsPrimedThroughActualControlsBeforeEveryPendingCountDrains()
    {
        ModelInspectionFixtureGalleryPage gallery = ModelInspectionFixtureGalleryTestHarness.CreateGallery();
        Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
        try
        {
            await gallery.PrimeEveryAuditedProducerThroughRealControlsAsync("MI-003");
            ModelInspectionFixtureSession session = gallery.ActiveHost!.Session;
            Assert.AreEqual(1, session.Evidence.HostActivationCount);
            Assert.IsTrue(session.Evidence.DispatcherCallbackCount > 0);
            Assert.IsTrue(session.Evidence.MotionBatchCount > 0);
            Assert.IsTrue(session.Evidence.FocusRequestCount > 0);
            Assert.IsTrue(session.Evidence.DisclosureOperationCount > 0);
            Assert.IsTrue(session.Evidence.LiveNotificationCount > 0);
            await WaitForPendingZeroAsync(session.Evidence);
            AssertAllPendingZero(session.Evidence, session.Service);
            Assert.AreEqual(0, session.Evidence.ServiceRetirementCount);
            Assert.AreEqual(0, session.Evidence.ServiceDisposalCount);
            Assert.AreEqual(0, session.Evidence.AnimationDriverDisposalCount);
            Assert.AreEqual(0, session.Evidence.MotionSettingsDisposalCount);

            gallery.CloseForTesting();
            await WaitForPendingZeroAsync(session.Evidence);
            AssertRetiredExactlyOnce(
                session.Evidence,
                session,
                "producer-audit-retirement");
        }
        finally { gallery.CloseForTesting(); window.Content = null; window.Close(); }
    }

    [TestMethod]
    public void LifetimeBoundaryAudit_RequiresEveryProducerToBeObservedBeforeDrain()
    {
        new ModelInspectionFixtureGalleryTests()
            .DebugGallerySourceAndIlClosure_ForbidExternalComposition();
        new ModelInspectionFixtureAdapterTests()
            .DebugFixtures_IlRecursivelyExcludesForbiddenIoAndComposition();
    }

    [TestMethod]
    public void DebugFactoryAndClosure_AllowOnlyConcreteFixtureSessionConstruction()
    {
        MethodInfo factory = typeof(ModelInspectionPage).GetMethod(
            "CreateForFixture", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.IsNotNull(factory);
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(ModelInspectionFixtureSession), typeof(bool),
                typeof(System.Action<ResourceDictionary>)
            },
            factory.GetParameters().Select(parameter => parameter.ParameterType)
                .ToArray());

        new ModelInspectionFixtureGalleryTests()
            .DebugGallerySourceAndIlClosure_ForbidExternalComposition();
        new ModelInspectionFixtureAdapterTests()
            .DebugFixtures_IlRecursivelyExcludesForbiddenIoAndComposition();
    }

    [UITestMethod]
    public void AuditedMotion_RetargetClosesSupersededBatchAndKeepsIndependentWorkPending()
    {
        var inner = new RetargetSuppressingAnimationDriver();
        using var session = new ModelInspectionFixtureSession(
            ModelInspectionFixtureTestCatalogue.Get("MI-003").Input,
            animationDriverFactory: () => inner);
        IModelInspectionAnimationDriver driver = session.CreateAnimationDriver();
        var firstTarget = new Border();
        var independentTarget = new Border();
        ModelInspectionVisualOperationKey first = Key(1);
        ModelInspectionVisualOperationKey replacement = Key(2);
        ModelInspectionVisualOperationKey independent = Key(3);

        driver.StartStageStatus(firstTarget, first, _ => { });
        Assert.AreEqual(1, session.Evidence.PendingMotionBatchCount);
        driver.StartStageStatus(firstTarget, replacement, _ => { });
        Assert.AreEqual(1, session.Evidence.PendingMotionBatchCount,
            "Retargeting must close the whole superseded batch whose callback is suppressed.");
        driver.StartStageStatus(independentTarget, independent, _ => { });
        Assert.AreEqual(2, session.Evidence.PendingMotionBatchCount,
            "A non-conflicting batch must remain independently pending.");

        inner.Complete(firstTarget);
        Assert.AreEqual(1, session.Evidence.PendingMotionBatchCount);
        inner.Complete(independentTarget);
        Assert.AreEqual(0, session.Evidence.PendingMotionBatchCount);
        Assert.AreEqual(3, session.Evidence.MotionBatchCount);
    }

    [UITestMethod]
    public void AuditedMotion_CancelAllClosesPendingAuditWhenInnerCancellationThrows()
    {
        var inner = new RetargetSuppressingAnimationDriver(
            cancellationFailures: 1);
        var session = new ModelInspectionFixtureSession(
            ModelInspectionFixtureTestCatalogue.Get("MI-003").Input,
            animationDriverFactory: () => inner);
        IModelInspectionAnimationDriver driver = session.CreateAnimationDriver();
        driver.StartStageStatus(
            new Border(),
            Key(1),
            _ => { });

        Assert.ThrowsExactly<InvalidOperationException>(driver.CancelAll);
        Assert.AreEqual(1, session.Evidence.MotionBatchCount);
        Assert.AreEqual(1, session.Evidence.CompletedMotionBatchCount);
        Assert.AreEqual(0, session.Evidence.PendingMotionBatchCount);
        Assert.AreEqual(1, session.Evidence.AnimationCancellationCount);

        session.Dispose();
    }

    [UITestMethod]
    public void AuditedMotion_DisposeClosesPendingAuditWhenInnerCancellationThrowsAndSecondDisposeIsSafe()
    {
        var inner = new RetargetSuppressingAnimationDriver(
            cancellationFailures: 1);
        var session = new ModelInspectionFixtureSession(
            ModelInspectionFixtureTestCatalogue.Get("MI-003").Input,
            animationDriverFactory: () => inner);
        IModelInspectionAnimationDriver driver = session.CreateAnimationDriver();
        driver.StartStageStatus(
            new Border(),
            Key(1),
            _ => { });

        Assert.ThrowsExactly<InvalidOperationException>(driver.Dispose);
        Assert.AreEqual(1, session.Evidence.MotionBatchCount);
        Assert.AreEqual(1, session.Evidence.CompletedMotionBatchCount);
        Assert.AreEqual(0, session.Evidence.PendingMotionBatchCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);

        driver.Dispose();
        Assert.AreEqual(0, session.Evidence.PendingMotionBatchCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        session.Dispose();
    }

    private static async Task<ModelInspectionFixtureStaleInvariant> CaptureAsync(
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        string gallerySelection)
    {
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");
        var outgoing = (InspectionContentCard)page.FindName(
            "OutgoingProgressContentCard");
        var observer = new ModelInspectionFixtureScreenObserver();
        using IModelInspectionFixtureObservationSession observation = observer.Begin(page);
        ModelInspectionObservedScreen screen = await observation.CaptureAsync(
            System.Threading.CancellationToken.None);
        return new ModelInspectionFixtureStaleInvariant(
            screen, page.CurrentPresentation!, page.CurrentFooterStatus,
            FocusManager.GetFocusedElement(page.XamlRoot),
            content.LiveRegionChangeNotificationCount,
            outcome.LiveRegionChangeNotificationCount,
            outgoing.Visibility,
            outgoing.Presentation,
            page.CurrentPresentation!.ContentCard.ProgressRows,
            page.CurrentPresentation.ContentCard.ProgressRows.Items.Cast<object>()
                .ToArray(),
            gallerySelection,
            string.Join("\u001f", page.CurrentPresentation!.ContentCard
                .ProgressRows.Items.Select(item => item.Title)),
            session.Evidence.DispatcherCallbackCount,
            session.Evidence.MotionBatchCount,
            session.Evidence.FocusRequestCount,
            session.Evidence.DisclosureOperationCount,
            session.Evidence.LiveNotificationCount);
    }

    private static void AssertAllPendingZero(
        ModelInspectionFixtureSessionEvidence evidence,
        DebugModelInspectionService service)
    {
        Assert.AreEqual(0, service.PendingCallCount);
        Assert.AreEqual(0, evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(
            evidence.CancellationRegistrationCreatedCount,
            evidence.CancellationRegistrationReleasedCount);
        Assert.AreEqual(0, evidence.PendingDispatcherCallbackCount);
        Assert.AreEqual(
            evidence.DispatcherCallbackCount,
            evidence.CompletedDispatcherCallbackCount);
        Assert.AreEqual(0, evidence.PendingMotionBatchCount);
        Assert.AreEqual(
            evidence.MotionBatchCount,
            evidence.CompletedMotionBatchCount);
        Assert.AreEqual(0, evidence.PendingFocusRequestCount);
        Assert.AreEqual(
            evidence.FocusRequestCount,
            evidence.CompletedFocusRequestCount);
        Assert.AreEqual(0, evidence.PendingDisclosureOperationCount);
        Assert.AreEqual(
            evidence.DisclosureOperationCount,
            evidence.CompletedDisclosureOperationCount);
        Assert.AreEqual(0, evidence.PendingLiveNotificationCount);
        Assert.AreEqual(
            evidence.LiveNotificationCount,
            evidence.CompletedLiveNotificationCount);
    }

    private static void AssertRetiredExactlyOnce(
        ModelInspectionFixtureSessionEvidence evidence,
        ModelInspectionFixtureSession session,
        string route)
    {
        Assert.AreEqual(1, evidence.HostActivationCount, route);
        Assert.AreEqual(1, evidence.PageRetirementCount, route);
        Assert.AreEqual(1, evidence.ServiceRetirementCount, route);
        Assert.AreEqual(1, evidence.ServiceDisposalCount, route);
        Assert.AreEqual(1, evidence.SessionRetirementCount, route);
        Assert.AreEqual(1, evidence.SessionDisposalCount, route);
        Assert.AreEqual(1, evidence.AnimationDriverDisposalCount, route);
        Assert.AreEqual(1, evidence.MotionSettingsDisposalCount, route);
        Assert.AreEqual(
            evidence.CancellationRegistrationCreatedCount,
            evidence.CancellationRegistrationReleasedCount,
            route);
        Assert.IsTrue(
            evidence.CancellationRegistrationCreatedCount > 0,
            route);
        AssertAllPendingZero(evidence, session.Service);
    }

    private static async Task<ModelInspectionObservedScreen>
        ObserveCumulativeAsync(ModelInspectionPage page)
    {
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(page);
        ModelInspectionObservedScreen observed = await observation.CaptureAsync(
            CancellationToken.None);
        return ModelInspectionFixtureGalleryTestHarness
            .WithCumulativeAnnouncements(observed, page);
    }

    private static async Task WaitForPendingZeroAsync(
        ModelInspectionFixtureSessionEvidence evidence)
    {
        try
        {
            await evidence.WaitForPendingZeroAsync().WaitAsync(
                TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Timed out waiting for fixture producer audits to drain.");
        }
    }

    private static ModelInspectionVisualOperationKey Key(long revision) => new(
        new ModelInspectionRenderKey(1, revision),
        revision);
}

internal sealed class ModelInspectionFixtureStaleInvariant
{
    internal ModelInspectionFixtureStaleInvariant(
        ModelInspectionObservedScreen independentScreen,
        object presentation,
        object footer,
        object? focus,
        int progressAnnouncements,
        int outcomeAnnouncements,
        Visibility outgoingOverlay,
        object outgoingPresentation,
        object retainedRows,
        object[] retainedRowInstances,
        string gallerySelection,
        string orderedRowIds,
        int dispatcherCallbacks,
        int motionBatches,
        int focusRequests,
        int disclosureOperations,
        int liveNotifications)
    {
        IndependentScreen = independentScreen;
        Presentation = presentation;
        Footer = footer;
        Focus = focus;
        ProgressAnnouncements = progressAnnouncements;
        OutcomeAnnouncements = outcomeAnnouncements;
        OutgoingOverlay = outgoingOverlay;
        OutgoingPresentation = outgoingPresentation;
        RetainedRows = retainedRows;
        RetainedRowInstances = retainedRowInstances;
        GallerySelection = gallerySelection;
        OrderedRowIds = orderedRowIds;
        DispatcherCallbacks = dispatcherCallbacks;
        MotionBatches = motionBatches;
        FocusRequests = focusRequests;
        DisclosureOperations = disclosureOperations;
        LiveNotifications = liveNotifications;
    }

    internal ModelInspectionObservedScreen IndependentScreen { get; }
    internal object Presentation { get; }
    internal object Footer { get; }
    internal object? Focus { get; }
    internal int ProgressAnnouncements { get; }
    internal int OutcomeAnnouncements { get; }
    internal Visibility OutgoingOverlay { get; }
    internal object OutgoingPresentation { get; }
    internal object RetainedRows { get; }
    internal object[] RetainedRowInstances { get; }
    internal string GallerySelection { get; }
    internal string OrderedRowIds { get; }
    internal int DispatcherCallbacks { get; }
    internal int MotionBatches { get; }
    internal int FocusRequests { get; }
    internal int DisclosureOperations { get; }
    internal int LiveNotifications { get; }

    internal void AssertUnchanged(ModelInspectionFixtureStaleInvariant after,
        string scenario)
    {
        Assert.AreEqual(IndependentScreen.Figma.State, after.IndependentScreen.Figma.State,
            scenario);
        Assert.AreEqual(IndependentScreen.Content.Mode, after.IndependentScreen.Content.Mode,
            scenario);
        Assert.AreEqual(IndependentScreen.Footer.Status, after.IndependentScreen.Footer.Status,
            scenario);
        Assert.AreEqual(IndependentScreen.Focus.Target, after.IndependentScreen.Focus.Target,
            scenario);
        CollectionAssert.AreEqual(IndependentScreen.RowsAndScroll.OrderedRowIds.ToArray(),
            after.IndependentScreen.RowsAndScroll.OrderedRowIds.ToArray(), scenario);
        Assert.AreEqual(IndependentScreen.Announcements.Count,
            after.IndependentScreen.Announcements.Count, scenario);
        Assert.AreSame(Presentation, after.Presentation, scenario);
        Assert.AreEqual(Footer, after.Footer, scenario);
        Assert.AreSame(Focus, after.Focus, scenario);
        Assert.AreEqual(ProgressAnnouncements, after.ProgressAnnouncements, scenario);
        Assert.AreEqual(OutcomeAnnouncements, after.OutcomeAnnouncements, scenario);
        Assert.AreEqual(OutgoingOverlay, after.OutgoingOverlay, scenario);
        Assert.AreSame(OutgoingPresentation, after.OutgoingPresentation, scenario);
        Assert.AreSame(RetainedRows, after.RetainedRows, scenario);
        CollectionAssert.AreEqual(RetainedRowInstances, after.RetainedRowInstances,
            scenario);
        Assert.AreEqual(GallerySelection, after.GallerySelection, scenario);
        Assert.AreEqual(OrderedRowIds, after.OrderedRowIds, scenario);
        Assert.AreEqual(DispatcherCallbacks, after.DispatcherCallbacks, scenario);
        Assert.AreEqual(MotionBatches, after.MotionBatches, scenario);
        Assert.AreEqual(FocusRequests, after.FocusRequests, scenario);
        Assert.AreEqual(DisclosureOperations, after.DisclosureOperations, scenario);
        Assert.AreEqual(LiveNotifications, after.LiveNotifications, scenario);
    }
}

internal static class ModelInspectionFixtureLifetimeHarness
{
    internal static async Task DrainDispatcherAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
            () => drained.TrySetResult(true)));
        await drained.Task;
    }

}

[DoNotParallelize]
internal static class ModelInspectionFixtureLifetimeTestHarness
{
    internal static async Task DriveReplacementRouteAsync(
        ModelInspectionFixtureGalleryPage gallery,
        string route)
    {
        switch (route)
        {
            case "reset":
                ModelInspectionFixtureGalleryTestHarness.Invoke(
                    (Button)gallery.FindName("ResetFixtureButton"));
                await gallery.SelectionCompletedForTesting;
                break;
            case "fixture-switch":
                await gallery.SelectFixtureThroughRealListForTestingAsync(
                    "MI-003");
                break;
            default:
                Assert.Fail("Unknown replacement route: " + route);
                break;
        }
    }

    internal static async Task DriveTrueExitAsync(
        ModelInspectionFixtureGalleryPage gallery,
        Window window,
        string route)
    {
        switch (route)
        {
            case "choose-another":
                _ = await gallery.DispatchDeclaredActionForTestingAsync("choose-another");
                break;
            case "gallery-close-control":
                ModelInspectionFixtureGalleryTestHarness.Invoke(
                    (Button)gallery.FindName("CloseFixtureGalleryButton"));
                break;
            case "window-close":
                window.Close();
                break;
            default:
                Assert.Fail("Unknown exit route: " + route);
                break;
        }
        await ModelInspectionFixtureGalleryTestHarness.DrainAsync(gallery);
    }

    internal static async Task<(Window Window, Frame Frame,
        ModelInspectionFixtureGalleryPage Gallery)>
        ShowDefaultGalleryInOuterFrameAsync()
    {
        var frame = new Frame();
        var window = new Window { Content = frame };
        window.Activate();
        object activation = ModelInspectionFixtureGalleryPage.CreateActivation(
            static () => true);
        await NavigateAsync(
            frame,
            typeof(ModelInspectionFixtureGalleryPage),
            activation);
        var gallery = (ModelInspectionFixtureGalleryPage)frame.Content;
        await gallery.CatalogueLoaded;
        await ModelInspectionFixtureLifetimeHarness.DrainDispatcherAsync(gallery);
        return (window, frame, gallery);
    }

    internal static Task NavigateAsync(Frame frame, Type pageType) =>
        NavigateAsync(frame, pageType, parameter: null);

    internal static async Task NavigateAsync(
        Frame frame,
        Type pageType,
        object? parameter)
    {
        Task<NavigationEventArgs> navigated = NextNavigationAsync(frame);
        Assert.IsTrue(frame.Navigate(pageType, parameter));
        _ = await navigated;
    }

    internal static async Task GoBackAsync(Frame frame)
    {
        Task<NavigationEventArgs> navigated = NextNavigationAsync(frame);
        frame.GoBack();
        _ = await navigated;
    }

    private static Task<NavigationEventArgs> NextNavigationAsync(Frame frame)
    {
        var completion = new TaskCompletionSource<NavigationEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        NavigatedEventHandler? handler = null;
        handler = (_, eventArguments) =>
        {
            frame.Navigated -= handler;
            completion.TrySetResult(eventArguments);
        };
        frame.Navigated += handler;
        return completion.Task;
    }
}

internal sealed class RetargetSuppressingAnimationDriver :
    IModelInspectionAnimationDriver
{
    private readonly Dictionary<UIElement,
        Action<ModelInspectionVisualOperationKey>> stageCompletions =
        new(ReferenceEqualityComparer.Instance);
    private int cancellationFailures;

    internal RetargetSuppressingAnimationDriver(
        int cancellationFailures = 0)
    {
        this.cancellationFailures = cancellationFailures;
    }

    public void StartStageStatus(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed) =>
        stageCompletions[target] = completed;

    public void StartActiveDetail(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed) =>
        throw new NotSupportedException();

    public void StartDisclosure(
        UIElement chevron,
        FrameworkElement viewport,
        IReadOnlyList<UIElement> followingElements,
        bool isExpanded,
        IReadOnlyList<double> previousTopOffsets,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed) =>
        throw new NotSupportedException();

    public void StartTerminal(
        UIElement outgoing,
        UIElement incoming,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed) =>
        throw new NotSupportedException();

    public void CancelAll()
    {
        if (cancellationFailures > 0)
        {
            cancellationFailures--;
            throw new InvalidOperationException(
                "Injected animation cancellation failure.");
        }

        stageCompletions.Clear();
    }

    public void Dispose() => stageCompletions.Clear();

    internal void Complete(UIElement target)
    {
        Action<ModelInspectionVisualOperationKey> completed =
            stageCompletions[target];
        stageCompletions.Remove(target);
        completed(default);
    }
}
#endif
