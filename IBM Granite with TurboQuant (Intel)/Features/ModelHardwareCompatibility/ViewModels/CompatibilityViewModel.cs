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
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Planning;

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
    private AttemptCancellation? _experimentalConsentCancellation;
    private AttemptCancellation? _optimizationStartCancellation;
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
    private bool _optimizationStartBusy;
    private bool _optionalOptimizationOpen;
    private CurrentModelLaunchHandoff? _currentModelLaunchHandoff;
    private OptimizationPreferenceSelection? _recommendedPreference;
    private OptimizationPreferenceSelection? _exactPreference;

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

        StartOptimizationCommand = new DelegateCommand(
            StartOptimizationFromCommand,
            CanStartOptimization);

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
        ChatCurrentModelCommand = new DelegateCommand(
            ChatCurrentModel,
            () => CanChatWithCurrentModel);
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

    internal DelegateCommand StartOptimizationCommand { get; }

    internal DelegateCommand BackCommand { get; }

    internal DelegateCommand CancelCommand { get; }

    internal DelegateCommand RetryCommand { get; }

    internal DelegateCommand RefreshMemoryCommand { get; }

    internal DelegateCommand OpenTaskManagerCommand { get; }

    internal DelegateCommand OptionalOptimizationCommand { get; }

    internal DelegateCommand ChatCurrentModelCommand { get; }

    internal CompatibilityAuxiliaryStatus AuxiliaryStatus { get; private set; } =
        CompatibilityAuxiliaryStatus.None;

    internal OptimizationPreferenceSelection? SelectedPreference { get; private set; }

    internal OptimizationSelectionHandoff? CurrentOptimizationHandoff
        { get; private set; }

    internal bool IsOptimizationStartBusy => _optimizationStartBusy;

    internal bool CanChatWithCurrentModel =>
        _lastEvaluation?.Screen.State
            == CompatibilityScreenState.EstimatedCompatible
        && _currentModelLaunchHandoff is { } handoff
        && _actionAuthority.IsCurrentModelChatAvailable(handoff.Route);

    internal bool CanOptimiseFirst =>
        !_optionalOptimizationOpen
        && _lastEvaluation is
            { PlanningSession: not null, OptionalOptimization: not null } evaluation
        && HasOptimizationAuthority(evaluation.PlanningSession.Route);

    internal string? SelectedExperimentalConsentEvidenceId =>
        ResolveSelectedExperimentalEvidenceId();

    internal string? AvailableExperimentalConsentEvidenceId =>
        SelectedExperimentalConsentEvidenceId;

    internal bool IsExperimentalConsentGranted =>
        SelectedExperimentalConsentEvidenceId is { } evidenceId
        && _experimentalConsentEvidenceIds.Contains(evidenceId);

    internal bool IsExperimentalFinalConfirmationGranted =>
        IsExperimentalConsentGranted
        && _experimentalFinalConfirmation;

    internal bool RequiresExperimentalConfirmation =>
        SelectedExperimentalConsentEvidenceId is not null;

    internal bool CanConfirmExperimentalPlan
    {
        get
        {
            if (_lastEvaluation?.PlanningSession is null
                || SelectedPreference is null)
            {
                return false;
            }
            string? evidenceId = SelectedExperimentalConsentEvidenceId;
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
        CancelOptimizationStartIntent();
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
            string? selectedEvidenceId =
                ResolveSelectedExperimentalEvidenceId();
            bool isExactSelectedEvidence = string.Equals(
                selectedEvidenceId,
                evidenceId,
                StringComparison.Ordinal);
            bool known = _lastEvaluation?.ExperimentalConsentOptions.Any(option =>
                string.Equals(
                    option.EvidenceId,
                    evidenceId,
                    StringComparison.Ordinal)) == true;
            if (!isExactSelectedEvidence || !known)
            {
                return Task.FromResult(false);
            }

            changed = granted
                ? _experimentalConsentEvidenceIds.Add(evidenceId)
                : _experimentalConsentEvidenceIds.Remove(evidenceId);
            if (changed)
            {
                _experimentalFinalConfirmation = false;
            }
        }

        return changed
            ? ReevaluateExperimentalConsentAsync()
            : Task.FromResult(true);
    }

    internal void SetExperimentalFinalConfirmation(bool confirmed)
    {
        string? evidenceId = SelectedExperimentalConsentEvidenceId;
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
        StartOptimizationCommand.RaiseCanExecuteChanged();
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
                evaluation,
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
                _recommendedPreference = preference;
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

        if (HasOptimizationSelection)
        {
            // Configure's Start action never degrades into current-model chat.
            // A stale or mismatched plan must fail closed; Chat has its own
            // explicit command and exact current-model handoff
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

    private async void StartOptimizationFromCommand()
    {
        try
        {
            await StartOptimizationAsync().ConfigureAwait(true);
        }
        catch
        {
            // StartOptimizationAsync publishes a bounded, privacy-safe failure.
            // the command boundary must not surface an async-void exception
        }
    }

    private bool CanStartOptimization() =>
        !_optimizationStartBusy
        && TryGetSelectedSafeIdentity(out _);

    private bool TryGetSelectedSafeIdentity(out string candidateIdentity)
    {
        candidateIdentity = string.Empty;
        if (!_continueDestinationAvailable
            || _lastModel is not { } model
            || _lastEvaluation?.PlanningSession is not { } session
            || !HasOptimizationAuthority(session.Route)
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen
            || SelectedPreference is not
                { Kind: OptimizationPreferenceKind.Exact } preference
            || string.IsNullOrWhiteSpace(preference.ExactCandidateIdentity)
            || Presentation.Optimization is not
                { IsActionAuthoritative: true } optimization
            || !string.IsNullOrWhiteSpace(optimization.SelectionStatusText))
        {
            return false;
        }

        if (optimization.SafeSliderSelectedIndex is null)
            return TryGetAcknowledgedExactIdentity(optimization, preference, session, out candidateIdentity);
        if (optimization.SafeSliderSelectedIndex is not int selectedIndex
            || selectedIndex < 0 || selectedIndex >= optimization.SafeSliderModes.Count) return false;

        string selectedIdentity =
            optimization.SafeSliderModes[selectedIndex].CandidateIdentity;
        if (!string.Equals(
                preference.ExactCandidateIdentity,
                selectedIdentity,
                StringComparison.Ordinal)
            || optimization.SafeSliderModes.Count(mode => string.Equals(
                mode.CandidateIdentity,
                selectedIdentity,
                StringComparison.Ordinal)) != 1)
        {
            return false;
        }

        candidateIdentity = selectedIdentity;
        return true;
    }

    private bool TryGetAcknowledgedExactIdentity(
        CompatibilityOptimizationPresentation optimization,
        OptimizationPreferenceSelection preference,
        CompatibilityPlanningSession session,
        out string candidateIdentity)
    {
        candidateIdentity = string.Empty;
        if (!optimization.IsActionAuthoritative || optimization.Preference != preference
            || !string.IsNullOrWhiteSpace(optimization.SelectionStatusText)
            || preference.Kind != OptimizationPreferenceKind.Exact
            || string.IsNullOrWhiteSpace(preference.ExactCandidateIdentity)) return false;
        var matches = optimization.ExactSafeModes.Where(mode => string.Equals(
            mode.CandidateIdentity, preference.ExactCandidateIdentity, StringComparison.Ordinal)).Take(2).ToArray();
        if (matches.Length != 1) return false;
        try
        {
            string? evidenceId = session.RequiredExperimentalEvidenceId(preference);
            if (evidenceId is not null)
            {
                if (!matches[0].Mode.IsExperimental || !_experimentalConsentEvidenceIds.Contains(evidenceId)
                    || !_experimentalFinalConfirmation) return false;
            }
            else if (matches[0].Mode.IsExperimental) return false;
        }
        catch (InvalidOperationException) { return false; }
        candidateIdentity = preference.ExactCandidateIdentity;
        return true;
    }

    internal async Task StartOptimizationAsync()
    {
        if (!CanStartOptimization()
            || !TryGetSelectedSafeIdentity(out string candidateIdentity))
        {
            return;
        }

        bool wasOptionalOptimizationOpen = _optionalOptimizationOpen;
        string? experimentalEvidenceId = ResolveSelectedExperimentalEvidenceId();
        CompatibilityPresentation retainedPresentation = Presentation;
        AttemptCancellation cancellation = new();
        CancellationToken token = cancellation.Token;
        int generation;
        IReadOnlySet<string> consentSnapshot;
        lock (_attemptGate)
        {
            if (_optimizationStartBusy
                || !TryGetSelectedSafeIdentity(out string currentIdentity)
                || !string.Equals(
                    candidateIdentity,
                    currentIdentity,
                    StringComparison.Ordinal))
            {
                cancellation.Complete();
                return;
            }

            generation = unchecked(++_attemptGeneration);
            _optimizationStartBusy = true;
            _optimizationStartCancellation = cancellation;
            consentSnapshot = new HashSet<string>(
                _experimentalConsentEvidenceIds,
                StringComparer.Ordinal);
            CurrentOptimizationHandoff = null;
        }

        Publish(
            retainedPresentation with
            {
                PrimaryActionEnabled = false,
                Optimization = retainedPresentation.Optimization is { } optimization
                    ? optimization with
                    {
                        SelectionStatusText =
                            "Checking current memory and storage…"
                    }
                    : null
            },
            generation);

        try
        {
            CompatibilityEvaluation evaluation = await _evaluator(
                    consentSnapshot,
                    token)
                .ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _attemptGeneration))
            {
                return;
            }

            CompatibilityScreenModel model = evaluation.Screen;
            bool freshRequired = !wasOptionalOptimizationOpen
                && model.State == CompatibilityScreenState.OptimisationRequired;
            bool freshOptional = wasOptionalOptimizationOpen
                && model.State == CompatibilityScreenState.EstimatedCompatible
                && evaluation.OptionalOptimization is not null;
            if ((!freshRequired && !freshOptional)
                || evaluation.PlanningSession is not { } session
                || !HasOptimizationAuthority(session.Route))
            {
                FailClosedOptimizationStart(retainedPresentation, generation);
                return;
            }

            OptimizationPreferenceSelection preference;
            CompatibilityPresentation freshPresentation;
            try
            {
                preference = OptimizationPreferenceSelection.Exact(candidateIdentity);
                freshPresentation = freshOptional
                    ? CompatibilityPresentationFactory.OptionalOptimization(
                        evaluation,
                        evaluation.OptionalOptimization!,
                        preference)
                    : CompatibilityPresentationFactory.From(
                        evaluation,
                        preference);
                if (!string.Equals(experimentalEvidenceId,
                    session.RequiredExperimentalEvidenceId(preference), StringComparison.Ordinal))
                {
                    FailClosedOptimizationStart(retainedPresentation, generation);
                    return;
                }
            }
            catch (Exception exception) when (exception is
                ArgumentException or InvalidOperationException)
            {
                FailClosedOptimizationStart(retainedPresentation, generation);
                return;
            }

            if (freshPresentation.Optimization is not
                    { IsActionAuthoritative: true } freshOptimization
                || freshOptimization.Preference != preference
                || !(freshOptimization.SafeSliderSelectedIndex is null
                    && TryGetAcknowledgedExactIdentity(freshOptimization, preference, session, out string freshExactIdentity)
                    && string.Equals(freshExactIdentity, candidateIdentity, StringComparison.Ordinal))
                && (freshOptimization.SafeSliderSelectedIndex is not int freshIndex
                || freshIndex < 0
                || freshIndex >= freshOptimization.SafeSliderModes.Count
                || freshOptimization.SafeSliderModes.Count(mode => string.Equals(
                    mode.CandidateIdentity,
                    candidateIdentity,
                    StringComparison.Ordinal)) != 1
                || !string.Equals(
                    freshOptimization.SafeSliderModes[freshIndex].CandidateIdentity,
                    candidateIdentity,
                    StringComparison.Ordinal)))
            {
                FailClosedOptimizationStart(retainedPresentation, generation);
                return;
            }

            OptimizationSelectionHandoff? handoff = TryIssueOptimization(
                evaluation,
                preference);
            if (handoff is null)
            {
                FailClosedOptimizationStart(retainedPresentation, generation);
                return;
            }

            CurrentModelLaunchHandoff? currentHandoff =
                model.State == CompatibilityScreenState.EstimatedCompatible
                    ? _currentModelHandoffResolver(evaluation)
                    : null;
            OptimizationJourneyEntryContext freshContext;
            try
            {
                freshContext = new OptimizationJourneyEntryContext(
                    handoff,
                    freshOptional
                        ? OptimizationJourneyOrigin.Optional
                        : OptimizationJourneyOrigin.Required,
                    freshOptional ? currentHandoff : null);
            }
            catch (ArgumentException)
            {
                FailClosedOptimizationStart(retainedPresentation, generation);
                return;
            }
            lock (_attemptGate)
            {
                if (generation != _attemptGeneration
                    || !ReferenceEquals(_optimizationStartCancellation, cancellation))
                {
                    return;
                }
                _optimizationStartCancellation = null;
                _optimizationStartBusy = false;
            }

            Publish(
                freshPresentation with { PrimaryActionEnabled = true },
                generation,
                () =>
                {
                    _lastModel = model;
                    _lastEvaluation = evaluation;
                    SelectedPreference = preference;
                    _exactPreference = preference;
                    CurrentOptimizationHandoff = handoff;
                    _currentModelLaunchHandoff = currentHandoff;
                    _optionalOptimizationOpen = freshOptional;
                });

            if (generation == Volatile.Read(ref _attemptGeneration)
                && ReferenceEquals(CurrentOptimizationHandoff, handoff))
            {
                OptimizationRequested?.Invoke(
                    this,
                    new OptimizationRequestedEventArgs(freshContext));
                ContinueRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // the newer selection, page stage, or attempt owns the screen now
        }
        catch
        {
            FailClosedOptimizationStart(retainedPresentation, generation);
        }
        finally
        {
            lock (_attemptGate)
            {
                if (ReferenceEquals(_optimizationStartCancellation, cancellation))
                {
                    _optimizationStartCancellation = null;
                    _optimizationStartBusy = false;
                }
            }
            cancellation.Complete();
            StartOptimizationCommand.RaiseCanExecuteChanged();
        }
    }

    private void FailClosedOptimizationStart(
        CompatibilityPresentation retainedPresentation,
        int generation)
    {
        lock (_attemptGate)
        {
            if (generation != _attemptGeneration)
            {
                return;
            }
            _optimizationStartCancellation = null;
            _optimizationStartBusy = false;
        }

        Publish(
            retainedPresentation with
            {
                PrimaryActionEnabled = false,
                Optimization = retainedPresentation.Optimization is { } optimization
                    ? optimization with
                    {
                        SelectionStatusText =
                            "That exact setup is no longer available after checking current memory and storage. Go back and check compatibility again."
                    }
                    : null
            },
            generation,
            () =>
            {
                CurrentOptimizationHandoff = null;
                _currentModelLaunchHandoff = null;
            });
    }

    private void ChatCurrentModel()
    {
        if (!CanChatWithCurrentModel
            || _currentModelLaunchHandoff is not { } current)
        {
            return;
        }

        CurrentModelChatRequested?.Invoke(
            this,
            new CurrentModelChatRequestedEventArgs(current));
    }

    private bool CanContinue()
    {
        if (!Presentation.PrimaryActionEnabled)
        {
            return false;
        }

        return HasOptimizationSelection
            ? TryCurrentOptimizationContext(out _)
            : CanChatWithCurrentModel;
    }

    private bool HasOptimizationSelection =>
        SelectedPreference is not null
        || CurrentOptimizationHandoff is not null;

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
            // a resolver supplied identities from a different journey. this is
            // a stale-authority condition, not a recoverable UI substitution.
            return false;
        }
    }

    private void Back()
    {
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

    private async Task<bool> ReevaluateExperimentalConsentAsync()
    {
        if (_lastEvaluation is null
            || SelectedPreference is not { } selectedPreference
            || Presentation.Optimization?.SelectedMode is not { } selectedMode
            || ResolveSelectedExperimentalEvidenceId() is not { } selectedEvidenceId)
        {
            return false;
        }

        OptimizationModeIdentity selectedIdentity = IdentityOf(selectedMode);
        bool wasOptionalOptimizationOpen = _optionalOptimizationOpen;
        CompatibilityPresentation retainedPresentation = Presentation;
        AttemptCancellation cancellation = new();
        CancellationToken token = cancellation.Token;
        AttemptCancellation? previous;
        int generation;
        IReadOnlySet<string> consentSnapshot;
        lock (_attemptGate)
        {
            generation = unchecked(++_attemptGeneration);
            previous = _attemptCancellation;
            _attemptCancellation = cancellation;
            _experimentalConsentCancellation = cancellation;
            consentSnapshot = new HashSet<string>(
                _experimentalConsentEvidenceIds,
                StringComparer.Ordinal);
        }
        previous?.Cancel();

        try
        {
            CompatibilityEvaluation evaluation = await _evaluator(
                    consentSnapshot,
                    token)
                .ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _attemptGeneration))
            {
                return false;
            }

            CompatibilityScreenModel model = evaluation.Screen;
            bool requiredOptimization =
                model.State == CompatibilityScreenState.OptimisationRequired;
            bool optionalOptimization =
                model.State == CompatibilityScreenState.EstimatedCompatible
                && evaluation.OptionalOptimization is not null
                && evaluation.PlanningSession is { } optionalSession
                && HasOptimizationAuthority(optionalSession.Route);
            if (wasOptionalOptimizationOpen != optionalOptimization
                || !wasOptionalOptimizationOpen && !requiredOptimization
                || evaluation.PlanningSession is not { } session
                || !evaluation.ExperimentalConsentOptions.Any(option =>
                    string.Equals(
                        option.EvidenceId,
                        selectedEvidenceId,
                        StringComparison.Ordinal)))
            {
                FailClosedExperimentalConsent(
                    selectedEvidenceId,
                    retainedPresentation,
                    generation);
                return false;
            }

            string? freshEvidenceId;
            CompatibilityPresentation freshPresentation;
            try
            {
                freshEvidenceId = session.RequiredExperimentalEvidenceId(
                    selectedPreference);
                freshPresentation = wasOptionalOptimizationOpen
                    ? CompatibilityPresentationFactory.OptionalOptimization(
                        evaluation,
                        evaluation.OptionalOptimization!,
                        selectedPreference)
                    : CompatibilityPresentationFactory.From(
                        evaluation,
                        selectedPreference);
            }
            catch (InvalidOperationException)
            {
                FailClosedExperimentalConsent(
                    selectedEvidenceId,
                    retainedPresentation,
                    generation);
                return false;
            }

            if (!string.Equals(
                    freshEvidenceId,
                    selectedEvidenceId,
                    StringComparison.Ordinal)
                || freshPresentation.Optimization is not
                    { IsActionAuthoritative: true } freshOptimization
                || freshOptimization.Preference != selectedPreference
                || IdentityOf(freshOptimization.SelectedMode) != selectedIdentity)
            {
                FailClosedExperimentalConsent(
                    selectedEvidenceId,
                    retainedPresentation,
                    generation);
                return false;
            }

            CurrentModelLaunchHandoff? currentHandoff =
                model.State == CompatibilityScreenState.EstimatedCompatible
                    ? _currentModelHandoffResolver(evaluation)
                    : null;
            Publish(
                freshPresentation with { PrimaryActionEnabled = false },
                generation,
                () =>
                {
                    _lastModel = model;
                    _lastEvaluation = evaluation;
                    SelectedPreference = selectedPreference;
                    CurrentOptimizationHandoff = null;
                    _currentModelLaunchHandoff = currentHandoff;
                    _optionalOptimizationOpen = wasOptionalOptimizationOpen;
                    _experimentalFinalConfirmation = false;
                });
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            FailClosedExperimentalConsent(
                selectedEvidenceId,
                retainedPresentation,
                generation);
            return false;
        }
        finally
        {
            lock (_attemptGate)
            {
                if (ReferenceEquals(_attemptCancellation, cancellation))
                {
                    _attemptCancellation = null;
                }
                if (ReferenceEquals(
                        _experimentalConsentCancellation,
                        cancellation))
                {
                    _experimentalConsentCancellation = null;
                }
            }
            cancellation.Complete();
        }
    }

    private void FailClosedExperimentalConsent(
        string evidenceId,
        CompatibilityPresentation retainedPresentation,
        int generation)
    {
        if (generation != Volatile.Read(ref _attemptGeneration))
        {
            return;
        }

        lock (_attemptGate)
        {
            _experimentalConsentEvidenceIds.Remove(evidenceId);
            _experimentalFinalConfirmation = false;
        }
        CompatibilityPresentation failedPresentation = retainedPresentation with
        {
            PrimaryActionEnabled = false,
            Optimization = retainedPresentation.Optimization is { } optimization
                ? optimization with
                {
                    SelectionStatusText =
                        "That experimental preview could not be confirmed. Choose another setup or try again."
                }
                : null
        };
        Publish(
            failedPresentation,
            generation,
            () => CurrentOptimizationHandoff = null);
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
        CancelOptimizationStartIntent(publishRetainedPresentation: false);
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
            // slow adapter cannot freeze the page once adapters exist
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

            bool requiredOptimization =
                model.State == CompatibilityScreenState.OptimisationRequired;
            bool optionalOptimization =
                model.State == CompatibilityScreenState.EstimatedCompatible
                && evaluation.OptionalOptimization is not null
                && evaluation.PlanningSession is { } optionalSession
                && HasOptimizationAuthority(optionalSession.Route);
            OptimizationPreferenceSelection? preference =
                requiredOptimization || optionalOptimization
                    ? OptimizationPreferenceSelection.Automatic()
                    : null;
            CompatibilityPresentation presentation = optionalOptimization
                ? CompatibilityPresentationFactory.OptionalOptimization(
                    evaluation,
                    evaluation.OptionalOptimization!,
                    preference!)
                : preference is null
                    ? CompatibilityPresentationFactory.From(evaluation)
                    : CompatibilityPresentationFactory.From(evaluation, preference);
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
            if (optionalOptimization)
            {
                presentation = presentation with
                {
                    PrimaryActionEnabled = optimizationHandoff is not null
                };
            }
            else if (model.State == CompatibilityScreenState.EstimatedCompatible)
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
                    _recommendedPreference = preference;
                    CurrentOptimizationHandoff = optimizationHandoff;
                    _currentModelLaunchHandoff = currentHandoff;
                    _optionalOptimizationOpen = optionalOptimization;
                    _experimentalFinalConfirmation = false;
#if DEBUG
                    WriteAcceptedEvaluationTrace(
                        evaluation,
                        presentation,
                        _continueDestinationAvailable,
                        currentHandoff is not null,
                        optimizationHandoff is not null);
#endif
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

            // every attempt owns exactly its own source. a newer attempt may
            // cancel it, but disposal waits until this evaluator has returned
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
        // overwrite what a reviewer is looking at
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
        CancelOptimizationStartIntent(publishRetainedPresentation: false);
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
            // the operation already published a path-private safe failure
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
        _recommendedPreference = null;
        _exactPreference = null;
        CurrentOptimizationHandoff = null;
        _currentModelLaunchHandoff = null;
        _optionalOptimizationOpen = false;
        _experimentalFinalConfirmation = false;
    }

