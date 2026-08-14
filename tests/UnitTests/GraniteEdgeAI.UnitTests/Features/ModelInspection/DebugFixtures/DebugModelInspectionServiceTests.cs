#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class DebugModelInspectionServiceTests
{
    [ThreadStatic]
    private static bool releasingCheckpoint;

    [TestMethod]
    public async Task Service_ReleasesOnlyDeclaredStepsAndRetiresPendingCalls()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-004");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);
        RecordingProgress progress = new();

        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            progress,
            CancellationToken.None);

        Assert.AreEqual(3, progress.Values.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                ModelInspectionStage.ValidateModelStructure,
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility
            },
            progress.Values.Select(value => value.Stage).ToArray());
        Assert.IsFalse(run.IsCompleted);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            Task.Run(() => service.ReleaseServiceCheckpoint(1, "wrong")));
        Assert.IsFalse(run.IsCompleted);

        bool continuationRanOnReleaseThread = false;
        Task continuation = run.ContinueWith(
            _ => continuationRanOnReleaseThread = releasingCheckpoint,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        releasingCheckpoint = true;
        service.ReleaseServiceCheckpoint(1, "terminal");
        releasingCheckpoint = false;

        ModelInspectionExecutionResult result = await run;
        await continuation;
        Assert.AreEqual(ModelInspectionExecutionStatus.Completed, result.Status);
        Assert.AreEqual(
            ModelInspectionOutcome.ReadyWithWarnings,
            result.Result?.Outcome);
        Assert.IsFalse(
            continuationRanOnReleaseThread,
            "The result continuation ran inline with checkpoint release.");
        Assert.AreEqual(1, evidence.ServiceCallCount);
        Assert.AreEqual(1, evidence.ReleasedServiceCheckpoints.Count);
        Assert.AreEqual(1,
            evidence.ReleasedServiceCheckpoints[0].Attempt);
        Assert.AreEqual("terminal",
            evidence.ReleasedServiceCheckpoints[0].Checkpoint);
    }

    [TestMethod]
    public async Task CheckpointRelease_EmitsAutomaticPrefixAndSuffix()
    {
        ModelInspectionFixtureExecutionPlan source = Plan("MI-004");
        IReadOnlyList<ModelInspectionFixtureServiceStepPlan> sourceSteps =
            source.Attempts[0].ServiceSteps;
        ModelInspectionFixtureExecutionPlan plan = source with
        {
            Attempts =
            [
                new ModelInspectionFixtureAttemptPlan(
                    1,
                    [
                        sourceSteps[0],
                        sourceSteps[1] with
                        {
                            TriggerKind =
                                ModelInspectionFixtureServiceTriggerKind.Checkpoint,
                            Checkpoint = "middle"
                        },
                        sourceSteps[^1] with
                        {
                            TriggerKind =
                                ModelInspectionFixtureServiceTriggerKind.Automatic,
                            Checkpoint = null
                        }
                    ])
            ]
        };
        using DebugModelInspectionService service = new(
            plan,
            new ModelInspectionFixtureSessionEvidence());
        RecordingProgress progress = new();

        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            progress,
            CancellationToken.None);

        Assert.AreEqual(1, progress.Values.Count);
        Assert.IsFalse(run.IsCompleted);
        service.ReleaseServiceCheckpoint(1, "middle");

        Assert.AreEqual(2, progress.Values.Count);
        Assert.AreEqual(
            ModelInspectionExecutionStatus.Completed,
            (await run).Status);
    }

    [TestMethod]
    public async Task Service_BindsCallsByExactRequestAndAttemptAndRejectsExcess()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-013");
        ModelInspectionFixtureExecutionPlan foreignPlan = Plan("MI-002");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);
        using CancellationTokenSource firstCancellation = new();
        using CancellationTokenSource secondCancellation = new();

        Task<ModelInspectionExecutionResult> first = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            firstCancellation.Token);
        Assert.AreEqual(plan.Request, foreignPlan.Request);
        Assert.AreNotSame(plan.Request, foreignPlan.Request);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.InspectAsync(
                foreignPlan.Request,
                new RecordingProgress(),
                secondCancellation.Token));
        Assert.AreEqual(1, evidence.ServiceCallCount);

        service.ReleaseServiceCheckpoint(1, "terminal");
        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            (await first).Status);

        Task<ModelInspectionExecutionResult> second = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            secondCancellation.Token);
        Assert.AreNotEqual(
            firstCancellation.Token,
            secondCancellation.Token);
        service.ReleaseServiceCheckpoint(2, "attempt-2-ready");
        Assert.AreEqual(
            ModelInspectionOutcome.Ready,
            (await second).Result?.Outcome);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.InspectAsync(
                plan.Request,
                new RecordingProgress(),
                CancellationToken.None));
        Assert.AreEqual(2, evidence.ServiceCallCount);
        CollectionAssert.AreEqual(
            new[] { 1, 2 },
            evidence.ServiceCalls.Select(call => call.Attempt).ToArray());
        Assert.IsTrue(evidence.ServiceCalls.All(call =>
            ReferenceEquals(plan.Request, call.Request)));
    }

    [DataTestMethod]
    [DataRow("MI-012")]
    [DataRow("MI-029")]
    public async Task Cancellation_IsObservedButWaitsForDeclaredCooperativeResult(
        string id)
    {
        ModelInspectionFixtureExecutionPlan plan = Plan(id);
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);
        using CancellationTokenSource cancellation = new();
        RecordingProgress progress = new();
        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            progress,
            cancellation.Token);
        service.ReleaseServiceCheckpoint(1, "progress");

        cancellation.Cancel();

        Assert.IsFalse(run.IsCompleted);
        Assert.AreEqual(1, evidence.CancellationObservationCount);
        CollectionAssert.AreEqual(
            new[] { 1 },
            evidence.CancellationObservedAttempts.ToArray());
        service.ReleaseServiceCheckpoint(1, "cancelled");

        ModelInspectionExecutionResult result = await run;
        Assert.AreEqual(ModelInspectionExecutionStatus.Cancelled, result.Status);
        Assert.AreEqual(true, result.CancellationWasCooperative);
        Assert.IsFalse(run.IsCanceled);
        Assert.IsFalse(run.IsFaulted);
    }

    [TestMethod]
    public async Task CancellationUnconfirmed_IsReleasedAsFixedOperationalFailure()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-030");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            cancellation.Token);
        service.ReleaseServiceCheckpoint(1, "progress");

        cancellation.Cancel();

        Assert.IsFalse(run.IsCompleted);
        Assert.AreEqual(1, evidence.CancellationObservationCount);
        CollectionAssert.AreEqual(
            new[] { 1 },
            evidence.CancellationObservedAttempts.ToArray());
        service.ReleaseServiceCheckpoint(1, "cancellation-unconfirmed");
        ModelInspectionExecutionResult result = await run;
        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            result.Status);
        Assert.AreEqual(
            "MI-OP-CANCELLATION-UNCONFIRMED",
            result.Failure?.Code);
    }

    [DataTestMethod]
    [DataRow("MI-027", "cancelled-progress", "terminal",
        (int)ModelInspectionExecutionStatus.Cancelled)]
    [DataRow("MI-042", null, "terminal",
        (int)ModelInspectionExecutionStatus.OperationalFailure)]
    public async Task DeclaredCancellationTerminal_DoesNotRequireTokenObservation(
        string id,
        string? progressCheckpoint,
        string terminalCheckpoint,
        int expectedStatus)
    {
        ModelInspectionFixtureExecutionPlan plan = Plan(id);
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);
        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            CancellationToken.None);

        if (progressCheckpoint is not null)
        {
            service.ReleaseServiceCheckpoint(1, progressCheckpoint);
        }

        service.ReleaseServiceCheckpoint(1, terminalCheckpoint);
        Assert.AreEqual((ModelInspectionExecutionStatus)expectedStatus,
            (await run).Status);
        Assert.AreEqual(0, evidence.CancellationObservationCount);
    }

    [TestMethod]
    public async Task CancellationRegistration_IsReleasedAfterTerminalResult()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-002");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            cancellation.Token);

        service.ReleaseServiceCheckpoint(1, "terminal");
        await run;
        cancellation.Cancel();

        Assert.AreEqual(0, evidence.CancellationObservationCount);
        Assert.AreEqual(0, evidence.ActiveCancellationRegistrationCount);
    }

    [TestMethod]
    public async Task DeferredProgress_RetainsOnlyItsDeclaredOldReporter()
    {
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-033").Input);
        RecordingProgress firstProgress = new();
        Task<ModelInspectionExecutionResult> first =
            session.Service.InspectAsync(
            session.Request,
            firstProgress,
            CancellationToken.None);

        session.Service.ReleaseServiceCheckpoint(1, "capture-old-progress");
        Assert.AreEqual(0, firstProgress.Values.Count);
        Assert.AreEqual(1, session.Evidence.CapturedDeferredEvents.Count);
        Assert.AreEqual(
            ModelInspectionFixtureDeferredEventKind.StaleProgress,
            session.Evidence.CapturedDeferredEvents[0].Kind);
        session.Service.ReleaseServiceCheckpoint(1, "failure");
        await first;

        Task<ModelInspectionExecutionResult> second =
            session.Service.InspectAsync(
            session.Request,
            new RecordingProgress(),
            CancellationToken.None);
        Assert.IsTrue(session.ReleaseStaleProgress(1, "old-progress"));
        Assert.AreEqual(1, firstProgress.Values.Count);
        Assert.AreEqual(
            ModelInspectionStage.ReadModelConfiguration,
            firstProgress.Values[0].Stage);
        Assert.AreEqual(1, session.Evidence.ReleasedDeferredProgressCount);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ReleaseStaleProgress(1, "old-progress"));

        session.Service.ReleaseServiceCheckpoint(2, "ready");
        await second;

        using ModelInspectionFixtureSession undeclared = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-032").Input);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            undeclared.ReleaseStaleProgress(1, "old-progress"));
    }

    [TestMethod]
    public async Task DeferredProgress_ReleaseAfterRetirementIsSilentNoOp()
    {
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-033").Input);
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
            session.Request,
            new RecordingProgress(),
            CancellationToken.None);
        session.Service.ReleaseServiceCheckpoint(1, "capture-old-progress");

        session.Retire();

        Assert.IsFalse(session.ReleaseStaleProgress(1, "old-progress"));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
    }

    [TestMethod]
    public async Task StaleProgressHandle_RequiresExactOwnerAndLaterAttempt()
    {
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-033").Input);
        Assert.AreEqual(1, session.StaleProgressHandles.Count);
        AssertDeferredHandle(
            session.StaleProgressHandles[0],
            ModelInspectionFixtureDeferredEventKind.StaleProgress,
            ownerAttempt: 1,
            "capture-old-progress",
            "old-progress");
        RecordingProgress oldProgress = new();
        Task<ModelInspectionExecutionResult> first =
            session.Service.InspectAsync(
                session.Request,
                oldProgress,
                CancellationToken.None);
        session.Service.ReleaseServiceCheckpoint(1, "capture-old-progress");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ReleaseStaleProgress(2, "old-progress"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ReleaseStaleProgress(1, "wrong"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ReleaseStaleProgress(1, "old-progress"));

        session.Service.ReleaseServiceCheckpoint(1, "failure");
        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            (await first).Status);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ReleaseStaleProgress(1, "old-progress"));

        Task<ModelInspectionExecutionResult> second =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);
        Assert.IsTrue(session.ReleaseStaleProgress(1, "old-progress"));
        Assert.AreEqual(1, oldProgress.Values.Count);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ReleaseStaleProgress(1, "old-progress"));

        session.Service.ReleaseServiceCheckpoint(2, "ready");
        Assert.AreEqual(ModelInspectionOutcome.Ready,
            (await second).Result?.Outcome);
        session.Retire();
        Assert.IsFalse(session.ReleaseStaleProgress(1, "old-progress"));
    }

    [TestMethod]
    public async Task TypedStaleCallbacks_RequireExactIdentityAndLaterAttempt()
    {
        (string Id, string Capture, string Release,
            ModelInspectionFixtureDeferredEventKind Kind)[] cases =
        [
            ("MI-034", "capture-old-result", "old-result",
                ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot),
            ("MI-035", "capture-old-motion", "old-motion",
                ModelInspectionFixtureDeferredEventKind.StaleMotion),
            ("MI-036", "capture-old-announcement", "old-announcement",
                ModelInspectionFixtureDeferredEventKind.StaleAnnouncement)
        ];

        foreach ((string id, string capture, string release,
                 ModelInspectionFixtureDeferredEventKind kind) in cases)
        {
            using ModelInspectionFixtureSession session = new(
                ModelInspectionFixtureTestCatalogue.Get(id).Input);
            Task<ModelInspectionExecutionResult> first =
                session.Service.InspectAsync(
                    session.Request,
                    new RecordingProgress(),
                    CancellationToken.None);
            session.Service.ReleaseServiceCheckpoint(1, capture);

            int callbackCount = 0;
            CaptureCallback(
                session,
                kind,
                ownerAttempt: 1,
                capture,
                () => callbackCount++);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                CaptureCallback(
                    session,
                    kind,
                    ownerAttempt: 1,
                    "wrong",
                    () => callbackCount++));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ReleaseCallback(session, kind, 2, release));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ReleaseCallback(session, kind, 1, "wrong"));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ReleaseCallback(session, kind, 1, release));

            session.Service.ReleaseServiceCheckpoint(1, "failure");
            ModelInspectionExecutionResult oldResult = await first;
            Assert.AreEqual(
                ModelInspectionExecutionStatus.OperationalFailure,
                oldResult.Status,
                id);
            Assert.AreEqual(1, session.Evidence.TerminalCompletionCount, id);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ReleaseCallback(session, kind, 1, release));

            Task<ModelInspectionExecutionResult> second =
                session.Service.InspectAsync(
                    session.Request,
                    new RecordingProgress(),
                    CancellationToken.None);
            Assert.IsTrue(ReleaseCallback(session, kind, 1, release), id);
            Assert.AreEqual(1, callbackCount, id);
            Assert.AreEqual(1, session.Evidence.TerminalCompletionCount, id);
            Assert.IsFalse(second.IsCompleted, id);
            Assert.AreEqual(
                ModelInspectionExecutionStatus.OperationalFailure,
                first.Result.Status,
                id);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ReleaseCallback(session, kind, 1, release));

            session.Service.ReleaseServiceCheckpoint(2, "ready");
            Assert.AreEqual(ModelInspectionOutcome.Ready,
                (await second).Result?.Outcome,
                id);
            Assert.AreEqual(2, session.Evidence.TerminalCompletionCount, id);
            CollectionAssert.AreEqual(
                new[] { 1, 2 },
                session.Evidence.TerminalCompletionAttempts.ToArray(),
                id);

            session.Retire();
            Assert.IsFalse(ReleaseCallback(session, kind, 1, release), id);
            Assert.AreEqual(1, callbackCount, id);
        }
    }

    [TestMethod]
    public async Task StaleCallback_CanRetireItsSessionWithoutDeadlockAndClosesAdmission()
    {
        int deliveryStarts = 0;
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-036").Input,
            beforeDeferredDelivery: kind =>
            {
                Assert.AreEqual(
                    ModelInspectionFixtureDeferredEventKind.StaleAnnouncement,
                    kind);
                Interlocked.Increment(ref deliveryStarts);
            });
        Task<ModelInspectionExecutionResult> first =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);
        session.Service.ReleaseServiceCheckpoint(1, "capture-old-announcement");

        bool callbackRetiredSession = false;
        session.CaptureStaleAnnouncementCallback(
            1,
            "capture-old-announcement",
            () =>
            {
                callbackRetiredSession = session.Retire();
            });
        session.Service.ReleaseServiceCheckpoint(1, "failure");
        await first;
        Task<ModelInspectionExecutionResult> second =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);

        Assert.IsTrue(session.ReleaseStaleAnnouncementCallback(
            1,
            "old-announcement"));
        Assert.IsTrue(callbackRetiredSession);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await second);
        Assert.IsFalse(session.Retire());
        Assert.IsFalse(session.ReleaseStaleAnnouncementCallback(
            1,
            "old-announcement"));
        Assert.AreEqual(1, deliveryStarts);
    }

    [TestMethod]
    public async Task StaleResultSnapshotCallback_RetirementBeforeReleaseSuppressesCallback()
    {
        await AssertRetirementBeforeStaleCallbackRelease(
            "MI-034",
            "capture-old-result",
            "old-result",
            ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot);
    }

    [TestMethod]
    public async Task StaleMotionCallback_RetirementBeforeReleaseSuppressesCallback()
    {
        await AssertRetirementBeforeStaleCallbackRelease(
            "MI-035",
            "capture-old-motion",
            "old-motion",
            ModelInspectionFixtureDeferredEventKind.StaleMotion);
    }

    [TestMethod]
    public async Task StaleAnnouncementCallback_RetirementBeforeReleaseSuppressesCallback()
    {
        await AssertRetirementBeforeStaleCallbackRelease(
            "MI-036",
            "capture-old-announcement",
            "old-announcement",
            ModelInspectionFixtureDeferredEventKind.StaleAnnouncement);
    }

    [TestMethod]
    public void Session_DeclaresTypedOwnedHandlesForEveryStaleEventKind()
    {
        Assembly assembly = typeof(ModelInspectionFixtureSession).Assembly;
        string runtimeNamespace = typeof(ModelInspectionFixtureSession)
            .Namespace!;
        Type[] handleTypes =
        [
            RequireType(assembly,
                runtimeNamespace +
                ".ModelInspectionFixtureStaleProgressHandle"),
            RequireType(assembly,
                runtimeNamespace +
                ".ModelInspectionFixtureStaleResultSnapshotHandle"),
            RequireType(assembly,
                runtimeNamespace +
                ".ModelInspectionFixtureStaleMotionCallbackHandle"),
            RequireType(assembly,
                runtimeNamespace +
                ".ModelInspectionFixtureStaleAnnouncementCallbackHandle")
        ];

        foreach (Type handleType in handleTypes)
        {
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    nameof(ModelInspectionFixtureDeferredEvent.Kind),
                    nameof(ModelInspectionFixtureDeferredEvent.OwnerAttempt),
                    nameof(ModelInspectionFixtureDeferredEvent.CaptureCheckpoint),
                    nameof(ModelInspectionFixtureDeferredEvent.ReleaseCheckpoint)
                },
                handleType.GetProperties(BindingFlags.Instance |
                        BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(property => property.Name)
                    .ToArray(),
                handleType.Name);
        }

        AssertSessionMethod(
            "ReleaseStaleProgress",
            typeof(bool),
            typeof(int),
            typeof(string));
        AssertSessionMethod(
            "CaptureStaleResultSnapshot",
            typeof(void),
            typeof(int),
            typeof(string),
            typeof(Action));
        AssertSessionMethod(
            "ReleaseStaleResultSnapshot",
            typeof(bool),
            typeof(int),
            typeof(string));
        AssertSessionMethod(
            "CaptureStaleMotionCallback",
            typeof(void),
            typeof(int),
            typeof(string),
            typeof(Action));
        AssertSessionMethod(
            "ReleaseStaleMotionCallback",
            typeof(bool),
            typeof(int),
            typeof(string));
        AssertSessionMethod(
            "CaptureStaleAnnouncementCallback",
            typeof(void),
            typeof(int),
            typeof(string),
            typeof(Action));
        AssertSessionMethod(
            "ReleaseStaleAnnouncementCallback",
            typeof(bool),
            typeof(int),
            typeof(string));
    }

    [DataTestMethod]
    [DataRow("MI-034", "capture-old-result", "old-result",
        (int)ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot)]
    [DataRow("MI-035", "capture-old-motion", "old-motion",
        (int)ModelInspectionFixtureDeferredEventKind.StaleMotion)]
    [DataRow("MI-036", "capture-old-announcement", "old-announcement",
        (int)ModelInspectionFixtureDeferredEventKind.StaleAnnouncement)]
    public async Task NonProgressDeferredEvents_RemainSessionOwnedMetadata(
        string id,
        string captureCheckpoint,
        string releaseCheckpoint,
        int expectedKind)
    {
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get(id).Input);
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);

        session.Service.ReleaseServiceCheckpoint(1, captureCheckpoint);

        ModelInspectionFixtureDeferredEventHandle handle =
            (ModelInspectionFixtureDeferredEventKind)expectedKind switch
            {
                ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot =>
                    session.StaleResultSnapshotHandles.Single(),
                ModelInspectionFixtureDeferredEventKind.StaleMotion =>
                    session.StaleMotionCallbackHandles.Single(),
                ModelInspectionFixtureDeferredEventKind.StaleAnnouncement =>
                    session.StaleAnnouncementCallbackHandles.Single(),
                _ => throw new AssertFailedException(
                    "The fixture has no expected non-progress handle kind.")
            };
        AssertDeferredHandle(
            handle,
            (ModelInspectionFixtureDeferredEventKind)expectedKind,
            ownerAttempt: 1,
            captureCheckpoint,
            releaseCheckpoint);

        Assert.AreEqual(1, session.CapturedDeferredEvents.Count);
        ModelInspectionFixtureDeferredEvent captured =
            session.CapturedDeferredEvents[0];
        Assert.AreEqual(
            (ModelInspectionFixtureDeferredEventKind)expectedKind,
            captured.Kind);
        Assert.AreEqual(1, captured.OwnerAttempt);
        Assert.AreEqual(captureCheckpoint, captured.CaptureCheckpoint);
        Assert.AreEqual(releaseCheckpoint, captured.ReleaseCheckpoint);
        Assert.AreEqual(
            0,
            typeof(DebugModelInspectionService)
                .GetMethods(BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .Where(name => name.StartsWith(
                    "ReleaseDeferred",
                    StringComparison.Ordinal))
                .Count(),
            "Deferred releases must remain on the owning session.");

        session.Retire();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        Assert.AreEqual(1, session.CapturedDeferredEvents.Count);
    }

    [TestMethod]
    public async Task ProgressCallback_CanReenterTheNextNamedCheckpoint()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-028");
        using DebugModelInspectionService service = new(
            plan,
            new ModelInspectionFixtureSessionEvidence());
        ReentrantProgress progress = new();
        progress.OnFirstReport = () =>
            service.ReleaseServiceCheckpoint(1, "fraction-75");
        Task<ModelInspectionExecutionResult> run = service.InspectAsync(
            plan.Request,
            progress,
            CancellationToken.None);

        service.ReleaseServiceCheckpoint(1, "fraction-25");

        CollectionAssert.AreEqual(
            new double?[] { 0.25d, 0.75d },
            progress.Values.Select(value => value.StageFraction).ToArray());
        service.ReleaseServiceCheckpoint(1, "terminal");
        await run;
    }

    [TestMethod]
    public async Task PreparedProgressDelivery_RetireAndDisposeWaitForReporter()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-028");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using ManualResetEventSlim prepared = new(initialState: false);
        using ManualResetEventSlim allowDelivery = new(initialState: false);
        using ManualResetEventSlim retirementReturned = new(initialState: false);
        using ManualResetEventSlim disposalReturned = new(initialState: false);
        int deliveryStarts = 0;
        DebugModelInspectionService service = new(
            plan,
            evidence,
            beforeProgressDelivery: () =>
            {
                Interlocked.Increment(ref deliveryStarts);
                prepared.Set();
                allowDelivery.Wait();
            });
        using CancellationTokenSource cancellation = new();
        RecordingProgress progress = new();
        Task<ModelInspectionExecutionResult> pending = service.InspectAsync(
            plan.Request,
            progress,
            cancellation.Token);
        Task release = Task.Run(() =>
            service.ReleaseServiceCheckpoint(1, "fraction-25"));
        Task<bool>? retirement = null;
        Task? disposal = null;
        bool retirementEscaped = false;
        bool disposalEscaped = false;

        try
        {
            Assert.IsTrue(
                prepared.Wait(TimeSpan.FromSeconds(1)),
                "The prepared progress delivery did not reach its barrier.");
            Assert.AreEqual(0, progress.Values.Count);
            retirement = Task.Run(() =>
            {
                try
                {
                    return service.Retire();
                }
                finally
                {
                    retirementReturned.Set();
                }
            });
            AssertRetirementBegan(service);
            disposal = Task.Run(() =>
            {
                try
                {
                    service.Dispose();
                }
                finally
                {
                    disposalReturned.Set();
                }
            });

            retirementEscaped = retirementReturned.Wait(
                TimeSpan.FromMilliseconds(100));
            disposalEscaped = disposalReturned.Wait(
                TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            allowDelivery.Set();
        }

        await release;
        Assert.IsNotNull(retirement);
        Assert.IsTrue(await retirement);
        Assert.IsNotNull(disposal);
        await disposal;
        Assert.IsFalse(
            retirementEscaped,
            "Retire returned while a prepared progress delivery was paused.");
        Assert.IsFalse(
            disposalEscaped,
            "Dispose returned while another retirement was still draining progress.");
        Assert.AreEqual(1, progress.Values.Count);
        Assert.AreEqual(1, deliveryStarts);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            service.ReleaseServiceCheckpoint(1, "fraction-75"));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        cancellation.Cancel();
        Assert.AreEqual(0, evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(0, evidence.CancellationObservationCount);
        Assert.AreEqual(1, evidence.ServiceRetirementCount);
        Assert.AreEqual(1, evidence.ServiceDisposalCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServiceProgressCallback_ReentrantSessionLifetimeDoesNotDeadlockExternalLifetime(
        bool dispose)
    {
        using ManualResetEventSlim callbackEntered = new(initialState: false);
        using ManualResetEventSlim allowReentrantLifetime = new(initialState: false);
        using ManualResetEventSlim reentrantLifetimeReturned =
            new(initialState: false);
        using ManualResetEventSlim allowCallbackExit = new(initialState: false);
        using ManualResetEventSlim callbackReturned = new(initialState: false);
        using ManualResetEventSlim externalReturned = new(initialState: false);
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-028").Input,
            animationDriverFactory: () => innerDriver);
        _ = session.CreateAnimationDriver();
        _ = session.CreateMotionSettings();
        Thread? deliveryThread = null;
        Exception? callbackError = null;
        bool reentrantCallReturned = false;
        bool? reentrantRetireResult = null;
        ReentrantProgress progress = new()
        {
            OnFirstReport = () =>
            {
                deliveryThread = Thread.CurrentThread;
                callbackEntered.Set();
                allowReentrantLifetime.Wait();
                try
                {
                    if (dispose)
                    {
                        session.Dispose();
                    }
                    else
                    {
                        reentrantRetireResult = session.Retire();
                    }

                    reentrantCallReturned = true;
                    reentrantLifetimeReturned.Set();
                    allowCallbackExit.Wait();
                }
                catch (ThreadInterruptedException exception)
                {
                    callbackError = exception;
                }
                finally
                {
                    callbackReturned.Set();
                }
            }
        };
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                progress,
                cancellation.Token);
        Task release = Task.Run(() =>
            session.Service.ReleaseServiceCheckpoint(1, "fraction-25"));
        Task<bool>? externalLifetime = null;
        bool callbackReached = false;
        bool externalEscaped = false;

        try
        {
            callbackReached = callbackEntered.Wait(TimeSpan.FromSeconds(1));
            if (callbackReached)
            {
                externalLifetime = Task.Run(() =>
                {
                    try
                    {
                        if (dispose)
                        {
                            session.Dispose();
                            return true;
                        }

                        return session.Retire();
                    }
                    finally
                    {
                        externalReturned.Set();
                    }
                });
                AssertRetirementBegan(session);
                AssertRetirementBegan(session.Service);
                externalEscaped = externalReturned.Wait(
                    TimeSpan.FromMilliseconds(100));
            }
        }
        finally
        {
            allowReentrantLifetime.Set();
        }

        bool reentrantReturnedWithoutRecovery = reentrantLifetimeReturned.Wait(
            TimeSpan.FromMilliseconds(250));
        bool externalEscapedAfterReentry = reentrantReturnedWithoutRecovery &&
            externalReturned.Wait(TimeSpan.FromMilliseconds(100));
        allowCallbackExit.Set();
        bool callbackCompletedWithoutRecovery = callbackReturned.Wait(
            TimeSpan.FromMilliseconds(250));
        if ((!reentrantReturnedWithoutRecovery ||
                !callbackCompletedWithoutRecovery) &&
            deliveryThread is not null)
        {
            deliveryThread.Interrupt();
        }

        bool callbackWasRecovered = callbackReturned.Wait(
            TimeSpan.FromSeconds(1));
        await release;
        Assert.IsNotNull(externalLifetime);
        Assert.IsTrue(await externalLifetime);

        Assert.IsTrue(callbackReached);
        Assert.IsFalse(
            externalEscaped,
            "External lifetime completion escaped while progress delivery was paused.");
        Assert.IsFalse(
            externalEscapedAfterReentry,
            "External lifetime completion escaped before the reentrant callback unwound.");
        Assert.IsTrue(
            callbackWasRecovered,
            "The bounded recovery did not unwind the reporter callback.");
        Assert.IsTrue(
            reentrantReturnedWithoutRecovery,
            "The service callback did not return from its reentrant lifetime call.");
        Assert.IsTrue(
            callbackCompletedWithoutRecovery,
            "The service callback deadlocked reentering the session lifetime.");
        Assert.IsTrue(reentrantCallReturned);
        Assert.IsNull(callbackError);
        if (!dispose)
        {
            Assert.AreEqual(false, reentrantRetireResult);
        }

        Assert.AreEqual(1, progress.Values.Count);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            session.Service.ReleaseServiceCheckpoint(1, "fraction-75"));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        cancellation.Cancel();
        Assert.AreEqual(0, session.Evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(0, session.Evidence.CancellationObservationCount);
        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount);
        Assert.AreEqual(1, session.Evidence.ServiceRetirementCount);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.AreEqual(
            dispose ? 1 : 0,
            session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(
            dispose ? 1 : 0,
            session.Evidence.SessionDisposalCount);

        session.Dispose();

        Assert.AreEqual(1, session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServiceProgressCallback_CallbackFirstLifetimeKeepsLaterExternalLifetimeJoined(
        bool dispose)
    {
        using ManualResetEventSlim callbackEntered = new(initialState: false);
        using ManualResetEventSlim reentrantLifetimeReturned =
            new(initialState: false);
        using ManualResetEventSlim allowCallbackExit = new(initialState: false);
        using ManualResetEventSlim callbackExited = new(initialState: false);
        using ManualResetEventSlim externalStarted = new(initialState: false);
        using ManualResetEventSlim externalReturned = new(initialState: false);
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-028").Input,
            animationDriverFactory: () => innerDriver);
        _ = session.CreateAnimationDriver();
        _ = session.CreateMotionSettings();
        Thread? deliveryThread = null;
        Exception? callbackError = null;
        bool reentrantCallReturned = false;
        bool? reentrantRetireResult = null;
        ReentrantProgress progress = new()
        {
            OnFirstReport = () =>
            {
                deliveryThread = Thread.CurrentThread;
                callbackEntered.Set();
                try
                {
                    if (dispose)
                    {
                        session.Dispose();
                    }
                    else
                    {
                        reentrantRetireResult = session.Retire();
                    }

                    reentrantCallReturned = true;
                    reentrantLifetimeReturned.Set();
                    allowCallbackExit.Wait();
                }
                catch (ThreadInterruptedException exception)
                {
                    callbackError = exception;
                }
                finally
                {
                    callbackExited.Set();
                }
            }
        };
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                progress,
                cancellation.Token);
        Task release = Task.Run(() =>
            session.Service.ReleaseServiceCheckpoint(1, "fraction-25"));
        Task<bool>? externalLifetime = null;
        bool callbackReached = false;
        bool reentrantReturnedWithoutRecovery = false;
        bool externalStartedWithinBound = false;
        bool externalEscaped = false;

        try
        {
            callbackReached = callbackEntered.Wait(TimeSpan.FromSeconds(1));
            if (callbackReached)
            {
                reentrantReturnedWithoutRecovery =
                    reentrantLifetimeReturned.Wait(
                        TimeSpan.FromMilliseconds(250));
            }

            if (reentrantReturnedWithoutRecovery)
            {
                externalLifetime = Task.Run(() =>
                {
                    externalStarted.Set();
                    try
                    {
                        if (dispose)
                        {
                            session.Dispose();
                            return true;
                        }

                        return session.Retire();
                    }
                    finally
                    {
                        externalReturned.Set();
                    }
                });
                externalStartedWithinBound = externalStarted.Wait(
                    TimeSpan.FromSeconds(1));
                if (externalStartedWithinBound)
                {
                    externalEscaped = externalReturned.Wait(
                        TimeSpan.FromMilliseconds(100));
                }
            }
        }
        finally
        {
            allowCallbackExit.Set();
        }

        bool callbackCompletedWithoutRecovery = callbackExited.Wait(
            TimeSpan.FromMilliseconds(250));
        if ((!reentrantReturnedWithoutRecovery ||
                !callbackCompletedWithoutRecovery) &&
            deliveryThread is not null)
        {
            deliveryThread.Interrupt();
        }

        bool callbackWasRecovered = callbackExited.Wait(
            TimeSpan.FromSeconds(1));
        await release;
        Assert.IsNotNull(externalLifetime);
        bool externalResult = await externalLifetime;

        Assert.IsTrue(callbackReached);
        Assert.IsTrue(
            reentrantReturnedWithoutRecovery,
            "The callback-first lifetime call did not return reentrantly.");
        Assert.IsTrue(
            callbackWasRecovered,
            "The bounded recovery did not unwind the callback-first reporter.");
        Assert.IsTrue(callbackCompletedWithoutRecovery);
        Assert.IsTrue(
            externalStartedWithinBound,
            "The later external lifetime call was not scheduled within the bounded barrier.");
        Assert.IsFalse(
            externalEscaped,
            "A later external lifetime call escaped before the admitted callback unwound.");
        Assert.IsTrue(reentrantCallReturned);
        Assert.IsNull(callbackError);
        if (dispose)
        {
            Assert.IsTrue(externalResult);
        }
        else
        {
            Assert.AreEqual(true, reentrantRetireResult);
            Assert.IsFalse(externalResult);
        }

        Assert.AreEqual(1, progress.Values.Count);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            session.Service.ReleaseServiceCheckpoint(1, "fraction-75"));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        cancellation.Cancel();
        Assert.AreEqual(0, session.Evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(0, session.Evidence.CancellationObservationCount);
        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount);
        Assert.AreEqual(1, session.Evidence.ServiceRetirementCount);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.AreEqual(
            dispose ? 1 : 0,
            session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(
            dispose ? 1 : 0,
            session.Evidence.SessionDisposalCount);

        session.Dispose();

        Assert.AreEqual(1, session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
    }

    [TestMethod]
    public async Task OperationDrains_FromDifferentLifetimeDomainsRemainIsolated()
    {
        using ManualResetEventSlim firstLeaseEntered = new(initialState: false);
        using ManualResetEventSlim allowFirstLeaseExit = new(initialState: false);
        using ManualResetEventSlim observerReady = new(initialState: false);
        using ManualResetEventSlim closeReturned = new(initialState: false);
        ModelInspectionFixtureOperationDrain first = new();
        ModelInspectionFixtureOperationDrain second = new();
        int cleanupCount = 0;
        Task firstLease = Task.Run(() =>
        {
            using ModelInspectionFixtureOperationDrain.Lease lease =
                first.Enter();
            firstLeaseEntered.Set();
            allowFirstLeaseExit.Wait();
        });
        Assert.IsTrue(firstLeaseEntered.Wait(TimeSpan.FromSeconds(1)));
        bool closeEscapedOtherDomain = false;
        Task observer = Task.Run(() =>
        {
            observerReady.Set();
            closeEscapedOtherDomain = closeReturned.Wait(
                TimeSpan.FromMilliseconds(100));
            allowFirstLeaseExit.Set();
        });
        Assert.IsTrue(observerReady.Wait(TimeSpan.FromSeconds(1)));

        bool firstClose;
        using (second.Enter())
        {
            try
            {
                firstClose = first.Close(() =>
                    Interlocked.Increment(ref cleanupCount));
            }
            finally
            {
                closeReturned.Set();
                allowFirstLeaseExit.Set();
            }
        }

        await observer;
        await firstLease;
        Assert.IsFalse(
            closeEscapedOtherDomain,
            "An operation in another lifetime domain bypassed drain waiting.");
        Assert.IsTrue(firstClose);
        Assert.AreEqual(1, cleanupCount);
    }

    [TestMethod]
    public async Task PreparedTerminalDelivery_RetireAndDisposeDrainBeforeReturning()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-002");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using ManualResetEventSlim prepared = new(initialState: false);
        using ManualResetEventSlim allowDelivery = new(initialState: false);
        using ManualResetEventSlim retirementReturned = new(initialState: false);
        using ManualResetEventSlim disposalReturned = new(initialState: false);
        int deliveryStarts = 0;
        DebugModelInspectionService service = new(
            plan,
            evidence,
            beforeTerminalDelivery: () =>
            {
                Interlocked.Increment(ref deliveryStarts);
                prepared.Set();
                allowDelivery.Wait();
            });
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> pending = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            cancellation.Token);
        Task release = Task.Run(() =>
            service.ReleaseServiceCheckpoint(1, "terminal"));
        Task<bool>? retirement = null;
        Task? disposal = null;
        bool retirementEscaped = false;
        bool disposalEscaped = false;

        try
        {
            Assert.IsTrue(
                prepared.Wait(TimeSpan.FromSeconds(1)),
                "The prepared terminal delivery did not reach its barrier.");
            Assert.IsFalse(pending.IsCompleted);
            retirement = Task.Run(() =>
            {
                try
                {
                    return service.Retire();
                }
                finally
                {
                    retirementReturned.Set();
                }
            });
            AssertRetirementBegan(service);
            disposal = Task.Run(() =>
            {
                try
                {
                    service.Dispose();
                }
                finally
                {
                    disposalReturned.Set();
                }
            });

            retirementEscaped = retirementReturned.Wait(
                TimeSpan.FromMilliseconds(100));
            disposalEscaped = disposalReturned.Wait(
                TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            allowDelivery.Set();
        }

        await release;
        Assert.IsNotNull(retirement);
        Assert.IsTrue(await retirement);
        Assert.IsNotNull(disposal);
        await disposal;
        Assert.IsFalse(
            retirementEscaped,
            "Retire returned while terminal completion was paused.");
        Assert.IsFalse(
            disposalEscaped,
            "Dispose returned while another retirement was still draining terminal completion.");
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        Assert.IsTrue(pending.IsCanceled);
        Assert.AreEqual(1, deliveryStarts);
        Assert.AreEqual(0, evidence.TerminalCompletionCount);
        Assert.AreEqual(1, evidence.RetiredPendingCallCount);
        Assert.AreEqual(0, evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(1, evidence.ServiceRetirementCount);
        Assert.AreEqual(1, evidence.ServiceDisposalCount);
    }

    [DataTestMethod]
    [DataRow("MI-034", "capture-old-result", "old-result",
        (int)ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot)]
    [DataRow("MI-035", "capture-old-motion", "old-motion",
        (int)ModelInspectionFixtureDeferredEventKind.StaleMotion)]
    [DataRow("MI-036", "capture-old-announcement", "old-announcement",
        (int)ModelInspectionFixtureDeferredEventKind.StaleAnnouncement)]
    public async Task MarkedDeferredCallback_RetireAndDisposeWaitForInvocation(
        string id,
        string captureCheckpoint,
        string releaseCheckpoint,
        int expectedKind)
    {
        ModelInspectionFixtureDeferredEventKind kind =
            (ModelInspectionFixtureDeferredEventKind)expectedKind;
        using ManualResetEventSlim prepared = new(initialState: false);
        using ManualResetEventSlim allowDelivery = new(initialState: false);
        using ManualResetEventSlim retirementReturned = new(initialState: false);
        using ManualResetEventSlim disposalReturned = new(initialState: false);
        int deliveryStarts = 0;
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get(id).Input,
            animationDriverFactory: () => innerDriver,
            beforeDeferredDelivery: actualKind =>
            {
                Assert.AreEqual(kind, actualKind, id);
                Interlocked.Increment(ref deliveryStarts);
                prepared.Set();
                allowDelivery.Wait();
            });
        _ = session.CreateAnimationDriver();
        _ = session.CreateMotionSettings();
        Task<ModelInspectionExecutionResult> first =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);
        session.Service.ReleaseServiceCheckpoint(1, captureCheckpoint);
        int callbackCount = 0;
        CaptureCallback(
            session,
            kind,
            ownerAttempt: 1,
            captureCheckpoint,
            () => Interlocked.Increment(ref callbackCount));
        session.Service.ReleaseServiceCheckpoint(1, "failure");
        await first;
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                cancellation.Token);
        Task<bool> release = Task.Run(() => ReleaseCallback(
            session,
            kind,
            ownerAttempt: 1,
            releaseCheckpoint));
        Task<bool>? retirement = null;
        Task? disposal = null;
        bool retirementEscaped = false;
        bool disposalEscaped = false;

        try
        {
            Assert.IsTrue(
                prepared.Wait(TimeSpan.FromSeconds(1)),
                $"The marked {kind} delivery did not reach its barrier.");
            Assert.AreEqual(0, callbackCount, id);
            retirement = Task.Run(() =>
            {
                try
                {
                    return session.Retire();
                }
                finally
                {
                    retirementReturned.Set();
                }
            });
            AssertRetirementBegan(session);
            disposal = Task.Run(() =>
            {
                try
                {
                    session.Dispose();
                }
                finally
                {
                    disposalReturned.Set();
                }
            });

            retirementEscaped = retirementReturned.Wait(
                TimeSpan.FromMilliseconds(100));
            disposalEscaped = disposalReturned.Wait(
                TimeSpan.FromMilliseconds(100));
        }
        finally
        {
            allowDelivery.Set();
        }

        Assert.IsTrue(await release, id);
        Assert.IsNotNull(retirement);
        Assert.IsTrue(await retirement, id);
        Assert.IsNotNull(disposal);
        await disposal;
        Assert.IsFalse(
            retirementEscaped,
            $"Retire returned while the marked {kind} callback was paused.");
        Assert.IsFalse(
            disposalEscaped,
            $"Dispose returned while another retirement was draining {kind}.");
        Assert.AreEqual(1, callbackCount, id);
        Assert.AreEqual(1, deliveryStarts, id);
        Assert.IsFalse(ReleaseCallback(
            session,
            kind,
            ownerAttempt: 1,
            releaseCheckpoint));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending,
            id);
        cancellation.Cancel();
        Assert.AreEqual(0, session.Evidence.ActiveCancellationRegistrationCount, id);
        Assert.AreEqual(1, innerDriver.DisposeCount, id);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount, id);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount, id);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount, id);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount, id);
    }

    [TestMethod]
    public async Task InitialFixture_HasNoServiceAttemptToStart()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-001");
        ModelInspectionFixtureSessionEvidence evidence = new();
        using DebugModelInspectionService service = new(plan, evidence);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.InspectAsync(
                plan.Request,
                new RecordingProgress(),
                CancellationToken.None));

        Assert.AreEqual(0, evidence.ServiceCallCount);
    }

    [TestMethod]
    public async Task Retirement_SilentlyCompletesPendingCallAndIsIdempotent()
    {
        ModelInspectionFixtureExecutionPlan plan = Plan("MI-002");
        ModelInspectionFixtureSessionEvidence evidence = new();
        DebugModelInspectionService service = new(plan, evidence);
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> pending = service.InspectAsync(
            plan.Request,
            new RecordingProgress(),
            cancellation.Token);

        Assert.AreEqual(1, evidence.ActiveCancellationRegistrationCount);
        Assert.IsTrue(service.Retire());
        Assert.IsFalse(service.Retire());
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        cancellation.Cancel();

        Assert.AreEqual(1, evidence.ServiceRetirementCount);
        Assert.AreEqual(1, evidence.RetiredPendingCallCount);
        Assert.AreEqual(0, evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(0, evidence.CancellationObservationCount);
        Assert.AreEqual(0, evidence.ReleasedServiceCheckpoints.Count);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            service.InspectAsync(
                plan.Request,
                new RecordingProgress(),
                CancellationToken.None));

        service.Dispose();
        service.Dispose();
        Assert.AreEqual(1, evidence.ServiceDisposalCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AnimationDriverFactory_ReentrantLifetimeRejectsPostCloseProductAndJoinsExternalLifetime(
        bool dispose)
    {
        using ManualResetEventSlim factoryEntered = new(initialState: false);
        using ManualResetEventSlim reentrantLifetimeReturned =
            new(initialState: false);
        using ManualResetEventSlim allowFactoryReturn = new(initialState: false);
        using ManualResetEventSlim factoryExited = new(initialState: false);
        using ManualResetEventSlim externalStarted = new(initialState: false);
        using ManualResetEventSlim externalReturned = new(initialState: false);
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession? session = null;
        Thread? factoryThread = null;
        Exception? callbackError = null;
        bool reentrantCallReturned = false;
        bool? reentrantRetireResult = null;
        session = new ModelInspectionFixtureSession(
            ModelInspectionFixtureTestCatalogue.Get("MI-002").Input,
            animationDriverFactory: () =>
            {
                factoryThread = Thread.CurrentThread;
                factoryEntered.Set();
                try
                {
                    if (dispose)
                    {
                        session!.Dispose();
                    }
                    else
                    {
                        reentrantRetireResult = session!.Retire();
                    }

                    reentrantCallReturned = true;
                    reentrantLifetimeReturned.Set();
                    allowFactoryReturn.Wait();
                    return innerDriver;
                }
                catch (ThreadInterruptedException exception)
                {
                    callbackError = exception;
                    throw;
                }
                finally
                {
                    factoryExited.Set();
                }
            });
        _ = session.CreateMotionSettings();
        using CancellationTokenSource cancellation = new();
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                cancellation.Token);
        Task<IModelInspectionAnimationDriver> creation = Task.Run(() =>
            session.CreateAnimationDriver());
        Task<bool>? externalLifetime = null;
        bool factoryReached = false;
        bool reentrantReturnedWithoutRecovery = false;
        bool externalStartedWithinBound = false;
        bool externalEscaped = false;

        try
        {
            factoryReached = factoryEntered.Wait(TimeSpan.FromSeconds(1));
            if (factoryReached)
            {
                reentrantReturnedWithoutRecovery =
                    reentrantLifetimeReturned.Wait(
                        TimeSpan.FromMilliseconds(250));
            }

            if (reentrantReturnedWithoutRecovery)
            {
                externalLifetime = Task.Run(() =>
                {
                    externalStarted.Set();
                    try
                    {
                        if (dispose)
                        {
                            session.Dispose();
                            return true;
                        }

                        return session.Retire();
                    }
                    finally
                    {
                        externalReturned.Set();
                    }
                });
                externalStartedWithinBound = externalStarted.Wait(
                    TimeSpan.FromSeconds(1));
                if (externalStartedWithinBound)
                {
                    externalEscaped = externalReturned.Wait(
                        TimeSpan.FromMilliseconds(100));
                }
            }
        }
        finally
        {
            allowFactoryReturn.Set();
        }

        bool factoryCompletedWithoutRecovery = factoryExited.Wait(
            TimeSpan.FromMilliseconds(250));
        if ((!reentrantReturnedWithoutRecovery ||
                !factoryCompletedWithoutRecovery) &&
            factoryThread is not null)
        {
            factoryThread.Interrupt();
        }

        bool factoryWasRecovered = factoryExited.Wait(
            TimeSpan.FromSeconds(1));
        IModelInspectionAnimationDriver? returnedDriver = null;
        Exception? creationError = null;
        try
        {
            returnedDriver = await creation;
        }
        catch (Exception exception)
        {
            creationError = exception;
        }

        Assert.IsNotNull(externalLifetime);
        bool externalResult = await externalLifetime;
        int disposalCountBeforeRecovery = innerDriver.DisposeCount;
        returnedDriver?.Dispose();

        Assert.IsTrue(factoryReached);
        Assert.IsTrue(
            reentrantReturnedWithoutRecovery,
            "The injected factory lifetime call did not return reentrantly.");
        Assert.IsTrue(
            factoryWasRecovered,
            "The bounded recovery did not unwind the injected factory.");
        Assert.IsTrue(factoryCompletedWithoutRecovery);
        Assert.IsTrue(
            externalStartedWithinBound,
            "The external lifetime call was not scheduled within the bounded barrier.");
        Assert.IsFalse(
            externalEscaped,
            "External lifetime completion escaped while factory creation was admitted.");
        Assert.IsTrue(reentrantCallReturned);
        Assert.IsNull(callbackError);
        Assert.IsNull(returnedDriver);
        Assert.IsNotNull(creationError);
        Assert.IsInstanceOfType<ObjectDisposedException>(creationError);
        Assert.AreEqual(1, disposalCountBeforeRecovery);
        if (dispose)
        {
            Assert.IsTrue(externalResult);
        }
        else
        {
            Assert.AreEqual(true, reentrantRetireResult);
            Assert.IsFalse(externalResult);
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            session.CreateAnimationDriver());
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);
        cancellation.Cancel();
        Assert.AreEqual(0, session.Evidence.ActiveCancellationRegistrationCount);
        Assert.AreEqual(0, session.Evidence.CancellationObservationCount);
        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount);
        Assert.AreEqual(1, session.Evidence.ServiceRetirementCount);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.AreEqual(
            dispose ? 1 : 0,
            session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(
            dispose ? 1 : 0,
            session.Evidence.SessionDisposalCount);

        session.Dispose();

        Assert.AreEqual(1, session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
    }

    [TestMethod]
    public void AnimationDriverFactory_ThrowRollsBackCreationReservation()
    {
        int factoryCalls = 0;
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-001").Input,
            animationDriverFactory: () =>
            {
                factoryCalls++;
                if (factoryCalls == 1)
                {
                    throw new InvalidOperationException("factory failure");
                }

                return innerDriver;
            });

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.CreateAnimationDriver());
        Assert.AreEqual("factory failure", error.Message);
        Assert.AreEqual(0, innerDriver.DisposeCount);
        Assert.AreEqual(0, session.Evidence.AnimationDriverDisposalCount);

        _ = session.CreateAnimationDriver();
        Assert.AreEqual(2, factoryCalls);

        session.Dispose();

        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
    }

    [TestMethod]
    public void Session_RetireCompletesOwnedCleanupWhenServiceRetirementThrows()
    {
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-001").Input,
            animationDriverFactory: () => innerDriver);
        _ = session.CreateAnimationDriver();
        _ = session.CreateMotionSettings();
        InvalidOperationException serviceFailure = new(
            "service retirement failure");
        FieldInfo? deliveriesField = typeof(DebugModelInspectionService).GetField(
            "deliveries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(deliveriesField);
        ModelInspectionFixtureOperationDrain serviceDeliveries =
            (ModelInspectionFixtureOperationDrain)deliveriesField.GetValue(
                session.Service)!;
        InvalidOperationException injected =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                serviceDeliveries.Close(() => throw serviceFailure));
        Assert.AreSame(serviceFailure, injected);

        InvalidOperationException observed =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.Retire());

        Assert.AreSame(serviceFailure, observed);
        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            session.CreateAnimationDriver());

        InvalidOperationException disposalObserved =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.Dispose());

        Assert.AreSame(serviceFailure, disposalObserved);
        Assert.AreEqual(1, session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
    }

    [TestMethod]
    public void Session_DisposeAttemptsEveryOwnedCleanupWhenDriverDisposalThrows()
    {
        InvalidOperationException driverFailure = new(
            "animation-driver disposal failure");
        RecordingAnimationDriver innerDriver = new(driverFailure);
        ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-001").Input,
            animationDriverFactory: () => innerDriver);
        _ = session.CreateAnimationDriver();
        _ = session.CreateMotionSettings();

        InvalidOperationException observed =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                session.Dispose());

        Assert.AreSame(driverFailure, observed);
        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount);
        Assert.AreEqual(1, session.Evidence.ServiceRetirementCount);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.AreEqual(1, session.Evidence.ServiceDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
    }

    [TestMethod]
    public async Task Session_OwnsServiceMotionAndIdempotentRetirementAudit()
    {
        ValidatedModelInspectionFixture fixture =
            ModelInspectionFixtureTestCatalogue.Get("MI-002");
        RecordingAnimationDriver innerDriver = new();
        ModelInspectionFixtureSession session = new(
            fixture.Input,
            animationsEnabled: false,
            animationDriverFactory: () => innerDriver);
        IModelInspectionAnimationDriver driver =
            session.CreateAnimationDriver();
        IModelInspectionMotionSettings settings =
            session.CreateMotionSettings();
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);

        Assert.IsFalse(settings.AnimationsEnabled);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.CreateAnimationDriver());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.CreateMotionSettings());

        driver.CancelAll();
        driver.CancelAll();
        Assert.AreEqual(2, innerDriver.CancelAllCount);
        Assert.AreEqual(2, session.Evidence.AnimationCancellationCount);
        Assert.IsTrue(session.Retire());
        Assert.AreEqual(
            session.Evidence.AnimationCancellationCount +
                session.Evidence.AnimationDriverDisposalCount,
            innerDriver.CancelAllCount,
            "Retirement must cancel the owned driver once before disposal.");
        Assert.IsFalse(session.Retire());
        session.Dispose();
        session.Dispose();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending);

        Assert.AreEqual(
            session.Evidence.AnimationCancellationCount +
                session.Evidence.AnimationDriverDisposalCount,
            innerDriver.CancelAllCount,
            "Repeated retirement and disposal must not cancel the driver again.");
        Assert.AreEqual(1, innerDriver.DisposeCount);
        Assert.AreEqual(1, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(1, session.Evidence.MotionSettingsDisposalCount);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
        Assert.AreEqual(0, session.Evidence.AnimationStartCount);
    }

    [TestMethod]
    public void Session_RetiresCleanlyWhenMotionProductsWereNeverCreated()
    {
        int factoryCalls = 0;
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get("MI-001").Input,
            animationDriverFactory: () =>
            {
                factoryCalls++;
                return new RecordingAnimationDriver();
            });

        Assert.IsTrue(session.Retire());

        Assert.AreEqual(0, factoryCalls);
        Assert.AreEqual(0, session.Evidence.AnimationDriverDisposalCount);
        Assert.AreEqual(0, session.Evidence.MotionSettingsDisposalCount);
    }

    private static ModelInspectionFixtureExecutionPlan Plan(string id) =>
        ModelInspectionFixtureAdapter.CreatePlan(
            ModelInspectionFixtureTestCatalogue.Get(id).Input);

    private static void AssertRetirementBegan(object owner)
    {
        FieldInfo? retired = owner.GetType().GetField(
            "retired",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(retired, "The retirement state field is absent.");
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => (bool)retired.GetValue(owner)!,
                TimeSpan.FromSeconds(1)),
            "Retirement did not close admission within the bounded barrier.");
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false);
        Assert.IsNotNull(type, $"Required typed stale-event handle {fullName} is absent.");
        return type;
    }

    private static void AssertSessionMethod(
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        MethodInfo? method = typeof(ModelInspectionFixtureSession).GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic,
            binder: null,
            parameterTypes,
            modifiers: null);
        Assert.IsNotNull(method, $"Session method {name} is absent.");
        Assert.AreEqual(returnType, method.ReturnType, name);
    }

    private static void CaptureCallback(
        ModelInspectionFixtureSession session,
        ModelInspectionFixtureDeferredEventKind kind,
        int ownerAttempt,
        string captureCheckpoint,
        Action callback)
    {
        switch (kind)
        {
            case ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot:
                session.CaptureStaleResultSnapshot(
                    ownerAttempt,
                    captureCheckpoint,
                    callback);
                break;
            case ModelInspectionFixtureDeferredEventKind.StaleMotion:
                session.CaptureStaleMotionCallback(
                    ownerAttempt,
                    captureCheckpoint,
                    callback);
                break;
            case ModelInspectionFixtureDeferredEventKind.StaleAnnouncement:
                session.CaptureStaleAnnouncementCallback(
                    ownerAttempt,
                    captureCheckpoint,
                    callback);
                break;
            default:
                throw new AssertFailedException(
                    $"No callback capture exists for {kind}.");
        }
    }

    private static bool ReleaseCallback(
        ModelInspectionFixtureSession session,
        ModelInspectionFixtureDeferredEventKind kind,
        int ownerAttempt,
        string releaseCheckpoint) => kind switch
        {
            ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot =>
                session.ReleaseStaleResultSnapshot(
                    ownerAttempt,
                    releaseCheckpoint),
            ModelInspectionFixtureDeferredEventKind.StaleMotion =>
                session.ReleaseStaleMotionCallback(
                    ownerAttempt,
                    releaseCheckpoint),
            ModelInspectionFixtureDeferredEventKind.StaleAnnouncement =>
                session.ReleaseStaleAnnouncementCallback(
                    ownerAttempt,
                    releaseCheckpoint),
            _ => throw new AssertFailedException(
                $"No callback release exists for {kind}.")
        };

    private static void AssertDeferredHandle(
        ModelInspectionFixtureDeferredEventHandle handle,
        ModelInspectionFixtureDeferredEventKind kind,
        int ownerAttempt,
        string captureCheckpoint,
        string releaseCheckpoint)
    {
        Assert.AreEqual(kind, handle.Kind);
        Assert.AreEqual(ownerAttempt, handle.OwnerAttempt);
        Assert.AreEqual(captureCheckpoint, handle.CaptureCheckpoint);
        Assert.AreEqual(releaseCheckpoint, handle.ReleaseCheckpoint);
    }

    private static async Task AssertRetirementBeforeStaleCallbackRelease(
        string id,
        string captureCheckpoint,
        string releaseCheckpoint,
        ModelInspectionFixtureDeferredEventKind kind)
    {
        using ModelInspectionFixtureSession session = new(
            ModelInspectionFixtureTestCatalogue.Get(id).Input);
        Task<ModelInspectionExecutionResult> pending =
            session.Service.InspectAsync(
                session.Request,
                new RecordingProgress(),
                CancellationToken.None);
        session.Service.ReleaseServiceCheckpoint(1, captureCheckpoint);

        int callbackCount = 0;
        CaptureCallback(
            session,
            kind,
            ownerAttempt: 1,
            captureCheckpoint,
            () => callbackCount++);

        Assert.IsTrue(session.Retire(), id);
        Assert.IsFalse(
            ReleaseCallback(
                session,
                kind,
                ownerAttempt: 1,
                releaseCheckpoint),
            id);
        Assert.AreEqual(0, callbackCount, id);
        Assert.AreEqual(0,
            ReleasedDeferredCount(session.Evidence, kind),
            id);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await pending,
            id);
    }

    private static int ReleasedDeferredCount(
        ModelInspectionFixtureSessionEvidence evidence,
        ModelInspectionFixtureDeferredEventKind kind) => kind switch
        {
            ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot =>
                evidence.ReleasedDeferredResultSnapshotCount,
            ModelInspectionFixtureDeferredEventKind.StaleMotion =>
                evidence.ReleasedDeferredMotionCount,
            ModelInspectionFixtureDeferredEventKind.StaleAnnouncement =>
                evidence.ReleasedDeferredAnnouncementCount,
            _ => throw new AssertFailedException(
                $"No deferred callback counter exists for {kind}.")
        };

    private sealed class RecordingProgress : IProgress<ModelInspectionProgress>
    {
        internal List<ModelInspectionProgress> Values { get; } = [];

        public void Report(ModelInspectionProgress value) => Values.Add(value);
    }

    private sealed class ReentrantProgress : IProgress<ModelInspectionProgress>
    {
        internal List<ModelInspectionProgress> Values { get; } = [];

        internal Action? OnFirstReport { get; set; }

        public void Report(ModelInspectionProgress value)
        {
            Values.Add(value);
            if (Values.Count == 1)
            {
                OnFirstReport?.Invoke();
            }
        }
    }

    private sealed class RecordingAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly Exception? disposeError;

        internal RecordingAnimationDriver(Exception? disposeError = null)
        {
            this.disposeError = disposeError;
        }

        internal int CancelAllCount { get; private set; }

        internal int DisposeCount { get; private set; }

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            throw new AssertFailedException("No animation should start.");

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            throw new AssertFailedException("No animation should start.");

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            throw new AssertFailedException("No animation should start.");

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            throw new AssertFailedException("No animation should start.");

        public void CancelAll() => CancelAllCount++;

        public void Dispose()
        {
            DisposeCount++;
            if (disposeError is not null)
            {
                throw disposeError;
            }
        }
    }
}
#endif
