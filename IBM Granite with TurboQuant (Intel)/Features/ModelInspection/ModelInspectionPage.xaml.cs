using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection;

/// <summary>
/// Displays and owns the UI lifecycle of one GGUF Model Inspection request.
/// </summary>
public sealed partial class ModelInspectionPage : Page
{
    private readonly IModelInspectionService _service;
    private readonly Func<IModelInspectionRenderDispatcher> _dispatcherFactory;
    private readonly Func<IModelInspectionAnimationDriver> _animationDriverFactory;
    private readonly Func<IModelInspectionMotionSettings> _motionSettingsFactory;
    private readonly HashSet<(long AttemptGeneration, ModelInspectionFigmaState Outcome)>
        _announcedTerminalOutcomes = [];

    private ModelInspectionViewModel? _startedViewModel;
    private IModelInspectionRenderDispatcher? _renderDispatcher;
    private IModelInspectionAnimationDriver? _animationDriver;
    private IModelInspectionMotionSettings? _motionSettings;
    private MotionSettingsChangeRegistration? _motionSettingsRegistration;
    private ModelInspectionRenderCoordinator? _coordinator;
    private long _navigationLifetime;
    private bool _isApplyingMotionSettingsChange;

    /// <summary>
    /// Creates the production page with the approved x64 Model Inspection
    /// service composition.
    /// </summary>
    public ModelInspectionPage()
        : this(ModelInspectionServiceComposition.CreateDefault())
    {
    }

    /// <summary>
    /// Creates a page with an explicit application service.
    /// </summary>
    internal ModelInspectionPage(IModelInspectionService service)
        : this(
            service,
            CreateProductionDispatcher,
            () => new WinUiModelInspectionAnimationDriver(
                ModelInspectionMotionSpec.Approved),
            () => new UiSettingsModelInspectionMotionSettings())
    {
    }

    internal ModelInspectionPage(
        IModelInspectionService service,
        Func<IModelInspectionRenderDispatcher> dispatcherFactory,
        Func<IModelInspectionAnimationDriver> animationDriverFactory,
        Func<IModelInspectionMotionSettings> motionSettingsFactory)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dispatcherFactory = dispatcherFactory ??
            throw new ArgumentNullException(nameof(dispatcherFactory));
        _animationDriverFactory = animationDriverFactory ??
            throw new ArgumentNullException(nameof(animationDriverFactory));
        _motionSettingsFactory = motionSettingsFactory ??
            throw new ArgumentNullException(nameof(motionSettingsFactory));

