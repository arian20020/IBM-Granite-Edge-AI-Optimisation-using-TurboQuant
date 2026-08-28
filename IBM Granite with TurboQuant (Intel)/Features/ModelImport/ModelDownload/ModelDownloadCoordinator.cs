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
    private CancellationTokenSource? _activeCancellation;
    private TaskCompletionSource? _activeCompletion;
    private VerifiedDownloadedModel? _claimableModel;
    private ModelDownloadOperationId? _claimableOperation;
    private ModelDownloadOperationId? _authorizedHandoffOperation;
    private bool _automaticHandoffRetired;
    private bool _disposed;

    internal ModelDownloadCoordinator(
        IModelDownloadService service,
        IModelDownloadNetworkPolicy networkPolicy)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _networkPolicy = networkPolicy ?? throw new ArgumentNullException(nameof(networkPolicy));
        State = ModelDownloadCoordinatorState.Idle(PinnedGraniteModelCatalog.ForSliderValue(50));
    }

    internal ModelDownloadCoordinatorState State { get; private set; }
    internal event EventHandler<ModelDownloadCoordinatorState>? StateChanged;
    internal event EventHandler<VerifiedModelAvailableEventArgs>? VerifiedModelAvailable;

    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {
        foreach (ModelDownloadCatalogEntry entry in PinnedGraniteModelCatalog.Entries)
        {
            ModelDownloadResumeInfo? resume = await _service.GetResumeInfoAsync(entry, cancellationToken);
            if (resume is null)
            {
                continue;
            }

            ModelDownloadConnectionKind connection = _networkPolicy.GetCurrentConnectionKind();
            if (connection == ModelDownloadConnectionKind.Unrestricted)
            {
                await StartAsync(entry.MinimumSliderValue, allowMetered: false, cancellationToken);
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
            started = new(operationId, ModelDownloadStage.Preparing,
                entry.PreferenceLabel, entry.Quantisation, entry.DownloadSizeText,
                0, entry.ExpectedByteLength, null);
            State = started;
        }
        StateChanged?.Invoke(this, started);

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
            if (!IsCurrent(operationId))
            {
                return;
            }

            ModelDownloadStage stage = result.Kind switch
            {
                ModelDownloadResultKind.Completed or ModelDownloadResultKind.AlreadyAvailable => ModelDownloadStage.Completed,
                ModelDownloadResultKind.Interrupted => ModelDownloadStage.Interrupted,
                _ => ModelDownloadStage.Failed
            };
            Publish(operationId, entry, stage,
                result.VerifiedModel?.ByteLength ?? State.DownloadedBytes, result.ErrorCode);

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
                    VerifiedModelAvailable?.Invoke(this,
                        new VerifiedModelAvailableEventArgs(operationId, result.VerifiedModel.DisplayName));
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
            active = _activeCancellation;
            completion = _activeCompletion?.Task;
            operationId = State.OperationId;
            entry = PinnedGraniteModelCatalog.Entries.First(value => value.Quantisation == State.Quantisation);
            _automaticHandoffRetired = true;
            _claimableModel = null;
            _claimableOperation = null;
            _authorizedHandoffOperation = null;
        }
        active?.Cancel();
        if (completion is not null)
        {
            await completion.WaitAsync(cancellationToken);
        }
        if (discardPartial)
        {
            await _service.DiscardPartialAsync(entry, cancellationToken);
            ModelDownloadCoordinatorState cancelled = new(
                operationId,
                ModelDownloadStage.Interrupted,
                entry.PreferenceLabel,
                entry.Quantisation,
                entry.DownloadSizeText,
                0,
                entry.ExpectedByteLength,
                "download-cancelled-discarded");
            lock (_sync)
            {
                State = cancelled;
            }
            StateChanged?.Invoke(this, cancelled);
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
            if (State.OperationId is not null && State.OperationId != operationId)
            {
                return;
            }
            next = new(operationId, stage, entry.PreferenceLabel, entry.Quantisation,
                entry.DownloadSizeText, Math.Clamp(downloadedBytes, 0, entry.ExpectedByteLength),
                entry.ExpectedByteLength, errorCode);
            State = next;
        }
        StateChanged?.Invoke(this, next);
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
            State = recovered;
        }
        StateChanged?.Invoke(this, recovered);
    }

    private bool IsCurrent(ModelDownloadOperationId operationId)
    {
        lock (_sync) return IsCurrentUnsafe(operationId);
    }

    private bool IsCurrentUnsafe(ModelDownloadOperationId operationId) => State.OperationId == operationId;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _activeCancellation?.Cancel();
            _claimableModel = null;
            _authorizedHandoffOperation = null;
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
