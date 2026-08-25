using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;

internal enum CompatibilityAuxiliaryStatusKind
{
    None,
    Information,
    Error
}

internal sealed record CompatibilityAuxiliaryStatus(
    CompatibilityAuxiliaryStatusKind Kind,
    string Message)
{
    internal static CompatibilityAuxiliaryStatus None { get; } =
        new(CompatibilityAuxiliaryStatusKind.None, string.Empty);
}

/// <summary>
/// Owns one compatibility check for the lifetime of one navigation.
///
/// Three behaviours matter more than the code that implements them.
///
/// One automatic attempt per navigation. Arriving on this page starts a check;
/// arriving again starts another. The user never has to ask for the thing the
/// page exists to do.
///
/// Stale attempts are rejected rather than raced. Each attempt takes a
/// generation number, and a result whose generation is no longer current is
/// dropped on the floor. Without that, a slow first attempt could overwrite a
/// fast second one and show the user an answer about a state of the world that
/// has already passed.
///
/// The page is told what to show, never how to think. This produces immutable
/// snapshots; the page applies them to a tree it already built.
/// </summary>
internal sealed class CompatibilityViewModel
{
    private readonly SynchronizationContext? _uiContext;
    private readonly Func<CancellationToken, Task<CompatibilityScreenModel>> _evaluator;
    private readonly bool _continueDestinationAvailable;
    private readonly ICompatibilityMemoryRecovery _memoryRecovery;
    private readonly OptimizationDestination _optimizationDestination;
    private readonly object _attemptGate = new();

    private AttemptCancellation? _attemptCancellation;
    private AttemptCancellation? _auxiliaryCancellation;
    private int _attemptGeneration;
    private long _operationSerial;
    private long _recoveryOwner;
    private long _taskManagerOwner;
    private long _optimizationSelectionRevision;
    private int _optimizationAuthorityGeneration;
    private long _optimizationAuthoritySelectionRevision;
    private int _optimizationIssuanceOwner;
    private int _optimizationIssuanceConsumed;
    private CompatibilityScreenModel? _lastModel;

    internal CompatibilityViewModel()
        : this(static token => Task.Run(
            () => CompatibilityEngine.RunWithAvailableAdapters(token),
            token),
            continueDestinationAvailable: true)
    {
    }

    internal CompatibilityViewModel(
        Func<CancellationToken, Task<CompatibilityScreenModel>> evaluator,
        bool continueDestinationAvailable = true,
        ICompatibilityMemoryRecovery? memoryRecovery = null)
        : this(
            evaluator,
            continueDestinationAvailable,
            memoryRecovery,
            OptimizationDestination.Unavailable)
    {
    }

    internal CompatibilityViewModel(
        Func<CancellationToken, Task<CompatibilityScreenModel>> evaluator,
        bool continueDestinationAvailable,
        ICompatibilityMemoryRecovery? memoryRecovery,
        OptimizationDestination optimizationDestination)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _continueDestinationAvailable = continueDestinationAvailable;
        _memoryRecovery = memoryRecovery ?? new WindowsCompatibilityMemoryRecovery([]);
        _optimizationDestination = optimizationDestination
            ?? throw new ArgumentNullException(nameof(optimizationDestination));
        if (_optimizationDestination.IsAvailable && !continueDestinationAvailable)
        {
            throw new ArgumentException(
                "An optimisation issuer cannot be registered while navigation is unavailable.",
                nameof(continueDestinationAvailable));
        }
        _uiContext = SynchronizationContext.Current;

        // Assigned before the commands, because their guards read it: a command
        // must never be asked whether it can run before there is a state to ask
        // about.
        Presentation = CompatibilityPresentationFactory.Analysing(0);

        ContinueCommand = new DelegateCommand(
            Continue,
            CanContinue);

