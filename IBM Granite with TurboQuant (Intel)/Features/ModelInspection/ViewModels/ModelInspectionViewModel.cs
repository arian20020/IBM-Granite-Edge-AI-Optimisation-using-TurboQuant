using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Services;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelInspection.ViewModels;

/// <summary>
/// Owns one selected model and coordinates replaceable inspection attempts.
/// </summary>
internal sealed class ModelInspectionViewModel : INotifyPropertyChanged, IDisposable
{
    private const string UnconfirmedCancellationCode =
        "MI-OP-CANCELLATION-UNCONFIRMED";
    private const string UnexpectedServiceFailureCode =
        "MI-OP-SERVICE-UNEXPECTED";

    private readonly object stateLock = new();
    private readonly IModelInspectionService service;
    private readonly SynchronizationContext? notificationContext;
    private readonly DelegateCommand cancelCommand;
    private readonly DelegateCommand retryCommand;
    private readonly DelegateCommand chooseAnotherCommand;

    private InspectionAttempt? activeAttempt;
    private ModelInspectionProgress? progress;
    private ModelInspectionExecutionResult? result;
    private long nextAttemptId;
    private bool disposed;

    internal ModelInspectionViewModel(
        IModelInspectionService service,
        ModelInspectionRequest request)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        notificationContext = SynchronizationContext.Current;

        cancelCommand = new DelegateCommand(
            _ => CancelActiveAttempt(),
            _ => CanCancel());
        retryCommand = new DelegateCommand(
            _ => _ = StartAsync(),
            _ => CanRetry());
        chooseAnotherCommand = new DelegateCommand(
            _ => ChooseAnother(),
            _ => CanChooseAnother());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal event EventHandler? ChooseAnotherRequested;

    internal ModelInspectionRequest Request { get; }

    internal ModelInspectionProgress? Progress
    {
        get
        {
            lock (stateLock)
            {
                return progress;
            }
        }
    }

    internal ModelInspectionExecutionResult? Result
    {
        get
        {
            lock (stateLock)
            {
                return result;
            }
        }
    }

    internal bool IsRunActive
    {
        get
        {
            lock (stateLock)
            {
                return activeAttempt is not null;
            }
        }
    }

    internal ICommand CancelCommand => cancelCommand;

    internal ICommand RetryCommand => retryCommand;

    internal ICommand ChooseAnotherCommand => chooseAnotherCommand;

    /// <summary>
    /// Starts a fresh attempt for the exact immutable navigation request.
    /// A replacement becomes current before the prior attempt is cancelled.
    /// </summary>
    internal async Task StartAsync()
    {
        InspectionAttempt attempt;
        InspectionAttempt? replacedAttempt;
        bool progressWasCleared;
        bool resultWasCleared;
        bool becameActive;

        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (nextAttemptId == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The model inspection attempt sequence is exhausted.");
            }

            attempt = new InspectionAttempt(++nextAttemptId);
            replacedAttempt = activeAttempt;
            becameActive = replacedAttempt is null;

            // Publish the new identity first. Synchronous cancellation callbacks
            // from the retired attempt are stale from this point forward.
            activeAttempt = attempt;
            progressWasCleared = progress is not null;
            resultWasCleared = result is not null;
            progress = null;
            result = null;
        }

        PublishStartState(
            becameActive,
            progressWasCleared,
            resultWasCleared);
        replacedAttempt?.Cancel();

        IProgress<ModelInspectionProgress> attemptProgress =
            new ContextProgress<ModelInspectionProgress>(
                notificationContext,
                value => PublishProgress(attempt, value));

        ModelInspectionExecutionResult execution;
        try
        {
            execution = await service.InspectAsync(
                Request,
                attemptProgress,
                attempt.Token);
        }
        catch (OperationCanceledException)
        {
            // A cancellation exception is not the worker's trusted cooperative
            // terminal result, so it must remain an operational failure.
            execution = ModelInspectionExecutionResult.OperationalFailure(
                new ModelInspectionOperationalFailure(
                    UnconfirmedCancellationCode,
                    "Model inspection stopped without a confirmed cancellation.",
                    "The inspection service did not return cooperative cancellation evidence."));
        }
        catch (Exception)
        {
            execution = ModelInspectionExecutionResult.OperationalFailure(
                new ModelInspectionOperationalFailure(
                    UnexpectedServiceFailureCode,
                    "Model inspection could not be completed.",
                    "The inspection service ended unexpectedly."));
        }

