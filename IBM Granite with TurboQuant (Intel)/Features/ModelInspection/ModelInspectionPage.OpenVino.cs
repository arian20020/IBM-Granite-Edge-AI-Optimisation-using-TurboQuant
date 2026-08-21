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
    private PromptRouteRegistry? _promptRouteRegistry;
    private CancellationTokenSource? _openVinoCancellation;
    private IPromptRouteSession? _promptSession;
    private PromptSessionPresenter? _promptPresenter;
    private readonly object _openVinoRetirementLock = new();
    private readonly object _navigationRetirementLock = new();
    private Task? _navigationRetirementTask;
    private long _openVinoLifetime;
    private int _requestedOpenVinoNewTokens =
        OpenVinoRouteCapability.DefaultRequestedNewTokens;

    internal Task? CurrentOpenVinoInspectionTask { get; private set; }
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
        OpenVinoInspectionRequestedEventArgs request)
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
            lifetime,
            cancellation.Token);
    }

    private async Task InspectAndStartOpenVinoAsync(
        OpenVinoRouteService service,
        OpenVinoInspectionRequestedEventArgs request,
        long lifetime,
        CancellationToken cancellationToken)
    {
        OpenVinoRouteHandoffLease? handoffLease = null;
        try
        {
            OpenVinoRouteInspectionResult result = await Task.Run(
                () => service.InspectAsync(request.DirectoryPath, cancellationToken),
                cancellationToken);
            handoffLease = result.HandoffLease;
            if (!IsCurrentOpenVinoLifetime(lifetime))
            {
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

            PromptRouteSessionActivation active = await _promptRouteRegistry!
                .ActivateAsync(
                handoffLease,
                promptEvent => ApplyPromptEvent(lifetime, promptEvent),
                cancellationToken);
            if (!IsCurrentOpenVinoLifetime(lifetime))
            {
                await active.Session.CancelAsync(CancellationToken.None);
                await active.Session.DisposeAsync();
                return;
            }

            _promptSession = active.Session;
            _promptPresenter = new PromptSessionPresenter(active.Presentation);
            ApplyOpenVinoReadyPresentation(
                result,
                active.Presentation);
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
        OpenVinoRouteInspectionResult result,
        PromptRoutePresentation promptPresentation)
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
        PromptCapabilitySummary.Text = promptPresentation.CapabilitySummary;
        PromptExecutionEvidenceText.Text = promptPresentation.ExecutionEvidence;
        PromptBuildEvidenceText.Text = promptPresentation.BuildEvidence;
        ApplyPromptSurfaceState(_promptPresenter!.State);
        PromptInput.Focus(FocusState.Programmatic);
        AnnouncePromptStatus(promptPresentation.ReadyAnnouncement);
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
            await session.StopAsync(CancellationToken.None);
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
        OpenVinoInspectionRequestedEventArgs request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RetireOpenVinoLifetime();
        OpenVinoRequest = request;
        BeginOpenVinoInspection(request);
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
