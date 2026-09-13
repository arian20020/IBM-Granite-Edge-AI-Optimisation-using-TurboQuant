using System;
using System.Runtime.InteropServices;
using GraniteEdgeAI.Features.GgufRuntime.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatMessageBubble : UserControl
{
    private const string CopyMessageLabel = "Copy message";
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer copyFeedbackTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer markdownRenderTimer;
    private string? renderedMarkdown;
    private bool isPointerOver;
    private ulong lastPrimaryClick;
    private Windows.Foundation.Point lastPrimaryPoint;
    private int primaryClickCount;
    private Paragraph? tripleClickParagraph;
    private RichTextBlock? tripleClickOwner;
    private long paragraphSelectionGeneration;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    public static readonly DependencyProperty MessageContentProperty = DependencyProperty.Register(
        nameof(MessageContent), typeof(string), typeof(ChatMessageBubble),
        new PropertyMetadata(string.Empty, OnMessageContentChanged));
    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(ChatMessageBubble),
        new PropertyMetadata(string.Empty, OnStatusTextChanged));
    public static readonly DependencyProperty IsUserProperty = DependencyProperty.Register(
        nameof(IsUser), typeof(bool), typeof(ChatMessageBubble),
        new PropertyMetadata(false, OnIsUserChanged));
    public static readonly DependencyProperty MessageIdProperty = DependencyProperty.Register(
        nameof(MessageId), typeof(Guid), typeof(ChatMessageBubble),
        new PropertyMetadata(Guid.Empty));
    public static readonly DependencyProperty CanContinueProperty = DependencyProperty.Register(
        nameof(CanContinue), typeof(bool), typeof(ChatMessageBubble),
        new PropertyMetadata(false, OnCanContinueChanged));

    public ChatMessageBubble()
    {
        InitializeComponent();
        copyFeedbackTimer = DispatcherQueue.CreateTimer();
        copyFeedbackTimer.Interval = TimeSpan.FromSeconds(1.5);
        copyFeedbackTimer.Tick += CopyFeedbackTimer_Tick;
        markdownRenderTimer = DispatcherQueue.CreateTimer();
        markdownRenderTimer.Interval = TimeSpan.FromMilliseconds(60);
        markdownRenderTimer.IsRepeating = false;
        markdownRenderTimer.Tick += (_, _) =>
        {
            if (IsLoaded) RenderMarkdown();
        };
        Loaded += (_, _) => RenderMarkdown();
        MarkdownMessageText.AddHandler(PointerPressedEvent, new PointerEventHandler(MarkdownPointerPressed), true);
        MarkdownMessageText.AddHandler(PointerReleasedEvent, new PointerEventHandler(MarkdownPointerReleased), true);
        MessageText.AddHandler(PointerPressedEvent, new PointerEventHandler(MarkdownPointerPressed), true);
        MessageText.AddHandler(PointerReleasedEvent, new PointerEventHandler(MarkdownPointerReleased), true);
        MarkdownMessageText.AddHandler(PointerMovedEvent, new PointerEventHandler(MarkdownPointerMoved), true);
        MessageText.AddHandler(PointerMovedEvent, new PointerEventHandler(MarkdownPointerMoved), true);
        AddHandler(KeyDownEvent, new KeyEventHandler((_, _) => paragraphSelectionGeneration++), true);
        Unloaded += ChatMessageBubble_Unloaded;
        ApplyRole();
        UpdateStatusVisibility();
        UpdateCopyAvailability();
    }

    public event EventHandler<string>? CopyRequested;
    public event EventHandler<Guid>? ContinueRequested;

    public string MessageContent
    {
        get => (string)GetValue(MessageContentProperty);
        set => SetValue(MessageContentProperty, value ?? string.Empty);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value ?? string.Empty);
    }

    public bool IsUser
    {
        get => (bool)GetValue(IsUserProperty);
        set => SetValue(IsUserProperty, value);
    }

    public Guid MessageId
    {
        get => (Guid)GetValue(MessageIdProperty);
        set => SetValue(MessageIdProperty, value);
    }

    public bool CanContinue
    {
        get => (bool)GetValue(CanContinueProperty);
        set => SetValue(CanContinueProperty, value);
    }

    private static void OnIsUserChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatMessageBubble)sender).ApplyRole();

    private static void OnMessageContentChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments)
    {
        var bubble = (ChatMessageBubble)sender;
        bubble.UpdateCopyAvailability();
        bubble.QueueMarkdownRender();
    }

    private static void OnCanContinueChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatMessageBubble)sender).UpdateContinuationAvailability();

    private static void OnStatusTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ChatMessageBubble)sender).UpdateStatusVisibility();

    private void UpdateStatusVisibility()
    {
        if (StatusTextBlock is not null)
            StatusTextBlock.Visibility = string.IsNullOrWhiteSpace(StatusText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyRole()
    {
        if (BubbleBorder is null)
        {
            return;
        }

        MessageContainer.HorizontalAlignment = IsUser
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        CopyMessageButton.HorizontalAlignment = IsUser
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        VisualStateManager.GoToState(this, IsUser ? "UserRole" : "AssistantRole", false);
        renderedMarkdown = null;
        QueueMarkdownRender();
    }

    private void QueueMarkdownRender()
    {
        if (markdownRenderTimer is null || !IsLoaded) return;
        // Do not restart: a continuous stream must still repaint periodically.
        if (!markdownRenderTimer.IsRunning) markdownRenderTimer.Start();
    }

    private void RenderMarkdown()
    {
        if (string.Equals(renderedMarkdown, MessageContent, StringComparison.Ordinal)) return;
        if (IsUser)
        {
            MarkdownMessageText.Blocks.Clear();
            MessageText.Blocks.Clear();
            foreach (string line in MessageContent.ReplaceLineEndings("\n").Split('\n'))
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = line });
                MessageText.Blocks.Add(paragraph);
            }
        }
        else ChatMarkdownRenderer.Render(MarkdownMessageText, MessageContent);
        renderedMarkdown = MessageContent;
    }

    private void MarkdownPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        paragraphSelectionGeneration++;
        if (sender is not RichTextBlock text) return;
        var point = args.GetCurrentPoint(text);
        if (!point.Properties.IsLeftButtonPressed) { primaryClickCount = 0; return; }
        double scale = XamlRoot?.RasterizationScale ?? 1d;
        bool nearby = Math.Abs(point.Position.X - lastPrimaryPoint.X) <= GetSystemMetrics(36) / (2d * scale)
            && Math.Abs(point.Position.Y - lastPrimaryPoint.Y) <= GetSystemMetrics(37) / (2d * scale);
        bool timely = point.Timestamp >= lastPrimaryClick
            && point.Timestamp - lastPrimaryClick <= (ulong)GetDoubleClickTime() * 1000;
        primaryClickCount = timely && nearby ? primaryClickCount + 1 : 1;
        lastPrimaryClick = point.Timestamp;
        lastPrimaryPoint = point.Position;
        tripleClickParagraph = null;
        tripleClickOwner = null;
        if (primaryClickCount != 3) return;
        primaryClickCount = 0;
        TextPointer? position = text.GetPositionFromPoint(point.Position);
        if (position is null) return;
        foreach (Block block in text.Blocks)
        {
            if (block is Paragraph paragraph && position.Offset >= paragraph.ContentStart.Offset && position.Offset <= paragraph.ContentEnd.Offset)
            {
                tripleClickParagraph = paragraph;
                tripleClickOwner = text;
                text.Select(paragraph.ContentStart, paragraph.ContentEnd);
                args.Handled = true;
                break;
            }
        }
    }

    private void MarkdownPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (tripleClickParagraph is not { } paragraph || tripleClickOwner is not { } text) return;
        tripleClickParagraph = null;
        tripleClickOwner = null;
        if (!text.Blocks.Contains(paragraph)) return;
        long generation = paragraphSelectionGeneration;
        // Native tap recognition completes after PointerReleased and can clear a synchronous selection.
        // Reapply once after that input transaction, never after newer input or a content rebuild.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (IsLoaded && generation == paragraphSelectionGeneration && text.Blocks.Contains(paragraph))
                text.Select(paragraph.ContentStart, paragraph.ContentEnd);
        });
        args.Handled = true;
    }

    private void MarkdownPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (tripleClickOwner is not { } text) return;
        var point = args.GetCurrentPoint(text);
        double scale = XamlRoot?.RasterizationScale ?? 1d;
        if (point.Properties.IsLeftButtonPressed &&
            (Math.Abs(point.Position.X - lastPrimaryPoint.X) > GetSystemMetrics(36) / (2d * scale) ||
             Math.Abs(point.Position.Y - lastPrimaryPoint.Y) > GetSystemMetrics(37) / (2d * scale)))
        {
            paragraphSelectionGeneration++;
            tripleClickParagraph = null;
            tripleClickOwner = null;
        }
    }

    internal void ShowCopyResult(bool succeeded)
    {
        copyFeedbackTimer.Stop();
        CopyMessageGlyph.Glyph = succeeded ? "\uE73E" : "\uE783";
        string label = succeeded ? "Copied" : "Couldn't copy";
        AutomationProperties.SetName(CopyMessageButton, label);
        ToolTipService.SetToolTip(CopyMessageButton, label);
        CopyMessageButton.Opacity = 1;
        copyFeedbackTimer.Start();
    }

    private void UpdateCopyAvailability()
    {
        if (CopyMessageButton is not null)
        {
            CopyMessageButton.IsEnabled = !string.IsNullOrEmpty(MessageContent);
        }
    }

    private void CopyMessageButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (!string.IsNullOrEmpty(MessageContent))
        {
            CopyRequested?.Invoke(this, MessageContent);
        }
    }

    private void ContinueButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (CanContinue && MessageId != Guid.Empty)
        {
            ContinueRequested?.Invoke(this, MessageId);
        }
    }

    private void UpdateContinuationAvailability()
    {
        if (ContinueButton is not null)
        {
            ContinueButton.Visibility = CanContinue
                ? Visibility.Visible
                : Visibility.Collapsed;
            ContinueButton.IsEnabled = CanContinue;
        }
    }

    private void MessageContainer_PointerEntered(
        object sender,
        PointerRoutedEventArgs eventArguments)
    {
        isPointerOver = true;
        CopyMessageButton.Opacity = 1;
    }

    private void MessageContainer_PointerExited(
        object sender,
        PointerRoutedEventArgs eventArguments)
    {
        isPointerOver = false;
        UpdateCopyActionOpacity();
    }

    private void CopyMessageButton_GotFocus(
        object sender,
        RoutedEventArgs eventArguments) =>
        CopyMessageButton.Opacity = 1;

    private void CopyMessageButton_LostFocus(
        object sender,
        RoutedEventArgs eventArguments) =>
        UpdateCopyActionOpacity();

    private void CopyFeedbackTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object eventArguments)
    {
        sender.Stop();
        ResetCopyFeedback();
    }

    private void ResetCopyFeedback()
    {
        CopyMessageGlyph.Glyph = "\uE8C8";
        AutomationProperties.SetName(CopyMessageButton, CopyMessageLabel);
        ToolTipService.SetToolTip(CopyMessageButton, CopyMessageLabel);
        UpdateCopyActionOpacity();
    }

    private void UpdateCopyActionOpacity()
    {
        CopyMessageButton.Opacity = isPointerOver ||
            CopyMessageButton.FocusState != FocusState.Unfocused
                ? 1
                : 0;
    }

    private void ChatMessageBubble_Unloaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        copyFeedbackTimer.Stop();
        ResetCopyFeedback();
        markdownRenderTimer.Stop();
        tripleClickParagraph = null;
        tripleClickOwner = null;
        primaryClickCount = 0;
        paragraphSelectionGeneration++;
    }
}
