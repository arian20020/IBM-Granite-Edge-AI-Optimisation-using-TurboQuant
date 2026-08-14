#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DebugFixturePreset = GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets.ModelInspectionFixturePreset;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal sealed record ModelInspectionFixtureActionDispatchResult(
    string ActionId,
    bool IsDispatched,
    string RuleCode);

internal sealed class ModelInspectionFixtureGalleryActivation
{
    internal ModelInspectionFixtureGalleryActivation(
        Func<bool> closeRequested) =>
        CloseRequested = closeRequested ??
            throw new ArgumentNullException(nameof(closeRequested));

    internal Func<bool> CloseRequested { get; }
}

public sealed partial class ModelInspectionFixtureGalleryPage : Page
{
    private readonly ModelInspectionFixturePackageLoader loader;
    private readonly IModelInspectionFixtureScenarioRunner runner;
    private readonly IModelInspectionFixtureScreenObserver screenObserver;
    private readonly IModelInspectionFixtureScreenComparer screenComparer;
    private Action? beforeHostNavigationConfirmation;
    private readonly TaskCompletionSource<bool> catalogueLoaded = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<bool>? closeRequested;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private CancellationTokenSource? selectionCancellation;
    private ModelInspectionFixtureHostPage? activeHost;
    private ModelInspectionFixtureHostActivation? pendingActivation;
    private Task selectionCompleted = Task.CompletedTask;
    private object? activationIdentity;
    private long selectionEpoch;
    private int catalogueLoadStarted;
    private int activationStarted;
    private int closeStarted;
    private int retired;
    private bool updatingPresetControls;
    private Func<ReadOnlyMemory<byte>>? nextListSelectionRawFactory;
    private string? declaredActionCheckpointForTesting;

    public ModelInspectionFixtureGalleryPage()
        : this(
            new ModelInspectionFixturePackageLoader(),
            static () => true,
            new ModelInspectionFixtureScenarioRunner(),
            beforeHostNavigationConfirmation: null,
            screenObserver: null,
            screenComparer: null)
    {
    }

    internal ModelInspectionFixtureGalleryPage(
        ModelInspectionFixturePackageLoader loader,
        Action closeRequested,
        IModelInspectionFixtureScenarioRunner? runner = null,
        Action? beforeHostNavigationConfirmation = null,
        IModelInspectionFixtureScreenObserver? screenObserver = null,
        IModelInspectionFixtureScreenComparer? screenComparer = null)
        : this(
            loader,
            WrapCloseRequested(closeRequested),
            runner,
            beforeHostNavigationConfirmation,
            screenObserver,
            screenComparer)
    {
    }

    private ModelInspectionFixtureGalleryPage(
        ModelInspectionFixturePackageLoader loader,
        Func<bool> closeRequested,
        IModelInspectionFixtureScenarioRunner? runner,
        Action? beforeHostNavigationConfirmation,
        IModelInspectionFixtureScreenObserver? screenObserver,
        IModelInspectionFixtureScreenComparer? screenComparer)
    {
        this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        this.closeRequested = closeRequested ??
            throw new ArgumentNullException(nameof(closeRequested));
        this.runner = runner ?? new ModelInspectionFixtureScenarioRunner();
        this.screenObserver = screenObserver ??
            new ModelInspectionFixtureScreenObserver();
        this.screenComparer = screenComparer ??
            new ModelInspectionFixtureScreenComparer();
        this.beforeHostNavigationConfirmation =
            beforeHostNavigationConfirmation;
        ViewModel = new ModelInspectionFixtureGalleryViewModel();

        InitializeComponent();
        PopulateStaticControls();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        FixtureSearchBox.TextChanged += FixtureSearchBox_TextChanged;
        FixtureCategoryFilter.SelectionChanged +=
            FixtureCategoryFilter_SelectionChanged;
        FixturePresetSelector.SelectionChanged +=
            FixturePresetSelector_SelectionChanged;
        FixtureList.SelectionChanged += FixtureList_SelectionChanged;
        ResetFixtureButton.Click += ResetFixtureButton_Click;
        CloseFixtureGalleryButton.Click += CloseFixtureGalleryButton_Click;
        Loaded += ModelInspectionFixtureGalleryPage_Loaded;
        Unloaded += ModelInspectionFixtureGalleryPage_Unloaded;
        RefreshView();
    }

    internal ModelInspectionFixtureGalleryViewModel ViewModel { get; }

    internal Task CatalogueLoaded => catalogueLoaded.Task;

    internal Frame HostFrame => FixtureHostFrame;

    internal ModelInspectionFixtureHostPage? ActiveHost => activeHost;

    internal ValidatedModelInspectionFixtureCoverageCatalogue
        CoverageCatalogueForTesting => loader.CoverageCatalogue ??
            throw new InvalidOperationException(
                "The fixture coverage catalogue is not loaded.");

    internal DebugFixturePreset CurrentPreset =>
        Volatile.Read(ref activeHost)?.Preset ?? DebugFixturePreset.Canonical;

    internal ModelInspectionFixtureHostActivation? PendingActivationForTesting =>
        Volatile.Read(ref pendingActivation);

    internal Task SelectionCompletedForTesting =>
        Volatile.Read(ref selectionCompleted);

    internal object? ActivationIdentityForTesting => activationIdentity;

    internal bool IsActivatedWith(object activation) =>
        Volatile.Read(ref activationStarted) == 1 &&
        Volatile.Read(ref retired) == 0 &&
        ReferenceEquals(activationIdentity, activation);