        BackCommand = new DelegateCommand(
            () => BackRequested?.Invoke(this, EventArgs.Empty),
            () => Presentation.SecondaryActionEnabled
                && Presentation.SecondaryActionKind == CompatibilitySecondaryActionKind.Back);

        CancelCommand = new DelegateCommand(
            Cancel,
            () => Presentation.SecondaryActionEnabled
                && Presentation.SecondaryActionKind == CompatibilitySecondaryActionKind.Cancel);
        RetryCommand = new DelegateCommand(
            RetryFromCommand,
            () => Presentation.SecondaryActionEnabled
                && Presentation.SecondaryActionKind == CompatibilitySecondaryActionKind.Retry);
        RefreshMemoryCommand = new DelegateCommand(
            ReleaseMemoryFromCommand,
            CanUseMemoryRecovery);
        OpenTaskManagerCommand = new DelegateCommand(
            OpenTaskManagerFromCommand,
            CanOpenTaskManager);
    }

    /// <summary>Raised whenever a new snapshot is ready to render.</summary>
    internal event EventHandler<CompatibilityPresentation>? PresentationChanged;

    internal event EventHandler<CompatibilityAuxiliaryStatus>? AuxiliaryStatusChanged;

    internal event EventHandler? ContinueRequested;

    internal event EventHandler<OptimizationSelectionHandoff>? OptimizationRequested;

    internal event EventHandler? BackRequested;

    internal CompatibilityPresentation Presentation { get; private set; }

    internal DelegateCommand ContinueCommand { get; }

    internal DelegateCommand BackCommand { get; }

    internal DelegateCommand CancelCommand { get; }

    internal DelegateCommand RetryCommand { get; }

    internal DelegateCommand RefreshMemoryCommand { get; }

    internal DelegateCommand OpenTaskManagerCommand { get; }

    internal CompatibilityAuxiliaryStatus AuxiliaryStatus { get; private set; } =
        CompatibilityAuxiliaryStatus.None;

    internal OptimizationPreferenceSelection? SelectedPreference { get; private set; }

    private void Continue()
    {
        if (Presentation.Optimization is null)
        {
            ContinueRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        OptimizationPreferenceSelection? preference = SelectedPreference;
        int generation = Volatile.Read(ref _attemptGeneration);
        long selectionRevision = Volatile.Read(ref _optimizationSelectionRevision);
        if (preference is null
            || generation != Volatile.Read(ref _optimizationAuthorityGeneration)
            || selectionRevision
                != Volatile.Read(ref _optimizationAuthoritySelectionRevision)
            || Volatile.Read(ref _optimizationIssuanceConsumed) != 0
            || Interlocked.CompareExchange(
                ref _optimizationIssuanceOwner, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (generation != Volatile.Read(ref _attemptGeneration)
                || selectionRevision
                    != Volatile.Read(ref _optimizationSelectionRevision)
                || !_optimizationDestination.TryIssue(
                    preference,
                    out OptimizationSelectionHandoff? handoff)
                || handoff is null
                || generation != Volatile.Read(ref _attemptGeneration)
                || selectionRevision
                    != Volatile.Read(ref _optimizationSelectionRevision))
            {
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _optimizationIssuanceConsumed, 1, 0) != 0)
            {
                return;
            }

            ContinueCommand.RaiseCanExecuteChanged();
            try
            {
                OptimizationRequested?.Invoke(this, handoff);
            }
            catch
            {
                // A subscriber may have acted before another subscriber failed,
                // so this issuance remains consumed. Retrying it could navigate
                // or execute twice. Only a new authority context may retry.
                PublishAuxiliaryStatus(new CompatibilityAuxiliaryStatus(
                    CompatibilityAuxiliaryStatusKind.Error,
                    "Optimisation could not be opened. Recheck compatibility to create a new plan."));
            }
        }
        catch
        {
            // Adapter failures are private implementation details. Fail closed
            // without turning a button press into a UI-thread exception.
        }
        finally
        {
            Volatile.Write(ref _optimizationIssuanceOwner, 0);
        }
    }

    private bool CanContinue()
    {
        if (!Presentation.PrimaryActionEnabled)
        {
            return false;
        }

        if (Presentation.Optimization is null)
        {
            return true;
        }

        return _optimizationDestination.IsAvailable
            && Volatile.Read(ref _optimizationIssuanceConsumed) == 0
            && Volatile.Read(ref _attemptGeneration)
                == Volatile.Read(ref _optimizationAuthorityGeneration)
            && Volatile.Read(ref _optimizationSelectionRevision)
                == Volatile.Read(ref _optimizationAuthoritySelectionRevision);
    }

    /// <summary>
    /// Starts one attempt. Safe to call again: the previous attempt is cancelled
    /// and its result, if it arrives late, is discarded.
    /// </summary>
    internal Task StartAsync() => RunAttemptAsync(preparation: null);

    /// <summary>
    /// Releases only registered application-owned caches and then reruns the
    /// exact evaluator. Both steps share one attempt generation, so neither a
    /// late release nor its late result can overwrite a newer check.
    /// </summary>
    internal Task RefreshMemoryAndRetryAsync()
    {
        if (!CanUseMemoryRecovery())
        {
            return Task.CompletedTask;
        }

        long owner;
        lock (_attemptGate)
        {
            if (_recoveryOwner != 0)
            {
                return Task.CompletedTask;
            }
            owner = ++_operationSerial;
            _recoveryOwner = owner;
        }
        RaiseRecoveryCanExecuteChanged();
        return CompleteRecoveryAsync(owner);
    }

    internal Task OpenTaskManagerAsync()
    {
        if (!CanOpenTaskManager())
        {
            return Task.CompletedTask;
        }

        long owner;
        lock (_attemptGate)
        {
            if (_taskManagerOwner != 0)
            {
                return Task.CompletedTask;
            }
            owner = ++_operationSerial;
            _taskManagerOwner = owner;
        }
        RaiseRecoveryCanExecuteChanged();
        return CompleteTaskManagerAsync(owner);
    }

    private async Task CompleteRecoveryAsync(long owner)
    {
        try
        {
            await RunAttemptAsync(_memoryRecovery.ReleaseApplicationMemoryAsync, owner);
        }
        finally
        {
            ReleaseBusyOwner(ref _recoveryOwner, owner);
            RaiseRecoveryCanExecuteChanged();
        }
    }

    private async Task CompleteTaskManagerAsync(long owner)
    {
        AttemptCancellation cancellation = new();
        AttemptCancellation? previous;
        lock (_attemptGate)
        {
            previous = _auxiliaryCancellation;
            _auxiliaryCancellation = cancellation;
        }
        previous?.Cancel();

        try
        {
            await _memoryRecovery.OpenTaskManagerAsync(cancellation.Token);
            PublishAuxiliaryStatus(
                new(CompatibilityAuxiliaryStatusKind.Information,
                    "Task Manager opened. Choose what to close, then return and refresh the check."),
                owner);
        }
        catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            PublishAuxiliaryStatus(
                new(CompatibilityAuxiliaryStatusKind.Error,
                    "Task Manager could not be opened. You can try again."),
                owner);
        }
        finally
        {
            lock (_attemptGate)
            {
                if (ReferenceEquals(_auxiliaryCancellation, cancellation))
                {
                    _auxiliaryCancellation = null;
                }
            }
            cancellation.Complete();
            ReleaseBusyOwner(ref _taskManagerOwner, owner);
            RaiseRecoveryCanExecuteChanged();
        }
    }

    private async Task RunAttemptAsync(
        Func<CancellationToken, Task>? preparation,
        long recoveryOwner = 0)
    {
        AttemptCancellation cancellation = new();
        CancellationToken token = cancellation.Token;
        AttemptCancellation? previous;
        AttemptCancellation? previousAuxiliary;
        int generation;
        lock (_attemptGate)
        {
            generation = unchecked(++_attemptGeneration);
            previous = _attemptCancellation;
            _attemptCancellation = cancellation;
            previousAuxiliary = _auxiliaryCancellation;
            _auxiliaryCancellation = null;
            if (_recoveryOwner != recoveryOwner)
            {
                _recoveryOwner = 0;
            }
            _taskManagerOwner = 0;
        }

        if (Volatile.Read(ref _optimizationAuthorityGeneration) != 0)
        {
            _optimizationDestination.Invalidate();
        }

        previous?.Cancel();
        previousAuxiliary?.Cancel();
        PublishAuxiliaryStatus(CompatibilityAuxiliaryStatus.None);
        RaiseRecoveryCanExecuteChanged();

        Publish(
            CompatibilityPresentationFactory.Analysing(0),
            generation,
            ClearCompatibilitySnapshot);

        try
        {
            if (preparation is not null)
            {
                await preparation(token).ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
            }

            // The engine is synchronous and pure. It runs off the UI thread so a
            // slow adapter cannot freeze the page once adapters exist.
            CompatibilityScreenModel model = await _evaluator(token)
                .ConfigureAwait(true);

            OptimizationPreferenceSelection? preference =
                model.State == CompatibilityScreenState.OptimisationRequired
                ? OptimizationPreferenceSelection.Automatic()
                : null;
            CompatibilityPresentation presentation = preference is null
                ? CompatibilityPresentationFactory.From(model)
                : CompatibilityPresentationFactory.From(model, preference);
            Publish(
                presentation,
                generation,
                () =>
                {
                    _lastModel = model;
                    CommitAttemptSelection(preference, generation);
                });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Publish(
                CompatibilityPresentationFactory.Cancelled(),
                generation,
                () =>
                {
                    _lastModel = null;
                    CommitSelection(null);
                });
        }
        catch
        {
            Publish(
                CompatibilityPresentationFactory.OperationalFailure(),
                generation,
                () =>
                {
                    _lastModel = null;
                    CommitSelection(null);
                });
            throw;
        }
        finally
        {
            lock (_attemptGate)
            {
                if (ReferenceEquals(_attemptCancellation, cancellation))
                {
                    _attemptCancellation = null;
                }
            }

            // Every attempt owns exactly its own source. A newer attempt may
            // cancel it, but disposal waits until this evaluator has returned.
            cancellation.Complete();
        }
    }

    /// <summary>
    /// Renders a snapshot directly, without running anything. Debug fixtures use
    /// this to show a state the engine cannot reach yet.
    /// </summary>
    internal void ShowFixture(CompatibilityPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        int generation = RetireAttempt();
        // Takes the retired generation so an in-flight real attempt cannot
        // overwrite what a reviewer is looking at.
        Publish(
            presentation,
            generation,
            () =>
            {
                _lastModel = null;
                CommitSelection(null);
            });
    }

    internal void Cancel()
    {
        int generation = RetireAttempt();
        Publish(
            CompatibilityPresentationFactory.Cancelled(),
            generation,
            () =>
            {
                _lastModel = null;
                CommitSelection(null);
            });
    }

    /// <summary>Retires active work without publishing a new page state.</summary>
    internal int RetireAttempt()
    {
        AttemptCancellation? cancellation;
        AttemptCancellation? auxiliary;
        int generation;
        lock (_attemptGate)
        {
            generation = unchecked(++_attemptGeneration);
            cancellation = _attemptCancellation;
            _attemptCancellation = null;
            auxiliary = _auxiliaryCancellation;
            _auxiliaryCancellation = null;
            _recoveryOwner = 0;
            _taskManagerOwner = 0;
        }

        if (Volatile.Read(ref _optimizationAuthorityGeneration) != 0)
        {
            _optimizationDestination.Invalidate();
        }

        cancellation?.Cancel();
        auxiliary?.Cancel();
        PublishAuxiliaryStatus(CompatibilityAuxiliaryStatus.None);
        RaiseRecoveryCanExecuteChanged();
        return generation;
    }

    private bool CanUseMemoryRecovery() =>
        Presentation.MemoryRecoveryReason
            == CompatibilityMemoryRecoveryReason.SystemMemoryPressure
        && Volatile.Read(ref _recoveryOwner) == 0;

    private bool CanOpenTaskManager() =>
        Presentation.MemoryRecoveryReason
            == CompatibilityMemoryRecoveryReason.SystemMemoryPressure
        && Volatile.Read(ref _taskManagerOwner) == 0;

    private async void ReleaseMemoryFromCommand()
    {
        try
        {
            await RefreshMemoryAndRetryAsync();
        }
        catch
        {
            // The operation already published a path-private safe failure.
        }
    }

    private async void OpenTaskManagerFromCommand()
    {
        try
        {
            await OpenTaskManagerAsync();
        }
        catch
        {
            // OS launch failures carry no user or process data into the page.
        }
    }

    private void RaiseRecoveryCanExecuteChanged()
    {
        RefreshMemoryCommand.RaiseCanExecuteChanged();
        OpenTaskManagerCommand.RaiseCanExecuteChanged();
    }

    private async void RetryFromCommand()
    {
        try
        {
            await StartAsync();
        }
        catch
        {
            // StartAsync already published a path-private operational failure.
            // Command dispatch must not surface an unhandled async-void fault.
        }
    }

    private void ReleaseBusyOwner(ref long ownerField, long owner)
    {
        lock (_attemptGate)
        {
            if (ownerField == owner)
            {
                ownerField = 0;
            }
        }
    }

    private void PublishAuxiliaryStatus(
        CompatibilityAuxiliaryStatus status,
        long requiredTaskManagerOwner = 0)
    {
        void Apply()
        {
            if (requiredTaskManagerOwner != 0
                && Volatile.Read(ref _taskManagerOwner) != requiredTaskManagerOwner)
            {
                return;
            }
            AuxiliaryStatus = status;
            AuxiliaryStatusChanged?.Invoke(this, status);
        }

        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            Apply();
        }
        else
        {
            _uiContext.Post(_ => Apply(), null);
        }
    }

    private void ClearCompatibilitySnapshot()
    {
        _lastModel = null;
        CommitSelection(null);
    }

    private void CommitAttemptSelection(
        OptimizationPreferenceSelection? preference,
        int generation)
    {
        long revision = CommitSelection(preference);
        if (preference is not null
            && Volatile.Read(ref _optimizationAuthorityGeneration) == 0
            && _optimizationDestination.MatchesExpectedPreference(preference))
        {
            Volatile.Write(ref _optimizationAuthoritySelectionRevision, revision);
            Volatile.Write(ref _optimizationAuthorityGeneration, generation);
        }
    }

    private long CommitSelection(OptimizationPreferenceSelection? preference)
    {
        SelectedPreference = preference;
        return Interlocked.Increment(ref _optimizationSelectionRevision);
    }

    internal void SelectAutomaticPreference()
    {
        if (_lastModel?.State != CompatibilityScreenState.OptimisationRequired)
        {
            return;
        }

        int generation = Volatile.Read(ref _attemptGeneration);
        CompatibilityScreenModel model = _lastModel;
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Automatic();
        if (SelectedPreference != preference)
        {
            _optimizationDestination.Invalidate();
        }
        Publish(
            CompatibilityPresentationFactory.From(model, preference),
            generation,
            () => CommitSelection(preference));
    }

    internal void SelectManualPreference(int value)
    {
        if (_lastModel?.State != CompatibilityScreenState.OptimisationRequired)
        {
            return;
        }

        int generation = Volatile.Read(ref _attemptGeneration);
        CompatibilityScreenModel model = _lastModel;
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Manual(value);
        if (SelectedPreference != preference)
        {
            _optimizationDestination.Invalidate();
        }
        CompatibilityPresentation next =
            CompatibilityPresentationFactory.From(model, preference);
        if (SelectedPreference?.Kind == OptimizationPreferenceKind.Manual
            && Presentation.Optimization is { } current
            && next.Optimization is { } candidate
            && string.Equals(
                current.SelectedMode.Label,
                candidate.SelectedMode.Label,
                StringComparison.Ordinal))
        {
            // The exact choice still invalidates the former plan, but the
            // effective visual band did not change, so avoid a redundant
            // presentation publish.
            CommitSelection(preference);
            ContinueCommand.RaiseCanExecuteChanged();
            return;
        }
        Publish(
            next,
            generation,
            () => CommitSelection(preference));
    }

    private void Publish(
        CompatibilityPresentation presentation,
        int generation,
        Action? commitState = null)
    {
        // A result from a superseded attempt describes a world that has already
        // moved on, so it is dropped rather than shown.
        if (generation != Volatile.Read(ref _attemptGeneration))
        {
            return;
        }

        void Apply()
        {
            if (generation != Volatile.Read(ref _attemptGeneration))
            {
                return;
            }

            commitState?.Invoke();
            CompatibilityPresentation effectivePresentation;
            if (presentation.Optimization is not null
                && !_optimizationDestination.IsAvailable)
            {
                effectivePresentation = presentation with
                {
                    PrimaryActionText = "Coming later",
                    PrimaryActionEnabled = false
                };
            }
            else if (!_continueDestinationAvailable
                && presentation.PrimaryActionEnabled)
            {
                effectivePresentation = presentation with
                {
                    PrimaryActionText = "Coming later",
                    PrimaryActionEnabled = false
                };
            }
            else
            {
                effectivePresentation = _continueDestinationAvailable
                    ? presentation
                    : presentation with { PrimaryActionEnabled = false };
            }
            Presentation = effectivePresentation;
            ContinueCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            RetryCommand.RaiseCanExecuteChanged();
            RaiseRecoveryCanExecuteChanged();
            PresentationChanged?.Invoke(this, effectivePresentation);
        }

        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            Apply();
            return;
        }

        _uiContext.Post(_ => Apply(), null);
    }

    /// <summary>
    /// Serialises cancellation with owner completion without holding a lock
    /// while arbitrary cancellation callbacks execute. Completion owns the
    /// source disposal, but defers it until every cancellation already in
    /// flight has returned.
    /// </summary>
    internal sealed class AttemptCancellation
    {
        private readonly object _lifetimeGate = new();
        private readonly CancellationTokenSource _source = new();
        private int _cancellationsInFlight;
        private bool _completionRequested;
        private bool _disposed;

        internal CancellationToken Token => _source.Token;

        internal void Cancel()
        {
            lock (_lifetimeGate)
            {
                if (_disposed)
                {
                    return;
                }

                _cancellationsInFlight++;
            }

            try
            {
                try
                {
                    _source.Cancel(throwOnFirstException: false);
                }
                catch (AggregateException)
                {
                    // Token callbacks are untrusted lifecycle observers. Their
                    // content is never surfaced and cannot strand cancellation.
                }
            }
            finally
            {
                bool dispose;
                lock (_lifetimeGate)
                {
                    _cancellationsInFlight--;
                    dispose = TryClaimDisposal();
                }

                if (dispose)
                {
                    _source.Dispose();
                }
            }
        }

        internal void Complete()
        {
            bool dispose;
            lock (_lifetimeGate)
            {
                _completionRequested = true;
                dispose = TryClaimDisposal();
            }

            if (dispose)
            {
                _source.Dispose();
            }
        }

        private bool TryClaimDisposal()
        {
            if (_disposed || !_completionRequested || _cancellationsInFlight != 0)
            {
                return false;
            }

            _disposed = true;
            return true;
        }
    }
}
