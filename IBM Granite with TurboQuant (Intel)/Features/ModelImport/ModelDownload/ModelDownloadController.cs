using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal enum ModelDownloadStateKind
{
    Unavailable,
    Ready,
    Running,
    Cancelling,
    Succeeded,
    Cancelled,
    Failed
}

internal sealed record ModelDownloadViewState(
    ModelDownloadStateKind Kind,
    string StatusText,
    RecommendedModelDownloadRequest? Request = null,
    ModelDownloadStage? Stage = null,
    double? Fraction = null,
    CompletedModelDownload? CompletedDownload = null,
    ModelDownloadFailure Failure = ModelDownloadFailure.None)
{
    internal static ModelDownloadViewState Unavailable() =>
        new(
            ModelDownloadStateKind.Unavailable,
            "A verified model recommendation is not available yet.");

    internal static ModelDownloadViewState Ready() =>
        new(
            ModelDownloadStateKind.Ready,
            "Ready to download the selected verified model.");
}

internal sealed class ModelDownloadController
{
    private readonly object _gate = new();
    private readonly IRecommendedModelDownloadService _service;
    private long _generation;
    private long? _activeGeneration;
    private long? _cancelledGeneration;
    private CancellationTokenSource? _activeCancellation;
    private RecommendedModelOffer? _lastOffer;
    private int _lastPreferenceValue;
    private ModelDownloadViewState _state = ModelDownloadViewState.Unavailable();

    internal ModelDownloadController(IRecommendedModelDownloadService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    internal event EventHandler<ModelDownloadViewState>? StateChanged;

    internal ModelDownloadViewState State
    {
        get { lock (_gate) { return _state; } }
    }

    internal void MarkReady()
    {
        ModelDownloadViewState next;
        lock (_gate)
        {
            if (_activeGeneration is not null)
            {
                return;
            }
            _state = next = ModelDownloadViewState.Ready();
        }
        StateChanged?.Invoke(this, next);
    }

    internal async Task<bool> TryStartAsync(
        RecommendedModelOffer offer,
        int preferenceValue)
    {
        ArgumentNullException.ThrowIfNull(offer);
        RecommendedModelDownloadRequest request;
        CancellationTokenSource cancellation;
        long generation;
        ModelDownloadViewState started;
        lock (_gate)
        {
            if (_activeGeneration is not null)
            {
                return false;
            }

            generation = checked(++_generation);
            request = new(
                Guid.NewGuid(),
                offer.OfferId,
                preferenceValue);
            cancellation = new CancellationTokenSource();
            _activeGeneration = generation;
            _activeCancellation = cancellation;
            _cancelledGeneration = null;
            _lastOffer = offer;
            _lastPreferenceValue = preferenceValue;
            _state = started = new(
                ModelDownloadStateKind.Running,
                "Preparing the verified model download.",
                request,
                ModelDownloadStage.Resolving,
                Fraction: null);
        }
        StateChanged?.Invoke(this, started);

        ModelDownloadResult result;
        try
        {
            result = await _service.DownloadAsync(
                request,
                new InlineProgress<ModelDownloadProgress>(
                    value => ApplyProgress(generation, request, value)),
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            result = ModelDownloadResult.Cancelled();
        }
        catch
        {
            result = ModelDownloadResult.Failed(
                ModelDownloadFailure.PublicationFailure);
        }

        ModelDownloadViewState completed;
        lock (_gate)
        {
            if (_activeGeneration != generation)
            {
                cancellation.Dispose();
                return true;
            }

            if (_cancelledGeneration == generation)
            {
                result = ModelDownloadResult.Cancelled();
            }
            completed = ToTerminalState(request, result);
            _state = completed;
            _activeGeneration = null;
            _activeCancellation = null;
            _cancelledGeneration = null;
        }
        cancellation.Dispose();
        StateChanged?.Invoke(this, completed);
        return true;
    }

    internal bool TryCancel()
    {
        CancellationTokenSource cancellation;
        ModelDownloadViewState cancelling;
        lock (_gate)
        {
            if (_activeGeneration is not long generation
                || _activeCancellation is null
                || _state.Kind != ModelDownloadStateKind.Running)
            {
                return false;
            }
            _cancelledGeneration = generation;
            cancellation = _activeCancellation;
            _state = cancelling = _state with
            {
                Kind = ModelDownloadStateKind.Cancelling,
                StatusText = "Cancelling the download and cleaning up."
            };
        }
        StateChanged?.Invoke(this, cancelling);
        cancellation.Cancel();
        return true;
    }

    internal Task<bool> TryRetryAsync()
    {
        RecommendedModelOffer? offer;
        int preference;
        lock (_gate)
        {
            if (_activeGeneration is not null
                || _state.Kind is not (ModelDownloadStateKind.Cancelled
                    or ModelDownloadStateKind.Failed)
                || _lastOffer is null)
            {
                return Task.FromResult(false);
            }
            offer = _lastOffer;
            preference = _lastPreferenceValue;
        }
        return TryStartAsync(offer, preference);
    }

    private void ApplyProgress(
        long generation,
        RecommendedModelDownloadRequest request,
        ModelDownloadProgress progress)
    {
        ModelDownloadViewState next;
        lock (_gate)
        {
            if (_activeGeneration != generation
                || _cancelledGeneration == generation)
            {
                return;
            }
            _state = next = new(
                ModelDownloadStateKind.Running,
                ProgressText(progress.Stage),
                request,
                progress.Stage,
                progress.Fraction);
        }
        StateChanged?.Invoke(this, next);
    }

    private static ModelDownloadViewState ToTerminalState(
        RecommendedModelDownloadRequest request,
        ModelDownloadResult result) => result.Kind switch
        {
            ModelDownloadResultKind.Succeeded when result.CompletedDownload is not null =>
                new(
                    ModelDownloadStateKind.Succeeded,
                    "The model was downloaded, verified, and published.",
                    request,
                    CompletedDownload: result.CompletedDownload),
            ModelDownloadResultKind.Cancelled =>
                new(
                    ModelDownloadStateKind.Cancelled,
                    "Download cancelled. No model was published.",
                    request),
            ModelDownloadResultKind.Failed =>
                new(
                    ModelDownloadStateKind.Failed,
                    FailureText(result.Failure),
                    request,
                    Failure: result.Failure),
            _ => new(
                ModelDownloadStateKind.Failed,
                FailureText(ModelDownloadFailure.PublicationFailure),
                request,
                Failure: ModelDownloadFailure.PublicationFailure)
        };

    private static string ProgressText(ModelDownloadStage stage) => stage switch
    {
        ModelDownloadStage.Resolving => "Preparing the verified model download.",
        ModelDownloadStage.Transferring => "Downloading the selected model.",
        ModelDownloadStage.Verifying => "Verifying model integrity.",
        ModelDownloadStage.Publishing => "Publishing the verified model.",
        ModelDownloadStage.CleaningUp => "Cleaning up incomplete download data.",
        _ => "Processing the model download."
    };

    private static string FailureText(ModelDownloadFailure failure) => failure switch
    {
        ModelDownloadFailure.IntegrityMismatch =>
            "The downloaded model failed its integrity check. Nothing was published.",
        ModelDownloadFailure.InsufficientSpace =>
            "There is not enough space to download and verify this model.",
        ModelDownloadFailure.CleanupFailure =>
            "The download stopped, but cleanup could not be confirmed. No model was published.",
        _ =>
            "The model could not be published. Nothing was added to your models."
    };

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
