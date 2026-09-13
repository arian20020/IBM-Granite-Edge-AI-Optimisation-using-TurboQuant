#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using System;

namespace GraniteEdgeAI.Features.ModelInspection;

public sealed partial class ModelInspectionPage
{
    private ModelInspectionFixtureSession? _fixtureSession;
    private bool _fixtureResponsiveTriggersSuppressed;
    private bool _staleMotionCallbackCaptured;
    private bool _staleAnnouncementCallbackCaptured;

    internal static ModelInspectionPage CreateForFixture(
        ModelInspectionFixtureSession session,
        bool startInspectionOnLoaded,
        Action<ResourceDictionary>? configureResourcesBeforeInitialize = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var page = new ModelInspectionPage(
            session.Service,
            session.CreateRenderDispatcher,
            session.CreateStartupPresentationBarrier,
            session.CreateAnimationDriver,
            session.CreateMotionSettings,
            session.CreateMilestoneScheduler,
            startInspectionOnLoaded,
            configureResourcesBeforeInitialize);
        page._fixtureSession = session;
        page.ActivateRequest(session.Request);
        return page;
    }

    internal string? FixturePageResponsiveStateName =>
        CurrentFixturePageResponsiveStateName();

    internal string? FixtureModelResponsiveStateName =>
        InspectionModelCardControl.FixtureResponsiveStateName;

    internal string? FixtureContentResponsiveStateName =>
        InspectionContentCardControl.FixtureResponsiveStateName;

    internal string? FixtureOutgoingContentResponsiveStateName =>
        ActivePreview.ResponsiveStateName;

    internal string? FixtureActionResponsiveStateName =>
        InspectionActionCardControl.FixtureResponsiveStateName;

    internal void ApplyFixtureResponsiveState(
        ModelInspectionFixtureWidthProfile width)
    {
        string pageState = width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 =>
                "DesktopPageState",
            ModelInspectionFixtureWidthProfile.Medium600 => "MediumPageState",
            ModelInspectionFixtureWidthProfile.Narrow360 => "NarrowPageState",
            _ => throw new ArgumentOutOfRangeException(
                nameof(width), width, "Unknown fixture width profile.")
        };

        SuppressFixtureResponsiveTriggers();
        if (!string.Equals(
                CurrentFixturePageResponsiveStateName(),
                pageState,
                StringComparison.Ordinal) &&
            !VisualStateManager.GoToState(this, pageState, false))
        {
            throw new InvalidOperationException(
                $"The page visual state '{pageState}' was not found.");
        }

