using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal enum OptimizationExportStateKind
{
    Unbound,
    Ready,
    Running,
    Cancelling,
    Succeeded,
    Cancelled,
    Failed
}

internal sealed record OptimizationExportViewState(
    OptimizationExportStateKind Kind,
    string StatusText,
    VerifiedPersistentExportTarget? Target = null,
    OptimizationExportStage? Stage = null,
    double? Fraction = null,
    OptimizationExportReceipt? Receipt = null,
    OptimizationExportFailure Failure = OptimizationExportFailure.None)
{
    internal static OptimizationExportViewState Unbound() =>
        new(
            OptimizationExportStateKind.Unbound,
            "Export is unavailable until this exact model result is verified.");
}

internal sealed class OptimizationExportController
{
    private readonly object _gate = new();
    private readonly IOptimizationExportService _service;
    private long _generation;
    private long? _activeGeneration;
    private long? _cancelledGeneration;
    private long? _cancellationFailureGeneration;
    private CancellationTokenSource? _activeCancellation;
    private Task<bool>? _activeTask;
    private VerifiedPersistentExportTarget? _target;
    private bool _retired;
    private Task? _retirementTask;
    private OptimizationExportViewState _state = OptimizationExportViewState.Unbound();

    internal OptimizationExportController(IOptimizationExportService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    internal event EventHandler<OptimizationExportViewState>? StateChanged;

    internal OptimizationExportViewState State
    {
        get { lock (_gate) { return _state; } }
    }

    internal void Bind(VerifiedPersistentExportTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        OptimizationExportViewState next;
        lock (_gate)
        {
            if (_retired)
            {
                throw new InvalidOperationException(
                    "A retired export controller cannot be rebound.");
            }
            if (_activeGeneration is not null)
            {
                throw new InvalidOperationException(
                    "An active export cannot be rebound.");
            }
            _target = target;
            _state = next = new(
                OptimizationExportStateKind.Ready,
                "Ready to save this verified model result.",
                target);
        }
        PublishState(next);
    }

    internal Task<bool> TryStartAsync()
    {
        VerifiedPersistentExportTarget target;
        CancellationTokenSource cancellation;
        OptimizationExportViewState started;
        long generation;
        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_retired
                || _activeGeneration is not null
                || _target is null
                || _state.Kind is not (OptimizationExportStateKind.Ready
                    or OptimizationExportStateKind.Cancelled
                    or OptimizationExportStateKind.Failed))
            {
                return Task.FromResult(false);
            }
            target = _target;
            generation = checked(++_generation);
            cancellation = new CancellationTokenSource();
            _activeGeneration = generation;
            _activeCancellation = cancellation;
            _activeTask = completion.Task;
            _cancelledGeneration = null;
            _state = started = new(
                OptimizationExportStateKind.Running,
                "Choose where to save the verified model.",
                target,
                OptimizationExportStage.ChoosingDestination);
        }
        PublishState(started);
        _ = CompleteExportAsync(
            generation,
            target,
            cancellation,
            completion);
        return completion.Task;
    }

    private async Task CompleteExportAsync(
        long generation,
        VerifiedPersistentExportTarget target,
        CancellationTokenSource cancellation,
        TaskCompletionSource<bool> completion)
    {
        OptimizationExportResult result;
        try
        {
            result = await _service.ExportAsync(
                target,
                new InlineProgress<OptimizationExportProgress>(
                    value => ApplyProgress(generation, target, value)),
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            result = OptimizationExportResult.Cancelled();
        }
        catch
        {
            result = OptimizationExportResult.Failed(
                OptimizationExportFailure.PublicationFailure);
        }
        result ??= OptimizationExportResult.Failed(
            OptimizationExportFailure.PublicationFailure);

        // Cancellation callbacks can complete the provider task inline and then
        // throw. Defer terminalization until TryCancel has recorded whether the
        // cancellation request itself succeeded, while preserving the caller's
        // synchronization context for UI state publication.
        await Task.Yield();

        OptimizationExportViewState? completed = null;
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
                _target = null;
                _state = OptimizationExportViewState.Unbound();
            }
            else if (_cancellationFailureGeneration == generation)
            {
                completed = ToTerminalState(
                    target,
                    OptimizationExportResult.Failed(
                        OptimizationExportFailure.CleanupFailure));
                _state = completed;
            }
            else if (_cancelledGeneration == generation
                && result.Kind != OptimizationExportResultKind.Failed)
            {
                result = OptimizationExportResult.Cancelled();
                completed = ToTerminalState(target, result);
                _state = completed;
            }
            else
            {
                completed = ToTerminalState(target, result);
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
        OptimizationExportViewState cancelling;
        long generation;
        lock (_gate)
        {
            if (_retired
                || _activeGeneration is not long activeGeneration
                || _activeCancellation is null
                || _state.Kind != OptimizationExportStateKind.Running)
            {
                return false;
            }
            generation = activeGeneration;
            _cancelledGeneration = generation;
            cancellation = _activeCancellation;
            _state = cancelling = _state with
            {
                Kind = OptimizationExportStateKind.Cancelling,
                StatusText = "Cancelling export and cleaning up incomplete output."
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
            _target = null;
            _state = OptimizationExportViewState.Unbound();
            CancellationTokenSource? cancellation = _activeCancellation;
            Task activeTask = _activeTask ?? Task.CompletedTask;
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
        lock (_gate)
        {
            if (_retired
                || _activeGeneration is not null
                || _state.Kind is not (OptimizationExportStateKind.Cancelled
                    or OptimizationExportStateKind.Failed)
                || _target is null)
            {
                return Task.FromResult(false);
            }
        }
        return TryStartAsync();
    }

    private void ApplyProgress(
        long generation,
        VerifiedPersistentExportTarget target,
        OptimizationExportProgress progress)
    {
        OptimizationExportViewState next;
        lock (_gate)
        {
            if (_retired
                || _activeGeneration != generation
                || _cancelledGeneration == generation)
            {
                return;
            }
            _state = next = new(
                OptimizationExportStateKind.Running,
                ProgressText(progress.Stage),
                target,
                progress.Stage,
                progress.Fraction);
        }
        PublishState(next);
    }

    private static OptimizationExportViewState ToTerminalState(
        VerifiedPersistentExportTarget target,
        OptimizationExportResult result)
    {
        if (result.Kind == OptimizationExportResultKind.Succeeded
            && result.Receipt is not null)
        {
            if (!string.Equals(
                    target.OutputManifestSha256,
                    result.Receipt.Sha256,
                    StringComparison.Ordinal)
                || target.OutputLengthBytes != result.Receipt.LengthBytes)
            {
                return Failed(target, OptimizationExportFailure.IntegrityMismatch);
            }
            return new(
                OptimizationExportStateKind.Succeeded,
                "The verified model was saved successfully.",
                target,
                Receipt: result.Receipt);
        }
        if (result.Kind == OptimizationExportResultKind.Cancelled)
        {
            return new(
                OptimizationExportStateKind.Cancelled,
                "Export cancelled. The verified model in the app is unchanged.",
                target);
        }
        return Failed(
            target,
            result.Failure == OptimizationExportFailure.None
                ? OptimizationExportFailure.PublicationFailure
                : result.Failure);
    }

    private static OptimizationExportViewState Failed(
        VerifiedPersistentExportTarget target,
        OptimizationExportFailure failure) =>
        new(
            OptimizationExportStateKind.Failed,
            FailureText(failure),
            target,
            Failure: failure);

    private static string ProgressText(OptimizationExportStage stage) => stage switch
    {
        OptimizationExportStage.ChoosingDestination =>
            "Choose where to save the verified model.",
        OptimizationExportStage.Writing =>
            "Saving the verified model.",
        OptimizationExportStage.Verifying =>
            "Verifying the saved model.",
        OptimizationExportStage.Publishing =>
            "Finishing the saved model.",
        OptimizationExportStage.CleaningUp =>
            "Cleaning up incomplete export data.",
        _ => "Processing the export."
    };

    private static string FailureText(OptimizationExportFailure failure) => failure switch
    {
        OptimizationExportFailure.IntegrityMismatch =>
            "The saved copy did not match the verified model. The export was not accepted.",
        OptimizationExportFailure.InsufficientSpace =>
            "There is not enough space to save and verify this model.",
        OptimizationExportFailure.DestinationUnavailable =>
            "The selected destination is not available or cannot be used.",
        OptimizationExportFailure.CleanupFailure =>
            "Export stopped, but cleanup could not be confirmed. The in-app model is unchanged.",
        _ =>
            "The verified model could not be saved. The in-app model is unchanged."
    };

    private void PublishState(OptimizationExportViewState state)
    {
        Delegate[] handlers = StateChanged?.GetInvocationList() ?? [];
        foreach (Delegate candidate in handlers)
        {
            try
            {
                ((EventHandler<OptimizationExportViewState>)candidate)(this, state);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "An optimization-export state observer failed with {0}.",
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