#if DEBUG
    private static void WriteAcceptedEvaluationTrace(
        CompatibilityEvaluation evaluation,
        CompatibilityPresentation presentation,
        bool continueDestinationAvailable,
        bool hasCurrentHandoff,
        bool hasOptimizationHandoff)
    {
        try
        {
            static string Defined<T>(T value) where T : struct, Enum =>
                Enum.IsDefined(value) ? value.ToString() : "Unknown";

            System.Diagnostics.Debug.WriteLine(
                $"Granite.Compatibility accepted state={Defined(evaluation.Screen.State)} " +
                $"baselineExclusion={Defined(evaluation.Screen.BaselineExclusionReason)} " +
                $"hasFindings={evaluation.Screen.Findings.Count > 0} " +
                $"hasBlockingFinding={evaluation.Screen.Findings.Any(f => f.Severity == FindingSeverity.Blocking)} " +
                $"hasPlanningSession={evaluation.PlanningSession is not null} " +
                $"hasCurrentConfiguration={evaluation.CurrentConfiguration is not null} " +
                $"hasOptionalOptimization={evaluation.OptionalOptimization is not null} " +
                $"hasCurrentHandoff={hasCurrentHandoff} " +
                $"hasOptimizationHandoff={hasOptimizationHandoff} " +
                $"hasRecoveries={presentation.Recoveries.Count > 0} " +
                $"projectedPrimaryEnabled={presentation.PrimaryActionEnabled} " +
                $"projectedSecondaryEnabled={presentation.SecondaryActionEnabled} " +
                $"continueDestinationAvailable={continueDestinationAvailable}");

            foreach (CompatibilityFindingCode code in Enum.GetValues<CompatibilityFindingCode>())
            {
                foreach (FindingSeverity severity in Enum.GetValues<FindingSeverity>())
                {
                    if (evaluation.Screen.Findings.Any(
                            finding => finding.Code == code && finding.Severity == severity))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Granite.Compatibility finding code={Defined(code)} severity={Defined(severity)}");
                    }
                }
            }
        }
        catch
        {
            // Diagnostics are observational and must never affect evaluation.
        }
    }
