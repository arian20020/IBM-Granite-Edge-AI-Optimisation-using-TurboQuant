using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
#if HARDWARE_INSPECTION_X64
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
#endif
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.Features.Onboarding
{
    /// <summary>
    /// Hosts the onboarding journey and displays one onboarding page at a time.
    /// </summary>
    public sealed partial class OnboardingShellPage : Page
    {
        // Stores the Model Import page whose inspection request the shell is
        // currently listening to.
        private ModelImportPage? _attachedModelImportPage;

        // Stores the Model Inspection page whose choose-another request the
        // shell is currently listening to.
        private ModelInspectionPage? _attachedModelInspectionPage;
        private HardwareInspectionPage? _attachedHardwareInspectionPage;
        private HardwareInspectionPage? _hardwarePageForCompatibilityReturn;
        private CompatibilityPage? _attachedCompatibilityPage;
        private ModelInspectionPage? _modelInspectionPageForHardwareReturn;
        private Guid _activeModelHandoffId;
        private Guid _activeProductHardwareRunId;
        private ModelInspectionHandoff? _pendingHardwareRetryHandoff;
        private bool _isHardwareNavigationTransaction;

        private readonly Func<Frame, ModelInspectionRequest, bool>
            _modelInspectionNavigator;
        private readonly IHardwareInspectionService? _hardwareInspectionService;
        private readonly Func<
            Frame,
            HardwareInspectionViewModel,
            ModelInspectionHandoff,
            bool> _hardwareInspectionNavigator;
        private readonly Func<ModelInspectionPage, ModelInspectionHandoff?>
            _hardwareHandoffReissuer;
        private readonly ModelInspectionHandoffRegistry _handoffRegistry = new();
        private readonly ICompatibilityFreshResourcesSource? _compatibilityFreshResourcesSource;
        private readonly Func<
            ModelInspectionPage,
            ModelInspectionHandoff,
            ModelInspectionExecutionResult?> _modelEvidenceResolver;
        private readonly Func<Frame, CompatibilityPage, bool> _compatibilityNavigator;

        /// <summary>
        /// Creates the onboarding shell and displays the first stage.
        /// </summary>
        public OnboardingShellPage()
            : this(static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request),
                hardwareInspectionService:
#if HARDWARE_INSPECTION_X64
                    HardwareInspectionComposition.CreateProduction(),
#else
                    null,
#endif
                hardwareInspectionNavigator: null,
                hardwareHandoffReissuer: null,
                initialize: true)
        {
        }

        internal OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator)
            : this(
                modelInspectionNavigator,
                hardwareInspectionService: null,
                hardwareInspectionNavigator: null,
                hardwareHandoffReissuer: null,
                initialize: true)
        {
        }

        internal OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator,
            IHardwareInspectionService hardwareInspectionService,
            Func<
                Frame,
                HardwareInspectionViewModel,
                ModelInspectionHandoff,
                bool>? hardwareInspectionNavigator,
            Func<ModelInspectionPage, ModelInspectionHandoff?>?
                hardwareHandoffReissuer,
            ICompatibilityFreshResourcesSource compatibilityFreshResourcesSource,
            Func<
                ModelInspectionPage,
                ModelInspectionHandoff,
                ModelInspectionExecutionResult?> modelEvidenceResolver,
            Func<Frame, CompatibilityPage, bool>? compatibilityNavigator)
            : this(
                modelInspectionNavigator,
                hardwareInspectionService,
                hardwareInspectionNavigator,
                hardwareHandoffReissuer,
                initialize: true)
        {
            _compatibilityFreshResourcesSource = compatibilityFreshResourcesSource
                ?? throw new ArgumentNullException(nameof(compatibilityFreshResourcesSource));
            _modelEvidenceResolver = modelEvidenceResolver
                ?? throw new ArgumentNullException(nameof(modelEvidenceResolver));
            _compatibilityNavigator = compatibilityNavigator ?? DefaultCompatibilityNavigation;
        }

        internal OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator,
            IHardwareInspectionService hardwareInspectionService,
            Func<
                Frame,
                HardwareInspectionViewModel,
                ModelInspectionHandoff,
                bool>? hardwareInspectionNavigator = null,
            Func<ModelInspectionPage, ModelInspectionHandoff?>?
                hardwareHandoffReissuer = null)
            : this(
                modelInspectionNavigator,
                hardwareInspectionService ?? throw new ArgumentNullException(
                    nameof(hardwareInspectionService)),
                hardwareInspectionNavigator,
                hardwareHandoffReissuer,
                initialize: true)
        {
        }

        private OnboardingShellPage(
            Func<Frame, ModelInspectionRequest, bool> modelInspectionNavigator,
            IHardwareInspectionService? hardwareInspectionService,
            Func<
                Frame,
                HardwareInspectionViewModel,
                ModelInspectionHandoff,
                bool>? hardwareInspectionNavigator,
            Func<ModelInspectionPage, ModelInspectionHandoff?>?
                hardwareHandoffReissuer,
            bool initialize)
        {
            _modelInspectionNavigator = modelInspectionNavigator ??
                throw new ArgumentNullException(nameof(modelInspectionNavigator));
            _hardwareInspectionService = hardwareInspectionService;
            _hardwareInspectionNavigator = hardwareInspectionNavigator ??
                (static (frame, viewModel, handoff) =>
                {
                    frame.Content = new HardwareInspectionPage(
                        viewModel,
                        handoff);
                    return true;
                });
            _hardwareHandoffReissuer = hardwareHandoffReissuer ??
                (static page => page.ReissueHardwareHandoff());
#if HARDWARE_INSPECTION_X64
            _compatibilityFreshResourcesSource =
                new WindowsCompatibilityFreshResourcesSource();
#else
            _compatibilityFreshResourcesSource = null;
#endif
            _modelEvidenceResolver = static (page, handoff) =>
                page.ResolveTerminalResult(handoff);
            _compatibilityNavigator = DefaultCompatibilityNavigation;

            // Create all controls declared in OnboardingShellPage.xaml.
            InitializeComponent();
            InitializeFixtureGalleryEntry();

            // Record that onboarding starts on the model-import stage.
            CurrentStage = OnboardingStage.ImportModel;

            // Keep the permanent stage indicator synchronized.
            StageIndicator.CurrentStage = CurrentStage;

            // Display ModelImportPage and subscribe to its navigation request.
            ShowInitialStage();
        }

        partial void InitializeFixtureGalleryEntry();

        /// <summary>
        /// Gets the onboarding stage currently displayed by the shell.
        /// </summary>
        public OnboardingStage CurrentStage { get; private set; }

        internal bool IsHardwareRouteRegistered =>
            _hardwareInspectionService is not null;

        internal Guid CurrentProductHardwareRunId =>
            _activeProductHardwareRunId;

        internal event EventHandler<SourceModelConversionRequestedEventArgs>?
            SourceModelConversionRequested;

        /// <summary>
        /// Subscribes the shell to one Model Import page.
        /// </summary>
        internal void AttachModelImportPage(
            ModelImportPage modelImportPage)
        {
            // Reject a missing page immediately.
            ArgumentNullException.ThrowIfNull(modelImportPage);

            // Avoid subscribing twice to the same page instance.
            if (ReferenceEquals(
                _attachedModelImportPage,
                modelImportPage))
            {
                return;
            }

            // Stop listening to an older Model Import page.
            DetachModelImportPage();

            // Store the current page.
            _attachedModelImportPage = modelImportPage;

            // Listen for the page's request to begin model inspection.
            _attachedModelImportPage.ModelInspectionRequested +=
                ModelImportPage_ModelInspectionRequested;
            _attachedModelImportPage.OpenVinoInspectionRequested +=
                ModelImportPage_OpenVinoInspectionRequested;
            _attachedModelImportPage.SourceModelConversionRequested +=
                ModelImportPage_SourceModelConversionRequested;
        }

        /// <summary>
        /// Subscribes the shell to one active Model Inspection page.
        /// </summary>
        internal void AttachModelInspectionPage(
            ModelInspectionPage modelInspectionPage)
        {
            ArgumentNullException.ThrowIfNull(modelInspectionPage);

            // Reattaching the same Frame content must not duplicate the event
            // handler and therefore cannot cause duplicate reset navigation.
            if (ReferenceEquals(
                _attachedModelInspectionPage,
                modelInspectionPage))
            {
                return;
            }

            // A page replaced by successful Frame navigation is no longer
            // allowed to drive the shell.
            DetachModelInspectionPage();
            _attachedModelInspectionPage = modelInspectionPage;
            _attachedModelInspectionPage.ChooseAnotherModelRequested +=
                ModelInspectionPage_ChooseAnotherModelRequested;
            _attachedModelInspectionPage.FooterStatusChanged +=
                ModelInspectionPage_FooterStatusChanged;
            _attachedModelInspectionPage.HardwareInspectionRequested +=
                ModelInspectionPage_HardwareInspectionRequested;
            _attachedModelInspectionPage.SetHardwareRouteAvailable(
                _hardwareInspectionService is not null);

            // Navigation has already completed by the time the Frame hands
            // ownership to the shell, so sample the authoritative status now.
            StageIndicator.InspectionStatus =
                _attachedModelInspectionPage.CurrentFooterStatus;
        }

        /// <summary>
        /// Navigates the onboarding frame to the model-inspection stage.
        /// </summary>
        /// <param name="request">
        /// The immutable, validated Model Inspection request.
        /// </param>
        /// <returns>
        /// True when Frame navigation succeeds; otherwise, false.
        /// </returns>
        internal bool NavigateToModelInspection(
            ModelInspectionRequest request)
        {
            // Never navigate without the validated request created by Model Import.
            ArgumentNullException.ThrowIfNull(request);

            object? previousContent = StageFrame.Content;

            // Ask the onboarding Frame to create ModelInspectionPage.
            // The request becomes NavigationEventArgs.Parameter on that page.
            bool navigationSucceeded = _modelInspectionNavigator(
                StageFrame,
                request);

            // A cancelled Navigating event can leave Navigate reporting true.
            // Confirm the destination before transferring any ownership.
            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not ModelInspectionPage modelInspectionPage ||
                !ReferenceEquals(modelInspectionPage.Request, request))
            {
                return false;
            }

            // Onboarding owns navigation through explicit stage transitions.
            // Do not retain the import page or its path-bearing state in the
            // Frame journal where Back could resurrect it outside that state.
            StageFrame.BackStack.Clear();

            AttachModelInspectionPage(modelInspectionPage);

            // Model Import is no longer the active page after navigation.
            DetachModelImportPage();

            // Record that onboarding has advanced to stage two.
            CurrentStage = OnboardingStage.InspectModel;

            // Keep the permanent visual indicator synchronized.
            StageIndicator.CurrentStage = CurrentStage;

            return true;
        }

        /// <summary>
        /// Displays ModelImportPage inside the shell's StageFrame.
        /// </summary>
        private void ShowInitialStage()
        {
            // Navigate the internal Frame to the initial onboarding page.
            bool navigationSucceeded =
                StageFrame.Navigate(typeof(ModelImportPage));

            // Fail immediately if the first page could not be displayed.
            if (!navigationSucceeded)
            {
                throw new InvalidOperationException(
                    "The onboarding shell could not display ModelImportPage.");
            }

            // Confirm that Frame created the expected page type.
            if (StageFrame.Content is not ModelImportPage modelImportPage)
            {
                throw new InvalidOperationException(
                    "StageFrame did not create the expected ModelImportPage.");
            }

            // Listen to the real Model Import page created by Frame navigation.
            AttachModelImportPage(modelImportPage);
        }

        /// <summary>
        /// Responds when ModelImportPage requests full model inspection.
        /// </summary>
        private void ModelImportPage_ModelInspectionRequested(
            object? sender,
            ModelInspectionRequestedEventArgs eventArguments)
        {
            // Forward the exact immutable request without reconstructing it.
            NavigateToModelInspection(eventArguments.Request);
        }

        private void ModelImportPage_OpenVinoInspectionRequested(
            object? sender,
            OpenVinoInspectionRequestedEventArgs eventArguments)
        {
            if (sender is ModelImportPage page &&
                ReferenceEquals(page, _attachedModelImportPage))
            {
                // O1 has no frozen inspection invocation port in this build.
                // Do not navigate to a placeholder or imply that inspection ran.
                page.RejectFolderRoute(
                    "openvino-inspection-unavailable",
                    "OpenVINO folder inspection is not available in this build.");
            }
        }

        private void ModelImportPage_SourceModelConversionRequested(
            object? sender,
            SourceModelConversionRequestedEventArgs eventArguments)
        {
            if (sender is not ModelImportPage page ||
                !ReferenceEquals(page, _attachedModelImportPage))
            {
                return;
            }

            // The shell relays the exact immutable intent. Conversion belongs
            // to a later route; this boundary never starts a converter/process
            // and never retains the selected folder path.
            SourceModelConversionRequested?.Invoke(this, eventArguments);
        }

        /// <summary>
        /// Responds only to the currently active inspection page and starts a
        /// fresh model-selection journey.
        /// </summary>
        private void ModelInspectionPage_ChooseAnotherModelRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedModelInspectionPage))
            {
                return;
            }

            NavigateToFreshModelImport();
        }

        private void ModelInspectionPage_FooterStatusChanged(
            object? sender,
            InspectionFooterStatusChangedEventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedModelInspectionPage))
            {
                return;
            }

            StageIndicator.InspectionStatus = eventArguments.Status;
        }

        private void ModelInspectionPage_HardwareInspectionRequested(
            object? sender,
            HardwareInspectionRequestedEventArgs eventArguments)
        {
            if (sender is ModelInspectionPage sourcePage)
            {
                NavigateToHardwareInspection(sourcePage, eventArguments.Handoff);
            }
        }

        internal bool NavigateToHardwareInspection(
            ModelInspectionPage sourcePage,
            ModelInspectionHandoff handoff)
        {
            ArgumentNullException.ThrowIfNull(sourcePage);
            ArgumentNullException.ThrowIfNull(handoff);
            if (_hardwareInspectionService is null ||
                !ReferenceEquals(sourcePage, _attachedModelInspectionPage))
            {
                return false;
            }

            _handoffRegistry.ActivateModelRun(handoff.ModelInspectionRunId);
            if (!_handoffRegistry.TryRegisterIssued(handoff))
            {
                return false;
            }

            Guid hardwareRunId = Guid.NewGuid();
            if (!_handoffRegistry.TryBindToHardwareRun(
                handoff,
                handoff.ModelInspectionRunId,
                hardwareRunId,
                out ModelInspectionHandoffClaim claim))
            {
                _handoffRegistry.Invalidate(handoff.ModelInspectionHandoffId);
                return false;
            }

            object? previousContent = StageFrame.Content;
            bool navigationSucceeded;
            try
            {
                _isHardwareNavigationTransaction = true;
                navigationSucceeded = _hardwareInspectionNavigator(
                    StageFrame,
                    new HardwareInspectionViewModel(
                        _hardwareInspectionService,
                        hardwareRunId),
                    handoff);
            }
            catch
            {
                navigationSucceeded = false;
            }
            finally
            {
                _isHardwareNavigationTransaction = false;
            }

            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not HardwareInspectionPage hardwarePage ||
                !ReferenceEquals(hardwarePage.OpaqueModelHandoff, handoff))
            {
                _handoffRegistry.TryRollbackBeforeHardwareStart(claim);
                if (!ReferenceEquals(StageFrame.Content, previousContent))
                {
                    StageFrame.Content = previousContent;
                }

                return false;
            }

            if (!_handoffRegistry.TryMarkHardwareStarted(claim))
            {
                _handoffRegistry.Invalidate(handoff.ModelInspectionHandoffId);
                StageFrame.Content = previousContent;
                return false;
            }

            hardwarePage.AuthorizeStart();
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            _attachedHardwareInspectionPage = hardwarePage;
            _attachedHardwareInspectionPage.ActionRequested +=
                HardwareInspectionPage_ActionRequested;
            _attachedHardwareInspectionPage.InspectionCompleted +=
                HardwareInspectionPage_InspectionCompleted;
            _attachedHardwareInspectionPage.JourneyAbandoned +=
                HardwareInspectionPage_JourneyAbandoned;
            _modelInspectionPageForHardwareReturn = sourcePage;
            _activeModelHandoffId = handoff.ModelInspectionHandoffId;
            _activeProductHardwareRunId = hardwareRunId;
            _pendingHardwareRetryHandoff = null;
            DetachModelInspectionPage();
            CurrentStage = OnboardingStage.CheckHardwareFit;
            StageIndicator.CurrentStage = CurrentStage;
            return true;
        }

        private async void HardwareInspectionPage_InspectionCompleted(
            object? sender,
            HardwareInspectionCompletedEventArgs eventArguments)
        {
            if (sender is HardwareInspectionPage page)
            {
                _ = await NavigateToCompatibilityAsync(page, eventArguments);
            }
        }

        internal async Task<bool> NavigateToCompatibilityAsync(
            HardwareInspectionPage sourcePage,
            HardwareInspectionCompletedEventArgs eventArguments)
        {
            ArgumentNullException.ThrowIfNull(sourcePage);
            ArgumentNullException.ThrowIfNull(eventArguments);
            ModelInspectionHandoff? modelHandoff = sourcePage.OpaqueModelHandoff;
            ModelInspectionPage? modelPage = _modelInspectionPageForHardwareReturn;
            if (_compatibilityFreshResourcesSource is null
                || !ReferenceEquals(sourcePage, _attachedHardwareInspectionPage)
                || modelHandoff is null
                || modelPage is null
                || _activeModelHandoffId != modelHandoff.ModelInspectionHandoffId
                || _activeProductHardwareRunId == Guid.Empty
                || eventArguments.Handoff.InspectionId != _activeProductHardwareRunId
                || _handoffRegistry.GetState(_activeModelHandoffId)
                    != ModelInspectionHandoffLifecycleState.BoundToHardwareRun
                || _modelEvidenceResolver(modelPage, modelHandoff)
                    is not { } terminal)
            {
                return false;
            }

            if (!ReferenceEquals(sourcePage, _attachedHardwareInspectionPage)
                || _activeModelHandoffId != modelHandoff.ModelInspectionHandoffId
                || _activeProductHardwareRunId != eventArguments.Handoff.InspectionId
                || !GgufCompatibilityInputProjector.TryPrepare(
                    modelHandoff,
                    terminal,
                    _activeProductHardwareRunId,
                    eventArguments.Handoff,
                    out PreparedGgufCompatibilityInput? prepared))
            {
                return false;
            }

            var compatibilityPage = new CompatibilityPage(async token =>
            {
                try
                {
                    CompatibilityFreshResourcesInput fresh =
                        await _compatibilityFreshResourcesSource.CaptureAsync(token);
                    if (!prepared!.TryBindFresh(fresh, out CompatibilityProductionInput? input))
                    {
                        return CompatibilityEngine.RunWithAvailableAdapters(token);
                    }

                    return await Task.Run(
                        () => CompatibilityEngine.Run(input!, token), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    return CompatibilityEngine.RunWithAvailableAdapters(token);
                }
            },
                continueDestinationAvailable: false);
            compatibilityPage.BackRequested += CompatibilityPage_BackRequested;
            compatibilityPage.ContinueRequested += CompatibilityPage_ContinueRequested;

            DetachHardwareInspectionPage();
            bool navigated;
            try
            {
                _isHardwareNavigationTransaction = true;
                navigated = _compatibilityNavigator(StageFrame, compatibilityPage)
                    && ReferenceEquals(StageFrame.Content, compatibilityPage);
            }
            catch
            {
                navigated = false;
            }
            finally
            {
                _isHardwareNavigationTransaction = false;
            }

            if (!navigated)
            {
                compatibilityPage.BackRequested -= CompatibilityPage_BackRequested;
                compatibilityPage.ContinueRequested -= CompatibilityPage_ContinueRequested;
                AttachHardwareInspectionPage(sourcePage);
                StageFrame.Content = sourcePage;
                return false;
            }

            _hardwarePageForCompatibilityReturn = sourcePage;
            _attachedCompatibilityPage = compatibilityPage;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            return true;
        }

        private void CompatibilityPage_BackRequested(object? sender, EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedCompatibilityPage)
                || _hardwarePageForCompatibilityReturn is not { } hardwarePage)
            {
                return;
            }

            DetachCompatibilityPage();
            _hardwarePageForCompatibilityReturn = null;
            StageFrame.Content = hardwarePage;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            AttachHardwareInspectionPage(hardwarePage);
        }

        private void CompatibilityPage_ContinueRequested(object? sender, EventArgs eventArguments)
        {
            // The optimisation-mode destination is intentionally not registered
            // in this increment, so C1 keeps this command disabled.
        }

        private void HardwareInspectionPage_ActionRequested(
            object? sender,
            HardwareInspectionActionRequestedEventArgs eventArguments)
        {
            if (sender is not HardwareInspectionPage sourcePage ||
                !ReferenceEquals(sourcePage, _attachedHardwareInspectionPage))
            {
                return;
            }

            if (eventArguments.Kind is
                HardwareInspectionActionKind.RunInspectionAgain or
                HardwareInspectionActionKind.TryAgain)
            {
                RetryHardwareInspection(sourcePage);
                return;
            }

            if (eventArguments.Kind is
                HardwareInspectionActionKind.Back or
                HardwareInspectionActionKind.BackToModelInspection)
            {
                ReturnToModelInspection();
            }
        }

        internal bool RetryHardwareInspection(HardwareInspectionPage sourcePage)
        {
            ArgumentNullException.ThrowIfNull(sourcePage);
            ModelInspectionPage? modelPage = _modelInspectionPageForHardwareReturn;
            if (_hardwareInspectionService is null ||
                !ReferenceEquals(sourcePage, _attachedHardwareInspectionPage) ||
                modelPage is null ||
                _activeModelHandoffId == Guid.Empty)
            {
                return false;
            }

            ModelInspectionHandoff? replacement = _pendingHardwareRetryHandoff;
            if (replacement is null)
            {
                replacement = _hardwareHandoffReissuer(modelPage);
                if (replacement is null ||
                    !_handoffRegistry.TryAcceptReissue(
                        _activeModelHandoffId,
                        replacement))
                {
                    return false;
                }

                _pendingHardwareRetryHandoff = replacement;
            }

            Guid hardwareRunId = Guid.NewGuid();
            if (!_handoffRegistry.TryBindToHardwareRun(
                replacement,
                replacement.ModelInspectionRunId,
                hardwareRunId,
                out ModelInspectionHandoffClaim claim))
            {
                return false;
            }

            object? previousContent = StageFrame.Content;
            bool navigationSucceeded;
            sourcePage.JourneyAbandoned -=
                HardwareInspectionPage_JourneyAbandoned;
            try
            {
                _isHardwareNavigationTransaction = true;
                navigationSucceeded = _hardwareInspectionNavigator(
                    StageFrame,
                    new HardwareInspectionViewModel(
                        _hardwareInspectionService,
                        hardwareRunId),
                    replacement);
            }
            catch
            {
                navigationSucceeded = false;
            }
            finally
            {
                _isHardwareNavigationTransaction = false;
            }

            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not HardwareInspectionPage hardwarePage ||
                !ReferenceEquals(
                    hardwarePage.OpaqueModelHandoff,
                    replacement))
            {
                _handoffRegistry.TryRollbackBeforeHardwareStart(claim);
                sourcePage.JourneyAbandoned +=
                    HardwareInspectionPage_JourneyAbandoned;
                if (!ReferenceEquals(StageFrame.Content, previousContent))
                {
                    StageFrame.Content = previousContent;
                }

                return false;
            }

            if (!_handoffRegistry.TryMarkHardwareStarted(claim))
            {
                _handoffRegistry.Invalidate(
                    replacement.ModelInspectionHandoffId);
                sourcePage.JourneyAbandoned +=
                    HardwareInspectionPage_JourneyAbandoned;
                StageFrame.Content = previousContent;
                return false;
            }

            DetachHardwareInspectionPage();
            _attachedHardwareInspectionPage = hardwarePage;
            _attachedHardwareInspectionPage.ActionRequested +=
                HardwareInspectionPage_ActionRequested;
            _attachedHardwareInspectionPage.InspectionCompleted +=
                HardwareInspectionPage_InspectionCompleted;
            _attachedHardwareInspectionPage.JourneyAbandoned +=
                HardwareInspectionPage_JourneyAbandoned;
            _activeModelHandoffId = replacement.ModelInspectionHandoffId;
            _activeProductHardwareRunId = hardwareRunId;
            _pendingHardwareRetryHandoff = null;
            hardwarePage.AuthorizeStart();
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            return true;
        }

        private void HardwareInspectionPage_JourneyAbandoned(
            object? sender,
            EventArgs eventArguments)
        {
            if (_isHardwareNavigationTransaction ||
                !ReferenceEquals(sender, _attachedHardwareInspectionPage))
            {
                return;
            }

            InvalidateActiveHardwareJourney();
            DetachHardwareInspectionPage();
            _modelInspectionPageForHardwareReturn = null;
        }

        private void ReturnToModelInspection()
        {
            ModelInspectionPage? modelPage = _modelInspectionPageForHardwareReturn;
            if (modelPage is null || _attachedHardwareInspectionPage is null)
            {
                return;
            }

            InvalidateActiveHardwareJourney();

            DetachHardwareInspectionPage();
            _modelInspectionPageForHardwareReturn = null;
            StageFrame.Content = modelPage;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            AttachModelInspectionPage(modelPage);
            modelPage.SetHardwareRouteAvailable(false);
            CurrentStage = OnboardingStage.InspectModel;
            StageIndicator.CurrentStage = CurrentStage;
        }

        private void InvalidateActiveHardwareJourney()
        {
            if (_activeModelHandoffId != Guid.Empty)
            {
                _handoffRegistry.Invalidate(_activeModelHandoffId);
            }

            if (_pendingHardwareRetryHandoff is not null)
            {
                _handoffRegistry.Invalidate(
                    _pendingHardwareRetryHandoff.ModelInspectionHandoffId);
            }

            _activeModelHandoffId = Guid.Empty;
            _activeProductHardwareRunId = Guid.Empty;
            _pendingHardwareRetryHandoff = null;
        }

        internal ModelInspectionHandoffLifecycleState? GetModelHandoffState(
            Guid modelInspectionHandoffId) =>
            _handoffRegistry.GetState(modelInspectionHandoffId);

        /// <summary>
        /// Replaces the inspection page with a new Model Import page without
        /// retaining Frame back-stack state as the active journey.
        /// </summary>
        private bool NavigateToFreshModelImport()
        {
            object? previousContent = StageFrame.Content;
            bool navigationSucceeded =
                StageFrame.Navigate(typeof(ModelImportPage));

            // Frame.Navigate can report true after a cancelled Navigating
            // event, so the expected content is the completion boundary.
            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not ModelImportPage modelImportPage)
            {
                return false;
            }


            // A new selection is a new journey. Remove the inspection page
            // and its request from the Frame journal before exposing the new
            // active stage.
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();

            // Transfer event ownership only after the new page exists.
            AttachModelImportPage(modelImportPage);
            DetachModelInspectionPage();
            InvalidateActiveHardwareJourney();
            DetachHardwareInspectionPage();
            _modelInspectionPageForHardwareReturn = null;

            CurrentStage = OnboardingStage.ImportModel;
            StageIndicator.CurrentStage = CurrentStage;
            return true;
        }

        /// <summary>
        /// Removes the current event subscription.
        /// </summary>
        private void DetachModelImportPage()
        {
            // There is nothing to detach before a page has been attached.
            if (_attachedModelImportPage is null)
            {
                return;
            }

            // Remove the event subscription to avoid retaining an inactive page.
            _attachedModelImportPage.ModelInspectionRequested -=
                ModelImportPage_ModelInspectionRequested;
            _attachedModelImportPage.OpenVinoInspectionRequested -=
                ModelImportPage_OpenVinoInspectionRequested;
            _attachedModelImportPage.SourceModelConversionRequested -=
                ModelImportPage_SourceModelConversionRequested;

            // Release the reference to the old page.
            _attachedModelImportPage = null;
        }

        /// <summary>
        /// Removes the current Model Inspection event subscription.
        /// </summary>
        private void DetachModelInspectionPage()
        {
            if (_attachedModelInspectionPage is null)
            {
                return;
            }

            _attachedModelInspectionPage.ChooseAnotherModelRequested -=
                ModelInspectionPage_ChooseAnotherModelRequested;
            _attachedModelInspectionPage.FooterStatusChanged -=
                ModelInspectionPage_FooterStatusChanged;
            _attachedModelInspectionPage.HardwareInspectionRequested -=
                ModelInspectionPage_HardwareInspectionRequested;
            _attachedModelInspectionPage.SetHardwareRouteAvailable(false);
            _attachedModelInspectionPage = null;
        }

        private void DetachHardwareInspectionPage()
        {
            if (_attachedHardwareInspectionPage is null)
            {
                return;
            }

            _attachedHardwareInspectionPage.ActionRequested -=
                HardwareInspectionPage_ActionRequested;
            _attachedHardwareInspectionPage.InspectionCompleted -=
                HardwareInspectionPage_InspectionCompleted;
            _attachedHardwareInspectionPage.JourneyAbandoned -=
                HardwareInspectionPage_JourneyAbandoned;
            _attachedHardwareInspectionPage = null;
        }

        private void AttachHardwareInspectionPage(HardwareInspectionPage page)
        {
            _attachedHardwareInspectionPage = page;
            page.ActionRequested += HardwareInspectionPage_ActionRequested;
            page.InspectionCompleted += HardwareInspectionPage_InspectionCompleted;
            page.JourneyAbandoned += HardwareInspectionPage_JourneyAbandoned;
        }

        private void DetachCompatibilityPage()
        {
            if (_attachedCompatibilityPage is null)
            {
                return;
            }

            _attachedCompatibilityPage.BackRequested -= CompatibilityPage_BackRequested;
            _attachedCompatibilityPage.ContinueRequested -= CompatibilityPage_ContinueRequested;
            _attachedCompatibilityPage = null;
        }

        private static bool DefaultCompatibilityNavigation(
            Frame frame,
            CompatibilityPage page)
        {
            frame.Content = page;
            return true;
        }
    }
}
