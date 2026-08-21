using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.Attachments;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatComposer : UserControl
{
    private readonly IKnowledgeFilePicker knowledgeFilePicker;
    private readonly ObservableCollection<KnowledgeAttachment> attachments = new();
    private bool isPickingKnowledgeFiles;

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
        UpdateAttachmentPresentation();
        ApplyGeneratingState();
    }

    public event EventHandler<string>? SendRequested;
    public event EventHandler? StopRequested;

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
        }
    }

    private static void OnIsGeneratingChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatComposer)sender).ApplyGeneratingState();

    private void ApplyGeneratingState()
    {
        if (StopButton is null)
        {
            return;
        }

        StopButton.Visibility = IsGenerating ? Visibility.Visible : Visibility.Collapsed;
        SendButton.Visibility = IsGenerating ? Visibility.Collapsed : Visibility.Visible;
        PromptTextBox.IsEnabled = !IsGenerating;
        bool canSelectKnowledgeFiles = !IsGenerating && !isPickingKnowledgeFiles;
        AttachmentButton.IsEnabled = canSelectKnowledgeFiles;
        AddFilesFlyoutButton.IsEnabled = canSelectKnowledgeFiles;
    }

    private async void AddKnowledgeFiles_Click(object sender, RoutedEventArgs eventArguments)
    {
        AttachmentButton.Flyout?.Hide();
        await AddKnowledgeFilesAsync();
    }

    internal async Task AddKnowledgeFilesAsync()
    {
        if (isPickingKnowledgeFiles || IsGenerating)
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
            // Native picker cancellation is equivalent to an empty selection.
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

        for (int index = 0; index < attachments.Count; index++)
        {
            if (ReferenceEquals(attachments[index], selected))
            {
                attachments.RemoveAt(index);
                break;
            }
        }

        UpdateAttachmentPresentation();
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

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs eventArguments) =>
        UpdatePromptVerticalAlignment();

    private void PromptTextBox_SizeChanged(object sender, SizeChangedEventArgs eventArguments) =>
        UpdatePromptVerticalAlignment();

    private void PromptTextBox_GettingFocus(object sender, GettingFocusEventArgs eventArguments) =>
        ComposerFocusVisual.Visibility = Visibility.Visible;

    private void PromptTextBox_LosingFocus(object sender, LosingFocusEventArgs eventArguments) =>
        ComposerFocusVisual.Visibility = Visibility.Collapsed;

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

    private void SendButton_Click(object sender, RoutedEventArgs eventArguments)
    {
        string prompt = PromptTextBox.Text.Trim();
        if (prompt.Length == 0)
        {
            return;
        }

        PromptTextBox.Text = string.Empty;
        SendRequested?.Invoke(this, prompt);
    }

    private void StopButton_Click(object sender, RoutedEventArgs eventArguments) =>
        StopRequested?.Invoke(this, EventArgs.Empty);
}