#endif

    private void ClearExperimentalConsent()
    {
        lock (_attemptGate)
        {
            _experimentalConsentEvidenceIds.Clear();
            _experimentalFinalConfirmation = false;
        }
    }

    private string? ResolveSelectedExperimentalEvidenceId()
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
        CancelOptimizationStartIntent();
        if (_lastModel is not { } model
            || _lastEvaluation is not { } evaluation
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen)
        {
            return;
        }

        InvalidateExperimentalConsentReevaluation();
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
                _recommendedPreference = preference;
                CurrentOptimizationHandoff = handoff;
            });
    }

    internal void SelectManualPreference(int value)
    {
        CancelOptimizationStartIntent();
        if (_lastModel is not { } model
            || _lastEvaluation is not { } evaluation
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen)
        {
            return;
        }

        InvalidateExperimentalConsentReevaluation();
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
            // preserve the exact user value for the future handoff, but a tick
            // inside the same effective band changes no rendered state
            SelectedPreference = preference;
            _recommendedPreference = preference;
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
                _recommendedPreference = preference;
                CurrentOptimizationHandoff = handoff;
            });
    }

    internal void SelectRecommendedPreference()
    {
        if (_recommendedPreference is not { } preference
            || preference.Kind == OptimizationPreferenceKind.Exact)
        {
            return;
        }

        SelectRetainedPreference(preference);
    }

    internal void SelectRetainedExactPreference()
    {
        if (Presentation.Optimization is not
            { IsActionAuthoritative: true, HasAdditionalExactSafeModes: true } optimization)
        {
            return;
        }

        string? retainedIdentity = _exactPreference?.ExactCandidateIdentity;
        CompatibilityExactOptimizationModePresentation? selected;
        if (retainedIdentity is not null)
        {
            CompatibilityExactOptimizationModePresentation[] matches =
            [.. optimization.ExactSafeModes.Where(mode => string.Equals(
                mode.CandidateIdentity,
                retainedIdentity,
                StringComparison.Ordinal))];
            if (matches.Length != 1)
            {
                FailClosedSelection();
                return;
            }
            selected = matches[0];
        }
        else
        {
            selected = optimization.ExactSafeModes.FirstOrDefault();
        }
        if (selected is not null)
        {
            SelectExactPreference(selected.CandidateIdentity);
        }
    }

    internal void SelectExactPreference(string candidateIdentity)
    {
        CancelOptimizationStartIntent();
        if (_lastModel is not { } model
            || _lastEvaluation is not { } evaluation
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen
            || Presentation.Optimization is not
                { IsActionAuthoritative: true } optimization)
        {
            return;
        }
        if (optimization.ExactSafeModes.Count(mode => string.Equals(
                mode.CandidateIdentity,
                candidateIdentity,
                StringComparison.Ordinal)) != 1)
        {
            FailClosedSelection();
            return;
        }

        OptimizationPreferenceSelection preference;
        try
        {
            preference = OptimizationPreferenceSelection.Exact(candidateIdentity);
        }
        catch (ArgumentException)
        {
            FailClosedSelection();
            return;
        }

        InvalidateExperimentalConsentReevaluation();
        _experimentalFinalConfirmation = false;
        int generation = Volatile.Read(ref _attemptGeneration);
        CompatibilityPresentation next = SelectionPresentation(
            evaluation,
            preference);
        if (next.Optimization is not
            { IsActionAuthoritative: true } nextOptimization
            || nextOptimization.Preference != preference)
        {
            FailClosedSelection();
            return;
        }
        OptimizationSelectionHandoff? handoff = TryIssueOptimization(
            evaluation,
            preference);
        Publish(
            next with { PrimaryActionEnabled = handoff is not null },
            generation,
            () =>
            {
                SelectedPreference = preference;
                _exactPreference = preference;
                CurrentOptimizationHandoff = handoff;
            });
    }

    private void FailClosedSelection()
    {
        InvalidateExperimentalConsentReevaluation();
        _experimentalFinalConfirmation = false;
        CurrentOptimizationHandoff = null;
        Presentation = Presentation with
        {
            PrimaryActionEnabled = false,
            Optimization = Presentation.Optimization is { } optimization
                ? optimization with
                {
                    SelectionStatusText =
                        "That exact setup is no longer available. Choose another setup to continue."
                }
                : null
        };
        ContinueCommand.RaiseCanExecuteChanged();
        StartOptimizationCommand.RaiseCanExecuteChanged();
        PresentationChanged?.Invoke(this, Presentation);
    }

    private void SelectRetainedPreference(
        OptimizationPreferenceSelection preference)
    {
        CancelOptimizationStartIntent();
        if (_lastModel is not { } model
            || _lastEvaluation is not { } evaluation
            || model.State != CompatibilityScreenState.OptimisationRequired
                && !_optionalOptimizationOpen)
        {
            return;
        }

        InvalidateExperimentalConsentReevaluation();
        _experimentalFinalConfirmation = false;
        int generation = Volatile.Read(ref _attemptGeneration);
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

    private void InvalidateExperimentalConsentReevaluation()
    {
        AttemptCancellation? cancellation;
        lock (_attemptGate)
        {
            cancellation = _experimentalConsentCancellation;
            if (cancellation is null)
            {
                return;
            }

            unchecked
            {
                _attemptGeneration++;
            }
            _experimentalConsentCancellation = null;
            if (ReferenceEquals(_attemptCancellation, cancellation))
            {
                _attemptCancellation = null;
            }
            if (ResolveSelectedExperimentalEvidenceId() is { } evidenceId)
            {
                _experimentalConsentEvidenceIds.Remove(evidenceId);
            }
            _experimentalFinalConfirmation = false;
        }

        cancellation.Cancel();
    }

    internal void CancelOptimizationStartIntent() =>
        CancelOptimizationStartIntent(publishRetainedPresentation: true);

    private void CancelOptimizationStartIntent(bool publishRetainedPresentation)
    {
        AttemptCancellation? cancellation;
        int generation;
        lock (_attemptGate)
        {
            cancellation = _optimizationStartCancellation;
            if (cancellation is null)
            {
                return;
            }

            generation = unchecked(++_attemptGeneration);
            _optimizationStartCancellation = null;
            _optimizationStartBusy = false;
            CurrentOptimizationHandoff = null;
        }

        cancellation.Cancel();
        if (!publishRetainedPresentation)
        {
            StartOptimizationCommand.RaiseCanExecuteChanged();
            return;
        }

        Presentation = Presentation with
        {
            PrimaryActionEnabled = false,
            Optimization = Presentation.Optimization is { } optimization
                ? optimization with { SelectionStatusText = string.Empty }
                : null
        };
        ContinueCommand.RaiseCanExecuteChanged();
        StartOptimizationCommand.RaiseCanExecuteChanged();
        PresentationChanged?.Invoke(this, Presentation);
    }

    internal static IReadOnlyList<CompatibilityOptimizationModePresentation>
        DistinctModes(CompatibilityOptimizationPresentation? optimization)
    {
        if (optimization is not { IsActionAuthoritative: true })
        {
            return [];
        }

        var identities = new HashSet<OptimizationModeIdentity>();
        var choices = new List<CompatibilityOptimizationModePresentation>();
        foreach (CompatibilityOptimizationModePresentation mode in
                 optimization.Modes)
        {
            if (mode.SliderValue is null || !identities.Add(IdentityOf(mode)))
            {
                continue;
            }

            choices.Add(mode);
        }

        return choices;
    }

    internal static int IndexOfSelectedMode(
        CompatibilityOptimizationPresentation optimization,
        IReadOnlyList<CompatibilityOptimizationModePresentation> choices)
    {
        OptimizationModeIdentity selected = IdentityOf(
            optimization.SelectedMode);
        for (int index = 0; index < choices.Count; index++)
        {
            if (IdentityOf(choices[index]) == selected)
            {
                return index;
            }
        }

        return 0;
    }

    private static OptimizationModeIdentity IdentityOf(
        CompatibilityOptimizationModePresentation mode) =>
        new(
            mode.ExpectedQualityText,
            mode.WeightFormat,
            mode.CacheFormat,
            mode.ContextText,
            mode.SystemSharedRequirementText,
            mode.SystemSharedBudgetText,
            mode.SystemSharedHeadroomText,
            mode.DedicatedRequirementText,
            mode.DedicatedBudgetText,
            mode.DedicatedHeadroomText,
            mode.IsExperimental,
            mode.HasStrongQualityWarning,
            mode.WarningText,
            mode.RequiresPersistentArtifact,
            mode.RequiresRequantisationAcknowledgement);

    private readonly record struct OptimizationModeIdentity(
        string ExpectedQualityText,
        string WeightFormat,
        string CacheFormat,
        string ContextText,
        string SystemSharedRequirementText,
        string SystemSharedBudgetText,
        string SystemSharedHeadroomText,
        string DedicatedRequirementText,
        string DedicatedBudgetText,
        string DedicatedHeadroomText,
        bool IsExperimental,
        bool HasStrongQualityWarning,
        string WarningText,
        bool RequiresPersistentArtifact,
        bool RequiresRequantisationAcknowledgement);

    private CompatibilityPresentation SelectionPresentation(
        CompatibilityEvaluation evaluation,
        OptimizationPreferenceSelection preference) =>
        _optionalOptimizationOpen
            && evaluation.OptionalOptimization is { } optional
                ? CompatibilityPresentationFactory.OptionalOptimization(
                    evaluation,
                    optional,
                    preference)
                : CompatibilityPresentationFactory.From(evaluation, preference);

    private void Publish(
        CompatibilityPresentation presentation,
        int generation,
        Action? commitState = null)
    {
        // a result from a superseded attempt describes a world that has already
        // moved on, so it is dropped rather than shown
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
            StartOptimizationCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            RetryCommand.RaiseCanExecuteChanged();
            OptionalOptimizationCommand.RaiseCanExecuteChanged();
            ChatCurrentModelCommand.RaiseCanExecuteChanged();
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
                    // content is never surfaced and cannot strand cancellation
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
