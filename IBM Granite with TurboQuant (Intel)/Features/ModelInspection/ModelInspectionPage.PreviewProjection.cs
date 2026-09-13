using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Presentation.Progress;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Windows.UI.ViewManagement;

namespace GraniteEdgeAI.Features.ModelInspection.Views;

/// <summary>
/// Projects the existing inspection presentation contract into the saved
/// full-journey XAML. It owns presentation only; operations remain owned by
/// the page/view-model command graph.
/// </summary>
internal sealed class ModelInspectionPreviewProjection : IModelInspectionPreviewView
{
    private static readonly string[] AllPanels =
    [
        "InspectionProgressPanel", "InspectionReadyPanel",
        "InspectionWarningPanel", "InspectionCancelledPanel",
        "InspectionFailurePanel", "OpenVinoConversionRequiredPanel",
        "OpenVinoIncompletePanel", "OpenVinoUnsupportedPanel",
        "OpenVinoTimedOutPanel", "OpenVinoInvalidOrStalePanel",
        "OpenVinoConversionProgressPanel",
        "OpenVinoConversionCancelledPanel", "OpenVinoConversionFailedPanel"
    ];

    private readonly UserControl _view;
    private readonly bool _isOpenVino;
    private readonly Dictionary<Button, RoutedEventHandler> _buttonHandlers = [];
    private readonly Dictionary<string, Button> _semanticActionButtons =
        new(StringComparer.Ordinal);
    private FrameworkElement? _actionBindingPanel;
    private bool _suppressDisclosure;
    private Expander? _activeDisclosure;
    private string _responsiveStateName = "WideInspectionState";
    private bool _motionEnabled = true;
    private bool _progressMotionActive = true;
    private object? _progressOwner;
    private readonly SerializedProgressSequence _progressSequence = new();
    private DispatcherProgressPresenter? _estimatePresenter;
    private long _estimateRevision;
    private long _estimateEpoch;
    private int _lastAuthoritativeActiveIndex = -1;
    private int _lastAuthoritativeSettledCount = -1;
    private int _displayedActiveStage = -1;
    private int _lastAnnouncedDisplayedStage = -1;
    private long _progressAnnouncementGeneration;
    private readonly string?[] _stageAnnouncements = new string?[5];
    private readonly string?[] _displayedStageDetails = new string?[5];
    private readonly SerializedProgressStageState?[] _projectedStageStates = new SerializedProgressStageState?[5];
    private readonly string?[] _projectedAccessibleStatuses = new string?[5];
    private readonly string?[] _projectedAccessibleNames = new string?[5];
    private ModelInspectionPagePresentation? _pendingSuccessfulPresentation;
    private Action? _pendingProgressCompletion;
    private bool _terminalHandoffQueued;
    private long _terminalHandoffRevision;
    private bool _openVinoCallbackCompletionFramePending;
    private object? _stoppingAnnouncementOwner;
    private long _stoppingAnnouncementEpoch = -1;
    private long _stoppingAnnouncementGeneration;

