using System;
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
    private CancellationTokenSource? _activeCancellation;
    private VerifiedPersistentExportTarget? _target;
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
        StateChanged?.Invoke(this, next);
    }

    internal void Unbind()
    {
        CancellationTokenSource? cancellation;
        OptimizationExportViewState next;
        lock (_gate)
        {
            cancellation = _activeCancellation;
            _activeGeneration = null;
            _activeCancellation = null;
            _cancelledGeneration = null;
            _target = null;
            _state = next = OptimizationExportViewState.Unbound();
        }
        cancellation?.Cancel();
        cancellation?.Dispose();
        StateChanged?.Invoke(this, next);
    }

    internal async Task<bool> TryStartAsync()
    {
        VerifiedPersistentExportTarget target;
        CancellationTokenSource cancellation;
        OptimizationExportViewState started;
        long generation;
        lock (_gate)
        {
            if (_activeGeneration is not null
                || _target is null
                || _state.Kind is not (OptimizationExportStateKind.Ready
                    or OptimizationExportStateKind.Cancelled
                    or OptimizationExportStateKind.Failed))
            {
                return false;
            }
            target = _target;
            generation = checked(++_generation);
            cancellation = new CancellationTokenSource();
            _activeGeneration = generation;
            _activeCancellation = cancellation;
            _cancelledGeneration = null;
            _state = started = new(
                OptimizationExportStateKind.Running,
                "Choose where to save the verified model.",
                target,
                OptimizationExportStage.ChoosingDestination);
        }
        StateChanged?.Invoke(this, started);

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

        OptimizationExportViewState completed;
        lock (_gate)
        {
            if (_activeGeneration != generation)
            {
                cancellation.Dispose();
                return true;
            }
            if (_cancelledGeneration == generation
                && result.Kind != OptimizationExportResultKind.Failed)
            {
                result = OptimizationExportResult.Cancelled();
            }
            completed = ToTerminalState(target, result);
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
        OptimizationExportViewState cancelling;
        lock (_gate)
        {
            if (_activeGeneration is not long generation
                || _activeCancellation is null
                || _state.Kind != OptimizationExportStateKind.Running)
            {
                return false;
            }
            _cancelledGeneration = generation;
            cancellation = _activeCancellation;
            _state = cancelling = _state with
            {
                Kind = OptimizationExportStateKind.Cancelling,
                StatusText = "Cancelling export and cleaning up incomplete output."
            };
        }
        StateChanged?.Invoke(this, cancelling);
        cancellation.Cancel();
        return true;
    }

    internal Task<bool> TryRetryAsync()
    {
        lock (_gate)
        {
            if (_activeGeneration is not null
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
            if (_activeGeneration != generation
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
        StateChanged?.Invoke(this, next);
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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
