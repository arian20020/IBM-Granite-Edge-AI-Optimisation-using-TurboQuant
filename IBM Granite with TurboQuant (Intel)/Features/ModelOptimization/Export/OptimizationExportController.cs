using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal enum OptimizationExportStateKind { Unbound, Ready, Running, Cancelling, Succeeded, Cancelled, Failed }

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
        new(OptimizationExportStateKind.Unbound, "Export is unavailable until this exact model result is verified.");
}

internal sealed class OptimizationExportController
{
    private readonly object _gate = new();
    private readonly IOptimizationExportService _service;
    private readonly TimeSpan _retirementTimeout;
    private readonly Dictionary<CancellationTokenSource, Task> _cancellationTasks = [];
    private long _generation;
    private long? _activeGeneration;
    private long? _cancelledGeneration;
    private long? _cancellationFailureGeneration;
    private long? _cancellationCallbackFailureGeneration;
    private long? _cleanupPendingGeneration;
    private long? _settledCleanupGeneration;
    private OptimizationExportViewState? _settledCleanupState;
    private CancellationTokenSource? _activeCancellation;
    private Task<bool>? _activeTask;
    private VerifiedPersistentExportTarget? _target;
    private bool _retired;
    private Task? _retirementTask;
    private Task _cleanupReconciliation = Task.CompletedTask;
    private OptimizationExportViewState _state = OptimizationExportViewState.Unbound();

    internal OptimizationExportController(IOptimizationExportService service, TimeSpan? retirementTimeout = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _retirementTimeout = retirementTimeout ?? TimeSpan.FromSeconds(5);
        if (_retirementTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(retirementTimeout));
    }

    internal event EventHandler<OptimizationExportViewState>? StateChanged;
    internal OptimizationExportViewState State { get { lock (_gate) return _state; } }
    internal bool IsCleanupPending { get { lock (_gate) return _cleanupPendingGeneration is not null; } }
    internal Task CleanupReconciliation { get { lock (_gate) return _cleanupReconciliation; } }

    internal void Bind(VerifiedPersistentExportTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        OptimizationExportViewState next;
        lock (_gate)
        {
            if (_retired) throw new InvalidOperationException("A retired export controller cannot be rebound.");
            if (_activeGeneration is not null || _cleanupPendingGeneration is not null) throw new InvalidOperationException("An active export cannot be rebound.");
            _target = target;
            _state = next = new(OptimizationExportStateKind.Ready, "Ready to save this verified model result.", target);
        }
        PublishState(next);
    }

    internal Task<bool> TryStartAsync()
    {
        VerifiedPersistentExportTarget target;
        CancellationTokenSource cancellation;
        OptimizationExportViewState started;
        long generation;
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> operation;
        lock (_gate)
        {
            if (_retired || _activeGeneration is not null || _cleanupPendingGeneration is not null || _target is null
                || _state.Kind is not (OptimizationExportStateKind.Ready or OptimizationExportStateKind.Cancelled or OptimizationExportStateKind.Failed))
                return Task.FromResult(false);
            target = _target;
            generation = checked(++_generation);
            cancellation = new();
            _activeGeneration = generation;
            _activeCancellation = cancellation;
            operation = CompleteExportAsync(generation, target, cancellation, start.Task);
            _activeTask = operation;
            _cancelledGeneration = null;
            _state = started = new(OptimizationExportStateKind.Running, "Choose where to save the verified model.", target, OptimizationExportStage.ChoosingDestination);
        }
        PublishState(started);
        start.TrySetResult();
        return operation;
    }

