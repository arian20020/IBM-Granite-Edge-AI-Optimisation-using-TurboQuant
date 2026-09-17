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
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.ChatModels;
using GraniteEdgeAI.Features.GgufRuntime.Presentation;
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
using System.Linq;
using System.Runtime.ExceptionServices;
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
        // currently listening to
        private ModelImportPage? _attachedModelImportPage;

        // Stores the Model Inspection page whose choose-another request the
        // shell is currently listening to
        private ModelInspectionPage? _attachedModelInspectionPage;
        private HardwareInspectionPage? _attachedHardwareInspectionPage;
        private HardwareInspectionPage? _hardwarePageForCompatibilityReturn;
        private CompatibilityPage? _attachedCompatibilityPage;
        private GgufOptimizationProductionAuthority? _activeGgufAuthority;
        private OpenVinoOptimizationProductionAuthority? _activeOpenVinoAuthority;
        private OpenVinoOptimizationService? _activeOpenVinoOptimizationService;
        private ChatPage? _attachedChatPage;
        private ChatDemoController? _chatController;
        private readonly ChatModelLibrary _chatModelLibrary = new();
        private readonly object _chatModelSwitchGate = new();
        private CancellationTokenSource? _chatModelSwitchCancellation;
        private Task _chatModelSwitchTask = Task.CompletedTask;
        private bool _ggufChatRouteRegistered;
        private int _openVinoChatLaunchFaultReported;
        private int _ggufChatLaunchFaultReported;
        private int _optimizationIntentFaultReported;
        private int _optimizationChatOpeningPending;
        private readonly OptimizationChatHandoffGate _optimizationChatHandoff = new();
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
        private long _compatibilityImportNavigationGeneration;

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
        /// creates the onboarding shell and displays the first stage
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

            // record that onboarding starts on the model-import stage
            CurrentStage = OnboardingStage.ImportModel;

            // keep the permanent stage indicator synchronized
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
            // reject a missing page immediately
            ArgumentNullException.ThrowIfNull(modelImportPage);

            // avoid subscribing twice to the same page instance
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
            // handler and therefore cannot cause duplicate reset navigation
            if (ReferenceEquals(
                _attachedModelInspectionPage,
                modelInspectionPage))
            {
                return;
            }

            // A page replaced by successful Frame navigation is no longer
            // allowed to drive the shell
            DetachModelInspectionPage();
            _attachedModelInspectionPage = modelInspectionPage;
            _attachedModelInspectionPage.ChooseAnotherModelRequested +=
                ModelInspectionPage_ChooseAnotherModelRequested;
            _attachedModelInspectionPage.OpenVinoChatView.ImportModelRequested += OpenVinoChat_ImportRequested;
            _attachedModelInspectionPage.OpenVinoChatView.ThemeRequested += OpenVinoChat_ThemeRequested;
            _attachedModelInspectionPage.FooterStatusChanged +=
                ModelInspectionPage_FooterStatusChanged;
            _attachedModelInspectionPage.HardwareInspectionRequested +=
                ModelInspectionPage_HardwareInspectionRequested;
            _attachedModelInspectionPage.SetHardwareRouteAvailable(
                _hardwareInspectionService is not null);

            // Navigation has already completed by the time the Frame hands
            // ownership to the shell, so sample the authoritative status now
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
            // do not retain the import page or its path-bearing state in the
            // Frame journal where Back could resurrect it outside that state.
            StageFrame.BackStack.Clear();

            AttachModelInspectionPage(modelInspectionPage);

            // Model Import is no longer the active page after navigation.
            DetachModelImportPage();

            // record that onboarding has advanced to stage two
            CurrentStage = OnboardingStage.InspectModel;

            // keep the permanent visual indicator synchronized
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
            if (!ReferenceEquals(importPage, _attachedModelImportPage))
            {
                return false;
            }

            object? previousContent = StageFrame.Content;
            ModelInspectionPage modelInspectionPage = new();
            try
            {
                StageFrame.Content = modelInspectionPage;
                if (ReferenceEquals(StageFrame.Content, previousContent) ||
                    !ReferenceEquals(StageFrame.Content, modelInspectionPage))
                {
                    StageFrame.Content = previousContent;
                    return false;
                }

                AttachModelInspectionPage(modelInspectionPage);
                bool activated = await modelInspectionPage
                    .ActivateOpenVinoInspectionWhenOwnedAsync(
                        request,
                        directoryPath,
                        StageFrame);
                if (!activated ||
                    !ReferenceEquals(StageFrame.Content, modelInspectionPage) ||
                    !ReferenceEquals(
                        _attachedModelInspectionPage,
                        modelInspectionPage) ||
                    !ReferenceEquals(importPage, _attachedModelImportPage))
                {
                    try
                    {
                        await modelInspectionPage.RetireOpenVinoInspectionAsync();
                    }
                    catch (Exception)
                    {
                        // Ownership rollback below remains authoritative when
                        // candidate lifetime cleanup has already done its best.
                    }
                    finally
                    {
                        if (ReferenceEquals(
                            _attachedModelInspectionPage,
                            modelInspectionPage))
                        {
                            DetachModelInspectionPage();
                        }
                        if (ReferenceEquals(StageFrame.Content, modelInspectionPage))
                        {
                            StageFrame.Content = previousContent;
                        }
                    }
                    return false;
                }
            }
            catch (Exception)
            {
                try
                {
                    await modelInspectionPage.RetireOpenVinoInspectionAsync();
                }
                catch (Exception)
                {
                    // The activation failure remains authoritative. Always
                    // restore the prior Frame and subscription ownership.
                }
                finally
                {
                    if (ReferenceEquals(
                        _attachedModelInspectionPage,
                        modelInspectionPage))
                    {
                        DetachModelInspectionPage();
                    }
                    if (ReferenceEquals(StageFrame.Content, modelInspectionPage))
                    {
                        StageFrame.Content = previousContent;
                    }
                }
                return false;
            }

            Task retirement = importPage.RetireForNavigationAsync();
            StageFrame.BackStack.Clear();
            DetachModelImportPage();
            CurrentStage = OnboardingStage.InspectModel;
            StageIndicator.CurrentStage = CurrentStage;
            request.AcceptNavigation();
            await retirement;
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

            // fail immediately if the first page could not be displayed
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
            // forward the exact immutable request without reconstructing it
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
            GgufOptimizationProductionAuthority? production = null;
            bool savedProfileRejected = false;
            bool hasGgufProduction = hasGguf && GgufOptimizationProductionAuthority.TryCreate(
                preparedGguf!,
                _modelSourceCustodyRegistry,
                out production,
                out savedProfileRejected);
            if (savedProfileRejected)
            {
                return false;
            }
            CompatibilityPage compatibilityPage;
            if (hasGgufProduction)
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
                && modelPage.TryGetOpenVinoBuildEvidence(
                    out GraniteEdgeAI.OpenVino.Contracts.OpenVinoBuildEvidence?
                        openVinoBuilds)
                && (modelPage.TryCreateOpenVinoOptimizationService(
                        out OpenVinoOptimizationService? openVinoService,
                        out GraniteEdgeAI.OpenVino.Contracts.OpenVinoBuildEvidence?
                            turboQuantBuilds)
                    && openVinoService is not null
                    ? OpenVinoOptimizationProductionAuthority.TryCreate(
                        preparedOpenVino!, openVinoBuilds!, turboQuantBuilds,
                        optimizationAvailable: true,
                        out OpenVinoOptimizationProductionAuthority? openVinoProduction)
                    : OpenVinoOptimizationProductionAuthority.TryCreate(
                        preparedOpenVino!, openVinoBuilds!, turboQuantBuilds: null,
                        optimizationAvailable: false,
                        out openVinoProduction)))
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
            if (sourcePage.TryGetCompletedReport(
                    eventArguments.Handoff,
                    out HardwareInspectionPresentationState? hardwarePresentation,
                    out HardwareSummaryPresentation? hardwareSummary,
                    out HardwareInspectionDetailsState? hardwareDetails))
            {
                compatibilityPage.SetHardwareReport(
                    hardwarePresentation,
                    hardwareSummary,
                    hardwareDetails);
            }
            compatibilityPage.BackRequested += CompatibilityPage_BackRequested;
            compatibilityPage.HardwareRetryRequested += CompatibilityPage_HardwareRetryRequested;
            compatibilityPage.ConfigureStageEntered +=
                CompatibilityPage_ConfigureStageEntered;
            compatibilityPage.ConfigureStageExited +=
                CompatibilityPage_ConfigureStageExited;
            compatibilityPage.ContinueRequested += CompatibilityPage_ContinueRequested;
            compatibilityPage.OptimizationRequested +=
                CompatibilityPage_OptimizationRequested;
            compatibilityPage.CurrentModelChatRequested +=
                CompatibilityPage_CurrentModelChatRequested;
            compatibilityPage.ImportAnotherModelRequested +=
                CompatibilityPage_ImportAnotherModelRequested;

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
                compatibilityPage.HardwareRetryRequested -= CompatibilityPage_HardwareRetryRequested;
                compatibilityPage.ConfigureStageEntered -=
                    CompatibilityPage_ConfigureStageEntered;
                compatibilityPage.ConfigureStageExited -=
                    CompatibilityPage_ConfigureStageExited;
                compatibilityPage.ContinueRequested -= CompatibilityPage_ContinueRequested;
                compatibilityPage.OptimizationRequested -=
                    CompatibilityPage_OptimizationRequested;
                compatibilityPage.CurrentModelChatRequested -=
                    CompatibilityPage_CurrentModelChatRequested;
                compatibilityPage.ImportAnotherModelRequested -=
                    CompatibilityPage_ImportAnotherModelRequested;
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
                || _modelInspectionPageForHardwareReturn is not { } modelPage)
            {
                return;
            }

            DetachCompatibilityPage();
            _hardwarePageForCompatibilityReturn = null;
            InvalidateActiveHardwareJourney();
            _modelInspectionPageForHardwareReturn = null;
            StageFrame.Content = modelPage;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            AttachModelInspectionPage(modelPage);
            modelPage.SetHardwareRouteAvailable(
                _hardwareInspectionService is not null
                && _hardwareHandoffReissuer(modelPage) is not null);
            CurrentStage = OnboardingStage.InspectModel;
            StageIndicator.CurrentStage = CurrentStage;
        }

        private void CompatibilityPage_HardwareRetryRequested(object? sender, EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedCompatibilityPage))
            {
                return;
            }
            if (_hardwarePageForCompatibilityReturn is not { } hardwarePage)
            {
                _attachedCompatibilityPage?.ShowHardwareRestartFailure();
                return;
            }
            AttachHardwareInspectionPage(hardwarePage);
            if (RetryHardwareInspection(hardwarePage))
            {
                DetachCompatibilityPage();
            }
            else
            {
                DetachHardwareInspectionPage();
                _attachedCompatibilityPage?.ShowHardwareRestartFailure();
            }
        }

        private void CompatibilityPage_ContinueRequested(object? sender, EventArgs eventArguments)
        {
            // The typed OptimizationRequested or CurrentModelChatRequested
            // event performs the transition. this compatibility event remains
            // subscribed for the existing page contract and intentionally does
            // not duplicate navigation
        }

        private void CompatibilityPage_ImportAnotherModelRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (sender is not CompatibilityPage sourcePage
                || !ReferenceEquals(sourcePage, _attachedCompatibilityPage))
            {
                return;
            }

            if (!CurrentNavigationTask.IsCompleted)
            {
                return;
            }

            if (!ReferenceEquals(StageFrame.Content, sourcePage)
                || CurrentStage != OnboardingStage.CheckHardwareFit
                || !sourcePage.CanCompleteImportNavigation)
            {
                sourcePage.CancelImportNavigation();
                return;
            }

            long generation = checked(
                Interlocked.Increment(ref _compatibilityImportNavigationGeneration));
            CurrentNavigationTask = ImportAnotherModelAsync(sourcePage, generation);
        }

        private async Task ImportAnotherModelAsync(
            CompatibilityPage sourcePage,
            long generation)
        {
            if (!TryQueueNavigationSettlement(out Task navigationSettlement))
            {
                if (IsCurrentCompatibilityImportAttempt(sourcePage, generation)
                    && ReferenceEquals(StageFrame.Content, sourcePage))
                {
                    sourcePage.CancelImportNavigation();
                }
                return;
            }

            Microsoft.UI.Xaml.Navigation.PageStackEntry[] backStack =
                StageFrame.BackStack.ToArray();
            Microsoft.UI.Xaml.Navigation.PageStackEntry[] forwardStack =
                StageFrame.ForwardStack.ToArray();
            ModelImportPage? importPage = null;
            bool navigated = false;
            try
            {
                navigated = StageFrame.Navigate(typeof(ModelImportPage));
                importPage = StageFrame.Content as ModelImportPage;
            }
            catch
            {
                navigated = false;
            }

            await navigationSettlement;
            if (!IsCurrentCompatibilityImportAttempt(sourcePage, generation))
            {
                return;
            }

            if (!navigated
                || importPage is null
                || !ReferenceEquals(StageFrame.Content, importPage)
                || !sourcePage.CanCompleteImportNavigation)
            {
                RollBackCompatibilityImport(
                    sourcePage, importPage, generation, backStack, forwardStack);
                return;
            }

            try
            {
                importPage.SetPresentationMode(ModelImportPresentationMode.AllSources);
                AttachModelImportPage(importPage);
            }
            catch
            {
                if (ReferenceEquals(_attachedModelImportPage, importPage))
                {
                    DetachModelImportPage();
                }
                RollBackCompatibilityImport(
                    sourcePage, importPage, generation, backStack, forwardStack);
                return;
            }

            if (!IsCurrentCompatibilityImportAttempt(sourcePage, generation)
                || !ReferenceEquals(StageFrame.Content, importPage)
                || !ReferenceEquals(_attachedModelImportPage, importPage))
            {
                return;
            }

            try
            {
                StageFrame.BackStack.Clear();
                StageFrame.ForwardStack.Clear();
                StageIndicator.CurrentStage = OnboardingStage.ImportModel;
                StageIndicator.Visibility = Visibility.Visible;
                _attachedModelInspectionPage?.SetHardwareRouteAvailable(false);
            }
            catch
            {
                RollBackCompatibilityImport(
                    sourcePage, importPage, generation, backStack, forwardStack);
                return;
            }

            // everything above this boundary is reversible while the exact
            // compatibility source still owns the attempt. the remaining
            // cleanup is synchronous local subscription and authority
            // retirement only; all Frame and UI work has already completed.
            _hardwarePageForCompatibilityReturn = null;
            InvalidateActiveHardwareJourney();
            DetachHardwareInspectionPage();
            DetachModelInspectionPage(updateHardwareRouteAvailability: false);
            _modelInspectionPageForHardwareReturn = null;

            // Invalidate the attempt only after every fallible commit step has
            // completed, so no rollback depends on a detached source page
            DetachCompatibilityPage();
            _currentStage = OnboardingStage.ImportModel;
        }

        private bool TryQueueNavigationSettlement(out Task settlementTask)
        {
            var settlement = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!DispatcherQueue.TryEnqueue(() => settlement.TrySetResult()))
            {
                settlementTask = Task.CompletedTask;
                return false;
            }

            settlementTask = settlement.Task;
            return true;
        }

        private bool IsCurrentCompatibilityImportAttempt(
            CompatibilityPage sourcePage,
            long generation) =>
            Volatile.Read(ref _disposed) == 0
            && generation == Volatile.Read(
                ref _compatibilityImportNavigationGeneration)
            && ReferenceEquals(sourcePage, _attachedCompatibilityPage)
            && CurrentStage == OnboardingStage.CheckHardwareFit;

        private void RollBackCompatibilityImport(
            CompatibilityPage sourcePage,
            ModelImportPage? importPage,
            long generation,
            IReadOnlyList<Microsoft.UI.Xaml.Navigation.PageStackEntry> backStack,
            IReadOnlyList<Microsoft.UI.Xaml.Navigation.PageStackEntry> forwardStack)
        {
            if (!IsCurrentCompatibilityImportAttempt(sourcePage, generation)
                || !ReferenceEquals(StageFrame.Content, sourcePage)
                    && !ReferenceEquals(StageFrame.Content, importPage))
            {
                return;
            }

            try
            {
                if (importPage is not null
                    && ReferenceEquals(_attachedModelImportPage, importPage))
                {
                    DetachModelImportPage();
                }
                StageFrame.Content = sourcePage;
                RestoreNavigationJournal(StageFrame.BackStack, backStack);
                RestoreNavigationJournal(StageFrame.ForwardStack, forwardStack);
                StageIndicator.CurrentStage = CurrentStage;
                StageIndicator.Visibility = CurrentStage == OnboardingStage.ReadyToChat
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            catch
            {
                // the async event boundary owns navigation failures. a later
                // journey must never observe an exception from this attempt
            }
            finally
            {
                if (IsCurrentCompatibilityImportAttempt(sourcePage, generation))
                {
                    try
                    {
                        sourcePage.CancelImportNavigation();
                    }
                    catch
                    {
                        // preserve the original settled navigation outcome
                    }
                }
            }
        }

        private static void RestoreNavigationJournal(
            System.Collections.Generic.IList<Microsoft.UI.Xaml.Navigation.PageStackEntry> journal,
            IReadOnlyList<Microsoft.UI.Xaml.Navigation.PageStackEntry> snapshot)
        {
            journal.Clear();
            foreach (Microsoft.UI.Xaml.Navigation.PageStackEntry entry in snapshot)
            {
                journal.Add(entry);
            }
        }

        private void CompatibilityPage_ConfigureStageEntered(
            object? sender,
            EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedCompatibilityPage)
                || !ReferenceEquals(StageFrame.Content, sender)
                || CurrentStage != OnboardingStage.CheckHardwareFit)
            {
                return;
            }

            CurrentStage = OnboardingStage.ConfigureModel;
            StageIndicator.CurrentStage = CurrentStage;
        }

        private void CompatibilityPage_ConfigureStageExited(
            object? sender,
            EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedCompatibilityPage)
                || !ReferenceEquals(StageFrame.Content, sender)
                || CurrentStage != OnboardingStage.ConfigureModel)
            {
                return;
            }

            CurrentStage = OnboardingStage.CheckHardwareFit;
            StageIndicator.CurrentStage = CurrentStage;
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
                        configuration,
                        plan.Route),
                OptimizationJourneyKind.Failed =>
                    OptimizationPresentationFactory.Failed(
                        preference,
                        configuration,
                        state.Entry.Origin,
                        state.Result?.SupportCode ?? OptimizationSupportCode.UnexpectedFailure,
                        state.Result?.DiskSpaceRequirement),
                OptimizationJourneyKind.SucceededPersistent or
                    OptimizationJourneyKind.SucceededRuntimeProfile =>
                    OptimizationPresentationFactory.Success(
                        preference,
                        configuration,
                        plan.OptimizationPlanId,
                        plan.ConfigurationSha256,
                        plan.Route),
                _ => throw new ArgumentOutOfRangeException(nameof(state)),
            };
            page.ApplyPresentation(presentation);
            if (state.Result is
                { Status: OptimizationExecutionStatus.SucceededPersistent }
                    result)
            {
                var target =
                    VerifiedPersistentExportTarget.FromExecutionResult(result);
                var exportService = new OptimizationDestinationExportService(
                    result,
                    (route, cancellationToken) =>
                        PickOptimizationExportDestinationAsync(
                            route, plan, result, cancellationToken),
                    ExportOptimizedModelAsync);
                _ = page.BindVerifiedExport(target, exportService);
            }
            else if (state.Result is
                {
                    Status: OptimizationExecutionStatus.SucceededRuntimeProfile,
                    Route: OptimizationRoute.Gguf,
                    ProducedPersistentArtifact: false
                } runtimeResult
                && plan.Route == OptimizationRoute.Gguf)
            {
                try
                {
                    VerifiedGgufRuntimeBundleExportTarget runtimeTarget =
                        VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, runtimeResult);
                    GgufRuntimeProfileBundleExportService runtimeExportService =
                        CreateGgufRuntimeProfileBundleExportService(
                            plan,
                            runtimeResult,
                            (route, cancellationToken) =>
                                PickGgufRuntimeBundleExportDestinationAsync(
                                    route,
                                    plan,
                                    runtimeResult,
                                    cancellationToken));
                    _ = page.BindVerifiedRuntimeBundleExport(runtimeTarget, runtimeExportService);
                }
                catch (Exception exception) when (exception is ArgumentException
                    or InvalidOperationException)
                {
                    Trace.TraceWarning(
                        "The GGUF runtime-bundle export binding was rejected: {0}.",
                        exception.GetType().Name);
                }
            }
        }

        private async Task<string?> PickGgufRuntimeBundleExportDestinationAsync(
            OptimizationRoute route,
            OptimizationExecutionPlan plan,
            OptimizationExecutionResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (route != OptimizationRoute.Gguf
                || plan.Route != route
                || result.Route != route)
            {
                return null;
            }

            try
            {
                VerifiedGgufRuntimeBundleExportTarget target =
                    VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                        plan, result);
                if (!target.Matches(plan, result))
                {
                    return null;
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException)
            {
                return null;
            }

            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            picker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder? selected =
                await picker.PickSingleFolderAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (selected is null)
            {
                return null;
            }

            return GgufRuntimeProfileBundleExportService.TryCreateAbsentDestination(
                selected.Path, plan, result, out string? destination)
                    ? destination
                    : null;
        }

        private async Task<string?> PickOptimizationExportDestinationAsync(
            OptimizationRoute route,
            OptimizationExecutionPlan plan,
            OptimizationExecutionResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OptimizationExportDestinationNamePolicy.MatchesIssuedResult(
                    route, plan, result))
            {
                return null;
            }

            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            picker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder? selected =
                await picker.PickSingleFolderAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (selected is null)
            {
                return null;
            }

            string sourceModelName =
                _modelInspectionPageForHardwareReturn?.Request?.FileName
                ?? string.Empty;
            return route == OptimizationRoute.Gguf
                ? OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
                    selected.Path, route, plan, result, sourceModelName)
                : OptimizationExportDestinationNamePolicy.CreateAbsentDestination(
                    selected.Path, route, plan, result);
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

            try
            {
                await HandleOptimizationIntentAsync(
                    coordinator, eventArguments.Command);
            }
            catch (OperationCanceledException)
            {
                // Ordered shell retirement owns this cancellation.
            }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(
                    ref _optimizationIntentFaultReported, 1) == 0)
                {
                    BoundedApplicationFaultReporter.Shared.Report(
                        ApplicationFault.FromException(
                            ApplicationFaultCode.OptimizationOperationUnexpected,
                            exception));
                }
            }
        }

        private async Task HandleOptimizationIntentAsync(
            OptimizationJourneyCoordinator coordinator,
            OptimizationCommand command)
        {
            switch (command)
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
                    await ReturnFromOptimizationAsync(coordinator);
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
                    await LaunchOptimizedChatAsync(coordinator);
                    break;
                case OptimizationCommand.ImportAnotherModel:
                    await ImportAnotherModelAsync(coordinator);
                    break;
                case OptimizationCommand.Save:
                    await SaveOptimizedModelAsync(coordinator.State);
                    break;
                case OptimizationCommand.Done:
                    await ReturnFromOptimizationAsync(coordinator);
                    break;
            }
        }

        private async Task ImportAnotherModelAsync(
            OptimizationJourneyCoordinator coordinator)
        {
            OptimizationPage? sourcePage = _attachedOptimizationPage;
            if (sourcePage is null || !sourcePage.CanCompleteImportNavigation)
            {
                sourcePage?.CancelImportNavigation();
                return;
            }

            OptimizationChatHandoffLease? handoff;
            try
            {
                handoff = await _optimizationChatHandoff.TryEnterAsync(
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                sourcePage.CancelImportNavigation();
                return;
            }
            if (handoff is null)
            {
                sourcePage.CancelImportNavigation();
                return;
            }

            using (handoff)
            {
                if (!ReferenceEquals(_optimizationCoordinator, coordinator)
                    || !ReferenceEquals(_attachedOptimizationPage, sourcePage)
                    || !ReferenceEquals(StageFrame.Content, sourcePage)
                    || CurrentStage != OnboardingStage.ConfigureModel
                    || (coordinator.State.Result is not { IsSuccessful: true }
                        && !(coordinator.State.Kind == OptimizationJourneyKind.ReplanRequired
                            && coordinator.State.Entry.OptimizationHandoff.Plan.Route == OptimizationRoute.OpenVino))
                    || !sourcePage.CanCompleteImportNavigation)
                {
                    sourcePage.CancelImportNavigation();
                    return;
                }

                object? previousContent = StageFrame.Content;
                bool navigationSucceeded;
                try
                {
                    navigationSucceeded = StageFrame.Navigate(typeof(ModelImportPage));
                }
                catch
                {
                    sourcePage.CancelImportNavigation();
                    throw;
                }
                if (!navigationSucceeded
                    || ReferenceEquals(StageFrame.Content, previousContent)
                    || StageFrame.Content is not ModelImportPage importPage)
                {
                    sourcePage.CancelImportNavigation();
                    return;
                }

                importPage.SetPresentationMode(ModelImportPresentationMode.AllSources);
                AttachModelImportPage(importPage);

                Exception? retirementFailure = null;
                try
                {
                    await RetireOptimizationAsync();
                }
                catch (Exception exception)
                {
                    retirementFailure = exception;
                }
                DetachCompatibilityPage();
                _compatibilityPageForOptimizationReturn = null;
                DetachModelInspectionPage();
                RetireActiveOptimizationChatTarget();
                InvalidateActiveHardwareJourney();
                DetachHardwareInspectionPage();
                _modelInspectionPageForHardwareReturn = null;

                StageFrame.BackStack.Clear();
                StageFrame.ForwardStack.Clear();
                CurrentStage = OnboardingStage.ImportModel;
                StageIndicator.CurrentStage = CurrentStage;
                if (retirementFailure is not null)
                {
                    ExceptionDispatchInfo.Capture(retirementFailure).Throw();
                }
            }
        }

        private async Task LaunchOptimizedChatAsync(
            OptimizationJourneyCoordinator coordinator)
        {
            if (!ReferenceEquals(_optimizationCoordinator, coordinator)
                || _attachedOptimizationPage is not { } openingPage
                || Interlocked.CompareExchange(
                    ref _optimizationChatOpeningPending, 1, 0) != 0)
            {
                return;
            }

            Button? openingButton =
                openingPage.FindName("BtnOptimizationPrimary") as Button;
            object? previousLabel = openingButton?.Content;
            bool previousEnabled = openingButton?.IsEnabled == true;
            string previousAccessibleName = openingButton is null
                ? string.Empty
                : Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(
                    openingButton);
            string previousHelpText = openingButton is null
                ? string.Empty
                : Microsoft.UI.Xaml.Automation.AutomationProperties.GetHelpText(
                    openingButton);

            void RestoreOpeningAction(bool failed)
            {
                if (openingButton is null
                    || !ReferenceEquals(_attachedOptimizationPage, openingPage))
                {
                    return;
                }

                openingButton.Content = failed
                    ? "Chat couldn't load — try again" : previousLabel;
                openingButton.IsEnabled = previousEnabled;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                    openingButton,
                    failed ? "Chat couldn't load. Try again"
                        : previousAccessibleName);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(
                    openingButton,
                    failed
                        ? "The chat did not open. Activate this button to try again."
                        : previousHelpText);
                if (!failed) return;
                if (openingButton.IsEnabled)
                {
                    openingButton.Focus(FocusState.Programmatic);
                }
                var failurePeer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(openingButton)
                    ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(openingButton);
                failurePeer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.Other,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    "Chat couldn't load. Try again",
                    "GraniteOptimizedChatFailure");
            }

            if (openingButton is not null)
            {
                openingButton.Content = "Opening chat…";
                openingButton.IsEnabled = false;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                    openingButton, "Opening chat");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(
                    openingButton, "Preparing the verified optimized model for chat.");
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(openingButton)
                    ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(openingButton);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.Other,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    "Opening chat", "GraniteChatOpening");
            }

            await Task.Yield();
            OptimizationChatHandoffLease? handoff;
            try
            {
                handoff = await _optimizationChatHandoff.TryEnterAsync(
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                RestoreOpeningAction(failed: false);
                Interlocked.Exchange(ref _optimizationChatOpeningPending, 0);
                return;
            }
            catch
            {
                RestoreOpeningAction(failed: true);
                Interlocked.Exchange(ref _optimizationChatOpeningPending, 0);
                throw;
            }
            if (handoff is null)
            {
                RestoreOpeningAction(failed: false);
                Interlocked.Exchange(ref _optimizationChatOpeningPending, 0);
                return;
            }

            try
            {
                using (handoff)
                {
                    if (!ReferenceEquals(_optimizationCoordinator, coordinator)
                        || !ReferenceEquals(_attachedOptimizationPage, openingPage))
                    {
                        RestoreOpeningAction(failed: false);
                        return;
                    }
                    await LaunchOptimizedChatCoreAsync(coordinator.State);
                    if (ReferenceEquals(_attachedOptimizationPage, openingPage))
                    {
                        RestoreOpeningAction(failed: true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                RestoreOpeningAction(failed: false);
            }
            catch
            {
                RestoreOpeningAction(failed: true);
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _optimizationChatOpeningPending, 0);
            }
        }

        private async Task LaunchOptimizedChatCoreAsync(
            OptimizationJourneyState state)
        {
            if (state.Result is not { IsSuccessful: true } result)
            {
                return;
            }

            OptimizationChatTargetUse? targetUse;
            try
            {
                targetUse = await CreateOptimizationChatTargetAsync(state.Result);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (targetUse is null)
            {
                return;
            }

            OptimizationChatTarget target = targetUse.Target;
            CancellationToken cancellationToken = targetUse.CancellationToken;

            if (target is OpenVinoDestinationChatTarget openVinoTarget)
            {
                await LaunchSharedOpenVinoOptimizedAsync(openVinoTarget, targetUse);
                return;
            }
            if (target is not GgufOptimizationChatTarget ggufTarget
                || _activeGgufAuthority is not { } authority)
            {
                targetUse.Dispose();
                RetireActiveOptimizationChatTarget();
                return;
            }

            var page = _attachedChatPage ?? new ChatPage();
            ChatDemoController? controller = null;
            bool ownsController = false;
            string? registeredModelId = null;
            try
            {
                using (targetUse)
                {
                    GraniteEdgeAI.GgufRuntime.Contracts.Configuration.GgufRuntimeConfiguration
                        configuration =
                        GgufCurrentModelChatRouteLauncher.CreateChatConfiguration(
                            $"optimized-{ggufTarget.VerifiedModelSha256[..12]}",
                            ggufTarget.VerifiedModelSha256,
                            ggufTarget.RuntimeOptions);
                    string displayName = CreateChatModelDisplayName(
                        _modelInspectionPageForHardwareReturn?.Request?.FileName,
                        "Optimised GGUF model");
                    string formatLabel = ChatModelFormatLabel.Gguf(
                        ggufTarget.RuntimeOptions.PersistentTargetWeightFormat.ToString(),
                        ggufTarget.RuntimeOptions.KeyCacheType.ToString(),
                        ggufTarget.RuntimeOptions.ValueCacheType.ToString());
                    var request = new GgufChatLaunchRequest(
                        authority.RuntimePackageRoot,
                        authority.TrustedRuntimeManifest.Span,
                        ggufTarget.VerifiedModelPath,
                        displayName,
                        configuration);
                    if (_chatController is { } existing)
                    {
                        var candidate = await A1BackendProductionAuthorities.Shared.CreateVerifiedChatSessionAsync(request, cancellationToken);
                        await existing.SwitchModelAsync(candidate, displayName, "llama.cpp", cancellationToken, request.Configuration.ModelId, request.Configuration.ProfileId);
                        controller = existing;
                    }
                    else
                    {
                        controller = await A1BackendProductionAuthorities.Shared.CreateInitializedChatAsync(page, request, cancellationToken);
                        ownsController = true;
                    }
                    page.SetActiveModelLabel(formatLabel);
                    registeredModelId = RegisterOptimizedGgufChatModel(
                        ggufTarget, request, page, controller, displayName, formatLabel);
                }

                try
                {
                    await RetireOptimizationAsync(preserveChatTarget: true);
                    ShowGgufChat(page, controller);
                    controller = null;
                    if (registeredModelId is not null)
                    {
                        await _chatModelLibrary.SwitchAsync(registeredModelId, _lifetimeCancellation.Token);
                        page.ApplyModelLibrary(_chatModelLibrary.Snapshots);
                    }
                }
                finally
                {
                    if (controller is not null && ownsController)
                    {
                        await controller.DisposeAsync();
                        RetireActiveOptimizationChatTarget();
                    }
                }
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                targetUse.Dispose();
                RetireActiveOptimizationChatTarget();
            }
            catch (Exception exception)
            {
                targetUse.Dispose();
                ReportGgufLaunchFault(exception);
                RetireActiveOptimizationChatTarget();
            }
        }

        private async Task LaunchSharedOpenVinoOptimizedAsync(OpenVinoDestinationChatTarget target, OptimizationChatTargetUse targetUse)
        {
            var owner = _modelInspectionPageForHardwareReturn;
            if (owner is null) { targetUse.Dispose(); return; }
            var sourcePage = _attachedOptimizationPage;
            ChatDemoController? created = null;
            IDisposable? custody = null;
            string id = $"openvino-optimized-{target.Target.ConfigurationSha256[..24]}";
            string label = ChatModelFormatLabel.OpenVino(
                _optimizationCoordinator?.State.Entry.OptimizationHandoff.Plan.ExecutionPayload.OpenVino?.TargetWeightPrecision.ToString(),
                target.Target.RuntimeOptions.KvCachePrecision);
            string displayName = ChatModelDisplayName.OpenVino(
                target.Target.PackageDirectory, owner.OpenVinoRequest?.DisplayName);
            try
            {
                ChatPage page = _attachedChatPage ?? new ChatPage();
                using (targetUse)
                {
                    custody = target.Target.RetainVerifiedCustody();
                    var session = owner.CreateSharedOpenVinoChatSession(target.Target.PackageDirectory, target.Target.RuntimeOptions);
                    if (_chatController is { } shared)
                        await shared.SwitchModelAsync(session, displayName, "OpenVINO · CPU", targetUse.CancellationToken, id, "local");
                    else
                        created = await A1BackendProductionAuthorities.Shared.CreateInitializedSharedChatAsync(
                            page, session, id, displayName, "OpenVINO · CPU", targetUse.CancellationToken);
                }
                ChatDemoController controller = _chatController ?? created!;
                if (!_chatModelLibrary.Snapshots.Any(model => model.Id == id))
                {
                    IDisposable ownedCustody = custody;
                    bool first = true;
                    var registration = new ShellChatModelActivationTarget(ChatModelRoute.OpenVino,
                        token =>
                        {
                            if (first) { first = false; return ValueTask.FromResult(ChatModelActivationDisposition.Committed); }
                            return ActivateRetainedOpenVinoSharedAsync(owner, target.Target.PackageDirectory,
                                target.Target.RuntimeOptions, id, displayName, label, token);
                        }, () => { ownedCustody.Dispose(); return ValueTask.CompletedTask; });
                    var descriptor = ChatModelDescriptor.Create(id, displayName, ChatModelRoute.OpenVino,
                        label, "OpenVINO · CPU", ChatModelReadiness.Ready);
                    if (_chatModelLibrary.Register(descriptor, registration) != ChatModelRegistrationDisposition.Added)
                        throw new InvalidOperationException("The validated model could not be added to the model library.");
                    custody = null;
                }
                await RetireOptimizationAsync(preserveChatTarget: true);
                page.SetActiveModelLabel(label);
                ShowGgufChat(page, controller);
                created = null;
                await _chatModelLibrary.SwitchAsync(id, _lifetimeCancellation.Token);
                page.ApplyModelLibrary(_chatModelLibrary.Snapshots);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                ReportGgufLaunchFault(error);
                if (ReferenceEquals(StageFrame.Content, sourcePage) && XamlRoot is not null)
                {
                    await new ContentDialog { XamlRoot = XamlRoot, Title = "Chat could not start",
                        Content = CreateSharedOpenVinoChatFailureContent(error), CloseButtonText = "Close" }.ShowAsync();
                }
            }
            finally
            {
                targetUse.Dispose();
                custody?.Dispose();
                if (created is not null) await created.DisposeAsync();
            }
        }

        internal static string CreateSharedOpenVinoChatFailureContent(Exception error)
        {
            const string recovery = "The selected model could not start with this conversation. Your saved model and previous chat remain unchanged.";
            if (error is ChatHistoryReplayException or ChatHistoryCapacityException)
                return error.Message;
            if (error is OpenVinoRouteWorkerFailureException failure)
            {
                string message = failure.SupportCode == GraniteEdgeAI.OpenVino.Contracts.OpenVinoSupportCode.RuntimeContextExceeded
                    ? "This conversation exceeds the selected model's context capacity. Start a new chat to use this model."
                    : recovery;
                return message + "\n\nSupport code: " + GraniteEdgeAI.OpenVino.Contracts.OpenVinoSupportCodeExtensions.ToProtocolValue(failure.SupportCode) + ".";
            }
            return recovery;
        }

        private ValueTask<ChatModelActivationDisposition> ActivateRetainedOpenVinoSharedAsync(ModelInspectionPage owner,
            string package, GraniteEdgeAI.OpenVino.Contracts.OpenVinoRuntimeOptions runtime,
            string id, string displayName, string label, CancellationToken token)
        {
            var completion = new TaskCompletionSource<ChatModelActivationDisposition>(TaskCreationOptions.RunContinuationsAsynchronously);
            async void Activate()
            {
                try
                {
                    if (_chatController is not { } controller || _attachedChatPage is not { } page
                        || !ReferenceEquals(StageFrame.Content, page)) { completion.SetResult(ChatModelActivationDisposition.Cancelled); return; }
                    var session = owner.CreateSharedOpenVinoChatSession(package, runtime);
                    await controller.SwitchModelAsync(session, displayName, "OpenVINO · CPU", token, id, "local");
                    page.SetActiveModelLabel(label);
                    completion.SetResult(ChatModelActivationDisposition.Committed);
                }
                catch (OperationCanceledException) { completion.SetResult(ChatModelActivationDisposition.Cancelled); }
                catch (ChatHistoryReplayException) { completion.SetResult(ChatModelActivationDisposition.HistoryRejected); }
                catch (Exception error) when (IsSharedChatContextFailure(error)) { completion.SetResult(ChatModelActivationDisposition.ContextExceeded); }
                catch (Exception error) { ReportGgufLaunchFault(error); completion.SetResult(ChatModelActivationDisposition.Failed); }
            }
            if (DispatcherQueue.HasThreadAccess) Activate();
            else if (!DispatcherQueue.TryEnqueue(Activate)) completion.SetResult(ChatModelActivationDisposition.Cancelled);
            return new(completion.Task);
        }

        private static bool IsSharedChatContextFailure(Exception error) =>
            error is ChatHistoryCapacityException
            || error is GraniteEdgeAI.Features.GgufRuntime.Services.GgufChatRuntimeUnavailableException
                { StartupFailure.Category: GraniteEdgeAI.GgufRuntime.Contracts.Failures.GgufRuntimeFailureCategory.ContextLimitReached }
            || error is OpenVinoRouteWorkerFailureException
                { SupportCode: GraniteEdgeAI.OpenVino.Contracts.OpenVinoSupportCode.RuntimeContextExceeded };

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
            OptimizationJourneyState state)
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

            string? destination = CreateAbsentOptimizationDestination(
                selected.Path, result.Route);
            if (destination is null)
            {
                return;
            }
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
                    state.Result, destination, maximumBytes);
            }
            catch (OperationCanceledException)
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

        private static string? CreateAbsentOptimizationDestination(
            string folder,
            OptimizationRoute route)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string identity = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
                string candidate = Path.Combine(
                    folder,
                    route == OptimizationRoute.Gguf
                        ? $"optimised-model-{identity}.gguf"
                        : $"optimised-openvino-model-{identity}");
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
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

        private async Task RetireOptimizationAsync(
            bool preserveChatTarget = false)
        {
            Exception? failure = null;
            try
            {
                await RetireOptimizationDestinationLifecycleAsync(
                    preserveChatTarget);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            if (_attachedOptimizationPage is { } page)
            {
                page.IntentRequested -= OptimizationPage_IntentRequested;
            }
            if (_optimizationCoordinator is { } coordinator)
            {
                coordinator.StateChanged -= OptimizationCoordinator_StateChanged;
                try
                {
                    await coordinator.DisposeAsync();
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }
            _attachedOptimizationPage = null;
            _optimizationCoordinator = null;
            _optimizationContextFactory = null;
            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private async Task ReturnFromOptimizationAsync(
            OptimizationJourneyCoordinator coordinator)
        {
            OptimizationChatHandoffLease? handoff =
                await _optimizationChatHandoff.TryEnterAsync(
                    _lifetimeCancellation.Token);
            if (handoff is null)
            {
                return;
            }

            using (handoff)
            {
                if (!ReferenceEquals(_optimizationCoordinator, coordinator)
                    || _attachedOptimizationPage is null)
                {
                    return;
                }

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

            CompatibilityPage openingPage = _attachedCompatibilityPage!;
            Button? openingButton = openingPage.FindName("BtnCompatibilityPrimary") as Button;
            bool previousEnabled = openingButton?.IsEnabled == true;

            void PresentChatRetryFailure()
            {
                if (openingButton is null
                    || !ReferenceEquals(_attachedCompatibilityPage, openingPage))
                {
                    return;
                }

                openingButton.Content = "Chat couldn't load — try again";
                openingButton.IsEnabled = previousEnabled;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                    openingButton, "Chat couldn't load. Try again");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(
                    openingButton,
                    "The chat did not open. Activate this button to try again.");
                var failurePeer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(openingButton)
                    ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(openingButton);
                failurePeer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.Other,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    "Chat couldn't load. Try again",
                    "GraniteCurrentModelChatFailure");
            }

            if (openingButton is not null)
            {
                openingButton.Content = "Loading chat…";
                openingButton.IsEnabled = false;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(openingButton, "Loading chat");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(
                    openingButton, "Preparing the verified model for chat.");
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(openingButton)
                    ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(openingButton);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.Other,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    "Loading chat",
                    "GraniteCurrentModelChatLoading");
            }

            await Task.Yield();
            if (!ReferenceEquals(_attachedCompatibilityPage, openingPage))
            {
                return;
            }

            if (!TryRegisterCurrentChatModel(
                    eventArguments.Handoff,
                    out string? modelId))
            {
                PresentChatRetryFailure();
                return;
            }
            try
            {
                Task switchTask = SwitchChatModelAsync(modelId!);
                lock (_chatModelSwitchGate)
                {
                    _chatModelSwitchTask = switchTask;
                }
                await switchTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                ReportGgufLaunchFault(error);
            }
            finally
            {
                if (openingButton is not null &&
                    ReferenceEquals(_attachedCompatibilityPage, openingPage))
                {
                    PresentChatRetryFailure();
                }
            }
        }

        private string? RegisterOptimizedGgufChatModel(
            GgufOptimizationChatTarget target,
            GgufChatLaunchRequest request,
            ChatPage initializedPage,
            ChatDemoController initializedController,
            string displayName,
            string formatLabel)
        {
            string id = $"gguf-optimized-{target.Result.ConfigurationSha256[..24]}";
            if (_chatModelLibrary.Snapshots.Any(model => model.Id == id)) return id;
            IDisposable custody = target.RetainVerifiedCustody();
            bool initialActivation = true;
            var activation = new ShellChatModelActivationTarget(
                ChatModelRoute.Gguf,
                token =>
                {
                    if (initialActivation && ReferenceEquals(_attachedChatPage, initializedPage)
                        && ReferenceEquals(_chatController, initializedController))
                    {
                        initialActivation = false;
                        return ValueTask.FromResult(ChatModelActivationDisposition.Committed);
                    }
                    initialActivation = false;
                    return ActivateRetainedGgufChatAsync(request, initializedPage, initializedController, formatLabel, token);
                },
                () => { custody.Dispose(); return ValueTask.CompletedTask; });
            try
            {
                var descriptor = ChatModelDescriptor.Create(id, displayName,
                    ChatModelRoute.Gguf, formatLabel,
                    $"llama.cpp · {target.RuntimeOptions.Backend} · {target.RuntimeOptions.KeyCacheType}",
                    ChatModelReadiness.Ready);
                if (_chatModelLibrary.Register(descriptor, activation) == ChatModelRegistrationDisposition.Added)
                    return id;
                custody.Dispose();
                return null;
            }
            catch { custody.Dispose(); throw; }
        }

        private ValueTask<ChatModelActivationDisposition> ActivateRetainedGgufChatAsync(
            GgufChatLaunchRequest request, ChatPage initializedPage,
            ChatDemoController initializedController, string formatLabel,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ChatModelActivationDisposition>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            async void ActivateOnUiThread()
            {
                ChatDemoController? nextController = null;
                ChatModelActivationDisposition disposition = ChatModelActivationDisposition.Failed;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    object? originContent = StageFrame.Content;
                    ChatPage? originPage = _attachedChatPage;
                    OnboardingStage originStage = CurrentStage;
                    if (_chatController is { } sharedController && _attachedChatPage is { } sharedPage)
                    {
                        var candidate = await A1BackendProductionAuthorities.Shared
                            .CreateVerifiedChatSessionAsync(request, cancellationToken);
                        if (!ReferenceEquals(StageFrame.Content, originContent) || !ReferenceEquals(_attachedChatPage, originPage))
                        {
                            await candidate.DisposeAsync();
                            disposition = ChatModelActivationDisposition.Cancelled;
                            return;
                        }
                        await sharedController.SwitchModelAsync(candidate, request.DisplayName, "llama.cpp", cancellationToken, request.Configuration.ModelId, request.Configuration.ProfileId);
                        sharedPage.SetActiveModelLabel(formatLabel);
                        disposition = ChatModelActivationDisposition.Committed;
                        return;
                    }
                    var nextPage = new ChatPage();
                    nextController = await A1BackendProductionAuthorities.Shared
                        .CreateInitializedChatAsync(nextPage, request, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    _lifetimeCancellation.Token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(StageFrame.Content, originContent)
                        || !ReferenceEquals(_attachedChatPage, originPage)
                        || CurrentStage != originStage)
                    {
                        disposition = ChatModelActivationDisposition.Cancelled;
                        return;
                    }
                    nextPage.SetActiveModelLabel(formatLabel);
                    ShowGgufChat(nextPage, nextController);
                    nextController = null;
                    disposition = ChatModelActivationDisposition.Activated;
                }
                catch (OperationCanceledException) { disposition = ChatModelActivationDisposition.Cancelled; }
                catch (ChatHistoryReplayException) { disposition = ChatModelActivationDisposition.HistoryRejected; }
                catch (Exception error) when (IsSharedChatContextFailure(error)) { disposition = ChatModelActivationDisposition.ContextExceeded; }
                catch (Exception exception)
                {
                    ReportGgufLaunchFault(exception);
                    disposition = ChatModelActivationDisposition.Failed;
                }
                finally
                {
                    if (nextController is not null) await DisposeReplacedChatControllerAsync(nextController);
                    completion.TrySetResult(disposition);
                }
            }
            if (DispatcherQueue.HasThreadAccess) ActivateOnUiThread();
            else if (!DispatcherQueue.TryEnqueue(ActivateOnUiThread))
                completion.SetResult(ChatModelActivationDisposition.Cancelled);
            return new ValueTask<ChatModelActivationDisposition>(completion.Task);
        }

        private bool TryRegisterCurrentChatModel(
            CurrentModelLaunchHandoff handoff,
            out string? modelId)
        {
            ArgumentNullException.ThrowIfNull(handoff);
            modelId = null;
            ChatModelRoute route = handoff.Route switch
            {
                OptimizationRoute.Gguf => ChatModelRoute.Gguf,
                OptimizationRoute.OpenVino => ChatModelRoute.OpenVino,
                _ => throw new ArgumentOutOfRangeException(nameof(handoff)),
            };
            string computedModelId =
                $"{(route == ChatModelRoute.Gguf ? "gguf" : "openvino")}-" +
                $"{handoff.ModelSha256[..12]}-" +
                handoff.ModelInspectionHandoffId.ToString("N")[..8];
            if (_chatModelLibrary.Snapshots.Any(snapshot =>
                    string.Equals(
                        snapshot.Id,
                        computedModelId,
                        StringComparison.Ordinal)))
            {
                modelId = computedModelId;
                return true;
            }

            var sourceKey = new ModelSourceCustodyKey(
                handoff.ModelInspectionHandoffId,
                handoff.ModelSha256,
                handoff.ModelLengthBytes,
                handoff.Route);
            if (!_modelSourceCustodyRegistry.TryAcquire(
                    sourceKey,
                    out ModelSourceLease? sourceLease))
            {
                return false;
            }
            sourceLease!.Dispose();

            ModelInspectionPage? sourcePage = _modelInspectionPageForHardwareReturn;
            string displayName = route == ChatModelRoute.Gguf
                ? CreateChatModelDisplayName(sourcePage?.Request?.FileName, "Imported GGUF model")
                : ChatModelDisplayName.OpenVino(sourcePage?.OpenVinoRequest?.DisplayName);
            string? weightLabel = route == ChatModelRoute.Gguf
                ? sourcePage?.Request?.QuickScan?.Quantisation
                : sourcePage?.VerifiedOpenVinoWeightLabel;
            if (!CurrentModelChatLaunchRegistry.TryGetExecutionPayload(handoff, out var executionPayload))
                return false;
            string formatLabel = route == ChatModelRoute.Gguf
                ? ChatModelFormatLabel.Gguf(weightLabel,
                    executionPayload!.Gguf?.KeyCacheType.ToString(),
                    executionPayload.Gguf?.ValueCacheType.ToString())
                : ChatModelFormatLabel.OpenVino(weightLabel,
                    executionPayload!.OpenVino?.KvCachePrecision.ToString());
            var descriptor = ChatModelDescriptor.Create(
                computedModelId,
                displayName,
                route,
                formatLabel,
                "Compatible local profile",
                ChatModelReadiness.Ready);

            RetainChatModelHandoff(handoff.ModelInspectionHandoffId);
            var target = new ShellChatModelActivationTarget(
                route,
                cancellationToken => ActivateRegisteredChatModelAsync(
                    handoff,
                    displayName,
                    formatLabel,
                    cancellationToken),
                () =>
                {
                    ReleaseChatModelHandoff(handoff.ModelInspectionHandoffId);
                    return ValueTask.CompletedTask;
                });
            ChatModelRegistrationDisposition registration =
                _chatModelLibrary.Register(descriptor, target);
            if (registration == ChatModelRegistrationDisposition.Added)
            {
                modelId = computedModelId;
                return true;
            }

            ReleaseChatModelHandoff(handoff.ModelInspectionHandoffId);
            modelId = null;
            return false;
        }

        private static string CreateChatModelDisplayName(
            string? candidate,
            string fallback)
        {
            string trimmed = candidate?.Trim().TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) ?? string.Empty;
            string finalSegment = Path.GetFileName(trimmed);
            string safe = string.IsNullOrWhiteSpace(finalSegment)
                ? fallback
                : new string(finalSegment.Where(character => !char.IsControl(character))
                    .ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = fallback;
            }
            return safe.Length <= 128 ? safe : safe[..128];
        }

        private async ValueTask<ChatModelActivationDisposition>
            ActivateRegisteredChatModelAsync(
                CurrentModelLaunchHandoff handoff,
                string displayName,
                string formatLabel,
                CancellationToken cancellationToken)
        {
            if (handoff.Route == OptimizationRoute.OpenVino)
            {
                return await ActivateRegisteredOpenVinoModelAsync(
                    handoff,
                    displayName,
                    formatLabel,
                    cancellationToken);
            }

            try
            {
                CurrentModelChatLaunchResult result =
                    await CurrentModelChatLaunchRegistry.LaunchAsync(handoff, cancellationToken);
                if (result.Succeeded && _attachedChatPage is { } page)
                {
                    page.SetActiveModelLabel(formatLabel);
                }
                return result.Succeeded ? ChatModelActivationDisposition.Committed : MapChatActivation(result);
            }
            catch (ChatHistoryReplayException) { return ChatModelActivationDisposition.HistoryRejected; }
            catch (Exception error) when (IsSharedChatContextFailure(error)) { return ChatModelActivationDisposition.ContextExceeded; }
        }

        private async ValueTask<ChatModelActivationDisposition>
            ActivateRegisteredOpenVinoModelAsync(
                CurrentModelLaunchHandoff handoff,
                string displayName,
                string formatLabel,
                CancellationToken cancellationToken)
        {
            if (!CurrentModelChatLaunchRegistry.TryGetOpenVinoRuntimeOptions(handoff, out var runtimeOptions))
                return ChatModelActivationDisposition.RuntimeUnavailable;
            var sourceKey = new ModelSourceCustodyKey(handoff.ModelInspectionHandoffId,
                handoff.ModelSha256, handoff.ModelLengthBytes, handoff.Route);
            if (!_modelSourceCustodyRegistry.TryAcquire(sourceKey, out ModelSourceLease? sourceLease))
                return ChatModelActivationDisposition.SourceUnavailable;
            using (sourceLease!)
            {
                ChatDemoController? created = null;
                try
                {
                    var owner = _modelInspectionPageForHardwareReturn ?? new ModelInspectionPage();
                    var session = owner.CreateSharedOpenVinoChatSession(sourceLease!.SourcePath,
                        runtimeOptions!);
                    ChatPage page = _attachedChatPage ?? new ChatPage();
                    if (_chatController is { } shared)
                        await shared.SwitchModelAsync(session, displayName, "OpenVINO · CPU", cancellationToken, handoff.ModelSha256, "local");
                    else
                        created = await A1BackendProductionAuthorities.Shared.CreateInitializedSharedChatAsync(
                            page, session, handoff.ModelSha256, displayName, "OpenVINO · CPU", cancellationToken);
                    page.SetActiveModelLabel(formatLabel);
                    ShowGgufChat(page, _chatController ?? created!);
                    created = null;
                    return ChatModelActivationDisposition.Committed;
                }
                catch (OperationCanceledException) { return ChatModelActivationDisposition.Cancelled; }
                catch (ChatHistoryReplayException) { return ChatModelActivationDisposition.HistoryRejected; }
                catch (Exception error) when (IsSharedChatContextFailure(error)) { return ChatModelActivationDisposition.ContextExceeded; }
                catch (Exception error) { ReportGgufLaunchFault(error); return ChatModelActivationDisposition.Failed; }
                finally { if (created is not null) await created.DisposeAsync(); }
            }
        }

        private async Task SwitchChatModelAsync(string modelId)
        {
            ChatPage? originPage = _attachedChatPage;
            originPage?.ApplyModelSwitchPresentation(
                ChatModelSwitchPresentation.Starting,
                modelId);

            using var cancellation = CancellationTokenSource
                .CreateLinkedTokenSource(_lifetimeCancellation.Token);
            CancellationTokenSource? previousCancellation;
            lock (_chatModelSwitchGate)
            {
                previousCancellation = _chatModelSwitchCancellation;
                _chatModelSwitchCancellation = cancellation;
            }
            previousCancellation?.Cancel();

            ChatModelSwitchResult result = await _chatModelLibrary.SwitchAsync(
                modelId,
                cancellation.Token);
            if (result.Disposition == ChatModelSwitchDisposition.SourceUnavailable)
            {
                _ = await _chatModelLibrary.RemoveAsync(
                    modelId,
                    CancellationToken.None);
            }

            ChatPage? destinationPage = _attachedChatPage;
            if (destinationPage is not null)
            {
                destinationPage.ApplyModelLibrary(_chatModelLibrary.Snapshots);
                destinationPage.ApplyModelSwitchPresentation(
                    ChatModelSwitchPresentation.FromResult(result),
                    modelId);
            }
            else if (originPage is not null
                && ReferenceEquals(StageFrame.Content, originPage))
            {
                originPage.ApplyModelLibrary(_chatModelLibrary.Snapshots);
                originPage.ApplyModelSwitchPresentation(
                    ChatModelSwitchPresentation.FromResult(result),
                    modelId);
            }

            lock (_chatModelSwitchGate)
            {
                if (ReferenceEquals(_chatModelSwitchCancellation, cancellation))
                {
                    _chatModelSwitchCancellation = null;
                }
            }
        }

        private void ChatPage_ModelSelectionRequested(
            object? sender,
            string modelId)
        {
            if (!ReferenceEquals(sender, _attachedChatPage))
            {
                return;
            }

            Task switchTask = SwitchChatModelAsync(modelId);
            lock (_chatModelSwitchGate)
            {
                _chatModelSwitchTask = switchTask;
            }
        }

        private void ChatPage_ModelSelectionCancellationRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (!ReferenceEquals(sender, _attachedChatPage))
            {
                return;
            }

            CancellationTokenSource? cancellation;
            lock (_chatModelSwitchGate)
            {
                cancellation = _chatModelSwitchCancellation;
            }
            cancellation?.Cancel();
        }

        private static ChatModelActivationDisposition MapChatActivation(
            CurrentModelChatLaunchResult result)
        {
            if (result.Succeeded)
            {
                return ChatModelActivationDisposition.Activated;
            }
            return result.SupportCode switch
            {
                CurrentModelChatSupportCode.CancelledByUser =>
                    ChatModelActivationDisposition.Cancelled,
                CurrentModelChatSupportCode.SourceUnavailable =>
                    ChatModelActivationDisposition.SourceUnavailable,
                CurrentModelChatSupportCode.RuntimeUnavailable =>
                    ChatModelActivationDisposition.RuntimeUnavailable,
                _ => ChatModelActivationDisposition.Failed,
            };
        }

        internal async Task<bool> ActivateOpenVinoCurrentModelChatAsync(
            ModelInspectionPage openVinoPage,
            Func<CancellationToken, Task<bool>> activateAsync,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(openVinoPage);
            ArgumentNullException.ThrowIfNull(activateAsync);
            CompatibilityPage? compatibilityPage = _attachedCompatibilityPage;
            if (compatibilityPage is null
                || !ReferenceEquals(
                    openVinoPage,
                    _modelInspectionPageForHardwareReturn)
                || !ReferenceEquals(StageFrame.Content, compatibilityPage))
            {
                return false;
            }

            // A WinUI page must be reconnected to the Frame before activation
            // mutates its prompt controls. Activating the retained, detached
            // inspection page can dereference stale XAML/COM peers and terminate
            // the process after the native session has loaded successfully
            StageFrame.Content = openVinoPage;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();

            bool activated;
            try
            {
                activated = await activateAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(
                        ref _openVinoChatLaunchFaultReported,
                        1) == 0)
                {
                    BoundedApplicationFaultReporter.Shared.Report(
                        ApplicationFault.FromException(
                            ApplicationFaultCode.OpenVinoChatOperationUnexpected,
                            exception));
                }
                activated = false;
            }

            if (!activated)
            {
                StageFrame.Content = compatibilityPage;
                StageFrame.BackStack.Clear();
                StageFrame.ForwardStack.Clear();
                return false;
            }

            DetachCompatibilityPage();
            DetachHardwareInspectionPage();
            AttachModelInspectionPage(openVinoPage);
            CurrentStage = OnboardingStage.ReadyToChat;
            StageIndicator.CurrentStage = CurrentStage;
            return true;
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
            modelPage.SetHardwareRouteAvailable(
                _hardwareInspectionService is not null
                && _hardwareHandoffReissuer(modelPage) is not null);
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
        private bool NavigateToFreshModelImport() =>
            NavigateToFreshModelImport(ModelImportPresentationMode.AllSources);

        private bool NavigateToFreshModelImport(
            ModelImportPresentationMode presentationMode)
        {
            object? previousContent = StageFrame.Content;
            bool navigationSucceeded =
                StageFrame.Navigate(typeof(ModelImportPage));

            // Frame.Navigate can report true after a cancelled Navigating
            // event, so the expected content is the completion boundary
            if (!navigationSucceeded ||
                ReferenceEquals(StageFrame.Content, previousContent) ||
                StageFrame.Content is not ModelImportPage modelImportPage)
            {
                return false;
            }


            // a new selection is a new journey. remove the inspection page
            // and its request from the Frame journal before exposing the new
            // active stage.
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();

            modelImportPage.SetPresentationMode(presentationMode);
            BackToChatRegion.Visibility = _chatController is not null && _attachedChatPage is not null
                ? Visibility.Visible : Visibility.Collapsed;

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
            // there is nothing to detach before a page has been attached
            if (_attachedModelImportPage is null)
            {
                return;
            }

            // remove the event subscription to avoid retaining an inactive page
            _attachedModelImportPage.ModelInspectionRequested -=
                ModelImportPage_ModelInspectionRequested;
            _attachedModelImportPage.OpenVinoInspectionRequested -=
                ModelImportPage_OpenVinoInspectionRequested;
            _attachedModelImportPage.SourceModelConversionRequested -=
                ModelImportPage_SourceModelConversionRequested;
            _attachedModelImportPage.VerifiedDownloadInspectionReady -=
                ModelImportPage_VerifiedDownloadInspectionReady;

            // release the reference to the old page
            _attachedModelImportPage = null;
        }

        /// <summary>
        /// Removes the current Model Inspection event subscription.
        /// </summary>
        private void DetachModelInspectionPage(
            bool updateHardwareRouteAvailability = true)
        {
            if (_attachedModelInspectionPage is null)
            {
                return;
            }

            _attachedModelInspectionPage.ChooseAnotherModelRequested -=
                ModelInspectionPage_ChooseAnotherModelRequested;
            _attachedModelInspectionPage.OpenVinoChatView.ImportModelRequested -= OpenVinoChat_ImportRequested;
            _attachedModelInspectionPage.OpenVinoChatView.ThemeRequested -= OpenVinoChat_ThemeRequested;
            _attachedModelInspectionPage.FooterStatusChanged -=
                ModelInspectionPage_FooterStatusChanged;
            _attachedModelInspectionPage.HardwareInspectionRequested -=
                ModelInspectionPage_HardwareInspectionRequested;
            if (updateHardwareRouteAvailability)
            {
                _attachedModelInspectionPage.SetHardwareRouteAvailable(false);
            }
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
            _attachedCompatibilityPage.HardwareRetryRequested -= CompatibilityPage_HardwareRetryRequested;
            _attachedCompatibilityPage.ConfigureStageEntered -=
                CompatibilityPage_ConfigureStageEntered;
            _attachedCompatibilityPage.ConfigureStageExited -=
                CompatibilityPage_ConfigureStageExited;
            _attachedCompatibilityPage.ContinueRequested -= CompatibilityPage_ContinueRequested;
            _attachedCompatibilityPage.OptimizationRequested -=
                CompatibilityPage_OptimizationRequested;
            _attachedCompatibilityPage.CurrentModelChatRequested -=
                CompatibilityPage_CurrentModelChatRequested;
            _attachedCompatibilityPage.ImportAnotherModelRequested -=
                CompatibilityPage_ImportAnotherModelRequested;
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
                new GgufCurrentModelChatRouteLauncher(accessor, ShowGgufChat, SwitchCurrentGgufSharedAsync));
        }

        private async Task<bool> SwitchCurrentGgufSharedAsync(GgufChatLaunchRequest request, CancellationToken token)
        {
            if (_chatController is not { } controller || _attachedChatPage is not { } page) return false;
            var candidate = await A1BackendProductionAuthorities.Shared.CreateVerifiedChatSessionAsync(request, token);
            await controller.SwitchModelAsync(candidate, request.DisplayName, "llama.cpp", token, request.Configuration.ModelId, request.Configuration.ProfileId);
            ChatModelSnapshot? descriptor = _chatModelLibrary.Snapshots
                .FirstOrDefault(model => model.Route == ChatModelRoute.Gguf &&
                    string.Equals(model.Id, request.Configuration.ModelId,
                        StringComparison.Ordinal))
                ?? _chatModelLibrary.Snapshots.FirstOrDefault(model =>
                    model.Route == ChatModelRoute.Gguf && model.IsActive &&
                    string.Equals(model.DisplayName, request.DisplayName,
                        StringComparison.Ordinal))
                ?? _chatModelLibrary.Snapshots.FirstOrDefault(model =>
                    model.Route == ChatModelRoute.Gguf && model.IsActive);
            string label = descriptor?.FormatLabel?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = "GGUF";
            }
            else if (!string.Equals(label, "GGUF", StringComparison.OrdinalIgnoreCase) &&
                !label.EndsWith(" · GGUF", StringComparison.OrdinalIgnoreCase))
            {
                label += " · GGUF";
            }
            page.SetActiveModelLabel(label);
            ShowGgufChat(page, controller);
            return true;
        }

        private void ShowGgufChat(
            ChatPage page,
            ChatDemoController controller)
        {
            BackToChatRegion.Visibility = Visibility.Collapsed;
            ChatPage? previousPage = _attachedChatPage;
            ChatDemoController? previousController = _chatController;
            if (previousPage is not null)
            {
                DetachChatPage(previousPage);
            }
            DetachCompatibilityPage();
            DetachModelInspectionPage();
            _hardwarePageForCompatibilityReturn = null;
            _attachedChatPage = page;
            _chatController = controller;
            AttachChatPage(page);
            page.ApplyModelLibrary(_chatModelLibrary.Snapshots);
            StageFrame.Content = page;
            StageFrame.BackStack.Clear();
            StageFrame.ForwardStack.Clear();
            CurrentStage = OnboardingStage.ReadyToChat;
            StageIndicator.CurrentStage = CurrentStage;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ReferenceEquals(_attachedChatPage, page) && ReferenceEquals(StageFrame.Content, page))
                    page.FocusMessageInput();
            });
            if (previousController is not null
                && !ReferenceEquals(previousController, controller))
            {
                _ = DisposeReplacedChatControllerAsync(previousController);
            }
        }

        private bool _openVinoChatImportPending;

        private async void OpenVinoChat_ImportRequested(object? sender, EventArgs args)
        {
            ModelInspectionPage? owner = _attachedModelInspectionPage;
            if (_openVinoChatImportPending || owner is null || !ReferenceEquals(sender, owner.OpenVinoChatView)
                || !ReferenceEquals(StageFrame.Content, owner) || CurrentStage != OnboardingStage.ReadyToChat) return;
            _openVinoChatImportPending = true;
            try
            {
                await owner.RetireForNavigationAsync();
                if (ReferenceEquals(owner, _attachedModelInspectionPage) && ReferenceEquals(StageFrame.Content, owner)
                    && CurrentStage == OnboardingStage.ReadyToChat) NavigateToFreshModelImport();
            }
            catch (Exception exception)
            {
                BoundedApplicationFaultReporter.Shared.Report(ApplicationFault.FromException(
                    ApplicationFaultCode.ShutdownUnexpected, exception));
            }
            finally { _openVinoChatImportPending = false; }
        }

        private void OpenVinoChat_ThemeRequested(object? sender, ElementTheme theme)
        {
            if (ReferenceEquals(sender, _attachedModelInspectionPage?.OpenVinoChatView)) RequestedTheme = theme;
        }

        private void AttachChatPage(ChatPage page)
        {
            page.ImportModelRequested += ChatPage_ImportModelRequested;
            page.GetMoreLocalModelsRequested +=
                ChatPage_GetMoreLocalModelsRequested;
            page.ThemeRequested += ChatPage_ThemeRequested;
            page.ModelSelectionRequested += ChatPage_ModelSelectionRequested;
            page.ModelSelectionCancellationRequested +=
                ChatPage_ModelSelectionCancellationRequested;
        }

        private void DetachChatPage(ChatPage page)
        {
            page.ImportModelRequested -= ChatPage_ImportModelRequested;
            page.GetMoreLocalModelsRequested -=
                ChatPage_GetMoreLocalModelsRequested;
            page.ThemeRequested -= ChatPage_ThemeRequested;
            page.ModelSelectionRequested -= ChatPage_ModelSelectionRequested;
            page.ModelSelectionCancellationRequested -=
                ChatPage_ModelSelectionCancellationRequested;
        }

        private static async Task DisposeReplacedChatControllerAsync(
            ChatDemoController controller)
        {
            try
            {
                await controller.DisposeAsync();
            }
            catch (Exception exception)
            {
                BoundedApplicationFaultReporter.Shared.Report(
                    ApplicationFault.FromException(
                        ApplicationFaultCode.ShutdownUnexpected,
                        exception));
            }
        }

        private async void ChatPage_ImportModelRequested(
            object? sender,
            EventArgs eventArguments) =>
            await RetireChatAndNavigateToImportAsync(
                sender as ChatPage,
                ModelImportPresentationMode.SourceChoiceFirst);

        private async void ChatPage_GetMoreLocalModelsRequested(
            object? sender,
            EventArgs eventArguments) =>
            await RetireChatAndNavigateToImportAsync(
                sender as ChatPage,
                ModelImportPresentationMode.RecommendedDownloadOnly);

        private void ChatPage_ThemeRequested(
            object? sender,
            ElementTheme requestedTheme)
        {
            if (ReferenceEquals(sender, _attachedChatPage))
            {
                RequestedTheme = requestedTheme;
            }
        }

        private async void BackToChat_Click(object sender, RoutedEventArgs args)
        {
            if (_attachedChatPage is not { } chat || _chatController is not { } controller) return;
            BackToChatButton.IsEnabled = false;
            try
            {
                lock (_chatModelSwitchGate) _chatModelSwitchCancellation?.Cancel();
                if (_optimizationCoordinator is not null) await RetireOptimizationAsync();
                if (_attachedModelInspectionPage is { } inspection) await inspection.RetireForNavigationAsync();
                InvalidateActiveHardwareJourney();
                DetachModelImportPage();
                DetachModelInspectionPage();
                DetachHardwareInspectionPage();
                DetachCompatibilityPage();
                if (ReferenceEquals(_attachedChatPage, chat) && ReferenceEquals(_chatController, controller)) ShowGgufChat(chat, controller);
            }
            catch (Exception error) { ReportShutdownFault(error); }
            finally { BackToChatButton.IsEnabled = true; }
        }

        private async Task RetireChatAndNavigateToImportAsync(
            ChatPage? page,
            ModelImportPresentationMode presentationMode)
        {
            if (!ReferenceEquals(page, _attachedChatPage))
            {
                return;
            }
            lock (_chatModelSwitchGate) _chatModelSwitchCancellation?.Cancel();
            await Task.Yield();
            NavigateToFreshModelImport(presentationMode);
        }

        private static bool DefaultCompatibilityNavigation(
            Frame frame,
            CompatibilityPage page)
        {
            frame.Content = page;
            return true;
        }
    }

    internal sealed class ShellChatModelActivationTarget : IChatModelActivationTarget
    {
        private readonly Func<CancellationToken, ValueTask<ChatModelActivationDisposition>>
            activateAsync;
        private readonly Func<ValueTask> disposeAsync;
        private int disposed;

        internal ShellChatModelActivationTarget(
            ChatModelRoute route,
            Func<CancellationToken, ValueTask<ChatModelActivationDisposition>>
                activateAsync,
            Func<ValueTask> disposeAsync)
        {
            Route = route;
            this.activateAsync = activateAsync ??
                throw new ArgumentNullException(nameof(activateAsync));
            this.disposeAsync = disposeAsync ??
                throw new ArgumentNullException(nameof(disposeAsync));
        }

        public ChatModelRoute Route { get; }

        public ValueTask<ChatModelActivationDisposition> ActivateAsync(
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref disposed) != 0,
                this);
            return activateAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() =>
            Interlocked.Exchange(ref disposed, 1) == 0
                ? disposeAsync()
                : ValueTask.CompletedTask;
    }
}
