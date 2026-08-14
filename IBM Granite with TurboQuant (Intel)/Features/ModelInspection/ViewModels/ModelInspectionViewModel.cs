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
    private readonly IModelInspectionStartupPresentationBarrier startupBarrier;
    private readonly SynchronizationContext? notificationContext;
    private readonly DelegateCommand cancelCommand;
    private readonly DelegateCommand retryCommand;
    private readonly DelegateCommand chooseAnotherCommand;

    private InspectionAttempt? activeAttempt;
    private ModelInspectionViewSnapshot snapshot =
        ModelInspectionViewSnapshot.Initial;
    private long nextAttemptGeneration;
    private bool lifecycleInvalidated;
    private bool disposed;

    internal ModelInspectionViewModel(
        IModelInspectionService service,
        ModelInspectionRequest request) : this(
            service,
            request,
            ImmediateStartupPresentationBarrier.Instance)
    {
    }

    internal ModelInspectionViewModel(
        IModelInspectionService service,
        ModelInspectionRequest request,
        IModelInspectionStartupPresentationBarrier startupBarrier)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.startupBarrier = startupBarrier ??
            throw new ArgumentNullException(nameof(startupBarrier));
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

    internal ModelInspectionViewSnapshot Snapshot
    {
        get
        {
            lock (stateLock)
            {
                return snapshot;
            }
        }
    }

    internal ModelInspectionProgress? Progress => Snapshot.Progress;

    internal ModelInspectionExecutionResult? Result => Snapshot.TerminalResult;

    internal bool IsRunActive => Snapshot.IsRunActive;

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

        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            long nextGeneration = GetNextAttemptGenerationLocked();
            attempt = new InspectionAttempt(nextGeneration);
            ModelInspectionViewSnapshot startSnapshot = new(
                new ModelInspectionRenderKey(nextGeneration, 0),
                isRunActive: true,
                isCancellationRequested: false,
                progress: null,
                terminalResult: null);
            replacedAttempt = activeAttempt;

            // Publish the new identity first. Synchronous cancellation callbacks
            // from the retired attempt are stale from this point forward.
            nextAttemptGeneration = nextGeneration;
            activeAttempt = attempt;
            lifecycleInvalidated = false;
            snapshot = startSnapshot;
        }

        PublishSnapshotChanged();
        RaiseCommandStates();
        replacedAttempt?.Cancel();

        IProgress<ModelInspectionProgress> attemptProgress =
            new ContextProgress<ModelInspectionProgress>(
                notificationContext,
                value => PublishProgress(attempt, value));

        ModelInspectionExecutionResult execution;
        try
        {
            await startupBarrier.WaitForPresentationAsync();
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
        InspectionAttempt? invalidatedAttempt = InvalidateLifecycle();
        invalidatedAttempt?.Cancel();
    }

    public void Dispose()
    {
        InspectionAttempt? invalidatedAttempt;
        bool snapshotChanged;

        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            snapshotChanged = !lifecycleInvalidated;
            if (snapshotChanged)
            {
                long nextGeneration = GetNextAttemptGenerationLocked();
                ModelInspectionViewSnapshot invalidatedSnapshot =
                    CreateInvalidatedSnapshot(nextGeneration);
                invalidatedAttempt = activeAttempt;
                disposed = true;
                nextAttemptGeneration = nextGeneration;
                activeAttempt = null;
                lifecycleInvalidated = true;
                snapshot = invalidatedSnapshot;
            }
            else
            {
                disposed = true;
                invalidatedAttempt = null;
            }
        }

        if (snapshotChanged)
        {
            PublishSnapshotChanged();
        }

        RaiseCommandStates();
        invalidatedAttempt?.Cancel();
        ChooseAnotherRequested = null;
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

            if (Equals(snapshot.Progress, value))
            {
                return;
            }

            snapshot = new ModelInspectionViewSnapshot(
                NextRevisionKeyLocked(),
                isRunActive: true,
                isCancellationRequested: snapshot.IsCancellationRequested,
                progress: value,
                terminalResult: null);
        }

        PublishSnapshotChanged();
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

            ModelInspectionViewSnapshot terminalSnapshot = new(
                NextRevisionKeyLocked(),
                isRunActive: false,
                isCancellationRequested: false,
                progress: null,
                terminalResult: execution);

            // Retire before notifying observers. Any already-queued progress
            // callback therefore fails the attempt-identity check.
            activeAttempt = null;
            snapshot = terminalSnapshot;
        }

        PublishSnapshotChanged();
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

            ModelInspectionViewSnapshot cancellationSnapshot = new(
                NextRevisionKeyLocked(),
                isRunActive: true,
                isCancellationRequested: true,
                progress: snapshot.Progress,
                terminalResult: null);
            attempt.CancellationRequested = true;
            snapshot = cancellationSnapshot;
        }

        // Disable immediately while keeping the attempt active until the
        // service returns a trusted terminal result.
        PublishSnapshotChanged();
        cancelCommand.RaiseCanExecuteChanged();
        attempt.Cancel();
    }

    private void ChooseAnother()
    {
        InspectionAttempt? invalidatedAttempt = InvalidateForNavigation();
        invalidatedAttempt?.Cancel();
        ChooseAnotherRequested?.Invoke(this, EventArgs.Empty);
    }

    private InspectionAttempt? InvalidateForNavigation()
    {
        InspectionAttempt? invalidatedAttempt;

        lock (stateLock)
        {
            if (disposed)
            {
                return null;
            }

            long nextGeneration = GetNextAttemptGenerationLocked();
            ModelInspectionViewSnapshot invalidatedSnapshot =
                CreateInvalidatedSnapshot(nextGeneration);
            invalidatedAttempt = activeAttempt;
            nextAttemptGeneration = nextGeneration;
            activeAttempt = null;
            snapshot = invalidatedSnapshot;
        }

        PublishSnapshotChanged();
        RaiseCommandStates();

        return invalidatedAttempt;
    }

    private InspectionAttempt? InvalidateLifecycle()
    {
        InspectionAttempt? invalidatedAttempt;

        lock (stateLock)
        {
            if (disposed || lifecycleInvalidated)
            {
                return null;
            }

            long nextGeneration = GetNextAttemptGenerationLocked();
            ModelInspectionViewSnapshot invalidatedSnapshot =
                CreateInvalidatedSnapshot(nextGeneration);
            invalidatedAttempt = activeAttempt;
            nextAttemptGeneration = nextGeneration;
            activeAttempt = null;
            lifecycleInvalidated = true;
            snapshot = invalidatedSnapshot;
        }

        PublishSnapshotChanged();
        RaiseCommandStates();
        return invalidatedAttempt;
    }

    private bool CanCancel()
    {
        lock (stateLock)
        {
            return !disposed &&
                activeAttempt is not null &&
                !snapshot.IsCancellationRequested;
        }
    }

    private bool CanRetry()
    {
        lock (stateLock)
        {
            return !disposed &&
                activeAttempt is null &&
                snapshot.TerminalResult is not null;
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

    private long GetNextAttemptGenerationLocked()
    {
        if (nextAttemptGeneration == long.MaxValue)
        {
            throw new InvalidOperationException(
                "The model inspection attempt sequence is exhausted.");
        }

        return nextAttemptGeneration + 1;
    }

    private ModelInspectionRenderKey NextRevisionKeyLocked()
    {
        long revision = snapshot.RenderKey.PresentationRevision;
        if (revision == long.MaxValue)
        {
            throw new InvalidOperationException(
                "The model inspection presentation sequence is exhausted.");
        }

        return new ModelInspectionRenderKey(
            snapshot.RenderKey.AttemptGeneration,
            revision + 1);
    }

    private static ModelInspectionViewSnapshot CreateInvalidatedSnapshot(
        long attemptGeneration) =>
        new(
            new ModelInspectionRenderKey(
                attemptGeneration,
                presentationRevision: 0),
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);

    private void PublishSnapshotChanged() =>
        OnPropertyChanged(nameof(Snapshot));

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

    private sealed class ImmediateStartupPresentationBarrier :
        IModelInspectionStartupPresentationBarrier
    {
        internal static ImmediateStartupPresentationBarrier Instance { get; } =
            new();

        public ValueTask WaitForPresentationAsync() => ValueTask.CompletedTask;
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