    internal static ModelInspectionFixtureGalleryActivation CreateActivation(
        Func<bool> closeRequested) => new(closeRequested);

    internal static object CreateActivationForTesting() =>
        new ModelInspectionFixtureGalleryActivation(static () => true);

    protected override void OnNavigatedTo(NavigationEventArgs eventArguments)
    {
        base.OnNavigatedTo(eventArguments);
        if (eventArguments.Parameter is not ModelInspectionFixtureGalleryActivation
            activation)
        {
            throw new ArgumentException(
                "The fixture gallery requires its exact activation.",
                nameof(eventArguments));
        }

        if (Volatile.Read(ref retired) != 0 ||
            Interlocked.CompareExchange(ref activationStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "The fixture gallery activation is one-shot.");
        }

        activationIdentity = activation;
        closeRequested = activation.CloseRequested;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs eventArguments)
    {
        base.OnNavigatedFrom(eventArguments);
        RetireGallery();
    }

    internal Task SelectFixtureForTestingAsync(string id) =>
        SelectFixtureForTestingAsync(id, DebugFixturePreset.Canonical);

    internal async Task SelectFixtureForTestingAsync(
        string id,
        DebugFixturePreset preset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(preset);
        ModelInspectionFixtureListItem item = ViewModel.Items.Single(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal));
        DebugFixturePreset validatedPreset = ValidateRequestedPreset(
            item,
            preset);
        Task selection = SelectAsync(
            item,
            () => item.RawUtf8,
            validatedPreset);
        Volatile.Write(ref selectionCompleted, selection);
        await selection;
    }

    internal async Task SelectRawFixtureForTestingAsync(
        string fileName,
        Func<ReadOnlyMemory<byte>> rawUtf8Factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(rawUtf8Factory);
        ModelInspectionFixtureListItem item = ViewModel.Items.Single(candidate =>
            string.Equals(candidate.FileName, fileName, StringComparison.Ordinal));
        DebugFixturePreset preset = ValidateRequestedPreset(
            item,
            DebugFixturePreset.Canonical);
        Task selection = SelectAsync(
            item,
            () => rawUtf8Factory(),
            preset);
        Volatile.Write(ref selectionCompleted, selection);
        await selection;
    }

    internal Task ResetForTestingAsync()
    {
        ModelInspectionFixtureListItem item = ViewModel.SelectedItem ??
            throw new InvalidOperationException(
                "No fixture is selected for reset.");
        DebugFixturePreset preset = ValidateRequestedPreset(
            item,
            CurrentPreset);
        Task selection = SelectAsync(item, () => item.RawUtf8, preset);
        Volatile.Write(ref selectionCompleted, selection);
        return selection;
    }

    internal void CloseForTesting() => CloseGallery();

    internal async Task<ModelInspectionFixtureActionDispatchResult>
        DispatchDeclaredActionForTestingAsync(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ModelInspectionPage? page = activeHost?.ModelInspectionPage;
        ModelInspectionFixtureListItem? item = ViewModel.SelectedItem;
        if (page is null || item is null ||
            !IsSupportedActionRequest(item, actionId))
        {
            return new(actionId, false, "fixture.action.unsupported");
        }

        Button? control = FindRenderedActionButtonOrNull(actionId);
        if (control is null)
        {
            return new(actionId, false, "fixture.action.unsupported");
        }

        if (!control.IsEnabled)
        {
            return new(actionId, false, "fixture.action.disabled");
        }

        string? checkpoint = Volatile.Read(
            ref declaredActionCheckpointForTesting);
        ModelInspectionFixtureInteraction? declaredInteraction =
            item.Fixture.Interactions.SingleOrDefault(interaction =>
                string.Equals(
                    interaction.Id,
                    actionId,
                    StringComparison.Ordinal) &&
                (checkpoint is null || string.Equals(
                    interaction.SourceCheckpoint,
                    checkpoint,
                    StringComparison.Ordinal)));
        ModelInspectionFixtureSession session = activeHost!.Session;
        int serviceCallCountBefore = session.Evidence.ServiceCallCount;
        InvokeRenderedButton(control);
        await DrainDispatcherAsync(this);
        if (string.Equals(actionId, "reset", StringComparison.Ordinal))
        {
            await SelectionCompletedForTesting;
        }
        else if ((actionId is "retry" or "retry-attempt" or
                  "restart" or "restart-attempt") &&
                 declaredInteraction is not null &&
                 declaredInteraction.Target.StartsWith(
                     "MI-",
                     StringComparison.Ordinal))
        {
            ModelInspectionFixtureServiceCallEvidence nextCall = await session
                .Evidence
                .WaitForNextServiceCallAsync(serviceCallCountBefore)
                .WaitAsync(TimeSpan.FromSeconds(5));
            await CompleteAttemptAsync(session, nextCall);
        }

        await DrainDispatcherAsync(this);
        return new(actionId, true, "fixture.action.dispatched");
    }

    internal async Task<Button> FindRenderedActionButtonForTestingAsync(
        string actionId)
    {
        await DrainDispatcherAsync(this);
        return FindRenderedActionButtonOrNull(actionId) ??
            throw new InvalidOperationException(
                $"The rendered fixture action '{actionId}' was not found for " +
                $"fixture '{ViewModel.SelectedItem?.Id ?? "<none>"}'.");
    }

    internal async Task<Button?> FindRenderedActionButtonOrNullForTestingAsync(
        string actionId)
    {
        await DrainDispatcherAsync(this);
        return FindRenderedActionButtonOrNull(actionId);
    }

    internal Task PositionAtDeclaredInteractionCheckpointThroughScenarioAsync(
        string fixtureId,
        string checkpoint) => SelectFixtureAtCheckpointAsync(
            fixtureId,
            checkpoint,
            continueUntilNextInteraction: false,
            captureStaleResultSnapshots: true);

    internal Task SelectFixtureForStaleEventCheckpointThroughLoadedRunnerAsync(
        string fixtureId,
        string checkpoint) => SelectFixtureAtCheckpointAsync(
            fixtureId,
            checkpoint,
            continueUntilNextInteraction: true,
            captureStaleResultSnapshots: !string.Equals(
                fixtureId, "MI-034", StringComparison.Ordinal));

    internal async Task InvokeRetryThroughRenderedControlAsync()
    {
        ModelInspectionFixtureSession session = activeHost?.Session ??
            throw new InvalidOperationException("No fixture session is active.");
        Button retry = FindRenderedPageActionButton("retry") ??
            FindRenderedPageActionButton("restart") ??
            throw new InvalidOperationException(
                "The loaded fixture has no rendered Retry control.");
        int serviceCallCountBefore = session.Evidence.ServiceCallCount;
        InvokeRenderedButton(retry);
        await DrainDispatcherAsync(this);
        ModelInspectionFixtureServiceCallEvidence nextCall = await session
            .Evidence
            .WaitForNextServiceCallAsync(serviceCallCountBefore)
            .WaitAsync(TimeSpan.FromSeconds(5));
        await CompleteAttemptAsync(session, nextCall);
        await DrainDispatcherAsync(this);
    }

    internal async Task SelectFixtureThroughRealListForTestingAsync(string id)
    {
        ModelInspectionFixtureListItem item = ViewModel.Items.Single(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal));
        FixtureList.SelectedItem = null;
        await DrainDispatcherAsync(this);
        FixtureList.SelectedItem = item;
        await SelectionCompletedForTesting;
    }

    internal async Task SelectInvalidRawFixtureThroughRealListForTestingAsync()
    {
        ModelInspectionFixtureListItem item = ViewModel.SelectedItem ??
            throw new InvalidOperationException("No fixture is selected.");
        nextListSelectionRawFactory = static () => new byte[] { (byte)'{' };
        FixtureList.SelectedItem = null;
        await DrainDispatcherAsync(this);
        FixtureList.SelectedItem = item;
        await SelectionCompletedForTesting;
    }

    internal async Task NavigateThroughOwningFrameAwayAndBackForTestingAsync(
        Window window)
    {
        string fixtureId = ViewModel.SelectedItem?.Id ??
            throw new InvalidOperationException("No fixture is selected.");
        FixtureHostFrame.Content = new Page();
        RetireActiveHost();
        await DrainDispatcherAsync(this);
        await SelectFixtureThroughRealListForTestingAsync(fixtureId);
    }

    internal async Task NavigateThroughOwningFrameAndUnloadForTestingAsync(
        Window window)
    {
        FixtureHostFrame.Content = new Page();
        RetireActiveHost();
        await DrainDispatcherAsync(this);
    }

    internal async Task PrimeEveryAuditedProducerThroughRealControlsAsync(
        string fixtureId)
    {
        await SelectFixtureThroughRealListForTestingAsync(fixtureId);
        Button? collapse = FindRenderedActionButtonOrNull("collapse");
        if (collapse is not null && collapse.IsEnabled)
        {
            InvokeRenderedButton(collapse);
            await DrainDispatcherAsync(this);
        }

        Button? expand = FindRenderedActionButtonOrNull("expand");
        if (expand is not null && expand.IsEnabled)
        {
            InvokeRenderedButton(expand);
            await DrainDispatcherAsync(this);
        }
    }

    internal bool RaiseUnloadedForTesting() => RetireGallery();

    private async Task SelectFixtureAtCheckpointAsync(
        string fixtureId,
        string checkpoint,
        bool continueUntilNextInteraction,
        bool captureStaleResultSnapshots)
    {
        ModelInspectionFixtureListItem item = ViewModel.Items.Single(candidate =>
            string.Equals(candidate.Id, fixtureId, StringComparison.Ordinal));
        Task selection = SelectAsync(
            item,
            () => item.RawUtf8,
            DebugFixturePreset.Canonical,
            checkpoint,
            continueUntilNextInteraction,
            captureStaleResultSnapshots);
        Volatile.Write(ref selectionCompleted, selection);
        await selection;
        if (activeHost is not null &&
            string.Equals(
                ViewModel.SelectedItem?.Id,
                fixtureId,
                StringComparison.Ordinal))
        {
            Volatile.Write(
                ref declaredActionCheckpointForTesting,
                checkpoint);
        }
    }

    private DebugFixturePreset ValidateRequestedPreset(
        ModelInspectionFixtureListItem item,
        DebugFixturePreset requested)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(requested);
        ModelInspectionFixtureCatalogue catalogue = loader.CoverageCatalogue?
            .Catalogue ?? throw new InvalidOperationException(
                "The validated fixture preset policy is unavailable.");
        if (!item.Fixture.Presets.Contains(
                requested.Id,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Fixture '{item.Id}' does not require preset " +
                $"'{requested.Id}'.");
        }

        GraniteEdgeAI.ModelInspection.Fixtures.ModelInspectionFixturePreset?
            policyPreset = catalogue.Policy.Value.Presets.SingleOrDefault(
                candidate => string.Equals(
                    candidate.Id,
                    requested.Id,
                    StringComparison.Ordinal));
        if (policyPreset is null ||
            policyPreset.Width != requested.Width ||
            policyPreset.Resources != requested.Resources ||
            policyPreset.Text != requested.Text ||
            policyPreset.Motion != requested.Motion)
        {
            throw new InvalidOperationException(
                $"Preset '{requested.Id}' does not match the validated " +
                "coverage-policy identity.");
        }

        return requested;
    }

    private async Task SelectAsync(
        ModelInspectionFixtureListItem item,
        Func<ReadOnlyMemory<byte>> rawUtf8Factory,
        DebugFixturePreset preset,
        string? stopCheckpoint = null,
        bool continueUntilNextInteraction = false,
        bool captureStaleResultSnapshots = true)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rawUtf8Factory);
        ArgumentNullException.ThrowIfNull(preset);
        Volatile.Write(ref declaredActionCheckpointForTesting, null);
        long epoch = Interlocked.Increment(ref selectionEpoch);
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref selectionCancellation,
            cancellation);
        previous?.Cancel();
        previous?.Dispose();

        ModelInspectionFixtureHostPage? candidate = null;
        ModelInspectionFixtureHostActivation? activation = null;
        try
        {
            RetireActiveHost();
            ViewModel.ClearSelection();
            ClearFixtureHeader();

            ReadOnlyMemory<byte> rawUtf8 = rawUtf8Factory();
            ValidatedModelInspectionFixture fixture = loader.RevalidateDescriptor(
                item.FileName,
                rawUtf8);
            if (!IsCurrent(epoch, cancellation))
            {
                return;
            }

            bool animationsEnabled =
                preset.Motion ==
                ModelInspectionFixtureMotionProfile.Normal;
            var session = new ModelInspectionFixtureSession(
                fixture.Input,
                animationsEnabled);
            activation = new ModelInspectionFixtureHostActivation(
                fixture.Input,
                session,
                startInspectionOnLoaded:
                    ShouldStartInspectionOnLoaded(fixture),
                preset: preset,
                chooseAnotherRequested: HandleChooseAnotherRequested);
            if (Interlocked.CompareExchange(
                    ref pendingActivation,
                    activation,
                    null) is not null)
            {
                throw new InvalidOperationException(
                    "Another fixture host activation is still pending.");
            }

            object? previousContent = FixtureHostFrame.Content;
            bool navigated;
            try
            {
                navigated = FixtureHostFrame.Navigate(
                    typeof(ModelInspectionFixtureHostPage),
                    activation);
                Volatile.Read(ref beforeHostNavigationConfirmation)?.Invoke();
            }
            catch (Exception error)
            {
                if (!IsCurrent(epoch, cancellation))
                {
                    RetireOwnedNavigation(activation);
                    return;
                }

                candidate = OwnedCurrentHost(activation);
                AttemptNavigationRollback(candidate, activation);
                ExceptionDispatchInfo.Capture(error).Throw();
                throw;
            }

            if (!IsCurrent(epoch, cancellation))
            {
                RetireOwnedNavigation(activation);
                return;
            }

            candidate = OwnedCurrentHost(activation);
            if (!navigated ||
                ReferenceEquals(previousContent, FixtureHostFrame.Content) ||
                candidate is null ||
                !candidate.IsActivatedWith(activation))
            {
                var error = new InvalidOperationException(
                    "The fixture host activation was not confirmed.");
                AttemptNavigationRollback(candidate, activation);
                throw error;
            }

            if (!IsCurrent(epoch, cancellation))
            {
                RetireNavigationCandidate(candidate);
                return;
            }

            activeHost = candidate;
            Interlocked.CompareExchange(
                ref pendingActivation,
                null,
                activation);
            if (!IsCurrent(epoch, cancellation) ||
                !ReferenceEquals(activeHost, candidate))
            {
                RetireOwnedNavigation(activation);
                return;
            }

            ClearHostJournals();
            activation.Dispose();
            activation = null;

            await candidate.ApplyPresetAsync(cancellation.Token);
            if (!IsCurrent(epoch, cancellation) ||
                !ReferenceEquals(activeHost, candidate))
            {
                return;
            }

            string checkpoint;
            ModelInspectionObservedScreen observed;
            using IModelInspectionFixtureObservationSession observation =
                screenObserver.Begin(candidate.ModelInspectionPage!);
            try
            {
                if (stopCheckpoint is null)
                {
                    checkpoint = await runner.RunAsync(
                        fixture.Input,
                        candidate.ModelInspectionPage!,
                        session,
                        cancellation.Token);
                }
                else if (runner is ModelInspectionFixtureScenarioRunner
                         checkpointRunner)
                {
                    checkpoint = await checkpointRunner.RunToCheckpointAsync(
                        fixture.Input,
                        candidate.ModelInspectionPage!,
                        session,
                        stopCheckpoint,
                        continueUntilNextInteraction,
                        captureStaleResultSnapshots,
                        cancellation.Token);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Checkpoint positioning requires the loaded fixture scenario runner.");
                }
                if (!IsCurrent(epoch, cancellation) ||
                    !ReferenceEquals(activeHost, candidate))
                {
                    return;
                }

                observed = await observation.CaptureAsync(cancellation.Token);
            }
            catch (OperationCanceledException) when (!IsCurrent(
                epoch,
                cancellation))
            {
                return;
            }

            if (!IsCurrent(epoch, cancellation) ||
                !ReferenceEquals(activeHost, candidate))
            {
                return;
            }

            ViewModel.SelectedItem = item;
            if (!IsCurrent(epoch, cancellation) ||
                !ReferenceEquals(activeHost, candidate))
            {
                return;
            }

            RefreshFixtureHeader(item);
            if (!IsCurrent(epoch, cancellation) ||
                !ReferenceEquals(activeHost, candidate))
            {
                return;
            }

            IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
                stopCheckpoint is null
                    ? screenComparer.Compare(
                        fixture,
                        observed,
                        loader.CoverageCatalogue?.Catalogue.Policy.Value.CopyRegistry ??
                            throw new InvalidOperationException(
                                "The validated fixture copy registry is unavailable."))
                    : [];
            if (!IsCurrent(epoch, cancellation) ||
                !ReferenceEquals(activeHost, candidate))
            {
                return;
            }

            ViewModel.ValidationStatus = differences.Count == 0
                ? $"Screen contract passed: {checkpoint}."
                : $"Screen contract failed: {differences[0].Diagnostic}";
        }
        catch (ModelInspectionFixtureGalleryLoadException error)
        {
            if (IsCurrent(epoch, cancellation))
            {
                RetireCandidateIfCurrent(candidate);
                ClearVisibleSelection();
                ViewModel.ValidationStatus =
                    $"Fixture unavailable: {error.Diagnostic}";
                ClearFixtureHeader();
            }
        }
        catch (OperationCanceledException) when (!IsCurrent(
            epoch,
            cancellation))
        {
        }
        catch (Exception)
        {
            if (IsCurrent(epoch, cancellation))
            {
                RetireCandidateIfCurrent(candidate);
                ClearVisibleSelection();
                ViewModel.ValidationStatus =
                    "Fixture unavailable: scenario.execution";
                ClearFixtureHeader();
            }
        }
        finally
        {
            activation?.Dispose();
            if (ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref selectionCancellation,
                        null,
                        cancellation),
                    cancellation))
            {
                cancellation.Dispose();
            }
        }
    }

    private bool IsCurrent(
        long epoch,
        CancellationTokenSource cancellation) =>
        Volatile.Read(ref selectionEpoch) == epoch &&
        !cancellation.IsCancellationRequested &&
        Volatile.Read(ref retired) == 0;

    private void RetireCandidateIfCurrent(
        ModelInspectionFixtureHostPage? candidate)
    {
        if (candidate is null || !ReferenceEquals(activeHost, candidate))
        {
            return;
        }

        RetireActiveHost();
    }

    private void RetireNavigationCandidate(
        ModelInspectionFixtureHostPage? candidate)
    {
        Exception? first = null;
        if (candidate is not null)
        {
            try
            {
                candidate.RetireForTesting();
            }
            catch (Exception error)
            {
                first = error;
            }
        }

        bool ownsCurrentContent = candidate is not null &&
            ReferenceEquals(FixtureHostFrame.Content, candidate);
        try
        {
            if (ownsCurrentContent &&
                ReferenceEquals(FixtureHostFrame.Content, candidate))
            {
                FixtureHostFrame.Content = null;
                ClearHostJournals();
            }
        }
        catch (Exception error)
        {
            first ??= error;
        }

        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    private ModelInspectionFixtureHostPage? OwnedCurrentHost(
        ModelInspectionFixtureHostActivation activation) =>
        FixtureHostFrame.Content is ModelInspectionFixtureHostPage host &&
        activation.OwnsDestination(host)
            ? host
            : null;

    private void RetireOwnedNavigation(
        ModelInspectionFixtureHostActivation activation)
    {
        Interlocked.CompareExchange(
            ref pendingActivation,
            null,
            activation);
        ModelInspectionFixtureHostPage? owned = OwnedCurrentHost(activation);
        Exception? first = null;
        if (owned is not null)
        {
            Attempt(() => owned.RetireForTesting(), ref first);
            if (ReferenceEquals(FixtureHostFrame.Content, owned))
            {
                Attempt(() => FixtureHostFrame.Content = null, ref first);
                Attempt(ClearHostJournals, ref first);
            }
        }

        Attempt(activation.Dispose, ref first);
        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    private void AttemptNavigationRollback(
        ModelInspectionFixtureHostPage? candidate,
        ModelInspectionFixtureHostActivation activation)
    {
        _ = Interlocked.CompareExchange(
            ref pendingActivation,
            null,
            activation);
        try
        {
            RetireNavigationCandidate(candidate);
        }
        catch
        {
        }

        if (candidate is null && FixtureHostFrame.Content is null)
        {
            try
            {
                ClearHostJournals();
            }
            catch
            {
            }
        }

        try
        {
            activation.Dispose();
        }
        catch
        {
        }
    }

    private void HandleChooseAnotherRequested(
        ModelInspectionFixtureHostPage owner)
    {
        if (!ReferenceEquals(Volatile.Read(ref activeHost), owner) ||
            Volatile.Read(ref retired) != 0)
        {
            return;
        }

        Interlocked.Increment(ref selectionEpoch);
        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref selectionCancellation,
            null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        RetireActiveHost();
        ClearVisibleSelection();
        ClearFixtureHeader();
        ViewModel.ValidationStatus = "gallery:no-active-fixture";
    }

    private void ClearVisibleSelection()
    {
        FixtureList.SelectedItem = null;
        ViewModel.ClearSelection();
    }

    private void RetireActiveHost()
    {
        ModelInspectionFixtureHostPage? retiredHost = Interlocked.Exchange(
            ref activeHost,
            null);
        ModelInspectionFixtureHostActivation? pending = Interlocked.Exchange(
            ref pendingActivation,
            null);
        ModelInspectionFixtureHostPage? pendingHost = pending is not null
            ? OwnedCurrentHost(pending)
            : null;
        Exception? first = null;
        if (retiredHost is not null)
        {
            try
            {
                retiredHost.RetireForTesting();
            }
            catch (Exception error)
            {
                first = error;
            }
        }

        if (pendingHost is not null &&
            !ReferenceEquals(pendingHost, retiredHost))
        {
            Attempt(() => pendingHost.RetireForTesting(), ref first);
        }

        if (pending is not null)
        {
            Attempt(pending.Dispose, ref first);
        }

        try
        {
            object? current = FixtureHostFrame.Content;
            if (current is null)
            {
                ClearHostJournals();
            }
            else if (ReferenceEquals(current, retiredHost) ||
                     ReferenceEquals(current, pendingHost))
            {
                FixtureHostFrame.Content = null;
                ClearHostJournals();
            }
        }
        catch (Exception error)
        {
            first ??= error;
        }

        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    private void ClearHostJournals()
    {
        if (FixtureHostFrame.BackStack.Count != 0)
        {
            FixtureHostFrame.BackStack.Clear();
        }

        if (FixtureHostFrame.ForwardStack.Count != 0)
        {
            FixtureHostFrame.ForwardStack.Clear();
        }
    }

    private bool RetireGallery() => RetireGallery(claimClose: false, out _);

    private bool RetireGallery(
        bool claimClose,
        out Func<bool>? claimedClose)
    {
        if (Interlocked.Exchange(ref retired, 1) != 0)
        {
            claimedClose = null;
            return false;
        }

        Func<bool>? callback = Interlocked.Exchange(ref closeRequested, null);
        Interlocked.Exchange(ref beforeHostNavigationConfirmation, null);
        claimedClose = claimClose ? callback : null;
        Exception? first = null;
        Interlocked.Increment(ref selectionEpoch);
        Attempt(lifetimeCancellation.Cancel, ref first);
        activationIdentity = null;
        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref selectionCancellation,
            null);
        if (cancellation is not null)
        {
            Attempt(cancellation.Cancel, ref first);
            Attempt(cancellation.Dispose, ref first);
        }
        Attempt(RetireActiveHost, ref first);
        catalogueLoaded.TrySetCanceled(lifetimeCancellation.Token);
        Attempt(
            () => ViewModel.PropertyChanged -= ViewModel_PropertyChanged,
            ref first);
        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }

        return true;
    }

    private void CloseGallery()
    {
        if (Interlocked.Exchange(ref closeStarted, 1) != 0)
        {
            return;
        }

        Exception? first = null;
        Func<bool>? callback = null;
        try
        {
            _ = RetireGallery(claimClose: true, out callback);
        }
        catch (Exception error)
        {
            first = error;
        }

        if (callback is null)
        {
            return;
        }

        bool freshImportConfirmed = false;
        try
        {
            freshImportConfirmed = callback();
        }
        catch (Exception error)
        {
            first ??= error;
        }

        if (!freshImportConfirmed && first is null)
        {
            ViewModel.ValidationStatus =
                "Fixture gallery closed: Model Import navigation did not complete.";
            RefreshView();
        }

        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    private static void Attempt(Action? action, ref Exception? first)
    {
        if (action is null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception error)
        {
            first ??= error;
        }
    }

    private async void ModelInspectionFixtureGalleryPage_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (Interlocked.CompareExchange(ref catalogueLoadStarted, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await ViewModel.LoadAsync(loader, lifetimeCancellation.Token);
            lifetimeCancellation.Token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref retired) != 0)
            {
                catalogueLoaded.TrySetCanceled(lifetimeCancellation.Token);
                return;
            }

            RefreshView();
            catalogueLoaded.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
            catalogueLoaded.TrySetCanceled(lifetimeCancellation.Token);
        }
        catch (Exception error)
        {
            RefreshView();
            catalogueLoaded.TrySetException(error);
        }
    }

    private void ModelInspectionFixtureGalleryPage_Unloaded(
        object sender,
        RoutedEventArgs eventArguments) => RetireGallery();

    private void FixtureSearchBox_TextChanged(
        object sender,
        TextChangedEventArgs eventArguments) =>
        ViewModel.SearchText = FixtureSearchBox.Text;

    private void FixtureCategoryFilter_SelectionChanged(
        object sender,
        SelectionChangedEventArgs eventArguments)
    {
        ViewModel.SelectedCategory = FixtureCategoryFilter.SelectedItem is
            ModelInspectionFixtureCategory category
                ? category
                : null;
    }

    private async void FixturePresetSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs eventArguments)
    {
        if (updatingPresetControls ||
            FixturePresetSelector.SelectedItem is not string presetId ||
            ViewModel.SelectedItem is not ModelInspectionFixtureListItem item)
        {
            return;
        }

        long requestEpoch = Volatile.Read(ref selectionEpoch);
        try
        {
            ModelInspectionFixtureCatalogue catalogue = loader.CoverageCatalogue?
                .Catalogue ?? throw new InvalidOperationException(
                    "The validated fixture preset policy is unavailable.");
            GraniteEdgeAI.ModelInspection.Fixtures.ModelInspectionFixturePreset
                policyPreset = catalogue.Policy.Value.Presets.Single(candidate =>
                    string.Equals(
                        candidate.Id,
                        presetId,
                        StringComparison.Ordinal));
            DebugFixturePreset requested = ValidateRequestedPreset(
                item,
                DebugFixturePreset.FromPolicy(policyPreset));
            if (requested == CurrentPreset)
            {
                return;
            }

            Task selection = SelectAsync(
                item,
                () => item.RawUtf8,
                requested);
            Volatile.Write(ref selectionCompleted, selection);
            await selection;
        }
        catch (Exception)
        {
            if (Volatile.Read(ref retired) != 0 ||
                Volatile.Read(ref selectionEpoch) != requestEpoch ||
                !ReferenceEquals(ViewModel.SelectedItem, item) ||
                FixturePresetSelector.SelectedItem is not string selectedPresetId ||
                !string.Equals(
                    selectedPresetId,
                    presetId,
                    StringComparison.Ordinal))
            {
                return;
            }

            RefreshPresetControls(
                item,
                CurrentPreset);
            ViewModel.ValidationStatus =
                "Fixture preset unavailable: preset.selection";
        }
    }

    private async void FixtureList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs eventArguments)
    {
        if (FixtureList.SelectedItem is ModelInspectionFixtureListItem item)
        {
            DebugFixturePreset preset = ValidateRequestedPreset(
                item,
                DebugFixturePreset.Canonical);
            Func<ReadOnlyMemory<byte>>? injected = Interlocked.Exchange(
                ref nextListSelectionRawFactory,
                null);
            Task selection = SelectAsync(
                item,
                injected ?? (() => item.RawUtf8),
                preset);
            Volatile.Write(ref selectionCompleted, selection);
            await selection;
        }
    }

    private async void ResetFixtureButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (ViewModel.SelectedItem is not null)
        {
            await ResetForTestingAsync();
        }
    }

    internal static bool ShouldStartInspectionOnLoaded(
        ValidatedModelInspectionFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return !string.Equals(fixture.Id, "MI-001", StringComparison.Ordinal);
    }

    private void CloseFixtureGalleryButton_Click(
        object sender,
        RoutedEventArgs eventArguments) => CloseGallery();

    private void ViewModel_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArguments)
    {
        if (eventArguments.PropertyName is
            nameof(ModelInspectionFixtureGalleryViewModel.FilteredItems) or
            nameof(ModelInspectionFixtureGalleryViewModel.ValidationStatus))
        {
            RefreshView();
        }
    }

    private void PopulateStaticControls()
    {
        FixtureCategoryFilter.Items.Add("All categories");
        foreach (ModelInspectionFixtureCategory category in
                 Enum.GetValues<ModelInspectionFixtureCategory>())
        {
            FixtureCategoryFilter.Items.Add(category);
        }

        FixtureCategoryFilter.SelectedIndex = 0;
        RefreshPresetControls(item: null, DebugFixturePreset.Canonical);
    }

    private void RefreshView()
    {
        FixtureList.ItemsSource = ViewModel.FilteredItems;
        FixtureValidationStatus.Text = ViewModel.ValidationStatus;
    }

    private void RefreshFixtureHeader(ModelInspectionFixtureListItem item)
    {
        FixtureIdText.Text = item.Id;
        FixtureFileNameText.Text = item.FileName;
        FixtureTargetText.Text = item.TargetCondition;
        FixtureCategoryText.Text = item.Category.ToString();
        FixtureRealWorkerCoverageText.Text = item.RealWorkerCoverageLabel;
        FixtureRealWorkerCoverageText.Visibility =
            item.HasN001RealWorkerCoverage
                ? Visibility.Visible
                : Visibility.Collapsed;
        RefreshPresetControls(item, CurrentPreset);
        FixtureInteractionPanel.Children.Clear();
    }

    private void ClearFixtureHeader()
    {
        FixtureIdText.Text = string.Empty;
        FixtureFileNameText.Text = string.Empty;
        FixtureTargetText.Text = string.Empty;
        FixtureCategoryText.Text = string.Empty;
        FixtureRealWorkerCoverageText.Text = string.Empty;
        FixtureRealWorkerCoverageText.Visibility = Visibility.Collapsed;
        RefreshPresetControls(item: null, DebugFixturePreset.Canonical);
        FixtureInteractionPanel.Children.Clear();
    }

    private void RefreshPresetControls(
        ModelInspectionFixtureListItem? item,
        DebugFixturePreset preset)
    {
        updatingPresetControls = true;
        try
        {
            FixturePresetSelector.Items.Clear();
            if (item is not null)
            {
                foreach (string id in item.Fixture.Presets)
                {
                    FixturePresetSelector.Items.Add(id);
                }

                FixturePresetSelector.SelectedItem = preset.Id;
            }

            FixturePresetSelector.IsEnabled = item?.Fixture.Presets.Count > 1;
            FixturePresetWidthText.Text = $"Width: {WidthLabel(preset.Width)}";
            FixturePresetResourcesText.Text =
                $"Resources: {ModelInspectionFixturePreviewResources.ResourceLabel(preset.Resources)}";
            FixturePresetTextScaleText.Text =
                $"Text: {ModelInspectionFixturePreviewResources.TextLabel(preset.Text)}";
            FixturePresetMotionText.Text = $"Motion: {preset.Motion}";
        }
        finally
        {
            updatingPresetControls = false;
        }
    }

    private static string WidthLabel(
        ModelInspectionFixtureWidthProfile width) => width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => "Desktop 1440",
            ModelInspectionFixtureWidthProfile.Medium600 => "Medium 600",
            ModelInspectionFixtureWidthProfile.Narrow360 => "Narrow 360",
            _ => throw new ArgumentOutOfRangeException(
                nameof(width), width, "Unknown fixture width profile.")
        };

    private static InspectionDisclosure RequireActiveDisclosure(
        ModelInspectionPage page)
    {
        var model = (InspectionModelCard)page.FindName(
            "InspectionModelCardControl");
        var content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        InspectionDisclosure[] active =
        [
            .. new[] { model.ActiveDisclosure, content.ActiveDisclosure }
                .OfType<InspectionDisclosure>()
        ];
        return active.Length == 1
            ? active[0]
            : throw new InvalidOperationException(
            "The fixture interaction has no unique disclosure target.");
    }

    private Button? FindRenderedActionButtonOrNull(string actionId)
    {
        ModelInspectionFixtureListItem? item = ViewModel.SelectedItem;
        ModelInspectionPage? page = activeHost?.ModelInspectionPage;
        if (item is null || page is null ||
            !IsSupportedActionRequest(item, actionId))
        {
            return null;
        }

        return actionId switch
        {
            "reset" => ResetFixtureButton,
            "expand" => FindDisclosureToggle(page, expanded: false),
            "collapse" => FindDisclosureToggle(page, expanded: true),
            "cancel-request" => FindRenderedPageActionButton("cancel"),
            "retry-attempt" => FindRenderedPageActionButton("retry"),
            "restart-attempt" => FindRenderedPageActionButton("restart"),
            _ => FindRenderedPageActionButton(actionId)
        };
    }

    private Button? FindRenderedPageActionButton(string actionId)
    {
        ModelInspectionPage? page = activeHost?.ModelInspectionPage;
        if (page is null)
        {
            return null;
        }

        var card = (InspectionActionCard)page.FindName(
            "InspectionActionCardControl");
        foreach (string name in new[]
                 {
                     "CancelActionButton", "SecondaryActionOneButton",
                     "SecondaryActionTwoButton", "PrimaryActionButton"
                 })
        {
            var button = (Button)card.FindName(name);
            if (button.Visibility == Visibility.Visible && string.Equals(
                    button.Tag as string,
                    actionId,
                    StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private static Button? FindDisclosureToggle(
        ModelInspectionPage page,
        bool expanded)
    {
        InspectionDisclosure disclosure;
        try
        {
            disclosure = RequireActiveDisclosure(page);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return disclosure.IsExpanded == expanded
            ? (Button)disclosure.FindName("DisclosureToggleButton")
            : null;
    }

    private bool IsSupportedActionRequest(
        ModelInspectionFixtureListItem item,
        string actionId)
    {
        if (!IsCanonicalInteractionActionId(actionId))
        {
            return true;
        }

        string? checkpoint = Volatile.Read(
            ref declaredActionCheckpointForTesting);
        return item.Fixture.Interactions.Any(interaction =>
            string.Equals(interaction.Id, actionId, StringComparison.Ordinal) &&
            (checkpoint is null || string.Equals(
                interaction.SourceCheckpoint,
                checkpoint,
                StringComparison.Ordinal)));
    }

    private static bool IsCanonicalInteractionActionId(string actionId) =>
        actionId is "cancel" or "cancel-request" or "choose-another" or
            "collapse" or "expand" or "locate-missing" or "reset" or
            "restart" or "restart-attempt" or "retry" or "retry-attempt";

    private static async Task CompleteAttemptAsync(
        ModelInspectionFixtureSession session,
        ModelInspectionFixtureServiceCallEvidence serviceCall)
    {
        ModelInspectionFixtureAttemptPlan attempt = session.Plan.Attempts
            .Single(candidate => candidate.Attempt == serviceCall.Attempt);
        foreach (ModelInspectionFixtureServiceStepPlan step in
                 attempt.ServiceSteps)
        {
            if (step.TriggerKind !=
                    ModelInspectionFixtureServiceTriggerKind.Checkpoint ||
                session.Evidence.ReleasedServiceCheckpoints.Any(released =>
                    released.Attempt == serviceCall.Attempt && string.Equals(
                        released.Checkpoint,
                        step.Checkpoint,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            session.Service.ReleaseServiceCheckpoint(
                serviceCall.Attempt,
                step.Checkpoint ?? throw new InvalidOperationException(
                    "The fixture checkpoint-triggered service step has no name."));
            await DrainDispatcherAsync(session);
        }
    }

    private static async Task DrainDispatcherAsync(object owner)
    {
        Microsoft.UI.Dispatching.DispatcherQueue queue = owner switch
        {
            FrameworkElement element => element.DispatcherQueue,
            ModelInspectionFixtureSession =>
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(),
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        } ?? throw new InvalidOperationException(
            "The fixture UI dispatcher is unavailable.");
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.TryEnqueue(() => drained.TrySetResult(true)))
        {
            throw new InvalidOperationException(
                "The fixture UI dispatcher rejected its drain callback.");
        }

        await drained.Task;
    }

    private static void InvokeRenderedButton(Button button)
    {
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(
            button);
        if (peer.GetPattern(
                Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke) is not
            Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider invoke)
        {
            throw new InvalidOperationException(
                "The declared fixture action has no invokable rendered control.");
        }

        invoke.Invoke();
    }

    private static Func<bool> WrapCloseRequested(Action closeRequested)
    {
        ArgumentNullException.ThrowIfNull(closeRequested);
        return () =>
        {
            closeRequested();
            return true;
        };
    }
}
#endif
