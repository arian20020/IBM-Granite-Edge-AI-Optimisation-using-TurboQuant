using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using ModelOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

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
    private string? _openVinoDirectoryPath;
    private ModelInspectionHandoff? _openVinoHardwareHandoff;
    private OpenVinoConfigurationCandidate? _openVinoConfiguration;
    private bool _openVinoHardwareRouteAvailable;
    private readonly object _openVinoRetirementLock = new();
    private readonly object _navigationRetirementLock = new();
    private Task? _navigationRetirementTask;
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
        string directoryPath)
    {
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
        long lifetime = checked(++_openVinoLifetime);
        ApplyOpenVinoInspectingPresentation(request.DisplayName);
        CurrentOpenVinoInspectionTask = InspectAndStartOpenVinoAsync(
            service,
            request,
            directoryPath,
            lifetime,
            cancellation.Token);
    }

    private async Task InspectAndStartOpenVinoAsync(
        OpenVinoRouteService service,
        OpenVinoInspectionRequestedEventArgs request,
        string directoryPath,
        long lifetime,
        CancellationToken cancellationToken)
    {
        OpenVinoRouteHandoffLease? handoffLease = null;
        OpenVinoConversionOffer? conversionOffer = null;
        try
        {
            OpenVinoRouteInspectionResult result = await Task.Run(
                () => service.InspectAsync(directoryPath, cancellationToken),
                cancellationToken);
            handoffLease = result.HandoffLease;
            conversionOffer = result.ConversionOffer;
            if (!IsCurrentOpenVinoLifetime(lifetime))
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

            PrepareOpenVinoHardwareHandoff(result);
            ApplyOpenVinoReadyPresentation(result);
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

    private void ApplyOpenVinoInspectingPresentation(string displayName)
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
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            SectionTitle = "Checking OpenVINO package",
            Startup = new InspectionStartupPresentation
            {
                Visibility = Visibility.Visible,
                Summary = "Starting secure local inspection",
                AutomationName = "Checking OpenVINO package. Starting secure local inspection."
            }
        };
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message = "Inspection runs locally in a protected worker.",
            CancelAction = new InspectionActionPresentation
            {
                Text = "Cancel",
                AutomationName = "Cancel OpenVINO inspection",
                ActionId = "cancel-openvino-inspection",
                Visibility = Visibility.Visible,
                Command = new DelegateCommand(_ =>
                    _openVinoCancellation?.Cancel())
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

    private void ApplyOpenVinoReadyPresentation(
        OpenVinoRouteInspectionResult result)
    {
        bool warnings = result.Outcome ==
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings;
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = warnings
                ? InspectionOutcomePresentationKind.ReadyWithWarnings
                : InspectionOutcomePresentationKind.Ready,
            Tone = warnings ? InspectionOutcomeTone.Warning : InspectionOutcomeTone.Success,
            GlyphKind = warnings
                ? InspectionStatusGlyphKind.Warning
                : InspectionStatusGlyphKind.Success,
            Title = warnings ? "Ready with warnings" : "Ready",
            Message = warnings
                ? "The package is ready for local CPU prompting with a non-blocking chat-template warning."
                : "The package is ready for local CPU prompting.",
            AutomationName = warnings
                ? "OpenVINO inspection ready with warnings"
                : "OpenVINO inspection ready"
        };
        InspectionContentCardControl.Presentation = warnings
            ? new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Warnings,
                SectionTitle = "OpenVINO package warning",
                SupportingText = "Prompt formatting may differ because the package has no embedded chat template.",
                SupportingTextVisibility = Visibility.Visible
            }
            : InspectionContentCardPresentation.Hidden;
        ApplyOpenVinoHardwareAction(warnings);
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
        AnnouncePromptStatus(warnings
            ? "OpenVINO model inspection completed with warnings. Continue to the hardware check."
            : "OpenVINO model inspection completed. Continue to the hardware check.");
    }

    private void PrepareOpenVinoHardwareHandoff(
        OpenVinoRouteInspectionResult result)
    {
        ModelInspectionHandoffV2 source = result.Handoff
            ?? throw new InvalidOperationException("A ready OpenVINO result requires a handoff.");
        ModelOutcome outcome = source.Outcome ==
            GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome.Ready
                ? ModelOutcome.Ready
                : ModelOutcome.ReadyWithWarnings;
        _openVinoHardwareHandoff = new ModelInspectionHandoff(
            ModelInspectionHandoff.CurrentSchemaVersion,
            source.ModelInspectionHandoffId,
            source.ModelInspectionRunId,
            outcome,
            source.ModelSha256,
            source.ModelLengthBytes);
        _openVinoConfiguration = result.Configuration;
    }

    private void ApplyOpenVinoHardwareAction(bool warnings)
    {
        InspectionActionCardControl.Presentation = new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = warnings ? "Model is ready with warnings" : "Model is ready",
            Message = "Check this model against the memory and devices available on this computer.",
            AutomationName = "Actions after OpenVINO model inspection",
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
                Text = "Check hardware fit",
                AutomationName = "Check model hardware fit",
                ActionId = "hardware-fit",
                Visibility = Visibility.Visible,
                IsEnabled = _openVinoHardwareRouteAvailable &&
                    _openVinoHardwareHandoff is not null,
                AutomationHelpText = _openVinoHardwareRouteAvailable
                    ? string.Empty
                    : "Hardware inspection is not available.",
                Command = new DelegateCommand(_ => RequestOpenVinoHardwareInspection())
            }
        };
    }

    private void RequestOpenVinoHardwareInspection()
    {
        if (!_openVinoHardwareRouteAvailable ||
            _openVinoHardwareHandoff is not { } handoff)
        {
            return;
        }

        HardwareInspectionRequested?.Invoke(
            this,
            new HardwareInspectionRequestedEventArgs(handoff));
    }

    internal void SetOpenVinoHardwareRouteAvailable(bool isAvailable)
    {
        _openVinoHardwareRouteAvailable = isAvailable;
        if (_openVinoHardwareHandoff is not null)
        {
            ApplyOpenVinoHardwareAction(
                _openVinoHardwareHandoff.Outcome == ModelOutcome.ReadyWithWarnings);
        }
    }

    internal bool TryGetOpenVinoCompatibilityEvidence(
        ModelInspectionHandoff handoff,
        out OpenVinoStaticPackageEvidence? evidence)
    {
        evidence = null;
        if (_openVinoHardwareHandoff is null ||
            !ReferenceEquals(handoff, _openVinoHardwareHandoff) ||
            string.IsNullOrWhiteSpace(_openVinoDirectoryPath) ||
            _openVinoConfiguration is null)
        {
            return false;
        }

        OpenVinoStaticPackageInspectionResult current =
            new OpenVinoStaticPackageInspector().Inspect(_openVinoDirectoryPath);
        if (current.Status != OpenVinoStaticInspectionStatus.NativeValidationRequired ||
            current.Evidence is not { } candidate ||
            !string.Equals(candidate.ModelSha256, handoff.ModelSha256,
                StringComparison.Ordinal) ||
            candidate.ModelLengthBytes != handoff.ModelLengthBytes)
        {
            return false;
        }

        evidence = candidate;
        return true;
    }

    internal bool TryGetOpenVinoSourceDirectory(
        ModelInspectionHandoff handoff,
        out string? directoryPath)
    {
        directoryPath = null;
        if (_openVinoHardwareHandoff is null ||
            !ReferenceEquals(handoff, _openVinoHardwareHandoff) ||
            string.IsNullOrWhiteSpace(_openVinoDirectoryPath))
        {
            return false;
        }

        directoryPath = _openVinoDirectoryPath;
        return true;
    }

    internal bool TryCreateOpenVinoOptimizationService(
        out OpenVinoOptimizationService? service,
        out OpenVinoBuildEvidence? buildEvidence)
    {
        service = null;
        buildEvidence = _openVinoRouteService?.ExpectedBuildEvidence;
        if (_openVinoRouteService is null || buildEvidence is null)
        {
            return false;
        }
        try
        {
            service = ModelInspectionServiceComposition
                .CreateDefaultOpenVinoOptimizationService(_openVinoRouteService);
            return true;
        }
        catch
        {
            service = null;
            buildEvidence = null;
            return false;
        }
    }

    internal async Task<bool> ActivateOpenVinoChatAsync(
        CancellationToken cancellationToken) =>
        await ActivateOpenVinoChatFromDirectoryAsync(
            _openVinoDirectoryPath, cancellationToken);

    internal async Task<bool> ActivateOpenVinoChatFromDirectoryAsync(
        string? packageDirectory,
        CancellationToken cancellationToken)
    {
        if (_openVinoRouteService is null
            || _promptRouteRegistry is null
            || string.IsNullOrWhiteSpace(packageDirectory))
        {
            return false;
        }
        OpenVinoRouteHandoffLease? lease = null;
        try
        {
            OpenVinoRouteInspectionResult inspection =
                await _openVinoRouteService.InspectAsync(
                    packageDirectory, cancellationToken);
            lease = inspection.HandoffLease;
            if (lease is null || inspection.Outcome is not (
                    OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings))
            {
                return false;
            }
            long lifetime = _openVinoLifetime;
            List<PromptEvent> buffered = [];
            object eventGate = new();
            bool presenterReady = false;
            PromptRouteSessionActivation activation =
                await _promptRouteRegistry.ActivateAsync(
                    lease,
                    promptEvent =>
                    {
                        lock (eventGate)
                        {
                            if (!presenterReady)
                            {
                                buffered.Add(promptEvent);
                                return;
                            }
                        }
                        ApplyPromptEvent(lifetime, promptEvent);
                    },
                    cancellationToken);
            lease = null;
            _promptSession = activation.Session;
            _promptPresenter = new PromptSessionPresenter(activation.Presentation);
            lock (eventGate)
            {
                presenterReady = true;
                foreach (PromptEvent promptEvent in buffered)
                {
                    _promptPresenter.Apply(promptEvent);
                }
                buffered.Clear();
            }
            SetPromptSurfaceVisible(true);
            ApplyPromptSurfaceState(_promptPresenter.State);
            AnnouncePromptStatus("OpenVINO local chat is ready.");
            return IsCurrentOpenVinoLifetime(lifetime);
        }
        catch
        {
            return false;
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private void ApplyOpenVinoNonReadyPresentation(
        OpenVinoRouteInspectionResult result)
    {
        OpenVinoInspectionPresentationDisposition disposition =
            OpenVinoInspectionPresentationPolicy.Create(result);
        if (disposition.Kind == OpenVinoInspectionPresentationKind.Cancelled)
        {
            ApplyOpenVinoCancelledPresentation();
            return;
        }

        (InspectionOutcomePresentationKind kind, InspectionContentCardMode mode) =
            disposition.Kind switch
        {
            OpenVinoInspectionPresentationKind.ConversionRequired =>
                (InspectionOutcomePresentationKind.ConversionRequired,
                    InspectionContentCardMode.ConversionRequired),
            OpenVinoInspectionPresentationKind.IncompletePackage =>
                (InspectionOutcomePresentationKind.IncompletePackage,
                    InspectionContentCardMode.IncompletePackage),
            OpenVinoInspectionPresentationKind.Unsupported =>
                (InspectionOutcomePresentationKind.Unsupported,
                    InspectionContentCardMode.Unsupported),
            OpenVinoInspectionPresentationKind.OperationalFailure =>
                (InspectionOutcomePresentationKind.OperationalFailure,
                    InspectionContentCardMode.OperationalFailure),
            OpenVinoInspectionPresentationKind.Invalid =>
                (InspectionOutcomePresentationKind.Invalid,
                    InspectionContentCardMode.Invalid),
            OpenVinoInspectionPresentationKind.Cancelled =>
                throw new InvalidOperationException(
                    "Cancelled OpenVINO presentation must use its established state."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition),
                disposition.Kind,
                "Unknown OpenVINO presentation disposition.")
        };
        bool conversion = disposition.Kind ==
            OpenVinoInspectionPresentationKind.ConversionRequired;
        InspectionOutcomeTone tone = disposition.IsWarning
            ? InspectionOutcomeTone.Warning
            : InspectionOutcomeTone.Error;
        InspectionStatusGlyphKind glyphKind = disposition.IsWarning
            ? InspectionStatusGlyphKind.Warning
            : InspectionStatusGlyphKind.Error;
        InspectionContentStatus diagnosticStatus = disposition.IsWarning
            ? InspectionContentStatus.Warning
            : InspectionContentStatus.Error;
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = kind,
            Tone = tone,
            GlyphKind = glyphKind,
            Title = disposition.Title,
            Message = disposition.Message,
            AutomationName = $"OpenVINO inspection. {disposition.Title}. " +
                disposition.Message
        };
        InspectionContentCardControl.Presentation = new InspectionContentCardPresentation
        {
            Mode = mode,
            SectionTitle = disposition.Title,
            SupportingText = disposition.RecoveryAction,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = disposition.DiagnosticCode,
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = diagnosticStatus
        };
        if (conversion) ApplyOpenVinoConversionAction();
        else ApplyChooseAnotherAction(disposition.Title);
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

            OpenVinoRouteInspectionResult inspection = await _openVinoRouteService!
                .InspectAsync(converted.PublishedDirectory, cancellationToken);
            OpenVinoRouteHandoffLease? lease = inspection.HandoffLease;
            try
            {
                if (inspection.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings) || lease is null)
                {
                    ApplyOpenVinoConversionFailure(OpenVinoSupportCode.ConversionOutputInvalid);
                    return;
                }
                _openVinoDirectoryPath = converted.PublishedDirectory;
                PrepareOpenVinoHardwareHandoff(inspection);
                _openVinoConversionSourceRequest = null;
                ApplyOpenVinoReadyPresentation(inspection);
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
        string? directoryPath = _openVinoDirectoryPath;
        if (request is null || string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }
        await RetireOpenVinoLifetime();
        ActivateOpenVinoInspection(request, directoryPath);
    }

    private void ApplyOpenVinoCancelledPresentation()
    {
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
        PromptFailure failure = new(supportCode, message, recovery);
        _promptPresenter ??= new PromptSessionPresenter(
            new PromptRoutePresentation(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        _promptPresenter.ApplyFailure(failure);
        SetPromptSurfaceVisible(true);
        ApplyPromptSurfaceState(_promptPresenter.State);
        PromptInput.Focus(FocusState.Programmatic);
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

    private static bool IsPromptSendKey(VirtualKey key, bool isShiftPressed) =>
        key == VirtualKey.Enter && !isShiftPressed;

    private async void PromptInput_PreviewKeyDown(
        object sender,
        KeyRoutedEventArgs eventArguments)
    {
        bool isShiftPressed = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (!IsPromptSendKey(eventArguments.Key, isShiftPressed))
        {
            return;
        }

        eventArguments.Handled = true;
        Task? promptTask = StartOpenVinoPrompt();
        if (promptTask is not null)
        {
            await promptTask;
        }
    }

    private async void PromptSendButton_Click(object sender, RoutedEventArgs e)
    {
        Task? promptTask = StartOpenVinoPrompt();
        if (promptTask is not null)
        {
            await promptTask;
        }
    }

    private Task? StartOpenVinoPrompt()
    {
        IPromptRouteSession? session = _promptSession;
        string prompt = PromptInput.Text;
        if (session is null || string.IsNullOrWhiteSpace(prompt))
        {
            PromptInput.Focus(FocusState.Programmatic);
            return null;
        }

        LastOpenVinoTurnResult = null;
        CurrentOpenVinoPromptTask = GenerateOpenVinoPromptAsync(
            session,
            prompt,
            _openVinoCancellation?.Token ?? CancellationToken.None);
        return CurrentOpenVinoPromptTask;
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
            _openVinoDirectoryPath = null;
            _openVinoHardwareHandoff = null;
            _openVinoConfiguration = null;
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
        string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        RetireOpenVinoLifetime();
        OpenVinoRequest = request;
        _openVinoDirectoryPath = directoryPath;
        BeginOpenVinoInspection(request, directoryPath);
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
