using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.ApplicationFaults;
using GraniteEdgeAI.Features.HardwareInspection.Application;
#if HARDWARE_INSPECTION_X64
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
#endif
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ApplicationComposition;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.ModelOptimization.Execution.OpenVino;
using GraniteEdgeAI.GgufQuantization.WorkerClient;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;

namespace GraniteEdgeAI.Features.Onboarding
{
    /// <summary>
    /// Hosts the onboarding journey and displays one onboarding page at a time.
    /// </summary>
    public sealed partial class OnboardingShellPage : Page, IDisposable
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
        private GgufOptimizationProductionAuthority? _activeGgufAuthority;
        private OpenVinoOptimizationProductionAuthority? _activeOpenVinoAuthority;
        private OpenVinoOptimizationService? _activeOpenVinoOptimizationService;
        private ChatPage? _attachedChatPage;
        private ChatDemoController? _chatController;
        private bool _ggufChatRouteRegistered;
        private int _ggufChatLaunchFaultReported;
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private int _disposed;
        private OptimizationPage? _attachedOptimizationPage;
        private CompatibilityPage? _compatibilityPageForOptimizationReturn;
        private OptimizationJourneyCoordinator? _optimizationCoordinator;
        private IOptimizationAttemptContextFactory? _optimizationContextFactory;
        private ModelInspectionPage? _modelInspectionPageForHardwareReturn;
        private Guid _activeModelHandoffId;
        private Guid _activeProductHardwareRunId;
        private ModelInspectionHandoff? _pendingHardwareRetryHandoff;
        private bool _isHardwareNavigationTransaction;
        private OnboardingStage _currentStage;

        internal Task CurrentNavigationTask { get; private set; } =
            Task.CompletedTask;

