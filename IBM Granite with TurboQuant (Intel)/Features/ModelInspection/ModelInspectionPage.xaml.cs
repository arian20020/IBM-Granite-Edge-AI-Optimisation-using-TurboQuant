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
using System.Runtime.ExceptionServices;
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
    private readonly bool _startInspectionOnLoaded;
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
    private bool _hasActiveLifetime;
    private bool _retirementInProgress;
    private DependencyObject? _semanticFocusOwner;
    private PendingSemanticFocusReclaim? _pendingSemanticFocusReclaim;
    private IDisposable? _activeDisclosureOperationAudit;

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
        : this(
            service,
            dispatcherFactory,
            animationDriverFactory,
            motionSettingsFactory,
            startInspectionOnLoaded: true,
            configureResourcesBeforeInitialize: null)
    {
    }

    private ModelInspectionPage(
        IModelInspectionService service,
        Func<IModelInspectionRenderDispatcher> dispatcherFactory,
        Func<IModelInspectionAnimationDriver> animationDriverFactory,
        Func<IModelInspectionMotionSettings> motionSettingsFactory,
        bool startInspectionOnLoaded,
        Action<ResourceDictionary>? configureResourcesBeforeInitialize)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dispatcherFactory = dispatcherFactory ??
            throw new ArgumentNullException(nameof(dispatcherFactory));
        _animationDriverFactory = animationDriverFactory ??
            throw new ArgumentNullException(nameof(animationDriverFactory));
        _motionSettingsFactory = motionSettingsFactory ??
            throw new ArgumentNullException(nameof(motionSettingsFactory));
        _startInspectionOnLoaded = startInspectionOnLoaded;

        configureResourcesBeforeInitialize?.Invoke(Resources);
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

        ActivateRequest(request);
    }

    private void ActivateRequest(ModelInspectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_retirementInProgress)
        {
            throw new InvalidOperationException(
                "A Model Inspection page cannot activate while its prior lifetime is retiring.");
        }

        RetirePageLifetime();

        IModelInspectionAnimationDriver? animationDriver = null;
        IModelInspectionMotionSettings? motionSettings = null;
        ModelInspectionViewModel? viewModel = null;
        ModelInspectionRenderCoordinator? coordinator = null;
        bool ownershipPublished = false;
        try
        {
            IModelInspectionRenderDispatcher dispatcher =
                _dispatcherFactory() ?? throw new InvalidOperationException(
                    "The Model Inspection dispatcher factory returned null.");
            animationDriver =
                _animationDriverFactory() ?? throw new InvalidOperationException(
                    "The Model Inspection animation factory returned null.");
            motionSettings =
                _motionSettingsFactory() ?? throw new InvalidOperationException(
                    "The Model Inspection motion-settings factory returned null.");
            viewModel = new ModelInspectionViewModel(_service, request);
            var commands = new ModelInspectionPresentationCommands(
                viewModel.CancelCommand,
                viewModel.RetryCommand,
                viewModel.ChooseAnotherCommand);

            long lifetime = checked(_navigationLifetime + 1);
            var motionSettingsRegistration = new MotionSettingsChangeRegistration(
                lifetime,
                motionSettings,
                dispatcher,
                MotionSettings_AnimationsEnabledChanged);
            coordinator = new ModelInspectionRenderCoordinator(
                dispatcher,
                (snapshot, isExpanded, progressRows) =>
                    ModelInspectionPresentationFactory.Create(
                        request,
                        snapshot,
                        commands,
                        isExpanded,
                        progressRows),
                ApplyDelta);

            _navigationLifetime = lifetime;
            _announcedTerminalOutcomes.Clear();
            Request = request;
            ViewModel = viewModel;
            CurrentPresentation = null;
            _renderDispatcher = dispatcher;
            _animationDriver = animationDriver;
            _motionSettings = motionSettings;
            Volatile.Write(
                ref _motionSettingsRegistration,
                motionSettingsRegistration);
            _coordinator = coordinator;
            _hasActiveLifetime = true;
            ownershipPublished = true;

            Subscribe(viewModel);
            motionSettings.AnimationsEnabledChanged +=
                motionSettingsRegistration.ChangedHandler;
            coordinator.ApplyInitial(viewModel.Snapshot);
        }
        catch (Exception activationError)
        {
            ExceptionDispatchInfo capturedActivation =
                ExceptionDispatchInfo.Capture(activationError);
            if (ownershipPublished)
            {
                try
                {
                    RetirePageLifetime();
                }
                catch
                {
                    // The activation failure remains the primary error. The shared
                    // retirement path has already attempted every owned cleanup.
                }
            }
            else
            {
                ExceptionDispatchInfo? ignoredCleanupError = null;
                AttemptCleanup(() => coordinator?.Dispose(), ref ignoredCleanupError);
                AttemptCleanup(() => animationDriver?.CancelAll(), ref ignoredCleanupError);
                AttemptCleanup(() => animationDriver?.Dispose(), ref ignoredCleanupError);
                AttemptCleanup(() => motionSettings?.Dispose(), ref ignoredCleanupError);
                AttemptCleanup(() => viewModel?.Dispose(), ref ignoredCleanupError);
            }

            capturedActivation.Throw();
            throw;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs eventArguments)
    {
        RetirePageLifetime();
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
        ModelInspectionPagePresentation? presentation = CurrentPresentation;
        if (_hasActiveLifetime && presentation is not null)
        {
            long lifetime = _navigationLifetime;
            ModelInspectionRenderKey renderKey = presentation.RenderKey;
            ApplySemanticDefaultFocus(
                presentation,
                claimWhenFocusIsOutsidePage: _semanticFocusOwner is null,
                reclaimEffectivePageFallback: false);
            bool stillNeedsInitialClaim = _semanticFocusOwner is null;
            IDisposable? dispatcherAudit = null;
            BeginDispatcherAuditForFixture(ref dispatcherAudit);
            bool enqueued = DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (_hasActiveLifetime &&
                        lifetime == _navigationLifetime &&
                        CurrentPresentation?.RenderKey == renderKey)
                    {
                        ApplySemanticDefaultFocus(
                            CurrentPresentation,
                            claimWhenFocusIsOutsidePage: stillNeedsInitialClaim,
                            reclaimEffectivePageFallback: false);
                    }
                }
                finally
                {
                    dispatcherAudit?.Dispose();
                }
            });
            if (!enqueued)
            {
                dispatcherAudit?.Dispose();
            }
        }

        if (_startInspectionOnLoaded)
        {
            _ = StartInspectionIfReadyAsync();
        }
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

        ModelInspectionRenderCoordinator? coordinator = _coordinator;
        if (coordinator is null)
        {
            return;
        }

        coordinator.RequestRender(viewModel.Snapshot);
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

        IDisposable? disclosureAudit = null;
        BeginDisclosureAuditForFixture(ref disclosureAudit);
        try
        {
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

            InspectionDisclosure? activeDisclosure = fromModel
                ? InspectionModelCardControl.ActiveDisclosure
                : InspectionContentCardControl.ActiveDisclosure;
            DependencyObject? focusedElement = XamlRoot is null
                ? null
                : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (activeDisclosure is not null &&
                ReferenceEquals(focusedElement, activeDisclosure))
            {
                _semanticFocusOwner = activeDisclosure;
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

            Interlocked.Exchange(
                ref _activeDisclosureOperationAudit,
                disclosureAudit)?.Dispose();
            disclosureAudit = null;
        }
        finally
        {
            disclosureAudit?.Dispose();
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
        Interlocked.Exchange(ref _pendingSemanticFocusReclaim, null);
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
        bool initialAutomaticModelFallback = previous is null &&
            _semanticFocusOwner is null &&
            focusedElement is not null &&
            IsDescendantOrSelf(focusedElement, InspectionModelCardControl);
        bool semanticFocusWasOwned = initialAutomaticModelFallback ||
            (_semanticFocusOwner is not null &&
             ReferenceEquals(focusedElement, _semanticFocusOwner));
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

        bool disclosureCompleted = false;
        void CompleteDisclosureBoundary()
        {
            disclosureCompleted = true;
            TryCompletePendingSemanticFocusReclaim(
                coordinator,
                current.RenderKey);
        }

        StartOrCompleteDisclosure(
            transition,
            delta.VisualOperationKey,
            coordinator,
            animationDriver,
            motionSettings.AnimationsEnabled &&
                !_isApplyingMotionSettingsChange &&
                !IsMotionSettingsChangePending,
            CompleteDisclosureBoundary);

        if (retiresProgress)
        {
            if (motionSettings.AnimationsEnabled &&
                !_isApplyingMotionSettingsChange &&
                !IsMotionSettingsChangePending)
            {
                long terminalLifetime = _navigationLifetime;
                Action<ModelInspectionVisualOperationKey> completed =
                    completedKey =>
                    {
                        // Terminal retirement belongs to the semantic render.
                        // Disclosure interactions for that same render must not
                        // strand the inert outgoing progress layer.
                        if (terminalLifetime == _navigationLifetime &&
                            ReferenceEquals(_coordinator, coordinator) &&
                            coordinator.IsCurrent(completedKey.RenderKey))
                        {
                            RetireOutgoingProgressLayer();
                        }
                    };
                CaptureStaleMotionCallbackForFixture(
                    delta.VisualOperationKey,
                    completed);
                animationDriver.StartTerminal(
                    OutgoingProgressContentCard,
                    InspectionOutcomeCardControl,
                    delta.VisualOperationKey,
                    completed);
            }
            else
            {
                RetireOutgoingProgressLayer();
            }
        }

        ApplyTerminalFocus(current, focusedRetiredProgress);
        ApplySemanticDefaultFocus(
            current,
            claimWhenFocusIsOutsidePage: semanticFocusWasOwned,
            reclaimEffectivePageFallback: semanticFocusWasOwned);
        DependencyObject? semanticFocusedElement = XamlRoot is null
            ? null
            : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        FrameworkElement? semanticTarget = FindSemanticDefaultFocusTarget(
            current,
            semanticFocusedElement);
        if (semanticFocusWasOwned &&
            semanticFocusedElement is not null &&
            semanticTarget is not null &&
            !ReferenceEquals(semanticFocusedElement, semanticTarget))
        {
            _pendingSemanticFocusReclaim = new PendingSemanticFocusReclaim(
                _navigationLifetime,
                current.RenderKey,
                coordinator,
                semanticFocusedElement);
        }

        if (disclosureCompleted)
        {
            TryCompletePendingSemanticFocusReclaim(
                coordinator,
                current.RenderKey);
        }

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
        bool animate,
        Action disclosureCompleted)
    {
        if (!transition.IsActive)
        {
            CompleteSelectedDisclosureImmediately();
            disclosureCompleted();
            CompleteDisclosureOperationAudit();
            return;
        }

        InspectionDisclosure? disclosure = transition.IsModelDisclosure
            ? InspectionModelCardControl.ActiveDisclosure
            : InspectionContentCardControl.ActiveDisclosure;
        if (disclosure is null)
        {
            disclosureCompleted();
            CompleteDisclosureOperationAudit();
            return;
        }

        if (!animate)
        {
            try
            {
                disclosure.CompleteTargetState(transition.IsExpanded);
                disclosureCompleted();
            }
            finally
            {
                CompleteDisclosureOperationAudit();
            }
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
                try
                {
                    if (coordinator.IsCurrent(completedKey))
                    {
                        disclosure.CompleteTargetState(transition.IsExpanded);
                        disclosureCompleted();
                    }
                }
                finally
                {
                    CompleteDisclosureOperationAudit();
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
            IDisposable? liveAudit = null;
            BeginLiveNotificationAuditForFixture(ref liveAudit);
            using (liveAudit)
            {
                InspectionContentCardControl.AnnounceProgress(
                    presentation.ProgressAnnouncement);
            }
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

        long announcementLifetime = _navigationLifetime;
        ModelInspectionRenderKey announcementKey = delta.RenderKey;
        string announcement = presentation.OutcomeAnnouncement;
        Action announceIfCurrent = () =>
        {
            if (announcementLifetime == _navigationLifetime &&
                ReferenceEquals(_coordinator, coordinator) &&
                coordinator.IsCurrent(announcementKey))
            {
                IDisposable? liveAudit = null;
                BeginLiveNotificationAuditForFixture(ref liveAudit);
                using (liveAudit)
                {
                    InspectionOutcomeCardControl.AnnounceOutcome(announcement);
                }
            }
        };
        CaptureStaleAnnouncementCallbackForFixture(
            announcementKey,
            announceIfCurrent);
        if (announcementLifetime == _navigationLifetime &&
            ReferenceEquals(_coordinator, coordinator) &&
            coordinator.IsCurrent(announcementKey))
        {
            announceIfCurrent();
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
            IDisposable? focusAudit = null;
            BeginFocusAuditForFixture(ref focusAudit);
            using (focusAudit)
            {
                InspectionOutcomeCardControl.FocusOutcome();
            }
        });
    }

    private void ApplySemanticDefaultFocus(
        ModelInspectionPagePresentation? presentation,
        bool claimWhenFocusIsOutsidePage,
        bool reclaimEffectivePageFallback)
    {
        if (!_hasActiveLifetime ||
            presentation is null ||
            !ReferenceEquals(presentation, CurrentPresentation) ||
            XamlRoot is null)
        {
            return;
        }

        DependencyObject? focused =
            FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (focused is not null &&
            !ReferenceEquals(focused, _semanticFocusOwner))
        {
            bool isInsidePage = IsDescendantOrSelf(focused, this);
            if ((isInsidePage &&
                    IsEffectivePageFocus(focused) &&
                    !reclaimEffectivePageFallback) ||
                (!isInsidePage && !claimWhenFocusIsOutsidePage))
            {
                return;
            }
        }

        FrameworkElement? target = FindSemanticDefaultFocusTarget(
            presentation,
            focused);
        if (target is null || ReferenceEquals(focused, target))
        {
            return;
        }

        bool wasTabStop = target.IsTabStop;
        target.IsTabStop = true;
        try
        {
            IDisposable? focusAudit = null;
            BeginFocusAuditForFixture(ref focusAudit);
            using (focusAudit)
            {
                if (target.Focus(FocusState.Programmatic))
                {
                    _semanticFocusOwner = target;
                }
            }
        }
        finally
        {
            target.IsTabStop = wasTabStop;
        }
    }

    private FrameworkElement? FindSemanticDefaultFocusTarget(
        ModelInspectionPagePresentation presentation,
        DependencyObject? focused)
    {
        if (presentation.State == ModelInspectionFigmaState.InspectionProgress)
        {
            Button cancel = (Button)InspectionActionCardControl.FindName(
                "CancelActionButton");
            return cancel.IsEnabled || ReferenceEquals(focused, cancel)
                ? cancel
                : InspectionModelCardControl;
        }

        return FindChooseAnotherAction();
    }

    private void TryCompletePendingSemanticFocusReclaim(
        ModelInspectionRenderCoordinator coordinator,
        ModelInspectionRenderKey renderKey)
    {
        PendingSemanticFocusReclaim? pending =
            Volatile.Read(ref _pendingSemanticFocusReclaim);
        if (pending is null ||
            pending.RenderKey != renderKey ||
            !ReferenceEquals(pending.Coordinator, coordinator) ||
            !ReferenceEquals(
                Interlocked.CompareExchange(
                    ref _pendingSemanticFocusReclaim,
                    null,
                    pending),
                pending))
        {
            return;
        }

        if (!_hasActiveLifetime ||
            pending.Lifetime != _navigationLifetime ||
            !ReferenceEquals(_coordinator, coordinator) ||
            !coordinator.IsCurrent(renderKey) ||
            CurrentPresentation?.RenderKey != renderKey ||
            XamlRoot is null ||
            !ReferenceEquals(
                FocusManager.GetFocusedElement(XamlRoot),
                pending.AutomaticFocusFallback))
        {
            return;
        }

        UpdateLayout();
        ApplySemanticDefaultFocus(
            CurrentPresentation,
            claimWhenFocusIsOutsidePage: false,
            reclaimEffectivePageFallback: true);
    }

    private Button? FindChooseAnotherAction()
    {
        foreach (string name in new[]
                 {
                     "SecondaryActionOneButton",
                     "SecondaryActionTwoButton",
                     "PrimaryActionButton"
                 })
        {
            var button = (Button)InspectionActionCardControl.FindName(name);
            if (button.Visibility == Visibility.Visible &&
                button.IsEnabled &&
                string.Equals(
                    button.Tag as string,
                    "choose-another",
                    StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private bool IsEffectivePageFocus(DependencyObject focused)
    {
        DependencyObject? current = focused;
        while (current is not null)
        {
            if (current is UIElement element &&
                element.Visibility != Visibility.Visible)
            {
                return false;
            }

            if (current is Control control && !control.IsEnabled)
            {
                return false;
            }

            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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
            ModelInspectionPagePresentation? presentation = CurrentPresentation;
            if (presentation is not null)
            {
                TryCompletePendingSemanticFocusReclaim(
                    coordinator,
                    presentation.RenderKey);
            }
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
        Thickness hostMargin = InspectionReflowHost.Margin;
        double hostTop = presentation.State switch
        {
            ModelInspectionFigmaState.InspectionProgress => 21d,
            _ when expanded => 11d,
            _ => 27d
        };
        InspectionReflowHost.Margin = new Thickness(
            hostMargin.Left,
            hostTop,
            hostMargin.Right,
            hostMargin.Bottom);

        Thickness contentMargin = InspectionContentCardControl.Margin;
        InspectionContentCardControl.Margin = new Thickness(
            contentMargin.Left,
            presentation.State == ModelInspectionFigmaState.InspectionProgress
                ? 20d
                : 16d,
            contentMargin.Right,
            contentMargin.Bottom);

        Thickness actionMargin = InspectionActionCardControl.Margin;
        double actionTop = presentation.State switch
        {
            ModelInspectionFigmaState.InspectionProgress or
            ModelInspectionFigmaState.Cancelled => 30d,
            ModelInspectionFigmaState.ReadyCollapsed => 14d,
            ModelInspectionFigmaState.ReadyExpanded => 6d,
            _ when expanded => 22d,
            _ => 24d
        };
        InspectionActionCardControl.Margin = new Thickness(
            actionMargin.Left,
            actionTop,
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

    private bool RetirePageLifetime()
    {
        if (_retirementInProgress || !_hasActiveLifetime)
        {
            return false;
        }

        long retiredLifetime = checked(_navigationLifetime + 1);
        _retirementInProgress = true;
        _hasActiveLifetime = false;
        ModelInspectionViewModel? retiredViewModel = ViewModel;
        ModelInspectionRenderCoordinator? retiredCoordinator = _coordinator;
        IModelInspectionAnimationDriver? retiredDriver = _animationDriver;
        IModelInspectionMotionSettings? retiredSettings = _motionSettings;
        MotionSettingsChangeRegistration? retiredSettingsRegistration =
            Interlocked.Exchange(ref _motionSettingsRegistration, null);

        _navigationLifetime = retiredLifetime;
        FooterStatusChanged = null;
        Request = null;
        ViewModel = null;
        _startedViewModel = null;
        CurrentInspectionTask = null;
        _renderDispatcher = null;
        _animationDriver = null;
        _motionSettings = null;
        _coordinator = null;
        _semanticFocusOwner = null;
        Interlocked.Exchange(ref _pendingSemanticFocusReclaim, null);
        ExceptionDispatchInfo? error = null;
        try
        {
            if (retiredSettings is not null &&
                retiredSettingsRegistration is not null)
            {
                AttemptCleanup(
                    () => retiredSettings.AnimationsEnabledChanged -=
                        retiredSettingsRegistration.ChangedHandler,
                    ref error);
            }

            if (retiredViewModel is not null)
            {
                AttemptCleanup(() => Unsubscribe(retiredViewModel), ref error);
            }

            // Invalidation must precede every callback-producing cancellation.
            AttemptCleanup(
                () => retiredCoordinator?.InvalidateInteractions(),
                ref error);
            AttemptCleanup(
                () => CompleteDispatcherAuditsForFixture(),
                ref error);
            AttemptCleanup(CompleteDisclosureOperationAudit, ref error);
            AttemptCleanup(
                InspectionContentCardControl.CancelProgressMotion,
                ref error);
            AttemptCleanup(() => retiredDriver?.CancelAll(), ref error);
            AttemptCleanup(() => retiredDriver?.Dispose(), ref error);
            AttemptCleanup(() => retiredSettings?.Dispose(), ref error);
            AttemptCleanup(() => retiredCoordinator?.Dispose(), ref error);
            AttemptCleanup(RetireOutgoingProgressLayer, ref error);

            if (retiredViewModel is not null)
            {
                AttemptCleanup(retiredViewModel.Deactivate, ref error);
                AttemptCleanup(retiredViewModel.Dispose, ref error);
            }
        }
        finally
        {
            _retirementInProgress = false;
        }

        error?.Throw();
        return true;
    }

    private static void AttemptCleanup(
        Action cleanup,
        ref ExceptionDispatchInfo? error)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    partial void CaptureStaleMotionCallbackForFixture(
        ModelInspectionVisualOperationKey operationKey,
        Action<ModelInspectionVisualOperationKey> completed);

    partial void CaptureStaleAnnouncementCallbackForFixture(
        ModelInspectionRenderKey renderKey,
        Action callback);

    partial void BeginDispatcherAuditForFixture(ref IDisposable? audit);

    partial void BeginFocusAuditForFixture(ref IDisposable? audit);

    partial void BeginDisclosureAuditForFixture(ref IDisposable? audit);

    partial void BeginLiveNotificationAuditForFixture(ref IDisposable? audit);

    partial void CompleteDispatcherAuditsForFixture();

    private void CompleteDisclosureOperationAudit() =>
        Interlocked.Exchange(
            ref _activeDisclosureOperationAudit,
            null)?.Dispose();

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

    private sealed record PendingSemanticFocusReclaim(
        long Lifetime,
        ModelInspectionRenderKey RenderKey,
        ModelInspectionRenderCoordinator Coordinator,
        DependencyObject AutomaticFocusFallback);

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