        try
        {
            PublishTerminalResult(attempt, execution);
        }
        finally
        {
            attempt.Dispose();
        }
    }

    /// <summary>
    /// Invalidates the current attempt before requesting its cancellation.
    /// </summary>
    internal void Deactivate()
    {
        InspectionAttempt? invalidatedAttempt = InvalidateActiveAttempt();
        invalidatedAttempt?.Cancel();
    }

    public void Dispose()
    {
        InspectionAttempt? invalidatedAttempt;

        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            invalidatedAttempt = activeAttempt;
            activeAttempt = null;
        }

        if (invalidatedAttempt is not null)
        {
            OnPropertyChanged(nameof(IsRunActive));
        }

        RaiseCommandStates();
        invalidatedAttempt?.Cancel();
        ChooseAnotherRequested = null;
    }

    private void PublishStartState(
        bool becameActive,
        bool progressWasCleared,
        bool resultWasCleared)
    {
        if (progressWasCleared)
        {
            OnPropertyChanged(nameof(Progress));
        }

        if (resultWasCleared)
        {
            OnPropertyChanged(nameof(Result));
        }

        if (becameActive)
        {
            OnPropertyChanged(nameof(IsRunActive));
        }

        RaiseCommandStates();
    }

    private void PublishProgress(
        InspectionAttempt attempt,
        ModelInspectionProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (stateLock)
        {
            if (disposed || activeAttempt?.Id != attempt.Id)
            {
                return;
            }

            progress = value;
        }

        OnPropertyChanged(nameof(Progress));
    }

    private void PublishTerminalResult(
        InspectionAttempt attempt,
        ModelInspectionExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        lock (stateLock)
        {
            if (disposed || activeAttempt?.Id != attempt.Id)
            {
                return;
            }

            // Retire before notifying observers. Any already-queued progress
            // callback therefore fails the attempt-identity check.
            activeAttempt = null;
            result = execution;
        }

        OnPropertyChanged(nameof(IsRunActive));
        OnPropertyChanged(nameof(Result));
        RaiseCommandStates();
    }

    private void CancelActiveAttempt()
    {
        InspectionAttempt? attempt;

        lock (stateLock)
        {
            attempt = activeAttempt;
            if (disposed ||
                attempt is null ||
                attempt.CancellationRequested)
            {
                return;
            }

            attempt.CancellationRequested = true;
        }

        // Disable immediately while keeping the attempt active until the
        // service returns a trusted terminal result.
        cancelCommand.RaiseCanExecuteChanged();
        attempt.Cancel();
    }

    private void ChooseAnother()
    {
        InspectionAttempt? invalidatedAttempt = InvalidateActiveAttempt();
        invalidatedAttempt?.Cancel();
        ChooseAnotherRequested?.Invoke(this, EventArgs.Empty);
    }

    private InspectionAttempt? InvalidateActiveAttempt()
    {
        InspectionAttempt? invalidatedAttempt;

        lock (stateLock)
        {
            if (disposed)
            {
                return null;
            }

            invalidatedAttempt = activeAttempt;
            activeAttempt = null;
        }

        if (invalidatedAttempt is not null)
        {
            OnPropertyChanged(nameof(IsRunActive));
            RaiseCommandStates();
        }

        return invalidatedAttempt;
    }

    private bool CanCancel()
    {
        lock (stateLock)
        {
            return !disposed &&
                activeAttempt is not null &&
                !activeAttempt.CancellationRequested;
        }
    }

    private bool CanRetry()
    {
        lock (stateLock)
        {
            return !disposed && activeAttempt is null && result is not null;
        }
    }

    private bool CanChooseAnother()
    {
        lock (stateLock)
        {
            return !disposed;
        }
    }

    private void RaiseCommandStates()
    {
        cancelCommand.RaiseCanExecuteChanged();
        retryCommand.RaiseCanExecuteChanged();
        chooseAnotherCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    private sealed class InspectionAttempt : IDisposable
    {
        private readonly object cancellationLock = new();
        private readonly CancellationTokenSource cancellation = new();
        private bool cancellationInProgress;
        private bool cancellationDisposed;
        private bool disposalRequested;

        internal InspectionAttempt(long id)
        {
            Id = id;
        }

        internal long Id { get; }

        internal bool CancellationRequested { get; set; }

        internal CancellationToken Token => cancellation.Token;

        internal void Cancel()
        {
            lock (cancellationLock)
            {
                if (cancellationDisposed ||
                    cancellationInProgress ||
                    cancellation.IsCancellationRequested)
                {
                    return;
                }

                cancellationInProgress = true;
            }

            try
            {
                // Do not hold cancellationLock while invoking callbacks. A
                // synchronous completion may retire and dispose this attempt.
                cancellation.Cancel();
            }
            finally
            {
                bool disposeAfterCancellation;
                lock (cancellationLock)
                {
                    cancellationInProgress = false;
                    disposeAfterCancellation =
                        disposalRequested && !cancellationDisposed;
                    if (disposeAfterCancellation)
                    {
                        cancellationDisposed = true;
                    }
                }

                if (disposeAfterCancellation)
                {
                    cancellation.Dispose();
                }
            }
        }

        public void Dispose()
        {
            bool disposeNow;
            lock (cancellationLock)
            {
                if (cancellationDisposed || disposalRequested)
                {
                    return;
                }

                if (cancellationInProgress)
                {
                    disposalRequested = true;
                    return;
                }

                cancellationDisposed = true;
                disposeNow = true;
            }

            if (disposeNow)
            {
                cancellation.Dispose();
            }
        }
    }

    private sealed class ContextProgress<T> : IProgress<T>
    {
        private readonly SynchronizationContext? context;
        private readonly Action<T> report;

        internal ContextProgress(
            SynchronizationContext? context,
            Action<T> report)
        {
            this.context = context;
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public void Report(T value)
        {
            if (context is null ||
                ReferenceEquals(context, SynchronizationContext.Current))
            {
                report(value);
                return;
            }

            context.Post(_ => report(value), state: null);
        }
    }
}