        internal ModelInspectionPage? ActiveInspectionPageForTesting =>
            _attachedModelInspectionPage;

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
        public OnboardingStage CurrentStage
        {
            get => _currentStage;
            private set
            {
                _currentStage = value;
                if (StageIndicator is not null)
                {
                    StageIndicator.Visibility = value == OnboardingStage.ReadyToChat
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
        }

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
            _attachedModelImportPage.VerifiedDownloadInspectionReady +=
                ModelImportPage_VerifiedDownloadInspectionReady;
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

        internal Task<bool> NavigateToOpenVinoInspectionAsync(
            OpenVinoInspectionRequestedEventArgs request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (_attachedModelImportPage is not { } importPage ||
                !importPage.TryGetAcceptedFolderLocalPath(
                    request.OperationId,
                    out string? directoryPath) ||
                string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(false);
            }

            return NavigateToOpenVinoInspectionDirectoryAsync(
                importPage,
                request,
                directoryPath);
        }

        private async Task<bool> NavigateToOpenVinoInspectionDirectoryAsync(
            ModelImportPage importPage,
            OpenVinoInspectionRequestedEventArgs request,
            string directoryPath)
        {
            await importPage.RetireForNavigationAsync();
            if (!ReferenceEquals(importPage, _attachedModelImportPage))
            {
                return false;
            }

            object? previousContent = StageFrame.Content;
            ModelInspectionPage modelInspectionPage = new();
            modelInspectionPage.ActivateOpenVinoInspection(request, directoryPath);
            StageFrame.Content = modelInspectionPage;
            if (ReferenceEquals(StageFrame.Content, previousContent) ||
                !ReferenceEquals(StageFrame.Content, modelInspectionPage))
            {
                return false;
            }

            StageFrame.BackStack.Clear();
            AttachModelInspectionPage(modelInspectionPage);
            DetachModelImportPage();
            CurrentStage = OnboardingStage.InspectModel;
            StageIndicator.CurrentStage = CurrentStage;
            request.AcceptNavigation();
            return true;
        }

        internal Task<bool> ReturnToModelImportAsync()
        {
            Task<bool> navigation = ReturnToModelImportCoreAsync();
            CurrentNavigationTask = navigation;
            return navigation;
        }

        private async Task<bool> ReturnToModelImportCoreAsync()
        {
            if (_attachedModelInspectionPage is { } inspectionPage)
            {
                await inspectionPage.RetireForNavigationAsync();
            }

            return NavigateToFreshModelImport();
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

        private async void ModelImportPage_OpenVinoInspectionRequested(
            object? sender,
            OpenVinoInspectionRequestedEventArgs eventArguments)
        {
            if (sender is ModelImportPage page &&
                ReferenceEquals(page, _attachedModelImportPage))
            {
                try
                {
                    await NavigateToOpenVinoInspectionAsync(eventArguments);
                }
                catch (OperationCanceledException)
                {
                    // Navigation retirement won the race.
                }
            }
        }

        private async void ModelImportPage_SourceModelConversionRequested(
            object? sender,
            SourceModelConversionRequestedEventArgs eventArguments)
        {
            if (sender is not ModelImportPage page ||
                !ReferenceEquals(page, _attachedModelImportPage))
            {
                return;
            }

            SourceModelConversionRequested?.Invoke(this, eventArguments);
            if (!page.TryGetAcceptedFolderOperation(
                    eventArguments.Selection.OperationId,
                    out string? sourceDirectory,
                    out CancellationToken selectionCancellationToken)
                || string.IsNullOrWhiteSpace(sourceDirectory))
            {
                page.RejectFolderRoute(
                    eventArguments.Selection.OperationId,
                    "conversion-source-unavailable",
                    "The selected model source is no longer available. Choose it again.");
                return;
            }

            try
            {
                using CancellationTokenSource conversionCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        selectionCancellationToken,
                        _lifetimeCancellation.Token);
                await ConvertAndInspectAsync(
                    page,
                    eventArguments,
                    sourceDirectory,
                    conversionCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Shell or selection retirement owns cancellation.
            }
            catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or InvalidOperationException
                                               or PlatformNotSupportedException)
            {
                if (ReferenceEquals(page, _attachedModelImportPage)
                    && page.IsCurrentSelectionOperation(
                        eventArguments.Selection.OperationId))
                {
                    page.RejectFolderRoute(
                        eventArguments.Selection.OperationId,
                        "conversion-unavailable",
                        "This model could not be converted on this computer. Check the local runtime, then try again.");
                }
            }
        }

        private async Task ConvertAndInspectAsync(
            ModelImportPage page,
            SourceModelConversionRequestedEventArgs eventArguments,
            string sourceDirectory,
            CancellationToken cancellationToken)
        {
            OpenVinoRouteService routeService =
                ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService();
            OpenVinoRouteInspectionResult inspection =
                await routeService.InspectAsync(
                    sourceDirectory,
                    cancellationToken);
            using OpenVinoConversionOffer? offer = inspection.ConversionOffer;
            if (offer is null)
            {
                page.RejectFolderRoute(
                    eventArguments.Selection.OperationId,
                    "conversion-source-invalid",
                    "The selected source model is incomplete or changed. Choose it again.");
                return;
            }

            OpenVinoConversionService conversionService =
                ModelInspectionServiceComposition.CreateDefaultOpenVinoConversionService(
                    routeService);
            OpenVinoConversionResult result = await conversionService.ConvertAsync(
                offer,
                confirmed: true,
                progress: null,
                cancellationToken);
            if (result.Status != OpenVinoConversionStatus.Published
                || string.IsNullOrWhiteSpace(result.PublishedDirectory)
                || !ReferenceEquals(page, _attachedModelImportPage)
                || !page.IsCurrentSelectionOperation(
                    eventArguments.Selection.OperationId))
            {
                if (ReferenceEquals(page, _attachedModelImportPage)
                    && page.IsCurrentSelectionOperation(
                        eventArguments.Selection.OperationId))
                {
                    page.RejectFolderRoute(
                        eventArguments.Selection.OperationId,
                        "conversion-failed",
                        "The selected model was not converted. Review local storage and try again.");
                }
                return;
            }

            var request = new OpenVinoInspectionRequestedEventArgs(
                eventArguments.Selection.OperationId,
                eventArguments.Selection.DisplayName);
            await NavigateToOpenVinoInspectionDirectoryAsync(
                page,
                request,
                result.PublishedDirectory);
        }

        private void ModelImportPage_VerifiedDownloadInspectionReady(
            object? sender,
            VerifiedDownloadInspectionReadyEventArgs eventArguments)
        {
            if (sender is not ModelImportPage page
                || !ReferenceEquals(page, _attachedModelImportPage))
            {
                return;
            }

            CurrentNavigationTask =
                HandleVerifiedDownloadInspectionReadyAsync(page, eventArguments);
        }

        private async Task HandleVerifiedDownloadInspectionReadyAsync(
            ModelImportPage page,
            VerifiedDownloadInspectionReadyEventArgs eventArguments)
        {
            if (!page.TryClaimVerifiedDownloadInspection(
                    eventArguments.OperationId,
                    out ModelInspectionRequest? request)
                || request is null)
            {
                return;
            }

            bool installed = false;
            try
            {
                await page.RetireForNavigationAsync();
                if (!ReferenceEquals(page, _attachedModelImportPage))
                {
                    return;
                }

                installed = NavigateToModelInspection(request);
            }
            catch (Exception exception)
            {
                Trace.TraceWarning(
                    "Verified-download navigation observed {0}.",
                    exception.GetType().Name);
            }

            if (installed || !ReferenceEquals(page, _attachedModelImportPage))
            {
                return;
            }

            try
            {
                if (!NavigateToFreshModelImport())
                {
                    Trace.TraceWarning(
                        "Verified-download navigation recovery could not install a fresh import page.");
                }
            }
            catch (Exception exception)
            {
                Trace.TraceWarning(
                    "Verified-download navigation recovery observed {0}.",
                    exception.GetType().Name);
            }
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
            if (!TryRegisterModelSourceCustody(sourcePage, handoff))
            {
                InvalidateModelHandoff(handoff.ModelInspectionHandoffId);
                return false;
            }

            Guid hardwareRunId = Guid.NewGuid();
            if (!_handoffRegistry.TryBindToHardwareRun(
                handoff,
                handoff.ModelInspectionRunId,
                hardwareRunId,
                out ModelInspectionHandoffClaim claim))
            {
                InvalidateModelHandoff(handoff.ModelInspectionHandoffId);
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
                InvalidateModelHandoff(handoff.ModelInspectionHandoffId);
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
                    != ModelInspectionHandoffLifecycleState.BoundToHardwareRun)
            {
                return false;
            }

            ModelInspectionExecutionResult? terminal =
                _modelEvidenceResolver(modelPage, modelHandoff);
            PreparedGgufCompatibilityInput? preparedGguf = null;
            PreparedOpenVinoCompatibilityInput? preparedOpenVino = null;
            bool hasGguf = terminal is not null &&
                GgufCompatibilityInputProjector.TryPrepare(
                    modelHandoff,
                    terminal,
                    _activeProductHardwareRunId,
                    eventArguments.Handoff,
                    out preparedGguf);
            bool hasOpenVino = !hasGguf &&
                modelPage.TryGetOpenVinoCompatibilityEvidence(
                    modelHandoff,
                    out OpenVinoStaticPackageEvidence? openVinoEvidence) &&
                OpenVinoCompatibilityInputProjector.TryPrepare(
                    modelHandoff,
                    openVinoEvidence!,
                    _activeProductHardwareRunId,
                    eventArguments.Handoff,
                    out preparedOpenVino);
            if (!hasGguf && !hasOpenVino)
            {
                return false;
            }

            var evaluator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
                _compatibilityFreshResourcesSource,
                TimeProvider.System);
            CompatibilityPage compatibilityPage;
            if (hasGguf && GgufOptimizationProductionAuthority.TryCreate(
                    preparedGguf!,
                    out GgufOptimizationProductionAuthority? production))
            {
                _activeGgufAuthority = production;
                _activeOpenVinoAuthority = null;
                _activeOpenVinoOptimizationService = null;
                EnsureGgufChatRoute(production!);
                compatibilityPage = new CompatibilityPage(
                    (optedInEvidence, token) =>
                        evaluator.EvaluateAuthorityAsync(
                            optedInEvidence,
                            production!.Evaluate,
                            token),
                    production!,
                    evaluation => production!.ResolveCurrentModel(
                        evaluation,
                        CurrentModelChatLaunchRegistry),
                    continueDestinationAvailable: true);
            }
            else if (hasOpenVino
                && modelPage.TryCreateOpenVinoOptimizationService(
                    out OpenVinoOptimizationService? openVinoService,
                    out GraniteEdgeAI.OpenVino.Contracts.OpenVinoBuildEvidence?
                        openVinoBuilds)
                && OpenVinoOptimizationProductionAuthority.TryCreate(
                    preparedOpenVino!, openVinoBuilds!,
                    out OpenVinoOptimizationProductionAuthority? openVinoProduction))
            {
                _activeGgufAuthority = null;
                _activeOpenVinoAuthority = openVinoProduction;
                _activeOpenVinoOptimizationService = openVinoService;
                compatibilityPage = new CompatibilityPage(
                    (optedInEvidence, token) =>
                        evaluator.EvaluateAuthorityAsync(
                            optedInEvidence,
                            openVinoProduction!.Evaluate,
                            token),
                    openVinoProduction!,
                    evaluation => openVinoProduction!.ResolveCurrentModel(
                        evaluation, CurrentModelChatLaunchRegistry),
                    continueDestinationAvailable: true);
            }
            else
            {
                _activeGgufAuthority = null;
                _activeOpenVinoAuthority = null;
                _activeOpenVinoOptimizationService = null;
                compatibilityPage = new CompatibilityPage(
                    token => evaluator.EvaluateBoundAsync(
                        (CompatibilityFreshResourcesInput fresh,
                            out CompatibilityProductionInput? input) =>
                            hasGguf
                                ? preparedGguf!.TryBindFresh(fresh, out input)
                                : preparedOpenVino!.TryBindFresh(fresh, out input),
                        token),
                    continueDestinationAvailable: false);
            }
            compatibilityPage.BackRequested += CompatibilityPage_BackRequested;
            compatibilityPage.ContinueRequested += CompatibilityPage_ContinueRequested;
            compatibilityPage.OptimizationRequested +=
                CompatibilityPage_OptimizationRequested;
            compatibilityPage.CurrentModelChatRequested +=
                CompatibilityPage_CurrentModelChatRequested;

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
                compatibilityPage.OptimizationRequested -=
                    CompatibilityPage_OptimizationRequested;
                compatibilityPage.CurrentModelChatRequested -=
                    CompatibilityPage_CurrentModelChatRequested;
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
            // The typed OptimizationRequested or CurrentModelChatRequested
            // event performs the transition. This compatibility event remains
            // subscribed for the existing page contract and intentionally does
            // not duplicate navigation.
        }

        private void CompatibilityPage_OptimizationRequested(
            object? sender,
            OptimizationRequestedEventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedCompatibilityPage))
            {
                return;
            }
            NavigateToOptimization(eventArguments.Context);
        }

        private void NavigateToOptimization(
            OptimizationJourneyEntryContext entry)
        {
            if (_attachedCompatibilityPage is not { } compatibilityPage)
            {
                return;
            }

            string appRoot = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "GraniteEdgeAI",
                "Optimization");
            var factory = A1BackendProductionAuthorities.Shared.CreateOptimization(
                _modelSourceCustodyRegistry,
                appRoot,
                _activeGgufAuthority,
                _activeOpenVinoAuthority,
                _activeOpenVinoOptimizationService,
                TimeProvider.System);
            if (!factory.TryCreate(
                    entry,
                    out OptimizationBackendComposition? backend) ||
                backend is null)
            {
                return;
            }

            OptimizationJourneyCoordinator coordinator = backend.Coordinator;
            IOptimizationAttemptContextFactory contextFactory =
                backend.ContextFactory;
            OptimizationOutputRegistry outputs = backend.Outputs;
            OptimizationDestinationFacade destinations =
                CreateOptimizationDestinationFacade(
                    entry.OptimizationHandoff.Plan,
                    backend.Executor,
                    outputs,
                    appRoot);
            BeginOptimizationDestinationLifecycle(destinations);
            var page = new OptimizationPage(entry);
            page.IntentRequested += OptimizationPage_IntentRequested;
            coordinator.StateChanged += OptimizationCoordinator_StateChanged;

            _compatibilityPageForOptimizationReturn = compatibilityPage;
            _attachedOptimizationPage = page;
            _optimizationCoordinator = coordinator;
            _optimizationContextFactory = contextFactory;
            StageFrame.Content = page;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            CurrentStage = OnboardingStage.ConfigureModel;
            StageIndicator.CurrentStage = CurrentStage;
        }

        private void OptimizationCoordinator_StateChanged(
            object? sender,
            OptimizationJourneyState state)
        {
            if (!ReferenceEquals(sender, _optimizationCoordinator))
            {
                return;
            }
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (ReferenceEquals(sender, _optimizationCoordinator))
                {
                    ApplyOptimizationState(state);
                }
            });
        }

        private void ApplyOptimizationState(OptimizationJourneyState state)
        {
            if (_attachedOptimizationPage is not { } page)
            {
                return;
            }
            var plan = state.Entry.OptimizationHandoff.Plan;
            OptimizationConfigurationPresentation configuration =
                OptimizationConfigurationProjection.From(plan);
            OptimizationPreferenceSelection preference = plan.Preference;
            OptimizationPresentationState presentation = state.Kind switch
            {
                OptimizationJourneyKind.Confirmation =>
                    OptimizationPresentationFactory.Confirmation(
                        preference,
                        configuration,
                        plan.OptimizationPlanId,
                        plan.ConfigurationSha256),
                OptimizationJourneyKind.Running or
                    OptimizationJourneyKind.Cancelling =>
                    OptimizationPresentationFactory.Running(
                        preference,
                        configuration,
                        MapStage(state.Stage),
                        state.Kind != OptimizationJourneyKind.Cancelling),
                OptimizationJourneyKind.Cancelled =>
                    OptimizationPresentationFactory.Cancelled(
                        preference,
                        configuration,
                        state.Entry.Origin),
                OptimizationJourneyKind.ReplanRequired =>
                    OptimizationPresentationFactory.ReplanRequired(
                        preference,
                        configuration),
                OptimizationJourneyKind.Failed =>
                    OptimizationPresentationFactory.Failed(
                        preference,
                        configuration,
                        state.Entry.Origin),
                OptimizationJourneyKind.SucceededPersistent or
                    OptimizationJourneyKind.SucceededRuntimeProfile =>
                    OptimizationPresentationFactory.Success(
                        preference,
                        configuration),
                _ => throw new ArgumentOutOfRangeException(nameof(state)),
            };
            page.ApplyPresentation(presentation);
        }

        private async void OptimizationPage_IntentRequested(
            object? sender,
            OptimizationIntentEventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedOptimizationPage)
                || _optimizationCoordinator is not { } coordinator)
            {
                return;
            }
            switch (eventArguments.Command)
            {
                case OptimizationCommand.Confirm:
                    await coordinator.ConfirmAsync();
                    break;
                case OptimizationCommand.Cancel:
                    await coordinator.CancelAsync();
                    break;
                case OptimizationCommand.Retry:
                    _ = coordinator.Retry();
                    break;
                case OptimizationCommand.BackToCompatibility:
                    await ReturnFromOptimizationAsync();
                    break;
                case OptimizationCommand.ChatWithOriginal:
                    if (coordinator.State.Entry.CurrentModelFallback is { } fallback)
                    {
                        _ = await CurrentModelChatLaunchRegistry.LaunchAsync(
                            fallback,
                            CancellationToken.None);
                    }
                    break;
                case OptimizationCommand.Chat:
                    await LaunchOptimizedChatAsync(
                        coordinator.State,
                        OptimizationDestinationToken);
                    break;
                case OptimizationCommand.Save:
                    await SaveOptimizedModelAsync(
                        coordinator.State,
                        OptimizationDestinationToken);
                    break;
                case OptimizationCommand.Done:
                    await ReturnFromOptimizationAsync();
                    break;
            }
        }

        private async Task LaunchOptimizedChatAsync(
            OptimizationJourneyState state,
            CancellationToken cancellationToken)
        {
            if (state.Result is not { IsSuccessful: true } result)
            {
                return;
            }

            OptimizationChatTarget? target;
            try
            {
                target = await CreateOptimizationChatTargetAsync(
                    state.Result, cancellationToken);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (target is null)
            {
                return;
            }

            if (target is OpenVinoDestinationChatTarget openVinoTarget)
            {
                if (_modelInspectionPageForHardwareReturn is not
                    { } openVinoPage)
                {
                    RetireActiveOptimizationChatTarget();
                    return;
                }

                bool activated;
                try
                {
                    activated = await openVinoPage.ActivateOpenVinoChatTargetAsync(
                        openVinoTarget.Target, cancellationToken);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    RetireActiveOptimizationChatTarget();
                    return;
                }
                if (activated)
                {
                    await RetireOptimizationAsync();
                    DetachCompatibilityPage();
                    DetachHardwareInspectionPage();
                    StageFrame.Content = openVinoPage;
                    StageFrame.BackStack.Clear();
                    StageFrame.ForwardStack.Clear();
                    AttachModelInspectionPage(openVinoPage);
                    CurrentStage = OnboardingStage.ReadyToChat;
                    StageIndicator.CurrentStage = CurrentStage;
                }
                else
                {
                    RetireActiveOptimizationChatTarget();
                }
                return;
            }

            if (target is not GgufOptimizationChatTarget ggufTarget
                || _activeGgufAuthority is not { } authority)
            {
                RetireActiveOptimizationChatTarget();
                return;
            }

            try
            {
                GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeConfiguration
                    configuration =
                    GgufCurrentModelChatRouteLauncher.CreateConfiguration(
                        $"optimized-{ggufTarget.VerifiedModelSha256[..12]}",
                        ggufTarget.VerifiedModelSha256,
                        ggufTarget.RuntimeOptions);
                var request = new GgufChatLaunchRequest(
                    authority.RuntimePackageRoot,
                    authority.TrustedRuntimeManifest.Span,
                    ggufTarget.VerifiedModelPath,
                    "Optimised model",
                    configuration);
                var page = new ChatPage();
                ChatDemoController? controller;
                try
                {
                    controller = await A1BackendProductionAuthorities.Shared
                        .CreateInitializedChatAsync(
                            page,
                            request,
                            cancellationToken);
                }
                catch (Exception exception)
                {
                    ReportGgufLaunchFault(exception);
                    RetireActiveOptimizationChatTarget();
                    return;
                }

                try
                {
                    await RetireOptimizationAsync();
                    ShowGgufChat(page, controller);
                    controller = null;
                }
                finally
                {
                    if (controller is not null)
                    {
                        await controller.DisposeAsync();
                        RetireActiveOptimizationChatTarget();
                    }
                }
            }
            catch (Exception exception)
            {
                ReportGgufLaunchFault(exception);
                RetireActiveOptimizationChatTarget();
            }
        }

        private void ReportGgufLaunchFault(Exception exception)
        {
            if (!ChatDemoController.TryClassifyOperationalFailure(
                    exception,
                    out _)
                && Interlocked.Exchange(
                    ref _ggufChatLaunchFaultReported,
                    1) == 0)
            {
                BoundedApplicationFaultReporter.Shared.Report(
                    ApplicationFault.FromException(
                        ApplicationFaultCode.GgufChatOperationUnexpected,
                        exception));
            }
        }

        private async Task SaveOptimizedModelAsync(
            OptimizationJourneyState state,
            CancellationToken cancellationToken)
        {
            if (state.Result is not
                    { Status: OptimizationExecutionStatus.SucceededPersistent }
                    result)
            {
                return;
            }

            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            picker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder? selected =
                await picker.PickSingleFolderAsync();
            if (selected is null)
            {
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string destination = Path.Combine(
                selected.Path,
                result.Route == OptimizationRoute.Gguf
                    ? $"optimised-model-{stamp}.gguf"
                    : $"optimised-openvino-model-{stamp}");
            const ulong maximumExportBytes = 1UL << 40;
            const ulong metadataAllowanceBytes = 64UL * 1024 * 1024;
            ulong maximumBytes = result.Route == OptimizationRoute.OpenVino
                && result.OutputSizeBytes
                    <= maximumExportBytes - metadataAllowanceBytes
                    ? result.OutputSizeBytes + metadataAllowanceBytes
                    : maximumExportBytes;
            OptimizationDestinationExportResult exportResult;
            try
            {
                exportResult = await ExportOptimizedModelAsync(
                    state.Result, destination, maximumBytes, cancellationToken);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            switch (exportResult.Disposition)
            {
                case OptimizationDestinationExportDisposition.Succeeded:
                case OptimizationDestinationExportDisposition.RuntimeOnly:
                case OptimizationDestinationExportDisposition.ResultRejected:
                case OptimizationDestinationExportDisposition.DestinationRejected:
                case OptimizationDestinationExportDisposition.DestinationExists:
                case OptimizationDestinationExportDisposition.Oversized:
                case OptimizationDestinationExportDisposition.CleanupFailed:
                case OptimizationDestinationExportDisposition.Failed:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(exportResult), exportResult.Disposition,
                        "The optimization export disposition is not supported.");
            }
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);
            foreach (string directory in Directory.EnumerateDirectories(
                sourceRoot, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(
                    destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
            }
            foreach (string file in Directory.EnumerateFiles(
                sourceRoot, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(
                    destinationRoot, Path.GetRelativePath(sourceRoot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: false);
            }
        }

        private async Task RetireOptimizationAsync()
        {
            await RetireOptimizationDestinationLifecycleAsync();
            if (_attachedOptimizationPage is { } page)
            {
                page.IntentRequested -= OptimizationPage_IntentRequested;
            }
            if (_optimizationCoordinator is { } coordinator)
            {
                coordinator.StateChanged -= OptimizationCoordinator_StateChanged;
                await coordinator.DisposeAsync();
            }
            _attachedOptimizationPage = null;
            _optimizationCoordinator = null;
            _optimizationContextFactory = null;
        }

        private async Task ReturnFromOptimizationAsync()
        {
            await RetireOptimizationAsync();
            if (_compatibilityPageForOptimizationReturn is { } compatibility)
            {
                StageFrame.Content = compatibility;
                StageFrame.BackStack.Clear();
                StageFrame.ForwardStack.Clear();
                CurrentStage = OnboardingStage.CheckHardwareFit;
                StageIndicator.CurrentStage = CurrentStage;
            }
        }

        private static OptimizationStage MapStage(
            OptimizationProgressStage? stage) => stage switch
        {
            OptimizationProgressStage.Preflight =>
                OptimizationStage.Preflight,
            OptimizationProgressStage.PrepareStaging =>
                OptimizationStage.PrepareStaging,
            OptimizationProgressStage.Optimise =>
                OptimizationStage.Optimise,
            OptimizationProgressStage.Validate =>
                OptimizationStage.Validate,
            OptimizationProgressStage.SmokeTest =>
                OptimizationStage.SmokeTest,
            OptimizationProgressStage.Reinspect =>
                OptimizationStage.Reinspect,
            OptimizationProgressStage.Publish =>
                OptimizationStage.Publish,
            _ => OptimizationStage.Preflight,
        };

        private async void CompatibilityPage_CurrentModelChatRequested(
            object? sender,
            CurrentModelChatRequestedEventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedCompatibilityPage))
            {
                return;
            }
            if (eventArguments.Handoff.Route == OptimizationRoute.OpenVino
                && _modelInspectionPageForHardwareReturn is { } openVinoPage
                && await openVinoPage.ActivateOpenVinoChatAsync(
                    CancellationToken.None))
            {
                DetachCompatibilityPage();
                DetachHardwareInspectionPage();
                StageFrame.Content = openVinoPage;
                StageFrame.BackStack.Clear();
                StageFrame.ForwardStack.Clear();
                AttachModelInspectionPage(openVinoPage);
                CurrentStage = OnboardingStage.ReadyToChat;
                StageIndicator.CurrentStage = CurrentStage;
                return;
            }
            _ = await CurrentModelChatLaunchRegistry.LaunchAsync(
                eventArguments.Handoff,
                CancellationToken.None);
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
                if (replacement is null
                    || !TryRegisterModelSourceCustody(modelPage, replacement))
                {
                    return false;
                }

                if (!_handoffRegistry.TryAcceptReissue(
                    _activeModelHandoffId,
                    replacement))
                {
                    _modelSourceCustodyRegistry.Retire(
                        replacement.ModelInspectionHandoffId);
                    return false;
                }

                _modelSourceCustodyRegistry.Retire(_activeModelHandoffId);
                _currentModelChatLaunchRegistry?.Retire(_activeModelHandoffId);

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
                InvalidateModelHandoff(replacement.ModelInspectionHandoffId);
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
                InvalidateModelHandoff(_activeModelHandoffId);
            }

            if (_pendingHardwareRetryHandoff is not null)
            {
                InvalidateModelHandoff(
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
            RetireActiveOptimizationChatTarget();
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
            _attachedModelImportPage.VerifiedDownloadInspectionReady -=
                ModelImportPage_VerifiedDownloadInspectionReady;

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
            _attachedCompatibilityPage.OptimizationRequested -=
                CompatibilityPage_OptimizationRequested;
            _attachedCompatibilityPage.CurrentModelChatRequested -=
                CompatibilityPage_CurrentModelChatRequested;
            _attachedCompatibilityPage = null;
        }

        private void EnsureGgufChatRoute(
            GgufOptimizationProductionAuthority authority)
        {
            if (_ggufChatRouteRegistered)
            {
                return;
            }
            var accessor = new GgufOptimizationProductionAuthorityAccessor(
                authority.RuntimePackageRoot,
                authority.TrustedRuntimeManifest);
            _ggufChatRouteRegistered = CurrentModelChatLaunchRegistry.RegisterRoute(
                new GgufCurrentModelChatRouteLauncher(accessor, ShowGgufChat));
        }

        private void ShowGgufChat(
            ChatPage page,
            ChatDemoController controller)
        {
            DetachCompatibilityPage();
            _hardwarePageForCompatibilityReturn = null;
            _attachedChatPage = page;
            _chatController = controller;
            page.ImportModelRequested += ChatPage_ImportModelRequested;
            StageFrame.Content = page;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            CurrentStage = OnboardingStage.ReadyToChat;
            StageIndicator.CurrentStage = CurrentStage;
        }

        private async void ChatPage_ImportModelRequested(
            object? sender,
            EventArgs eventArguments) =>
            await RetireChatAndNavigateToImportAsync(sender as ChatPage);

        private async Task RetireChatAndNavigateToImportAsync(ChatPage? page)
        {
            if (!ReferenceEquals(page, _attachedChatPage))
            {
                return;
            }
            _attachedChatPage!.ImportModelRequested -= ChatPage_ImportModelRequested;
            _attachedChatPage = null;
            try
            {
                if (_chatController is { } controller)
                {
                    _chatController = null;
                    await controller.DisposeAsync();
                }
            }
            finally
            {
                RetireActiveOptimizationChatTarget();
                NavigateToFreshModelImport();
            }
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
