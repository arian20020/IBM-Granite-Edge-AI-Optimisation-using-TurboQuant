using System;
using System.Diagnostics;
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
    private long? _cancellationFailureGeneration;
    private CancellationTokenSource? _activeCancellation;
    private Task<bool>? _activeTask;
    private RecommendedModelOffer? _lastOffer;
    private int _lastPreferenceValue;
    private bool _retired;
    private Task? _retirementTask;
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
            if (_retired || _activeGeneration is not null)
            {
                return;
            }
            _state = next = ModelDownloadViewState.Ready();
        }
        PublishState(next);
    }

    internal Task<bool> TryStartAsync(
        RecommendedModelOffer offer,
        int preferenceValue)
    {
        ArgumentNullException.ThrowIfNull(offer);
        RecommendedModelDownloadRequest request;
        CancellationTokenSource cancellation;
        long generation;
        ModelDownloadViewState started;
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_retired || _activeGeneration is not null)
            {
                return Task.FromResult(false);
            }

            generation = checked(++_generation);
            request = new(
                Guid.NewGuid(),
                offer.OfferId,
                preferenceValue);
            cancellation = new CancellationTokenSource();
            _activeGeneration = generation;
            _activeCancellation = cancellation;
            _activeTask = completion.Task;
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
        PublishState(started);
        _ = CompleteDownloadAsync(
            generation,
            request,
            cancellation,
            completion);
        return completion.Task;
    }

    private async Task CompleteDownloadAsync(
        long generation,
        RecommendedModelDownloadRequest request,
        CancellationTokenSource cancellation,
        TaskCompletionSource<bool> completion)
    {
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
        result ??= ModelDownloadResult.Failed(
            ModelDownloadFailure.PublicationFailure);

        // Cancellation callbacks can complete the provider task inline and then
        // throw. Defer terminalization until TryCancel has recorded whether the
        // cancellation request itself succeeded, while preserving the caller's
        // synchronization context for UI state publication.
        await Task.Yield();

        ModelDownloadViewState? completed = null;
        lock (_gate)
        {
            if (_activeGeneration != generation)
            {
                cancellation.Dispose();
                completion.TrySetResult(true);
                return;
            }

            if (_retired)
            {
                _state = ModelDownloadViewState.Unavailable();
            }
            else if (_cancellationFailureGeneration == generation)
            {
                completed = ToTerminalState(
                    request,
                    ModelDownloadResult.Failed(
                        ModelDownloadFailure.CleanupFailure));
                _state = completed;
            }
            else if (_cancelledGeneration == generation
                && result.Kind != ModelDownloadResultKind.Failed)
            {
                result = ModelDownloadResult.Cancelled();
                completed = ToTerminalState(request, result);
                _state = completed;
            }
            else
            {
                completed = ToTerminalState(request, result);
                _state = completed;
            }
            _activeGeneration = null;
            _activeCancellation = null;
            _activeTask = null;
            _cancelledGeneration = null;
            _cancellationFailureGeneration = null;
        }
        cancellation.Dispose();
        if (completed is not null)
        {
            PublishState(completed);
        }
        completion.TrySetResult(true);
    }

    internal bool TryCancel()
    {
        CancellationTokenSource cancellation;
        ModelDownloadViewState cancelling;
        long generation;
        lock (_gate)
        {
            if (_retired
                || _activeGeneration is not long activeGeneration
                || _activeCancellation is null
                || _state.Kind != ModelDownloadStateKind.Running)
            {
                return false;
            }
            generation = activeGeneration;
            _cancelledGeneration = generation;
            cancellation = _activeCancellation;
            _state = cancelling = _state with
            {
                Kind = ModelDownloadStateKind.Cancelling,
                StatusText = "Cancelling the download and cleaning up."
            };
        }
        PublishState(cancelling);
        lock (_gate)
        {
            if (_activeGeneration == generation
                && !RequestCancellation(cancellation))
            {
                _cancellationFailureGeneration = generation;
            }
        }
        return true;
    }

    internal Task RetireAsync()
    {
        lock (_gate)
        {
            if (_retirementTask is not null)
            {
                return _retirementTask;
            }

            _retired = true;
            CancellationTokenSource? cancellation = _activeCancellation;
            Task activeTask = _activeTask ?? Task.CompletedTask;
            _state = ModelDownloadViewState.Unavailable();
            _retirementTask = RetireCoreAsync(cancellation, activeTask);
            return _retirementTask;
        }
    }

    private static async Task RetireCoreAsync(
        CancellationTokenSource? cancellation,
        Task activeTask)
    {
        if (cancellation is not null)
        {
            RequestCancellation(cancellation);
        }
        await activeTask.ConfigureAwait(false);
    }

    internal Task<bool> TryRetryAsync()
    {
        RecommendedModelOffer? offer;
        int preference;
        lock (_gate)
        {
            if (_retired
                || _activeGeneration is not null
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
            if (_retired
                || _activeGeneration != generation
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
        PublishState(next);
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

    private void PublishState(ModelDownloadViewState state)
    {
        Delegate[] handlers = StateChanged?.GetInvocationList() ?? [];
        foreach (Delegate candidate in handlers)
        {
            try
            {
                ((EventHandler<ModelDownloadViewState>)candidate)(this, state);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "A model-download state observer failed with {0}.",
                    exception.GetType().Name);
            }
        }
    }

    private static bool RequestCancellation(
        CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
            return true;
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
