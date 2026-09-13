using GraniteEdgeAI.Features.ChatModels;

namespace GraniteEdgeAI.ChatBackend.Tests;

[TestClass]
public sealed class ChatModelLibraryTests
{
    private static readonly ChatModelDescriptor GgufDescriptor =
        ChatModelDescriptor.Create(
            "granite-gguf-q4km",
            "Granite 4.0 H Micro",
            ChatModelRoute.Gguf,
            "GGUF · Q4_K_M",
            "CPU · Balanced",
            ChatModelReadiness.Ready);

    [TestMethod]
    public void DescriptorAcceptsOnlyBoundedPresentationSafeMetadata()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ChatModelDescriptor.Create(
            "C:\\models\\granite.gguf",
            "Granite",
            ChatModelRoute.Gguf,
            "GGUF",
            "CPU",
            ChatModelReadiness.Ready));
        Assert.ThrowsExactly<ArgumentException>(() => ChatModelDescriptor.Create(
            "granite",
            "Granite\r\nprivate path",
            ChatModelRoute.Gguf,
            "GGUF",
            "CPU",
            ChatModelReadiness.Ready));
        Assert.ThrowsExactly<ArgumentException>(() => ChatModelDescriptor.Create(
            "granite",
            "C:\\Users\\Student\\models\\granite.gguf",
            ChatModelRoute.Gguf,
            "GGUF",
            "CPU",
            ChatModelReadiness.Ready));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ChatModelDescriptor.Create(
                "granite",
                "Granite",
                (ChatModelRoute)99,
                "GGUF",
                "CPU",
                ChatModelReadiness.Ready));
    }

    [TestMethod]
    public void SnapshotsExposeNoActivationTargetOrPrivateProvenance()
    {
        var library = new ChatModelLibrary();
        var target = new RecordingTarget();
        Assert.AreEqual(
            ChatModelRegistrationDisposition.Added,
            library.Register(GgufDescriptor, target));

        ChatModelSnapshot snapshot = library.Snapshots.Single();

        Assert.AreEqual(GgufDescriptor.Id, snapshot.Id);
        Assert.AreEqual(GgufDescriptor.DisplayName, snapshot.DisplayName);
        Assert.AreEqual(ChatModelRoute.Gguf, snapshot.Route);
        Assert.IsFalse(snapshot.IsActive);
        string[] propertyNames = typeof(ChatModelSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        CollectionAssert.DoesNotContain(propertyNames, "SourcePath");
        CollectionAssert.DoesNotContain(propertyNames, "Sha256");
        CollectionAssert.DoesNotContain(propertyNames, "ActivationTarget");
    }

    [TestMethod]
    public void RegistrationIsIdempotentOnlyForTheExactSameEntry()
    {
        var library = new ChatModelLibrary();
        var target = new RecordingTarget();

        Assert.AreEqual(
            ChatModelRegistrationDisposition.Added,
            library.Register(GgufDescriptor, target));
        Assert.AreEqual(
            ChatModelRegistrationDisposition.AlreadyRegistered,
            library.Register(GgufDescriptor, target));
        Assert.AreEqual(
            ChatModelRegistrationDisposition.Conflict,
            library.Register(CreateDescriptor(
                GgufDescriptor.Id,
                displayName: "Different"), target));
        Assert.AreEqual(
            ChatModelRegistrationDisposition.Conflict,
            library.Register(GgufDescriptor, new RecordingTarget()));
        Assert.AreEqual(1, library.Snapshots.Count);
    }

    [TestMethod]
    public void OneActivationTargetCannotBeOwnedByMultipleRegistrations()
    {
        var library = new ChatModelLibrary();
        var target = new RecordingTarget();
        library.Register(GgufDescriptor, target);

        ChatModelRegistrationDisposition result = library.Register(
            CreateDescriptor("granite-second"),
            target);

        Assert.AreEqual(ChatModelRegistrationDisposition.Conflict, result);
        Assert.AreEqual(1, library.Snapshots.Count);
    }

    [TestMethod]
    public void ReadyEntriesRequireTargetsAndUnreadyEntriesRejectTargets()
    {
        var library = new ChatModelLibrary();
        ChatModelDescriptor pending = CreateDescriptor(
            "granite-pending",
            readiness: ChatModelReadiness.NeedsInspection);

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            library.Register(GgufDescriptor, null));
        Assert.ThrowsExactly<ArgumentException>(() =>
            library.Register(pending, new RecordingTarget()));
        Assert.AreEqual(
            ChatModelRegistrationDisposition.Added,
            library.Register(pending, null));
    }

    [TestMethod]
    public void ActivationTargetRouteMustMatchItsDescriptor()
    {
        var library = new ChatModelLibrary();

        Assert.ThrowsExactly<ArgumentException>(() => library.Register(
            GgufDescriptor,
            new RecordingTarget(route: ChatModelRoute.OpenVino)));
        Assert.AreEqual(0, library.Snapshots.Count);
    }

    [TestMethod]
    public async Task SuccessfulSwitchIsTransactionalAndAlreadyActiveIsIdempotent()
    {
        await using var library = new ChatModelLibrary();
        var target = new RecordingTarget();
        library.Register(GgufDescriptor, target);

        ChatModelSwitchResult first = await library.SwitchAsync(
            GgufDescriptor.Id, CancellationToken.None);
        ChatModelSwitchResult second = await library.SwitchAsync(
            GgufDescriptor.Id, CancellationToken.None);

        Assert.AreEqual(ChatModelSwitchDisposition.Activated, first.Disposition);
        Assert.AreEqual(ChatModelSwitchDisposition.AlreadyActive, second.Disposition);
        Assert.AreEqual(GgufDescriptor.Id, library.ActiveModelId);
        Assert.IsTrue(library.Snapshots.Single().IsActive);
        Assert.AreEqual(1, target.ActivationCount);
    }

    [TestMethod]
    public async Task MissingAndUnreadyModelsNeverInvokeAnActivationTarget()
    {
        await using var library = new ChatModelLibrary();
        ChatModelDescriptor pending = CreateDescriptor(
            "granite-pending",
            readiness: ChatModelReadiness.NeedsInspection);
        library.Register(pending, null);

        ChatModelSwitchResult missing = await library.SwitchAsync(
            "missing", CancellationToken.None);
        ChatModelSwitchResult unready = await library.SwitchAsync(
            pending.Id, CancellationToken.None);

        Assert.AreEqual(ChatModelSwitchDisposition.NotFound, missing.Disposition);
        Assert.AreEqual(ChatModelSwitchDisposition.NotReady, unready.Disposition);
        Assert.IsNull(library.ActiveModelId);
    }

    [TestMethod]
    public async Task FailureAndThrownFaultPreserveThePreviouslyActiveModel()
    {
        await using var library = new ChatModelLibrary();
        var activeTarget = new RecordingTarget();
        var failedTarget = new RecordingTarget(
            ChatModelActivationDisposition.RuntimeUnavailable,
            route: ChatModelRoute.OpenVino);
        var throwingTarget = new RecordingTarget(
            exception: new IOException("private"),
            route: ChatModelRoute.OpenVino);
        ChatModelDescriptor failed = CreateDescriptor(
            "granite-openvino",
            route: ChatModelRoute.OpenVino,
            formatLabel: "OpenVINO · INT8");
        ChatModelDescriptor throwing = CreateDescriptor(
            "granite-throwing",
            route: ChatModelRoute.OpenVino,
            formatLabel: "OpenVINO · INT8");
        library.Register(GgufDescriptor, activeTarget);
        library.Register(failed, failedTarget);
        library.Register(throwing, throwingTarget);
        await library.SwitchAsync(GgufDescriptor.Id, CancellationToken.None);

        ChatModelSwitchResult failedResult = await library.SwitchAsync(
            failed.Id, CancellationToken.None);
        ChatModelSwitchResult thrownResult = await library.SwitchAsync(
            throwing.Id, CancellationToken.None);

        Assert.AreEqual(
            ChatModelSwitchDisposition.RuntimeUnavailable,
            failedResult.Disposition);
        Assert.AreEqual(
            ChatModelSwitchDisposition.ActivationFailed,
            thrownResult.Disposition);
        Assert.AreEqual(GgufDescriptor.Id, library.ActiveModelId);
        Assert.IsFalse(thrownResult.GetType().GetProperties().Any(
            property => property.PropertyType == typeof(Exception)));
    }

    [TestMethod]
    public async Task CancellationPreservesThePreviouslyActiveModel()
    {
        await using var library = new ChatModelLibrary();
        var activeTarget = new RecordingTarget();
        var cancellable = new CancellableTarget();
        ChatModelDescriptor second = CreateDescriptor("granite-second");
        library.Register(GgufDescriptor, activeTarget);
        library.Register(second, cancellable);
        await library.SwitchAsync(GgufDescriptor.Id, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        Task<ChatModelSwitchResult> switching = library.SwitchAsync(
            second.Id, cancellation.Token);
        await cancellable.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        ChatModelSwitchResult result = await switching;

        Assert.AreEqual(ChatModelSwitchDisposition.Cancelled, result.Disposition);
        Assert.AreEqual(GgufDescriptor.Id, library.ActiveModelId);
    }

    [TestMethod]
    public async Task ConcurrentSwitchesRunOneActivationAtATime()
    {
        await using var library = new ChatModelLibrary();
        var concurrency = new ActivationConcurrencyProbe();
        var firstTarget = new GatedTarget(concurrency);
        var secondTarget = new GatedTarget(concurrency);
        ChatModelDescriptor second = CreateDescriptor("granite-second");
        library.Register(GgufDescriptor, firstTarget);
        library.Register(second, secondTarget);

        Task<ChatModelSwitchResult> first = library.SwitchAsync(
            GgufDescriptor.Id, CancellationToken.None);
        await firstTarget.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<ChatModelSwitchResult> secondSwitch = library.SwitchAsync(
            second.Id, CancellationToken.None);
        await Task.Delay(50);

        Assert.AreEqual(1, concurrency.MaximumConcurrent);
        Assert.IsFalse(secondTarget.Started.Task.IsCompleted);
        firstTarget.Release();
        Assert.AreEqual(
            ChatModelSwitchDisposition.Activated,
            (await first).Disposition);
        await secondTarget.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        secondTarget.Release();
        Assert.AreEqual(
            ChatModelSwitchDisposition.Activated,
            (await secondSwitch).Disposition);
        Assert.AreEqual(1, concurrency.MaximumConcurrent);
        Assert.AreEqual(second.Id, library.ActiveModelId);
    }

    [TestMethod]
    public async Task RemoveClearsActiveIdentityAndDisposesItsTargetOnce()
    {
        await using var library = new ChatModelLibrary();
        var target = new RecordingTarget();
        library.Register(GgufDescriptor, target);
        await library.SwitchAsync(GgufDescriptor.Id, CancellationToken.None);

        bool removed = await library.RemoveAsync(
            GgufDescriptor.Id, CancellationToken.None);
        bool removedAgain = await library.RemoveAsync(
            GgufDescriptor.Id, CancellationToken.None);

        Assert.IsTrue(removed);
        Assert.IsFalse(removedAgain);
        Assert.IsNull(library.ActiveModelId);
        Assert.AreEqual(1, target.DisposeCount);
    }

    [TestMethod]
    public async Task DisposalDisposesEveryTargetAndClosesTheLibrary()
    {
        var library = new ChatModelLibrary();
        var first = new RecordingTarget();
        var second = new RecordingTarget();
        library.Register(GgufDescriptor, first);
        library.Register(CreateDescriptor("granite-second"), second);

        await library.DisposeAsync();
        await library.DisposeAsync();

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            library.Register(CreateDescriptor("granite-third"), null));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            library.SwitchAsync(GgufDescriptor.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposalCancelsAnActiveSwitchBeforeDisposingItsTarget()
    {
        var library = new ChatModelLibrary();
        var target = new CancellableTarget();
        library.Register(GgufDescriptor, target);
        using var callerCancellation = new CancellationTokenSource();
        Task<ChatModelSwitchResult> switching = library.SwitchAsync(
            GgufDescriptor.Id,
            callerCancellation.Token);
        await target.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task disposal = library.DisposeAsync().AsTask();
        try
        {
            await disposal.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            callerCancellation.Cancel();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.AreEqual(
            ChatModelSwitchDisposition.Cancelled,
            (await switching).Disposition);
        Assert.AreEqual(1, target.DisposeCount);
    }

    private static ChatModelDescriptor CreateDescriptor(
        string id,
        string displayName = "Granite 4.0 H Micro",
        ChatModelRoute route = ChatModelRoute.Gguf,
        string formatLabel = "GGUF · Q4_K_M",
        ChatModelReadiness readiness = ChatModelReadiness.Ready) =>
        ChatModelDescriptor.Create(
            id,
            displayName,
            route,
            formatLabel,
            "CPU · Balanced",
            readiness);

    private sealed class RecordingTarget(
        ChatModelActivationDisposition disposition =
            ChatModelActivationDisposition.Activated,
        Exception? exception = null,
        ChatModelRoute route = ChatModelRoute.Gguf) : IChatModelActivationTarget
    {
        internal int ActivationCount { get; private set; }
        internal int DisposeCount { get; private set; }
        public ChatModelRoute Route { get; } = route;

        public ValueTask<ChatModelActivationDisposition> ActivateAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActivationCount++;
            return exception is null
                ? ValueTask.FromResult(disposition)
                : ValueTask.FromException<ChatModelActivationDisposition>(exception);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellableTarget : IChatModelActivationTarget
    {
        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int DisposeCount { get; private set; }
        public ChatModelRoute Route => ChatModelRoute.Gguf;

        public async ValueTask<ChatModelActivationDisposition> ActivateAsync(
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ChatModelActivationDisposition.Activated;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class GatedTarget(ActivationConcurrencyProbe concurrency)
        : IChatModelActivationTarget
    {
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ChatModelRoute Route => ChatModelRoute.Gguf;

        internal void Release() => release.TrySetResult();

        public async ValueTask<ChatModelActivationDisposition> ActivateAsync(
            CancellationToken cancellationToken)
        {
            concurrency.Enter();
            Started.TrySetResult();
            try
            {
                await release.Task.WaitAsync(cancellationToken);
                return ChatModelActivationDisposition.Activated;
            }
            finally
            {
                concurrency.Exit();
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ActivationConcurrencyProbe
    {
        private int current;
        internal int MaximumConcurrent { get; private set; }

        internal void Enter()
        {
            int value = Interlocked.Increment(ref current);
            MaximumConcurrent = Math.Max(MaximumConcurrent, value);
        }

        internal void Exit() => Interlocked.Decrement(ref current);
    }
}
