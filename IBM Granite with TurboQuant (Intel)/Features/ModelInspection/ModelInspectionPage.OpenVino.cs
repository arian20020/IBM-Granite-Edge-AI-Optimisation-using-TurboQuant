using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GraniteEdgeAI.Features.ModelInspection;

public sealed partial class ModelInspectionPage
{
    private OpenVinoRouteService? _openVinoRouteService;
    private PromptRouteRegistry? _promptRouteRegistry;
    private CancellationTokenSource? _openVinoCancellation;
    private IPromptRouteSession? _promptSession;
    private PromptSessionPresenter? _promptPresenter;
    private OpenVinoConversionOffer? _conversionOffer;
    private OpenVinoConversionService? _conversionService;
    private OpenVinoInspectionRequestedEventArgs? _openVinoConversionSourceRequest;
    private string? _openVinoPackageDirectory;
    private OpenVinoRouteHandoffLease? _openVinoHardwareLease;
    private ModelInspectionHandoffV2? _openVinoAuthoritativeHandoff;
    private ModelInspectionHandoff? _openVinoHardwareHandoff;
    private OpenVinoConfigurationCandidate? _openVinoConfiguration;
    private OpenVinoStaticPackageEvidence? _openVinoCompatibilityFacts;
    private bool _openVinoHardwareRouteAvailable;
    private readonly object _openVinoRetirementLock = new();
    private readonly object _navigationRetirementLock = new();
    private Task? _navigationRetirementTask;
    private InspectionProgressRows? _openVinoProgressRows;
    private ModelInspectionMilestoneSequencer? _openVinoMilestoneSequencer;
    private IModelInspectionMotionSettings? _openVinoMotionSettings;
    private OpenVinoRouteInspectionResult? _openVinoPendingReadyResult;
    private OpenVinoRouteInspectionResult? _openVinoReadyResult;
    private string _openVinoDisplayName = string.Empty;
    private bool _openVinoInspectionDetailsExpanded;
    private bool _openVinoFinalProgressPresented;
    private long _openVinoProgressRevision;
    private long _openVinoLifetime;
    private int _requestedOpenVinoNewTokens =
        OpenVinoRouteCapability.DefaultRequestedNewTokens;