    private async Task<bool> CompleteExportAsync(long generation, VerifiedPersistentExportTarget target, CancellationTokenSource cancellation, Task start)
    {
        await start.ConfigureAwait(false);
        OptimizationExportResult? result = null;
        Exception? unexpectedFault = null;
        bool mayInvoke;
        lock (_gate)
        {
            mayInvoke = !_retired && _activeGeneration == generation && !cancellation.IsCancellationRequested;
        }
        if (!mayInvoke)
        {
            result = OptimizationExportResult.Cancelled();
        }
        else try { result = await _service.ExportAsync(target, new InlineProgress<OptimizationExportProgress>(v => ApplyProgress(generation, target, v)), cancellation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { result = OptimizationExportResult.Cancelled(); }
        catch (Exception exception) { unexpectedFault = exception; }
        if (unexpectedFault is null && result is null)
            unexpectedFault = new InvalidOperationException("The export provider returned no result.");
        // The fallback is never published when unexpectedFault is set; it only keeps the
        // typed terminal branches total without constructing the invalid Failure.None value.
        OptimizationExportResult settledResult = result ?? OptimizationExportResult.Failed(OptimizationExportFailure.PublicationFailure);
        Task? cancellationObservation;
        lock (_gate)
            cancellationObservation = _cancelledGeneration == generation
                && _cancellationTasks.TryGetValue(cancellation, out Task? pending)
                    ? pending
                    : null;
        if (cancellationObservation is not null)
        {
            try { await cancellationObservation.WaitAsync(_retirementTimeout).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                lock (_gate)
                    if (_activeGeneration == generation) _cancellationFailureGeneration = generation;
            }
        }
        OptimizationExportViewState? completed = null;
        lock (_gate)
        {
            if (_activeGeneration != generation)
            {
                DisposeAfterCancellation(cancellation);
                if (unexpectedFault is not null) ExceptionDispatchInfo.Capture(unexpectedFault).Throw();
                return true;
            }
            if (_retired) { _target = null; _state = OptimizationExportViewState.Unbound(); }
            else
            {
                OptimizationExportViewState classified = unexpectedFault is not null
                    ? UnexpectedFault(target)
                    : _cancellationCallbackFailureGeneration == generation
                        ? ToTerminalState(target, OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure))
                        : _cancelledGeneration == generation && settledResult.Kind == OptimizationExportResultKind.Succeeded
                            ? ToTerminalState(target, OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure))
                            : ToTerminalState(target, settledResult);
                if (_cleanupPendingGeneration == generation)
                {
                    _settledCleanupGeneration = generation;
                    _settledCleanupState = classified;
                }
                _state = completed = _cancellationFailureGeneration == generation
                    ? ToTerminalState(target, OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure))
                    : classified;
            }
            _activeGeneration = null; _activeCancellation = null; _activeTask = null;
            _cancelledGeneration = null; _cancellationFailureGeneration = null;
        }
        DisposeAfterCancellation(cancellation);
        if (completed is not null) PublishState(completed);
        if (unexpectedFault is not null) ExceptionDispatchInfo.Capture(unexpectedFault).Throw();
        return true;
    }

    internal bool TryCancel()
    {
        OptimizationExportViewState cancelling;
        lock (_gate)
        {
            if (_retired || _activeGeneration is not long active || _activeCancellation is null || _state.Kind != OptimizationExportStateKind.Running) return false;
            _cancelledGeneration = active;
            _state = cancelling = _state with { Kind = OptimizationExportStateKind.Cancelling, StatusText = "Cancelling export and cleaning up incomplete output." };
            Task cancellationObservation = RequestCancellation(_activeCancellation, active);
            _cleanupPendingGeneration = active;
            _cleanupReconciliation = PublishCancellationTimeoutAsync(active, _state.Target!, cancellationObservation, _activeTask!);
        }
        PublishState(cancelling);
        return true;
    }

    internal Task RetireAsync()
    {
        lock (_gate)
        {
            if (_retirementTask is not null) return _retirementTask;
            VerifiedPersistentExportTarget? target = _target;
            _retired = true; _target = null; _state = OptimizationExportViewState.Unbound();
            if (_activeCancellation is not null
                && _activeGeneration is long active
                && _cleanupPendingGeneration != active
                && target is not null)
            {
                _cancelledGeneration = active;
                _cleanupPendingGeneration = active;
                Task cancellationObservation = RequestCancellation(_activeCancellation, active);
                _cleanupReconciliation = PublishCancellationTimeoutAsync(
                    active,
                    target,
                    cancellationObservation,
                    _activeTask ?? Task.CompletedTask);
            }
            _retirementTask = RetireCoreAsync(_cleanupReconciliation);
            return _retirementTask;
        }
    }

    private async Task RetireCoreAsync(Task cleanupReconciliation)
    {
        try { await cleanupReconciliation.WaitAsync(_retirementTimeout).ConfigureAwait(false); }
        catch (TimeoutException) { Trace.TraceWarning("Optimization-export retirement detached after its five-second cleanup boundary."); }
        catch (Exception exception) { Trace.TraceWarning("Optimization-export retirement observed a settled {0} operation.", exception.GetType().Name); }
    }

    internal Task<bool> TryRetryAsync()
    {
        lock (_gate) if (_retired || _activeGeneration is not null || _cleanupPendingGeneration is not null || _state.Kind is not (OptimizationExportStateKind.Cancelled or OptimizationExportStateKind.Failed) || _target is null) return Task.FromResult(false);
        return TryStartAsync();
    }

    private void ApplyProgress(long generation, VerifiedPersistentExportTarget target, OptimizationExportProgress progress)
    {
        OptimizationExportViewState next;
        lock (_gate)
        {
            if (_retired || _activeGeneration != generation || _cancelledGeneration == generation) return;
            _state = next = new(OptimizationExportStateKind.Running, ProgressText(progress.Stage), target, progress.Stage, progress.Fraction);
        }
        PublishState(next);
    }

    private static OptimizationExportViewState ToTerminalState(VerifiedPersistentExportTarget target, OptimizationExportResult result)
    {
        if (result.Kind == OptimizationExportResultKind.Succeeded && result.Receipt is not null)
        {
            if (target.Route != result.Receipt.Route
                || target.OptimizationPlanId != result.Receipt.OptimizationPlanId
                || !string.Equals(target.ConfigurationSha256, result.Receipt.ConfigurationSha256, StringComparison.Ordinal)
                || !string.Equals(target.SourceSha256, result.Receipt.SourceSha256, StringComparison.Ordinal)
                || !result.Receipt.SourceUnchanged
                || !string.Equals(target.OutputIdentity, result.Receipt.OutputIdentity, StringComparison.Ordinal)
                || !string.Equals(target.OutputManifestSha256, result.Receipt.Sha256, StringComparison.Ordinal)
                || target.OutputLengthBytes != result.Receipt.LengthBytes)
                return Failed(target, OptimizationExportFailure.IntegrityMismatch);
            return new(OptimizationExportStateKind.Succeeded, "The verified model was saved successfully.", target, Receipt: result.Receipt);
        }
        if (result.Kind == OptimizationExportResultKind.Cancelled)
            return new(OptimizationExportStateKind.Cancelled, "Export cancelled. The verified model in the app is unchanged.", target);
        return Failed(target, result.Failure == OptimizationExportFailure.None ? OptimizationExportFailure.PublicationFailure : result.Failure);
    }

    private static OptimizationExportViewState Failed(VerifiedPersistentExportTarget target, OptimizationExportFailure failure) =>
        new(OptimizationExportStateKind.Failed, FailureText(failure), target, Failure: failure);

    private static OptimizationExportViewState UnexpectedFault(VerifiedPersistentExportTarget target) =>
        new(OptimizationExportStateKind.Failed, "Export stopped because of an unexpected internal error. Try again.", target);

    private static string ProgressText(OptimizationExportStage stage) => stage switch
    {
        OptimizationExportStage.ChoosingDestination => "Choose where to save the verified model.",
        OptimizationExportStage.Writing => "Saving the verified model.",
        OptimizationExportStage.Verifying => "Verifying the saved model.",
        OptimizationExportStage.Publishing => "Finishing the saved model.",
        OptimizationExportStage.CleaningUp => "Cleaning up incomplete export data.",
        _ => "Processing the export."
    };

    private static string FailureText(OptimizationExportFailure failure) => failure switch
    {
        OptimizationExportFailure.IntegrityMismatch => "The saved copy did not match the verified model. The export was not accepted.",
        OptimizationExportFailure.InsufficientSpace => "There is not enough space to save and verify this model.",
        OptimizationExportFailure.DestinationUnavailable => "The selected destination is not available or cannot be used.",
        OptimizationExportFailure.CleanupFailure => "Export stopped, but cleanup could not be confirmed. The in-app model is unchanged.",
        _ => "The verified model could not be saved. The in-app model is unchanged."
    };

    private void PublishState(OptimizationExportViewState state)
    {
        Delegate[] handlers = StateChanged?.GetInvocationList() ?? [];
        foreach (Delegate candidate in handlers)
            try { ((EventHandler<OptimizationExportViewState>)candidate)(this, state); }
            catch (Exception exception) { Trace.TraceError("An optimization-export state observer failed with {0}.", exception.GetType().Name); }
    }

    private Task RequestCancellation(CancellationTokenSource cancellation, long generation)
    {
        try
        {
            lock (_gate)
                if (_cancellationTasks.TryGetValue(cancellation, out Task? existing)) return existing;
            Task observation = ObserveCancellationAsync(cancellation.CancelAsync(), generation);
            lock (_gate) _cancellationTasks.Add(cancellation, observation);
            return observation;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Optimization-export cancellation observed {0}.", exception.GetType().Name);
            lock (_gate)
                if (_activeGeneration == generation)
                {
                    _cancellationFailureGeneration = generation;
                    _cancellationCallbackFailureGeneration = generation;
                }
            return Task.CompletedTask;
        }
    }

    private async Task PublishCancellationTimeoutAsync(long generation, VerifiedPersistentExportTarget target, Task cancellationObservation, Task activeTask)
    {
        bool timedOut = false;
        Task cleanup = Task.WhenAll(cancellationObservation, activeTask);
        bool cleanupSettled = false;
        try
        {
            await cleanup.WaitAsync(_retirementTimeout).ConfigureAwait(false);
            cleanupSettled = true;
        }
        catch (TimeoutException)
        {
            timedOut = true;
            OptimizationExportViewState? failed = null;
            lock (_gate)
            {
                if (!_retired && (_activeGeneration == generation || _cleanupPendingGeneration == generation))
                {
                    _cancellationFailureGeneration = generation;
                    _cleanupPendingGeneration = generation;
                    _state = failed = ToTerminalState(target, OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure));
                }
            }
            if (failed is not null) PublishState(failed);
        }
        catch (Exception exception)
        {
            cleanupSettled = true;
            Trace.TraceWarning("Optimization-export cleanup reconciliation observed {0}.", exception.GetType().Name);
        }
        if (!cleanupSettled)
            try { await cleanup.ConfigureAwait(false); }
            catch (Exception exception)
            {
                Trace.TraceWarning("Optimization-export cleanup reconciliation observed {0}.", exception.GetType().Name);
            }
        OptimizationExportViewState? reconciled = null;
        lock (_gate)
        {
            if (_cleanupPendingGeneration == generation)
            {
                _cleanupPendingGeneration = null;
                if (timedOut && !_retired)
                {
                    OptimizationExportViewState settled = _cancellationCallbackFailureGeneration == generation
                        ? ToTerminalState(target, OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure))
                        : _settledCleanupGeneration == generation && _settledCleanupState is not null
                            ? _settledCleanupState
                            : ToTerminalState(target, OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure));
                    _state = reconciled = settled;
                }
                if (_settledCleanupGeneration == generation)
                {
                    _settledCleanupGeneration = null;
                    _settledCleanupState = null;
                }
                if (_cancellationCallbackFailureGeneration == generation)
                    _cancellationCallbackFailureGeneration = null;
            }
        }
        if (reconciled is not null) PublishState(reconciled);
    }

    private async Task ObserveCancellationAsync(Task cancellation, long generation)
    {
        try { await cancellation.ConfigureAwait(false); }
        catch (Exception exception)
        {
            Trace.TraceWarning("Optimization-export cancellation observed {0}.", exception.GetType().Name);
            lock (_gate)
                if (_activeGeneration == generation || _cleanupPendingGeneration == generation)
                {
                    _cancellationFailureGeneration = generation;
                    _cancellationCallbackFailureGeneration = generation;
                }
        }
    }

    private void DisposeAfterCancellation(CancellationTokenSource cancellation)
    {
        Task observation;
        lock (_gate)
        {
            observation = _cancellationTasks.Remove(cancellation, out Task? pending)
                ? pending
                : Task.CompletedTask;
        }
        _ = DisposeAfterCancellationAsync(cancellation, observation);
    }

    private static async Task DisposeAfterCancellationAsync(CancellationTokenSource cancellation, Task observation)
    {
        try { await observation.ConfigureAwait(false); }
        finally { cancellation.Dispose(); }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
