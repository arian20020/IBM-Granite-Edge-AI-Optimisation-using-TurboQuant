using System.Diagnostics;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed record ModelDownloadCoordinatorState(
    ModelDownloadOperationId? OperationId,
    ModelDownloadStage Stage,
    string PreferenceLabel,
    string Quantisation,
    string DownloadSizeText,
    long DownloadedBytes,
    long TotalBytes,
    string? ErrorCode)
{
    internal static ModelDownloadCoordinatorState Idle(ModelDownloadCatalogEntry entry) =>
        new(null, ModelDownloadStage.Idle, entry.PreferenceLabel, entry.Quantisation,
            entry.DownloadSizeText, 0, entry.ExpectedByteLength, null);
}

internal sealed class VerifiedModelAvailableEventArgs(
    ModelDownloadOperationId operationId,
    string displayName) : EventArgs
{
    internal ModelDownloadOperationId OperationId { get; } = operationId;
    internal string DisplayName { get; } = displayName;

    public override string ToString() =>
        $"VerifiedModelAvailable {{ OperationId = {OperationId.Value}, DisplayName = {DisplayName} }}";
}

internal sealed class ModelDownloadCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly IModelDownloadService _service;
    private readonly IModelDownloadNetworkPolicy _networkPolicy;
    private readonly TimeSpan _retirementTimeout;
    private CancellationTokenSource? _activeCancellation;
    private TaskCompletionSource? _activeCompletion;
    private VerifiedDownloadedModel? _claimableModel;
    private ModelDownloadOperationId? _claimableOperation;
    private ModelDownloadOperationId? _authorizedHandoffOperation;
    private bool _automaticHandoffRetired;
    private bool _cancellationFailed;
    private ModelDownloadOperationId? _cancellationRequestedOperation;
    private CancellationTokenSource? _recoveryCancellation;
    private TaskCompletionSource? _recoveryCompletion;
    private bool _disposed;
    private Task? _retirementTask;

    internal ModelDownloadCoordinator(
        IModelDownloadService service,
        IModelDownloadNetworkPolicy networkPolicy,
        TimeSpan? retirementTimeout = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _networkPolicy = networkPolicy ?? throw new ArgumentNullException(nameof(networkPolicy));
        _retirementTimeout = retirementTimeout ?? TimeSpan.FromSeconds(5);
        if (_retirementTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retirementTimeout));
        State = ModelDownloadCoordinatorState.Idle(PinnedGraniteModelCatalog.ForSliderValue(50));
    }

    internal ModelDownloadCoordinatorState State { get; private set; }
    internal event EventHandler<ModelDownloadCoordinatorState>? StateChanged;
    internal event EventHandler<VerifiedModelAvailableEventArgs>? VerifiedModelAvailable;

    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource linked;
        TaskCompletionSource completion;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_recoveryCancellation is not null)
            {
                return;
            }
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _recoveryCancellation = linked;
            _recoveryCompletion = completion;
        }
        try
        {
            foreach (ModelDownloadCatalogEntry entry in PinnedGraniteModelCatalog.Entries)
            {
                linked.Token.ThrowIfCancellationRequested();
                ModelDownloadResumeInfo? resume = await _service.GetResumeInfoAsync(entry, linked.Token);
                if (resume is null) continue;

                ModelDownloadConnectionKind connection = _networkPolicy.GetCurrentConnectionKind();
                lock (_sync)
                {
                    if (_disposed || _activeCancellation is not null || State.OperationId is not null) return;
                }
                if (connection == ModelDownloadConnectionKind.Unrestricted)
                {
                    await StartAsync(entry.MinimumSliderValue, allowMetered: false, linked.Token);
                    return;
                }

            ModelDownloadOperationId operationId = ModelDownloadOperationId.CreateNew();
            string errorCode = connection == ModelDownloadConnectionKind.Offline
                ? "download-offline"
                : "download-network-confirmation-required";
                PublishRecovered(operationId, entry, resume.DownloadedBytes, errorCode);
                return;
            }
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_recoveryCancellation, linked))
                {
                    _recoveryCancellation = null;
                    _recoveryCompletion = null;
                }
            }
            linked.Dispose();
            completion.TrySetResult();
        }
    }

    internal async Task StartAsync(double sliderValue, bool allowMetered, CancellationToken cancellationToken)
    {
        ModelDownloadCatalogEntry entry = PinnedGraniteModelCatalog.ForSliderValue(sliderValue);
        ModelDownloadOperationId operationId = ModelDownloadOperationId.CreateNew();
        CancellationTokenSource linked;
        TaskCompletionSource completion;
        ModelDownloadCoordinatorState started;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_activeCancellation is not null)
            {
                throw new InvalidOperationException("A model download is already active.");
            }

            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeCancellation = linked;
            _activeCompletion = completion;
            _claimableModel = null;
            _claimableOperation = null;
            _authorizedHandoffOperation = null;
            _automaticHandoffRetired = false;
            _cancellationFailed = false;
            _cancellationRequestedOperation = null;
            started = new(operationId, ModelDownloadStage.Preparing,
                entry.PreferenceLabel, entry.Quantisation, entry.DownloadSizeText,
                0, entry.ExpectedByteLength, null);
            State = started;
        }
        PublishState(started);

        try
        {
            ModelDownloadConnectionKind connection = _networkPolicy.GetCurrentConnectionKind();
            if (connection == ModelDownloadConnectionKind.Offline)
            {
                Publish(operationId, entry, ModelDownloadStage.Interrupted, 0, "download-offline");
                return;
            }

            if (connection == ModelDownloadConnectionKind.ConfirmationRequired && !allowMetered)
            {
                Publish(operationId, entry, ModelDownloadStage.Interrupted, 0, "download-network-confirmation-required");
                return;
            }

            var progress = new InlineProgress<ModelDownloadProgress>(value =>
                Publish(operationId, entry, value.Stage, value.DownloadedBytes, null));
            ModelDownloadResult result = await _service.DownloadAsync(entry, progress, linked.Token);
            await Task.Yield();
            if (!IsCurrent(operationId))
            {
                return;
            }

            bool cancellationFailed;
            bool cancellationWon;
            lock (_sync)
            {
                cancellationFailed = _cancellationFailed;
                cancellationWon = _cancellationRequestedOperation == operationId;
            }
            ModelDownloadStage stage = cancellationFailed
                ? ModelDownloadStage.Failed
                : cancellationWon
                    ? ModelDownloadStage.Interrupted
                : result.Kind switch
            {
                ModelDownloadResultKind.Completed or ModelDownloadResultKind.AlreadyAvailable => ModelDownloadStage.Completed,
                ModelDownloadResultKind.Interrupted => ModelDownloadStage.Interrupted,
                _ => ModelDownloadStage.Failed
            };
            Publish(operationId, entry, stage,
                result.VerifiedModel?.ByteLength ?? State.DownloadedBytes,
                cancellationFailed ? "download-cancellation-cleanup-failed"
                    : cancellationWon ? "download-cancelled" : result.ErrorCode);

            if (result.VerifiedModel is not null && stage == ModelDownloadStage.Completed)
            {
                bool notify;
                lock (_sync)
                {
                    notify = !_automaticHandoffRetired && IsCurrentUnsafe(operationId);
                    if (notify)
                    {
                        _claimableModel = result.VerifiedModel;
                        _claimableOperation = operationId;
                        _authorizedHandoffOperation = operationId;
                    }
                }

                if (notify)
                {
                    PublishVerifiedModelAvailable(
                        new VerifiedModelAvailableEventArgs(operationId, entry.FileName));
                }
            }
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_activeCancellation, linked))
                {
                    _activeCancellation = null;
                    _activeCompletion = null;
                }
            }
            linked.Dispose();
            completion.TrySetResult();
        }
    }

    internal async Task CancelAsync(bool discardPartial, CancellationToken cancellationToken)
    {
        ModelDownloadCatalogEntry entry;
        CancellationTokenSource? active;
        Task? completion;
        ModelDownloadOperationId? operationId;
        lock (_sync)
        {
            ThrowIfDisposed();
            active = _activeCancellation;
            completion = _activeCompletion?.Task;
            operationId = State.OperationId;
            entry = PinnedGraniteModelCatalog.Entries.First(value => value.Quantisation == State.Quantisation);
            _automaticHandoffRetired = true;
            _claimableModel = null;
            _claimableOperation = null;
            _authorizedHandoffOperation = null;
        }
        if (operationId is null)
        {
            return;
        }

        if (active is null)
        {
            if (discardPartial && State.Stage == ModelDownloadStage.Interrupted)
            {
                await _service.DiscardPartialAsync(entry, cancellationToken);
                Publish(operationId.Value, entry, ModelDownloadStage.Interrupted, 0, "download-cancelled-discarded");
            }
            return;
        }

        ModelDownloadCoordinatorState cancelling;
        lock (_sync)
        {
            cancelling = State with { ErrorCode = "download-cancelling" };
            State = cancelling;
        }
        Task<bool> cancellationRequest = RequestCancellationAsync(active);
        lock (_sync)
        {
            _cancellationRequestedOperation = operationId;
        }
        PublishState(cancelling);
        try
        {
            await Task.WhenAll(completion ?? Task.CompletedTask, cancellationRequest)
                .WaitAsync(_retirementTimeout, cancellationToken);
            lock (_sync) _cancellationFailed = !cancellationRequest.Result;
        }
        catch (TimeoutException)
        {
            lock (_sync) _cancellationFailed = true;
            Trace.TraceWarning("Model-download cancellation detached after its five-second cleanup boundary.");
        }
        if (discardPartial)
        {
            await _service.DiscardPartialAsync(entry, cancellationToken);
            bool cancellationFailed;
            lock (_sync)
            {
                cancellationFailed = _cancellationFailed;
            }
            ModelDownloadCoordinatorState cancelled = new(
                operationId,
                cancellationFailed ? ModelDownloadStage.Failed : ModelDownloadStage.Interrupted,
                entry.PreferenceLabel,
                entry.Quantisation,
                entry.DownloadSizeText,
                0,
                entry.ExpectedByteLength,
                cancellationFailed
                    ? "download-cancellation-cleanup-failed"
                    : "download-cancelled-discarded");
            lock (_sync)
            {
                State = cancelled;
            }
            PublishState(cancelled);
        }
    }

    internal Task ResumeAsync(bool allowMetered, CancellationToken cancellationToken)
    {
        double slider = PinnedGraniteModelCatalog.Entries
            .First(value => value.Quantisation == State.Quantisation)
            .MinimumSliderValue;
        return StartAsync(slider, allowMetered, cancellationToken);
    }

    internal void RetireAutomaticHandoff()
    {
        lock (_sync)
        {
            _automaticHandoffRetired = true;
            _claimableModel = null;
            _claimableOperation = null;
            _authorizedHandoffOperation = null;
        }
    }

    internal bool IsAutomaticHandoffAuthorized(ModelDownloadOperationId operationId)
    {
        lock (_sync)
        {
            return !_automaticHandoffRetired && _authorizedHandoffOperation == operationId;
        }
    }

    internal bool TryClaimVerifiedModel(ModelDownloadOperationId operationId, out VerifiedDownloadedModel? model)
    {
        lock (_sync)
        {
            if (!_automaticHandoffRetired && _claimableOperation == operationId && _claimableModel is not null)
            {
                model = _claimableModel;
                _claimableModel = null;
                _claimableOperation = null;
                return true;
            }
        }
        model = null;
        return false;
    }

    private void Publish(ModelDownloadOperationId operationId, ModelDownloadCatalogEntry entry,
        ModelDownloadStage stage, long downloadedBytes, string? errorCode)
    {
        ModelDownloadCoordinatorState next;
        lock (_sync)
        {
            if (_disposed || (State.OperationId is not null && State.OperationId != operationId))
            {
                return;
            }
            next = new(operationId, stage, entry.PreferenceLabel, entry.Quantisation,
                entry.DownloadSizeText, Math.Clamp(downloadedBytes, 0, entry.ExpectedByteLength),
                entry.ExpectedByteLength, errorCode);
            State = next;
        }
        PublishState(next);
    }

    private void PublishRecovered(
        ModelDownloadOperationId operationId,
        ModelDownloadCatalogEntry entry,
        long downloadedBytes,
        string errorCode)
    {
        ModelDownloadCoordinatorState recovered = new(
            operationId,
            ModelDownloadStage.Interrupted,
            entry.PreferenceLabel,
            entry.Quantisation,
            entry.DownloadSizeText,
            downloadedBytes,
            entry.ExpectedByteLength,
            errorCode);
        lock (_sync)
        {
            if (_disposed || _activeCancellation is not null || State.OperationId is not null)
            {
                return;
            }
            State = recovered;
        }
        PublishState(recovered);
    }

    private bool IsCurrent(ModelDownloadOperationId operationId)
    {
        lock (_sync) return IsCurrentUnsafe(operationId);
    }

    private bool IsCurrentUnsafe(ModelDownloadOperationId operationId) =>
        !_disposed && State.OperationId == operationId;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    internal Task RetireAsync()
    {
        lock (_sync)
        {
            if (_retirementTask is not null)
            {
                return _retirementTask;
            }
            _disposed = true;
            _automaticHandoffRetired = true;
            _claimableModel = null;
            _claimableOperation = null;
            _authorizedHandoffOperation = null;
            CancellationTokenSource? cancellation = _activeCancellation;
            Task activeTask = _activeCompletion?.Task ?? Task.CompletedTask;
            CancellationTokenSource? recoveryCancellation = _recoveryCancellation;
            Task recoveryTask = _recoveryCompletion?.Task ?? Task.CompletedTask;
            _retirementTask = RetireCoreAsync(cancellation, activeTask, recoveryCancellation, recoveryTask, _retirementTimeout);
            return _retirementTask;
        }
    }

    public void Dispose() => _ = RetireAsync();

    private static async Task RetireCoreAsync(
        CancellationTokenSource? cancellation,
        Task activeTask,
        CancellationTokenSource? recoveryCancellation,
        Task recoveryTask,
        TimeSpan retirementTimeout)
    {
        if (cancellation is not null)
        {
            _ = RequestCancellationAsync(cancellation);
        }
        if (recoveryCancellation is not null)
        {
            _ = RequestCancellationAsync(recoveryCancellation);
        }
        try
        {
            await Task.WhenAll(activeTask, recoveryTask).WaitAsync(retirementTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Trace.TraceWarning("Model-download retirement detached after its five-second cleanup boundary.");
        }
    }

    private void PublishState(ModelDownloadCoordinatorState state)
    {
        Delegate[] handlers = StateChanged?.GetInvocationList() ?? [];
        foreach (Delegate candidate in handlers)
        {
            try
            {
                ((EventHandler<ModelDownloadCoordinatorState>)candidate)(this, state);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "A model-download state observer failed with {0}.",
                    exception.GetType().Name);
            }
        }
    }

    private void PublishVerifiedModelAvailable(VerifiedModelAvailableEventArgs value)
    {
        Delegate[] handlers = VerifiedModelAvailable?.GetInvocationList() ?? [];
        foreach (Delegate candidate in handlers)
        {
            try
            {
                ((EventHandler<VerifiedModelAvailableEventArgs>)candidate)(this, value);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "A verified-model observer failed with {0}.",
                    exception.GetType().Name);
            }
        }
    }

    private static async Task<bool> RequestCancellationAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Model-download cancellation observed {0}.", exception.GetType().Name);
            return false;
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