        InitializeComponent();
        InspectionModelCardControl.IsDisclosureStateExternallyOwned = true;
        InspectionContentCardControl.IsDisclosureStateExternallyOwned = true;
        InspectionModelCardControl.DisclosureToggleRequested +=
            DisclosureToggleRequested;
        InspectionContentCardControl.DisclosureToggleRequested +=
            DisclosureToggleRequested;
        Loaded += ModelInspectionPage_Loaded;
    }

    internal event EventHandler? ChooseAnotherModelRequested;

    internal event EventHandler<InspectionFooterStatusChangedEventArgs>?
        FooterStatusChanged;

    internal ModelInspectionRequest? Request { get; private set; }

    internal ModelInspectionViewModel? ViewModel { get; private set; }

    internal Task? CurrentInspectionTask { get; private set; }

    internal ModelInspectionPagePresentation? CurrentPresentation
        { get; private set; }

    internal InspectionFooterStatus CurrentFooterStatus { get; private set; } =
        InspectionFooterStatus.InProgress;

    internal Action? MotionSettingsChangeValidatedForTesting { get; set; }

    internal Action<long>? MotionSettingsChangeRevisionClaimedForTesting
        { get; set; }

    internal bool MotionSettingsChangePendingForTesting =>
        IsMotionSettingsChangePending;

    protected override void OnNavigatedTo(NavigationEventArgs eventArguments)
    {
        base.OnNavigatedTo(eventArguments);

        if (eventArguments.Parameter is not ModelInspectionRequest request)
        {
            throw new ArgumentException(
                "ModelInspectionPage requires a validated ModelInspectionRequest.",
                nameof(eventArguments));
        }

        RetireNavigationLifetime();

        IModelInspectionRenderDispatcher dispatcher =
            _dispatcherFactory() ?? throw new InvalidOperationException(
                "The Model Inspection dispatcher factory returned null.");
        IModelInspectionAnimationDriver animationDriver =
            _animationDriverFactory() ?? throw new InvalidOperationException(
                "The Model Inspection animation factory returned null.");
        IModelInspectionMotionSettings motionSettings =
            _motionSettingsFactory() ?? throw new InvalidOperationException(
                "The Model Inspection motion-settings factory returned null.");
        var viewModel = new ModelInspectionViewModel(_service, request);
        var commands = new ModelInspectionPresentationCommands(
            viewModel.CancelCommand,
            viewModel.RetryCommand,
            viewModel.ChooseAnotherCommand);

        _navigationLifetime = checked(_navigationLifetime + 1);
        _announcedTerminalOutcomes.Clear();
        Request = request;
        ViewModel = viewModel;
        CurrentPresentation = null;
        _renderDispatcher = dispatcher;
        _animationDriver = animationDriver;
        _motionSettings = motionSettings;
        var motionSettingsRegistration = new MotionSettingsChangeRegistration(
            _navigationLifetime,
            motionSettings,
            dispatcher,
            MotionSettings_AnimationsEnabledChanged);
        Volatile.Write(
            ref _motionSettingsRegistration,
            motionSettingsRegistration);
        _coordinator = new ModelInspectionRenderCoordinator(
            dispatcher,
            (snapshot, isExpanded, progressRows) =>
                ModelInspectionPresentationFactory.Create(
                    request,
                    snapshot,
                    commands,
                    isExpanded,
                    progressRows),
            ApplyDelta);

        Subscribe(viewModel);
        motionSettings.AnimationsEnabledChanged +=
            motionSettingsRegistration.ChangedHandler;
        _coordinator.ApplyInitial(viewModel.Snapshot);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs eventArguments)
    {
        RetireNavigationLifetime();
        base.OnNavigatedFrom(eventArguments);
    }

    internal Task? StartInspectionIfReadyAsync()
    {
        ModelInspectionViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return null;
        }

        if (ReferenceEquals(_startedViewModel, viewModel))
        {
            return CurrentInspectionTask;
        }

        _startedViewModel = viewModel;
        CurrentInspectionTask = viewModel.StartAsync();
        return CurrentInspectionTask;
    }

    private static IModelInspectionRenderDispatcher CreateProductionDispatcher()
    {
        DispatcherQueue dispatcherQueue =
            DispatcherQueue.GetForCurrentThread() ??
            throw new InvalidOperationException(
                "Model Inspection requires a UI DispatcherQueue.");
        return new DispatcherQueueModelInspectionRenderDispatcher(
            dispatcherQueue);
    }

    private void ModelInspectionPage_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        _ = StartInspectionIfReadyAsync();
    }

    private void Subscribe(ModelInspectionViewModel viewModel)
    {
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ChooseAnotherRequested += ViewModel_ChooseAnotherRequested;
    }

    private void Unsubscribe(ModelInspectionViewModel viewModel)
    {
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        viewModel.ChooseAnotherRequested -= ViewModel_ChooseAnotherRequested;
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArguments)
    {
        if (eventArguments.PropertyName != nameof(ModelInspectionViewModel.Snapshot) ||
            sender is not ModelInspectionViewModel viewModel ||
            !ReferenceEquals(viewModel, ViewModel))
        {
            return;
        }

        _coordinator?.RequestRender(viewModel.Snapshot);
    }

    private void ViewModel_ChooseAnotherRequested(
        object? sender,
        EventArgs eventArguments)
    {
        if (sender is ModelInspectionViewModel viewModel &&
            ReferenceEquals(viewModel, ViewModel))
        {
            ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DisclosureToggleRequested(
        object? sender,
        InspectionDisclosureToggleRequestedEventArgs eventArguments)
    {
        ModelInspectionRenderCoordinator? coordinator = _coordinator;
        ModelInspectionPagePresentation? presentation = CurrentPresentation;
        if (coordinator is null || presentation is null)
        {
            return;
        }

        bool fromModel = ReferenceEquals(sender, InspectionModelCardControl);
        bool fromContent = ReferenceEquals(sender, InspectionContentCardControl);
        if ((!fromModel && !fromContent) ||
            !coordinator.TryRequestDisclosure(
                presentation.RenderKey,
                eventArguments.IsExpanded,
                out _))
        {
            return;
        }

        if (fromModel)
        {
            InspectionModelCardControl.ClaimDisclosureTarget(
                eventArguments.IsExpanded);
        }
        else
        {
            InspectionContentCardControl.ClaimDisclosureTarget(
                eventArguments.IsExpanded);
        }
    }

    private void ApplyDelta(ModelInspectionPresentationDelta delta)
    {
        ModelInspectionRenderCoordinator? coordinator = _coordinator;
        IModelInspectionAnimationDriver? animationDriver = _animationDriver;
        IModelInspectionMotionSettings? motionSettings = _motionSettings;
        if (coordinator is null ||
            animationDriver is null ||
            motionSettings is null ||
            !coordinator.IsCurrent(delta.RenderKey))
        {
            return;
        }

        ModelInspectionPagePresentation? previous = CurrentPresentation;
        ModelInspectionPagePresentation current = delta.Presentation;
        bool retiresProgress = previous?.State ==
                ModelInspectionFigmaState.InspectionProgress &&
            current.State != ModelInspectionFigmaState.InspectionProgress;
        bool generationChanged = previous is not null &&
            previous.RenderKey.AttemptGeneration !=
                current.RenderKey.AttemptGeneration;
        bool outcomeChanged = previous is not null &&
            previous.RegionKeys.Outcome != current.RegionKeys.Outcome;
        DependencyObject? focusedElement = XamlRoot is null
            ? null
            : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        bool focusedRetiredProgress = retiresProgress &&
            focusedElement is not null &&
            IsDescendantOrSelf(focusedElement, InspectionContentCardControl);

        if (generationChanged || outcomeChanged)
        {
            animationDriver.CancelAll();
            InspectionContentCardControl.CancelProgressMotion();
            RetireOutgoingProgressLayer();
        }

        CurrentPresentation = current;

        // Semantic retirement is first. The outgoing layer is inert before
        // terminal model/action/footer/outcome state becomes authoritative.
        if (retiresProgress)
        {
            InspectionContentCardControl.CancelProgressMotion();
            OutgoingProgressContentCard.Presentation =
                InspectionContentCardControl.Presentation;
            AutomationProperties.SetAccessibilityView(
                OutgoingProgressContentCard,
                AccessibilityView.Raw);
            AutomationProperties.SetLiveSetting(
                OutgoingProgressContentCard,
                AutomationLiveSetting.Off);
            OutgoingProgressContentCard.IsHitTestVisible = false;
            OutgoingProgressContentCard.Opacity = 1d;
            OutgoingProgressContentCard.Visibility = Visibility.Visible;
            OutgoingProgressContentCard.UpdateLayout();
            SuppressAccessibilityTree(OutgoingProgressContentCard);
        }
        else if (current.State == ModelInspectionFigmaState.InspectionProgress &&
                 previous?.State != ModelInspectionFigmaState.InspectionProgress)
        {
            RetireOutgoingProgressLayer();
        }

        InspectionProgressRowsApplyResult progressChanges =
            InspectionProgressRowsApplyResult.Empty;
        if (delta.ChangedRegions.HasFlag(
                ModelInspectionPresentationRegions.ProgressRows))
        {
            progressChanges = current.ContentCard.ProgressRows.Apply(
                delta.ProgressRowsUpdate!);
        }

        DisclosureTransition transition = CaptureDisclosureTransition(
            previous,
            current,
            delta.ChangedRegions);

        if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Model))
        {
            InspectionModelCardControl.Presentation = current.ModelCard;
        }

        if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Content))
        {
            InspectionContentCardControl.Presentation = current.ContentCard;
        }

        if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Actions))
        {
            InspectionActionCardControl.Presentation = current.ActionCard;
        }

        if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Footer))
        {
            ApplyFooterStatus(current.FooterStatus);
        }

        if (delta.ChangedRegions.HasFlag(ModelInspectionPresentationRegions.Outcome))
        {
            InspectionOutcomeCardControl.Presentation = current.OutcomeCard;
        }

        ApplyPageReflow(current);
        PrepareDisclosureTransition(transition);
        UpdateLayout();

        if (!progressChanges.IsEmpty &&
            motionSettings.AnimationsEnabled &&
            !_isApplyingMotionSettingsChange &&
            !IsMotionSettingsChangePending)
        {
            InspectionContentCardControl.AnimateProgressChanges(
                progressChanges,
                animationDriver,
                delta.VisualOperationKey,
                coordinator.IsCurrent);
        }

        StartOrCompleteDisclosure(
            transition,
            delta.VisualOperationKey,
            coordinator,
            animationDriver,
            motionSettings.AnimationsEnabled &&
                !_isApplyingMotionSettingsChange &&
                !IsMotionSettingsChangePending);

        if (retiresProgress)
        {
            if (motionSettings.AnimationsEnabled &&
                !_isApplyingMotionSettingsChange &&
                !IsMotionSettingsChangePending)
            {
                long terminalLifetime = _navigationLifetime;
                animationDriver.StartTerminal(
                    OutgoingProgressContentCard,
                    InspectionOutcomeCardControl,
                    delta.VisualOperationKey,
                    completedKey =>
                    {
                        // Terminal retirement belongs to the semantic render.
                        // Disclosure interactions for that same render must not
                        // strand the inert outgoing progress layer.
                        if (terminalLifetime == _navigationLifetime &&
                            coordinator.IsCurrent(completedKey.RenderKey))
                        {
                            RetireOutgoingProgressLayer();
                        }
                    });
            }
            else
            {
                RetireOutgoingProgressLayer();
            }
        }

        ApplyTerminalFocus(current, focusedRetiredProgress);
        ApplyAnnouncements(delta, coordinator);
    }

    private DisclosureTransition CaptureDisclosureTransition(
        ModelInspectionPagePresentation? previous,
        ModelInspectionPagePresentation current,
        ModelInspectionPresentationRegions changedRegions)
    {
        if (previous is null || !TryGetDisclosureTransition(
                previous.State,
                current.State,
                out bool modelDisclosure,
                out bool isExpanded))
        {
            return DisclosureTransition.None;
        }

        if (modelDisclosure &&
            !changedRegions.HasFlag(ModelInspectionPresentationRegions.Model))
        {
            return DisclosureTransition.None;
        }

        if (!modelDisclosure &&
            !changedRegions.HasFlag(ModelInspectionPresentationRegions.Content))
        {
            return DisclosureTransition.None;
        }

        UIElement[] following = modelDisclosure
            ? [InspectionContentCardControl, InspectionActionCardControl]
            : [InspectionActionCardControl];
        double[] previousTops = new double[following.Length];
        for (int index = 0; index < following.Length; index++)
        {
            previousTops[index] = GetTop(following[index]);
        }

        return new DisclosureTransition(
            modelDisclosure,
            isExpanded,
            following,
            previousTops);
    }

    private void PrepareDisclosureTransition(DisclosureTransition transition)
    {
        if (!transition.IsActive)
        {
            return;
        }

        if (transition.IsModelDisclosure)
        {
            InspectionModelCardControl.PrepareDisclosureTarget(
                transition.IsExpanded);
        }
        else
        {
            InspectionContentCardControl.PrepareDisclosureTarget(
                transition.IsExpanded);
        }
    }

    private void StartOrCompleteDisclosure(
        DisclosureTransition transition,
        ModelInspectionVisualOperationKey operationKey,
        ModelInspectionRenderCoordinator coordinator,
        IModelInspectionAnimationDriver animationDriver,
        bool animate)
    {
        if (!transition.IsActive)
        {
            CompleteSelectedDisclosureImmediately();
            return;
        }

        InspectionDisclosure? disclosure = transition.IsModelDisclosure
            ? InspectionModelCardControl.ActiveDisclosure
            : InspectionContentCardControl.ActiveDisclosure;
        if (disclosure is null)
        {
            return;
        }

        if (!animate)
        {
            disclosure.CompleteTargetState(transition.IsExpanded);
            return;
        }

        animationDriver.StartDisclosure(
            disclosure.ChevronTarget,
            disclosure.ViewportTarget,
            transition.FollowingElements,
            transition.IsExpanded,
            transition.PreviousTopOffsets,
            operationKey,
            completedKey =>
            {
                if (coordinator.IsCurrent(completedKey))
                {
                    disclosure.CompleteTargetState(transition.IsExpanded);
                }
            });
    }

    private void CompleteSelectedDisclosureImmediately()
    {
        ModelInspectionPagePresentation? presentation = CurrentPresentation;
        if (presentation is null)
        {
            return;
        }

        bool expanded = IsExpandedState(presentation.State);
        if (HasModelDisclosure(presentation.State))
        {
            if (InspectionModelCardControl.ActiveDisclosure is not
                    InspectionDisclosure disclosure ||
                disclosure.IsExpanded == expanded)
            {
                return;
            }

            InspectionModelCardControl.PrepareDisclosureTarget(expanded);
            InspectionModelCardControl.CompleteDisclosureTarget(expanded);
        }
        else if (HasContentDisclosure(presentation.State))
        {
            if (InspectionContentCardControl.ActiveDisclosure is not
                    InspectionDisclosure disclosure ||
                disclosure.IsExpanded == expanded)
            {
                return;
            }

            InspectionContentCardControl.PrepareDisclosureTarget(expanded);
            InspectionContentCardControl.CompleteDisclosureTarget(expanded);
        }
    }

    private void ApplyAnnouncements(
        ModelInspectionPresentationDelta delta,
        ModelInspectionRenderCoordinator coordinator)
    {
        if (!delta.ChangedRegions.HasFlag(
                ModelInspectionPresentationRegions.LiveRegions))
        {
            return;
        }

        ModelInspectionPagePresentation presentation = delta.Presentation;
        if (presentation.ProgressAnnouncement.Length > 0 &&
            coordinator.IsCurrent(delta.RenderKey))
        {
            InspectionContentCardControl.AnnounceProgress(
                presentation.ProgressAnnouncement);
        }

        if (presentation.OutcomeAnnouncement.Length == 0)
        {
            return;
        }

        var identity = (
            delta.RenderKey.AttemptGeneration,
            NormalizeOutcomeState(presentation.State));
        if (!_announcedTerminalOutcomes.Add(identity))
        {
            return;
        }

        if (coordinator.IsCurrent(delta.RenderKey))
        {
            InspectionOutcomeCardControl.AnnounceOutcome(
                presentation.OutcomeAnnouncement);
        }
        else
        {
            _announcedTerminalOutcomes.Remove(identity);
        }
    }

    private void ApplyFooterStatus(InspectionFooterStatus status)
    {
        if (CurrentFooterStatus == status)
        {
            return;
        }

        CurrentFooterStatus = status;
        FooterStatusChanged?.Invoke(
            this,
            new InspectionFooterStatusChangedEventArgs(status));
    }

    private void ApplyTerminalFocus(
        ModelInspectionPagePresentation presentation,
        bool focusedRetiredProgress)
    {
        if (!focusedRetiredProgress ||
            presentation.State is not (
                ModelInspectionFigmaState.InvalidCollapsed or
                ModelInspectionFigmaState.InvalidExpanded or
                ModelInspectionFigmaState.OperationalFailure))
        {
            return;
        }

        IModelInspectionRenderDispatcher? dispatcher = _renderDispatcher;
        ModelInspectionRenderCoordinator? coordinator = _coordinator;
        if (dispatcher is null || coordinator is null)
        {
            return;
        }

        long lifetime = _navigationLifetime;
        ModelInspectionRenderKey renderKey = presentation.RenderKey;
        dispatcher.TryEnqueue(() =>
        {
            if (lifetime != _navigationLifetime ||
                !ReferenceEquals(coordinator, _coordinator) ||
                !coordinator.IsCurrent(renderKey) ||
                CurrentPresentation?.RenderKey != renderKey ||
                !InspectionOutcomeCardControl.FocusTarget.IsLoaded)
            {
                return;
            }

            UpdateLayout();
            InspectionOutcomeCardControl.FocusOutcome();
        });
    }

    private void MotionSettings_AnimationsEnabledChanged(
        MotionSettingsChangeRegistration registration,
        object? sender,
        EventArgs eventArguments)
    {
        if (!ReferenceEquals(sender, registration.Settings))
        {
            return;
        }

        MotionSettingsChangeValidatedForTesting?.Invoke();

        long revision = Interlocked.Increment(ref registration.Revision);
        MotionSettingsChangeRevisionClaimedForTesting?.Invoke(revision);
        if (!registration.Dispatcher.TryEnqueue(() =>
        {
            if (!ReferenceEquals(
                    registration,
                    Volatile.Read(ref _motionSettingsRegistration)) ||
                registration.Lifetime != _navigationLifetime ||
                !ReferenceEquals(registration.Settings, _motionSettings) ||
                revision != Volatile.Read(ref registration.Revision))
            {
                return;
            }

            ApplyMotionSettingsChange(registration, revision);
        }))
        {
            CompleteMotionSettingsChangeRevision(registration, revision);
        }
    }

    private void ApplyMotionSettingsChange(
        MotionSettingsChangeRegistration registration,
        long revision)
    {
        if (!ReferenceEquals(
                registration,
                Volatile.Read(ref _motionSettingsRegistration)) ||
            registration.Lifetime != _navigationLifetime ||
            !ReferenceEquals(registration.Settings, _motionSettings) ||
            revision != Volatile.Read(ref registration.Revision))
        {
            return;
        }

        ModelInspectionRenderCoordinator? coordinator = _coordinator;
        IModelInspectionAnimationDriver? animationDriver = _animationDriver;
        if (coordinator is null || animationDriver is null)
        {
            return;
        }

        _isApplyingMotionSettingsChange = true;
        try
        {
            coordinator.InvalidateInteractions(
                preserveDisclosureTarget: true);
            animationDriver.CancelAll();
            InspectionContentCardControl.CancelProgressMotion();
            coordinator.FlushPendingRender();
            CompleteSelectedDisclosureImmediately();
            RetireOutgoingProgressLayer();
        }
        finally
        {
            _isApplyingMotionSettingsChange = false;
            CompleteMotionSettingsChangeRevision(registration, revision);
        }
    }

    private static void CompleteMotionSettingsChangeRevision(
        MotionSettingsChangeRegistration registration,
        long revision)
    {
        long appliedRevision = Volatile.Read(ref registration.AppliedRevision);
        while (revision > appliedRevision)
        {
            long observedRevision = Interlocked.CompareExchange(
                ref registration.AppliedRevision,
                revision,
                appliedRevision);
            if (observedRevision == appliedRevision)
            {
                return;
            }

            appliedRevision = observedRevision;
        }
    }

    private bool IsMotionSettingsChangePending =>
        Volatile.Read(ref _motionSettingsRegistration) is
            MotionSettingsChangeRegistration registration &&
        Volatile.Read(ref registration.Revision) !=
            Volatile.Read(ref registration.AppliedRevision);

    private void ApplyPageReflow(ModelInspectionPagePresentation presentation)
    {
        bool expanded = IsExpandedState(presentation.State);
        Thickness hostMargin = InspectionContentHost.Margin;
        InspectionContentHost.Margin = new Thickness(
            hostMargin.Left,
            expanded ? 12d : 28d,
            hostMargin.Right,
            hostMargin.Bottom);

        Thickness actionMargin = InspectionActionCardControl.Margin;
        InspectionActionCardControl.Margin = new Thickness(
            actionMargin.Left,
            presentation.State == ModelInspectionFigmaState.InspectionProgress
                ? 16d
                : 24d,
            actionMargin.Right,
            actionMargin.Bottom);
    }

    private double GetTop(UIElement element)
    {
        XamlRoot? sharedRoot = XamlRoot;
        if (sharedRoot is null || element.XamlRoot is null)
        {
            return 0d;
        }

        if (!ReferenceEquals(element.XamlRoot, sharedRoot) ||
            sharedRoot.Content is not UIElement coordinateRoot)
        {
            throw new InvalidOperationException(
                "Disclosure reflow targets must share the page XAML root.");
        }

        try
        {
            return element.TransformToVisual(coordinateRoot)
                .TransformPoint(default)
                .Y;
        }
        catch (InvalidOperationException)
        {
            return 0d;
        }
    }

    private static bool IsDescendantOrSelf(
        DependencyObject candidate,
        DependencyObject ancestor)
    {
        DependencyObject? current = candidate;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static void SuppressAccessibilityTree(DependencyObject root)
    {
        AutomationProperties.SetAccessibilityView(root, AccessibilityView.Raw);
        if (root is Control control)
        {
            control.IsEnabled = false;
            control.IsTabStop = false;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            SuppressAccessibilityTree(
                VisualTreeHelper.GetChild(root, index));
        }
    }

    private void RetireOutgoingProgressLayer()
    {
        OutgoingProgressContentCard.Visibility = Visibility.Collapsed;
        OutgoingProgressContentCard.Opacity = 0d;
        OutgoingProgressContentCard.Presentation =
            InspectionContentCardPresentation.Hidden;
    }

    private void RetireNavigationLifetime()
    {
        ModelInspectionViewModel? retiredViewModel = ViewModel;
        ModelInspectionRenderCoordinator? retiredCoordinator = _coordinator;
        IModelInspectionAnimationDriver? retiredDriver = _animationDriver;
        IModelInspectionMotionSettings? retiredSettings = _motionSettings;
        MotionSettingsChangeRegistration? retiredSettingsRegistration =
            Interlocked.Exchange(ref _motionSettingsRegistration, null);

        _navigationLifetime = checked(_navigationLifetime + 1);
        if (retiredSettings is not null &&
            retiredSettingsRegistration is not null)
        {
            retiredSettings.AnimationsEnabledChanged -=
                retiredSettingsRegistration.ChangedHandler;
        }

        FooterStatusChanged = null;
        if (retiredViewModel is not null)
        {
            Unsubscribe(retiredViewModel);
        }

        retiredCoordinator?.InvalidateInteractions();
        InspectionContentCardControl.CancelProgressMotion();
        retiredDriver?.CancelAll();
        retiredDriver?.Dispose();
        retiredSettings?.Dispose();
        retiredCoordinator?.Dispose();

        Request = null;
        ViewModel = null;
        _startedViewModel = null;
        CurrentInspectionTask = null;
        _renderDispatcher = null;
        _animationDriver = null;
        _motionSettings = null;
        _coordinator = null;
        RetireOutgoingProgressLayer();

        if (retiredViewModel is null)
        {
            return;
        }

        retiredViewModel.Deactivate();
        retiredViewModel.Dispose();
    }

    private static bool TryGetDisclosureTransition(
        ModelInspectionFigmaState previous,
        ModelInspectionFigmaState current,
        out bool modelDisclosure,
        out bool isExpanded)
    {
        modelDisclosure = false;
        isExpanded = false;
        if (previous == ModelInspectionFigmaState.ReadyCollapsed &&
            current == ModelInspectionFigmaState.ReadyExpanded)
        {
            modelDisclosure = true;
            isExpanded = true;
            return true;
        }

        if (previous == ModelInspectionFigmaState.ReadyExpanded &&
            current == ModelInspectionFigmaState.ReadyCollapsed)
        {
            modelDisclosure = true;
            return true;
        }

        if (IsContentDisclosurePair(previous, current))
        {
            isExpanded = IsExpandedState(current);
            return true;
        }

        return false;
    }

    private static bool IsContentDisclosurePair(
        ModelInspectionFigmaState previous,
        ModelInspectionFigmaState current) =>
        (previous, current) is
            (ModelInspectionFigmaState.ReadyWithWarningsCollapsed,
             ModelInspectionFigmaState.ReadyWithWarningsExpanded) or
            (ModelInspectionFigmaState.ReadyWithWarningsExpanded,
             ModelInspectionFigmaState.ReadyWithWarningsCollapsed) or
            (ModelInspectionFigmaState.ConversionRequiredCollapsed,
             ModelInspectionFigmaState.ConversionRequiredExpanded) or
            (ModelInspectionFigmaState.ConversionRequiredExpanded,
             ModelInspectionFigmaState.ConversionRequiredCollapsed) or
            (ModelInspectionFigmaState.InvalidCollapsed,
             ModelInspectionFigmaState.InvalidExpanded) or
            (ModelInspectionFigmaState.InvalidExpanded,
             ModelInspectionFigmaState.InvalidCollapsed);

    private static bool HasModelDisclosure(ModelInspectionFigmaState state) =>
        state is ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded;

    private static bool HasContentDisclosure(ModelInspectionFigmaState state) =>
        state is ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded or
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded or
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded;

    private static bool IsExpandedState(ModelInspectionFigmaState state) =>
        state is ModelInspectionFigmaState.ReadyExpanded or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded or
            ModelInspectionFigmaState.ConversionRequiredExpanded or
            ModelInspectionFigmaState.InvalidExpanded;

    private static ModelInspectionFigmaState NormalizeOutcomeState(
        ModelInspectionFigmaState state) => state switch
        {
            ModelInspectionFigmaState.ReadyExpanded =>
                ModelInspectionFigmaState.ReadyCollapsed,
            ModelInspectionFigmaState.ReadyWithWarningsExpanded =>
                ModelInspectionFigmaState.ReadyWithWarningsCollapsed,
            ModelInspectionFigmaState.ConversionRequiredExpanded =>
                ModelInspectionFigmaState.ConversionRequiredCollapsed,
            ModelInspectionFigmaState.InvalidExpanded =>
                ModelInspectionFigmaState.InvalidCollapsed,
            _ => state
        };

    private sealed class MotionSettingsChangeRegistration
    {
        internal MotionSettingsChangeRegistration(
            long lifetime,
            IModelInspectionMotionSettings settings,
            IModelInspectionRenderDispatcher dispatcher,
            Action<MotionSettingsChangeRegistration, object?, EventArgs> changed)
        {
            Lifetime = lifetime;
            Settings = settings;
            Dispatcher = dispatcher;
            ChangedHandler = (sender, eventArguments) =>
                changed(this, sender, eventArguments);
        }

        internal long Lifetime { get; }

        internal IModelInspectionMotionSettings Settings { get; }

        internal IModelInspectionRenderDispatcher Dispatcher { get; }

        internal EventHandler ChangedHandler { get; }

        internal long Revision;

        internal long AppliedRevision;
    }

    private sealed record DisclosureTransition(
        bool IsModelDisclosure,
        bool IsExpanded,
        IReadOnlyList<UIElement> FollowingElements,
        IReadOnlyList<double> PreviousTopOffsets)
    {
        internal static DisclosureTransition None { get; } = new(
            false,
            false,
            Array.Empty<UIElement>(),
            Array.Empty<double>());

        internal bool IsActive => FollowingElements.Count > 0;
    }
}
