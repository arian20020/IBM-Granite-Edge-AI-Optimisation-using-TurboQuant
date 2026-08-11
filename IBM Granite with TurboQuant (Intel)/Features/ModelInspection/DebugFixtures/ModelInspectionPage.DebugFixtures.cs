#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using System;

namespace GraniteEdgeAI.Features.ModelInspection;

public sealed partial class ModelInspectionPage
{
    private ModelInspectionFixtureSession? _fixtureSession;
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
            CreateProductionDispatcher,
            session.CreateAnimationDriver,
            session.CreateMotionSettings,
            startInspectionOnLoaded,
            configureResourcesBeforeInitialize);
        page._fixtureSession = session;
        page.ActivateRequest(session.Request);
        return page;
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
}
#endif
