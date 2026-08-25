using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;

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

    private CancellationTokenSource? _attemptCancellation;
    private int _attemptGeneration;
    private CompatibilityScreenModel? _lastModel;

    internal CompatibilityViewModel()
        : this(static token => Task.Run(
            () => CompatibilityEngine.RunWithAvailableAdapters(token),
            token))
    {
    }

    internal CompatibilityViewModel(
        Func<CancellationToken, Task<CompatibilityScreenModel>> evaluator,
        bool continueDestinationAvailable = true)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _continueDestinationAvailable = continueDestinationAvailable;
        _uiContext = SynchronizationContext.Current;

        // Assigned before the commands, because their guards read it: a command
        // must never be asked whether it can run before there is a state to ask
        // about.
        Presentation = CompatibilityPresentationFactory.Analysing(0);

        ContinueCommand = new DelegateCommand(
            () => ContinueRequested?.Invoke(this, EventArgs.Empty),
            () => Presentation.PrimaryActionEnabled);

        BackCommand = new DelegateCommand(
            () => BackRequested?.Invoke(this, EventArgs.Empty),
            () => Presentation.SecondaryActionEnabled);

        CancelCommand = new DelegateCommand(Cancel);
    }

    /// <summary>Raised whenever a new snapshot is ready to render.</summary>
    internal event EventHandler<CompatibilityPresentation>? PresentationChanged;

    internal event EventHandler? ContinueRequested;

    internal event EventHandler? BackRequested;

    internal CompatibilityPresentation Presentation { get; private set; }

    internal DelegateCommand ContinueCommand { get; }

    internal DelegateCommand BackCommand { get; }

    internal DelegateCommand CancelCommand { get; }

    internal OptimizationPreferenceSelection? SelectedPreference { get; private set; }

    /// <summary>
    /// Starts one attempt. Safe to call again: the previous attempt is cancelled
    /// and its result, if it arrives late, is discarded.
    /// </summary>
    internal async Task StartAsync()
    {
        int generation = Interlocked.Increment(ref _attemptGeneration);

        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous =
            Interlocked.Exchange(ref _attemptCancellation, cancellation);

        previous?.Cancel();
        previous?.Dispose();

        Publish(
            CompatibilityPresentationFactory.Analysing(0),
            generation,
            () =>
            {
                _lastModel = null;
                SelectedPreference = null;
            });

        try
        {
            // The engine is synchronous and pure. It runs off the UI thread so a
            // slow adapter cannot freeze the page once adapters exist.
            CompatibilityScreenModel model = await _evaluator(cancellation.Token)
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
                    SelectedPreference = preference;
                });
        }
        catch (OperationCanceledException)
        {
            Publish(
                CompatibilityPresentationFactory.Cancelled(),
                generation,
                () =>
                {
                    _lastModel = null;
                    SelectedPreference = null;
                });
        }
    }

    /// <summary>
    /// Renders a snapshot directly, without running anything. Debug fixtures use
    /// this to show a state the engine cannot reach yet.
    /// </summary>
    internal void ShowFixture(CompatibilityPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        // Takes a generation so an in-flight real attempt cannot overwrite what a
        // reviewer is looking at.
        Publish(
            presentation,
            Interlocked.Increment(ref _attemptGeneration),
            () =>
            {
                _lastModel = null;
                SelectedPreference = null;
            });
    }

    internal void Cancel()
    {
        _attemptCancellation?.Cancel();
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
        Publish(
            CompatibilityPresentationFactory.From(model, preference),
            generation,
            () => SelectedPreference = preference);
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
        Publish(
            CompatibilityPresentationFactory.From(model, preference),
            generation,
            () => SelectedPreference = preference);
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
            Presentation = _continueDestinationAvailable
                ? presentation
                : presentation with { PrimaryActionEnabled = false };
            ContinueCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
            PresentationChanged?.Invoke(this, presentation);
        }

        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            Apply();
            return;
        }

        _uiContext.Post(_ => Apply(), null);
    }
}
