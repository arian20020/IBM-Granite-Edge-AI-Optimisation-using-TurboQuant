#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class ModelInspectionFixtureInteractionTests
{
    private static readonly string[] CanonicalActionIds =
    [
        "cancel",
        "cancel-request",
        "choose-another",
        "collapse",
        "expand",
        "locate-missing",
        "reset",
        "restart",
        "restart-attempt",
        "retry",
        "retry-attempt"
    ];

    [UITestMethod]
    public async Task EveryDeclaredActionId_UsesTheRenderedControlAndChangesOnlyItsActualLoadedSurface()
    {
        ModelInspectionFixtureGalleryPage gallery = ModelInspectionFixtureGalleryTestHarness.CreateGallery();
        Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
        try
        {
            CollectionAssert.AreEquivalent(
                CanonicalActionIds,
                gallery.ViewModel.Items.SelectMany(item => item.Fixture.Interactions)
                    .Select(interaction => interaction.Id).Distinct().ToArray());
            foreach (ModelInspectionFixtureListItem item in gallery.ViewModel.Items)
            {
                foreach (ModelInspectionFixtureInteraction interaction in item.Fixture.Interactions)
                {
                    string diagnostic = Case(item.Id, interaction.Id);
                    await SelectThroughRealListAsync(gallery, item.Id);
                    await gallery.PositionAtDeclaredInteractionCheckpointThroughScenarioAsync(
                        item.Id, interaction.SourceCheckpoint);
                    ModelInspectionFixtureHostPage host = gallery.ActiveHost!;
                    ModelInspectionFixtureSession session = host.Session;
                    ModelInspectionPage page = host.ModelInspectionPage!;
                    Button control = await gallery.FindRenderedActionButtonForTestingAsync(interaction.Id);
                    Assert.IsTrue(control.IsEnabled, diagnostic);
                    ModelInspectionFixtureGalleryTestHarness.AssertActualRenderedControl(
                        gallery, page, interaction.Id, control, diagnostic);
                    object? disclosureRows =
                        interaction.Kind is ModelInspectionFixtureInteractionKind.Expand or
                            ModelInspectionFixtureInteractionKind.Collapse
                            ? ActiveDisclosureRows(page, diagnostic)
                            : null;
                    object content = page.FindName("InspectionContentCardControl");
                    int live = ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(page);
                    bool immediateServiceInteraction =
                        IsImmediateServiceInteraction(interaction);
                    int serviceCallsBefore = session.Evidence.ServiceCallCount;
                    int cancellationsBefore =
                        session.Evidence.CancellationObservationCount;
                    int releasedCheckpointsBefore =
                        session.Evidence.ReleasedServiceCheckpoints.Count;
                    int terminalCompletionsBefore =
                        session.Evidence.TerminalCompletionCount;
                    long attemptGenerationBefore =
                        page.ViewModel!.Snapshot.RenderKey.AttemptGeneration;
                    string[] availableAtCheckpoint = item.Fixture.Interactions
                        .Where(candidate => string.Equals(
                            candidate.SourceCheckpoint,
                            interaction.SourceCheckpoint,
                            System.StringComparison.Ordinal))
                        .Select(candidate => candidate.Id)
                        .Distinct(System.StringComparer.Ordinal)
                        .ToArray();

                    foreach (string omittedActionId in
                             CanonicalActionIds.Except(
                                 availableAtCheckpoint,
                                 System.StringComparer.Ordinal))
                    {
                        string omittedDiagnostic =
                            $"{diagnostic}; omitted={omittedActionId}; " +
                            $"checkpoint={interaction.SourceCheckpoint}";
                        ModelInspectionFixtureNoMutationSnapshot before =
                            ModelInspectionFixtureNoMutationSnapshot.Capture(gallery);
                        Assert.IsNull(
                            await gallery.FindRenderedActionButtonOrNullForTestingAsync(
                                omittedActionId),
                            omittedDiagnostic);
                        ModelInspectionFixtureActionDispatchResult omitted =
                            await gallery.DispatchDeclaredActionForTestingAsync(
                                omittedActionId);
                        Assert.AreEqual(
                            omittedActionId, omitted.ActionId, omittedDiagnostic);
                        Assert.IsFalse(omitted.IsDispatched, omittedDiagnostic);
                        Assert.AreEqual(
                            "fixture.action.unsupported",
                            omitted.RuleCode,
                            omittedDiagnostic);
                        before.AssertUnchanged(gallery, omittedDiagnostic);
                    }

                    IModelInspectionFixtureObservationSession? observation =
                        interaction.LifetimeEffect ==
                            ModelInspectionFixtureInteractionLifetimeEffect.None &&
                        !immediateServiceInteraction
                            ? new ModelInspectionFixtureScreenObserver().Begin(page)
                            : null;

                    try
                    {
                        ModelInspectionFixtureActionDispatchResult result =
                            await gallery.DispatchDeclaredActionForTestingAsync(interaction.Id);
                        Assert.AreEqual(interaction.Id, result.ActionId, diagnostic);
                        Assert.IsTrue(result.IsDispatched, diagnostic);
                        Assert.AreEqual("fixture.action.dispatched", result.RuleCode, diagnostic);
                        await gallery.SelectionCompletedForTesting;

                        if (immediateServiceInteraction)
                        {
                            await ModelInspectionFixtureGalleryTestHarness.DrainAsync(page);
                            await ModelInspectionFixtureGalleryTestHarness
                                .WaitForUiPendingZeroAsync(page, session);
                            page.UpdateLayout();

                            Assert.AreSame(host, gallery.ActiveHost, diagnostic);
                            Assert.AreSame(page, gallery.ActiveHost!.ModelInspectionPage,
                                diagnostic);
                            Assert.AreSame(session, gallery.ActiveHost.Session, diagnostic);
                            Assert.AreEqual(item.Id, gallery.ViewModel.SelectedItem!.Id,
                                diagnostic);
                            Assert.AreEqual(interaction.ExpectedFocus,
                                ModelInspectionFixtureGalleryTestHarness
                                    .SemanticFocusedTarget(page),
                                diagnostic);
                            Assert.AreEqual(interaction.ExpectedFooterStatus,
                                (ModelInspectionExpectedFooterStatus)
                                    page.CurrentFooterStatus,
                                diagnostic);
                            Assert.AreEqual(
                                live + ExpectedJourneyAnnouncementDelta(
                                    interaction),
                                ModelInspectionFixtureGalleryTestHarness
                                    .LiveNotificationCount(page),
                                diagnostic);
                            Assert.AreEqual(
                                ModelInspectionFigmaState.InspectionProgress,
                                page.CurrentPresentation!.State,
                                diagnostic);
                            Assert.IsTrue(page.ViewModel!.Snapshot.IsRunActive,
                                diagnostic);
                            Assert.AreEqual(1, session.Service.PendingCallCount,
                                diagnostic);
                            Assert.AreEqual(1,
                                session.Evidence.ActiveCancellationRegistrationCount,
                                diagnostic);
                            Assert.AreEqual(releasedCheckpointsBefore,
                                session.Evidence.ReleasedServiceCheckpoints.Count,
                                diagnostic);
                            Assert.AreEqual(terminalCompletionsBefore,
                                session.Evidence.TerminalCompletionCount,
                                diagnostic);
                            Assert.AreEqual(0, session.Evidence.PageRetirementCount,
                                diagnostic);
                            Assert.AreEqual(0,
                                session.Evidence.ServiceRetirementCount,
                                diagnostic);
                            Assert.AreEqual(0, session.Evidence.ServiceDisposalCount,
                                diagnostic);
                            Assert.AreEqual(0,
                                session.Evidence.SessionRetirementCount,
                                diagnostic);
                            Assert.AreEqual(0, session.Evidence.SessionDisposalCount,
                                diagnostic);
                            ModelInspectionFixtureGalleryTestHarness
                                .AssertUiPendingZero(session, diagnostic);
                            AssertImmediateTargetCheckpoint(
                                session, interaction, diagnostic);

                            if (interaction.Kind ==
                                ModelInspectionFixtureInteractionKind.Cancel)
                            {
                                Assert.AreEqual(serviceCallsBefore,
                                    session.Evidence.ServiceCallCount,
                                    diagnostic);
                                Assert.AreEqual(cancellationsBefore + 1,
                                    session.Evidence.CancellationObservationCount,
                                    diagnostic);
                                Assert.AreEqual(attemptGenerationBefore,
                                    page.ViewModel.Snapshot.RenderKey
                                        .AttemptGeneration,
                                    diagnostic);
                                Assert.IsTrue(
                                    page.ViewModel.Snapshot.IsCancellationRequested,
                                    diagnostic);
                                Assert.IsFalse(control.IsEnabled, diagnostic);
                            }
                            else
                            {
                                Assert.AreEqual(serviceCallsBefore + 1,
                                    session.Evidence.ServiceCallCount,
                                    diagnostic);
                                Assert.AreEqual(cancellationsBefore,
                                    session.Evidence.CancellationObservationCount,
                                    diagnostic);
                                Assert.AreEqual(attemptGenerationBefore + 1,
                                    page.ViewModel.Snapshot.RenderKey
                                        .AttemptGeneration,
                                    diagnostic);
                                Assert.IsFalse(
                                    page.ViewModel.Snapshot.IsCancellationRequested,
                                    diagnostic);
                                Button cancel = RenderedPageAction(
                                    page, "cancel", diagnostic);
                                Assert.IsTrue(cancel.IsEnabled, diagnostic);
                            }

                            continue;
                        }

                        if (interaction.LifetimeEffect == ModelInspectionFixtureInteractionLifetimeEffect.NoActiveFixture)
                        {
                            Assert.IsNull(gallery.ActiveHost, diagnostic);
                            Assert.IsNull(gallery.ViewModel.SelectedItem, diagnostic);
                            Assert.AreEqual(interaction.ExpectedFocus ?? string.Empty,
                                ModelInspectionFixtureGalleryTestHarness.SemanticFocusedTarget(page),
                                diagnostic);
                            Assert.AreEqual(interaction.ExpectedFooterStatus,
                                (ModelInspectionExpectedFooterStatus)page.CurrentFooterStatus,
                                diagnostic);
                            Assert.AreEqual(
                                live + ExpectedJourneyAnnouncementDelta(
                                    interaction),
                                ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(page),
                                diagnostic);
                            Assert.AreEqual(1, session.Evidence.PageRetirementCount, diagnostic);
                            Assert.AreEqual(1, session.Evidence.SessionDisposalCount, diagnostic);
                            ModelInspectionFixtureGalleryTestHarness.AssertAllPendingZero(
                                session, diagnostic);
                            continue;
                        }

                        ModelInspectionPage loaded = gallery.ActiveHost!.ModelInspectionPage!;
                        ValidatedModelInspectionFixture target =
                            ModelInspectionFixtureGalleryTestHarness.ExpectedTargetFixture(
                                item.Fixture, interaction);
                        await ModelInspectionFixtureGalleryTestHarness
                            .WaitForInteractionSettlementAsync(
                                loaded,
                                gallery.ActiveHost.Session,
                                target.Expected.Figma.State,
                                diagnostic);
                        ModelInspectionObservedScreen actual;
                        if (observation is not null)
                        {
                            actual = await observation.CaptureAsync(CancellationToken.None);
                        }
                        else
                        {
                            using IModelInspectionFixtureObservationSession replacementObservation =
                                new ModelInspectionFixtureScreenObserver().Begin(loaded);
                            actual = await replacementObservation.CaptureAsync(CancellationToken.None);
                        }

                        Assert.IsTrue(actual.RenderBarrier.DispatcherDrained && actual.RenderBarrier.LayoutUpdated,
                            diagnostic);
                        Assert.AreEqual(item.Id, gallery.ViewModel.SelectedItem!.Id,
                            diagnostic);
                        ModelInspectionFixtureGalleryTestHarness.AssertExactScreen(
                            gallery,
                            target,
                            actual,
                            diagnostic,
                            sourceFixture: item.Fixture,
                            interactionKind: interaction.Kind,
                            journeyRelativeAnnouncements: true);
                        Assert.AreEqual(interaction.ExpectedFocus,
                            actual.Focus.Target, diagnostic);
                        Assert.AreEqual(interaction.ExpectedFooterStatus,
                            (ModelInspectionExpectedFooterStatus)loaded.CurrentFooterStatus,
                            diagnostic);
                        Assert.AreEqual(
                            live + ExpectedJourneyAnnouncementDelta(interaction),
                            ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(loaded), diagnostic);

                        if (interaction.LifetimeEffect == ModelInspectionFixtureInteractionLifetimeEffect.None)
                        {
                            Assert.AreSame(host, gallery.ActiveHost, diagnostic);
                            Assert.AreSame(page, loaded, diagnostic);
                            Assert.AreSame(content, loaded.FindName("InspectionContentCardControl"), diagnostic);
                            if (interaction.Kind is ModelInspectionFixtureInteractionKind.Expand or
                                ModelInspectionFixtureInteractionKind.Collapse)
                            {
                                Assert.AreSame(disclosureRows,
                                    ActiveDisclosureRows(loaded, diagnostic),
                                    diagnostic);
                            }
                            Assert.AreEqual(0, session.Evidence.PageRetirementCount, diagnostic);
                        }
                        else
                        {
                            Assert.AreNotSame(host, gallery.ActiveHost, diagnostic);
                            Assert.AreEqual(1, session.Evidence.PageRetirementCount, diagnostic);
                            Assert.AreEqual(1, session.Evidence.SessionDisposalCount, diagnostic);
                        }
                    }
                    finally
                    {
                        observation?.Dispose();
                    }
                }
            }
        }
        finally { gallery.CloseForTesting(); window.Content = null; window.Close(); }
    }

    [UITestMethod]
    public async Task AllFixtures_ReadExactlyOnceAndRecordRealSetupInteractionsBeforeFreshActionDispatch()
    {
        var reader = ModelInspectionFixtureGalleryTestHarness.CreateCountingReader();
        ModelInspectionFixtureGalleryPage gallery = ModelInspectionFixtureGalleryTestHarness.CreateGallery(reader);
        Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
        try
        {
            Assert.AreEqual(49, gallery.ViewModel.Items.Count);
            foreach (ModelInspectionFixtureListItem item in gallery.ViewModel.Items)
            {
                await SelectThroughRealListAsync(gallery, item.Id);
                ModelInspectionPage page = gallery.ActiveHost!.ModelInspectionPage!;
                ModelInspectionFixtureSession session = gallery.ActiveHost.Session;
                var observer = new ModelInspectionFixtureScreenObserver();
                using IModelInspectionFixtureObservationSession observation = observer.Begin(page);
                ModelInspectionObservedScreen screen = await observation.CaptureAsync(CancellationToken.None);
                Assert.IsTrue(screen.RenderBarrier.DispatcherDrained, item.Id);
                CollectionAssert.AreEquivalent(
                    item.Fixture.Input.SetupSteps.Where(step => step.InteractionId is not null)
                        .Select(step => step.InteractionId!).ToArray(),
                    session.Evidence.InvokedSetupInteractionIds.ToArray(), item.Id);
            }
            CollectionAssert.AreEqual(ModelInspectionFixtureGalleryTestHarness.ExpectedPackageUris(), reader.Requests);
            Assert.AreEqual(51, reader.Requests.Count);
            Assert.IsTrue(reader.Requests.All(uri => reader.Count(uri) == 1));
        }
        finally { gallery.CloseForTesting(); window.Content = null; window.Close(); }
    }

    [UITestMethod]
    public async Task EveryRenderedComingLaterAction_IsDisabledAndNeverMutates()
    {
        ModelInspectionFixtureGalleryPage gallery = ModelInspectionFixtureGalleryTestHarness.CreateGallery();
        Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
        try
        {
            foreach (ModelInspectionFixtureListItem item in gallery.ViewModel.Items)
            {
                string[] declared = item.Fixture.Interactions
                    .Select(interaction => interaction.Id).ToArray();
                ModelInspectionExpectedAction[] unavailable = item.Fixture.Expected.Actions.Items
                    .Where(action => (!action.Visible || !action.Enabled) &&
                        (!CanonicalActionIds.Contains(action.Id) ||
                         declared.Contains(action.Id)))
                    .ToArray();
                if (unavailable.Length == 0)
                {
                    continue;
                }

                await SelectThroughRealListAsync(gallery, item.Id);
                ModelInspectionPage page = gallery.ActiveHost!.ModelInspectionPage!;

                foreach (ModelInspectionExpectedAction action in unavailable)
                {
                    string diagnostic = Case(item.Id, action.Id);
                    ModelInspectionFixtureNoMutationSnapshot before =
                        ModelInspectionFixtureNoMutationSnapshot.Capture(gallery);
                    Button? button = await gallery.FindRenderedActionButtonOrNullForTestingAsync(action.Id);
                    Assert.AreEqual(action.Visible, button is not null, diagnostic);
                    if (button is not null)
                    {
                        Assert.IsFalse(button.IsEnabled, diagnostic);
                        ModelInspectionFixtureGalleryTestHarness.AssertActualRenderedControl(
                            gallery, page, action.Id, button, diagnostic);
                    }

                    ModelInspectionFixtureActionDispatchResult result =
                        await gallery.DispatchDeclaredActionForTestingAsync(action.Id);
                    Assert.AreEqual(action.Id, result.ActionId, diagnostic);
                    Assert.IsFalse(result.IsDispatched, diagnostic);
                    Assert.AreEqual(action.Visible
                            ? "fixture.action.disabled"
                            : "fixture.action.unsupported",
                        result.RuleCode, diagnostic);
                    before.AssertUnchanged(gallery, diagnostic);
                }
            }
        }
        finally { gallery.CloseForTesting(); window.Content = null; window.Close(); }
    }

    [TestMethod]
    public void RegionKeys_ActionsChangesWhenOnlyActionIdChanges()
    {
        var originalAction = new InspectionActionPresentation
        {
            Text = "Retry inspection",
            IsEnabled = true,
            Visibility = Visibility.Visible,
            AutomationName = "Retry model inspection",
            ActionId = "retry",
            MinimumWidth = 174d
        };
        var changedAction = new InspectionActionPresentation
        {
            Text = originalAction.Text,
            Command = originalAction.Command,
            CommandParameter = originalAction.CommandParameter,
            IsEnabled = originalAction.IsEnabled,
            Visibility = originalAction.Visibility,
            AutomationName = originalAction.AutomationName,
            ActionId = "restart",
            AutomationHelpText = originalAction.AutomationHelpText,
            MinimumWidth = originalAction.MinimumWidth
        };
        var originalCard = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Inspection interrupted",
            Message = "Try again.",
            PrimaryAction = originalAction
        };
        var changedCard = new InspectionActionCardPresentation
        {
            Mode = originalCard.Mode,
            Title = originalCard.Title,
            Message = originalCard.Message,
            AutomationName = originalCard.AutomationName,
            CancelAction = originalCard.CancelAction,
            SecondaryActionOne = originalCard.SecondaryActionOne,
            SecondaryActionTwo = originalCard.SecondaryActionTwo,
            PrimaryAction = changedAction
        };
        MethodInfo createActionsRegionKey =
            typeof(ModelInspectionPresentationFactory).GetMethod(
                "CreateActionsRegionKey",
                BindingFlags.Static | BindingFlags.NonPublic)!;

        var originalKey = (ModelInspectionRegionKey)createActionsRegionKey.Invoke(
            obj: null,
            ["same-outcome", originalCard])!;
        var changedKey = (ModelInspectionRegionKey)createActionsRegionKey.Invoke(
            obj: null,
            ["same-outcome", changedCard])!;

        Assert.AreNotEqual(originalAction.ActionId, changedAction.ActionId);
        Assert.AreEqual(originalAction.Text, changedAction.Text);
        Assert.AreSame(originalAction.Command, changedAction.Command);
        Assert.AreSame(
            originalAction.CommandParameter,
            changedAction.CommandParameter);
        Assert.AreEqual(originalAction.IsEnabled, changedAction.IsEnabled);
        Assert.AreEqual(originalAction.Visibility, changedAction.Visibility);
        Assert.AreEqual(
            originalAction.AutomationName,
            changedAction.AutomationName);
        Assert.AreEqual(
            originalAction.AutomationHelpText,
            changedAction.AutomationHelpText);
        Assert.AreEqual(originalAction.MinimumWidth, changedAction.MinimumWidth);
        Assert.AreNotEqual(originalKey, changedKey);
    }

    [UITestMethod]
    public async Task AllFourDisclosurePairs_RoundTripInOneLiveSessionAndRetainRealControls()
    {
        ModelInspectionFixtureGalleryPage gallery = ModelInspectionFixtureGalleryTestHarness.CreateGallery();
        Microsoft.UI.Xaml.Window window = await ModelInspectionFixtureGalleryTestHarness.ShowAndLoadWindowAsync(gallery);
        try
        {
            foreach ((string collapsed, string expanded) pair in new[]
                     { ("MI-002", "MI-003"), ("MI-004", "MI-005"), ("MI-006", "MI-007"), ("MI-010", "MI-011") })
            {
                await SelectThroughRealListAsync(gallery, pair.collapsed);
                ModelInspectionFixtureHostPage host = gallery.ActiveHost!;
                ModelInspectionPage page = host.ModelInspectionPage!;
                object disclosureRows = ActiveDisclosureRows(page, pair.collapsed);
                object card = page.FindName("InspectionContentCardControl");
                int live = ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(page);
                Button toggle = ModelInspectionFixtureGalleryTestHarness.ActiveDisclosureToggle(page);
                using (IModelInspectionFixtureObservationSession expandedObservation =
                       new ModelInspectionFixtureScreenObserver().Begin(page))
                {
                    ModelInspectionFixtureGalleryTestHarness.Invoke(toggle);
                    await ModelInspectionFixtureGalleryTestHarness
                        .WaitForInteractionSettlementAsync(
                            page,
                            host.Session,
                            ModelInspectionFixtureTestCatalogue.Get(pair.expanded)
                                .Expected.Figma.State);
                    ModelInspectionObservedScreen expanded = await expandedObservation
                        .CaptureAsync(CancellationToken.None);
                    ModelInspectionFixtureGalleryTestHarness.AssertExactScreen(gallery,
                        ModelInspectionFixtureTestCatalogue.Get(pair.expanded), expanded,
                        Case(pair.collapsed, "expand"),
                        journeyRelativeAnnouncements: true);
                }
                Assert.AreSame(toggle,
                    ModelInspectionFixtureGalleryTestHarness.ActiveDisclosureToggle(page),
                    pair.expanded);
                Assert.AreEqual(live,
                    ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(page),
                    Case(pair.collapsed, "expand"));
                using (IModelInspectionFixtureObservationSession collapsedObservation =
                       new ModelInspectionFixtureScreenObserver().Begin(page))
                {
                    ModelInspectionFixtureGalleryTestHarness.Invoke(toggle);
                    await ModelInspectionFixtureGalleryTestHarness
                        .WaitForInteractionSettlementAsync(
                            page,
                            host.Session,
                            ModelInspectionFixtureTestCatalogue.Get(pair.collapsed)
                                .Expected.Figma.State);
                    ModelInspectionObservedScreen collapsed = await collapsedObservation
                        .CaptureAsync(CancellationToken.None);
                    ModelInspectionFixtureGalleryTestHarness.AssertExactScreen(gallery,
                        ModelInspectionFixtureTestCatalogue.Get(pair.collapsed), collapsed,
                        Case(pair.expanded, "collapse"),
                        journeyRelativeAnnouncements: true);
                }
                Assert.AreSame(host, gallery.ActiveHost, pair.collapsed);
                Assert.AreSame(page, gallery.ActiveHost!.ModelInspectionPage, pair.collapsed);
                Assert.AreSame(
                    disclosureRows,
                    ActiveDisclosureRows(page, pair.collapsed),
                    pair.collapsed);
                Assert.AreSame(card, page.FindName("InspectionContentCardControl"), pair.collapsed);
                Assert.AreEqual(live, ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(page), pair.collapsed);
                Assert.AreEqual(0, host.Session.Evidence.PageRetirementCount, pair.collapsed);
            }
        }
        finally { gallery.CloseForTesting(); window.Content = null; window.Close(); }
    }

    [TestMethod]
    public void GalleryClosure_UsesExpectedDtosOnlyInTheScreenComparer()
    {
        new ModelInspectionFixtureGalleryTests().DebugGallerySourceAndIlClosure_ForbidExternalComposition();
    }

    private static object ActiveDisclosureRows(
        ModelInspectionPage page,
        string diagnostic)
    {
        var model = (InspectionModelCard)page.FindName("InspectionModelCardControl");
        var content = (InspectionContentCard)page.FindName("InspectionContentCardControl");
        bool modelDisclosureActive = model.ActiveDisclosure is not null;
        bool contentDisclosureActive = content.ActiveDisclosure is not null;

        Assert.IsTrue(
            modelDisclosureActive ^ contentDisclosureActive,
            $"Exactly one rendered disclosure must be active. {diagnostic}");
        return modelDisclosureActive
            ? model.InspectionChecks
            : content.ExpandedItems;
    }

    private static bool IsImmediateServiceInteraction(
        ModelInspectionFixtureInteraction interaction) =>
        interaction.Kind == ModelInspectionFixtureInteractionKind.Cancel ||
        (interaction.Kind is ModelInspectionFixtureInteractionKind.Retry or
            ModelInspectionFixtureInteractionKind.Restart &&
         !interaction.Target.StartsWith(
             "MI-", System.StringComparison.Ordinal));

    private static int ExpectedJourneyAnnouncementDelta(
        ModelInspectionFixtureInteraction interaction) =>
        interaction.ExpectedAnnouncementCount +
        (interaction.Kind is ModelInspectionFixtureInteractionKind.Retry or
            ModelInspectionFixtureInteractionKind.Restart
                ? 1
                : 0);

    private static void AssertImmediateTargetCheckpoint(
        ModelInspectionFixtureSession session,
        ModelInspectionFixtureInteraction interaction,
        string diagnostic)
    {
        if (interaction.Target.StartsWith(
                "MI-", System.StringComparison.Ordinal))
        {
            return;
        }

        ModelInspectionFixtureServiceCallEvidence latest =
            session.Evidence.ServiceCalls.Last();
        ModelInspectionFixtureAttemptPlan attempt = session.Plan.Attempts
            .Single(candidate => candidate.Attempt == latest.Attempt);
        string[] unreleased = attempt.ServiceSteps
            .Where(step =>
                step.TriggerKind ==
                    ModelInspectionFixtureServiceTriggerKind.Checkpoint &&
                !session.Evidence.ReleasedServiceCheckpoints.Any(released =>
                    released.Attempt == latest.Attempt &&
                    string.Equals(
                        released.Checkpoint,
                        step.Checkpoint,
                        System.StringComparison.Ordinal)))
            .Select(step => step.Checkpoint!)
            .ToArray();

        Assert.AreEqual(1, unreleased.Length,
            $"The immediate interaction must own one next checkpoint. {diagnostic}");
        Assert.AreEqual(interaction.Target, unreleased[0], diagnostic);
    }

    private static Button RenderedPageAction(
        ModelInspectionPage page,
        string actionId,
        string diagnostic)
    {
        var card = (InspectionActionCard)page.FindName(
            "InspectionActionCardControl");
        Button[] rendered = new[]
            {
                "CancelActionButton", "SecondaryActionOneButton",
                "SecondaryActionTwoButton", "PrimaryActionButton"
            }
            .Select(name => (Button)card.FindName(name))
            .Where(button =>
                button.Visibility == Visibility.Visible &&
                string.Equals(
                    button.Tag as string,
                    actionId,
                    System.StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(1, rendered.Length, diagnostic);
        return rendered[0];
    }

    private static async Task SelectThroughRealListAsync(ModelInspectionFixtureGalleryPage gallery, string id)
    {
        await gallery.SelectFixtureThroughRealListForTestingAsync(id);
    }

    private static string Case(string fixtureId, string actionId) =>
        $"fixture={fixtureId}; action={actionId}";
}

internal sealed class ModelInspectionFixtureNoMutationSnapshot
{
    private readonly ModelInspectionFixtureListItem? selectedItem;
    private readonly ModelInspectionFixtureHostPage host;
    private readonly ModelInspectionFixtureSession session;
    private readonly DebugModelInspectionService service;
    private readonly ModelInspectionFixtureSessionEvidence evidence;
    private readonly ModelInspectionPage page;
    private readonly object presentation;
    private readonly InspectionProgressRows progressRows;
    private readonly string progressSummary;
    private readonly ProgressItemSnapshot[] progressItems;
    private readonly object contentCard;
    private readonly object? focus;
    private readonly InspectionFooterStatus footer;
    private readonly int liveNotificationCount;
    private readonly int startedAttemptCount;
    private readonly int pendingCallCount;
    private readonly EvidenceSnapshot evidenceSnapshot;

    private ModelInspectionFixtureNoMutationSnapshot(
        ModelInspectionFixtureGalleryPage gallery)
    {
        selectedItem = gallery.ViewModel.SelectedItem;
        host = gallery.ActiveHost ?? throw new AssertFailedException(
            "A no-mutation snapshot requires an active fixture host.");
        session = host.Session;
        service = session.Service;
        evidence = session.Evidence;
        page = host.ModelInspectionPage ?? throw new AssertFailedException(
            "A no-mutation snapshot requires an active Model Inspection page.");
        var current = page.CurrentPresentation ??
            throw new AssertFailedException(
                "A no-mutation snapshot requires a current presentation.");
        presentation = current;
        progressRows = current.ContentCard.ProgressRows;
        progressSummary = progressRows.ProgressSummary;
        progressItems = progressRows.Items.Select(
            static item => new ProgressItemSnapshot(item)).ToArray();
        contentCard = page.FindName("InspectionContentCardControl") ??
            throw new AssertFailedException(
                "The rendered content card must exist.");
        focus = FocusManager.GetFocusedElement(page.XamlRoot);
        footer = page.CurrentFooterStatus;
        liveNotificationCount =
            ModelInspectionFixtureGalleryTestHarness.LiveNotificationCount(page);
        startedAttemptCount = service.StartedAttemptCount;
        pendingCallCount = service.PendingCallCount;
        evidenceSnapshot = new EvidenceSnapshot(evidence);
    }

    internal static ModelInspectionFixtureNoMutationSnapshot Capture(
        ModelInspectionFixtureGalleryPage gallery) => new(gallery);

    internal void AssertUnchanged(
        ModelInspectionFixtureGalleryPage gallery,
        string diagnostic)
    {
        var actual = new ModelInspectionFixtureNoMutationSnapshot(gallery);
        Assert.AreSame(selectedItem, actual.selectedItem, diagnostic);
        Assert.AreSame(host, actual.host, diagnostic);
        Assert.AreSame(session, actual.session, diagnostic);
        Assert.AreSame(service, actual.service, diagnostic);
        Assert.AreSame(evidence, actual.evidence, diagnostic);
        Assert.AreSame(page, actual.page, diagnostic);
        Assert.AreSame(presentation, actual.presentation, diagnostic);
        Assert.AreSame(progressRows, actual.progressRows, diagnostic);
        Assert.AreEqual(progressSummary, actual.progressSummary, diagnostic);
        Assert.AreEqual(progressItems.Length, actual.progressItems.Length,
            diagnostic);
        for (int index = 0; index < progressItems.Length; index++)
        {
            Assert.AreSame(
                progressItems[index].Item,
                actual.progressItems[index].Item,
                $"{diagnostic}; progress-item={index}");
            Assert.AreEqual(
                progressItems[index],
                actual.progressItems[index],
                $"{diagnostic}; progress-item={index}");
        }

        Assert.AreSame(contentCard, actual.contentCard, diagnostic);
        Assert.AreSame(focus, actual.focus, diagnostic);
        Assert.AreEqual(footer, actual.footer, diagnostic);
        Assert.AreEqual(
            liveNotificationCount,
            actual.liveNotificationCount,
            diagnostic);
        Assert.AreEqual(
            startedAttemptCount,
            actual.startedAttemptCount,
            $"{diagnostic}; service.StartedAttemptCount");
        Assert.AreEqual(
            pendingCallCount,
            actual.pendingCallCount,
            $"{diagnostic}; service.PendingCallCount");
        Assert.AreEqual(
            evidenceSnapshot,
            actual.evidenceSnapshot,
            $"{diagnostic}; evidence-audit-counts");
    }

    private sealed record ProgressItemSnapshot(
        InspectionContentItemPresentation Item,
        string StageNumber,
        string Title,
        string DefaultDetail,
        string Detail,
        string AutomationHelpText,
        Visibility DetailVisibility,
        InspectionContentStatus Status,
        string StatusText,
        bool IsActive,
        double? StageFraction,
        bool ShowConnector,
        string AutomationName)
    {
        internal ProgressItemSnapshot(InspectionContentItemPresentation item)
            : this(
                item,
                item.StageNumber,
                item.Title,
                item.DefaultDetail,
                item.Detail,
                item.AutomationHelpText,
                item.DetailVisibility,
                item.Status,
                item.StatusText,
                item.IsActive,
                item.StageFraction,
                item.ShowConnector,
                item.AutomationName)
        {
        }
    }

    private sealed record EvidenceSnapshot(
        int ServiceCallCount,
        int CancellationObservationCount,
        int ActiveCancellationRegistrationCount,
        int CancellationRegistrationCreatedCount,
        int CancellationRegistrationReleasedCount,
        int ReleasedDeferredProgressCount,
        int ReleasedDeferredResultSnapshotCount,
        int ReleasedDeferredMotionCount,
        int ReleasedDeferredAnnouncementCount,
        int TerminalCompletionCount,
        int RetiredPendingCallCount,
        int ServiceRetirementCount,
        int ServiceDisposalCount,
        int SessionRetirementCount,
        int SessionDisposalCount,
        int AnimationStartCount,
        int AnimationCancellationCount,
        int AnimationDriverDisposalCount,
        int MotionSettingsDisposalCount,
        int HostActivationCount,
        int PageRetirementCount,
        int DispatcherCallbackCount,
        int CompletedDispatcherCallbackCount,
        int PendingDispatcherCallbackCount,
        int MotionBatchCount,
        int CompletedMotionBatchCount,
        int PendingMotionBatchCount,
        int FocusRequestCount,
        int CompletedFocusRequestCount,
        int PendingFocusRequestCount,
        int DisclosureOperationCount,
        int CompletedDisclosureOperationCount,
        int PendingDisclosureOperationCount,
        int LiveNotificationCount,
        int CompletedLiveNotificationCount,
        int PendingLiveNotificationCount)
    {
        internal EvidenceSnapshot(ModelInspectionFixtureSessionEvidence evidence)
            : this(
                evidence.ServiceCallCount,
                evidence.CancellationObservationCount,
                evidence.ActiveCancellationRegistrationCount,
                evidence.CancellationRegistrationCreatedCount,
                evidence.CancellationRegistrationReleasedCount,
                evidence.ReleasedDeferredProgressCount,
                evidence.ReleasedDeferredResultSnapshotCount,
                evidence.ReleasedDeferredMotionCount,
                evidence.ReleasedDeferredAnnouncementCount,
                evidence.TerminalCompletionCount,
                evidence.RetiredPendingCallCount,
                evidence.ServiceRetirementCount,
                evidence.ServiceDisposalCount,
                evidence.SessionRetirementCount,
                evidence.SessionDisposalCount,
                evidence.AnimationStartCount,
                evidence.AnimationCancellationCount,
                evidence.AnimationDriverDisposalCount,
                evidence.MotionSettingsDisposalCount,
                evidence.HostActivationCount,
                evidence.PageRetirementCount,
                evidence.DispatcherCallbackCount,
                evidence.CompletedDispatcherCallbackCount,
                evidence.PendingDispatcherCallbackCount,
                evidence.MotionBatchCount,
                evidence.CompletedMotionBatchCount,
                evidence.PendingMotionBatchCount,
                evidence.FocusRequestCount,
                evidence.CompletedFocusRequestCount,
                evidence.PendingFocusRequestCount,
                evidence.DisclosureOperationCount,
                evidence.CompletedDisclosureOperationCount,
                evidence.PendingDisclosureOperationCount,
                evidence.LiveNotificationCount,
                evidence.CompletedLiveNotificationCount,
                evidence.PendingLiveNotificationCount)
        {
        }
    }
}

[DoNotParallelize]
internal static class ModelInspectionFixtureGalleryTestHarness
{
    internal static ModelInspectionFixtureGalleryPage CreateGallery() => CreateGallery(CreateCountingReader());
    internal static ModelInspectionFixtureGalleryPage CreateGallery(ModelInspectionFixtureGalleryTests.CountingReader reader) => new(new ModelInspectionFixturePackageLoader(reader), static () => { });
    internal static ModelInspectionFixtureGalleryTests.CountingReader CreateCountingReader() => ModelInspectionFixtureGalleryTests.CountingReader.Valid();
    internal static string[] ExpectedPackageUris() => ModelInspectionFixtureGalleryTests.ExpectedPackageUris();
    internal static ValidatedModelInspectionFixture ExpectedTargetFixture(
        ValidatedModelInspectionFixture source,
        ModelInspectionFixtureInteraction interaction)
    {
        string target = interaction.Target.StartsWith("MI-", System.StringComparison.Ordinal)
            ? interaction.Target
            : source.Id;
        return ModelInspectionFixtureTestCatalogue.Get(target);
    }
    internal static void AssertExactScreen(ModelInspectionFixtureGalleryPage gallery,
        ValidatedModelInspectionFixture expected,
        ModelInspectionObservedScreen actual,
        string? diagnostic = null,
        ValidatedModelInspectionFixture? sourceFixture = null,
        ModelInspectionFixtureInteractionKind? interactionKind = null,
        bool journeyRelativeAnnouncements = false)
    {
        ModelInspectionExpectedScreen expectedScreen = expected.Expected;
        if (sourceFixture is not null && interactionKind is (
            ModelInspectionFixtureInteractionKind.Expand or
            ModelInspectionFixtureInteractionKind.Collapse))
        {
            ModelInspectionExpectedAutomationControl[] automationControls =
                expectedScreen.Automation.Controls
                    .Select(control => string.Equals(
                            control.Id,
                            "model-card",
                            System.StringComparison.Ordinal)
                        ? control with
                        {
                            AccessibleName = sourceFixture.Expected.Automation
                                .Controls.Single(sourceControl => string.Equals(
                                    sourceControl.Id,
                                    "model-card",
                                    System.StringComparison.Ordinal))
                                .AccessibleName
                        }
                        : control)
                    .ToArray();
            expectedScreen = expectedScreen with
            {
                Model = expectedScreen.Model with
                {
                    DisplayName = sourceFixture.Expected.Model.DisplayName,
                    DisplayFileName = sourceFixture.Expected.Model.DisplayFileName,
                    Metadata = sourceFixture.Expected.Model.Metadata
                },
                Automation = expectedScreen.Automation with
                {
                    Controls = automationControls
                }
            };
        }

        var comparer = new ModelInspectionFixtureScreenComparer();
        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences = comparer
            .Compare(expected.FileName, expectedScreen, actual,
                gallery.CoverageCatalogueForTesting.Catalogue.Policy.Value.CopyRegistry);
        if (journeyRelativeAnnouncements)
        {
            differences = differences.Where(difference =>
                    !difference.ExpectedPath.StartsWith(
                        "$.Announcements.", System.StringComparison.Ordinal))
                .ToArray();
        }
        Assert.AreEqual(0, differences.Count,
            differences.Count == 0
                ? diagnostic ?? expected.Id
                : $"{diagnostic ?? expected.Id}; {differences[0].Diagnostic}");
    }
    internal static void AssertActualRenderedControl(
        ModelInspectionFixtureGalleryPage gallery,
        ModelInspectionPage page,
        string actionId,
        Button actual,
        string diagnostic)
    {
        Button expected;
        if (string.Equals(actionId, "reset", System.StringComparison.Ordinal))
        {
            expected = (Button)gallery.FindName("ResetFixtureButton");
        }
        else if (actionId is "expand" or "collapse")
        {
            expected = ActiveDisclosureToggle(page);
        }
        else
        {
            string renderedId = RenderedActionId(actionId);
            var card = (InspectionActionCard)page.FindName("InspectionActionCardControl");
            expected = new[]
                {
                    "CancelActionButton", "SecondaryActionOneButton",
                    "SecondaryActionTwoButton", "PrimaryActionButton"
                }
                .Select(name => (Button)card.FindName(name))
                .Single(button => button.Visibility == Visibility.Visible &&
                    string.Equals(button.Tag as string, renderedId,
                        System.StringComparison.Ordinal));
            Assert.AreEqual(renderedId, expected.Tag as string, diagnostic);
            if (expected.IsEnabled)
            {
                Assert.IsNotNull(expected.Command, diagnostic);
            }
        }

        Assert.AreSame(expected, actual, diagnostic);
    }
    private static string RenderedActionId(string actionId) => actionId switch
    {
        "cancel-request" => "cancel",
        "restart-attempt" => "restart",
        "retry-attempt" => "retry",
        _ => actionId
    };
    internal static Button ActiveDisclosureToggle(ModelInspectionPage page)
    {
        var model = (InspectionModelCard)page.FindName("InspectionModelCardControl");
        var content = (InspectionContentCard)page.FindName("InspectionContentCardControl");
        InspectionDisclosure disclosure = new[] { model.ActiveDisclosure, content.ActiveDisclosure }
            .OfType<InspectionDisclosure>().Single();
        return (Button)disclosure.FindName("DisclosureToggleButton");
    }
    internal static void Invoke(Button button)
    {
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
        Assert.IsInstanceOfType<Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider>(
            peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
    }
    internal static async Task WaitForInteractionSettlementAsync(
        ModelInspectionPage page,
        ModelInspectionFixtureSession session,
        ModelInspectionExpectedFigmaState expectedState,
        string diagnostic = "fixture interaction")
    {
        bool activeProgress =
            expectedState == ModelInspectionExpectedFigmaState.InspectionProgress &&
            session.Plan.Attempts.Count > 0;
        try
        {
            if (expectedState ==
                ModelInspectionExpectedFigmaState.InspectionProgress)
            {
                await WaitForUiPendingZeroAsync(page, session);
            }
            else
            {
                await session.Evidence.WaitForPendingZeroAsync().WaitAsync(
                    System.TimeSpan.FromSeconds(5));
            }
        }
        catch (System.TimeoutException exception)
        {
            throw new System.TimeoutException(
                $"{diagnostic}; state={page.CurrentPresentation?.State}; " +
                $"serviceCalls={session.Evidence.ServiceCallCount}; " +
                $"serviceAttempts=[{string.Join(",", session.Evidence.ServiceCalls.Select(call => call.Attempt))}]; " +
                $"servicePending={session.Service.PendingCallCount}; " +
                $"activeCancellation={session.Evidence.ActiveCancellationRegistrationCount}; " +
                $"dispatcher={session.Evidence.PendingDispatcherCallbackCount}; " +
                $"motion={session.Evidence.PendingMotionBatchCount}; " +
                $"focus={session.Evidence.PendingFocusRequestCount}; " +
                $"disclosure={session.Evidence.PendingDisclosureOperationCount}; " +
                $"live={session.Evidence.PendingLiveNotificationCount}; " +
                $"released={session.Evidence.ReleasedServiceCheckpoints.Count}; " +
                $"terminal={session.Evidence.TerminalCompletionCount}",
                exception);
        }
        await DrainAsync(page);
        page.UpdateLayout();

        if (expectedState == ModelInspectionExpectedFigmaState.InspectionProgress)
        {
            int expectedPending = activeProgress ? 1 : 0;
            Assert.AreEqual(
                expectedPending,
                session.Service.PendingCallCount,
                diagnostic);
            Assert.AreEqual(
                expectedPending,
                session.Evidence.ActiveCancellationRegistrationCount,
                diagnostic);
            AssertUiPendingZero(session, diagnostic);
        }

        ModelInspectionPagePresentation presentation =
            page.CurrentPresentation ?? throw new AssertFailedException(
                "The settled fixture page must have a current presentation.");
        Assert.AreEqual(
            System.Enum.Parse<ModelInspectionFigmaState>(
                expectedState.ToString()),
            presentation.State);

        var model = (InspectionModelCard)page.FindName(
            "InspectionModelCardControl");
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        InspectionDisclosure[] disclosures =
            new[] { model.ActiveDisclosure, content.ActiveDisclosure }
                .OfType<InspectionDisclosure>()
                .ToArray();
        bool? expectedExpanded = expectedState switch
        {
            ModelInspectionExpectedFigmaState.ReadyExpanded or
            ModelInspectionExpectedFigmaState.ReadyWithWarningsExpanded or
            ModelInspectionExpectedFigmaState.ConversionRequiredExpanded or
            ModelInspectionExpectedFigmaState.InvalidExpanded => true,
            ModelInspectionExpectedFigmaState.ReadyCollapsed or
            ModelInspectionExpectedFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionExpectedFigmaState.ConversionRequiredCollapsed or
            ModelInspectionExpectedFigmaState.InvalidCollapsed => false,
            _ => null
        };
        Assert.AreEqual(expectedExpanded.HasValue ? 1 : 0, disclosures.Length);
        if (expectedExpanded.HasValue)
        {
            Assert.AreEqual(expectedExpanded.Value, disclosures[0].IsExpanded);
        }
    }
    internal static async Task DrainAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
            () => drained.TrySetResult(true)));
        await drained.Task;
    }
    internal static int LiveNotificationCount(ModelInspectionPage? page) => page is null ? 0 : ((GraniteEdgeAI.Features.ModelInspection.Controls.InspectionContentCard)page.FindName("InspectionContentCardControl")).LiveRegionChangeNotificationCount + ((GraniteEdgeAI.Features.ModelInspection.Controls.InspectionOutcomeCard)page.FindName("InspectionOutcomeCardControl")).LiveRegionChangeNotificationCount;
    internal static ModelInspectionObservedScreen WithCumulativeAnnouncements(
        ModelInspectionObservedScreen observed,
        ModelInspectionPage page)
    {
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");
        IReadOnlyList<string> progress =
            content.LiveRegionAnnouncementHistory;
        IReadOnlyList<string> terminal =
            outcome.LiveRegionAnnouncementHistory;
        return observed with
        {
            Announcements = new ModelInspectionObservedAnnouncements
            {
                Count = progress.Count + terminal.Count,
                Items = progress.Concat(terminal).ToArray()
            }
        };
    }
    internal static string SemanticFocusedTarget(ModelInspectionPage page)
    {
        DependencyObject? focused = page.XamlRoot is null
            ? null
            : FocusManager.GetFocusedElement(page.XamlRoot) as DependencyObject;
        if (focused is null)
        {
            return string.Empty;
        }

        var model = (InspectionModelCard)page.FindName("InspectionModelCardControl");
        if (IsDescendantOrSelf(focused, model))
        {
            return "model-card";
        }

        var card = (InspectionActionCard)page.FindName("InspectionActionCardControl");
        foreach (string name in new[]
                 {
                     "CancelActionButton", "SecondaryActionOneButton",
                     "SecondaryActionTwoButton", "PrimaryActionButton"
                 })
        {
            var button = (Button)card.FindName(name);
            if (IsDescendantOrSelf(focused, button))
            {
                return button.Tag as string ?? string.Empty;
            }
        }

        return string.Empty;
    }
    internal static void AssertAllPendingZero(
        ModelInspectionFixtureSession session,
        string diagnostic)
    {
        Assert.AreEqual(0, session.Service.PendingCallCount, diagnostic);
        AssertUiPendingZero(session, diagnostic);
    }
    internal static void AssertUiPendingZero(
        ModelInspectionFixtureSession session,
        string diagnostic)
    {
        Assert.AreEqual(0, session.Evidence.PendingDispatcherCallbackCount, diagnostic);
        Assert.AreEqual(0, session.Evidence.PendingMotionBatchCount, diagnostic);
        Assert.AreEqual(0, session.Evidence.PendingFocusRequestCount, diagnostic);
        Assert.AreEqual(0, session.Evidence.PendingDisclosureOperationCount, diagnostic);
        Assert.AreEqual(0, session.Evidence.PendingLiveNotificationCount, diagnostic);
    }
    internal static async Task WaitForUiPendingZeroAsync(
        FrameworkElement element,
        ModelInspectionFixtureSession session)
    {
        if (UiPendingCountsAreZero(session))
        {
            return;
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Microsoft.UI.Dispatching.DispatcherQueueTimer timer =
            element.DispatcherQueue.CreateTimer();
        timer.Interval = System.TimeSpan.FromMilliseconds(16);
        timer.Tick += (_, _) =>
        {
            if (UiPendingCountsAreZero(session))
            {
                timer.Stop();
                completion.TrySetResult(true);
            }
        };
        timer.Start();
        try
        {
            await completion.Task.WaitAsync(System.TimeSpan.FromSeconds(5));
        }
        finally
        {
            timer.Stop();
        }
    }
    private static bool UiPendingCountsAreZero(
        ModelInspectionFixtureSession session) =>
        session.Evidence.PendingDispatcherCallbackCount == 0 &&
        session.Evidence.PendingMotionBatchCount == 0 &&
        session.Evidence.PendingFocusRequestCount == 0 &&
        session.Evidence.PendingDisclosureOperationCount == 0 &&
        session.Evidence.PendingLiveNotificationCount == 0;
    private static bool IsDescendantOrSelf(
        DependencyObject element,
        DependencyObject owner)
    {
        DependencyObject? cursor = element;
        while (cursor is not null)
        {
            if (ReferenceEquals(cursor, owner))
            {
                return true;
            }

            cursor = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(cursor);
        }

        return false;
    }
    internal static async Task<Microsoft.UI.Xaml.Window> ShowAndLoadWindowAsync(ModelInspectionFixtureGalleryPage gallery)
    {
        var window = new Microsoft.UI.Xaml.Window { Content = gallery };
        window.Activate(); await gallery.CatalogueLoaded;
        var drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(gallery.DispatcherQueue.TryEnqueue(() => drained.TrySetResult(true)));
        await drained.Task; return window;
    }
}
#endif
