#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DebugFixturePreset = GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets.ModelInspectionFixturePreset;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

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

    public ModelInspectionFixtureGalleryPage()
        : this(
            new ModelInspectionFixturePackageLoader(),
            static () => true,
            new ModelInspectionFixtureScenarioRunner())
    {
    }

    internal ModelInspectionFixtureGalleryPage(
        ModelInspectionFixturePackageLoader loader,
        Action closeRequested,
        IModelInspectionFixtureScenarioRunner? runner = null,
        Action? beforeHostNavigationConfirmation = null)
        : this(
            loader,
            WrapCloseRequested(closeRequested),
            runner,
            beforeHostNavigationConfirmation)
    {
    }

    private ModelInspectionFixtureGalleryPage(
        ModelInspectionFixturePackageLoader loader,
        Func<bool> closeRequested,
        IModelInspectionFixtureScenarioRunner? runner,
        Action? beforeHostNavigationConfirmation = null)
    {
        this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
        this.closeRequested = closeRequested ??
            throw new ArgumentNullException(nameof(closeRequested));
        this.runner = runner ?? new ModelInspectionFixtureScenarioRunner();
        this.beforeHostNavigationConfirmation =
            beforeHostNavigationConfirmation;
        ViewModel = new ModelInspectionFixtureGalleryViewModel();

        InitializeComponent();
        PopulateStaticControls();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        FixtureSearchBox.TextChanged += FixtureSearchBox_TextChanged;
        FixtureCategoryFilter.SelectionChanged +=
            FixtureCategoryFilter_SelectionChanged;
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

    internal async Task SelectFixtureForTestingAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ModelInspectionFixtureListItem item = ViewModel.Items.Single(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal));
        Task selection = SelectAsync(item, () => item.RawUtf8);
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
        Task selection = SelectAsync(item, () => rawUtf8Factory());
        Volatile.Write(ref selectionCompleted, selection);
        await selection;
    }

    internal Task ResetForTestingAsync()
    {
        ModelInspectionFixtureListItem item = ViewModel.SelectedItem ??
            throw new InvalidOperationException(
                "No fixture is selected for reset.");
        Task selection = SelectAsync(item, () => item.RawUtf8);
        Volatile.Write(ref selectionCompleted, selection);
        return selection;
    }

    internal void CloseForTesting() => CloseGallery();

    internal bool RaiseUnloadedForTesting() => RetireGallery();

    private async Task SelectAsync(
        ModelInspectionFixtureListItem item,
        Func<ReadOnlyMemory<byte>> rawUtf8Factory)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rawUtf8Factory);
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

            DebugFixturePreset preset = DebugFixturePreset.Canonical;
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

            string checkpoint;
            try
            {
                checkpoint = await runner.RunAsync(
                    fixture.Input,
                    candidate.ModelInspectionPage!,
                    session,
                    cancellation.Token);
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

            ViewModel.ValidationStatus = $"Reached checkpoint: {checkpoint}.";
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

    private async void FixtureList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs eventArguments)
    {
        if (FixtureList.SelectedItem is ModelInspectionFixtureListItem item)
        {
            Task selection = SelectAsync(item, () => item.RawUtf8);
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
        DebugFixturePreset preset = DebugFixturePreset.Canonical;
        FixturePresetControls.Children.Add(new TextBlock
        {
            Text = $"Width: {preset.Width}"
        });
        FixturePresetControls.Children.Add(new TextBlock
        {
            Text = $"Resources: {preset.Resources}"
        });
        FixturePresetControls.Children.Add(new TextBlock
        {
            Text = $"Text: {preset.Text}"
        });
        FixturePresetControls.Children.Add(new TextBlock
        {
            Text = $"Motion: {preset.Motion}"
        });
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
        FixtureInteractionPanel.Children.Clear();
        foreach (ModelInspectionFixtureInteraction interaction in
                 item.VisibleInteractions)
        {
            var button = new Button
            {
                Content = InteractionLabel(interaction.Kind),
                Tag = interaction.Id
            };
            button.CommandParameter = interaction.Kind;
            button.Click += FixtureInteractionButton_Click;
            FixtureInteractionPanel.Children.Add(button);
        }
    }

    private void ClearFixtureHeader()
    {
        FixtureIdText.Text = string.Empty;
        FixtureFileNameText.Text = string.Empty;
        FixtureTargetText.Text = string.Empty;
        FixtureCategoryText.Text = string.Empty;
        FixtureRealWorkerCoverageText.Text = string.Empty;
        FixtureRealWorkerCoverageText.Visibility = Visibility.Collapsed;
        FixtureInteractionPanel.Children.Clear();
    }

    private async void FixtureInteractionButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (sender is not Button button ||
            button.CommandParameter is not
                ModelInspectionFixtureInteractionKind kind ||
            activeHost?.ModelInspectionPage is not ModelInspectionPage page)
        {
            return;
        }

        switch (kind)
        {
            case ModelInspectionFixtureInteractionKind.Expand:
                RequireActiveDisclosure(page).RequestTargetState(true);
                break;
            case ModelInspectionFixtureInteractionKind.Collapse:
                RequireActiveDisclosure(page).RequestTargetState(false);
                break;
            case ModelInspectionFixtureInteractionKind.Cancel:
                ExecuteIfAvailable(page.ViewModel?.CancelCommand);
                break;
            case ModelInspectionFixtureInteractionKind.Retry:
            case ModelInspectionFixtureInteractionKind.Restart:
                ExecuteIfAvailable(page.ViewModel?.RetryCommand);
                break;
            case ModelInspectionFixtureInteractionKind.ChooseAnother:
                ExecuteIfAvailable(page.ViewModel?.ChooseAnotherCommand);
                break;
            case ModelInspectionFixtureInteractionKind.Reset:
                await ResetForTestingAsync();
                break;
            default:
                throw new InvalidOperationException(
                    "The fixture interaction kind has no UI route.");
        }
    }

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

    private static void ExecuteIfAvailable(
        System.Windows.Input.ICommand? command)
    {
        if (command is null || !command.CanExecute(null))
        {
            throw new InvalidOperationException(
                "The fixture interaction command is unavailable.");
        }

        command.Execute(null);
    }

    private static string InteractionLabel(
        ModelInspectionFixtureInteractionKind kind) => kind switch
        {
            ModelInspectionFixtureInteractionKind.ChooseAnother =>
                "Choose another",
            _ => kind.ToString()
        };

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
