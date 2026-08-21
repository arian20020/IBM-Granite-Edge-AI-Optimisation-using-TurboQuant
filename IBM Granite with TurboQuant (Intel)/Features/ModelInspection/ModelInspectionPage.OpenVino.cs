using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;
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
    private Func<OpenVinoRouteService> _openVinoRouteServiceFactory =
        ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService;
    private CancellationTokenSource? _openVinoCancellation;
    private OpenVinoRouteSession? _openVinoSession;
    private long _openVinoLifetime;
    private int _requestedOpenVinoNewTokens =
        OpenVinoRouteCapability.DefaultRequestedNewTokens;

    internal Task? CurrentOpenVinoInspectionTask { get; private set; }
    internal Task? CurrentOpenVinoPromptTask { get; private set; }
    internal Task? CurrentOpenVinoStopTask { get; private set; }
    internal Task? CurrentOpenVinoCancelTask { get; private set; }
    internal Task? CurrentOpenVinoCleanupTask { get; private set; }
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

    // Internal-only injection lets packaged tests exercise a damaged
    // operation-owned closure without mutating the installed app package.
    internal Func<OpenVinoRouteService> OpenVinoRouteServiceFactory
    {
        get => _openVinoRouteServiceFactory;
        set => _openVinoRouteServiceFactory = value ??
            throw new ArgumentNullException(nameof(value));
    }

    private void BeginOpenVinoInspection(
        OpenVinoInspectionRequestedEventArgs request)
    {
        OpenVinoRouteService service;
        try
        {
            service = _openVinoRouteService ??=
                _openVinoRouteServiceFactory();
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
            lifetime,
            cancellation.Token);
    }

    private async Task InspectAndStartOpenVinoAsync(
        OpenVinoRouteService service,
        OpenVinoInspectionRequestedEventArgs request,
        long lifetime,
        CancellationToken cancellationToken)
    {
        try
        {
            OpenVinoRouteInspectionResult result = await Task.Run(
                () => service.InspectAsync(request.DirectoryPath, cancellationToken),
                cancellationToken);
            if (!IsCurrentOpenVinoLifetime(lifetime))
            {
                return;
            }

            if (result.Outcome is not (
                    OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings) ||
                result.Handoff is null)
            {
                ApplyOpenVinoNonReadyPresentation(result);
                return;
            }

            OpenVinoRouteSession session = await service.StartSessionAsync(
                result.Handoff,
                promptEvent => ApplyOpenVinoPromptEvent(lifetime, promptEvent),
                cancellationToken);
            if (!IsCurrentOpenVinoLifetime(lifetime))
            {
                await session.CancelAsync(CancellationToken.None);
                await session.DisposeAsync();
                return;
            }

            _openVinoSession = session;
            ApplyOpenVinoReadyPresentation(
                result,
                service.ExpectedBuildEvidence);
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
        OpenVinoExecutionEvidenceText.Text = string.Empty;
        OpenVinoBuildEvidenceText.Text = string.Empty;
    }

    private void ApplyOpenVinoReadyPresentation(
        OpenVinoRouteInspectionResult result,
        OpenVinoBuildEvidence? buildEvidence)
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
        InspectionActionCardControl.Presentation =
            InspectionActionCardPresentation.Hidden;
        SetPromptSurfaceVisible(true);
        PromptSendButton.IsEnabled = true;
        PromptStopButton.IsEnabled = false;
        PromptCancelButton.IsEnabled = true;
        OpenVinoExecutionEvidenceText.Text = "Requested CPU · Running CPU";
        OpenVinoBuildEvidenceText.Text = buildEvidence is null
            ? "Verified official worker build"
            : $"Runtime {buildEvidence.RuntimeBuild} · " +
              $"GenAI {buildEvidence.GenAiBuild} · " +
              $"Tokenizers {buildEvidence.TokenizersBuild} · " +
              $"Worker manifest {buildEvidence.WorkerManifestDigest}";
        PromptInput.Focus(FocusState.Programmatic);
        AnnouncePromptStatus("OpenVINO CPU session ready.");
    }

    private void ApplyOpenVinoNonReadyPresentation(
        OpenVinoRouteInspectionResult result)
    {
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
        string message = result.Failure?.Message ??
            "The selected package cannot continue to local prompting.";
        InspectionOutcomeCardControl.Presentation = new InspectionOutcomePresentation
        {
            Kind = kind,
            Tone = InspectionOutcomeTone.Error,
            GlyphKind = InspectionStatusGlyphKind.Error,
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
            DiagnosticCode = result.Failure?.SupportCode ?? "package_invalid",
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = InspectionContentStatus.Error
        };
        ApplyChooseAnotherAction(title);
        SetPromptSurfaceVisible(false);
        SetPromptControlsEnabled(send: false, stop: false, cancel: false);
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
        SetPromptSurfaceVisible(true);
        PromptSendButton.IsEnabled = false;
        PromptStopButton.IsEnabled = false;
        PromptCancelButton.IsEnabled = false;
        PromptResponseText.Text = $"{message} {recovery}";
        PromptInput.Focus(FocusState.Programmatic);
        AnnouncePromptStatus($"{message} {recovery}");
    }

    private void ApplyOpenVinoPromptEvent(long lifetime, PromptEvent promptEvent)
    {
        void Apply()
        {
            if (!IsCurrentOpenVinoLifetime(lifetime))
            {
                return;
            }

            switch (promptEvent.Kind)
            {
                case PromptEventKind.GeneratingTurn:
                    PromptResponseText.Text = string.Empty;
                    PromptSendButton.IsEnabled = false;
                    PromptStopButton.IsEnabled = true;
                    break;
                case PromptEventKind.TextDelta:
                    PromptResponseText.Text += promptEvent.Text;
                    break;
                case PromptEventKind.StoppingTurn:
                    PromptStopButton.IsEnabled = false;
                    AnnouncePromptStatus("Stopping generation.");
                    break;
                case PromptEventKind.TurnCompleted:
                case PromptEventKind.SessionReady:
                    PromptSendButton.IsEnabled = true;
                    PromptStopButton.IsEnabled = false;
                    PromptInput.Focus(FocusState.Programmatic);
                    break;
                case PromptEventKind.Failed:
                    ApplyOpenVinoFailurePresentation(
                        promptEvent.Failure?.SupportCode ?? "runtime_protocol_failed",
                        promptEvent.Failure?.Message ??
                            "The local OpenVINO operation could not continue.",
                        promptEvent.Failure?.RecoveryAction ??
                            "Close the session and choose the package again.");
                    break;
                case PromptEventKind.CancellingSession:
                    PromptSendButton.IsEnabled = false;
                    PromptStopButton.IsEnabled = false;
                    AnnouncePromptStatus("Cancelling local session.");
                    break;
                case PromptEventKind.Cancelled:
                case PromptEventKind.SessionCompleted:
                    PromptSendButton.IsEnabled = false;
                    PromptStopButton.IsEnabled = false;
                    PromptInput.Focus(FocusState.Programmatic);
                    AnnouncePromptStatus("Local session closed.");
                    break;
            }
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
        OpenVinoRouteSession? session = _openVinoSession;
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
        OpenVinoRouteSession session,
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
                    ref _openVinoSession,
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
                "The local OpenVINO prompt could not continue.",
                "Close the session and inspect the package again.");
        }
    }

    private async void PromptStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_openVinoSession is not null)
        {
            CurrentOpenVinoStopTask = StopOpenVinoPromptAsync(
                _openVinoSession);
            await CurrentOpenVinoStopTask;
        }
    }

    private async Task StopOpenVinoPromptAsync(OpenVinoRouteSession session)
    {
        try
        {
            await session.StopAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local OpenVINO prompt could not be stopped safely.",
                "Close the session and inspect the package again.");
        }
    }

    private async void PromptCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_openVinoSession is not null)
        {
            CurrentOpenVinoCancelTask = CancelOpenVinoSessionAsync(
                _openVinoSession);
            await CurrentOpenVinoCancelTask;
        }
        PromptInput.Focus(FocusState.Programmatic);
    }

    private async Task CancelOpenVinoSessionAsync(OpenVinoRouteSession session)
    {
        try
        {
            await session.CancelAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            ApplyOpenVinoFailurePresentation(
                "runtime_protocol_failed",
                "The local OpenVINO session could not be cancelled safely.",
                "Close this page and inspect the package again.");
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

    private void AnnouncePromptStatus(string text)
    {
        AutomationProperties.SetName(PromptResponseText, text);
        AutomationPeer peer = FrameworkElementAutomationPeer.FromElement(
            PromptResponseText) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(PromptResponseText);
        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private bool IsCurrentOpenVinoLifetime(long lifetime) =>
        lifetime == _openVinoLifetime && OpenVinoRequest is not null;

    private void RetireOpenVinoLifetime()
    {
        CancellationTokenSource? cancellation =
            Interlocked.Exchange(ref _openVinoCancellation, null);
        OpenVinoRouteSession? session =
            Interlocked.Exchange(ref _openVinoSession, null);
        Task? inspectionTask = CurrentOpenVinoInspectionTask;
        Task? promptTask = CurrentOpenVinoPromptTask;
        Task? stopTask = CurrentOpenVinoStopTask;
        Task? cancelTask = CurrentOpenVinoCancelTask;
        OpenVinoRequest = null;
        checked
        {
            _openVinoLifetime++;
        }
        cancellation?.Cancel();
        CurrentOpenVinoCleanupTask = CleanupOpenVinoAsync(
            session,
            cancellation,
            inspectionTask,
            promptTask,
            stopTask,
            cancelTask);
    }

    private static async Task CleanupOpenVinoAsync(
        OpenVinoRouteSession? session,
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
                OpenVinoRouteState state = session.Snapshot.State;
                if (state is not (
                        OpenVinoRouteState.SessionCompleted or
                        OpenVinoRouteState.Failed or
                        OpenVinoRouteState.Cancelled))
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
        OpenVinoInspectionRequestedEventArgs request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RetireOpenVinoLifetime();
        OpenVinoRequest = request;
        BeginOpenVinoInspection(request);
    }

    internal async Task RetireOpenVinoInspectionAsync()
    {
        RetireOpenVinoLifetime();
        if (CurrentOpenVinoCleanupTask is not null)
        {
            await CurrentOpenVinoCleanupTask;
        }
    }
}
