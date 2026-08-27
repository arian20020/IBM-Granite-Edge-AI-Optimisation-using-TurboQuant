using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

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
    private readonly Func<IReadOnlySet<string>, CancellationToken,
        Task<CompatibilityEvaluation>> _evaluator;
    private readonly bool _continueDestinationAvailable;
    private readonly ICompatibilityMemoryRecovery _memoryRecovery;
    private readonly ICompatibilityActionAuthority _actionAuthority;
    private readonly Func<CompatibilityEvaluation, CurrentModelLaunchHandoff?>
        _currentModelHandoffResolver;
    private readonly TimeProvider _timeProvider;
    private readonly object _attemptGate = new();

    private AttemptCancellation? _attemptCancellation;
    private AttemptCancellation? _auxiliaryCancellation;
    private int _attemptGeneration;
    private long _operationSerial;
    private long _recoveryOwner;
    private long _taskManagerOwner;
    private CompatibilityScreenModel? _lastModel;
    private CompatibilityEvaluation? _lastEvaluation;
    private readonly HashSet<string> _experimentalConsentEvidenceIds =
        new(StringComparer.Ordinal);
    private bool _experimentalFinalConfirmation;
    private bool _optionalOptimizationOpen;
    private CurrentModelLaunchHandoff? _currentModelLaunchHandoff;

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
            async (_, token) => new CompatibilityEvaluation(
                await evaluator(token).ConfigureAwait(false),
                PlanningSession: null,
                CurrentConfiguration: null),
            continueDestinationAvailable,
            memoryRecovery,
            actionAuthority: null,
            currentModelHandoffResolver: null,
            timeProvider: null)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
    }

    internal CompatibilityViewModel(
        Func<IReadOnlySet<string>, CancellationToken,
            Task<CompatibilityEvaluation>> evaluator,
        bool continueDestinationAvailable = true,
        ICompatibilityMemoryRecovery? memoryRecovery = null,
        ICompatibilityActionAuthority? actionAuthority = null,
        Func<CompatibilityEvaluation, CurrentModelLaunchHandoff?>?
            currentModelHandoffResolver = null,
        TimeProvider? timeProvider = null)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _continueDestinationAvailable = continueDestinationAvailable;
        _memoryRecovery = memoryRecovery ?? new WindowsCompatibilityMemoryRecovery([]);
        _actionAuthority = actionAuthority
            ?? UnavailableCompatibilityActionAuthority.Instance;
        _currentModelHandoffResolver = currentModelHandoffResolver
            ?? (static _ => null);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _uiContext = SynchronizationContext.Current;

        // Assigned before the commands, because their guards read it: a command
        // must never be asked whether it can run before there is a state to ask
        // about.
        Presentation = CompatibilityPresentationFactory.Analysing(0);

        ContinueCommand = new DelegateCommand(
            Continue,
            CanContinue);

        BackCommand = new DelegateCommand(
            Back,
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
        OptionalOptimizationCommand = new DelegateCommand(
            BeginOptionalOptimization,
            () => CanOptimiseFirst);
    }

    /// <summary>Raised whenever a new snapshot is ready to render.</summary>
    internal event EventHandler<CompatibilityPresentation>? PresentationChanged;

    internal event EventHandler<CompatibilityAuxiliaryStatus>? AuxiliaryStatusChanged;

    internal event EventHandler? ContinueRequested;

    internal event EventHandler<OptimizationRequestedEventArgs>?
        OptimizationRequested;

    internal event EventHandler<CurrentModelChatRequestedEventArgs>?
        CurrentModelChatRequested;

    internal event EventHandler? BackRequested;

    internal CompatibilityPresentation Presentation { get; private set; }

    internal DelegateCommand ContinueCommand { get; }

    internal DelegateCommand BackCommand { get; }

    internal DelegateCommand CancelCommand { get; }

    internal DelegateCommand RetryCommand { get; }

    internal DelegateCommand RefreshMemoryCommand { get; }

    internal DelegateCommand OpenTaskManagerCommand { get; }

    internal DelegateCommand OptionalOptimizationCommand { get; }

    internal CompatibilityAuxiliaryStatus AuxiliaryStatus { get; private set; } =
        CompatibilityAuxiliaryStatus.None;

    internal OptimizationPreferenceSelection? SelectedPreference { get; private set; }

    internal OptimizationSelectionHandoff? CurrentOptimizationHandoff
        { get; private set; }

    internal bool CanChatWithCurrentModel =>
        !_optionalOptimizationOpen
        && _lastEvaluation?.Screen.State
            == CompatibilityScreenState.EstimatedCompatible
        && _currentModelLaunchHandoff is { } handoff
        && _actionAuthority.IsCurrentModelChatAvailable(handoff.Route);

    internal bool CanOptimiseFirst =>
        !_optionalOptimizationOpen
        && _lastEvaluation is
            { PlanningSession: not null, OptionalOptimization: not null } evaluation
        && HasOptimizationAuthority(evaluation.PlanningSession.Route);

    internal string? AvailableExperimentalConsentEvidenceId
    {
        get
        {
            string[] options = (_lastEvaluation?.ExperimentalConsentOptions
                    .Select(option => option.EvidenceId)
                    ?? [])
                .Concat(_experimentalConsentEvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return options.Length == 1 ? options[0] : null;
        }
    }

    internal bool IsExperimentalConsentGranted =>
        AvailableExperimentalConsentEvidenceId is { } evidenceId
        && _experimentalConsentEvidenceIds.Contains(evidenceId);

    internal bool RequiresExperimentalConfirmation =>
        SelectedExperimentalEvidenceId() is not null;

    internal bool CanConfirmExperimentalPlan
    {
        get
        {
            if (_lastEvaluation?.PlanningSession is null
                || SelectedPreference is null)
            {
                return false;
            }
            string? evidenceId = SelectedExperimentalEvidenceId();
            return evidenceId is null
                || _experimentalConsentEvidenceIds.Contains(evidenceId)
                    && _experimentalFinalConfirmation;
        }
    }

    /// <summary>
    /// Starts one attempt. Safe to call again: the previous attempt is cancelled
    /// and its result, if it arrives late, is discarded.
    /// </summary>
    internal Task StartAsync()
    {
        ClearExperimentalConsent();
        return RunAttemptAsync(preparation: null);
    }

    internal Task<bool> SetExperimentalConsentAsync(
        string evidenceId,
        bool granted)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            return Task.FromResult(false);
        }

        bool changed;
        lock (_attemptGate)
        {
            bool known = _experimentalConsentEvidenceIds.Contains(evidenceId)
                || _lastEvaluation?.ExperimentalConsentOptions.Any(option =>
                    string.Equals(
                        option.EvidenceId,
                        evidenceId,
                        StringComparison.Ordinal)) == true;
            if (!known)
            {
                return Task.FromResult(false);
            }

            changed = granted
                ? _experimentalConsentEvidenceIds.Add(evidenceId)
                : _experimentalConsentEvidenceIds.Remove(evidenceId);
            _experimentalFinalConfirmation = false;
        }

        return changed
            ? ReevaluateExperimentalConsentAsync()
            : Task.FromResult(true);
    }

    internal void SetExperimentalFinalConfirmation(bool confirmed)
    {
        string? evidenceId = SelectedExperimentalEvidenceId();
        _experimentalFinalConfirmation = confirmed
            && evidenceId is not null
            && _experimentalConsentEvidenceIds.Contains(evidenceId);
        if (_lastEvaluation is { } evaluation
            && SelectedPreference is { } preference)
        {
            CurrentOptimizationHandoff = TryIssueOptimization(
                evaluation,
                preference);
            Presentation = Presentation with
            {
                PrimaryActionEnabled = _continueDestinationAvailable
                    && CurrentOptimizationHandoff is not null
            };
        }
        ContinueCommand.RaiseCanExecuteChanged();
        PresentationChanged?.Invoke(this, Presentation);
    }

    internal void BeginOptionalOptimization()
    {
        if (!CanOptimiseFirst
            || _lastEvaluation is not
                { OptionalOptimization: { } optimization } evaluation)
        {
            return;
        }

        _optionalOptimizationOpen = true;
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Automatic();
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.OptionalOptimization(
                evaluation.Screen,
                optimization,
                preference);
        OptimizationSelectionHandoff? handoff = TryIssueOptimization(
            evaluation,
            preference);
        Publish(
            presentation with
            {
                PrimaryActionEnabled = handoff is not null
            },
            Volatile.Read(ref _attemptGeneration),
            () =>
            {
                SelectedPreference = preference;
                CurrentOptimizationHandoff = handoff;
                _experimentalFinalConfirmation = false;
            });
    }

    private void Continue()
    {
        if (!CanContinue())
        {
            return;
        }

        if (TryCurrentOptimizationContext(out OptimizationJourneyEntryContext? context))
        {
            OptimizationRequested?.Invoke(
                this,
                new OptimizationRequestedEventArgs(context!));
            ContinueRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (CanChatWithCurrentModel
            && _currentModelLaunchHandoff is { } current)
        {
            CurrentModelChatRequested?.Invoke(
                this,
                new CurrentModelChatRequestedEventArgs(current));
            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool CanContinue() =>
        Presentation.PrimaryActionEnabled
        && (TryCurrentOptimizationContext(out _)
            || CanChatWithCurrentModel);

    private bool TryCurrentOptimizationContext(
        out OptimizationJourneyEntryContext? context)
    {
        context = null;
        if (CurrentOptimizationHandoff is not { } optimization)
        {
            return false;
        }

        OptimizationJourneyOrigin origin = _optionalOptimizationOpen
            ? OptimizationJourneyOrigin.Optional
            : OptimizationJourneyOrigin.Required;
        CurrentModelLaunchHandoff? fallback =
            origin == OptimizationJourneyOrigin.Optional
                ? _currentModelLaunchHandoff
                : null;
        try
        {
            context = new OptimizationJourneyEntryContext(
                optimization,
                origin,
                fallback);
            return true;
        }
        catch (ArgumentException)
        {
            // A resolver supplied identities from a different journey. This is
            // a stale-authority condition, not a recoverable UI substitution.
            return false;
        }
    }

    private void Back()
    {
        if (_optionalOptimizationOpen
            && _lastEvaluation is { } evaluation)
        {
            _optionalOptimizationOpen = false;
            CurrentOptimizationHandoff = null;
            SelectedPreference = null;
            Publish(
                CurrentFitPresentation(evaluation),
                Volatile.Read(ref _attemptGeneration));
            return;
        }
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool HasOptimizationAuthority(OptimizationRoute route) =>
        _actionAuthority.TryGetOptimizationAuthority(
            route,
            out IOptimizationExecutionPayloadComposer? composer,
            out OptimizationIssuanceAuthority? authority)
        && composer is not null
        && authority is not null;

    private OptimizationSelectionHandoff? TryIssueOptimization(
        CompatibilityEvaluation evaluation,
        OptimizationPreferenceSelection preference)
    {
        if (evaluation.PlanningSession is not { } session
            || !_actionAuthority.TryGetOptimizationAuthority(
                session.Route,
                out IOptimizationExecutionPayloadComposer? composer,
                out OptimizationIssuanceAuthority? authority)
            || composer is null
            || authority is null)
        {
            return null;
        }

        string? experimental = session.RequiredExperimentalEvidenceId(preference);
        if (experimental is not null
            && (!_experimentalConsentEvidenceIds.Contains(experimental)
                || !_experimentalFinalConfirmation))
        {
            return null;
        }

        try
        {
            OptimizationExecutionPlan plan = session.Issue(
                preference,
                composer,
                authority,
                _timeProvider);
            return OptimizationSelectionHandoff.TryCreate(
                plan,
                session,
                preference,
                out OptimizationSelectionHandoff? handoff)
                    ? handoff
                    : null;
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private CompatibilityPresentation CurrentFitPresentation(
        CompatibilityEvaluation evaluation)
    {
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(evaluation.Screen);
        bool chat = _currentModelLaunchHandoff is { } handoff
            && _actionAuthority.IsCurrentModelChatAvailable(handoff.Route);
        return presentation with
        {
            PrimaryActionText = chat
                ? "Chat with current model"
                : "Coming later",
            PrimaryActionEnabled = chat
        };
    }

    private async Task<bool> ReevaluateExperimentalConsentAsync()
    {
        try
        {
            await RunAttemptAsync(preparation: null).ConfigureAwait(true);
            return true;
        }
        catch
        {
            return false;
        }
    }

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

        ClearExperimentalConsent();

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
            IReadOnlySet<string> consentSnapshot;
            lock (_attemptGate)
            {
                consentSnapshot = new HashSet<string>(
                    _experimentalConsentEvidenceIds,
                    StringComparer.Ordinal);
            }
            CompatibilityEvaluation evaluation = await _evaluator(
                    consentSnapshot,
                    token)
                .ConfigureAwait(true);
            CompatibilityScreenModel model = evaluation.Screen;

            OptimizationPreferenceSelection? preference =
                model.State == CompatibilityScreenState.OptimisationRequired
                ? OptimizationPreferenceSelection.Automatic()
                : null;
            CompatibilityPresentation presentation = preference is null
                ? CompatibilityPresentationFactory.From(model)
                : CompatibilityPresentationFactory.From(model, preference);
            CurrentModelLaunchHandoff? currentHandoff =
                model.State == CompatibilityScreenState.EstimatedCompatible
                    ? _currentModelHandoffResolver(evaluation)
                    : null;
            OptimizationSelectionHandoff? optimizationHandoff =
                preference is not null
                    && evaluation.PlanningSession?
                        .RequiredExperimentalEvidenceId(preference) is null
                    ? TryIssueOptimization(evaluation, preference)
                    : null;
            if (model.State == CompatibilityScreenState.EstimatedCompatible)
            {
                bool canChat = currentHandoff is { } handoff
                    && _actionAuthority.IsCurrentModelChatAvailable(handoff.Route);
                presentation = presentation with
                {
                    PrimaryActionText = canChat
                        ? "Chat with current model"
                        : "Coming later",
                    PrimaryActionEnabled = canChat
                };
            }
            else if (preference is not null)
            {
                presentation = presentation with
                {
                    PrimaryActionEnabled = optimizationHandoff is not null
                };
            }
            Publish(
                presentation,
                generation,
                () =>
                {
                    _lastModel = model;
                    _lastEvaluation = evaluation;
                    SelectedPreference = preference;
                    CurrentOptimizationHandoff = optimizationHandoff;
                    _currentModelLaunchHandoff = currentHandoff;
                    _optionalOptimizationOpen = false;
                    _experimentalFinalConfirmation = false;
                });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Publish(
                CompatibilityPresentationFactory.Cancelled(),
                generation,
                () =>
                {
                    ClearCompatibilitySnapshot();
                    ClearExperimentalConsent();
                });
        }
        catch
        {
            Publish(
                CompatibilityPresentationFactory.OperationalFailure(),
                generation,
                () =>
                {
                    ClearCompatibilitySnapshot();
                    ClearExperimentalConsent();
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
                ClearCompatibilitySnapshot();
                ClearExperimentalConsent();
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
                ClearCompatibilitySnapshot();
                ClearExperimentalConsent();
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

        cancellation?.Cancel();
        auxiliary?.Cancel();
        PublishAuxiliaryStatus(CompatibilityAuxiliaryStatus.None);
        RaiseRecoveryCanExecuteChanged();
        ClearCompatibilitySnapshot();
        ClearExperimentalConsent();
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
        _lastEvaluation = null;
        SelectedPreference = null;
        CurrentOptimizationHandoff = null;
        _currentModelLaunchHandoff = null;
        _optionalOptimizationOpen = false;
        _experimentalFinalConfirmation = false;
    }

    private void ClearExperimentalConsent()
    {
        lock (_attemptGate)
        {
            _experimentalConsentEvidenceIds.Clear();
            _experimentalFinalConfirmation = false;
        }
    }

    private string? SelectedExperimentalEvidenceId()
    {
        if (_lastEvaluation?.PlanningSession is not { } session
            || SelectedPreference is not { } preference)
        {
            return null;
        }

        try
        {
            return session.RequiredExperimentalEvidenceId(preference);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    internal void SelectAutomaticPreference()
    {
        if (_lastModel is not { } model
            || _lastEvaluation is not { } evaluation
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen)
        {
            return;
        }

        int generation = Volatile.Read(ref _attemptGeneration);
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Automatic();
        _experimentalFinalConfirmation = false;
        OptimizationSelectionHandoff? handoff = TryIssueOptimization(
            evaluation,
            preference);
        Publish(
            SelectionPresentation(evaluation, preference) with
            {
                PrimaryActionEnabled = handoff is not null
            },
            generation,
            () =>
            {
                SelectedPreference = preference;
                CurrentOptimizationHandoff = handoff;
            });
    }

    internal void SelectManualPreference(int value)
    {
        if (_lastModel is not { } model
            || _lastEvaluation is not { } evaluation
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen)
        {
            return;
        }

        int generation = Volatile.Read(ref _attemptGeneration);
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Manual(value);
        _experimentalFinalConfirmation = false;
        OptimizationSelectionHandoff? handoff = TryIssueOptimization(
            evaluation,
            preference);
        CompatibilityPresentation next = SelectionPresentation(
            evaluation,
            preference) with
        {
            PrimaryActionEnabled = handoff is not null
        };
        if (SelectedPreference?.Kind == OptimizationPreferenceKind.Manual
            && Presentation.Optimization is { } current
            && next.Optimization is { } candidate
            && string.Equals(
                current.SelectedMode.Label,
                candidate.SelectedMode.Label,
                StringComparison.Ordinal))
        {
            // Preserve the exact user value for the future handoff, but a tick
            // inside the same effective band changes no rendered state.
            SelectedPreference = preference;
            CurrentOptimizationHandoff = handoff;
            bool primaryActionEnabled = _continueDestinationAvailable
                && handoff is not null;
            if (Presentation.PrimaryActionEnabled != primaryActionEnabled)
            {
                Presentation = Presentation with
                {
                    PrimaryActionEnabled = primaryActionEnabled
                };
                PresentationChanged?.Invoke(this, Presentation);
            }
            ContinueCommand.RaiseCanExecuteChanged();
            return;
        }
        Publish(
            next,
            generation,
            () =>
            {
                SelectedPreference = preference;
                CurrentOptimizationHandoff = handoff;
            });
    }

    private CompatibilityPresentation SelectionPresentation(
        CompatibilityEvaluation evaluation,
        OptimizationPreferenceSelection preference) =>
        _optionalOptimizationOpen
            && evaluation.OptionalOptimization is { } optional
                ? CompatibilityPresentationFactory.OptionalOptimization(
                    evaluation.Screen,
                    optional,
                    preference)
                : CompatibilityPresentationFactory.From(
                    evaluation.Screen,
                    preference);

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
            if (!_continueDestinationAvailable
                && presentation.PrimaryActionText is
                    "Continue" or
                    "Choose optimisation" or
                    "Chat with current model")
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
            OptionalOptimizationCommand.RaiseCanExecuteChanged();
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
