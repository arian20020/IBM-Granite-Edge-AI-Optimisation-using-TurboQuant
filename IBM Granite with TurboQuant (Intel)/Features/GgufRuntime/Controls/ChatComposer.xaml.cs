using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.Attachments;
using GraniteEdgeAI.Features.GgufRuntime.Presentation;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatComposer : UserControl
{
    private readonly IKnowledgeFilePicker knowledgeFilePicker;
    private readonly ObservableCollection<KnowledgeAttachment> attachments = new();
    private readonly ObservableCollection<ChatModelSelectorItem> modelOptions = new();
    private readonly Dictionary<string, WeakReference<Button>> modelOptionButtons =
        new(StringComparer.Ordinal);
    private bool isPickingKnowledgeFiles;
    private bool isSwitchingModel;
    private string? requestedModelId;

    public static readonly DependencyProperty IsGeneratingProperty =
        DependencyProperty.Register(
            nameof(IsGenerating),
            typeof(bool),
            typeof(ChatComposer),
            new PropertyMetadata(false, OnIsGeneratingChanged));

    public ChatComposer() : this(new WindowsKnowledgeFilePicker())
    {
    }

    internal ChatComposer(IKnowledgeFilePicker knowledgeFilePicker)
    {
        this.knowledgeFilePicker = knowledgeFilePicker ??
            throw new ArgumentNullException(nameof(knowledgeFilePicker));
        InitializeComponent();
        AttachmentItems.ItemsSource = attachments;
        ChatModelItems.ItemsSource = modelOptions;
        UpdateAttachmentPresentation();
        ApplyModelSwitchPresentation(ChatModelSwitchPresentation.Idle, null);
        ApplyGeneratingState();
    }

    public event EventHandler<string>? SendRequested;
    public event EventHandler? StopRequested;
    internal event EventHandler<string>? ModelSelectionRequested;
    internal event EventHandler? ModelSelectionCancellationRequested;
    internal event EventHandler? ImportModelRequested;
    internal event EventHandler? GetMoreLocalModelsRequested;

    public bool IsGenerating
    {
        get => (bool)GetValue(IsGeneratingProperty);
        set => SetValue(IsGeneratingProperty, value);
    }

    public string PromptText
    {
        get => PromptTextBox.Text;
        set
        {
            PromptTextBox.Text = value ?? string.Empty;
            UpdatePromptVerticalAlignment();
            UpdateSubmissionState();
        }
    }

    private static void OnIsGeneratingChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatComposer)sender).ApplyGeneratingState();

    private bool externalRoute;
    private string? activeModelLabelOverride;
    private bool isPreparingSession;
    private bool allowPreparingDraft;
    private bool conversationReady = true;
    private bool isStopping;

    internal void SetStopping(bool stopping)
    {
        isStopping = stopping && IsGenerating;
        ApplyGeneratingState();
    }

    internal void SetConversationReady(bool ready)
    {
        conversationReady = ready;
        UpdateSubmissionState();
    }

    internal void SetSessionPreparing(bool value, bool allowDraftEditing = false)
    {
        isPreparingSession = value;
        allowPreparingDraft = value && allowDraftEditing;
        ApplyGeneratingState();
    }

    internal void FocusPrompt() => PromptTextBox.Focus(FocusState.Programmatic);

    internal void SetActiveModelLabel(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        activeModelLabelOverride = label;
        ChatCurrentModelLabel.Text = label;
        AutomationProperties.SetName(ChatComposerModelPill, $"Current model {label}");
        ToolTipService.SetToolTip(ChatComposerModelPill, label);
    }
    private bool externalSendEnabled;
    private bool externalStopEnabled;

    internal void ConfigureExternalRoute(string modelLabel)
    {
        externalRoute = true;
        AttachmentButton.Visibility = Visibility.Collapsed;
        ChatCurrentModelLabel.Text = modelLabel;
        AutomationProperties.SetName(ChatComposerModelPill, modelLabel);
        ToolTipService.SetToolTip(ChatComposerModelPill, modelLabel);
        ApplyGeneratingState();
    }

    internal void ApplyExternalRouteState(bool sendEnabled, bool stopEnabled, bool busy)
    {
        externalSendEnabled = sendEnabled;
        externalStopEnabled = stopEnabled;
        IsGenerating = busy;
        ApplyGeneratingState();
    }

    private void ApplyGeneratingState()
    {
        if (StopButton is null)
        {
            return;
        }

        bool restoreFromStop = StopButton.FocusState != FocusState.Unfocused
            && (!IsGenerating || (externalRoute && !externalStopEnabled));
        bool moveFromInput = IsGenerating && (SendButton.FocusState != FocusState.Unfocused
            || (!externalRoute && PromptTextBox.FocusState != FocusState.Unfocused));
        StopButton.Visibility = IsGenerating ? Visibility.Visible : Visibility.Collapsed;
        if (!IsGenerating) isStopping = false;
        StopLabel.Text = isStopping ? "Stopping…" : "Stop";
        AutomationProperties.SetName(StopButton, isStopping ? "Stopping generation" : "Stop generation");
        ToolTipService.SetToolTip(StopButton, isStopping ? "Stopping generation" : "Stop generation");
        StopButton.IsEnabled = !isStopping;
        SendButton.Visibility = IsGenerating ? Visibility.Collapsed : Visibility.Visible;
        PromptTextBox.IsEnabled = !IsGenerating && (!isPreparingSession || allowPreparingDraft);
        bool canSelectKnowledgeFiles = !isPickingKnowledgeFiles && !isPreparingSession;
        AttachmentButton.IsEnabled = canSelectKnowledgeFiles;
        AddFilesFlyoutButton.IsEnabled = canSelectKnowledgeFiles;
        bool canChooseModel = !isSwitchingModel && !isPreparingSession;
        ChatComposerModelPill.IsEnabled = canChooseModel;
        if (externalRoute)
        {
            PromptTextBox.IsEnabled = true;
            StopButton.IsEnabled = externalStopEnabled && !isStopping;
            ChatComposerModelPill.IsEnabled = false;
            AttachmentButton.IsEnabled = false;
            AddFilesFlyoutButton.IsEnabled = false;
        }
        foreach (WeakReference<Button> reference in modelOptionButtons.Values)
        {
            if (reference.TryGetTarget(out Button? button))
            {
                button.IsEnabled = canChooseModel && button.CommandParameter is ChatModelSelectorItem { IsSelectable: true };
            }
        }
        UpdateSubmissionState();
        if (restoreFromStop) PromptTextBox.Focus(FocusState.Programmatic);
        else if (moveFromInput)
        {
            if (StopButton.IsEnabled) StopButton.Focus(FocusState.Programmatic);
            else if (PromptTextBox.IsEnabled) PromptTextBox.Focus(FocusState.Programmatic);
        }
    }

    internal void ApplyModelLibrary(ChatModelSelectorPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        modelOptionButtons.Clear();
        modelOptions.Clear();
        foreach (ChatModelSelectorItem model in presentation.Models)
        {
            modelOptions.Add(model);
        }
        ChatModelEmptyMessage.Visibility = modelOptions.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

        ChatCurrentModelLabel.Text = presentation.ActiveModelId is not null
            ? presentation.CurrentLabel : activeModelLabelOverride ?? presentation.CurrentLabel;
        AutomationProperties.SetName(
            ChatComposerModelPill,
            presentation.ActiveModelId is not null ? presentation.CurrentAutomationName
                : activeModelLabelOverride is { } label ? $"Current model {label}" : presentation.CurrentAutomationName);
        ApplyGeneratingState();
    }

    internal void ApplyModelSwitchPresentation(
        ChatModelSwitchPresentation presentation,
        string? modelId)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        isSwitchingModel = presentation.IsSwitching;
        requestedModelId = presentation.IsSwitching ? modelId : null;
        ChatModelSwitchProgress.IsActive = presentation.IsSwitching;
        ChatModelSwitchProgress.Visibility = presentation.IsSwitching
            ? Visibility.Visible
            : Visibility.Collapsed;
        ChatModelSwitchCancelButton.Visibility = presentation.CanCancel
            ? Visibility.Visible
            : Visibility.Collapsed;
        ChatModelSwitchCancelButton.IsEnabled = presentation.CanCancel;
        ChatModelSwitchStatus.Text = presentation.StatusMessage;
        ChatModelSwitchStatus.FontWeight = presentation.IsFailure
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;
        ChatModelSwitchFeedback.Visibility = presentation.IsSwitching
            || presentation.StatusMessage.Length > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        ApplyGeneratingState();
    }

    internal void RestoreModelSelectionFocus(string? modelId)
    {
        if (modelId is not null
            && modelOptionButtons.TryGetValue(modelId, out WeakReference<Button>? reference)
            && reference.TryGetTarget(out Button? button)
            && button.Focus(FocusState.Programmatic))
        {
            return;
        }

        ChatComposerModelPill.Focus(FocusState.Programmatic);
    }

    private void ChatModelOption_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (sender is not Button
            {
                CommandParameter: ChatModelSelectorItem { IsSelectable: true } model,
            }
            || isSwitchingModel
            || model.IsActive)
        {
            return;
        }

        requestedModelId = model.Id;
        ModelSelectionRequested?.Invoke(this, model.Id);
    }

    private void ChatModelOption_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (sender is not Button
            {
                CommandParameter: ChatModelSelectorItem model,
            } button)
        {
            return;
        }

        modelOptionButtons[model.Id] = new WeakReference<Button>(button);
        button.IsEnabled = model.IsSelectable && !isSwitchingModel && !isPreparingSession;
    }

    private void ChatModelOption_Unloaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (sender is Button
            {
                CommandParameter: ChatModelSelectorItem model,
            }
            && modelOptionButtons.TryGetValue(
                model.Id,
                out WeakReference<Button>? reference)
            && reference.TryGetTarget(out Button? registered)
            && ReferenceEquals(registered, sender))
        {
            modelOptionButtons.Remove(model.Id);
        }
    }

    private void ChatModelSwitchCancelButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (!isSwitchingModel)
        {
            return;
        }

        ChatModelSwitchCancelButton.IsEnabled = false;
        ModelSelectionCancellationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void GetMoreLocalModelsButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        ChatModelFlyout.Hide();
        GetMoreLocalModelsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ImportAnotherModelButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        ChatModelFlyout.Hide();
        ImportModelRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void AddKnowledgeFiles_Click(object sender, RoutedEventArgs eventArguments)
    {
        AttachmentButton.Flyout?.Hide();
        await AddKnowledgeFilesAsync();
    }

    internal async Task AddKnowledgeFilesAsync()
    {
        if (isPickingKnowledgeFiles || isPreparingSession)
        {
            return;
        }

        isPickingKnowledgeFiles = true;
        ApplyGeneratingState();
        try
        {
            IReadOnlyList<KnowledgeFileCandidate> selected =
                await knowledgeFilePicker.PickAsync();
            if (selected is null || selected.Count == 0)
            {
                return;
            }

            KnowledgeAttachmentValidationResult result =
                KnowledgeAttachmentPolicy.Validate(selected, attachments.ToList());
            foreach (KnowledgeAttachment attachment in result.Accepted)
            {
                attachments.Add(attachment);
            }

            RejectionSummaryText.Text = BuildSafeRejectionSummary(result.Rejections);
            RejectionSummaryText.Visibility = result.Rejections.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateAttachmentPresentation();
        }
        catch (OperationCanceledException)
        {
            // native picker cancellation is equivalent to an empty selection
        }
        catch (Exception)
        {
            RejectionSummaryText.Text =
                "Knowledge files could not be selected. Try again.";
            RejectionSummaryText.Visibility = Visibility.Visible;
            UpdateAttachmentPresentation();
        }
        finally
        {
            isPickingKnowledgeFiles = false;
            ApplyGeneratingState();
        }
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs eventArguments)
    {
        if (sender is not Button { CommandParameter: KnowledgeAttachment selected })
        {
            return;
        }

        int removedIndex = -1;
        for (int index = 0; index < attachments.Count; index++)
        {
            if (ReferenceEquals(attachments[index], selected))
            {
                removedIndex = index;
                attachments.RemoveAt(index);
                break;
            }
        }

        UpdateAttachmentPresentation();
        if (removedIndex >= 0)
        {
            RestoreAttachmentRemovalFocus(removedIndex);
        }
    }

    private void RestoreAttachmentRemovalFocus(int removedIndex)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (attachments.Count == 0)
            {
                PromptTextBox.Focus(FocusState.Programmatic);
                return;
            }

            AttachmentItems.UpdateLayout();
            int targetIndex = Math.Min(removedIndex, attachments.Count - 1);
            DependencyObject? container = AttachmentItems.ContainerFromIndex(targetIndex);
            Button? nextRemoveButton = FindDescendant<Button>(
                container,
                "RemoveAttachmentButton");
            if (nextRemoveButton is not null)
            {
                nextRemoveButton.Focus(FocusState.Programmatic);
            }
            else
            {
                PromptTextBox.Focus(FocusState.Programmatic);
            }
        });
    }

    private void AttachmentItems_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs eventArguments)
    {
        if (eventArguments.ItemContainer is ListViewItem container &&
            eventArguments.Item is KnowledgeAttachment attachment)
        {
            AutomationProperties.SetName(
                container,
                $"{attachment.FileName}. Not indexed.");
            AutomationProperties.SetItemStatus(container, "Not indexed");
        }
    }

    private static T? FindDescendant<T>(
        DependencyObject? root,
        string automationId)
        where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match && string.Equals(
                    AutomationProperties.GetAutomationId(match),
                    automationId,
                    StringComparison.Ordinal))
            {
                return match;
            }

            T? descendant = FindDescendant<T>(child, automationId);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void RemoveAttachmentButton_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (sender is Button { DataContext: KnowledgeAttachment attachment } button)
        {
            AutomationProperties.SetName(button, $"Remove {attachment.FileName}");
        }
    }

    private void UpdateAttachmentPresentation()
    {
        bool hasAttachments = attachments.Count > 0;
        bool hasRejectionSummary =
            RejectionSummaryText.Visibility == Visibility.Visible;
        AttachmentItems.Visibility = hasAttachments
            ? Visibility.Visible
            : Visibility.Collapsed;
        AttachmentPresentation.Visibility = hasAttachments || hasRejectionSummary
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string BuildSafeRejectionSummary(
        IReadOnlyList<KnowledgeAttachmentRejection> rejections)
    {
        if (rejections.Count == 0)
        {
            return string.Empty;
        }

        var counts = rejections
            .GroupBy(rejection => rejection.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var details = new List<string>();
        int knownRejectionCount = 0;
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-duplicate", "duplicate");
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-unsupported-type", "unsupported type");
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-too-large", "too large");
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-inaccessible", "inaccessible");
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-empty", "empty");
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-count-exceeded", "over the attachment limit");
        knownRejectionCount += AddRejectionCount(
            details, counts, "attachment-invalid", "invalid");

        int otherRejectionCount = rejections.Count - knownRejectionCount;
        if (otherRejectionCount > 0)
        {
            details.Add($"{otherRejectionCount} invalid");
        }

        string fileWord = rejections.Count == 1 ? "file was" : "files were";
        return $"{rejections.Count} knowledge {fileWord} not added: {string.Join(", ", details)}.";
    }

    private static int AddRejectionCount(
        ICollection<string> details,
        IReadOnlyDictionary<string, int> counts,
        string code,
        string safeDescription)
    {
        if (counts.TryGetValue(code, out int count))
        {
            details.Add($"{count} {safeDescription}");
            return count;
        }

        return 0;
    }

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs eventArguments)
    {
        UpdatePromptVerticalAlignment();
        UpdateSubmissionState();
    }

    private void PromptTextBox_SizeChanged(object sender, SizeChangedEventArgs eventArguments) =>
        UpdatePromptVerticalAlignment();

    private void PromptTextBox_GettingFocus(object sender, GettingFocusEventArgs eventArguments) =>
        ComposerFocusVisual.Visibility = Visibility.Visible;

    private void PromptTextBox_LosingFocus(object sender, LosingFocusEventArgs eventArguments) =>
        ComposerFocusVisual.Visibility = Visibility.Collapsed;

    internal static bool IsSendKey(VirtualKey key, bool isShiftPressed) =>
        key == VirtualKey.Enter && !isShiftPressed;

    private bool TryHandlePromptKeyDown(VirtualKey key, bool isShiftPressed)
    {
        if (!IsSendKey(key, isShiftPressed))
        {
            return false;
        }

        TrySubmitPrompt();
        return true;
    }

    private void PromptTextBox_PreviewKeyDown(
        object sender,
        KeyRoutedEventArgs eventArguments)
    {
        bool isShiftPressed = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (!TryHandlePromptKeyDown(eventArguments.Key, isShiftPressed))
        {
            return;
        }

        eventArguments.Handled = true;
    }

    private void UpdatePromptVerticalAlignment()
    {
        bool hasExplicitLineBreak =
            PromptTextBox.Text.Contains('\r') || PromptTextBox.Text.Contains('\n');
        bool hasGrown = PromptTextBox.ActualHeight > PromptTextBox.MinHeight + 0.5;
        VerticalAlignment desiredAlignment = hasExplicitLineBreak || hasGrown
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
        if (PromptTextBox.VerticalContentAlignment != desiredAlignment)
        {
            PromptTextBox.VerticalContentAlignment = desiredAlignment;
        }
    }

    private void UpdateSubmissionState()
    {
        if (SendButton is null || PromptTextBox is null)
        {
            return;
        }

        SendButton.IsEnabled = externalRoute ? externalSendEnabled :
            conversationReady && !IsGenerating && !isSwitchingModel && !isPreparingSession && PromptTextBox.Text.Trim().Length > 0;
    }

    private bool TrySubmitPrompt()
    {
        string prompt = externalRoute ? PromptTextBox.Text : PromptTextBox.Text.Trim();
        if ((!externalRoute && !conversationReady) || IsGenerating || isSwitchingModel || isPreparingSession || string.IsNullOrWhiteSpace(prompt) || (externalRoute && !externalSendEnabled))
        {
            return false;
        }

        PromptTextBox.Text = string.Empty;
        UpdateSubmissionState();
        SendRequested?.Invoke(this, prompt);
        return true;
    }

    private void SendButton_Click(object sender, RoutedEventArgs eventArguments) =>
        TrySubmitPrompt();

    private void StopButton_Click(object sender, RoutedEventArgs eventArguments)
    {
        if (!IsGenerating || isStopping || !StopButton.IsEnabled) return;
        SetStopping(true);
        StopRequested?.Invoke(this, EventArgs.Empty);
    }
}