    internal Task? CurrentOpenVinoInspectionTask { get; private set; }
    internal Task? CurrentOpenVinoConversionTask { get; private set; }
    internal Task? CurrentOpenVinoPromptTask { get; private set; }
    internal Task? CurrentOpenVinoStopTask { get; private set; }
    internal Task? CurrentOpenVinoCancelTask { get; private set; }
    internal Task? CurrentOpenVinoCleanupTask { get; private set; }
    internal PromptSurfaceState? CurrentPromptSurfaceState =>
        _promptPresenter?.State;
    internal Func<Task>? NavigationRetirementOverride { get; set; }
    internal PromptTurnResult? LastOpenVinoTurnResult { get; private set; }
    internal int RequestedOpenVinoNewTokens
    {
        get => _requestedOpenVinoNewTokens;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value,
                OpenVinoRouteCapability.MaximumRequestedNewTokens);
            _requestedOpenVinoNewTokens = value;
        }
    }

    private void BeginOpenVinoInspection(
        OpenVinoInspectionRequestedEventArgs request,
        string packageDirectory)
    {
        // Model Inspection never owns prompting. Keep the legacy shared
        // surface hidden even when service composition fails before the
        // inspecting presentation can be applied.
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        OpenVinoRouteService service;
        try
        {
            service = _openVinoRouteService ??=
                ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService();
            _promptRouteRegistry ??=
                ModelInspectionServiceComposition.CreatePromptRouteRegistry(service);
        }
        catch (Exception)
        {
            ApplyOpenVinoFailurePresentation(
                "runtime_load_failed",
                "The verified OpenVINO worker is unavailable.",
                "Repair or reinstall the app, then retry.");
            CurrentOpenVinoInspectionTask = Task.CompletedTask;
            return;
        }
        CancellationTokenSource cancellation = new();
        _openVinoCancellation = cancellation;
        _openVinoConversionSourceRequest = request;
        long lifetime = checked(++_openVinoLifetime);
        ApplyOpenVinoInspectingPresentation(request.DisplayName, lifetime);
        CurrentOpenVinoInspectionTask = InspectAndStartOpenVinoAsync(
            service,
            request,
            packageDirectory,
            lifetime,
            cancellation.Token);
    }

    private async Task InspectAndStartOpenVinoAsync(
        OpenVinoRouteService service,
        OpenVinoInspectionRequestedEventArgs request,
        string packageDirectory,
        long lifetime,
        CancellationToken cancellationToken)
    {
        OpenVinoRouteHandoffLease? handoffLease = null;
        OpenVinoConversionOffer? conversionOffer = null;
        try
        {
            IProgress<OpenVinoRouteInspectionProgress> inspectionProgress =
                new InlineProgress<OpenVinoRouteInspectionProgress>(progress =>
                DispatcherQueue.TryEnqueue(() =>
                    ApplyOpenVinoRouteProgress(lifetime, progress)));
            OpenVinoRouteInspectionResult result = await Task.Run(
                () => service.InspectAsync(
                    packageDirectory,
                    inspectionProgress,
                    cancellationToken),
                cancellationToken);
            handoffLease = result.HandoffLease;
            conversionOffer = result.ConversionOffer;
            if (!IsCurrentOpenVinoLifetime(lifetime) ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.Outcome == OpenVinoRouteInspectionOutcome.ConversionRequired &&
                conversionOffer is not null)
            {
                _conversionOffer?.Dispose();
                _conversionOffer = conversionOffer;
                _openVinoConversionSourceRequest = request;
                conversionOffer = null;
                ApplyOpenVinoNonReadyPresentation(result);
                return;
            }

            if (result.Outcome is not (
                    OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings) ||
                handoffLease is null)
            {
                ApplyOpenVinoNonReadyPresentation(result);
                return;
            }

            if (!OpenVinoHardwareHandoffAdapter.TryProject(
                    handoffLease.Handoff,
                    out ModelInspectionHandoff? hardwareHandoff))
            {
                ApplyOpenVinoFailurePresentation(
                    "inspection_handoff_invalid",
                    "The OpenVINO inspection evidence could not be prepared.",
                    "Choose the package again and rerun model inspection.");
                return;
            }

            _openVinoHardwareLease?.Dispose();
            _openVinoHardwareLease = handoffLease;
            handoffLease = null;
            _openVinoAuthoritativeHandoff = _openVinoHardwareLease.Handoff;
            _openVinoHardwareHandoff = hardwareHandoff;
            _openVinoConfiguration = result.Configuration;
            _openVinoCompatibilityFacts = result.CompatibilityFacts;
            _openVinoPendingReadyResult = result;
            TryApplyOpenVinoReadyPresentation(lifetime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
            {
                ApplyOpenVinoCancelledPresentation();
            }
        }
        catch (Exception)
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
            {
                ApplyOpenVinoFailurePresentation(
                    "runtime_load_failed",
                    "The OpenVINO runtime could not load the model package.",
                    "Choose the package again or retry loading.");
            }
        }
        finally
        {
            handoffLease?.Dispose();
            conversionOffer?.Dispose();
            if (IsCurrentOpenVinoLifetime(lifetime))
            {
                OpenVinoRequest = null;
            }
        }
    }

    private void ApplyOpenVinoInspectingPresentation(
        string displayName,
        long lifetime)
    {
        InspectionModelCardControl.Presentation = new InspectionModelCardPresentation
        {
            DisplayMode = InspectionModelCardMode.Compact,
            BadgeState = InspectionModelBadgeState.ModelSelected,
            ModelName = displayName,
            CompactSummary = "OpenVINO GenAI package",
            FormatShortName = "OV",
            OverviewFormatBadgeText = "OpenVINO",
            FormatName = "OpenVINO GenAI IR"
        };
        InspectionProgressRows progressRows = new();
        ModelInspectionRenderKey initialKey = new(lifetime, 0);
        progressRows.Reset(initialKey);
        _openVinoProgressRows = progressRows;
        _openVinoProgressRevision = 0;
        _openVinoDisplayName = displayName;
        _openVinoPendingReadyResult = null;
        _openVinoReadyResult = null;
        _openVinoInspectionDetailsExpanded = false;
        _openVinoFinalProgressPresented = false;
        _openVinoMilestoneSequencer?.Dispose();
        if (_openVinoMotionSettings is not null)
        {
            _openVinoMotionSettings.AnimationsEnabledChanged -=
                OpenVinoMotionSettings_AnimationsEnabledChanged;
            _openVinoMotionSettings.Dispose();
        }
        _openVinoMotionSettings = _motionSettingsFactory();
        _openVinoMotionSettings.AnimationsEnabledChanged +=
            OpenVinoMotionSettings_AnimationsEnabledChanged;
        _openVinoMilestoneSequencer = new ModelInspectionMilestoneSequencer(
            _milestoneSchedulerFactory(),
            snapshot => ApplyOpenVinoMilestoneSnapshot(lifetime, snapshot),
            _openVinoMotionSettings.AnimationsEnabled);
        InspectionContentCardControl.Presentation =
            InitialInspectionProgressPresentationFactory.Create(
                progressRows,
                new InspectionStartupPresentation
                {
                    Visibility = Visibility.Visible,
                    Summary = "Starting secure local inspection",
                    AutomationName = "OpenVINO model inspection is starting."
                });
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message = "You can safely return to model selection at any time.",
            AutomationName = "Actions available while inspecting the model",
            CancelAction = new InspectionActionPresentation
            {
                Text = "Cancel inspection",
                AutomationName = "Cancel OpenVINO inspection",
                ActionId = "cancel-openvino-inspection",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    CancelOpenVinoInspection(lifetime))
            }
        };
        InspectionOutcomeCardControl.Presentation =
            InspectionOutcomePresentation.Hidden;
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        PromptCapabilitySummary.Text = string.Empty;
        PromptExecutionEvidenceText.Text = string.Empty;
        PromptBuildEvidenceText.Text = string.Empty;
    }

    private void ApplyOpenVinoRouteProgress(
        long lifetime,
        OpenVinoRouteInspectionProgress progress)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            _openVinoProgressRows is null ||
            _openVinoMilestoneSequencer is null)
        {
            return;
        }

        long revision = checked(++_openVinoProgressRevision);
        ModelInspectionProgress mapped =
            OpenVinoModelInspectionPresentationFactory.CreateProgress(progress);
        _openVinoMilestoneSequencer.Accept(new ModelInspectionViewSnapshot(
            new ModelInspectionRenderKey(lifetime, revision),
            isRunActive: true,
            isCancellationRequested: false,
            mapped,
            terminalResult: null));
    }

    private void OpenVinoMotionSettings_AnimationsEnabledChanged(
        object? sender,
        EventArgs eventArguments)
    {
        IModelInspectionMotionSettings? settings = _openVinoMotionSettings;
        if (settings is null || !ReferenceEquals(sender, settings))
        {
            return;
        }

        bool enabled = settings.AnimationsEnabled;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(_openVinoMotionSettings, settings))
            {
                _openVinoMilestoneSequencer?.SetAnimationsEnabled(enabled);
            }
        });
    }

    private void ApplyOpenVinoMilestoneSnapshot(
        long lifetime,
        ModelInspectionViewSnapshot snapshot)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            _openVinoProgressRows is null ||
            snapshot.Progress is null)
        {
            return;
        }

        _openVinoProgressRows.Apply(InspectionProgressPresentationFactory.Create(
            snapshot.Progress,
            snapshot.RenderKey));
        _openVinoMilestoneSequencer?.NotifyPresented(snapshot.RenderKey);
        if (snapshot.Progress.Stage ==
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility &&
            snapshot.Progress.StageStatus == ModelInspectionStageStatus.Completed)
        {
            _openVinoFinalProgressPresented = true;
            TryApplyOpenVinoReadyPresentation(lifetime);
        }
    }

    private void TryApplyOpenVinoReadyPresentation(long lifetime)
    {
        OpenVinoRouteInspectionResult? result = _openVinoPendingReadyResult;
        if (!IsCurrentOpenVinoLifetime(lifetime) ||
            _openVinoCancellation?.IsCancellationRequested == true ||
            !_openVinoFinalProgressPresented ||
            result is null)
        {
            return;
        }

        _openVinoPendingReadyResult = null;
        _openVinoReadyResult = result;
        bool enqueued = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrentOpenVinoLifetime(lifetime) &&
                ReferenceEquals(_openVinoReadyResult, result))
            {
                ApplyOpenVinoReadyPresentation(result);
            }
        });
        if (!enqueued)
        {
            ApplyOpenVinoReadyPresentation(result);
        }
    }

    private void ApplyOpenVinoReadyPresentation(
        OpenVinoRouteInspectionResult result,
        bool announce = true)
    {
        bool warnings = result.Outcome ==
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings;
        _openVinoReadyResult = result;
        OpenVinoStaticPackageEvidence evidence = result.CompatibilityFacts ??
            throw new InvalidOperationException(
                "A ready OpenVINO result requires static package evidence.");
        OpenVinoReadyPresentation presentation =
            OpenVinoModelInspectionPresentationFactory.CreateReady(
                _openVinoDisplayName,
                evidence,
                warnings,
                _openVinoInspectionDetailsExpanded,
                new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty)),
                _openVinoHardwareRouteAvailable &&
                    _openVinoHardwareHandoff is not null
                    ? new DelegateCommand(_ => RequestOpenVinoHardwareInspection())
                    : null);
        InspectionModelCardControl.Presentation = presentation.Model;
        InspectionOutcomeCardControl.Presentation = presentation.Outcome;
        InspectionContentCardControl.Presentation = presentation.Content;
        InspectionActionCardControl.Presentation = presentation.Actions;
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        if (announce)
        {
            AnnouncePromptStatus(warnings
                ? "OpenVINO inspection is ready with warnings."
                : "OpenVINO inspection is ready.");
        }
    }

    private bool TryHandleOpenVinoDisclosureToggle(
        object? sender,
        InspectionDisclosureToggleRequestedEventArgs eventArguments)
    {
        OpenVinoRouteInspectionResult? result = _openVinoReadyResult;
        bool warnings = result?.Outcome ==
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings;
        if (result is null ||
            !(warnings
                ? ReferenceEquals(sender, InspectionContentCardControl)
                : ReferenceEquals(sender, InspectionModelCardControl)))
        {
            return false;
        }

        _openVinoInspectionDetailsExpanded = eventArguments.IsExpanded;
        if (warnings)
        {
            InspectionContentCardControl.ClaimDisclosureTarget(
                eventArguments.IsExpanded);
        }
        else
        {
            InspectionModelCardControl.ClaimDisclosureTarget(
                eventArguments.IsExpanded);
        }
        ApplyOpenVinoReadyPresentation(result, announce: false);
        return true;
    }

    private void CancelOpenVinoInspection(long lifetime)
    {
        if (!IsCurrentOpenVinoLifetime(lifetime))
        {
            return;
        }

        _openVinoPendingReadyResult = null;
        _openVinoReadyResult = null;
        _openVinoCancellation?.Cancel();
        _openVinoHardwareLease?.Dispose();
        _openVinoHardwareLease = null;
        _openVinoAuthoritativeHandoff = null;
        _openVinoHardwareHandoff = null;
        _openVinoConfiguration = null;
        _openVinoCompatibilityFacts = null;
        ApplyOpenVinoCancelledPresentation();
    }

    private void ApplyOpenVinoHardwareAction()
    {
        if (_openVinoReadyResult is not null)
        {
            ApplyOpenVinoReadyPresentation(_openVinoReadyResult);
        }
    }

    private void RequestOpenVinoHardwareInspection()
    {
        if (!_openVinoHardwareRouteAvailable || _openVinoHardwareHandoff is null)
        {
            return;
        }

        HardwareInspectionRequested?.Invoke(
            this,
            new HardwareInspectionRequestedEventArgs(_openVinoHardwareHandoff));
    }

    private void SetOpenVinoHardwareRouteAvailable(bool isAvailable)
    {
        _openVinoHardwareRouteAvailable = isAvailable;
        if (_openVinoHardwareHandoff is not null)
        {
            ApplyOpenVinoHardwareAction();
        }
    }

    private ModelInspectionHandoff? ReissueOpenVinoHardwareHandoff()
    {
        ModelInspectionHandoff? prior = _openVinoHardwareHandoff;
        if (prior is null || _openVinoAuthoritativeHandoff is null)
        {
            return null;
        }

        _openVinoHardwareHandoff = new ModelInspectionHandoff(
            prior.SchemaVersion,
            Guid.NewGuid(),
            prior.ModelInspectionRunId,
            prior.Outcome,
            prior.ModelSha256,
            prior.ModelLengthBytes,
            ModelInspectionRouteKind.OpenVino);
        return _openVinoHardwareHandoff;
    }

    internal bool TryResolveOpenVinoInspection(
        ModelInspectionHandoff handoff,
        out ModelInspectionHandoffV2? authoritative,
        out OpenVinoConfigurationCandidate? configuration,
        out OpenVinoStaticPackageEvidence? compatibilityFacts,
        out OpenVinoBuildEvidence? buildEvidence)
    {
        authoritative = null;
        configuration = null;
        compatibilityFacts = null;
        buildEvidence = null;
        if (handoff.Route != ModelInspectionRouteKind.OpenVino ||
            _openVinoAuthoritativeHandoff is null ||
            _openVinoHardwareLease is null ||
            handoff.ModelInspectionRunId != _openVinoAuthoritativeHandoff.ModelInspectionRunId ||
            handoff.ModelSha256 != _openVinoAuthoritativeHandoff.ModelSha256 ||
            handoff.ModelLengthBytes != _openVinoAuthoritativeHandoff.ModelLengthBytes)
        {
            return false;
        }

        authoritative = _openVinoAuthoritativeHandoff;
        configuration = _openVinoConfiguration;
        compatibilityFacts = _openVinoCompatibilityFacts;
        buildEvidence = _openVinoRouteService?.ExpectedBuildEvidence;
        return configuration is not null && compatibilityFacts is not null &&
            buildEvidence is not null;
    }

    internal async Task<OptimizationExecutionResult> ExecuteOpenVinoOptimizationAsync(
        OptimizationExecutionPlan plan,
        IOpenVinoOptimizationCurrentStateProvider currentStateProvider,
        string? selectedDestinationDirectory,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentStateProvider);
        if (_openVinoRouteService is null ||
            string.IsNullOrWhiteSpace(_openVinoPackageDirectory) ||
            !plan.IsExecutableBy(2) ||
            plan.ExecutionPayload.Route != OptimizationRoute.OpenVino)
        {
            throw new InvalidOperationException("openvino_journey_stale");
        }

        string destination;
        if (plan.ProducesPersistentArtifact)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedDestinationDirectory);
            destination = Path.GetFullPath(selectedDestinationDirectory);
        }
        else
        {
            string profileRoot = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                "OpenVino",
                "RuntimeProfiles");
            Directory.CreateDirectory(profileRoot);
            destination = Path.Combine(
                profileRoot,
                plan.OptimizationPlanId.ToString("N"));
        }

        OpenVinoOptimizationService service =
            ModelInspectionServiceComposition.CreateDefaultOpenVinoOptimizationService(
                _openVinoRouteService);
        return await service.ExecuteAsync(
            new OpenVinoOptimizationRequest(
                _openVinoPackageDirectory,
                destination,
                plan,
                currentStateProvider,
                Confirmed: true),
            progress,
            cancellationToken).ConfigureAwait(true);
    }

    private void ApplyOpenVinoNonReadyPresentation(
        OpenVinoRouteInspectionResult result)
    {
        StopOpenVinoProgressPlayback();
        (InspectionOutcomePresentationKind kind, InspectionContentCardMode mode,
            string title) = result.Outcome switch
        {
            OpenVinoRouteInspectionOutcome.ConversionRequired =>
                (InspectionOutcomePresentationKind.ConversionRequired,
                    InspectionContentCardMode.ConversionRequired,
                    "Conversion required"),
            OpenVinoRouteInspectionOutcome.IncompletePackage =>
                (InspectionOutcomePresentationKind.IncompletePackage,
                    InspectionContentCardMode.IncompletePackage,
                    "Incomplete package"),
            OpenVinoRouteInspectionOutcome.Unsupported =>
                (InspectionOutcomePresentationKind.Unsupported,
                    InspectionContentCardMode.Unsupported,
                    "Unsupported package"),
            _ =>
                (InspectionOutcomePresentationKind.Invalid,
                    InspectionContentCardMode.Invalid,
                    "Invalid package")
        };
        bool conversion = result.Outcome ==
            OpenVinoRouteInspectionOutcome.ConversionRequired;
        string message = result.Failure?.Message ?? (conversion
            ? "This supported Granite source must be converted before local prompting."
            : "The selected package cannot continue to local prompting.");
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = kind,
            Tone = conversion ? InspectionOutcomeTone.Warning : InspectionOutcomeTone.Error,
            GlyphKind = conversion ? InspectionStatusGlyphKind.Warning : InspectionStatusGlyphKind.Error,
            Title = title,
            Message = message,
            AutomationName = $"OpenVINO inspection. {title}. {message}"
        };
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = mode,
            SectionTitle = title,
            SupportingText = result.Failure?.RecoveryAction ??
                "Choose another model package.",
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = result.Failure?.SupportCode ??
                (conversion ? "conversion_required" : "package_invalid"),
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = conversion
                ? InspectionContentStatus.Warning
                : InspectionContentStatus.Error
        };
        if (conversion) ApplyOpenVinoConversionAction();
        else ApplyChooseAnotherAction(title);
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
    }

    private void ApplyOpenVinoConversionAction()
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Prepare this model",
            Message = "Create a verified FP16 OpenVINO package beside the selected source.",
            AutomationName = "OpenVINO conversion actions",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Convert to OpenVINO",
                AutomationName = "Convert model to OpenVINO",
                ActionId = "convert-openvino",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ => BeginOpenVinoConversion())
            }
        };
    }

    private async void BeginOpenVinoConversion()
    {
        OpenVinoConversionOffer? offer = _conversionOffer;
        if (offer is null || _openVinoRouteService is null ||
            _openVinoCancellation is null)
        {
            return;
        }
        ContentDialog confirmation = new()
        {
            XamlRoot = XamlRoot,
            Title = "Create an OpenVINO package?",
            Content = "A new FP16 package will be created beside the selected source. The source will not be changed.",
            PrimaryButtonText = "Convert",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _conversionOffer = null;
        long lifetime = _openVinoLifetime;
        try
        {
            _conversionService ??=
                ModelInspectionServiceComposition.CreateDefaultOpenVinoConversionService(
                    _openVinoRouteService);
        }
        catch (Exception)
        {
            offer.Dispose();
            ApplyOpenVinoConversionFailure(OpenVinoSupportCode.RuntimeIntegrityFailed);
            return;
        }
        ApplyOpenVinoConvertingPresentation();
        CurrentOpenVinoConversionTask = ConvertAndActivateOpenVinoAsync(
            offer,
            lifetime,
            _openVinoCancellation.Token);
        await CurrentOpenVinoConversionTask;
    }

    private async Task ConvertAndActivateOpenVinoAsync(
        OpenVinoConversionOffer offer,
        long lifetime,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenVinoConversionResult converted = await _conversionService!.ConvertAsync(
                offer,
                confirmed: true,
                new Progress<OpenVinoConversionProgress>(value =>
                {
                    if (IsCurrentOpenVinoLifetime(lifetime))
                    {
                        ApplyOpenVinoConversionProgress(value.Stage);
                    }
                }),
                cancellationToken);
            if (!IsCurrentOpenVinoLifetime(lifetime)) return;
            if (converted.Status == OpenVinoConversionStatus.Cancelled)
            {
                ApplyOpenVinoConversionFailure(OpenVinoSupportCode.OperationCancelled);
                return;
            }
            if (converted.Status != OpenVinoConversionStatus.Published ||
                converted.PublishedDirectory is null)
            {
                ApplyOpenVinoConversionFailure(converted.SupportCode ??
                    OpenVinoSupportCode.ConversionFailed);
                return;
            }

            ApplyOpenVinoInspectingPresentation(_openVinoDisplayName, lifetime);
            IProgress<OpenVinoRouteInspectionProgress> inspectionProgress =
                new InlineProgress<OpenVinoRouteInspectionProgress>(progress =>
                    DispatcherQueue.TryEnqueue(() =>
                        ApplyOpenVinoRouteProgress(lifetime, progress)));
            OpenVinoRouteInspectionResult inspection = await Task.Run(
                () => _openVinoRouteService!.InspectAsync(
                    converted.PublishedDirectory,
                    inspectionProgress,
                    cancellationToken),
                cancellationToken);
            OpenVinoRouteHandoffLease? lease = inspection.HandoffLease;
            try
            {
                if (inspection.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings) || lease is null)
                {
                    ApplyOpenVinoConversionFailure(OpenVinoSupportCode.ConversionOutputInvalid);
                    return;
                }
                if (!IsCurrentOpenVinoLifetime(lifetime) ||
                    cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                _openVinoConversionSourceRequest = null;
                if (!OpenVinoHardwareHandoffAdapter.TryProject(
                        lease.Handoff,
                        out ModelInspectionHandoff? hardwareHandoff))
                {
                    ApplyOpenVinoConversionFailure(OpenVinoSupportCode.ConversionOutputInvalid);
                    return;
                }

                _openVinoHardwareLease?.Dispose();
                _openVinoHardwareLease = lease;
                lease = null;
                _openVinoAuthoritativeHandoff = _openVinoHardwareLease.Handoff;
                _openVinoHardwareHandoff = hardwareHandoff;
                _openVinoConfiguration = inspection.Configuration;
                _openVinoCompatibilityFacts = inspection.CompatibilityFacts;
                _openVinoPendingReadyResult = inspection;
                TryApplyOpenVinoReadyPresentation(lifetime);
            }
            finally
            {
                lease?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
                ApplyOpenVinoConversionFailure(OpenVinoSupportCode.OperationCancelled);
        }
        catch (Exception)
        {
            if (IsCurrentOpenVinoLifetime(lifetime))
                ApplyOpenVinoConversionFailure(OpenVinoSupportCode.ConversionFailed);
        }
        finally
        {
            offer.Dispose();
        }
    }

    private void ApplyOpenVinoConvertingPresentation()
    {
        InspectionOutcomeCardControl.Presentation = InspectionOutcomePresentation.Hidden;
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            SectionTitle = "Preparing OpenVINO package",
            Startup = new InspectionStartupPresentation
            {
                Visibility = Visibility.Visible,
                Summary = "Running verified offline conversion",
                AutomationName = "Preparing OpenVINO package. Running verified offline conversion."
            }
        };
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message = "Conversion runs locally and leaves the source unchanged.",
            CancelAction = new InspectionActionPresentation
            {
                Text = "Cancel",
                AutomationName = "Cancel OpenVINO conversion",
                ActionId = "cancel-openvino-conversion",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ => _openVinoCancellation?.Cancel())
            }
        };
    }

    private void ApplyOpenVinoConversionProgress(OpenVinoConversionStage stage)
    {
        string summary = stage switch
        {
            OpenVinoConversionStage.Preflight => "Checking source and destination",
            OpenVinoConversionStage.Converting => "Converting model and tokenizer",
            OpenVinoConversionStage.ValidatingOutput => "Validating converted package",
            OpenVinoConversionStage.SmokeTesting => "Running official CPU smoke test",
            OpenVinoConversionStage.Publishing => "Publishing complete package",
            OpenVinoConversionStage.Reinspecting => "Reinspecting published package",
            _ => "Finishing conversion"
        };
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            SectionTitle = "Preparing OpenVINO package",
            Startup = new InspectionStartupPresentation
            {
                Visibility = Visibility.Visible,
                Summary = summary,
                AutomationName = "Preparing OpenVINO package. " + summary + "."
            }
        };
    }

    private void ApplyOpenVinoConversionFailure(OpenVinoSupportCode supportCode)
    {
        StopOpenVinoProgressPlayback();
        bool cancelled = supportCode == OpenVinoSupportCode.OperationCancelled;
        bool hardware = supportCode is OpenVinoSupportCode.RuntimeLoadFailed or
            OpenVinoSupportCode.RuntimeDeviceUnavailable or
            OpenVinoSupportCode.RuntimeDeviceMismatch;
        string title = cancelled ? "Conversion cancelled" :
            hardware ? "Hardware check failed" : "Conversion failed";
        string recovery = cancelled
            ? "Inspect the source again to start a new conversion."
            : hardware
                ? "The source is suitable, but the official CPU smoke test could not complete. Retry or check the runtime installation."
                : "No package was published. Inspect the source again and retry.";
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = cancelled ? InspectionOutcomePresentationKind.Cancelled :
                InspectionOutcomePresentationKind.Invalid,
            Tone = cancelled ? InspectionOutcomeTone.Neutral : InspectionOutcomeTone.Error,
            GlyphKind = cancelled ? InspectionStatusGlyphKind.NotComplete :
                InspectionStatusGlyphKind.Error,
            Title = title,
            Message = recovery,
            AutomationName = title + ". " + recovery
        };
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = cancelled ? InspectionContentCardMode.Cancelled :
                InspectionContentCardMode.Invalid,
            SectionTitle = title,
            SupportingText = recovery,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = supportCode.ToProtocolValue(),
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = cancelled ? InspectionContentStatus.Neutral :
                InspectionContentStatus.Error
        };
        ApplyOpenVinoConversionRecoveryAction(title);
    }

    private void ApplyOpenVinoConversionRecoveryAction(string title)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = title,
            Message = "Retry the verified conversion or choose another model.",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Retry conversion",
                AutomationName = "Retry OpenVINO conversion",
                ActionId = "retry-openvino-conversion",
                Visibility = _openVinoConversionSourceRequest is null
                    ? Visibility.Collapsed
                    : Visibility.Visible,
                Command = new DelegateCommand(_ => RetryOpenVinoConversion())
            }
        };
    }

    private async void RetryOpenVinoConversion()
    {
        OpenVinoInspectionRequestedEventArgs? request = _openVinoConversionSourceRequest;
        string? packageDirectory = _openVinoPackageDirectory;
        if (request is null || string.IsNullOrWhiteSpace(packageDirectory))
        {
            return;
        }
        await RetireOpenVinoLifetime();
        ActivateOpenVinoInspection(request, packageDirectory);
    }

    private void ApplyOpenVinoCancelledPresentation()
    {
        StopOpenVinoProgressPlayback();
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Cancelled,
            Tone = InspectionOutcomeTone.Neutral,
            GlyphKind = InspectionStatusGlyphKind.NotComplete,
            Title = "Inspection cancelled",
            Message = "The local OpenVINO operation was cancelled.",
            AutomationName = "OpenVINO inspection cancelled"
        };
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Cancelled,
            SectionTitle = "Inspection cancelled"
        };
        ApplyChooseAnotherAction("Inspection cancelled");
        SetPromptSurfaceVisible(false);
    }

    private void ApplyChooseAnotherAction(string title)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = title,
            Message = "Select a different model package to continue.",
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            }
        };
    }

    private void ApplyOpenVinoFailurePresentation(
        string supportCode,
        string message,
        string recovery)
    {
        StopOpenVinoProgressPlayback();
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.OperationalFailure,
            Tone = InspectionOutcomeTone.Error,
            GlyphKind = InspectionStatusGlyphKind.Error,
            Title = "Inspection could not be completed",
            Message = message,
            AutomationName = "Model inspection could not be completed."
        };
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            SectionTitle = "Inspection did not complete",
            Items =
            [
                new InspectionContentItemPresentation
                {
                    Title = "Model result unavailable",
                    DefaultDetail = message,
                    Status = InspectionContentStatus.Error,
                    StatusText = "Not completed",
                    AutomationName =
                        $"Model result unavailable. Not completed. {message}"
                }
            ],
            SupportingText = recovery,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = supportCode,
            DiagnosticStatus = InspectionContentStatus.Error,
            DiagnosticCodeVisibility = Visibility.Visible
        };
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Inspection could not be completed",
            Message = recovery,
            AutomationName = "Actions after an incomplete model inspection",
            SecondaryActionOne = new InspectionActionPresentation
            {
                Text = "Choose another model",
                AutomationName = "Choose another model",
                ActionId = "choose-another-model",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty))
            },
            PrimaryAction = new InspectionActionPresentation
            {
                Text = "Retry inspection",
                AutomationName = "Retry model inspection",
                ActionId = "retry-openvino-inspection",
                Visibility = _openVinoConversionSourceRequest is null
                    ? Visibility.Collapsed
                    : Visibility.Visible,
                Command = new DelegateCommand(_ => RetryOpenVinoInspection())
            }
        };
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
    }

    private async void RetryOpenVinoInspection()
    {
        OpenVinoInspectionRequestedEventArgs? request =
            _openVinoConversionSourceRequest;
        string? packageDirectory = _openVinoPackageDirectory;
        if (request is null || string.IsNullOrWhiteSpace(packageDirectory))
        {
            return;
        }

        await RetireOpenVinoLifetime();
        ActivateOpenVinoInspection(request, packageDirectory);
    }

    private void StopOpenVinoProgressPlayback()
    {
        _openVinoMilestoneSequencer?.Dispose();
        _openVinoMilestoneSequencer = null;
        if (_openVinoMotionSettings is not null)
        {
            _openVinoMotionSettings.AnimationsEnabledChanged -=
                OpenVinoMotionSettings_AnimationsEnabledChanged;
            _openVinoMotionSettings.Dispose();
            _openVinoMotionSettings = null;
        }
    }

    private void ApplyPromptEvent(long lifetime, PromptEvent promptEvent)
    {
        void Apply()
        {
            if (!IsCurrentOpenVinoLifetime(lifetime) ||
                _promptPresenter is null)
            {
                return;
            }

            _promptPresenter.Apply(promptEvent);
            ApplyPromptSurfaceState(_promptPresenter.State);
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Apply();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Apply);
        }
    }

    private async void PromptSendButton_Click(object sender, RoutedEventArgs e)
    {
        IPromptRouteSession? session = _promptSession;
        string prompt = PromptInput.Text;
        if (session is null || string.IsNullOrWhiteSpace(prompt))
        {
            PromptInput.Focus(FocusState.Programmatic);
            return;
        }

        LastOpenVinoTurnResult = null;
        CurrentOpenVinoPromptTask = GenerateOpenVinoPromptAsync(
            session,
            prompt,
            _openVinoCancellation?.Token ?? CancellationToken.None);
        await CurrentOpenVinoPromptTask;
    }

    private async Task GenerateOpenVinoPromptAsync(
        IPromptRouteSession session,
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            LastOpenVinoTurnResult = await session.GenerateAsync(
                prompt,
                RequestedOpenVinoNewTokens,
                cancellationToken);
            if (LastOpenVinoTurnResult.Status == PromptTurnStatus.Failed)
            {
                Interlocked.CompareExchange(
                    ref _promptSession,
                    null,
                    session);
                await session.DisposeAsync();
            }
        }
        catch (OperationCanceledException)
        {
            PromptInput.Focus(FocusState.Programmatic);
        }
        catch (Exception)
        {
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local prompt could not continue.",
                "Close the session and inspect the model again.");
        }
    }

    private async void PromptStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_promptSession is not null)
        {
            CurrentOpenVinoStopTask = StopOpenVinoPromptAsync(
                _promptSession);
            await CurrentOpenVinoStopTask;
        }
    }

    private async Task StopOpenVinoPromptAsync(IPromptRouteSession session)
    {
        try
        {
            Guid confirmedTurnId = _promptPresenter?.State.ActiveTurnId ??
                throw new InvalidOperationException(
                    "No worker-confirmed generation turn is active.");
            await session.StopActiveTurnAsync(
                confirmedTurnId,
                CancellationToken.None);
        }
        catch (Exception)
        {
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local prompt could not be stopped safely.",
                "Close the session and inspect the model again.");
        }
    }

    private async void PromptCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_promptSession is not null)
        {
            CurrentOpenVinoCancelTask = CancelOpenVinoSessionAsync(
                _promptSession);
            await CurrentOpenVinoCancelTask;
        }
        PromptInput.Focus(FocusState.Programmatic);
    }

    private async Task CancelOpenVinoSessionAsync(IPromptRouteSession session)
    {
        try
        {
            Guid? activeTurnId = _promptPresenter?.State.ActiveTurnId;
            if (activeTurnId is Guid confirmedTurnId)
            {
                await session.CancelActiveTurnAsync(
                    confirmedTurnId,
                    CancellationToken.None);
            }
            else
            {
                await session.CancelAsync(CancellationToken.None);
            }
        }
        catch (Exception)
        {
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local session could not be cancelled safely.",
                "Close this page and inspect the model again.");
        }
    }

    private void SetPromptSurfaceVisible(bool visible)
    {
        Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PromptSurface.Visibility = visibility;
        ActionToPromptGap.Visibility = visibility;
    }

    private void SetPromptControlsEnabled(bool send, bool stop, bool cancel)
    {
        PromptSendButton.IsEnabled = send;
        PromptStopButton.IsEnabled = stop;
        PromptCancelButton.IsEnabled = cancel;
    }

    private void ApplyPromptSurfaceState(PromptSurfaceState state)
    {
        PromptCapabilitySummary.Text = state.CapabilitySummary;
        PromptExecutionEvidenceText.Text = state.ExecutionEvidence;
        PromptBuildEvidenceText.Text = state.BuildEvidence;
        PromptResponseText.Text = state.ResponseText;
        SetPromptControlsEnabled(
            state.SendEnabled,
            state.StopEnabled,
            state.CancelEnabled);
        if (state.SendEnabled ||
            (!state.SendEnabled && !state.StopEnabled && !state.CancelEnabled))
        {
            PromptInput.Focus(FocusState.Programmatic);
        }
        if (!string.IsNullOrWhiteSpace(state.Announcement))
        {
            AnnouncePromptStatus(state.Announcement);
        }
    }

    private void AnnouncePromptStatus(string text)
    {
        AutomationProperties.SetName(PromptResponseText, text);
        AutomationPeer peer = FrameworkElementAutomationPeer.FromElement(
            PromptResponseText) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(PromptResponseText);
        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private bool IsCurrentOpenVinoLifetime(long lifetime) =>
        lifetime == _openVinoLifetime && _openVinoCancellation is not null;

    private Task RetireOpenVinoLifetime()
    {
        lock (_openVinoRetirementLock)
        {
            if (_openVinoCancellation is null &&
                _promptSession is null &&
                OpenVinoRequest is null)
            {
                return CurrentOpenVinoCleanupTask ?? Task.CompletedTask;
            }

            CancellationTokenSource? cancellation =
                Interlocked.Exchange(ref _openVinoCancellation, null);
            IPromptRouteSession? session =
                Interlocked.Exchange(ref _promptSession, null);
            _promptPresenter = null;
            Task? inspectionTask = CurrentOpenVinoInspectionTask;
            Task? conversionTask = CurrentOpenVinoConversionTask;
            Task? promptTask = CurrentOpenVinoPromptTask;
            Task? stopTask = CurrentOpenVinoStopTask;
            Task? cancelTask = CurrentOpenVinoCancelTask;
            OpenVinoRequest = null;
            _openVinoConversionSourceRequest = null;
            _openVinoPackageDirectory = null;
            Interlocked.Exchange(ref _openVinoHardwareLease, null)?.Dispose();
            _openVinoAuthoritativeHandoff = null;
            _openVinoHardwareHandoff = null;
            _openVinoConfiguration = null;
            _openVinoCompatibilityFacts = null;
            _openVinoPendingReadyResult = null;
            _openVinoReadyResult = null;
            _openVinoInspectionDetailsExpanded = false;
            _openVinoFinalProgressPresented = false;
            _openVinoProgressRows = null;
            _openVinoMilestoneSequencer?.Dispose();
            _openVinoMilestoneSequencer = null;
            if (_openVinoMotionSettings is not null)
            {
                _openVinoMotionSettings.AnimationsEnabledChanged -=
                    OpenVinoMotionSettings_AnimationsEnabledChanged;
                _openVinoMotionSettings.Dispose();
                _openVinoMotionSettings = null;
            }
            Interlocked.Exchange(ref _conversionOffer, null)?.Dispose();
            checked
            {
                _openVinoLifetime++;
            }
            cancellation?.Cancel();
            CurrentOpenVinoCleanupTask = CleanupOpenVinoAsync(
                session,
                cancellation,
                inspectionTask,
                conversionTask,
                promptTask,
                stopTask,
                cancelTask);
            return CurrentOpenVinoCleanupTask;
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private static async Task CleanupOpenVinoAsync(
        IPromptRouteSession? session,
        CancellationTokenSource? cancellation,
        params Task?[] activeTasks)
    {
        try
        {
            foreach (Task activeTask in activeTasks
                         .Where(static task => task is not null)
                         .Cast<Task>()
                         .Distinct())
            {
                try
                {
                    await activeTask.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Retired UI ignores task presentation failures; owned
                    // channel cleanup below remains authoritative.
                }
            }

            if (session is not null)
            {
                try
                {
                    await session.CancelAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Disposal still owns the terminal resource release.
                }
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    internal void ActivateOpenVinoInspection(
        OpenVinoInspectionRequestedEventArgs request,
        string packageDirectory)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        RetireOpenVinoLifetime();
        OpenVinoRequest = request;
        _openVinoPackageDirectory = Path.GetFullPath(packageDirectory);
        BeginOpenVinoInspection(request, _openVinoPackageDirectory);
    }

    internal async Task RetireOpenVinoInspectionAsync()
    {
        await RetireOpenVinoLifetime();
    }

    internal Task RetireForNavigationAsync()
    {
        lock (_navigationRetirementLock)
        {
            return _navigationRetirementTask ??=
                RetireForNavigationCoreAsync();
        }
    }

    private async Task RetireForNavigationCoreAsync()
    {
        if (NavigationRetirementOverride is not null)
        {
            await NavigationRetirementOverride();
        }
        else
        {
            await RetireOpenVinoInspectionAsync();
        }
        RetirePageLifetime();
    }
}