    internal ModelInspectionPreviewProjection(UserControl view, bool isOpenVino)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _isOpenVino = isOpenVino;
        _view.Loaded += (_, _) => ApplyProgressPresentation();
        _view.Unloaded += (_, _) => CancelProgressMotion();
        ModelCard = new PreviewModelCardAdapter(this);
        ContentCard = new PreviewContentCardAdapter(this);
        OutcomeCard = new PreviewOutcomeCardAdapter(this);
        ActionCard = new PreviewActionCardAdapter(this);
        foreach (string name in new[] { "ReadyDetailsExpander", "WarningDetailsExpander" })
        {
            if (FindElement(name) is not Expander expander)
            {
                continue;
            }

            expander.Expanding += Disclosure_Expanding;
            expander.Collapsed += Disclosure_Collapsed;
        }
    }

    public UserControl Element => _view;
    internal PreviewModelCardAdapter ModelCard { get; }
    internal PreviewContentCardAdapter ContentCard { get; }
    internal PreviewOutcomeCardAdapter OutcomeCard { get; }
    internal PreviewActionCardAdapter ActionCard { get; }
    public FrameworkElement ModelSurface => Require<FrameworkElement>("InspectionBay");
    public FrameworkElement ContentSurface => Require<FrameworkElement>("InspectionProgressPanel");
    public FrameworkElement OutcomeSurface => CurrentPanel;
    public FrameworkElement OutcomeFocusTarget =>
        FindElement(CurrentOutcomeHeadingName) as FrameworkElement ?? CurrentPanel;
    public Button CancelActionButton => ResolveCancelActionButton();
    public Expander? ActiveDisclosure => _activeDisclosure;
    public bool IsDisclosureExpanded => _activeDisclosure?.IsExpanded == true;
    public string ResponsiveStateName => _responsiveStateName;

    internal ModelInspectionPagePresentation? Presentation { get; private set; }
    internal InspectionModelCardPresentation ModelPresentation { get; private set; } =
        InspectionModelCardPresentation.Empty;
    internal InspectionContentCardPresentation ContentPresentation { get; private set; } =
        InspectionContentCardPresentation.Hidden;
    internal InspectionOutcomePresentation OutcomePresentation { get; private set; } =
        InspectionOutcomePresentation.Hidden;
    internal InspectionActionCardPresentation ActionPresentation { get; private set; } =
        InspectionActionCardPresentation.Hidden;

    internal int ProgressAnnouncementCount { get; private set; }
    internal int OutcomeAnnouncementCount { get; private set; }
    internal List<string> ProgressAnnouncementHistory { get; } = [];
    internal List<string> OutcomeAnnouncementHistory { get; } = [];

    public event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
        DisclosureToggleRequested;

    public object? FindElement(string name)
    {
        if (string.Equals(name, "CancelActionButton", StringComparison.Ordinal))
        {
            return _semanticActionButtons.TryGetValue(name, out Button? mappedCancel)
                ? mappedCancel
                : ResolveCancelActionButton();
        }

        if (_semanticActionButtons.TryGetValue(name, out Button? semanticButton))
        {
            return semanticButton;
        }

        return _view.FindName(name);
    }

    public void ApplyPresentation(ModelInspectionPagePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (_isOpenVino
            && IsSuccessfulTerminal(presentation.State)
            && ContentPresentation.Mode == InspectionContentCardMode.Progress
            && _progressOwner is not null)
        {
            BeginSuccessfulPresentationHandoff(presentation);
            return;
        }
        _pendingSuccessfulPresentation = null;
        ApplyPresentationCore(presentation);
    }

    private void ApplyPresentationCore(ModelInspectionPagePresentation presentation)
    {
        Presentation = presentation;
        ApplyModel(presentation.ModelCard);
        SelectPanel(presentation.State);
        ApplyContent(presentation.ContentCard);
        ApplyOutcome(presentation.OutcomeCard);
        ApplyActions(presentation.ActionCard);
    }

    private static bool IsSuccessfulTerminal(ModelInspectionFigmaState state) => state is
        ModelInspectionFigmaState.ReadyCollapsed or
        ModelInspectionFigmaState.ReadyExpanded or
        ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
        ModelInspectionFigmaState.ReadyWithWarningsExpanded;

    private void BeginSuccessfulPresentationHandoff(
        ModelInspectionPagePresentation presentation)
    {
        _pendingSuccessfulPresentation = presentation;
        BeginSuccessfulProgressCatchUp();
    }

    internal void CompleteProgressPresentation(Action completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        if (ContentPresentation.Mode != InspectionContentCardMode.Progress
            || _progressOwner is null)
        {
            completion();
            return;
        }

        _pendingProgressCompletion = completion;
        if (_isOpenVino)
        {
            PresentOpenVinoCallbackCompletionFrame();
            return;
        }
        BeginSuccessfulProgressCatchUp();
    }

    private void PresentOpenVinoCallbackCompletionFrame()
    {
        _openVinoCallbackCompletionFramePending = true;
        SerializedProgressStageObservation[] observations =
            ContentPresentation.ProgressRows.Items
                .Select(_ => new SerializedProgressStageObservation(
                    false, true, false))
                .ToArray();
        _progressSequence.Observe(
            _progressOwner!,
            ++_estimateRevision,
            observations,
            false);
        _estimateEpoch = _progressSequence.Epoch;
        UpdateEstimatedProgressValues();
    }

    private void BeginSuccessfulProgressCatchUp()
    {
        SerializedProgressStageObservation[] observations = ContentPresentation.ProgressRows.Items
            .Select(item => new SerializedProgressStageObservation(
                false, true, false)).ToArray();
        _progressSequence.Observe(
            _progressOwner!, ++_estimateRevision, observations,
            _motionEnabled && new UISettings().AnimationsEnabled);
        _estimateEpoch = _progressSequence.Epoch;
        UpdateEstimatedProgressValues();
        _estimatePresenter ??= new DispatcherProgressPresenter(
            _view, UpdateEstimatedProgressValues, TimeSpan.FromMilliseconds(16));
        _estimatePresenter.Start();
    }

    private void CompleteSuccessfulPresentationHandoff()
    {
        ModelInspectionPagePresentation? presentation = _pendingSuccessfulPresentation;
        Action? completion = _pendingProgressCompletion;
        if (presentation is null && completion is null) return;
        _pendingSuccessfulPresentation = null;
        _pendingProgressCompletion = null;
        _terminalHandoffQueued = false;
        _openVinoCallbackCompletionFramePending = false;
        _estimatePresenter?.Stop();
        if (presentation is not null) ApplyPresentationCore(presentation);
        else completion?.Invoke();
    }

    public void ApplyModel(InspectionModelCardPresentation presentation)
    {
        ModelPresentation = presentation ?? InspectionModelCardPresentation.Empty;
        SetTextAndToolTip("CompactModelName", ModelPresentation.ModelName);
        SetTextAndToolTip("CompactModelSummary", ModelPresentation.CompactSummary);
        SetText("InspectionIdentityStatusText", BadgeText(ModelPresentation.BadgeState));
        AutomationProperties.SetName(ModelSurface,
            $"Selected model {ModelPresentation.ModelName}. {ModelPresentation.CompactSummary}");

        ApplyMetadata("Ready", ModelPresentation);
        ApplyMetadata("Warning", ModelPresentation);
    }

    public void ApplyContent(InspectionContentCardPresentation presentation)
    {
        ContentPresentation = presentation ?? InspectionContentCardPresentation.Hidden;
        string? panel = ContentPresentation.Mode switch
        {
            InspectionContentCardMode.Progress => "InspectionProgressPanel",
            InspectionContentCardMode.Warnings => "InspectionWarningPanel",
            InspectionContentCardMode.ConversionRequired when _isOpenVino => "OpenVinoConversionRequiredPanel",
            InspectionContentCardMode.IncompletePackage when _isOpenVino => "OpenVinoIncompletePanel",
            InspectionContentCardMode.Unsupported when _isOpenVino => "OpenVinoUnsupportedPanel",
            InspectionContentCardMode.Invalid when _isOpenVino => "OpenVinoInvalidOrStalePanel",
            InspectionContentCardMode.Invalid => "InspectionFailurePanel",
            InspectionContentCardMode.Cancelled => "InspectionCancelledPanel",
            InspectionContentCardMode.OperationalFailure => "InspectionFailurePanel",
            _ => null
        };
        if (panel is not null)
        {
            SelectPanelName(panel);
        }
        ProjectTerminalContent();
        ProjectOutcomeToCurrentPanel();
        ApplyProgressPresentation();
        if (ContentPresentation.Mode != InspectionContentCardMode.Progress)
        {
            SetText("InspectionProgressSupportingText", ContentPresentation.SupportingText);
        }
        SetText("ReadyEvidenceHeaderStatus", ModelPresentation.InspectionChecksSummary);
        SetText("WarningEvidenceHeaderStatus", ModelPresentation.InspectionChecksSummary);
        ApplyEvidence("ReadyEvidenceBody", ModelPresentation.InspectionChecks);
        ApplyEvidence("WarningEvidenceBody", ModelPresentation.InspectionChecks);
    }

    public void ApplyOutcome(InspectionOutcomePresentation presentation)
    {
        OutcomePresentation = presentation ?? InspectionOutcomePresentation.Hidden;
        if (OutcomePresentation.Kind != InspectionOutcomePresentationKind.Hidden)
        {
            if (OutcomePresentation.Tone == InspectionOutcomeTone.Success)
            {
                SelectPanelName("InspectionReadyPanel");
            }
            else if (OutcomePresentation.Tone == InspectionOutcomeTone.Warning &&
                     ContentPresentation.Mode == InspectionContentCardMode.Warnings)
            {
                SelectPanelName("InspectionWarningPanel");
            }
        }
        ProjectOutcomeToCurrentPanel();
    }

    public void ApplyActions(InspectionActionCardPresentation presentation)
    {
        presentation ??= InspectionActionCardPresentation.Hidden;
        if (_isOpenVino && string.Equals(
            presentation.PrimaryAction.ActionId,
            "retry-openvino-conversion",
            StringComparison.Ordinal))
        {
            SelectPanelName(OutcomePresentation.Kind ==
                InspectionOutcomePresentationKind.Cancelled
                    ? "OpenVinoConversionCancelledPanel"
                    : "OpenVinoConversionFailedPanel");
            ProjectTerminalContent();
            ProjectOutcomeToCurrentPanel();
        }

        FrameworkElement actionPanel = CurrentPanel;
        if (ReferenceEquals(_actionBindingPanel, actionPanel) &&
            ActionsEquivalent(ActionPresentation, presentation))
        {
            ActionPresentation = presentation;
            Button stableCancelButton = ResolveCancelActionButton();
            ApplyGgufStoppingPresentation(
                stableCancelButton,
                ActionPresentation.CancelAction);
            return;
        }

        ActionPresentation = presentation;
        _actionBindingPanel = actionPanel;
        IReadOnlyList<Button> buttons = Descendants<Button>(actionPanel).ToArray();
        (string Slot, InspectionActionPresentation Action)[] actions =
        [
            ("SecondaryActionOneButton", ActionPresentation.SecondaryActionOne),
            ("SecondaryActionTwoButton", ActionPresentation.SecondaryActionTwo),
            ("PrimaryActionButton", ActionPresentation.PrimaryAction)
        ];
        actions = actions
            .Where(item => item.Action.Visibility == Visibility.Visible)
            .ToArray();
        _semanticActionButtons.Clear();
        Button cancelButton = ResolveCancelActionButton();
        foreach (string cancelName in new[] { "BtnCancelInspection", "BtnCancelOpenVinoConversion" })
        {
            if (_view.FindName(cancelName) is Button candidate)
            {
                BindAction(candidate, InspectionActionPresentation.Hidden);
            }
        }
        BindAction(cancelButton, ActionPresentation.CancelAction);
        _semanticActionButtons["CancelActionButton"] = cancelButton;
        ApplyGgufStoppingPresentation(cancelButton, ActionPresentation.CancelAction);

        int actionIndex = 0;
        foreach (Button button in buttons)
        {
            if (ReferenceEquals(button, cancelButton))
            {
                continue;
            }

            if (actionIndex < actions.Length)
            {
                (string slot, InspectionActionPresentation action) =
                    actions[actionIndex++];
                _semanticActionButtons[slot] = button;
                BindAction(button, action);
            }
            else
            {
                button.Visibility = Visibility.Collapsed;
                button.IsEnabled = false;
            }
        }
    }

    private void ApplyGgufStoppingPresentation(
        Button cancelButton,
        InspectionActionPresentation cancelAction)
    {
        bool stopping = !_isOpenVino
            && ContentPresentation.Mode == InspectionContentCardMode.Progress
            && string.Equals(cancelAction.ActionId, "cancel", StringComparison.Ordinal)
            && cancelAction.Visibility == Visibility.Visible
            && string.Equals(cancelAction.Text, "Stopping...", StringComparison.Ordinal);
        if (!stopping)
        {
            AutomationProperties.SetLiveSetting(
                cancelButton,
                AutomationLiveSetting.Off);
            AutomationProperties.SetItemStatus(cancelButton, string.Empty);
            _stoppingAnnouncementOwner = null;
            _stoppingAnnouncementEpoch = -1;
            checked { _stoppingAnnouncementGeneration++; }
            return;
        }

        const string visibleText = "Stopping...";
        const string accessibleText = "Stopping model inspection";
        cancelButton.Content = visibleText;
        AutomationProperties.SetName(cancelButton, accessibleText);
        AutomationProperties.SetItemStatus(cancelButton, accessibleText);
        AutomationProperties.SetLiveSetting(cancelButton, AutomationLiveSetting.Polite);

        object? owner = _progressOwner;
        long epoch = _estimateEpoch;
        if (owner is null
            || ReferenceEquals(owner, _stoppingAnnouncementOwner)
                && epoch == _stoppingAnnouncementEpoch)
        {
            return;
        }

        _stoppingAnnouncementOwner = owner;
        _stoppingAnnouncementEpoch = epoch;
        long generation = checked(++_stoppingAnnouncementGeneration);
        _ = _view.DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _stoppingAnnouncementGeneration
                || epoch != _estimateEpoch
                || !ReferenceEquals(owner, _progressOwner)
                || !ReferenceEquals(cancelButton, ResolveCancelActionButton())
                || cancelButton.IsEnabled
                || CurrentPanel.Visibility != Visibility.Visible)
            {
                return;
            }

            RaiseLiveRegionChanged(cancelButton, accessibleText);
        });
    }

    private static bool ActionsEquivalent(
        InspectionActionCardPresentation left,
        InspectionActionCardPresentation right) =>
        left.Mode == right.Mode &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
        string.Equals(left.Message, right.Message, StringComparison.Ordinal) &&
        string.Equals(left.AutomationName, right.AutomationName, StringComparison.Ordinal) &&
        ActionsEquivalent(left.CancelAction, right.CancelAction) &&
        ActionsEquivalent(left.SecondaryActionOne, right.SecondaryActionOne) &&
        ActionsEquivalent(left.SecondaryActionTwo, right.SecondaryActionTwo) &&
        ActionsEquivalent(left.PrimaryAction, right.PrimaryAction);

    private static bool ActionsEquivalent(
        InspectionActionPresentation left,
        InspectionActionPresentation right) =>
        string.Equals(left.Text, right.Text, StringComparison.Ordinal) &&
        ReferenceEquals(left.Command, right.Command) &&
        Equals(left.CommandParameter, right.CommandParameter) &&
        left.IsEnabled == right.IsEnabled &&
        left.Visibility == right.Visibility &&
        string.Equals(left.AutomationName, right.AutomationName, StringComparison.Ordinal) &&
        string.Equals(left.ActionId, right.ActionId, StringComparison.Ordinal) &&
        string.Equals(left.AutomationHelpText, right.AutomationHelpText, StringComparison.Ordinal) &&
        left.MinimumWidth.Equals(right.MinimumWidth);

    public void SetDisclosureState(bool expanded)
    {
        if (_activeDisclosure is null)
        {
            return;
        }

        _suppressDisclosure = true;
        try
        {
            _activeDisclosure.IsExpanded = expanded;
        }
        finally
        {
            _suppressDisclosure = false;
        }
    }

    public void SetMotionEnabled(bool enabled)
    {
        _motionEnabled = enabled;
        ApplyProgressPresentation();
    }

    public void CancelProgressMotion() =>
        CancelProgressMotion(completePendingSuccessfulPresentation: true);

    internal void CancelProgressMotion(bool completePendingSuccessfulPresentation)
    {
        ModelInspectionPagePresentation? pending = completePendingSuccessfulPresentation
            ? _pendingSuccessfulPresentation
            : null;
        _estimatePresenter?.Stop();
        _progressSequence.Abort();
        _progressOwner = null;
        _lastAuthoritativeActiveIndex = -1;
        _lastAuthoritativeSettledCount = -1;
        ResetProgressProjectionCache();
        ResetDisplayedStageAnnouncements();
        _estimateEpoch = _progressSequence.Epoch;
        _pendingSuccessfulPresentation = null;
        _pendingProgressCompletion = null;
        _terminalHandoffQueued = false;
        _openVinoCallbackCompletionFramePending = false;
        if (FindElement("InspectionOverallProgress") is ProgressBar overall) overall.Value = 0;
        foreach (ProgressRing ring in Descendants<ProgressRing>(_view))
        {
            ring.IsActive = false;
            ring.Visibility = Visibility.Collapsed;
        }
        if (pending is not null) ApplyPresentationCore(pending);
    }

    public void RefreshProgressPresentation() => ApplyProgressPresentation();

    public void AnnounceProgress(string text)
    {
        if (_progressOwner is not null
            && ContentPresentation.Mode == InspectionContentCardMode.Progress)
        {
            int authoritativeStage = ContentPresentation.ProgressRows.Items.ToList().FindIndex(
                item => item.Status == InspectionContentStatus.Active && item.IsActive);
            if (authoritativeStage >= 0 && authoritativeStage < _stageAnnouncements.Length)
                _stageAnnouncements[authoritativeStage] = text;
            QueueDisplayedStageAnnouncement();
            return;
        }
        ProgressAnnouncementCount++;
        ProgressAnnouncementHistory.Add(text);
        RaiseLiveRegionChanged(Require<FrameworkElement>("InspectionProgressPanel"), text);
    }

    public void AnnounceOutcome(string text)
    {
        OutcomeAnnouncementCount++;
        OutcomeAnnouncementHistory.Add(text);
        RaiseLiveRegionChanged(CurrentPanel, text);
    }

    public bool FocusOutcome()
    {
        FrameworkElement target = OutcomeFocusTarget;
        bool wasTabStop = target is Control control && control.IsTabStop;
        if (target is Control focusable)
        {
            focusable.IsTabStop = true;
        }
        try
        {
            return target.Focus(FocusState.Programmatic);
        }
        finally
        {
            if (target is Control restore)
            {
                restore.IsTabStop = wasTabStop;
            }
        }
    }

    public void ApplyResponsiveState(string stateName)
    {
        if (VisualStateManager.GoToState(_view, stateName, false))
        {
            _responsiveStateName = stateName;
        }
    }

    internal void ApplyOpenVinoMetadata(params string[] values)
    {
        string[] names =
        [
            "ModelType", "Format", "Architecture", "Context",
            "Precision", "Tokenizer", "Resources", "Task"
        ];
        foreach (string prefix in new[] { "Ready", "Warning" })
        {
            for (int index = 0; index < names.Length && index < values.Length; index++)
            {
                if (FindElement(prefix + "Metadata" + names[index]) is Panel panel)
                {
                    TextBlock? target = panel.Children.OfType<TextBlock>().LastOrDefault();
                    if (target is not null)
                    {
                        target.Text = values[index];
                    }
                }
            }
        }
    }

    internal void ShowOpenVinoSpecialProgress(
        string title,
        string summary,
        string count,
        IReadOnlyList<InspectionContentItemPresentation> rows)
    {
        SetText("OpenVinoConversionHeading", title);
        SetText("OpenVinoConversionSummary", $"{summary} · {count}");
        foreach (string name in AllPanels)
        {
            if (FindElement(name) is FrameworkElement panel)
            {
                panel.Visibility = string.Equals(name, "OpenVinoConversionProgressPanel", StringComparison.Ordinal)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        for (int index = 0; index < rows.Count && index < 6; index++)
        {
            if (FindElement($"ConversionStage{index + 1}") is FrameworkElement stage)
            {
                InspectionContentItemPresentation row = rows[index];
                AutomationProperties.SetName(stage, row.AutomationName);
                AutomationProperties.SetItemStatus(stage, row.StatusText);
                TextBlock[] texts = Descendants<TextBlock>(stage).ToArray();
                if (texts.Length >= 2)
                {
                    texts[^2].Text = row.Title;
                    texts[^1].Text = row.StatusText;
                }
            }
        }
    }

    internal void HideOpenVinoSpecialProgress()
    {
        if (FindElement("OpenVinoConversionProgressPanel") is FrameworkElement panel)
        {
            panel.Visibility = Visibility.Collapsed;
        }
        if (Presentation is not null)
        {
            SelectPanel(Presentation.State);
        }
    }

    private FrameworkElement CurrentPanel
    {
        get
        {
            foreach (string name in AllPanels)
            {
                if (FindElement(name) is FrameworkElement panel &&
                    panel.Visibility == Visibility.Visible)
                {
                    return panel;
                }
            }
            return Require<FrameworkElement>("InspectionProgressPanel");
        }
    }

    private string CurrentOutcomeHeadingName => CurrentPanel.Name switch
    {
        "InspectionReadyPanel" => "InspectionReadyHeading",
        "InspectionWarningPanel" => "InspectionWarningHeading",
        "InspectionCancelledPanel" => "InspectionCancelledHeading",
        "OpenVinoConversionRequiredPanel" => "OpenVinoConversionRequiredHeading",
        "OpenVinoIncompletePanel" => "OpenVinoIncompleteHeading",
        "OpenVinoUnsupportedPanel" => "OpenVinoUnsupportedHeading",
        "OpenVinoTimedOutPanel" => "OpenVinoTimedOutHeading",
        "OpenVinoInvalidOrStalePanel" => "OpenVinoInvalidOrStaleHeading",
        "OpenVinoConversionProgressPanel" => "OpenVinoConversionHeading",
        "OpenVinoConversionCancelledPanel" => "OpenVinoConversionCancelledHeading",
        "OpenVinoConversionFailedPanel" => "OpenVinoConversionFailedHeading",
        _ => "InspectionFailureHeading"
    };

    private Button ResolveCancelActionButton()
    {
        string name = string.Equals(
            CurrentPanel.Name,
            "OpenVinoConversionProgressPanel",
            StringComparison.Ordinal)
            ? "BtnCancelOpenVinoConversion"
            : "BtnCancelInspection";
        return _view.FindName(name) as Button ?? throw new InvalidOperationException(
            $"The exact inspection preview element '{name}' is unavailable.");
    }

    private void ProjectOutcomeToCurrentPanel()
    {
        string heading = CurrentOutcomeHeadingName;
        SetText(heading, OutcomePresentation.Title);
        FrameworkElement panel = CurrentPanel;
        AutomationProperties.SetName(panel, OutcomePresentation.AutomationName);

        string? messageName = TerminalTargetName(panel.Name, "Message");
        if (messageName is not null &&
            FindElement(messageName) is TextBlock message)
        {
            message.Text = OutcomePresentation.Message;
            return;
        }

        // The saved ready, warning, GGUF failure, and cancellation panels keep
        // their detail immediately after the heading. preserve that literal
        // structure while the OpenVINO terminals use explicit named targets.
        if (FindElement(heading) is TextBlock headingBlock)
        {
            TextBlock? detail = null;
            if ((headingBlock.Parent ?? VisualTreeHelper.GetParent(headingBlock))
                is Panel parent)
            {
                detail = parent.Children.OfType<TextBlock>()
                    .FirstOrDefault(item => !ReferenceEquals(item, headingBlock));
            }
            else
            {
                TextBlock[] panelText = Descendants<TextBlock>(panel).ToArray();
                int headingIndex = Array.IndexOf(panelText, headingBlock);
                if (headingIndex >= 0 && headingIndex + 1 < panelText.Length)
                {
                    detail = panelText[headingIndex + 1];
                }
            }
            if (detail is not null)
            {
                detail.Text = OutcomePresentation.Message;
            }
        }
    }

    private void ProjectTerminalContent()
    {
        FrameworkElement panel = CurrentPanel;
        string? diagnosticContainerName = TerminalTargetName(panel.Name, "DiagnosticContainer");
        string? diagnosticName = TerminalTargetName(panel.Name, "Diagnostic");
        string? recoveryName = TerminalTargetName(panel.Name, "Recovery");

        if (diagnosticContainerName is not null &&
            FindElement(diagnosticContainerName) is FrameworkElement diagnosticContainer)
        {
            diagnosticContainer.Visibility = ContentPresentation.DiagnosticCodeVisibility;
            AutomationProperties.SetItemStatus(
                diagnosticContainer,
                ContentPresentation.DiagnosticStatus.ToString());
        }
        if (diagnosticName is not null &&
            FindElement(diagnosticName) is TextBlock diagnostic)
        {
            diagnostic.Visibility = ContentPresentation.DiagnosticCodeVisibility;
            diagnostic.Text = ContentPresentation.DiagnosticCodeVisibility == Visibility.Visible
                ? ContentPresentation.DiagnosticCode
                : string.Empty;
            AutomationProperties.SetName(diagnostic, diagnostic.Text);
            AutomationProperties.SetItemStatus(
                diagnostic,
                ContentPresentation.DiagnosticStatus.ToString());
        }
        if (recoveryName is not null &&
            FindElement(recoveryName) is TextBlock recovery)
        {
            recovery.Text = ContentPresentation.SupportingTextVisibility == Visibility.Visible
                ? ContentPresentation.SupportingText
                : string.Empty;
            recovery.Visibility = ContentPresentation.SupportingTextVisibility;
            AutomationProperties.SetName(recovery, recovery.Text);
        }
    }

    private static string? TerminalTargetName(string panelName, string suffix) => panelName switch
    {
        "InspectionFailurePanel" => "InspectionFailure" + suffix,
        "InspectionCancelledPanel" => "InspectionCancelled" + suffix,
        "OpenVinoConversionRequiredPanel" => "OpenVinoConversionRequired" + suffix,
        "OpenVinoIncompletePanel" => "OpenVinoIncomplete" + suffix,
        "OpenVinoUnsupportedPanel" => "OpenVinoUnsupported" + suffix,
        "OpenVinoTimedOutPanel" => "OpenVinoTimedOut" + suffix,
        "OpenVinoInvalidOrStalePanel" => "OpenVinoInvalidOrStale" + suffix,
        "OpenVinoConversionCancelledPanel" => "OpenVinoConversionCancelled" + suffix,
        "OpenVinoConversionFailedPanel" => "OpenVinoConversionFailed" + suffix,
        _ => null
    };

    private void SelectPanel(ModelInspectionFigmaState state)
    {
        string selected = state switch
        {
            ModelInspectionFigmaState.InspectionProgress => "InspectionProgressPanel",
            ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded => "InspectionReadyPanel",
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded => "InspectionWarningPanel",
            ModelInspectionFigmaState.Cancelled => "InspectionCancelledPanel",
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded when _isOpenVino => "OpenVinoConversionRequiredPanel",
            ModelInspectionFigmaState.IncompletePackage when _isOpenVino => "OpenVinoIncompletePanel",
            ModelInspectionFigmaState.Unsupported when _isOpenVino => "OpenVinoUnsupportedPanel",
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded when _isOpenVino => "OpenVinoInvalidOrStalePanel",
            _ => "InspectionFailurePanel"
        };

        SelectPanelName(selected);

        _activeDisclosure = state switch
        {
            ModelInspectionFigmaState.ReadyCollapsed or ModelInspectionFigmaState.ReadyExpanded =>
                FindElement("ReadyDetailsExpander") as Expander,
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or ModelInspectionFigmaState.ReadyWithWarningsExpanded =>
                FindElement("WarningDetailsExpander") as Expander,
            _ => null
        };
        SetDisclosureState(state is ModelInspectionFigmaState.ReadyExpanded or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded or
            ModelInspectionFigmaState.ConversionRequiredExpanded or
            ModelInspectionFigmaState.InvalidExpanded);
    }

    private void SelectPanelName(string selected)
    {
        if (selected != "InspectionProgressPanel")
        {
            _estimatePresenter?.Stop();
            _progressSequence.Abort();
            _progressOwner = null;
            _pendingProgressCompletion = null;
            _terminalHandoffQueued = false;
            _openVinoCallbackCompletionFramePending = false;
            _lastAuthoritativeActiveIndex = -1;
            _lastAuthoritativeSettledCount = -1;
            ResetProgressProjectionCache();
            ResetDisplayedStageAnnouncements();
            _estimateEpoch = _progressSequence.Epoch;
        }
        foreach (string name in AllPanels)
        {
            if (FindElement(name) is FrameworkElement panel)
            {
                panel.Visibility = string.Equals(name, selected, StringComparison.Ordinal)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }

    private void ApplyProgressRowMetadata(IReadOnlyList<InspectionContentItemPresentation> rows)
    {
        for (int index = 0; index < 5; index++)
        {
            InspectionContentItemPresentation? row = index < rows.Count ? rows[index] : null;
            string prefix = $"InspectionStage{index + 1}";
            SetText(prefix + "Title", row?.Title ?? string.Empty);
        }
    }

    private void ApplyProgressPresentation()
    {
        if (ContentPresentation.Mode != InspectionContentCardMode.Progress)
        {
            _estimatePresenter?.Stop();
            _progressSequence.Abort();
            _progressOwner = null;
            _terminalHandoffQueued = false;
            _openVinoCallbackCompletionFramePending = false;
            _lastAuthoritativeActiveIndex = -1;
            _lastAuthoritativeSettledCount = -1;
            ResetProgressProjectionCache();
            ResetDisplayedStageAnnouncements();
            _estimateEpoch = _progressSequence.Epoch;
            return;
        }
        IReadOnlyList<InspectionContentItemPresentation> rows =
            ContentPresentation.ProgressRows.Items;
        _progressMotionActive = _motionEnabled && new UISettings().AnimationsEnabled;
        ApplyProgressRowMetadata(rows);
        if (FindElement("InspectionOverallProgress") is ProgressBar)
        {
            int authoritativeActiveIndex = rows.ToList().FindIndex(item =>
                item.Status == InspectionContentStatus.Active && item.IsActive);
            int authoritativeSettledCount = rows.Count(item =>
                item.Status == InspectionContentStatus.Passed);
            bool regressiveObservation = _progressOwner is not null
                && ((authoritativeActiveIndex >= 0
                        && _lastAuthoritativeActiveIndex >= 0
                        && authoritativeActiveIndex < _lastAuthoritativeActiveIndex)
                    || authoritativeSettledCount < _lastAuthoritativeSettledCount);
            if (regressiveObservation)
            {
                UpdateEstimatedProgressValues();
                return;
            }
            _progressOwner ??= ContentPresentation.ProgressRows;
            if (authoritativeActiveIndex >= 0)
                _lastAuthoritativeActiveIndex = Math.Max(
                    _lastAuthoritativeActiveIndex,
                    authoritativeActiveIndex);
            _lastAuthoritativeSettledCount = Math.Max(
                _lastAuthoritativeSettledCount,
                authoritativeSettledCount);
            bool successfulTerminalReady = _pendingSuccessfulPresentation is not null
                || _pendingProgressCompletion is not null;
            SerializedProgressStageObservation[] observations = rows.Select((item, index) =>
            {
                bool authoritativeActive = item.Status == InspectionContentStatus.Active
                    && item.IsActive;
                bool holdFinalStage = index == rows.Count - 1
                    && item.Status == InspectionContentStatus.Passed
                    && !successfulTerminalReady;
                double? measured = authoritativeActive
                    && item.StageFraction is double value && double.IsFinite(value)
                    && value >= 0 && value <= 1
                        ? index == rows.Count - 1 && !successfulTerminalReady
                            ? Math.Min(value, .99)
                            : value
                        : holdFinalStage ? .99 : null;
                return new SerializedProgressStageObservation(
                    authoritativeActive || holdFinalStage,
                    item.Status == InspectionContentStatus.Passed && !holdFinalStage,
                    false,
                    measured);
            }).ToArray();
            _progressSequence.Observe(
                _progressOwner, ++_estimateRevision, observations,
                ProgressMotionEnabled());
            _estimateEpoch = _progressSequence.Epoch;
            UpdateEstimatedProgressValues();
            _estimatePresenter ??= new DispatcherProgressPresenter(
                _view, UpdateEstimatedProgressValues, TimeSpan.FromMilliseconds(16));
            _estimatePresenter.Start();
        }
    }

    internal static string ProjectDisplayedStageDetail(
        InspectionContentItemPresentation row, bool current, bool settled)
    {
        // Backend completion can lead the paced visible row. Keep its copy in
        // the same tense as that visible state until the row displays Passed.
        if (current)
        {
            return row.IsActive && row.Status == InspectionContentStatus.Active
                && !string.IsNullOrWhiteSpace(row.Detail)
                    ? row.Detail : row.DefaultDetail;
        }

        return settled ? row.Detail : string.Empty;
    }

    private void UpdateEstimatedProgressValues()
    {
        if (_progressOwner is null || _estimateEpoch != _progressSequence.Epoch) return;
        if (FindElement("InspectionOverallProgress") is not ProgressBar bar) return;
        var rows = ContentPresentation.ProgressRows.Items;
        SerializedProgressFrame frame = _progressSequence.GetFrame();
        _displayedActiveStage = frame.ActiveIndex;
        for (int i = 0; i < Math.Min(5, rows.Count); i++)
        {
            var row = rows[i];
            SerializedProgressStageState state = frame.Stages[i];
            bool current = state == SerializedProgressStageState.Active;
            bool settled = state is SerializedProgressStageState.Completed
                or SerializedProgressStageState.NotNeeded;
            string frameDetail = ProjectDisplayedStageDetail(row, current, settled);
            if (current && !string.IsNullOrWhiteSpace(frameDetail))
            {
                _displayedStageDetails[i] = frameDetail;
            }
            else if (settled &&
                _projectedStageStates[i] != state &&
                !string.IsNullOrWhiteSpace(row.Detail))
            {
                _displayedStageDetails[i] = row.Detail;
            }
            string displayedDetail = current || settled
                ? _displayedStageDetails[i] ?? string.Empty
                : string.Empty;
            SetText($"InspectionStage{i + 1}Detail", displayedDetail);
            string rowPercentage = $"{Math.Min(100m, Math.Floor((decimal)frame.ActiveFraction * 100m)):0}%";
            string estimatedPercentage = $"Estimated {rowPercentage}";
            string display = current ? $"Checking\n{rowPercentage}"
                : settled ? state == SerializedProgressStageState.NotNeeded ? "Not needed" : "Passed"
                : "Waiting";
            TextBlock? statusText = FindElement($"InspectionStage{i + 1}Status") as TextBlock;
            if (statusText is not null && statusText.Text != display)
            {
                statusText.Text = display;
            }
            if (FindElement($"InspectionStage{i + 1}") is FrameworkElement completedHost)
            {
                string accessibleStatus = current
                    ? $"Checking · {estimatedPercentage}" : display;
                string accessibleName = displayedDetail.Length == 0
                    ? $"{row.Title}. {accessibleStatus}."
                    : $"{row.Title}. {accessibleStatus}. {displayedDetail}";
                if (!string.Equals(_projectedAccessibleStatuses[i], accessibleStatus, StringComparison.Ordinal))
                {
                    AutomationProperties.SetItemStatus(completedHost, accessibleStatus);
                    _projectedAccessibleStatuses[i] = accessibleStatus;
                }
                if (!string.Equals(_projectedAccessibleNames[i], accessibleName, StringComparison.Ordinal))
                {
                    AutomationProperties.SetName(completedHost, accessibleName);
                    _projectedAccessibleNames[i] = accessibleName;
                }
            }
            Border? surface = FindElement($"InspectionStage{i + 1}GlyphSurface") as Border;
            Grid? arc = FindElement($"InspectionStage{i + 1}Arc") as Grid;
            FontIcon? glyph = FindElement($"InspectionStage{i + 1}Glyph") as FontIcon;
            TextBlock? number = FindElement($"InspectionStage{i + 1}Number") as TextBlock;
            ProgressRing? ring = FindElement($"InspectionStage{i + 1}Ring") as ProgressRing;
            if (ring is not null)
            {
                bool shouldAnimate = current && ProgressMotionEnabled();
                Visibility ringVisibility = current ? Visibility.Visible : Visibility.Collapsed;
                if (ring.IsActive != shouldAnimate) ring.IsActive = shouldAnimate;
                if (ring.Visibility != ringVisibility) ring.Visibility = ringVisibility;
            }
            if (arc is not null && arc.Visibility != Visibility.Collapsed)
                arc.Visibility = Visibility.Collapsed;
            Visibility glyphVisibility = settled ? Visibility.Visible : Visibility.Collapsed;
            if (glyph is not null && glyph.Visibility != glyphVisibility)
                glyph.Visibility = glyphVisibility;
            Visibility numberVisibility = !current && !settled
                ? Visibility.Visible : Visibility.Collapsed;
            if (number is not null && number.Visibility != numberVisibility)
                number.Visibility = numberVisibility;
            if (_projectedStageStates[i] != state)
            {
                ApplyRowTone(surface, statusText, glyph, number,
                    settled ? InspectionContentStatus.Passed
                        : current ? InspectionContentStatus.Active : InspectionContentStatus.Waiting);
                _projectedStageStates[i] = state;
            }
        }
        bar.IsIndeterminate = false;
        if (bar.Value != frame.OverallValue) bar.Value = frame.OverallValue;
        string overallPercentage = $"{Math.Min(100, Math.Floor(frame.OverallValue * 20)):0}%";
        string estimatedOverall = $"Estimated {overallPercentage}";
        if (FindElement("InspectionOverallPercentage") is TextBlock percentage
            && percentage.Text != overallPercentage)
        {
            percentage.Text = overallPercentage;
        }
        AutomationProperties.SetItemStatus(bar, estimatedOverall);
        int completed = frame.Stages.Count(item => item is SerializedProgressStageState.Completed
            or SerializedProgressStageState.NotNeeded);
        SetText("InspectionProgressCount", $"{completed} of 5 checks complete");
        if (frame.ActiveIndex >= 0 && frame.ActiveIndex < rows.Count)
        {
            SetText("InspectionProgressHeading", rows[frame.ActiveIndex].Title);
            SetText("InspectionProgressSupportingText",
                ProjectDisplayedStageDetail(rows[frame.ActiveIndex], current: true, settled: false));
        }
        QueueDisplayedStageAnnouncement();
        bool fullyRendered = frame.Stages.Count > 0
            && frame.Stages.All(item => item is SerializedProgressStageState.Completed
                or SerializedProgressStageState.NotNeeded)
            && frame.OverallValue >= frame.Stages.Count;
        if (fullyRendered && (_pendingSuccessfulPresentation is not null
                || _pendingProgressCompletion is not null))
        {
            QueueSuccessfulPresentationHandoff();
        }
        if (!frame.NeedsTicks)
        {
            _estimatePresenter?.Stop();
            if (frame.IsFinished && !_openVinoCallbackCompletionFramePending)
            {
                CompleteSuccessfulPresentationHandoff();
            }
        }
    }

    private bool ProgressMotionEnabled() =>
        _progressMotionActive;

    private void QueueSuccessfulPresentationHandoff()
    {
        if (_terminalHandoffQueued || _progressOwner is null)
        {
            return;
        }
        _terminalHandoffQueued = true;
        object owner = _progressOwner;
        long epoch = _progressSequence.Epoch;
        long revision = checked(++_terminalHandoffRevision);
        _ = _view.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_terminalHandoffQueued
                || revision != _terminalHandoffRevision
                || epoch != _progressSequence.Epoch
                || !ReferenceEquals(owner, _progressOwner))
            {
                return;
            }

            CompleteSuccessfulPresentationHandoff();
        });
    }

    private void ResetProgressProjectionCache()
    {
        Array.Clear(_projectedStageStates);
        Array.Clear(_projectedAccessibleStatuses);
        Array.Clear(_projectedAccessibleNames);
        Array.Clear(_displayedStageDetails);
    }

    private void ResetDisplayedStageAnnouncements()
    {
        _displayedActiveStage = -1;
        _lastAnnouncedDisplayedStage = -1;
        Array.Clear(_stageAnnouncements);
        checked { _progressAnnouncementGeneration++; }
    }

    private void QueueDisplayedStageAnnouncement()
    {
        int stage = _displayedActiveStage;
        if (stage < 0 || stage >= ContentPresentation.ProgressRows.Items.Count
            || stage == _lastAnnouncedDisplayedStage) return;
        _lastAnnouncedDisplayedStage = stage;
        long epoch = _estimateEpoch;
        object? owner = _progressOwner;
        long generation = checked(++_progressAnnouncementGeneration);
        string announcement = _stageAnnouncements[stage]
            ?? $"Model inspection. {ContentPresentation.ProgressRows.Items[stage].Title}. Step {stage + 1} of 5.";
        _ = _view.DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _progressAnnouncementGeneration
                || epoch != _progressSequence.Epoch || !ReferenceEquals(owner, _progressOwner)
                || _displayedActiveStage != stage
                || CurrentPanel.Visibility != Visibility.Visible) return;
            ProgressAnnouncementCount++;
            ProgressAnnouncementHistory.Add(announcement);
            RaiseLiveRegionChanged(
                Require<FrameworkElement>("InspectionProgressPanel"), announcement);
        });
    }

    private void ApplyRowTone(
        Border? surface,
        TextBlock? status,
        FontIcon? glyph,
        TextBlock? number,
        InspectionContentStatus state)
    {
        if (surface is null || status is null)
        {
            return;
        }
        (string background, string border, string foreground) = state switch
        {
            InspectionContentStatus.Active =>
                ("InspectionAccentSurfaceBrush", "InspectionAccentBorderBrush", "InspectionAccentBrush"),
            InspectionContentStatus.Passed =>
                ("InspectionSuccessSurfaceBrush", "InspectionSuccessBorderBrush", "InspectionSuccessBrush"),
            InspectionContentStatus.Warning =>
                ("InspectionWarningSurfaceBrush", "InspectionWarningBorderBrush", "InspectionWarningBrush"),
            InspectionContentStatus.Error =>
                ("InspectionErrorSurfaceBrush", "InspectionErrorBorderBrush", "InspectionErrorBrush"),
            _ =>
                ("InspectionWaitingSurfaceBrush", "InspectionBorderBrush", "InspectionWaitingTextBrush")
        };
        surface.Background = PreviewBrush(background);
        surface.BorderBrush = PreviewBrush(border);
        surface.BorderThickness = state == InspectionContentStatus.Passed
            ? new Thickness(0)
            : new Thickness(1);
        status.Foreground = PreviewBrush(foreground);
        if (glyph is not null)
        {
            glyph.Foreground = PreviewBrush(foreground);
        }
        if (number is not null)
        {
            number.Foreground = PreviewBrush(foreground);
        }
    }

    private Brush PreviewBrush(string key)
    {
        string theme = new AccessibilitySettings().HighContrast
            ? "HighContrast"
            : _view.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        ResourceDictionary resources =
            (_view.Content as FrameworkElement)?.Resources ?? _view.Resources;
        if (!resources.ThemeDictionaries.TryGetValue(theme, out object? themeValue) ||
            themeValue is not ResourceDictionary dictionary ||
            dictionary[key] is not Brush brush)
        {
            throw new InvalidOperationException(
                $"The exact inspection preview brush '{theme}/{key}' is unavailable.");
        }
        return brush;
    }

    private void ApplyMetadata(string prefix, InspectionModelCardPresentation model)
    {
        (string Name, string Value)[] values =
        [
            ("Publisher", model.Publisher), ("ModelName", model.ModelName),
            ("Parameters", model.ParameterCount), ("ModelType", model.ModelType),
            ("Context", model.DeclaredContext), ("FileSize", model.FileSize),
            ("Format", model.FormatName), ("Quantisation", model.Quantisation)
        ];
        foreach ((string name, string value) in values)
        {
            if (FindElement(prefix + "Metadata" + name) is Panel panel)
            {
                TextBlock? target = panel.Children.OfType<TextBlock>().LastOrDefault();
                if (target is not null)
                {
                    target.Text = value;
                }
            }
        }
    }

    private void ApplyEvidence(string hostName, IReadOnlyList<InspectionCheckPresentation> checks)
    {
        if (FindElement(hostName) is not Panel host)
        {
            return;
        }
        TextBlock[] text = Descendants<TextBlock>(host).ToArray();
        for (int index = 0; index < checks.Count && index * 2 + 1 < text.Length; index++)
        {
            text[index * 2].Text = checks[index].Title;
            text[index * 2 + 1].Text = checks[index].Detail;
        }
    }

    private void BindAction(Button button, InspectionActionPresentation action)
    {
        if (_buttonHandlers.Remove(button, out RoutedEventHandler? oldHandler))
        {
            button.Click -= oldHandler;
        }
        button.Content = action.Text;
        button.Tag = action.ActionId;
        button.Visibility = action.Visibility;
        button.IsEnabled = action.IsEnabled && (action.Command?.CanExecute(action.CommandParameter) ?? true);
        AutomationProperties.SetName(button, action.AutomationName);
        AutomationProperties.SetHelpText(button, action.AutomationHelpText);
        RoutedEventHandler handler = (_, _) => Execute(action);
        _buttonHandlers[button] = handler;
        button.Click += handler;
    }

    private static void Execute(InspectionActionPresentation action)
    {
        ICommand? command = action.Command;
        if (action.IsEnabled && command?.CanExecute(action.CommandParameter) == true)
        {
            command.Execute(action.CommandParameter);
        }
    }

    private void Disclosure_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (!_suppressDisclosure)
        {
            DisclosureToggleRequested?.Invoke(this,
                new InspectionDisclosureToggleRequestedEventArgs(true));
        }
    }

    private void Disclosure_Collapsed(object sender, ExpanderCollapsedEventArgs args)
    {
        if (!_suppressDisclosure)
        {
            DisclosureToggleRequested?.Invoke(this,
                new InspectionDisclosureToggleRequestedEventArgs(false));
        }
    }

    private void SetText(string name, string? value)
    {
        if (FindElement(name) is TextBlock text)
        {
            string next = value ?? string.Empty;
            if (!string.Equals(text.Text, next, StringComparison.Ordinal))
                text.Text = next;
        }
    }

    private void SetTextAndToolTip(string name, string? value)
    {
        if (FindElement(name) is TextBlock text)
        {
            string next = value ?? string.Empty;
            if (!string.Equals(text.Text, next, StringComparison.Ordinal))
            {
                text.Text = next;
            }

            ToolTipService.SetToolTip(text, next);
        }
    }

    private T Require<T>(string name) where T : class =>
        FindElement(name) as T ?? throw new InvalidOperationException(
            $"The exact inspection preview element '{name}' is unavailable.");

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (T nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static string BadgeText(InspectionModelBadgeState state) => state switch
    {
        InspectionModelBadgeState.ModelSelected => "MODEL SELECTED",
        InspectionModelBadgeState.Inspected => "INSPECTED",
        InspectionModelBadgeState.SourceModel => "SOURCE MODEL",
        InspectionModelBadgeState.Incomplete => "INCOMPLETE",
        InspectionModelBadgeState.Unsupported => "UNSUPPORTED",
        InspectionModelBadgeState.Invalid => "INVALID",
        InspectionModelBadgeState.NotInspected => "NOT INSPECTED",
        _ => "RESULT UNKNOWN"
    };

    private static void RaiseLiveRegionChanged(FrameworkElement target, string text)
    {
        AutomationProperties.SetName(target, text);
        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(target) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(target);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}

internal sealed class PreviewModelCardAdapter
{
    private readonly ModelInspectionPreviewProjection _owner;
    internal PreviewModelCardAdapter(ModelInspectionPreviewProjection owner) => _owner = owner;
    internal InspectionModelCardPresentation Presentation
    {
        get => _owner.ModelPresentation;
        set => _owner.ApplyModel(value);
    }
    internal bool IsDisclosureStateExternallyOwned { get; set; }
    internal Expander? ActiveDisclosure => _owner.ActiveDisclosure;
    internal string? FixtureResponsiveStateName => _owner.ResponsiveStateName;
    internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
        DisclosureToggleRequested
    {
        add => _owner.DisclosureToggleRequested += value;
        remove => _owner.DisclosureToggleRequested -= value;
    }
    internal object? FindName(string name) => _owner.FindElement(name);
    internal void ClaimDisclosureTarget(bool expanded) => _owner.SetDisclosureState(expanded);
    internal void PrepareDisclosureTarget(bool expanded) => _owner.SetDisclosureState(expanded);
    internal void CompleteDisclosureTarget(bool expanded) => _owner.SetDisclosureState(expanded);
    internal void ApplyFixtureResponsiveState(double width) =>
        _owner.ApplyResponsiveState(width >= 900 ? "WideInspectionState" : width >= 640 ? "MediumInspectionState" : "NarrowInspectionState");
}

internal sealed class PreviewContentCardAdapter
{
    private readonly ModelInspectionPreviewProjection _owner;
    internal PreviewContentCardAdapter(ModelInspectionPreviewProjection owner) => _owner = owner;
    internal InspectionContentCardPresentation Presentation
    {
        get => _owner.ContentPresentation;
        set => _owner.ApplyContent(value);
    }
    internal Visibility Visibility { get => _owner.ContentSurface.Visibility; set => _owner.ContentSurface.Visibility = value; }
    internal Visibility CardVisibility => ContentPresentationVisibility(Presentation);
    internal Visibility ProgressModeVisibility => Presentation.Mode == InspectionContentCardMode.Progress ? Visibility.Visible : Visibility.Collapsed;
    internal Visibility TerminalModeVisibility => Presentation.Mode == InspectionContentCardMode.Progress ? Visibility.Collapsed : Visibility.Visible;
    internal bool IsDisclosureStateExternallyOwned { get; set; }
    internal Expander? ActiveDisclosure => _owner.ActiveDisclosure;
    internal string? FixtureResponsiveStateName => _owner.ResponsiveStateName;
    internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
        DisclosureToggleRequested
    {
        add => _owner.DisclosureToggleRequested += value;
        remove => _owner.DisclosureToggleRequested -= value;
    }
    internal void RefreshProgressPresentation() => _owner.RefreshProgressPresentation();
    internal void AnnounceProgress(string text) => _owner.AnnounceProgress(text);
    internal void CancelProgressMotion() => _owner.CancelProgressMotion();
    internal void SetMotionEnabled(bool enabled) => _owner.SetMotionEnabled(enabled);
    internal void ClaimDisclosureTarget(bool expanded) => _owner.SetDisclosureState(expanded);
    internal void PrepareDisclosureTarget(bool expanded) => _owner.SetDisclosureState(expanded);
    internal void CompleteDisclosureTarget(bool expanded) => _owner.SetDisclosureState(expanded);
    internal void ApplyFixtureResponsiveState(double width) =>
        _owner.ApplyResponsiveState(width >= 900 ? "WideInspectionState" : width >= 640 ? "MediumInspectionState" : "NarrowInspectionState");
    private static Visibility ContentPresentationVisibility(InspectionContentCardPresentation value) =>
        value.Mode == InspectionContentCardMode.Hidden ? Visibility.Collapsed : Visibility.Visible;
}

internal sealed class PreviewOutcomeCardAdapter
{
    private readonly ModelInspectionPreviewProjection _owner;
    internal PreviewOutcomeCardAdapter(ModelInspectionPreviewProjection owner) => _owner = owner;
    internal InspectionOutcomePresentation Presentation
    {
        get => _owner.OutcomePresentation;
        set => _owner.ApplyOutcome(value);
    }
    internal FrameworkElement FocusTarget => _owner.OutcomeFocusTarget;
    internal int LiveRegionChangeNotificationCount => _owner.OutcomeAnnouncementCount;
    internal IReadOnlyList<string> LiveRegionAnnouncementHistory => _owner.OutcomeAnnouncementHistory;
    internal bool FocusOutcome() => _owner.FocusOutcome();
    internal void AnnounceOutcome(string text) => _owner.AnnounceOutcome(text);
}

internal sealed class PreviewActionCardAdapter
{
    private readonly ModelInspectionPreviewProjection _owner;
    internal PreviewActionCardAdapter(ModelInspectionPreviewProjection owner) => _owner = owner;
    internal InspectionActionCardPresentation Presentation
    {
        get => _owner.ActionPresentation;
        set => _owner.ApplyActions(value);
    }
    internal string? FixtureResponsiveStateName => _owner.ResponsiveStateName;
    internal object? FindName(string name) => _owner.FindElement(name);
    internal void ApplyFixtureResponsiveState(double width) =>
        _owner.ApplyResponsiveState(width >= 900 ? "WideInspectionState" : width >= 640 ? "MediumInspectionState" : "NarrowInspectionState");
}