        double responsiveWidth = width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => 1440d,
            ModelInspectionFixtureWidthProfile.Medium600 => 600d,
            ModelInspectionFixtureWidthProfile.Narrow360 => 360d,
            _ => throw new ArgumentOutOfRangeException(nameof(width))
        };
        InspectionModelCardControl.ApplyFixtureResponsiveState(responsiveWidth);
        InspectionContentCardControl.ApplyFixtureResponsiveState(responsiveWidth);
        InspectionActionCardControl.ApplyFixtureResponsiveState(responsiveWidth);
    }

    private void SuppressFixtureResponsiveTriggers()
    {
        if (_fixtureResponsiveTriggersSuppressed)
        {
            return;
        }

        foreach (VisualStateGroup group in
                 VisualStateManager.GetVisualStateGroups(LayoutRoot))
        {
            if (!string.Equals(
                    group.Name,
                    "ResponsivePageStates",
                    StringComparison.Ordinal))
            {
                continue;
            }

            foreach (VisualState state in group.States)
            {
                state.StateTriggers.Clear();
            }

            _fixtureResponsiveTriggersSuppressed = true;
            return;
        }

        throw new InvalidOperationException(
            "The page responsive visual-state group was not found.");
    }

    private string? CurrentFixturePageResponsiveStateName()
    {
        foreach (VisualStateGroup group in
                 VisualStateManager.GetVisualStateGroups(LayoutRoot))
        {
            if (string.Equals(
                    group.Name,
                    "ResponsivePageStates",
                    StringComparison.Ordinal))
            {
                return group.CurrentState?.Name;
            }
        }

        throw new InvalidOperationException(
            "The page responsive visual-state group was not found.");
    }

    internal bool RetireForFixture() => RetirePageLifetime();

    internal ModelInspectionRenderKey CaptureStaleResultSnapshotForFixture(
        int ownerAttempt,
        string captureCheckpoint)
    {
        ModelInspectionFixtureSession session = _fixtureSession ??
            throw new InvalidOperationException(
                "Only an active fixture page can capture a stale snapshot.");
        ModelInspectionRenderCoordinator coordinator = _coordinator ??
            throw new InvalidOperationException(
                "The fixture page has no active render coordinator.");
        ModelInspectionViewSnapshot snapshot = ViewModel?.Snapshot ??
            throw new InvalidOperationException(
                "The fixture page has no active view snapshot.");
        if (snapshot.TerminalResult is null)
        {
            throw new InvalidOperationException(
                "Only a terminal fixture snapshot can be captured as a stale result.");
        }

        if (snapshot.RenderKey.AttemptGeneration != ownerAttempt)
        {
            throw new InvalidOperationException(
                "The stale snapshot does not belong to the declared attempt.");
        }

        long navigationLifetime = _navigationLifetime;
        session.CaptureStaleResultSnapshot(
            ownerAttempt,
            captureCheckpoint,
            () =>
            {
                if (navigationLifetime == _navigationLifetime &&
                    ReferenceEquals(_coordinator, coordinator))
                {
                    coordinator.RequestRender(snapshot);
                }
            });
        return snapshot.RenderKey;
    }

    internal bool SubmitStaleResultSnapshotForFixture(
        int ownerAttempt,
        string releaseCheckpoint) =>
        _hasActiveLifetime &&
        _fixtureSession is ModelInspectionFixtureSession session &&
        session.ReleaseStaleResultSnapshot(ownerAttempt, releaseCheckpoint);

    internal bool ReleaseStaleMotionForFixture(
        int ownerAttempt,
        string releaseCheckpoint) =>
        _hasActiveLifetime &&
        _fixtureSession is ModelInspectionFixtureSession session &&
        session.ReleaseStaleMotionCallback(ownerAttempt, releaseCheckpoint);

    internal bool ReleaseStaleAnnouncementForFixture(
        int ownerAttempt,
        string releaseCheckpoint) =>
        _hasActiveLifetime &&
        _fixtureSession is ModelInspectionFixtureSession session &&
        session.ReleaseStaleAnnouncementCallback(ownerAttempt, releaseCheckpoint);

    internal bool ReleaseTypedStaleEventForFixture(
        string fixtureId,
        int ownerAttempt,
        string releaseCheckpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureId);
        return fixtureId switch
        {
            "MI-033" => _hasActiveLifetime &&
                _fixtureSession is ModelInspectionFixtureSession progress &&
                progress.ReleaseStaleProgress(ownerAttempt, releaseCheckpoint),
            "MI-034" => SubmitStaleResultSnapshotForFixture(
                ownerAttempt, releaseCheckpoint),
            "MI-035" => ReleaseStaleMotionForFixture(
                ownerAttempt, releaseCheckpoint),
            "MI-036" => ReleaseStaleAnnouncementForFixture(
                ownerAttempt, releaseCheckpoint),
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureId),
                fixtureId, "Unknown typed stale fixture route.")
        };
    }

    partial void CaptureStaleMotionCallbackForFixture(
        ModelInspectionVisualOperationKey operationKey,
        Action<ModelInspectionVisualOperationKey> completed)
    {
        ModelInspectionFixtureSession? session = _fixtureSession;
        if (_staleMotionCallbackCaptured || session is null)
        {
            return;
        }

        foreach (ModelInspectionFixtureStaleMotionCallbackHandle handle in
                 session.StaleMotionCallbackHandles)
        {
            if (!handle.IsCaptured ||
                operationKey.RenderKey.AttemptGeneration != handle.OwnerAttempt)
            {
                continue;
            }

            session.CaptureStaleMotionCallback(
                handle.OwnerAttempt,
                handle.CaptureCheckpoint,
                () => completed(operationKey));
            _staleMotionCallbackCaptured = true;
            return;
        }
    }

    partial void CaptureStaleAnnouncementCallbackForFixture(
        ModelInspectionRenderKey renderKey,
        Action callback)
    {
        ModelInspectionFixtureSession? session = _fixtureSession;
        if (_staleAnnouncementCallbackCaptured || session is null)
        {
            return;
        }

        foreach (ModelInspectionFixtureStaleAnnouncementCallbackHandle handle in
                 session.StaleAnnouncementCallbackHandles)
        {
            if (!handle.IsCaptured ||
                renderKey.AttemptGeneration != handle.OwnerAttempt)
            {
                continue;
            }

            session.CaptureStaleAnnouncementCallback(
                handle.OwnerAttempt,
                handle.CaptureCheckpoint,
                callback);
            _staleAnnouncementCallbackCaptured = true;
            return;
        }
    }

    partial void BeginDispatcherAuditForFixture(ref IDisposable? audit) =>
        audit = _fixtureSession?.BeginPageDispatcherCallback();

    partial void BeginFocusAuditForFixture(ref IDisposable? audit) =>
        audit = _fixtureSession?.Evidence.BeginFocusRequest();

    partial void BeginDisclosureAuditForFixture(ref IDisposable? audit) =>
        audit = _fixtureSession?.Evidence.BeginDisclosureOperation();

    partial void BeginLiveNotificationAuditForFixture(ref IDisposable? audit) =>
        audit = _fixtureSession?.Evidence.BeginLiveNotification();

    partial void CompleteDispatcherAuditsForFixture() =>
        _fixtureSession?.ClosePageDispatcherCallbacks();
}
#endif
